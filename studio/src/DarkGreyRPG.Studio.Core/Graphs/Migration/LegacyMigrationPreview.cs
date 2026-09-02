using System.Globalization;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Dialogues;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Quests;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Graphs.Migration;

/// <summary>Stable source labels exposed by the read-only migration boundary.</summary>
public enum LegacyMigrationSourceKind
{
    Story,
    Dialogue,
    Quest,
}

/// <summary>
/// A detached, read-only migration decision.  An envelope is present only if
/// every source and conversion check succeeded; callers may therefore use
/// <see cref="CanApply"/> as the later transaction-layer gate.
/// </summary>
public sealed class LegacyMigrationPreviewResult
{
    private readonly string? _envelopeJson;

    internal LegacyMigrationPreviewResult(
        LegacyMigrationSourceKind sourceKind,
        string sourceId,
        GraphResourceEnvelope? envelope,
        IEnumerable<ValidationIssue>? issues)
    {
        SourceKind = sourceKind;
        SourceKindName = sourceKind switch
        {
            LegacyMigrationSourceKind.Story => "story",
            LegacyMigrationSourceKind.Dialogue => "dialogue",
            _ => "quest",
        };
        SourceId = sourceId ?? string.Empty;
        _envelopeJson = envelope is null ? null : GraphResourceEnvelopeSerializer.Serialize(envelope);
        Issues = Array.AsReadOnly((issues ?? []).ToArray());
    }

    public LegacyMigrationSourceKind SourceKind { get; }
    public string SourceKindName { get; }
    public string Kind => SourceKindName;
    public string SourceId { get; }
    public string Id => SourceId;
    public GraphResourceEnvelope? Envelope => _envelopeJson is null
        ? null
        : GraphResourceEnvelopeSerializer.Deserialize(_envelopeJson);
    public GraphResourceEnvelope? CanonicalEnvelope => Envelope;
    public IReadOnlyList<ValidationIssue> Issues { get; }
    public IReadOnlyList<ValidationIssue> ValidationIssues => Issues;
    public bool HasErrors => Issues.Any(issue => issue.Severity == ValidationSeverity.Error);
    public bool CanApply => Envelope is not null && !HasErrors;
    public bool IsSuccess => CanApply;
    public bool Succeeded => CanApply;
    public bool Success => CanApply;
}

/// <summary>
/// Pure in-memory conversion of Studio 2.1.3 Story, Dialogue, and Quest resources.
/// The input is inspected only; no source collection is ever edited.
/// </summary>
public static class CanonicalLegacyMigrationPreview
{
    public static LegacyMigrationPreviewResult Preview(StoryResource source,
        IEnumerable<GraphResourceEnvelope>? canonicalChildren = null)
        => StoryFlowMigrationPreview.PreviewStory(source, canonicalChildren);

    public static LegacyMigrationPreviewResult Preview(DialogueResource source)
        => PreviewDialogue(source);

    public static LegacyMigrationPreviewResult Preview(QuestResource source)
        => PreviewQuest(source);

    public static LegacyMigrationPreviewResult PreviewDialogue(DialogueResource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var issues = ValidateDialogueSource(source).ToList();
        if (issues.Count != 0)
            return Failure(LegacyMigrationSourceKind.Dialogue, source.Id, issues);

        var sourceNodes = source.Nodes!;
        var usedIds = sourceNodes.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        var startId = SyntheticId("start", usedIds);
        var graphNodes = new List<GraphNode>();
        var graphConnections = new List<GraphConnection>();
        var byId = sourceNodes.ToDictionary(node => node.Id, StringComparer.Ordinal);

        var start = GraphNodeFactory.Create(GraphScope.Session, "start", startId, "起始");
        graphNodes.Add(start);

        var endResults = new HashSet<string>(StringComparer.Ordinal);
        foreach (var sourceNode in sourceNodes)
        {
            var type = sourceNode.Type.ToLowerInvariant();
            GraphNode target;
            switch (type)
            {
                case "line":
                    target = GraphNodeFactory.Create(GraphScope.Session, "line", sourceNode.Id, sourceNode.Id);
                    target.Properties["speaker_actor_id"] = JsonSerializer.SerializeToElement(sourceNode.Speaker!);
                    target.Properties["text"] = JsonSerializer.SerializeToElement(sourceNode.Text!);
                    break;
                case "choice":
                    target = CreateChoice(sourceNode);
                    break;
                case "end":
                    var result = sourceNode.Result!;
                    if (!endResults.Add(result))
                        issues.Add(Issue("migration.dialogue.end.result.duplicate",
                            $"Dialogue end result '{result}' is duplicated.", "result", sourceNode.Id));
                    if (result is "flow_in" or "logic_in")
                        issues.Add(Issue("migration.dialogue.end.result.reserved",
                            $"Dialogue end result '{result}' uses a reserved canonical port ID.", "result", sourceNode.Id));
                    target = GraphNodeFactory.Create(GraphScope.Session, "end", sourceNode.Id, sourceNode.Id);
                    target.Properties["port_id"] = JsonSerializer.SerializeToElement(result);
                    target.Properties["display_name"] = JsonSerializer.SerializeToElement(result);
                    break;
                case "jump":
                    // The compatibility node retains the old identity and
                    // target for inspection.  Edges bypass it, which is the
                    // equivalent direct conversion supported by the contract.
                    target = GraphNodeFactory.Create(GraphScope.Session, "legacy_jump", sourceNode.Id, sourceNode.Id, compatibilityMode: true);
                    target.Properties["target"] = JsonSerializer.SerializeToElement(sourceNode.Target!);
                    break;
                default:
                    continue;
            }
            graphNodes.Add(target);
        }

        AddDialogueEdge(startId, "flow_out", source.Entry, byId, graphNodes, graphConnections, issues);
        foreach (var sourceNode in sourceNodes)
        {
            var type = sourceNode.Type.ToLowerInvariant();
            if (type == "line" && sourceNode.Next is not null)
                AddDialogueEdge(sourceNode.Id, "flow_out", sourceNode.Next, byId, graphNodes, graphConnections, issues);
            else if (type == "choice")
            {
                var choice = graphNodes.Single(node => node.Id == sourceNode.Id);
                var options = sourceNode.Choices!;
                for (var index = 0; index < options.Count; index++)
                {
                    var flowPort = ChoiceFlowPortId(sourceNode.Id, index);
                    AddDialogueEdge(sourceNode.Id, flowPort, options[index].Next, byId, graphNodes, graphConnections, issues);
                }
            }
        }

        if (issues.Count != 0)
            return Failure(LegacyMigrationSourceKind.Dialogue, source.Id, issues);

        var graph = new GraphDocument(graphNodes, graphConnections);
        issues.AddRange(GraphScopePolicy.Validate(graph, GraphScope.Session, compatibilityMode: true));
        issues.AddRange(GraphNodeShapeValidator.Validate(graph, GraphScope.Session, compatibilityMode: true));
        if (issues.Count != 0)
            return Failure(LegacyMigrationSourceKind.Dialogue, source.Id, issues);

        var envelope = new GraphResourceEnvelope(GraphResourceKind.Session, source.Id,
            DisplayName(source.Title, source.DisplayName), graph);
        return Success(LegacyMigrationSourceKind.Dialogue, source.Id, envelope);
    }

    public static LegacyMigrationPreviewResult PreviewQuest(QuestResource source)
    {
        ArgumentNullException.ThrowIfNull(source);
        var issues = ValidateQuestSource(source).ToList();
        if (issues.Count != 0)
            return Failure(LegacyMigrationSourceKind.Quest, source.Id, issues);

        if (source.ObjectiveGroups.Count != 1)
        {
            issues.Add(Issue("migration.quest.groups.ambiguous",
                "Quest migration requires exactly one objective group.", "objective_groups"));
            return Failure(LegacyMigrationSourceKind.Quest, source.Id, issues);
        }

        var group = source.ObjectiveGroups[0];
        var objectiveById = source.Objectives.ToDictionary(objective => objective.Id, StringComparer.Ordinal);
        var usedIds = source.Objectives.Select(objective => objective.Id).ToHashSet(StringComparer.Ordinal);
        var settleId = SyntheticId("settle", usedIds);
        var graphNodes = new List<GraphNode>();
        var graphConnections = new List<GraphConnection>();

        var settle = GraphNodeFactory.Create(GraphScope.Task, "settle", settleId, "结算");
        const string completionPortId = "completion";
        settle.Ports.Add(new GraphPort(completionPortId, "完成", true, GraphInterfaceKind.Logic, 0));
        graphNodes.Add(settle);

        foreach (var objective in source.Objectives)
        {
            if (objective.Type == "reach_location")
            {
                issues.Add(Issue("migration.quest.objective.type.unsupported",
                    "reach_location has no canonical Task Objective equivalent.", "objectives", objective.Id));
                continue;
            }
            var node = GraphNodeFactory.Create(GraphScope.Task, "objective", objective.Id, objective.Description);
            node.Properties.Clear();
            node.Properties[CanonicalTaskObjectiveSchema.TypeProperty] = JsonSerializer.SerializeToElement(objective.Type);
            node.Properties[CanonicalTaskObjectiveSchema.DescriptionProperty] = JsonSerializer.SerializeToElement(objective.Description);
            node.Properties[CanonicalTaskObjectiveSchema.PrerequisiteEnabledProperty] = JsonSerializer.SerializeToElement(false);
            node.Properties[CanonicalTaskObjectiveSchema.RequiredProperty] = JsonSerializer.SerializeToElement(objective.Required!.Value);
            switch (objective.Type)
            {
                case CanonicalTaskObjectiveSchema.KillEntity:
                    node.Properties[CanonicalTaskObjectiveSchema.EntityProperty] = JsonSerializer.SerializeToElement(objective.Entity!);
                    break;
                case CanonicalTaskObjectiveSchema.CollectItem:
                    node.Properties[CanonicalTaskObjectiveSchema.ItemProperty] = JsonSerializer.SerializeToElement(objective.Item!);
                    node.Properties[CanonicalTaskObjectiveSchema.MetadataProperty] = JsonSerializer.SerializeToElement(
                        new Dictionary<string, string>(StringComparer.Ordinal)
                        {
                            ["damage"] = objective.ItemMetadata!.Value.ToString(CultureInfo.InvariantCulture),
                        });
                    break;
                case CanonicalTaskObjectiveSchema.InteractActor:
                    node.Properties[CanonicalTaskObjectiveSchema.ActorIdProperty] = JsonSerializer.SerializeToElement(objective.ActorId!);
                    break;
            }
            if (group.Mode == "SEQUENCE" && !string.Equals(objective.Id, group.Objectives[0], StringComparison.Ordinal))
            {
                node.Properties[CanonicalTaskObjectiveSchema.PrerequisiteEnabledProperty] = JsonSerializer.SerializeToElement(true);
                node.Ports.Add(new GraphPort(CanonicalTaskObjectiveSchema.PrerequisitePortId,
                    CanonicalTaskObjectiveSchema.PrerequisiteDisplayName, true, GraphInterfaceKind.Logic, 0));
            }
            graphNodes.Add(node);
        }

        if (issues.Count != 0)
            return Failure(LegacyMigrationSourceKind.Quest, source.Id, issues);

        var orderedIds = group.Objectives;
        if (orderedIds.Count == 1)
        {
            Connect(graphConnections, orderedIds[0], "logic_status", settleId, completionPortId);
        }
        else if (group.Mode is "ALL" or "ANY")
        {
            var combineId = SyntheticId(group.Mode == "ALL" ? "and" : "or", usedIds);
            var combine = GraphNodeFactory.Create(GraphScope.Task, group.Mode == "ALL" ? "and" : "or", combineId, group.Mode == "ALL" ? "与" : "或");
            for (var index = 0; index < orderedIds.Count; index++)
                combine.Ports.Add(new GraphPort($"dynamic_port_{index}", $"输入 {index + 1}", true, GraphInterfaceKind.Logic, index));
            graphNodes.Add(combine);
            for (var index = 0; index < orderedIds.Count; index++)
            {
                var objectiveId = orderedIds[index];
                Connect(graphConnections, objectiveId, "logic_status", combineId, $"dynamic_port_{index}");
            }
            Connect(graphConnections, combineId, "logic_out", settleId, completionPortId);
        }
        else // SEQUENCE
        {
            for (var index = 0; index < orderedIds.Count - 1; index++)
                Connect(graphConnections, orderedIds[index], "logic_status", orderedIds[index + 1],
                    CanonicalTaskObjectiveSchema.PrerequisitePortId);
            Connect(graphConnections, orderedIds[^1], "logic_status", settleId, completionPortId);
        }

        var graph = new GraphDocument(graphNodes, graphConnections);
        issues.AddRange(GraphScopePolicy.Validate(graph, GraphScope.Task));
        issues.AddRange(GraphNodeShapeValidator.Validate(graph, GraphScope.Task));
        if (issues.Count != 0)
            return Failure(LegacyMigrationSourceKind.Quest, source.Id, issues);

        var envelope = new GraphResourceEnvelope(GraphResourceKind.Task, source.Id,
            DisplayName(source.Title, source.DisplayName), graph);
        return Success(LegacyMigrationSourceKind.Quest, source.Id, envelope);
    }

    public static LegacyMigrationPreviewResult PreviewDialogueResource(DialogueResource source)
        => PreviewDialogue(source);

    public static LegacyMigrationPreviewResult PreviewQuestResource(QuestResource source)
        => PreviewQuest(source);

    public static LegacyMigrationPreviewResult PreviewStory(StoryResource source,
        IEnumerable<GraphResourceEnvelope>? canonicalChildren = null)
        => StoryFlowMigrationPreview.PreviewStory(source, canonicalChildren);

    public static LegacyMigrationPreviewResult PreviewStory(StoryResource source,
        IReadOnlyDictionary<string, GraphResourceEnvelope> canonicalChildren)
        => StoryFlowMigrationPreview.PreviewStory(source, canonicalChildren);

    public static LegacyMigrationPreviewResult PreviewStory(StoryResource source,
        IReadOnlyDictionary<string, GraphResourceEnvelope>? sessions,
        IReadOnlyDictionary<string, GraphResourceEnvelope>? tasks)
        => StoryFlowMigrationPreview.PreviewStory(source, sessions, tasks);

    private static GraphNode CreateChoice(DialogueNodeResource source)
    {
        var node = GraphNodeFactory.Create(GraphScope.Session, "choice", source.Id, source.Id);
        var options = source.Choices!;
        node.Properties[SessionChoiceSchema.PromptProperty] = JsonSerializer.SerializeToElement(source.Prompt!);
        node.Properties[SessionChoiceSchema.OptionsProperty] = JsonSerializer.SerializeToElement(options.Select((option, index) =>
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["option_id"] = ChoiceOptionId(source.Id, index),
                ["display_text"] = option.Text,
                ["flow_port_id"] = ChoiceFlowPortId(source.Id, index),
            }).ToArray());
        for (var index = 0; index < options.Count; index++)
            node.Ports.Add(new(ChoiceFlowPortId(source.Id, index), options[index].Text, false, GraphInterfaceKind.Flow, index));
        return node;
    }

    private static void AddDialogueEdge(string fromNodeId, string fromPortId, string targetId,
        IReadOnlyDictionary<string, DialogueNodeResource> byId, IReadOnlyList<GraphNode> nodes,
        ICollection<GraphConnection> connections, ICollection<ValidationIssue> issues)
    {
        var resolved = ResolveTarget(targetId, byId, issues);
        if (resolved is null) return;
        var target = nodes.SingleOrDefault(node => node.Id == resolved.Id);
        if (target is null || target.Type == "legacy_jump")
        {
            issues.Add(Issue("migration.dialogue.target.unconvertible",
                $"Dialogue target '{targetId}' could not be converted to a canonical node.", "target", fromNodeId));
            return;
        }
        connections.Add(new GraphConnection(fromNodeId, fromPortId, target.Id, "flow_in", GraphInterfaceKind.Flow));
    }

    private static DialogueNodeResource? ResolveTarget(string targetId,
        IReadOnlyDictionary<string, DialogueNodeResource> byId, ICollection<ValidationIssue> issues)
    {
        var visited = new HashSet<string>(StringComparer.Ordinal);
        var current = targetId;
        while (byId.TryGetValue(current, out var node) && node.Type.Equals("jump", StringComparison.OrdinalIgnoreCase))
        {
            if (!visited.Add(current))
            {
                issues.Add(Issue("migration.dialogue.jump.cycle",
                    $"Dialogue Jump chain beginning at '{targetId}' contains a cycle.", "target", current));
                return null;
            }
            current = node.Target!;
        }
        return byId.TryGetValue(current, out var resolved) ? resolved : null;
    }

    private static IReadOnlyList<ValidationIssue> ValidateDialogueSource(DialogueResource source)
    {
        try { return DialogueValidator.Validate(source, ActorIdPolicy.ExistingResource).ToArray(); }
        catch (Exception exception) { return [Issue("migration.dialogue.source.invalid", exception.Message, "source")]; }
    }

    private static IReadOnlyList<ValidationIssue> ValidateQuestSource(QuestResource source)
    {
        try { return QuestValidator.Validate(source, ActorIdPolicy.ExistingResource).ToArray(); }
        catch (Exception exception) { return [Issue("migration.quest.source.invalid", exception.Message, "source")]; }
    }

    private static string SyntheticId(string preferred, IEnumerable<string> occupied)
    {
        var used = occupied.ToHashSet(StringComparer.Ordinal);
        if (!used.Contains(preferred)) return preferred;
        var candidate = $"{preferred}_migration";
        var suffix = 2;
        while (used.Contains(candidate)) candidate = $"{preferred}_migration_{suffix++}";
        return candidate;
    }

    private static string ChoiceOptionId(string nodeId, int index) => $"{nodeId}_option_{index + 1}";
    private static string ChoiceFlowPortId(string nodeId, int index) => $"{nodeId}_flow_{index + 1}";
    private static string DisplayName(string title, string displayName)
        => string.IsNullOrWhiteSpace(displayName) ? title : displayName;

    private static void Connect(ICollection<GraphConnection> connections,
        string fromNode, string fromPort, string toNode, string toPort)
        => connections.Add(new GraphConnection(fromNode, fromPort, toNode, toPort, GraphInterfaceKind.Logic));

    private static ValidationIssue Issue(string code, string message, string field, string? nodeId = null)
        => new(code, message, field, NodeId: string.IsNullOrWhiteSpace(nodeId) ? null : nodeId);

    private static LegacyMigrationPreviewResult Success(LegacyMigrationSourceKind kind, string id, GraphResourceEnvelope envelope)
        => new(kind, id, envelope, []);

    private static LegacyMigrationPreviewResult Failure(LegacyMigrationSourceKind kind, string id, IEnumerable<ValidationIssue> issues)
        => new(kind, id, null, issues);
}

/// <summary>Instance facade convenient for dependency injection and future UI seams.</summary>
public sealed class CanonicalLegacyMigrationPreviewService
{
    public LegacyMigrationPreviewResult Preview(StoryResource source, IEnumerable<GraphResourceEnvelope>? canonicalChildren = null) => CanonicalLegacyMigrationPreview.PreviewStory(source, canonicalChildren);
    public LegacyMigrationPreviewResult Preview(DialogueResource source) => CanonicalLegacyMigrationPreview.PreviewDialogue(source);
    public LegacyMigrationPreviewResult Preview(QuestResource source) => CanonicalLegacyMigrationPreview.PreviewQuest(source);
    public LegacyMigrationPreviewResult PreviewDialogue(DialogueResource source) => CanonicalLegacyMigrationPreview.PreviewDialogue(source);
    public LegacyMigrationPreviewResult PreviewQuest(QuestResource source) => CanonicalLegacyMigrationPreview.PreviewQuest(source);
    public LegacyMigrationPreviewResult PreviewStory(StoryResource source, IEnumerable<GraphResourceEnvelope>? canonicalChildren = null) => CanonicalLegacyMigrationPreview.PreviewStory(source, canonicalChildren);
    public LegacyMigrationPreviewResult PreviewStory(StoryResource source, IReadOnlyDictionary<string, GraphResourceEnvelope> canonicalChildren) => CanonicalLegacyMigrationPreview.PreviewStory(source, canonicalChildren);
}

/// <summary>Short compatibility facade for callers that omit "Canonical".</summary>
public static class LegacyMigrationPreview
{
    public static LegacyMigrationPreviewResult Preview(StoryResource source, IEnumerable<GraphResourceEnvelope>? canonicalChildren = null) => CanonicalLegacyMigrationPreview.PreviewStory(source, canonicalChildren);
    public static LegacyMigrationPreviewResult Preview(DialogueResource source) => CanonicalLegacyMigrationPreview.PreviewDialogue(source);
    public static LegacyMigrationPreviewResult Preview(QuestResource source) => CanonicalLegacyMigrationPreview.PreviewQuest(source);
    public static LegacyMigrationPreviewResult PreviewDialogue(DialogueResource source) => CanonicalLegacyMigrationPreview.PreviewDialogue(source);
    public static LegacyMigrationPreviewResult PreviewQuest(QuestResource source) => CanonicalLegacyMigrationPreview.PreviewQuest(source);
    public static LegacyMigrationPreviewResult PreviewStory(StoryResource source, IEnumerable<GraphResourceEnvelope>? canonicalChildren = null) => CanonicalLegacyMigrationPreview.PreviewStory(source, canonicalChildren);
}
