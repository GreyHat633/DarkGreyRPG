using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.Core.Stories.Definitions;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Graphs.Migration;

/// <summary>
/// Read-only preview of the frozen, deliberately small legacy Story Flow
/// migration set.  This class never writes a project or changes any input
/// resource.  A candidate envelope is returned only when every pattern is
/// unambiguous and all referenced canonical child resources are available.
/// </summary>
public static class StoryFlowMigrationPreview
{
    public static LegacyMigrationPreviewResult Preview(
        StoryResource source,
        IEnumerable<GraphResourceEnvelope>? canonicalChildren = null)
        => PreviewStory(source, canonicalChildren);

    public static LegacyMigrationPreviewResult PreviewStory(
        StoryResource source,
        IEnumerable<GraphResourceEnvelope>? canonicalChildren = null)
        => PreviewStoryCore(source, ChildResourcesFrom(canonicalChildren));

    public static LegacyMigrationPreviewResult PreviewStory(
        StoryResource source,
        IReadOnlyDictionary<string, GraphResourceEnvelope> canonicalChildren)
        => PreviewStoryCore(source, ChildResourcesFrom(canonicalChildren?.Values));

    private static LegacyMigrationPreviewResult PreviewStoryCore(
        StoryResource source,
        IReadOnlyList<GraphResourceEnvelope> canonicalChildren)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(canonicalChildren);

        var issues = new List<ValidationIssue>();
        issues.AddRange(StoryValidator.Validate(source));
        if (string.IsNullOrWhiteSpace(source.DisplayName) && string.IsNullOrWhiteSpace(source.Title))
            issues.Add(Issue("migration.story.display_name.required", "Story display name is required.", "display_name"));
        if (issues.Count != 0)
            return Failure(source, issues);

        var nodes = source.Nodes!;
        var byId = nodes.ToDictionary(node => node.Id, StringComparer.Ordinal);
        var edges = source.Connections ?? [];
        var start = byId[source.Entry];
        var startType = StoryNodeDefinitionRegistry.CanonicalizeType(start.Type);
        if (startType is "End" or "EndStory" && nodes.Count == 1 && edges.Count == 0)
            return PreviewTerminalOnlyStory(source, start);
        if (!string.Equals(startType, "StoryStart", StringComparison.Ordinal))
        {
            issues.Add(Issue("migration.story.entry.start.required", "Story entry must be a StoryStart node.", "entry", start.Id));
            return Failure(source, issues);
        }
        var startEdges = Outgoing(edges, start.Id);
        if (startEdges.Count != 1 || !string.Equals(startEdges[0].Output, "next", StringComparison.Ordinal))
        {
            issues.Add(Issue("migration.story.start.topology.ambiguous",
                "StoryStart must have exactly one next edge in the safe migration pattern.", "next", start.Id));
            return Failure(source, issues);
        }

        var replacement = new Dictionary<string, GraphNode>(StringComparer.Ordinal);
        var consumed = new HashSet<string>(StringComparer.Ordinal);
        var aggregateEdges = new List<GraphConnection>();
        var aggregateOutputNames = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        // Recognize pair patterns before creating ordinary nodes.  This makes
        // the topology check independent of source collection order.
        foreach (var node in nodes)
        {
            var type = StoryNodeDefinitionRegistry.CanonicalizeType(node.Type);
            if (type == "PlayDialogue")
                TryCreateDialogueAggregate(node, byId, edges, canonicalChildren, replacement, consumed,
                    aggregateEdges, aggregateOutputNames, issues);
            else if (type == "StartQuest")
                TryCreateQuestAggregate(node, byId, edges, canonicalChildren, replacement, consumed,
                    aggregateEdges, issues);
        }

        foreach (var node in nodes)
        {
            if (consumed.Contains(node.Id)) continue;
            var type = StoryNodeDefinitionRegistry.CanonicalizeType(node.Type);
            if (type is null)
            {
                issues.Add(Issue("migration.story.node.type.unsupported",
                    $"Story node '{node.Id}' has unsupported type '{node.Type}'.", "type", node.Id));
                continue;
            }

            if (type is "ActorInteract" or "EnterRegion")
            {
                var outgoing = Outgoing(edges, node.Id);
                if (outgoing.Count != 1 || !string.Equals(outgoing[0].Output, "next", StringComparison.Ordinal))
                {
                    issues.Add(Issue("migration.story.topology.mid_flow_trigger",
                        $"Mid-flow {type} must have exactly one next edge.", "next", node.Id));
                }
            }
            if (type is "DialogueExitBranch" or "WaitQuestComplete")
            {
                issues.Add(Issue("migration.story.topology.unpaired",
                    $"{type} must be immediately preceded by its supported aggregate pair.", "type", node.Id));
                continue;
            }
            if (type is "Branch" or "QuestState" or "HasItem" or "VariableCompare" or "Sequence"
                or "CompleteQuest" or "GiveXp" or "SetVariable")
            {
                issues.Add(Issue("migration.story.node.type.unsupported",
                    $"Story node type '{type}' is outside the frozen migration pattern set.", "type", node.Id));
                continue;
            }

            try
            {
                var converted = ConvertNode(node, type, issues);
                if (converted is not null) replacement[node.Id] = converted;
            }
            catch (Exception exception) when (exception is ArgumentException or InvalidOperationException)
            {
                issues.Add(Issue("migration.story.node.convert.invalid", exception.Message, "node", node.Id));
            }
        }

        if (issues.Count != 0)
            return Failure(source, issues);

        var graphNodes = nodes.Where(node => !consumed.Contains(node.Id))
            .Select(node => replacement[node.Id]).ToList();
        var graphConnections = new List<GraphConnection>();
        graphConnections.AddRange(aggregateEdges);

        var startPort = replacement[start.Id].Ports.Single(port => port.IsOutput && port.InterfaceKind == GraphInterfaceKind.Flow).Id;
        foreach (var edge in edges)
        {
            if (IsInternalPairEdge(edge, consumed, replacement, aggregateOutputNames)) continue;
            if (aggregateOutputNames.TryGetValue(edge.From, out var outputs)
                && outputs.Contains(edge.Output, StringComparer.Ordinal))
                continue; // Branch/Wait edges were emitted with the aggregate.
            if (!replacement.TryGetValue(edge.From, out var from)
                || !replacement.TryGetValue(edge.To, out var to))
                continue;

            var fromPort = from == replacement[start.Id]
                ? startPort
                : from.Type is "action" ? "flow_out" : "flow_out";
            if (from == replacement[start.Id] && !string.Equals(edge.Output, "next", StringComparison.Ordinal))
            {
                issues.Add(Issue("migration.story.start.output.unsupported",
                    $"StoryStart output '{edge.Output}' is not in the safe migration pattern.", "output", edge.From));
                continue;
            }
            if (from.Type is "terminate" or "enter_story")
            {
                issues.Add(Issue("migration.story.topology.terminal_source",
                    $"Terminal node '{edge.From}' cannot have an outgoing edge.", "output", edge.From));
                continue;
            }
            graphConnections.Add(new GraphConnection(from.Id, fromPort, to.Id, "flow_in", GraphInterfaceKind.Flow));
        }

        if (issues.Count != 0)
            return Failure(source, issues);

        var graph = new GraphDocument(graphNodes, graphConnections);
        issues.AddRange(GraphScopePolicy.Validate(graph, GraphScope.StoryFlow));
        issues.AddRange(GraphNodeShapeValidator.Validate(graph, GraphScope.StoryFlow));
        if (issues.Count != 0)
            return Failure(source, issues);

        var envelope = new GraphResourceEnvelope(GraphResourceKind.Story, source.Id,
            string.IsNullOrWhiteSpace(source.DisplayName) ? source.Title : source.DisplayName, graph);
        return new LegacyMigrationPreviewResult(LegacyMigrationSourceKind.Story, source.Id, envelope, []);
    }

    public static LegacyMigrationPreviewResult PreviewStory(
        StoryResource source,
        IReadOnlyDictionary<string, GraphResourceEnvelope>? sessions,
        IReadOnlyDictionary<string, GraphResourceEnvelope>? tasks)
    {
        var children = (sessions?.Values ?? [])
            .Concat(tasks?.Values ?? [])
            .ToArray();
        return PreviewStory(source, children);
    }

    private static LegacyMigrationPreviewResult PreviewTerminalOnlyStory(
        StoryResource source,
        StoryNodeResource legacyTerminal)
    {
        var startId = SyntheticNodeId("start", source.Nodes.Select(node => node.Id));
        var start = GraphNodeFactory.Create(GraphScope.StoryFlow, "start", startId, startId);
        StoryStartSchema.InitializeDefault(start, SyntheticPortId(startId));
        var terminal = GraphNodeFactory.Create(GraphScope.StoryFlow, "terminate", legacyTerminal.Id, legacyTerminal.Id);
        var startPort = start.Ports.Single(port => port.IsOutput && port.InterfaceKind == GraphInterfaceKind.Flow).Id;
        var graph = new GraphDocument(
            [start, terminal],
            [new GraphConnection(start.Id, startPort, terminal.Id, "flow_in", GraphInterfaceKind.Flow)]);
        var issues = GraphScopePolicy.Validate(graph, GraphScope.StoryFlow)
            .Concat(GraphNodeShapeValidator.Validate(graph, GraphScope.StoryFlow))
            .ToArray();
        if (issues.Length != 0) return Failure(source, issues);
        return new LegacyMigrationPreviewResult(
            LegacyMigrationSourceKind.Story,
            source.Id,
            new GraphResourceEnvelope(GraphResourceKind.Story, source.Id,
                string.IsNullOrWhiteSpace(source.DisplayName) ? source.Title : source.DisplayName, graph),
            []);
    }

    private static GraphNode? ConvertNode(
        StoryNodeResource node,
        string type,
        ICollection<ValidationIssue> issues)
    {
        switch (type)
        {
            case "StoryStart":
                var start = GraphNodeFactory.Create(GraphScope.StoryFlow, "start", node.Id, node.Id);
                StoryStartSchema.InitializeDefault(start, SyntheticPortId(node.Id));
                return start;
            case "End":
            case "EndStory":
                return GraphNodeFactory.Create(GraphScope.StoryFlow, "terminate", node.Id, node.Id);
            case "EnterStory":
                var enter = GraphNodeFactory.Create(GraphScope.StoryFlow, "enter_story", node.Id, node.Id);
                if (!TryRead(node, ["target_story_id", "story_id", "story", "target"], out var target))
                    issues.Add(Issue("migration.story.enter_story.target.required", "EnterStory target is required.", "target_story_id", node.Id));
                else enter.Properties["target_story_id"] = target;
                return enter;
            case "GiveItem":
                var item = GraphNodeFactory.Create(GraphScope.StoryFlow, "action", node.Id, node.Id);
                item.Properties.Clear();
                item.Properties[CanonicalStoryActionSchema.TypeProperty] = JsonSerializer.SerializeToElement(CanonicalStoryActionSchema.GiveItem);
                CopyRequired(node, item, "item", ["item", "item_id", "itemId"], issues);
                CopyRequired(node, item, "metadata", ["metadata", "damage"], issues);
                CopyRequired(node, item, "amount", ["amount", "count"], issues);
                return item;
            case "SendMessage":
                var message = GraphNodeFactory.Create(GraphScope.StoryFlow, "action", node.Id, node.Id);
                message.Properties.Clear();
                message.Properties[CanonicalStoryActionSchema.TypeProperty] = JsonSerializer.SerializeToElement(CanonicalStoryActionSchema.SendMessage);
                CopyRequired(node, message, "message", ["message", "text"], issues);
                return message;
            case "ActorInteract":
                var interact = GraphNodeFactory.Create(GraphScope.StoryFlow, StoryStartSchema.ActorInteraction, node.Id, node.Id);
                CopyEventProperties(node, interact, [StoryStartSchema.ActorIdProperty, "actor", "actorId"], issues,
                    (name, value) => name == StoryStartSchema.ActorIdProperty
                        && value.ValueKind == JsonValueKind.String
                        && !string.IsNullOrWhiteSpace(value.GetString()));
                return interact;
            case "EnterRegion":
                var region = GraphNodeFactory.Create(GraphScope.StoryFlow, StoryStartSchema.RegionEntry, node.Id, node.Id);
                CopyEventProperties(node, region,
                    [StoryStartSchema.DimensionProperty, StoryStartSchema.XProperty, StoryStartSchema.YProperty,
                        StoryStartSchema.ZProperty, StoryStartSchema.RadiusProperty], issues,
                    (name, value) => value.ValueKind == JsonValueKind.Number
                        && value.TryGetDouble(out var number)
                        && double.IsFinite(number)
                        && (name != StoryStartSchema.DimensionProperty || value.TryGetInt32(out _))
                        && (name != StoryStartSchema.RadiusProperty || number > 0));
                return region;
            default:
                return null;
        }
    }

    /// <summary>
    /// Copies a legacy event payload without coercion.  Aliases are accepted
    /// for the actor reference because they are part of the legacy Story
    /// vocabulary, but conflicting aliases and unknown fields fail closed.
    /// </summary>
    private static void CopyEventProperties(
        StoryNodeResource source,
        GraphNode destination,
        IReadOnlyList<string> allowedNames,
        ICollection<ValidationIssue> issues,
        Func<string, JsonElement, bool> isValid)
    {
        var properties = source.Properties ?? [];
        foreach (var name in properties.Keys.Where(key => !allowedNames.Contains(key, StringComparer.Ordinal)))
            issues.Add(Issue("migration.story.node.property.unsupported",
                $"Property '{name}' is not supported on {source.Type}.", $"properties.{name}", source.Id));

        // The first name in each group is the canonical persisted key.  The
        // region payload has no aliases, while actor interaction supports the
        // historical actor/actorId spellings.
        var groups = source.Type is not null
            && StoryNodeDefinitionRegistry.CanonicalizeType(source.Type) == "ActorInteract"
            ? new[] { new[] { StoryStartSchema.ActorIdProperty, "actor", "actorId" } }
            : allowedNames.Select(name => new[] { name }).ToArray();
        foreach (var group in groups)
        {
            var matches = properties.Where(pair => group.Contains(pair.Key, StringComparer.Ordinal)).ToArray();
            if (matches.Length == 0)
            {
                issues.Add(Issue("migration.story.node.property.required",
                    $"Property '{group[0]}' is required on {source.Type}.", $"properties.{group[0]}", source.Id));
                continue;
            }

            if (matches.Skip(1).Any(match => !string.Equals(
                    RawValue(match.Value), RawValue(matches[0].Value), StringComparison.Ordinal)))
            {
                issues.Add(Issue("migration.story.node.property.ambiguous",
                    $"Legacy aliases for property '{group[0]}' disagree on {source.Id}.",
                    $"properties.{group[0]}", source.Id));
                continue;
            }

            var value = matches[0].Value;
            if (!isValid(group[0], value))
            {
                issues.Add(Issue("migration.story.node.property.invalid",
                    $"Property '{group[0]}' has an invalid scalar value on {source.Id}.",
                    $"properties.{group[0]}", source.Id));
                continue;
            }

            destination.Properties[group[0]] = value.Clone();
        }
    }

    private static void TryCreateDialogueAggregate(
        StoryNodeResource play,
        IReadOnlyDictionary<string, StoryNodeResource> byId,
        IReadOnlyList<StoryConnectionResource> edges,
        IReadOnlyList<GraphResourceEnvelope> children,
        IDictionary<string, GraphNode> replacement,
        ISet<string> consumed,
        ICollection<GraphConnection> aggregateEdges,
        IDictionary<string, HashSet<string>> aggregateOutputNames,
        ICollection<ValidationIssue> issues)
    {
        if (!TryReadString(play, ["dialogue_id", "dialogue", "dialogueId"], out var dialogueId))
        {
            issues.Add(Issue("migration.story.dialogue.resource.required", "PlayDialogue dialogue reference is required.", "dialogue_id", play.Id));
            return;
        }
        var next = Outgoing(edges, play.Id, "next");
        if (next.Count != 1 || !byId.TryGetValue(next[0].To, out var branch)
            || StoryNodeDefinitionRegistry.CanonicalizeType(branch.Type) != "DialogueExitBranch")
        {
            issues.Add(Issue("migration.story.dialogue.topology.ambiguous",
                "PlayDialogue must be immediately followed by one DialogueExitBranch.", "next", play.Id));
            return;
        }
        if (Outgoing(edges, play.Id).Count != 1 || Incoming(edges, branch.Id).Count != 1)
        {
            issues.Add(Issue("migration.story.dialogue.topology.ambiguous",
                "Dialogue aggregate topology has extra or ambiguous edges.", "topology", play.Id));
            return;
        }
        if (!TryResolveChild(children, GraphResourceKind.Session, dialogueId, "dialogue_id", play.Id, issues, out var child))
            return;
        GraphNodeAuthoringResult created;
        try
        {
            var childGraph = child.Graph;
            if (childGraph is null)
            {
                issues.Add(Issue("migration.story.child.resource.invalid", "Canonical Session graph is missing.", "graph", play.Id));
                return;
            }
            var childIssues = GraphScopePolicy.Validate(childGraph, GraphScope.Session, compatibilityMode: true)
                .Concat(GraphNodeShapeValidator.Validate(childGraph, GraphScope.Session, compatibilityMode: true))
                .ToArray();
            foreach (var issue in childIssues) issues.Add(issue);
            if (childIssues.Length != 0) return;
            created = CanonicalAggregateNodeFactory.Create(child, play.Id);
        }
        catch (Exception exception) when (exception is GraphResourceEnvelopeException or InvalidOperationException or ArgumentException)
        {
            issues.Add(Issue("migration.story.child.resource.invalid", exception.Message, "dialogue_id", play.Id));
            return;
        }
        if (!created.IsSuccess || created.Candidate is null)
        {
            foreach (var issue in created.Issues) issues.Add(issue);
            return;
        }
        var outputs = created.Candidate.Ports.Where(port => port.IsOutput && port.InterfaceKind == GraphInterfaceKind.Flow)
            .Select(port => port.Id).ToHashSet(StringComparer.Ordinal);
        var branchEdges = Outgoing(edges, branch.Id);
        var branchOutputs = branchEdges.Select(edge => edge.Output).ToHashSet(StringComparer.Ordinal);
        if (branchEdges.Count == 0 || branchOutputs.Count != branchEdges.Count || !branchOutputs.SetEquals(outputs))
        {
            issues.Add(Issue("migration.story.dialogue.topology.ambiguous",
                "DialogueExitBranch outputs must match the child Session public outputs exactly.", "outputs", branch.Id));
            return;
        }
        var configured = ReadNames(branch);
        if (configured.Count != 0 && (!configured.SetEquals(branchEdges.Select(edge => edge.Output)) || !configured.SetEquals(outputs)))
        {
            issues.Add(Issue("migration.story.dialogue.topology.ambiguous",
                "Configured Dialogue exits do not match child Session public outputs.", "exit_names", branch.Id));
            return;
        }
        replacement[play.Id] = created.Candidate;
        aggregateOutputNames[branch.Id] = outputs;
        consumed.Add(branch.Id);
        foreach (var edge in branchEdges)
            aggregateEdges.Add(new GraphConnection(play.Id, edge.Output, edge.To, "flow_in", GraphInterfaceKind.Flow));
    }

    private static void TryCreateQuestAggregate(
        StoryNodeResource startQuest,
        IReadOnlyDictionary<string, StoryNodeResource> byId,
        IReadOnlyList<StoryConnectionResource> edges,
        IReadOnlyList<GraphResourceEnvelope> children,
        IDictionary<string, GraphNode> replacement,
        ISet<string> consumed,
        ICollection<GraphConnection> aggregateEdges,
        ICollection<ValidationIssue> issues)
    {
        if (!TryReadString(startQuest, ["quest_id", "quest", "questId"], out var questId))
        {
            issues.Add(Issue("migration.story.quest.resource.required", "StartQuest quest reference is required.", "quest_id", startQuest.Id));
            return;
        }
        var next = Outgoing(edges, startQuest.Id, "next");
        if (next.Count != 1 || !byId.TryGetValue(next[0].To, out var wait)
            || StoryNodeDefinitionRegistry.CanonicalizeType(wait.Type) != "WaitQuestComplete"
            || !TryReadString(wait, ["quest_id", "quest", "questId"], out var waitQuestId)
            || !string.Equals(questId, waitQuestId, StringComparison.Ordinal))
        {
            issues.Add(Issue("migration.story.quest.topology.unpaired",
                "StartQuest must be immediately followed by WaitQuestComplete for the same quest.", "next", startQuest.Id));
            return;
        }
        if (Outgoing(edges, startQuest.Id).Count != 1 || Incoming(edges, wait.Id).Count != 1)
        {
            issues.Add(Issue("migration.story.quest.topology.ambiguous", "Quest aggregate topology has extra or ambiguous edges.", "topology", startQuest.Id));
            return;
        }
        if (!TryResolveChild(children, GraphResourceKind.Task, questId, "quest_id", startQuest.Id, issues, out var child))
            return;
        GraphNodeAuthoringResult created;
        try
        {
            var childGraph = child.Graph;
            if (childGraph is null)
            {
                issues.Add(Issue("migration.story.child.resource.invalid", "Canonical Task graph is missing.", "graph", startQuest.Id));
                return;
            }
            var childIssues = GraphScopePolicy.Validate(childGraph, GraphScope.Task)
                .Concat(GraphNodeShapeValidator.Validate(childGraph, GraphScope.Task))
                .ToArray();
            foreach (var issue in childIssues) issues.Add(issue);
            if (childIssues.Length != 0) return;
            created = CanonicalAggregateNodeFactory.Create(child, startQuest.Id);
        }
        catch (Exception exception) when (exception is GraphResourceEnvelopeException or InvalidOperationException or ArgumentException)
        {
            issues.Add(Issue("migration.story.child.resource.invalid", exception.Message, "quest_id", startQuest.Id));
            return;
        }
        if (!created.IsSuccess || created.Candidate is null)
        {
            foreach (var issue in created.Issues) issues.Add(issue);
            return;
        }
        var outputs = created.Candidate.Ports.Where(port => port.IsOutput && port.InterfaceKind == GraphInterfaceKind.Flow).ToArray();
        if (outputs.Length != 1)
        {
            issues.Add(Issue("migration.story.quest.topology.ambiguous", "Task settlement must expose exactly one public output.", "quest_id", startQuest.Id));
            return;
        }
        replacement[startQuest.Id] = created.Candidate;
        consumed.Add(wait.Id);
        var outgoingWait = Outgoing(edges, wait.Id, "next");
        if (outgoingWait.Count != 1)
        {
            issues.Add(Issue("migration.story.quest.topology.ambiguous", "WaitQuestComplete must have exactly one next edge.", "next", wait.Id));
            return;
        }
        var edge = outgoingWait[0];
        aggregateEdges.Add(new GraphConnection(startQuest.Id, outputs[0].Id, edge.To, "flow_in", GraphInterfaceKind.Flow));
    }

    private static bool IsInternalPairEdge(StoryConnectionResource edge, ISet<string> consumed,
        IReadOnlyDictionary<string, GraphNode> replacement,
        IReadOnlyDictionary<string, HashSet<string>> aggregateOutputs)
        => (aggregateOutputs.ContainsKey(edge.From) && consumed.Contains(edge.From))
            || (replacement.TryGetValue(edge.From, out var from) && replacement.ContainsKey(edge.To)
                && from.Id == edge.From && consumed.Contains(edge.To));

    private static IReadOnlyList<StoryConnectionResource> Outgoing(IEnumerable<StoryConnectionResource> edges, string id, string? output = null)
        => edges.Where(edge => edge is not null && edge.From == id && (output is null || edge.Output == output)).ToArray();

    private static IReadOnlyList<StoryConnectionResource> Incoming(IEnumerable<StoryConnectionResource> edges, string id)
        => edges.Where(edge => edge is not null && edge.To == id).ToArray();

    private static bool TryRead(StoryNodeResource node, IEnumerable<string> names, out JsonElement value)
    {
        foreach (var name in names)
            if ((node.Properties ?? []).TryGetValue(name, out value)
                && value.ValueKind is not JsonValueKind.Null and not JsonValueKind.Undefined
                && ((value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))
                    || value.ValueKind is JsonValueKind.Number or JsonValueKind.True or JsonValueKind.False))
                return true;
        value = default;
        return false;
    }

    private static bool TryReadString(StoryNodeResource node, IEnumerable<string> names, out string value)
    {
        value = string.Empty;
        if (!TryRead(node, names, out var element) || element.ValueKind != JsonValueKind.String)
            return false;
        value = element.GetString() ?? string.Empty;
        return !string.IsNullOrWhiteSpace(value);
    }

    private static void CopyRequired(StoryNodeResource source, GraphNode destination, string name,
        IEnumerable<string> aliases, ICollection<ValidationIssue> issues)
    {
        if (!TryRead(source, aliases, out var value))
        {
            issues.Add(Issue("migration.story.action.property.required", $"Action property '{name}' is required.", name, source.Id));
            return;
        }
        destination.Properties[name] = value.Clone();
    }

    private static HashSet<string> ReadNames(StoryNodeResource node)
    {
        if (!(node.Properties ?? []).TryGetValue("exit_names", out var value)) return new(StringComparer.Ordinal);
        IEnumerable<string> values = value.ValueKind switch
        {
            JsonValueKind.Array => value.EnumerateArray().Where(item => item.ValueKind == JsonValueKind.String).Select(item => item.GetString() ?? string.Empty),
            JsonValueKind.String => (value.GetString() ?? string.Empty).Split([',', '，', ';', '；', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries),
            _ => [],
        };
        return values.Where(item => !string.IsNullOrWhiteSpace(item)).Select(item => item.Trim()).ToHashSet(StringComparer.Ordinal);
    }

    private static string SyntheticPortId(string nodeId) => $"story_trigger_{nodeId}";

    private static string RawValue(JsonElement value)
        => value.ValueKind == JsonValueKind.Undefined ? "<undefined>" : value.GetRawText();

    private static string SyntheticNodeId(string preferred, IEnumerable<string> occupied)
    {
        var used = occupied.ToHashSet(StringComparer.Ordinal);
        if (!used.Contains(preferred)) return preferred;
        var candidate = preferred + "_migration";
        var suffix = 2;
        while (used.Contains(candidate)) candidate = $"{preferred}_migration_{suffix++}";
        return candidate;
    }

    private static bool TryResolveChild(
        IReadOnlyList<GraphResourceEnvelope> children,
        GraphResourceKind expectedKind,
        string id,
        string field,
        string nodeId,
        ICollection<ValidationIssue> issues,
        out GraphResourceEnvelope child)
    {
        var idMatches = children.Where(candidate => candidate is not null
            && string.Equals(candidate.Id, id, StringComparison.Ordinal)).ToArray();
        var matches = idMatches.Where(candidate => candidate.ResourceKind == expectedKind).ToArray();
        if (matches.Length == 1)
        {
            child = matches[0];
            return true;
        }

        child = null!;
        var kindName = expectedKind == GraphResourceKind.Session ? "Session" : "Task";
        if (matches.Length > 1)
            issues.Add(Issue("migration.story.child.resource.duplicate",
                $"Canonical {kindName} '{id}' occurs more than once.", field, nodeId));
        else if (idMatches.Length != 0)
            issues.Add(Issue("migration.story.child.resource.kind",
                $"Child resource '{id}' does not include a {kindName}.", field, nodeId));
        else
            issues.Add(Issue("migration.story.child.resource.missing",
                $"Canonical {kindName} '{id}' is missing.", field, nodeId));
        return false;
    }

    private static ValidationIssue Issue(string code, string message, string field, string? nodeId = null)
        => new(code, message, field, NodeId: string.IsNullOrWhiteSpace(nodeId) ? null : nodeId);

    private static LegacyMigrationPreviewResult Failure(StoryResource source, IEnumerable<ValidationIssue> issues)
        => new(LegacyMigrationSourceKind.Story, source.Id, null, issues);

    private static IReadOnlyList<GraphResourceEnvelope> ChildResourcesFrom(IEnumerable<GraphResourceEnvelope>? source)
        => (source ?? []).Where(envelope => envelope is not null).ToArray();
}

/// <summary>Compatibility facade for callers that use the canonical prefix.</summary>
public static class CanonicalStoryFlowMigrationPreview
{
    public static LegacyMigrationPreviewResult Preview(StoryResource source, IEnumerable<GraphResourceEnvelope>? children = null)
        => StoryFlowMigrationPreview.PreviewStory(source, children);
    public static LegacyMigrationPreviewResult PreviewStory(StoryResource source, IEnumerable<GraphResourceEnvelope>? children = null)
        => StoryFlowMigrationPreview.PreviewStory(source, children);
    public static LegacyMigrationPreviewResult PreviewStory(StoryResource source, IReadOnlyDictionary<string, GraphResourceEnvelope> children)
        => StoryFlowMigrationPreview.PreviewStory(source, children);
}
