using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Graphs.Editing;

/// <summary>
/// UI-independent, atomic editing surface for a canonical graph document.
/// The supplied document instance is retained; Undo and Redo restore its contents.
/// </summary>
public sealed class GraphEditSession
{
    private readonly Stack<EditHistory> _undo = new();
    private readonly Stack<EditHistory> _redo = new();
    private readonly Func<string> _dynamicPortIdSource;
    private readonly HashSet<string> _issuedDynamicPortIds = new(StringComparer.Ordinal);
    private IReadOnlyList<ValidationIssue> _lastValidationIssues = [];
    private readonly Dictionary<(string Node, string Type), Dictionary<string, JsonElement>> _actionDrafts = new();

    public GraphEditSession(GraphDocument document, GraphScope? scope = null, bool compatibilityMode = false,
        Func<string>? dynamicPortIdSource = null)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        Scope = scope;
        CompatibilityMode = compatibilityMode;
        _dynamicPortIdSource = dynamicPortIdSource ?? NextDefaultDynamicPortId;
    }

    public GraphEditSession(GraphDocument document, GraphScope scope, bool compatibilityMode = false,
        Func<string>? dynamicPortIdSource = null)
        : this(document, (GraphScope?)scope, compatibilityMode, dynamicPortIdSource) { }

    public GraphDocument Document { get; }
    public GraphDocument Graph => Document;
    public GraphScope? Scope { get; }
    public bool CompatibilityMode { get; }
    public bool CanUndo => _undo.Count != 0;
    public bool CanRedo => _redo.Count != 0;
    public int UndoCount => _undo.Count;
    public int RedoCount => _redo.Count;
    public IReadOnlyList<ValidationIssue> LastValidationIssues => _lastValidationIssues;
    public IReadOnlyList<ValidationIssue> ValidationIssues => _lastValidationIssues;

    public IReadOnlyList<ValidationIssue> ValidateCandidate(GraphConnection candidate, GraphConnection? excludedConnection = null)
        => CandidateEdgeValidator.Validate(Document, candidate, Scope, excludedConnection, CompatibilityMode);

    public bool CanConnect(GraphConnection candidate)
        => ValidateCandidate(candidate).Count == 0;

    public bool CanConnect(string fromNodeId, string fromPortId, string toNodeId, string toPortId, GraphInterfaceKind interfaceKind)
        => CanConnect(new GraphConnection(fromNodeId, fromPortId, toNodeId, toPortId, interfaceKind));

    public bool Connect(GraphConnection candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        var issues = ValidateCandidate(candidate);
        if (issues.Count != 0) return Fail(issues);

        var before = DeepClone(Document);
        Document.Connections ??= [];
        Document.Connections.Add(Clone(candidate));
        Commit(before);
        return true;
    }

    /// <summary>
    /// Adds one caller-supplied canonical node. Validation is scoped to the
    /// candidate and the identity/type constraints it affects; unrelated draft
    /// Problems (including missing required nodes) do not block this edit.
    /// </summary>
    public bool AddNode(GraphNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (Scope == GraphScope.Project) return Fail([new("graph.project.projection.readonly", "请通过项目资源操作创建故事。", "node")]);
        if (!Scope.HasValue)
            return Fail([new("graph.node.scope.required",
                "A graph scope is required for node edits.", "scope", NodeId: NullIfBlank(node.Id))]);

        var issues = new List<ValidationIssue>(GraphValidator.Validate([node], []));
        if (!GraphNodeDefinitionRegistry.TryGet(Scope.Value, node.Type, out var definition))
        {
            var known = GraphNodeDefinitionRegistry.TryGet(node.Type, out var other);
            issues.Add(new(
                known ? "graph.scope.node_type.wrong_scope" : "graph.scope.node_type.unknown",
                known
                    ? $"Node type '{node.Type}' belongs to scope '{other.Scope}', not '{Scope.Value}'."
                    : $"Node type '{node.Type}' is not registered for scope '{Scope.Value}'.",
                "type", NodeId: NullIfBlank(node.Id)));
        }
        else
        {
            if (definition.CompatibilityOnly && !CompatibilityMode)
                issues.Add(new("graph.scope.node_type.compatibility_only",
                    $"Node type '{node.Type}' is compatibility-only.", "type", NodeId: NullIfBlank(node.Id)));

            foreach (var port in node.Ports ?? [])
            {
                if (port is null) continue;
                if (!definition.AllowedInterfaceKinds.Contains(port.InterfaceKind))
                    issues.Add(new("graph.scope.port.interface_kind.disallowed",
                        $"Port '{port.Id}' on node '{node.Id}' is not allowed for node type '{node.Type}'.",
                        "kind", NodeId: NullIfBlank(node.Id)));
                if (Scope.Value == GraphScope.Task && port.InterfaceKind == GraphInterfaceKind.Flow)
                    issues.Add(new("graph.scope.task.flow_port.disallowed",
                        "Task graphs cannot contain flow ports.", "kind", NodeId: NullIfBlank(node.Id)));
            }

            var sameType = (Document.Nodes ?? []).Count(existing => existing is not null
                && string.Equals(existing.Type, node.Type, StringComparison.Ordinal));
            if (definition.Unique && sameType != 0)
                issues.Add(new("graph.scope.required_node.duplicate",
                    $"Scope '{Scope.Value}' allows only one '{node.Type}' node.",
                    "type", NodeId: NullIfBlank(node.Id)));

            if (Scope.Value == GraphScope.Task
                && string.Equals(node.Type, CanonicalTaskObjectiveSchema.NodeType, StringComparison.Ordinal))
                issues.AddRange(AllowUnselectedObjectiveTarget(node,
                    CanonicalTaskObjectiveSchema.Validate(node)));
            if (Scope.Value == GraphScope.StoryFlow
                && string.Equals(node.Type, CanonicalStoryActionSchema.NodeType, StringComparison.Ordinal))
                issues.AddRange(CanonicalStoryActionSchema.AllowDraftIssues(node, CanonicalStoryActionSchema.Validate(node)));
        }

        if (!string.IsNullOrWhiteSpace(node.Id))
        {
            var sameId = (Document.Nodes ?? []).Count(existing => existing is not null
                && string.Equals(existing.Id, node.Id, StringComparison.Ordinal));
            if (sameId == 1)
                issues.Add(new("graph.node.id.duplicate",
                    $"Graph node ID '{node.Id}' is already used.", "id", NodeId: node.Id));
            else if (sameId > 1)
                issues.Add(new("graph.node.id.ambiguous",
                    $"Graph node ID '{node.Id}' is ambiguous in the draft.", "id", NodeId: node.Id));
        }

        if (issues.Count != 0) return Fail(issues);

        var before = DeepClone(Document);
        Document.Nodes ??= [];
        Document.Nodes.Add(Clone(node));
        Commit(before);
        return true;
    }

    /// <summary>Reports every edge incident to one uniquely identified node.</summary>
    public IReadOnlyList<GraphConnection> GetNodeReferences(string nodeId)
    {
        if (string.IsNullOrWhiteSpace(nodeId)) return [];
        var matches = (Document.Nodes ?? []).Where(node => node is not null
            && string.Equals(node.Id, nodeId, StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1) return [];

        return (Document.Connections ?? [])
            .Where(connection => connection is not null
                && (string.Equals(connection.FromNodeId, nodeId, StringComparison.Ordinal)
                    || string.Equals(connection.ToNodeId, nodeId, StringComparison.Ordinal)))
            .Select(Clone)
            .ToArray();
    }

    /// <summary>Builds a detached plan for every Story placement of one canonical resource.</summary>
    public CanonicalAggregateSynchronizationPlan AnalyzeAggregateSynchronization(
        GraphResourceEnvelope resource)
    {
        if (Scope != GraphScope.StoryFlow)
        {
            var issue = new ValidationIssue("graph.aggregate.sync.scope.invalid",
                "Aggregate synchronization requires a Story Flow graph.", "scope");
            _lastValidationIssues = [issue];
            return new CanonicalAggregateSynchronizationPlan(resource ?? throw new ArgumentNullException(nameof(resource)), [], [issue]);
        }

        try
        {
            var plan = new CanonicalAggregateSynchronizationService().Analyze(Document, resource);
            _lastValidationIssues = plan.Issues;
            return plan;
        }
        catch (ArgumentNullException) { throw; }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        {
            var issue = new ValidationIssue("graph.aggregate.sync.invalid",
                exception.Message, "resource");
            _lastValidationIssues = [issue];
            return new CanonicalAggregateSynchronizationPlan(resource, [], [issue]);
        }
    }

    public CanonicalAggregateSynchronizationPlan AnalyzeCanonicalAggregateSynchronization(
        GraphResourceEnvelope resource)
        => AnalyzeAggregateSynchronization(resource);

    public CanonicalAggregateSynchronizationPlan AnalyzeAggregate(GraphResourceEnvelope resource)
        => AnalyzeAggregateSynchronization(resource);

    /// <summary>
    /// Applies one previously analyzed aggregate plan as exactly one graph
    /// history unit. Obsolete incident edges require explicit confirmation.
    /// </summary>
    public bool ApplyAggregateSynchronization(
        CanonicalAggregateSynchronizationPlan plan,
        bool confirmReferencedRemoval = false)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (Scope != GraphScope.StoryFlow)
            return Fail([new("graph.aggregate.sync.scope.invalid",
                "Aggregate synchronization requires a Story Flow graph.", "scope")]);
        if (!plan.IsSuccess)
            return Fail(plan.Issues);

        var placements = plan.Placements;
        var indexes = new List<(int Index, CanonicalAggregateSynchronizationCandidate Candidate)>();
        foreach (var candidate in placements)
        {
            var matches = (Document.Nodes ?? []).Select((node, index) => (node, index))
                .Where(item => item.node is not null
                    && string.Equals(item.node.Id, candidate.PlacementNodeId, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length != 1)
                return Fail([new("graph.aggregate.sync.placement.stale",
                    $"Aggregate placement node ID '{candidate.PlacementNodeId}' is no longer uniquely present.",
                    "id", NodeId: candidate.PlacementNodeId)]);
            if (!CanonicalAggregateSynchronizationCandidate.Equivalent(matches[0].node!, candidate.AnalysisCurrentNode))
                return Fail([new("graph.aggregate.sync.plan.stale",
                    $"Aggregate placement '{candidate.PlacementNodeId}' changed after analysis.",
                    "placement", NodeId: candidate.PlacementNodeId)]);
            if (!SameConnectionMultiset(ObsoleteReferencesFor(candidate), candidate.AnalysisObsoleteReferences))
                return Fail([new("graph.aggregate.sync.references.stale",
                    $"Connections incident to obsolete ports on placement '{candidate.PlacementNodeId}' changed after analysis.",
                    "connections", NodeId: candidate.PlacementNodeId)]);
            indexes.Add((matches[0].index, candidate));
        }

        if (plan.RequiresConfirmation && !confirmReferencedRemoval)
        {
            return Fail([new("graph.aggregate.sync.references.confirmation_required",
                $"Synchronization would remove {plan.AnalysisObsoleteReferences.Count} obsolete connection(s); confirm referenced removal.",
                "connections")]);
        }

        if (!plan.HasChanges)
        {
            _lastValidationIssues = [];
            return true;
        }

        var before = DeepClone(Document);
        var liveNodes = Document.Nodes ??= [];
        foreach (var item in indexes)
            liveNodes[item.Index] = Clone(item.Candidate.AnalysisReplacementNode);

        if (plan.AnalysisObsoleteReferences.Count != 0)
        {
            var obsolete = plan.AnalysisObsoleteReferences;
            Document.Connections = (Document.Connections ?? [])
                .Where(connection => connection is not null
                    && !obsolete.Any(reference => reference.Equals(connection)))
                .ToList();
        }

        Commit(before);
        return true;
    }

    private IReadOnlyList<GraphConnection> ObsoleteReferencesFor(
        CanonicalAggregateSynchronizationCandidate candidate)
    {
        var obsoletePortIds = (candidate.AnalysisCurrentNode.Ports ?? []).Where(port => port is not null
            && !(candidate.AnalysisReplacementNode.Ports ?? []).Any(replacement => replacement is not null
                && string.Equals(replacement.Id, port.Id, StringComparison.Ordinal)
                && replacement.IsInput == port.IsInput
                && replacement.InterfaceKind == port.InterfaceKind))
            .Select(port => port.Id)
            .ToHashSet(StringComparer.Ordinal);
        return (Document.Connections ?? []).Where(connection => connection is not null
                && ((string.Equals(connection.FromNodeId, candidate.PlacementNodeId, StringComparison.Ordinal)
                     && obsoletePortIds.Contains(connection.FromPortId))
                    || (string.Equals(connection.ToNodeId, candidate.PlacementNodeId, StringComparison.Ordinal)
                     && obsoletePortIds.Contains(connection.ToPortId))))
            .Select(Clone)
            .ToArray();
    }

    private static bool SameConnectionMultiset(
        IReadOnlyList<GraphConnection> left,
        IReadOnlyList<GraphConnection> right)
    {
        if (left.Count != right.Count) return false;
        var unmatched = right.ToList();
        foreach (var connection in left)
        {
            var index = unmatched.FindIndex(candidate => candidate.Equals(connection));
            if (index < 0) return false;
            unmatched.RemoveAt(index);
        }
        return unmatched.Count == 0;
    }

    public bool ApplyCanonicalAggregateSynchronization(
        CanonicalAggregateSynchronizationPlan plan,
        bool confirmReferencedRemoval = false)
        => ApplyAggregateSynchronization(plan, confirmReferencedRemoval);

    public bool ApplyAggregateSynchronization(
        GraphResourceEnvelope resource,
        bool confirmReferencedRemoval = false)
        => SynchronizeAggregate(resource, confirmReferencedRemoval);

    public bool ApplyAggregateSynchronizationPlan(
        CanonicalAggregateSynchronizationPlan plan,
        bool confirmReferencedRemoval = false)
        => ApplyAggregateSynchronization(plan, confirmReferencedRemoval);

    public bool SynchronizeAggregate(GraphResourceEnvelope resource,
        bool confirmReferencedRemoval = false)
    {
        var plan = AnalyzeAggregateSynchronization(resource);
        return ApplyAggregateSynchronization(plan, confirmReferencedRemoval);
    }

    /// <summary>
    /// Removes one uniquely identified node. Referenced removal requires an
    /// explicit confirmation and cleans the node's incident edges atomically.
    /// </summary>
    public bool RemoveNode(string nodeId, bool confirmReferencedRemoval = false)
    {
        if (!Scope.HasValue)
            return Fail([new("graph.node.scope.required",
                "A graph scope is required for node edits.", "scope", NodeId: NullIfBlank(nodeId))]);
        if (string.IsNullOrWhiteSpace(nodeId))
            return Fail([new("graph.node.id.required", "Graph node ID is required.", "node_id")]);

        var matches = (Document.Nodes ?? []).Where(node => node is not null
            && string.Equals(node.Id, nodeId, StringComparison.Ordinal)).ToArray();
        if (matches.Length == 0)
            return Fail([new("graph.node.missing", $"Node '{nodeId}' does not exist.", "node_id", NodeId: NullIfBlank(nodeId))]);
        if (matches.Length > 1)
            return Fail([new("graph.node.ambiguous", $"Node '{nodeId}' is ambiguous.", "node_id", NodeId: NullIfBlank(nodeId))]);

        var node = matches[0];
        // Unknown and wrong-scope nodes must remain deletable so a malformed or
        // migrated draft can be repaired. Only a canonical definition in this
        // scope may grant the non-deletable protection.
        if (GraphNodeDefinitionRegistry.TryGet(Scope.Value, node.Type, out var definition)
            && (definition.NonDeletable || definition.Required))
            return Fail([new("graph.node.not_deletable",
                $"Node type '{node.Type}' cannot be deleted in scope '{Scope.Value}'.", "node_id", NodeId: node.Id)]);

        var references = GetNodeReferences(node.Id);
        if (references.Count != 0 && !confirmReferencedRemoval)
            return Fail([new("graph.node.references.confirmation_required",
                $"Node '{node.Id}' is referenced by {references.Count} connection(s); confirm removal to clean them up.",
                "confirm_referenced_removal", NodeId: node.Id)]);

        var before = DeepClone(Document);
        Document.Nodes ??= [];
        Document.Nodes.Remove(node);
        if (references.Count != 0)
        {
            Document.Connections = (Document.Connections ?? [])
                .Where(connection => connection is null
                    || (!string.Equals(connection.FromNodeId, node.Id, StringComparison.Ordinal)
                        && !string.Equals(connection.ToNodeId, node.Id, StringComparison.Ordinal)))
                .ToList();
        }

        Commit(before);
        return true;
    }

    /// <summary>
    /// Removes every deletable node in one document mutation and one Undo unit.
    /// Required/non-deletable nodes are retained without blocking deletable
    /// peers. Every edge incident to a removed node is cleaned up atomically.
    /// </summary>
    public bool RemoveNodes(
        IReadOnlyList<string> nodeIds,
        bool confirmReferencedRemoval = false)
    {
        ArgumentNullException.ThrowIfNull(nodeIds);
        if (!Scope.HasValue)
            return Fail([new("graph.node.scope.required",
                "A graph scope is required for node edits.", "scope")]);

        var requestedIds = nodeIds.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal).ToArray();
        if (requestedIds.Length == 0)
            return Fail([new("graph.node.id.required", "At least one graph node ID is required.", "node_id")]);

        var resolved = new List<GraphNode>(requestedIds.Length);
        foreach (var nodeId in requestedIds)
        {
            var matches = (Document.Nodes ?? []).Where(node => node is not null
                && string.Equals(node.Id, nodeId, StringComparison.Ordinal)).ToArray();
            if (matches.Length == 0)
                return Fail([new("graph.node.missing", $"Node '{nodeId}' does not exist.",
                    "node_id", NodeId: nodeId)]);
            if (matches.Length > 1)
                return Fail([new("graph.node.ambiguous", $"Node '{nodeId}' is ambiguous.",
                    "node_id", NodeId: nodeId)]);
            resolved.Add(matches[0]);
        }

        var deletable = resolved.Where(node =>
            !GraphNodeDefinitionRegistry.TryGet(Scope.Value, node.Type, out var definition)
            || !definition.NonDeletable && !definition.Required).ToArray();
        if (deletable.Length == 0)
        {
            var protectedNode = resolved[0];
            return Fail([new("graph.node.not_deletable",
                $"Node type '{protectedNode.Type}' cannot be deleted in scope '{Scope.Value}'.",
                "node_id", NodeId: protectedNode.Id)]);
        }

        var deletableIds = deletable.Select(node => node.Id).ToHashSet(StringComparer.Ordinal);
        var references = (Document.Connections ?? []).Where(connection => connection is not null
            && (deletableIds.Contains(connection.FromNodeId)
                || deletableIds.Contains(connection.ToNodeId))).ToArray();
        if (references.Length != 0 && !confirmReferencedRemoval)
            return Fail([new("graph.node.references.confirmation_required",
                $"The selected nodes are referenced by {references.Length} connection(s); "
                    + "confirm removal to clean them up.",
                "confirm_referenced_removal")]);

        var before = DeepClone(Document);
        Document.Nodes ??= [];
        Document.Nodes.RemoveAll(node => node is not null && deletableIds.Contains(node.Id));
        if (references.Length != 0)
        {
            Document.Connections = (Document.Connections ?? []).Where(connection => connection is null
                || !deletableIds.Contains(connection.FromNodeId)
                    && !deletableIds.Contains(connection.ToNodeId)).ToList();
        }

        Commit(before);
        return true;
    }

    public bool Connect(string fromNodeId, string fromPortId, string toNodeId, string toPortId, GraphInterfaceKind interfaceKind)
        => Connect(new GraphConnection(fromNodeId, fromPortId, toNodeId, toPortId, interfaceKind));

    /// <summary>Infers the edge kind from the unique source port, for endpoint-oriented callers.</summary>
    public bool Connect(string fromNodeId, string fromPortId, string toNodeId, string toPortId)
    {
        var source = (Document.Nodes ?? []).Where(node => node is not null && node.Id == fromNodeId)
            .SelectMany(node => node.Ports ?? []).Where(port => port is not null && port.Id == fromPortId).ToArray();
        var kind = source.Length == 1 ? source[0].InterfaceKind : GraphInterfaceKind.Flow;
        return Connect(fromNodeId, fromPortId, toNodeId, toPortId, kind);
    }

    public bool Disconnect(GraphConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var index = (Document.Connections ?? []).FindIndex(item => item is not null && item.Equals(connection));
        if (index < 0)
        {
            return Fail([new("graph.connection.not_found", "The requested graph connection does not exist.", "connections")]);
        }

        var before = DeepClone(Document);
        var connections = Document.Connections ??= [];
        connections.RemoveAt(index);
        Commit(before);
        return true;
    }

    public bool Disconnect(string fromNodeId, string fromPortId, string toNodeId, string toPortId, GraphInterfaceKind interfaceKind)
        => Disconnect(new GraphConnection(fromNodeId, fromPortId, toNodeId, toPortId, interfaceKind));

    public bool Disconnect(string fromNodeId, string fromPortId, string toNodeId, string toPortId)
    {
        var connection = (Document.Connections ?? []).FirstOrDefault(item => item is not null
            && item.FromNodeId == fromNodeId && item.FromPortId == fromPortId
            && item.ToNodeId == toNodeId && item.ToPortId == toPortId);
        return connection is not null
            ? Disconnect(connection)
            : Fail([new("graph.connection.not_found", "The requested graph connection does not exist.", "connections")]);
    }

    public bool Reconnect(GraphConnection original, GraphConnection replacement)
    {
        ArgumentNullException.ThrowIfNull(original);
        ArgumentNullException.ThrowIfNull(replacement);
        var index = (Document.Connections ?? []).FindIndex(item => item is not null && item.Equals(original));
        if (index < 0)
            return Fail([new("graph.connection.original.missing", "The connection to reconnect does not exist.", "connections")]);
        if (original.Equals(replacement))
            return Fail([]);

        // The original remains in the live document until validation succeeds.
        // CandidateEdgeValidator excludes it only for this prospective transaction.
        var issues = ValidateCandidate(replacement, original);
        if (issues.Count != 0) return Fail(issues);

        var before = DeepClone(Document);
        var connections = Document.Connections ??= [];
        // Replacing in place keeps collection order stable for editor projections
        // and makes the two-endpoint change one atomic document operation.
        connections[index] = Clone(replacement);
        Commit(before);
        return true;
    }

    /// <summary>
    /// Replaces or removes a validated bundle of existing connections as one
    /// document mutation and one Undo unit. The replacement count is allowed
    /// to differ from the original count so a single edge can be atomically
    /// expanded into a validated path (the canonical editor's splice gesture).
    /// Passing an empty replacement list disconnects the whole bundle.
    /// </summary>
    public bool ReplaceConnections(
        IReadOnlyList<GraphConnection> originals,
        IReadOnlyList<GraphConnection> replacements)
    {
        ArgumentNullException.ThrowIfNull(originals);
        ArgumentNullException.ThrowIfNull(replacements);
        if (originals.Count == 0)
            return Fail([]);

        var uniqueOriginals = originals.Where(connection => connection is not null).Distinct().ToArray();
        if (uniqueOriginals.Length != originals.Count)
            return Fail([new("graph.connection.bundle.originals.invalid",
                "The connection bundle contains a null or duplicate original.", "connections")]);

        var liveConnections = (Document.Connections ?? []).Where(connection => connection is not null).ToArray();
        if (uniqueOriginals.Any(original => liveConnections.Count(connection => connection.Equals(original)) != 1))
            return Fail([new("graph.connection.bundle.original.missing",
                "One or more original connections do not exist exactly once.", "connections")]);

        if (originals.Count == replacements.Count
            && originals.Zip(replacements).All(pair => pair.First.Equals(pair.Second)))
            return Fail([]);

        var detached = DeepClone(Document);
        detached.Connections = (detached.Connections ?? [])
            .Where(connection => connection is not null
                && !uniqueOriginals.Any(original => connection.Equals(original)))
            .ToList();
        foreach (var replacement in replacements)
        {
            if (replacement is null)
                return Fail([new("graph.connection.bundle.replacement.invalid",
                    "The connection bundle contains a null replacement.", "connections")]);
            var issues = CandidateEdgeValidator.Validate(
                detached,
                replacement,
                Scope,
                excludedConnection: null,
                compatibilityMode: CompatibilityMode);
            if (issues.Count != 0) return Fail(issues);
            detached.Connections.Add(Clone(replacement));
        }

        var before = DeepClone(Document);
        var current = (Document.Connections ?? []).Where(connection => connection is not null).ToList();
        var firstOriginalIndex = current.FindIndex(connection =>
            uniqueOriginals.Any(original => original.Equals(connection)));
        var insertionIndex = current.Take(firstOriginalIndex)
            .Count(connection => !uniqueOriginals.Any(original => original.Equals(connection)));
        current.RemoveAll(connection => uniqueOriginals.Any(original => original.Equals(connection)));
        current.InsertRange(insertionIndex, replacements.Select(Clone));
        Document.Connections = current;
        Commit(before);
        return true;
    }

    public bool Reconnect(GraphConnection original, string fromNodeId, string fromPortId, string toNodeId,
        string toPortId, GraphInterfaceKind interfaceKind)
        => Reconnect(original, new GraphConnection(fromNodeId, fromPortId, toNodeId, toPortId, interfaceKind));

    public bool Reconnect(GraphConnection original, string fromNodeId, string fromPortId, string toNodeId,
        string toPortId)
    {
        var source = (Document.Nodes ?? []).Where(node => node is not null && node.Id == fromNodeId)
            .SelectMany(node => node.Ports ?? []).Where(port => port is not null && port.Id == fromPortId).ToArray();
        var kind = source.Length == 1 ? source[0].InterfaceKind : GraphInterfaceKind.Flow;
        return Reconnect(original, fromNodeId, fromPortId, toNodeId, toPortId, kind);
    }

    public bool RenamePortDisplayName(string nodeId, string portId, string displayName)
    {
        var port = FindUniquePort(nodeId, portId, out var issues);
        if (port is null) return Fail(issues);
        if (TryGetDynamicRole(nodeId, port, out var role))
        {
            if (!role!.UserEditable)
                return Fail([ReadOnlyDynamicPortIssue(nodeId, role)]);
            if (string.IsNullOrWhiteSpace(displayName))
                return Fail([new("graph.dynamic_port.label.required", "Dynamic port display name is required.", "display_name", NodeId: nodeId)]);
            if ((Document.Nodes ?? []).Where(node => node is not null && string.Equals(node.Id, nodeId, StringComparison.Ordinal))
                .SelectMany(node => node.Ports ?? [])
                .Any(candidate => candidate is not null && !ReferenceEquals(candidate, port)
                    && string.Equals(candidate.DisplayName, displayName, StringComparison.Ordinal)))
            {
                return Fail([new("graph.dynamic_port.label.duplicate",
                    $"Display name '{displayName}' is already used on node '{nodeId}'.", "display_name", NodeId: nodeId)]);
            }
            if (Scope == GraphScope.Task
                && string.Equals((Document.Nodes ?? []).SingleOrDefault(candidate => candidate is not null
                    && string.Equals(candidate.Id, nodeId, StringComparison.Ordinal))?.Type,
                    "settle", StringComparison.Ordinal)
                && IsTaskPublicDisplayNameUsed(nodeId, displayName, port))
            {
                return Fail([TaskPublicDisplayNameDuplicateIssue(nodeId, displayName)]);
            }
        }
        if (string.Equals(port.DisplayName, displayName, StringComparison.Ordinal)) return Fail([]);
        var before = DeepClone(Document);
        port.DisplayName = displayName;
        Commit(before);
        return true;
    }

    public bool ReorderPort(string nodeId, string portId, int order)
    {
        var port = FindUniquePort(nodeId, portId, out var issues);
        if (port is null) return Fail(issues);
        if (TryGetDynamicRole(nodeId, port, out var role))
        {
            if (!role!.UserEditable)
                return Fail([ReadOnlyDynamicPortIssue(nodeId, role)]);
            if (order < 0)
                return Fail([new("graph.dynamic_port.order.invalid", "Dynamic port order cannot be negative.", "order", NodeId: nodeId)]);
            if ((Document.Nodes ?? []).Where(node => node is not null && string.Equals(node.Id, nodeId, StringComparison.Ordinal))
                .SelectMany(node => node.Ports ?? [])
                .Any(candidate => candidate is not null && !ReferenceEquals(candidate, port) && candidate.Order == order
                    && MatchesDynamicRole(candidate, port)))
            {
                return Fail([new("graph.dynamic_port.order.duplicate",
                    $"Order '{order}' is already used by another port in this dynamic role.", "order", NodeId: nodeId)]);
            }
        }
        if (port.Order == order) return Fail([]);
        var before = DeepClone(Document);
        port.Order = order;
        Commit(before);
        return true;
    }

    /// <summary>
    /// Moves one editable dynamic port to a zero-based position and shifts the
    /// other ports in the same role atomically. Stable port IDs are retained;
    /// only presentation order changes. Unlike <see cref="ReorderPort"/>,
    /// this operation owns the contiguous reorder transaction.
    /// </summary>
    public bool MoveDynamicPort(string nodeId, string portId, int order)
    {
        if (!TryResolveDynamicPort(nodeId, portId, out var node, out var port, out var role, out var issues))
            return Fail(issues);
        if (!role!.UserEditable)
            return Fail([ReadOnlyDynamicPortIssue(node!.Id, role)]);

        var rolePorts = (node!.Ports ?? [])
            .Where(candidate => candidate is not null && MatchesRole(candidate, role))
            .OrderBy(candidate => candidate.Order)
            .ThenBy(candidate => candidate.Id, StringComparer.Ordinal)
            .ToList();
        if (rolePorts.Count != rolePorts.Select(candidate => candidate.Order).Distinct().Count()
            || rolePorts.Select((candidate, index) => candidate.Order == index).Any(isContiguous => !isContiguous))
        {
            return Fail([new("graph.dynamic_port.order.contiguous",
                "Dynamic ports in this role must have contiguous zero-based order.",
                "order", NodeId: node.Id)]);
        }
        if (order < 0 || order >= rolePorts.Count)
            return Fail([new("graph.dynamic_port.order.invalid",
                "Dynamic port order is outside the current role list.",
                "order", NodeId: node.Id)]);

        var current = rolePorts.FindIndex(candidate => ReferenceEquals(candidate, port));
        if (current < 0)
            return Fail([new("graph.dynamic_port.port.missing",
                $"Port '{portId}' does not exist in its dynamic role.",
                "port_id", NodeId: node.Id)]);
        if (current == order) return Fail([]);

        var before = DeepClone(Document);
        rolePorts.RemoveAt(current);
        rolePorts.Insert(order, port!);
        for (var index = 0; index < rolePorts.Count; index++)
            rolePorts[index].Order = index;
        Commit(before);
        return true;
    }

    /// <summary>Alias emphasizing that this is a dynamic-port reorder.</summary>
    public bool ReorderDynamicPort(string nodeId, string portId, int order)
        => MoveDynamicPort(nodeId, portId, order);

    /// <summary>Atomically sets one untyped node property.</summary>
    public bool SetNodeProperty(string nodeId, string property, JsonElement value)
    {
        if (Scope == GraphScope.Project) return Fail([new("graph.project.projection.readonly", "请进入故事编辑内部内容。", "node")]);
        if (string.IsNullOrWhiteSpace(property))
            return Fail([new("graph.node.property.key.required", "Node property key is required.", "property", NodeId: NullIfBlank(nodeId))]);
        if (value.ValueKind == JsonValueKind.Undefined)
            return Fail([new("graph.node.property.value.undefined", "Node property value cannot be undefined.", "value", NodeId: NullIfBlank(nodeId))]);
        if (!TryResolvePropertyNode(nodeId, out var node, out var issues)) return Fail(issues);

        if (Scope == GraphScope.Task && string.Equals(node!.Type, CanonicalTaskObjectiveSchema.NodeType, StringComparison.Ordinal))
        {
            if (string.Equals(property, CanonicalTaskObjectiveSchema.TypeProperty, StringComparison.Ordinal))
                return Fail([ObjectiveTypeAtomicIssue(node.Id)]);
            if (string.Equals(property, CanonicalTaskObjectiveSchema.PrerequisiteEnabledProperty, StringComparison.Ordinal))
                return Fail([ObjectivePropertyIssue("graph.objective.prerequisite.atomic_required",
                    "Objective prerequisite changes must update the flag, port, and incident connections atomically.",
                    property, node.Id)]);
            if (!CanonicalTaskObjectiveSchema.AllProperties.Contains(property))
                return Fail([ObjectivePropertyIssue("graph.objective.property.unsupported",
                    $"Objective property '{property}' is not part of the frozen contract.", property, node.Id)]);

            var candidate = Clone(node);
            candidate.Properties[property] = value.Clone();
            var objectiveIssues = AllowUnselectedObjectiveTarget(
                candidate,
                CanonicalTaskObjectiveSchema.Validate(candidate));
            if (objectiveIssues.Count != 0) return Fail(objectiveIssues);
            var beforeObjective = DeepClone(Document);
            node.Properties[property] = value.Clone();
            Commit(beforeObjective);
            return true;
        }

        if (Scope == GraphScope.StoryFlow
            && string.Equals(node!.Type, CanonicalStoryActionSchema.NodeType, StringComparison.Ordinal))
        {
            if (string.Equals(property, CanonicalStoryActionSchema.TypeProperty, StringComparison.Ordinal))
                return Fail([new("graph.story.action.type.atomic_required",
                    "Action type changes must replace their payload atomically.", property, NodeId: node.Id)]);
            if (!CanonicalStoryActionSchema.AllProperties.Contains(property))
                return Fail([new("graph.story.action.property.unsupported",
                    $"Action property '{property}' is not part of the enabled contract.", property, NodeId: node.Id)]);
            var candidate = Clone(node);
            candidate.Properties[property] = value.Clone();
            var actionIssues = CanonicalStoryActionSchema.AllowDraftIssues(candidate, CanonicalStoryActionSchema.Validate(candidate));
            if (actionIssues.Count != 0) return Fail(actionIssues);
            var beforeAction = DeepClone(Document);
            node.Properties[property] = value.Clone();
            Commit(beforeAction);
            return true;
        }

        if (Scope == GraphScope.StoryFlow && node!.Type == "title")
        {
            var candidate = Clone(node); candidate.Properties[property] = value.Clone();
            var titleIssues = CanonicalTitleSchema.Validate(candidate);
            if (titleIssues.Count != 0) return Fail(titleIssues);
        }

        if (Scope == GraphScope.Session && node!.Type is "music" or "screen")
        {
            var candidate = Clone(node);
            candidate.Properties[property] = value.Clone();
            var presentationIssues = CanonicalSessionPresentationSchema.Validate(candidate);
            if (presentationIssues.Count != 0) return Fail(presentationIssues);
        }

        var isPublicBoundary = node!.Type is "logic_input" or "logic_output" or "terminate"
            || (Scope == GraphScope.Session && string.Equals(node.Type, "end", StringComparison.Ordinal));
        var isSessionEndDisplayName = Scope == GraphScope.Session
            && string.Equals(node.Type, "end", StringComparison.Ordinal)
            && string.Equals(property, "display_name", StringComparison.Ordinal);
        var isTaskLogicOutput = Scope == GraphScope.Task
            && string.Equals(node.Type, "logic_output", StringComparison.Ordinal);
        if ((node!.Properties ?? []).TryGetValue(property, out var existing)
            && JsonElement.DeepEquals(existing, value))
        {
            // Preserve the existing no-op behavior for a stable identity
            // property, while never creating an Undo unit.
            if (isTaskLogicOutput && property == "display_name"
                && (value.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(value.GetString())))
            {
                return Fail([TaskLogicOutputDisplayNameInvalidIssue(node.Id)]);
            }
            var endFlowInput = isSessionEndDisplayName
                ? (node.Ports ?? []).FirstOrDefault(port => port is not null
                    && string.Equals(port.Id, "flow_in", StringComparison.Ordinal))
                : null;
            if (endFlowInput is null
                || value.ValueKind != JsonValueKind.String
                || string.Equals(endFlowInput.DisplayName, value.GetString(), StringComparison.Ordinal))
                return Fail([]);
        }

        if (isTaskLogicOutput && property == "port_id")
        {
            return Fail([TaskLogicOutputPortIdImmutableIssue(node.Id)]);
        }

        if (isPublicBoundary && property == "port_id")
        {
            return Fail([new("graph.public_boundary.port_id.immutable",
                "Public boundary port_id is a stable identity and cannot be changed.",
                "properties.port_id", NodeId: node.Id)]);
        }

        if (isTaskLogicOutput && property == "display_name"
            && (value.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(value.GetString())))
        {
            return Fail([TaskLogicOutputDisplayNameInvalidIssue(node.Id)]);
        }

        if (isTaskLogicOutput && property == "display_name"
            && IsTaskPublicDisplayNameUsed(node.Id, value.GetString()!))
        {
            return Fail([TaskPublicDisplayNameDuplicateIssue(node.Id, value.GetString() ?? string.Empty)]);
        }

        if (isPublicBoundary && property == "display_name"
            && (value.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(value.GetString())))
        {
            return Fail([new("graph.public_boundary.display_name.invalid",
                "Public boundary display_name must be a nonblank JSON string.",
                "properties.display_name", NodeId: node.Id)]);
        }

        if (isPublicBoundary && property == "display_name"
            && Document.Nodes.Any(other => other.Id != node.Id && other.Type == node.Type
                && other.Properties.TryGetValue("display_name", out var name)
                && name.ValueKind == JsonValueKind.String && name.GetString() == value.GetString()))
            return Fail([new("graph.public_boundary.display_name.duplicate", "同类公共端口显示名不能重复。", "properties.display_name", NodeId: node.Id)]);
        var before = DeepClone(Document);
        node.Properties ??= new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        node.Properties[property] = value.Clone();
        if (isSessionEndDisplayName)
        {
            var flowInput = (node.Ports ?? []).FirstOrDefault(port => port is not null
                && string.Equals(port.Id, "flow_in", StringComparison.Ordinal));
            if (flowInput is not null) flowInput.DisplayName = value.GetString()!;
        }
        Commit(before);
        return true;
    }

    public bool SetNodeProperty<T>(string nodeId, string property, T value)
        => SetNodeProperty(nodeId, property, JsonSerializer.SerializeToElement(value));

    /// <summary>
    /// Changes a Task Objective type as one transaction. Common description and
    /// required fields are retained; every old type payload is replaced.
    /// </summary>
    public bool ChangeObjectiveType(string nodeId, string? type, string? actorId = null)
    {
        if (Scope != GraphScope.Task)
            return Fail([ObjectivePropertyIssue("graph.objective.scope.required", "Objective editing requires a Task graph.", "scope", nodeId)]);
        if (!TryResolvePropertyNode(nodeId, out var node, out var issues)) return Fail(issues);
        if (!string.Equals(node!.Type, CanonicalTaskObjectiveSchema.NodeType, StringComparison.Ordinal))
            return Fail([ObjectivePropertyIssue("graph.objective.node.required", $"Node '{nodeId}' is not a Task Objective.", "type", node.Id)]);

        // Selecting the current type is a genuine no-op. This intentionally
        // preserves custom payload values and does not create history.
        if (node.Properties.TryGetValue(CanonicalTaskObjectiveSchema.TypeProperty, out var currentType)
            && currentType.ValueKind == JsonValueKind.String
            && string.Equals(currentType.GetString(), type, StringComparison.Ordinal))
            return true;

        var candidate = Clone(node);
        if (!CanonicalTaskObjectiveSchema.TryInitializeType(candidate, type, actorId, out var typeIssues))
            return Fail(typeIssues);
        var shapeIssues = GraphNodeShapeValidator.Validate(candidate, GraphScope.Task);
        var authoringShapeIssues = AllowUnselectedObjectiveTarget(candidate, shapeIssues);
        if (authoringShapeIssues.Count != 0) return Fail(authoringShapeIssues);

        var before = DeepClone(Document);
        node.Properties = candidate.Properties;
        Commit(before);
        return true;
    }

    public bool SetObjectiveType(string nodeId, string? type, string? actorId = null) => ChangeObjectiveType(nodeId, type, actorId);

    /// <summary>
    /// Changes only the target identity for the Objective's current type. The
    /// target itself must be legal, while unrelated pre-existing authoring
    /// issues remain reported so an invalid legacy Objective can be repaired
    /// incrementally instead of making every picker selection fail closed.
    /// </summary>
    public bool ChangeObjectiveTarget(string nodeId, string? targetId)
    {
        if (Scope != GraphScope.Task)
            return Fail([ObjectivePropertyIssue("graph.objective.scope.required",
                "Objective editing requires a Task graph.", "scope", nodeId)]);
        if (!TryResolvePropertyNode(nodeId, out var node, out var issues)) return Fail(issues);
        if (!string.Equals(node!.Type, CanonicalTaskObjectiveSchema.NodeType, StringComparison.Ordinal))
            return Fail([ObjectivePropertyIssue("graph.objective.node.required",
                $"Node '{nodeId}' is not a Task Objective.", "type", node.Id)]);
        if (string.IsNullOrWhiteSpace(targetId))
            return Fail([ObjectivePropertyIssue("graph.objective.target.required",
                "Objective target identity must be nonblank.", "target", node.Id)]);

        var type = node.Properties.TryGetValue(CanonicalTaskObjectiveSchema.TypeProperty, out var typeValue)
            && typeValue.ValueKind == JsonValueKind.String ? typeValue.GetString() : null;
        var property = type switch
        {
            CanonicalTaskObjectiveSchema.KillEntity => CanonicalTaskObjectiveSchema.EntityProperty,
            CanonicalTaskObjectiveSchema.CollectItem or CanonicalTaskObjectiveSchema.SubmitItem => CanonicalTaskObjectiveSchema.ItemProperty,
            CanonicalTaskObjectiveSchema.InteractActor => CanonicalTaskObjectiveSchema.ActorIdProperty,
            _ => null,
        };
        if (property is null)
            return Fail([ObjectivePropertyIssue("graph.objective.type.invalid",
                $"Unsupported objective type '{type}'.",
                CanonicalTaskObjectiveSchema.TypeProperty, node.Id)]);
        if (node.Properties.TryGetValue(property, out var existing)
            && existing.ValueKind == JsonValueKind.String
            && string.Equals(existing.GetString(), targetId, StringComparison.Ordinal))
            return true;

        var before = DeepClone(Document);
        node.Properties[property] = JsonSerializer.SerializeToElement(targetId);
        var remainingIssues = AllowUnselectedObjectiveTarget(
            node, CanonicalTaskObjectiveSchema.Validate(node));
        Commit(before);
        _lastValidationIssues = remainingIssues.ToArray();
        return true;
    }

    public bool SetObjectiveDescription(string nodeId, string? description)
        => SetNodeProperty(nodeId, CanonicalTaskObjectiveSchema.DescriptionProperty,
            JsonSerializer.SerializeToElement(description ?? string.Empty));

    public bool SetObjectiveRequired(string nodeId, int required)
    {
        var node = (Document.Nodes ?? []).FirstOrDefault(candidate => candidate is not null
            && string.Equals(candidate.Id, nodeId, StringComparison.Ordinal));
        if (node is null || !node.Properties.TryGetValue(CanonicalTaskObjectiveSchema.TypeProperty, out var type)
            || type.ValueKind != JsonValueKind.String)
            return SetNodeProperty(nodeId, CanonicalTaskObjectiveSchema.RequiredProperty,
                JsonSerializer.SerializeToElement(required));
        if (string.Equals(type.GetString(), CanonicalTaskObjectiveSchema.InteractActor, StringComparison.Ordinal))
            return Fail([new("graph.objective.interact.required.unsupported",
                "角色交互目标不使用次数。", $"properties.{CanonicalTaskObjectiveSchema.RequiredProperty}", NodeId: nodeId)]);
        return SetNodeProperty(nodeId, CanonicalTaskObjectiveSchema.RequiredProperty,
            JsonSerializer.SerializeToElement(required));
    }

    /// <summary>
    /// Atomically toggles the schema-owned Objective prerequisite flag and its
    /// single stable Logic input. Disabling the flag also removes every edge
    /// incident to the removed prerequisite port in the same Undo unit.
    /// </summary>
    public bool SetObjectivePrerequisiteEnabled(string nodeId, bool enabled)
    {
        if (Scope != GraphScope.Task)
            return Fail([ObjectivePropertyIssue("graph.objective.scope.required",
                "Objective editing requires a Task graph.", "scope", nodeId)]);
        if (!TryResolvePropertyNode(nodeId, out var node, out var issues)) return Fail(issues);
        if (!string.Equals(node!.Type, CanonicalTaskObjectiveSchema.NodeType, StringComparison.Ordinal))
            return Fail([ObjectivePropertyIssue("graph.objective.node.required",
                $"Node '{nodeId}' is not a Task Objective.", "type", node.Id)]);

        var currentInputs = (node.Ports ?? []).Where(port => port is not null
            && port.IsInput && port.InterfaceKind == GraphInterfaceKind.Logic).ToArray();
        var alreadyExact = CanonicalTaskObjectiveSchema.IsPrerequisiteEnabled(node) == enabled
            && (enabled
                ? currentInputs.Length == 1
                    && string.Equals(currentInputs[0]!.Id, CanonicalTaskObjectiveSchema.PrerequisitePortId, StringComparison.Ordinal)
                    && string.Equals(currentInputs[0]!.DisplayName, CanonicalTaskObjectiveSchema.PrerequisiteDisplayName, StringComparison.Ordinal)
                : currentInputs.Length == 0);
        if (alreadyExact) return true;

        var candidate = Clone(node);
        candidate.Properties[CanonicalTaskObjectiveSchema.PrerequisiteEnabledProperty] =
            JsonSerializer.SerializeToElement(enabled);
        var oldInputIds = currentInputs.Select(port => port!.Id).ToHashSet(StringComparer.Ordinal);
        candidate.Ports.RemoveAll(port => port is not null
            && port.IsInput && port.InterfaceKind == GraphInterfaceKind.Logic);
        if (enabled)
        {
            candidate.Ports.Add(new GraphPort(
                CanonicalTaskObjectiveSchema.PrerequisitePortId,
                CanonicalTaskObjectiveSchema.PrerequisiteDisplayName,
                true,
                GraphInterfaceKind.Logic,
                0));
        }

        var shapeIssues = AllowUnselectedObjectiveTarget(candidate,
            GraphNodeShapeValidator.Validate(candidate, GraphScope.Task));
        if (shapeIssues.Count != 0) return Fail(shapeIssues);

        var retainedInputIds = candidate.Ports.Where(port => port is not null
                && port.IsInput && port.InterfaceKind == GraphInterfaceKind.Logic)
            .Select(port => port.Id).ToHashSet(StringComparer.Ordinal);
        oldInputIds.ExceptWith(retainedInputIds);
        var before = DeepClone(Document);
        node.Properties = candidate.Properties;
        node.Ports = candidate.Ports;
        if (oldInputIds.Count != 0)
        {
            Document.Connections = (Document.Connections ?? []).Where(connection => connection is not null
                && !((string.Equals(connection.FromNodeId, node.Id, StringComparison.Ordinal)
                        && oldInputIds.Contains(connection.FromPortId))
                    || (string.Equals(connection.ToNodeId, node.Id, StringComparison.Ordinal)
                        && oldInputIds.Contains(connection.ToPortId))))
                .ToList();
        }
        Commit(before);
        return true;
    }

    public bool ChangeStoryActionType(string nodeId, string? type, string? itemId = null)
    {
        if (Scope != GraphScope.StoryFlow)
            return Fail([new("graph.story.action.scope.required",
                "Action editing requires a Story Flow graph.", "scope", NodeId: NullIfBlank(nodeId))]);
        if (!TryResolvePropertyNode(nodeId, out var node, out var issues)) return Fail(issues);
        if (!string.Equals(node!.Type, CanonicalStoryActionSchema.NodeType, StringComparison.Ordinal))
            return Fail([new("graph.story.action.node.required", $"Node '{nodeId}' is not a Story action.",
                "type", NodeId: node.Id)]);
        if (node.Properties.TryGetValue(CanonicalStoryActionSchema.TypeProperty, out var currentType)
            && currentType.ValueKind == JsonValueKind.String
            && string.Equals(currentType.GetString(), type, StringComparison.Ordinal)) return true;

        var candidate = Clone(node);
        if (!CanonicalStoryActionSchema.TryInitializeType(candidate, type, out var typeIssues))
            return Fail(typeIssues);
        if (currentType.ValueKind == JsonValueKind.String)
            _actionDrafts[(nodeId, currentType.GetString()!)] = node.Properties.ToDictionary(p => p.Key, p => p.Value.Clone());
        if (_actionDrafts.TryGetValue((nodeId, type!), out var draft))
            candidate.Properties = draft.ToDictionary(p => p.Key, p => p.Value.Clone());
        if (string.Equals(type, CanonicalStoryActionSchema.GiveItem, StringComparison.Ordinal)
            && !string.IsNullOrWhiteSpace(itemId))
            candidate.Properties[CanonicalStoryActionSchema.ItemProperty] =
                JsonSerializer.SerializeToElement(itemId.Trim());
        var shapeIssues = CanonicalStoryActionSchema.AllowDraftIssues(candidate, GraphNodeShapeValidator.Validate(candidate, GraphScope.StoryFlow));
        if (shapeIssues.Count != 0) return Fail(shapeIssues);
        var before = DeepClone(Document);
        node.Properties = candidate.Properties;
        Commit(before);
        return true;
    }

    public bool SetSessionMusic(string nodeId, string? mediaRef)
    {
        if (Scope != GraphScope.Session || !TryResolvePropertyNode(nodeId, out var node, out _) || node!.Type != "music") return false;
        var candidate = Clone(node);
        candidate.Properties["operation"] = JsonSerializer.SerializeToElement(mediaRef is null ? "stop" : "play");
        candidate.Properties["media_ref"] = JsonSerializer.SerializeToElement(mediaRef);
        var issues = CanonicalSessionPresentationSchema.Validate(candidate);
        if (issues.Count != 0) return Fail(issues);
        var before = DeepClone(Document); node.Properties = candidate.Properties; Commit(before); return true;
    }

    public bool ChangeSessionSpeaker(string nodeId, string? actorId)
    {
        if (Scope != GraphScope.Session || !TryResolvePropertyNode(nodeId, out var node, out _) || node!.Type != "line") return false;
        var next = string.IsNullOrWhiteSpace(actorId) ? null : actorId;
        var previous = node.Properties.TryGetValue("speaker_actor_id", out var old) && old.ValueKind == JsonValueKind.String ? old.GetString() : null;
        if (previous == next) return true;
        var candidate = Clone(node);
        candidate.Properties["speaker_actor_id"] = JsonSerializer.SerializeToElement(next);
        candidate.Properties.Remove("portrait_variant");
        var issues = CanonicalSessionLineSchema.Validate(candidate);
        if (issues.Count != 0) return Fail(issues);
        var before = DeepClone(Document); node.Properties = candidate.Properties; Commit(before); return true;
    }

    public bool ChangeStoryBuffMode(string nodeId, bool modExtension)
    {
        if (Scope != GraphScope.StoryFlow || !TryResolvePropertyNode(nodeId, out var node, out _)
            || node!.Type != "action" || !node.Properties.TryGetValue("action_type", out var type)
            || type.GetString() != CanonicalStoryActionSchema.GiveBuff) return false;
        if (node.Properties["mod_extension"].GetBoolean() == modExtension) return true;
        var candidate = Clone(node);
        candidate.Properties["mod_extension"] = JsonSerializer.SerializeToElement(modExtension);
        candidate.Properties.Remove("buff"); candidate.Properties.Remove("mod_id"); candidate.Properties.Remove("buff_name");
        if (modExtension)
        {
            candidate.Properties["mod_id"] = JsonSerializer.SerializeToElement("");
            candidate.Properties["buff_name"] = JsonSerializer.SerializeToElement("");
        }
        else candidate.Properties["buff"] = JsonSerializer.SerializeToElement("speed");
        var issues = CanonicalStoryActionSchema.AllowDraftIssues(candidate, CanonicalStoryActionSchema.Validate(candidate));
        if (issues.Count != 0) return Fail(issues);
        var before = DeepClone(Document); node.Properties = candidate.Properties; Commit(before); return true;
    }

    /// <summary>Atomically removes one existing untyped node property.</summary>
    public bool RemoveNodeProperty(string nodeId, string property)
    {
        if (string.IsNullOrWhiteSpace(property))
            return Fail([new("graph.node.property.key.required", "Node property key is required.", "property", NodeId: NullIfBlank(nodeId))]);
        if (!TryResolvePropertyNode(nodeId, out var node, out var issues)) return Fail(issues);
        var isPublicBoundary = node!.Type is "logic_input" or "logic_output" or "terminate"
            || (Scope == GraphScope.Session && string.Equals(node.Type, "end", StringComparison.Ordinal));
        if (Scope == GraphScope.Task && string.Equals(node!.Type, CanonicalTaskObjectiveSchema.NodeType, StringComparison.Ordinal))
            return Fail([ObjectivePropertyIssue("graph.objective.property.immutable",
                "Task Objective properties are a frozen typed contract and cannot be removed directly.", property, node.Id)]);
        if (Scope == GraphScope.Task
            && node!.Type is ("logic_input" or "logic_output")
            && property is ("port_id" or "display_name"))
        {
            return Fail([property == "port_id"
                ? TaskLogicOutputPortIdImmutableIssue(node.Id)
                : TaskLogicOutputDisplayNameImmutableIssue(node.Id)]);
        }
        if (isPublicBoundary && property is ("port_id" or "display_name"))
        {
            return Fail([new("graph.public_boundary.property.immutable",
                "Public boundary port_id and display_name are required stable boundary metadata.",
                $"properties.{property}", NodeId: node.Id)]);
        }
        if (node!.Properties is null || !node.Properties.ContainsKey(property))
            return Fail([new("graph.node.property.missing", $"Node property '{property}' does not exist.", "property", NodeId: node.Id)]);

        var before = DeepClone(Document);
        node.Properties.Remove(property);
        Commit(before);
        return true;
    }

    /// <summary>
    /// Adds one Session Choice option and its Flow output as one atomic edit.
    /// The option and Flow IDs are opaque and never label-derived.
    /// </summary>
    public bool AddSessionChoiceOption(string nodeId, string displayText)
    {
        if (!TryResolveSessionChoice(nodeId, out var node, out var options, out var issues))
            return Fail(issues);
        if (string.IsNullOrWhiteSpace(displayText))
            return Fail([ChoiceIssue("graph.session.choice.display_text.required",
                "Session Choice display text is required.", "display_text", nodeId)]);

        var optionId = AllocateDynamicPortId();
        var flowPortId = AllocateDynamicPortId();
        if (optionId is null || flowPortId is null)
            return Fail([ChoiceIssue("graph.session.choice.port_id.unavailable",
                "Two unique opaque IDs are required for a Session Choice option.", "ports", nodeId)]);

        var before = DeepClone(Document);
        options!.Add(new(optionId, displayText, flowPortId, HasLegacyLogicOutput: false));
        ApplySessionChoiceOptions(node!, options);
        return CommitValidatedChoice(before, node!);
    }

    /// <summary>Renames one option without changing either stable output ID.</summary>
    public bool RenameSessionChoiceOption(string nodeId, string optionId, string displayText)
    {
        if (!TryResolveSessionChoice(nodeId, out var node, out var options, out var issues))
            return Fail(issues);
        if (string.IsNullOrWhiteSpace(displayText))
            return Fail([ChoiceIssue("graph.session.choice.display_text.required",
                "Session Choice display text is required.", "display_text", nodeId)]);
        var index = options!.FindIndex(option => string.Equals(option.OptionId, optionId, StringComparison.Ordinal));
        if (index < 0)
            return Fail([ChoiceIssue("graph.session.choice.option.missing",
                $"Session Choice option '{optionId}' does not exist.", "option_id", nodeId)]);
        if (string.Equals(options[index].DisplayText, displayText, StringComparison.Ordinal)) return Fail([]);

        var before = DeepClone(Document);
        options[index] = options[index] with { DisplayText = displayText };
        ApplySessionChoiceOptions(node!, options);
        return CommitValidatedChoice(before, node!);
    }

    /// <summary>Moves one option and its retained outputs to the same zero-based order.</summary>
    public bool ReorderSessionChoiceOption(string nodeId, string optionId, int order)
    {
        if (!TryResolveSessionChoice(nodeId, out var node, out var options, out var issues))
            return Fail(issues);
        if (order < 0 || order >= options!.Count)
            return Fail([ChoiceIssue("graph.session.choice.order.invalid",
                "Session Choice option order is outside the current option list.", "order", nodeId)]);
        var index = options.FindIndex(option => string.Equals(option.OptionId, optionId, StringComparison.Ordinal));
        if (index < 0)
            return Fail([ChoiceIssue("graph.session.choice.option.missing",
                $"Session Choice option '{optionId}' does not exist.", "option_id", nodeId)]);
        if (index == order) return Fail([]);

        var before = DeepClone(Document);
        var moved = options[index];
        options.RemoveAt(index);
        options.Insert(order, moved);
        ApplySessionChoiceOptions(node!, options);
        return CommitValidatedChoice(before, node!);
    }

    /// <summary>
    /// Removes one option and its Flow output plus any retained legacy Logic
    /// output. References require explicit confirmation and are cleaned in the
    /// same Undo unit.
    /// </summary>
    public bool RemoveSessionChoiceOption(
        string nodeId,
        string optionId,
        bool confirmReferencedRemoval = false)
    {
        if (!TryResolveSessionChoice(nodeId, out var node, out var options, out var issues))
            return Fail(issues);
        if (options!.Count <= 1)
            return Fail([ChoiceIssue("graph.session.choice.options.minimum",
                "Session Choice requires at least one option.", "options", nodeId)]);
        var index = options.FindIndex(option => string.Equals(option.OptionId, optionId, StringComparison.Ordinal));
        if (index < 0)
            return Fail([ChoiceIssue("graph.session.choice.option.missing",
                $"Session Choice option '{optionId}' does not exist.", "option_id", nodeId)]);

        var option = options[index];
        var removedPortIds = new HashSet<string>([option.OptionId, option.FlowPortId], StringComparer.Ordinal);
        var references = (Document.Connections ?? []).Where(connection => connection is not null
            && ((string.Equals(connection.FromNodeId, nodeId, StringComparison.Ordinal)
                    && removedPortIds.Contains(connection.FromPortId))
                || (string.Equals(connection.ToNodeId, nodeId, StringComparison.Ordinal)
                    && removedPortIds.Contains(connection.ToPortId))))
            .ToArray();
        if (references.Length != 0 && !confirmReferencedRemoval)
            return Fail([ChoiceIssue("graph.session.choice.references.confirmation_required",
                $"Session Choice option '{optionId}' is referenced by {references.Length} connection(s); confirm removal to clean them up.",
                "confirm_referenced_removal", nodeId)]);

        var before = DeepClone(Document);
        options.RemoveAt(index);
        ApplySessionChoiceOptions(node!, options);
        if (references.Length != 0)
        {
            Document.Connections = (Document.Connections ?? []).Where(connection => connection is not null
                && !((string.Equals(connection.FromNodeId, nodeId, StringComparison.Ordinal)
                        && removedPortIds.Contains(connection.FromPortId))
                    || (string.Equals(connection.ToNodeId, nodeId, StringComparison.Ordinal)
                        && removedPortIds.Contains(connection.ToPortId))))
                .ToList();
        }
        return CommitValidatedChoice(before, node!);
    }

    /// <summary>
    /// Adds one port in a canonical dynamic role.  The generated ID is opaque
    /// and is never derived from the display label.
    /// </summary>
    public bool AddDynamicPort(string nodeId, string displayName, GraphPortDirection direction,
        GraphInterfaceKind interfaceKind)
        => AddDynamicPortCore(nodeId, displayName, direction, interfaceKind);

    public bool AddDynamicPort(string nodeId, string displayName, bool isInput,
        GraphInterfaceKind interfaceKind)
        => AddDynamicPortCore(nodeId, displayName,
            isInput ? GraphPortDirection.Input : GraphPortDirection.Output, interfaceKind);

    public bool AddDynamicPort(string nodeId, string displayName,
        GraphInterfaceKind interfaceKind, bool isInput)
        => AddDynamicPort(nodeId, displayName, isInput, interfaceKind);

    public bool AddDynamicPort(string nodeId, bool isInput,
        GraphInterfaceKind interfaceKind, string displayName)
        => AddDynamicPort(nodeId, displayName, isInput, interfaceKind);

    public bool AddDynamicPort(string nodeId, GraphPortDirection direction,
        GraphInterfaceKind interfaceKind, string displayName)
        => AddDynamicPortCore(nodeId, displayName, direction, interfaceKind);

    public bool AddDynamicPort(string nodeId, GraphInterfaceKind interfaceKind,
        bool isInput, string displayName)
        => AddDynamicPortCore(nodeId, displayName,
            isInput ? GraphPortDirection.Input : GraphPortDirection.Output, interfaceKind);

    /// <summary>Adds a port when the node has exactly one canonical role.</summary>
    public bool AddDynamicPort(string nodeId, string displayName, GraphInterfaceKind interfaceKind)
        => AddDynamicPortInferred(nodeId, displayName, interfaceKind, null);

    /// <summary>Adds a port when the node has exactly one canonical role.</summary>
    public bool AddDynamicPort(string nodeId, string displayName)
        => AddDynamicPortInferred(nodeId, displayName, null, null);

    /// <summary>
    /// Reports every incident edge without changing the document or history.
    /// Returned connections are copies so callers cannot mutate the document
    /// through a read-only inspection operation.
    /// </summary>
    public IReadOnlyList<GraphConnection> GetPortReferences(string nodeId, string portId)
        => (Document.Connections ?? [])
            .Where(connection => connection is not null
                && ((string.Equals(connection.FromNodeId, nodeId, StringComparison.Ordinal)
                        && string.Equals(connection.FromPortId, portId, StringComparison.Ordinal))
                    || (string.Equals(connection.ToNodeId, nodeId, StringComparison.Ordinal)
                        && string.Equals(connection.ToPortId, portId, StringComparison.Ordinal))))
            .Select(Clone)
            .ToArray();

    /// <summary>
    /// Removes a dynamic port.  Referenced ports require explicit confirmation;
    /// confirmed removal removes the port and all incident edges atomically.
    /// </summary>
    public bool RemoveDynamicPort(string nodeId, string portId, bool confirmReferencedRemoval = false)
    {
        if (!TryResolveDynamicPort(nodeId, portId, out var node, out var port, out var role, out var issues))
            return Fail(issues);
        if (!role!.UserEditable)
            return Fail([ReadOnlyDynamicPortIssue(node!.Id, role)]);

        var roleCount = CountRolePorts(node!, role!);
        if (roleCount <= role!.MinimumCount)
        {
            return Fail([new("graph.dynamic_port.minimum",
                $"Dynamic role '{role.NodeType}/{role.InterfaceKind}/{role.Direction}' requires at least {role.MinimumCount} port(s).",
                "port_id", NodeId: node!.Id)]);
        }

        var references = GetPortReferences(node!.Id, port!.Id);
        if (references.Count != 0 && !confirmReferencedRemoval)
        {
            return Fail([new("graph.dynamic_port.references.confirmation_required",
                $"Port '{port.Id}' is referenced by {references.Count} connection(s); confirm removal to clean them up.",
                "confirm_referenced_removal", NodeId: node.Id)]);
        }

        var before = DeepClone(Document);
        node.Ports.Remove(port);
        ReindexDynamicRole(node, role);
        if (references.Count != 0)
        {
            Document.Connections = (Document.Connections ?? [])
                .Where(connection => connection is not null
                    && !((string.Equals(connection.FromNodeId, node.Id, StringComparison.Ordinal)
                            && string.Equals(connection.FromPortId, port.Id, StringComparison.Ordinal))
                        || (string.Equals(connection.ToNodeId, node.Id, StringComparison.Ordinal)
                            && string.Equals(connection.ToPortId, port.Id, StringComparison.Ordinal))))
                .ToList();
        }

        Commit(before);
        return true;
    }

    /// <summary>Atomically adds one typed Story Start trigger slot.</summary>
    public bool AddStoryStartTrigger(string nodeId, string displayName, string triggerType,
        IReadOnlyDictionary<string, JsonElement>? triggerProperties = null)
    {
        if (!TryResolveStoryStart(nodeId, out var node, out var slots, out var issues))
            return Fail(issues);
        if (string.IsNullOrWhiteSpace(displayName))
            return Fail([StoryStartIssue("graph.story.start.trigger.display_name.required", "Story Start trigger display_name is required.", "display_name", nodeId)]);
        if (!StoryStartSchema.SupportedTriggerTypes.Contains(triggerType ?? string.Empty, StringComparer.Ordinal))
            return Fail([StoryStartIssue("graph.story.start.trigger.type.unsupported", $"Unsupported Story Start trigger type '{triggerType}'.", "trigger_type", nodeId)]);
        var id = AllocateDynamicPortId();
        if (id is null)
            return Fail([StoryStartIssue("graph.story.start.trigger.port_id.unavailable", "No opaque Story Start trigger port ID was available.", "port_id", nodeId)]);

        var properties = triggerProperties?.ToDictionary(item => item.Key, item => item.Value.Clone(), StringComparer.Ordinal)
            ?? StoryStartSchema.DefaultTriggerProperties(triggerType!);
        var logicPortId = string.Equals(triggerType, StoryStartSchema.Logic, StringComparison.Ordinal)
            ? AllocateDynamicPortId() : null;
        if (string.Equals(triggerType, StoryStartSchema.Logic, StringComparison.Ordinal) && logicPortId is null)
            return Fail([StoryStartIssue("graph.story.start.trigger.logic_port_id.unavailable",
                "No opaque Story Start Logic condition port ID was available.", "logic_port_id", nodeId)]);
        var next = slots!.Select(slot => new StoryStartTriggerSlot(slot.PortId, slot.DisplayName,
            slot.TriggerType, slot.TriggerProperties.Clone(), slot.Order, slot.LogicPortId)).ToList();
        next.Add(new StoryStartTriggerSlot(id, displayName.Trim(), triggerType!,
            JsonSerializer.SerializeToElement(properties), next.Count, logicPortId));
        var before = DeepClone(Document);
        ApplyStoryStartSlots(node!, next);
        return CommitValidatedStoryStart(before, node!);
    }

    /// <summary>Atomically changes a trigger type and its complete payload.</summary>
    public bool SetStoryStartTrigger(string nodeId, string portId, string triggerType,
        IReadOnlyDictionary<string, JsonElement>? triggerProperties = null)
    {
        if (!TryResolveStoryStart(nodeId, out var node, out var slots, out var issues)) return Fail(issues);
        if (!StoryStartSchema.SupportedTriggerTypes.Contains(triggerType ?? string.Empty, StringComparer.Ordinal))
            return Fail([StoryStartIssue("graph.story.start.trigger.type.unsupported",
                $"Unsupported Story Start trigger type '{triggerType}'.", "trigger_type", nodeId)]);
        var index = slots!.FindIndex(slot => string.Equals(slot.PortId, portId, StringComparison.Ordinal));
        if (index < 0)
            return Fail([StoryStartIssue("graph.story.start.trigger.port.missing",
                $"Story Start trigger port '{portId}' does not exist.", "port_id", nodeId)]);
        var properties = triggerProperties?.ToDictionary(item => item.Key, item => item.Value.Clone(), StringComparer.Ordinal)
            ?? StoryStartSchema.DefaultTriggerProperties(triggerType!);
        var logicPortId = string.Equals(triggerType, StoryStartSchema.Logic, StringComparison.Ordinal)
            ? slots[index].LogicPortId ?? AllocateDynamicPortId()
            : null;
        if (string.Equals(triggerType, StoryStartSchema.Logic, StringComparison.Ordinal) && logicPortId is null)
            return Fail([StoryStartIssue("graph.story.start.trigger.logic_port_id.unavailable",
                "No opaque Story Start Logic condition port ID was available.", "logic_port_id", nodeId)]);
        var next = slots[index] with
        {
            TriggerType = triggerType!,
            TriggerProperties = JsonSerializer.SerializeToElement(properties),
            LogicPortId = logicPortId,
        };
        if (string.Equals(slots[index].TriggerType, next.TriggerType, StringComparison.Ordinal)
            && JsonElement.DeepEquals(slots[index].TriggerProperties, next.TriggerProperties)
            && string.Equals(slots[index].LogicPortId, next.LogicPortId, StringComparison.Ordinal))
            return Fail([]);
        var before = DeepClone(Document);
        slots[index] = next;
        ApplyStoryStartSlots(node!, slots);
        return CommitValidatedStoryStart(before, node!);
    }

    public bool SetStoryStartTriggerType(string nodeId, string portId, string triggerType,
        string? actorId = null)
        => SetStoryStartTrigger(nodeId, portId, triggerType,
            StoryStartSchema.DefaultTriggerProperties(triggerType, actorId));

    public bool SetStoryStartTriggerProperties(string nodeId, string portId,
        IReadOnlyDictionary<string, JsonElement> triggerProperties)
    {
        if (!TryResolveStoryStart(nodeId, out var node, out var slots, out var issues)) return Fail(issues);
        var slot = slots!.FirstOrDefault(candidate => string.Equals(candidate.PortId, portId, StringComparison.Ordinal));
        if (slot is null)
            return Fail([StoryStartIssue("graph.story.start.trigger.port.missing",
                $"Story Start trigger port '{portId}' does not exist.", "port_id", nodeId)]);
        return SetStoryStartTrigger(nodeId, portId, slot.TriggerType, triggerProperties);
    }

    public bool UpdateStoryStartTrigger(string nodeId, string portId, string triggerType,
        IReadOnlyDictionary<string, JsonElement>? triggerProperties = null)
        => SetStoryStartTrigger(nodeId, portId, triggerType, triggerProperties);

    public bool AddStoryStartTriggerSlot(string nodeId, string displayName, string triggerType,
        IReadOnlyDictionary<string, JsonElement>? triggerProperties = null)
        => AddStoryStartTrigger(nodeId, displayName, triggerType, triggerProperties);

    /// <summary>Renames a trigger without changing its opaque port identity.</summary>
    public bool RenameStoryStartTrigger(string nodeId, string portId, string displayName)
    {
        if (!TryResolveStoryStart(nodeId, out var node, out var slots, out var issues)) return Fail(issues);
        if (string.IsNullOrWhiteSpace(displayName))
            return Fail([StoryStartIssue("graph.story.start.trigger.display_name.required", "Story Start trigger display_name is required.", "display_name", nodeId)]);
        var index = slots!.FindIndex(slot => string.Equals(slot.PortId, portId, StringComparison.Ordinal));
        if (index < 0) return Fail([StoryStartIssue("graph.story.start.trigger.port.missing", $"Story Start trigger port '{portId}' does not exist.", "port_id", nodeId)]);
        if (string.Equals(slots[index].DisplayName, displayName.Trim(), StringComparison.Ordinal)) return Fail([]);
        var before = DeepClone(Document);
        slots[index] = slots[index] with { DisplayName = displayName.Trim() };
        ApplyStoryStartSlots(node!, slots);
        return CommitValidatedStoryStart(before, node!);
    }

    public bool RenameStoryStartTriggerSlot(string nodeId, string portId, string displayName)
        => RenameStoryStartTrigger(nodeId, portId, displayName);

    /// <summary>Reorders metadata and projected Flow ports as one edit.</summary>
    public bool ReorderStoryStartTrigger(string nodeId, string portId, int order)
    {
        if (!TryResolveStoryStart(nodeId, out var node, out var slots, out var issues)) return Fail(issues);
        if (order < 0 || order >= slots!.Count)
            return Fail([StoryStartIssue("graph.story.start.trigger.order.invalid", "Story Start trigger order is outside the current trigger list.", "order", nodeId)]);
        var index = slots.FindIndex(slot => string.Equals(slot.PortId, portId, StringComparison.Ordinal));
        if (index < 0) return Fail([StoryStartIssue("graph.story.start.trigger.port.missing", $"Story Start trigger port '{portId}' does not exist.", "port_id", nodeId)]);
        if (index == order) return Fail([]);
        var before = DeepClone(Document);
        var moved = slots[index]; slots.RemoveAt(index); slots.Insert(order, moved);
        slots = slots.Select((slot, position) => slot with { Order = position }).ToList();
        ApplyStoryStartSlots(node!, slots);
        return CommitValidatedStoryStart(before, node!);
    }

    public bool ReorderStoryStartTriggerSlot(string nodeId, string portId, int order)
        => ReorderStoryStartTrigger(nodeId, portId, order);

    /// <summary>Removes a trigger; referenced removal is explicitly confirmed.</summary>
    public bool RemoveStoryStartTrigger(string nodeId, string portId, bool confirmReferencedRemoval = false)
    {
        if (!TryResolveStoryStart(nodeId, out var node, out var slots, out var issues)) return Fail(issues);
        if (slots!.Count <= 1)
            return Fail([StoryStartIssue("graph.story.start.triggers.minimum", "Story Start requires at least one trigger.", "triggers", nodeId)]);
        var index = slots.FindIndex(slot => string.Equals(slot.PortId, portId, StringComparison.Ordinal));
        if (index < 0) return Fail([StoryStartIssue("graph.story.start.trigger.port.missing", $"Story Start trigger port '{portId}' does not exist.", "port_id", nodeId)]);
        var references = GetPortReferences(nodeId, portId);
        if (references.Count != 0 && !confirmReferencedRemoval)
            return Fail([StoryStartIssue("graph.story.start.trigger.references.confirmation_required", $"Story Start trigger '{portId}' is referenced by {references.Count} connection(s); confirm removal to clean them up.", "confirm_referenced_removal", nodeId)]);
        var before = DeepClone(Document);
        slots.RemoveAt(index);
        slots = slots.Select((slot, position) => slot with { Order = position }).ToList();
        ApplyStoryStartSlots(node!, slots);
        if (references.Count != 0)
            Document.Connections = (Document.Connections ?? []).Where(connection => connection is not null
                && !((string.Equals(connection.FromNodeId, nodeId, StringComparison.Ordinal) && string.Equals(connection.FromPortId, portId, StringComparison.Ordinal))
                    || (string.Equals(connection.ToNodeId, nodeId, StringComparison.Ordinal) && string.Equals(connection.ToPortId, portId, StringComparison.Ordinal)))).ToList();
        return CommitValidatedStoryStart(before, node!);
    }

    public bool RemoveStoryStartTriggerSlot(string nodeId, string portId, bool confirmReferencedRemoval = false)
        => RemoveStoryStartTrigger(nodeId, portId, confirmReferencedRemoval);

    /// <summary>Sets the enabled minimum repeat policy for Story Start.</summary>
    public bool SetStoryStartRepeatPolicy(string nodeId, string repeatPolicy)
    {
        if (!TryResolveStoryStart(nodeId, out var node, out _, out var issues)) return Fail(issues);
        if (!StoryStartSchema.SupportedRepeatPolicies.Contains(repeatPolicy ?? string.Empty, StringComparer.Ordinal))
            return Fail([StoryStartIssue("graph.story.start.repeat_policy.unsupported", "Only once and repeatable repeat policies are enabled.", "repeat_policy", nodeId)]);
        var before = DeepClone(Document);
        node!.Properties[StoryStartSchema.RepeatPolicyProperty] = JsonSerializer.SerializeToElement(repeatPolicy);
        return CommitValidatedStoryStart(before, node);
    }

    public bool SetStoryStartRepeatMode(string nodeId, string repeatPolicy)
        => SetStoryStartRepeatPolicy(nodeId, repeatPolicy);

    private bool TryResolveStoryStart(string nodeId, out GraphNode? node,
        out List<StoryStartTriggerSlot>? slots, out IReadOnlyList<ValidationIssue> issues)
    {
        node = null; slots = null;
        if (Scope != GraphScope.StoryFlow)
        { issues = [new("graph.story.start.scope.required", "Story Start authoring requires a Story Flow graph.", "scope")]; return false; }
        var matches = (Document.Nodes ?? []).Where(item => item is not null && string.Equals(item.Id, nodeId, StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1)
        { issues = [new("graph.story.start.node.missing", $"Story Start node '{nodeId}' does not exist uniquely.", "node_id", NodeId: nodeId)]; return false; }
        if (!string.Equals(matches[0].Type, "start", StringComparison.Ordinal))
        { issues = [new("graph.story.start.node.required", $"Node '{nodeId}' is not a Story Start node.", "type", NodeId: nodeId)]; return false; }
        node = matches[0];
        slots = StoryStartSchema.ReadTriggers(node).ToList();
        if (slots.Count == 0)
        { issues = [new("graph.story.start.triggers.required", "Story Start trigger metadata is required.", "triggers", NodeId: nodeId)]; return false; }
        issues = []; return true;
    }

    private static void ApplyStoryStartSlots(GraphNode node, IReadOnlyList<StoryStartTriggerSlot> slots)
    {
        node.Properties[StoryStartSchema.TriggersProperty] = JsonSerializer.SerializeToElement(slots.Select(slot =>
        {
            var trigger = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["port_id"] = slot.PortId,
                ["display_name"] = slot.DisplayName,
                ["trigger_type"] = slot.TriggerType,
                ["trigger_properties"] = slot.TriggerProperties.Clone(),
                ["order"] = slot.Order,
            };
            if (slot.LogicPortId is not null)
                trigger[StoryStartSchema.LogicPortIdProperty] = slot.LogicPortId;
            return trigger;
        }).ToArray());
        node.Ports.RemoveAll(port => port is not null && port.IsOutput && port.InterfaceKind == GraphInterfaceKind.Flow);
        node.Ports.RemoveAll(port => port is not null && port.IsInput && port.InterfaceKind == GraphInterfaceKind.Logic);
        foreach (var slot in slots.OrderBy(item => item.Order))
        {
            node.Ports.Add(new GraphPort(slot.PortId, slot.DisplayName, false, GraphInterfaceKind.Flow, slot.Order));
            if (slot.LogicPortId is not null)
                node.Ports.Add(new GraphPort(slot.LogicPortId, $"条件：{slot.DisplayName}", true, GraphInterfaceKind.Logic, slot.Order));
        }
    }

    private bool CommitValidatedStoryStart(GraphDocument before, GraphNode node)
    {
        var validation = StoryStartSchema.Validate(node, compatibilityMode: false);
        if (validation.Count != 0)
        { ReplaceContents(before); return Fail(validation); }
        Commit(before); return true;
    }

    private static ValidationIssue StoryStartIssue(string code, string message, string field, string nodeId)
        => new(code, message, field, NodeId: string.IsNullOrWhiteSpace(nodeId) ? null : nodeId);

    private bool AddDynamicPortInferred(string nodeId, string displayName,
        GraphInterfaceKind? interfaceKind, GraphPortDirection? direction)
    {
        if (!TryResolveNode(nodeId, out var node, out var issues))
            return Fail(issues);
        if (!Scope.HasValue)
            return Fail([new("graph.dynamic_port.scope.required",
                "A graph scope is required for dynamic-port edits.", "scope", NodeId: NullIfBlank(nodeId))]);

        var roles = GraphDynamicPortPolicy.ForNode(Scope.Value, node!.Type)
            .Where(role => (!interfaceKind.HasValue || role.InterfaceKind == interfaceKind.Value)
                && (!direction.HasValue || role.Direction == direction.Value))
            .ToArray();
        if (roles.Length != 0 && roles.All(role => !role.UserEditable))
            return Fail([ReadOnlyDynamicPortIssue(node.Id, roles[0])]);
        if (roles.Length != 1)
        {
            return Fail([new("graph.dynamic_port.role.required",
                $"Node type '{node.Type}' does not have one unambiguous dynamic-port role for the requested kind.",
                "role", NodeId: node.Id)]);
        }

        return AddDynamicPortCore(nodeId, displayName, roles[0].Direction, roles[0].InterfaceKind);
    }

    private bool AddDynamicPortCore(string nodeId, string displayName,
        GraphPortDirection direction, GraphInterfaceKind interfaceKind)
    {
        if (!TryResolveNode(nodeId, out var node, out var nodeIssues))
            return Fail(nodeIssues);
        if (!Scope.HasValue)
            return Fail([new("graph.dynamic_port.scope.required",
                "A graph scope is required for dynamic-port edits.", "scope", NodeId: NullIfBlank(nodeId))]);
        if (!GraphDynamicPortPolicy.TryGetRole(Scope.Value, node!.Type, direction, interfaceKind, out var role))
        {
            return Fail([new("graph.dynamic_port.role.disallowed",
                $"Node type '{node.Type}' cannot add a {interfaceKind} {direction.ToString().ToLowerInvariant()} dynamic port in scope '{Scope.Value}'.",
                "role", NodeId: node.Id)]);
        }
        if (!role.UserEditable)
            return Fail([ReadOnlyDynamicPortIssue(node.Id, role)]);
        if (string.IsNullOrWhiteSpace(displayName))
            return Fail([new("graph.dynamic_port.label.required", "Dynamic port display name is required.", "display_name", NodeId: node.Id)]);
        if ((node.Ports ?? []).Any(port => port is not null
            && string.Equals(port.DisplayName, displayName, StringComparison.Ordinal)))
        {
            return Fail([new("graph.dynamic_port.label.duplicate",
                $"Display name '{displayName}' is already used on node '{node.Id}'.", "display_name", NodeId: node.Id)]);
        }
        if (Scope == GraphScope.Task && string.Equals(node.Type, "settle", StringComparison.Ordinal)
            && IsTaskPublicDisplayNameUsed(node.Id, displayName))
        {
            return Fail([TaskPublicDisplayNameDuplicateIssue(node.Id, displayName)]);
        }

        var usedOrders = (node.Ports ?? [])
            .Where(port => port is not null && MatchesRole(port, role))
            .Select(port => port.Order)
            .ToHashSet();
        var order = 0;
        while (usedOrders.Contains(order)) order++;

        var portId = Scope == GraphScope.Task
            && string.Equals(node.Type, "settle", StringComparison.Ordinal)
            ? AllocateTaskSettlePortId()
            : AllocateDynamicPortId();
        if (portId is null)
            return Fail([new("graph.dynamic_port.port_id.unavailable",
                "No unique generated dynamic port ID was available.", "port_id", NodeId: node.Id)]);

        var before = DeepClone(Document);
        node.Ports ??= [];
        node.Ports.Add(new GraphPort(portId, displayName, role.IsInput, role.InterfaceKind, order));
        Commit(before);
        return true;
    }

    private bool TryResolveDynamicPort(string nodeId, string portId, out GraphNode? node,
        out GraphPort? port, out GraphDynamicPortRole? role, out IReadOnlyList<ValidationIssue> issues)
    {
        node = null;
        port = null;
        role = null;
        if (!TryResolveNode(nodeId, out node, out issues)) return false;
        if (!Scope.HasValue)
        {
            issues = [new("graph.dynamic_port.scope.required",
                "A graph scope is required for dynamic-port edits.", "scope", NodeId: NullIfBlank(nodeId))];
            return false;
        }

        var matches = (node!.Ports ?? []).Where(item => item is not null
            && string.Equals(item.Id, portId, StringComparison.Ordinal)).ToArray();
        if (matches.Length == 0)
        {
            issues = [new("graph.dynamic_port.port.missing",
                $"Port '{portId}' does not exist on node '{node.Id}'.", "port_id", NodeId: node.Id)];
            return false;
        }
        if (matches.Length > 1)
        {
            issues = [new("graph.dynamic_port.port.ambiguous",
                $"Port '{portId}' is ambiguous on node '{node.Id}'.", "port_id", NodeId: node.Id)];
            return false;
        }

        port = matches[0];
        if (!GraphDynamicPortPolicy.TryGetRole(Scope.Value, node.Type,
            port.IsInput ? GraphPortDirection.Input : GraphPortDirection.Output,
            port.InterfaceKind, out var foundRole))
        {
            issues = [new("graph.dynamic_port.fixed",
                $"Port '{port.Id}' is fixed or is not in a dynamic role for node type '{node.Type}'.",
                "port_id", NodeId: node.Id)];
            return false;
        }

        role = foundRole;
        issues = [];
        return true;
    }

    private bool TryResolveNode(string nodeId, out GraphNode? node, out IReadOnlyList<ValidationIssue> issues)
    {
        var matches = (Document.Nodes ?? []).Where(item => item is not null
            && string.Equals(item.Id, nodeId, StringComparison.Ordinal)).ToArray();
        if (matches.Length == 1)
        {
            node = matches[0];
            issues = [];
            return true;
        }

        node = null;
        issues = matches.Length == 0
            ? [new("graph.dynamic_port.node.missing", $"Node '{nodeId}' does not exist.", "node_id", NodeId: NullIfBlank(nodeId))]
            : [new("graph.dynamic_port.node.ambiguous", $"Node '{nodeId}' is ambiguous.", "node_id", NodeId: NullIfBlank(nodeId))];
        return false;
    }

    private bool TryResolvePropertyNode(string nodeId, out GraphNode? node,
        out IReadOnlyList<ValidationIssue> issues)
    {
        var matches = (Document.Nodes ?? []).Where(item => item is not null
            && string.Equals(item.Id, nodeId, StringComparison.Ordinal)).ToArray();
        if (matches.Length == 1)
        {
            node = matches[0];
            issues = [];
            return true;
        }

        node = null;
        issues = matches.Length == 0
            ? [new("graph.node.property.node.missing", $"Node '{nodeId}' does not exist.", "node_id", NodeId: NullIfBlank(nodeId))]
            : [new("graph.node.property.node.ambiguous", $"Node '{nodeId}' is ambiguous.", "node_id", NodeId: NullIfBlank(nodeId))];
        return false;
    }

    private bool TryResolveSessionChoice(
        string nodeId,
        out GraphNode? node,
        out List<SessionChoiceOptionState>? options,
        out IReadOnlyList<ValidationIssue> issues)
    {
        options = null;
        if (Scope != GraphScope.Session)
        {
            node = null;
            issues = [ChoiceIssue("graph.session.choice.scope.required",
                "Session Choice editing requires a Session graph.", "scope", nodeId)];
            return false;
        }
        if (!TryResolveNode(nodeId, out node, out issues)) return false;
        if (!string.Equals(node!.Type, "choice", StringComparison.Ordinal))
        {
            issues = [ChoiceIssue("graph.session.choice.node.required",
                $"Node '{nodeId}' is not a Session Choice.", "type", nodeId)];
            return false;
        }

        var shapeIssues = GraphNodeShapeValidator.Validate(node, GraphScope.Session, CompatibilityMode);
        if (shapeIssues.Count != 0)
        {
            issues = shapeIssues;
            return false;
        }

        var choiceNode = node;
        options = choiceNode.Properties[SessionChoiceSchema.OptionsProperty].EnumerateArray()
            .Select(element => new SessionChoiceOptionState(
                element.GetProperty("option_id").GetString()!,
                element.GetProperty("display_text").GetString()!,
                element.GetProperty("flow_port_id").GetString()!,
                (choiceNode.Ports ?? []).Any(port => port is not null
                    && port.IsOutput
                    && port.InterfaceKind == GraphInterfaceKind.Logic
                    && string.Equals(port.Id, element.GetProperty("option_id").GetString(), StringComparison.Ordinal))))
            .ToList();
        issues = [];
        return true;
    }

    private static void ApplySessionChoiceOptions(GraphNode node, IReadOnlyList<SessionChoiceOptionState> options)
    {
        node.Properties[SessionChoiceSchema.OptionsProperty] = JsonSerializer.SerializeToElement(options.Select(option =>
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["option_id"] = option.OptionId,
                ["display_text"] = option.DisplayText,
                ["flow_port_id"] = option.FlowPortId,
            }).ToArray());
        var flowInput = node.Ports.Single(port => string.Equals(port.Id, "flow_in", StringComparison.Ordinal));
        node.Ports = [Clone(flowInput)];
        for (var index = 0; index < options.Count; index++)
        {
            var option = options[index];
            node.Ports.Add(new(option.FlowPortId, option.DisplayText, false, GraphInterfaceKind.Flow, index));
            if (option.HasLegacyLogicOutput)
                node.Ports.Add(new(option.OptionId, $"已选择：{option.DisplayText}", false, GraphInterfaceKind.Logic, index));
        }
    }

    private bool CommitValidatedChoice(GraphDocument before, GraphNode node)
    {
        var issues = GraphNodeShapeValidator.Validate(node, GraphScope.Session, CompatibilityMode);
        if (issues.Count != 0)
        {
            ReplaceContents(before);
            return Fail(issues);
        }
        Commit(before);
        return true;
    }

    private static ValidationIssue ChoiceIssue(string code, string message, string field, string nodeId)
        => new(code, message, field, NodeId: NullIfBlank(nodeId));

    private static int CountRolePorts(GraphNode node, GraphDynamicPortRole role)
        => (node.Ports ?? []).Count(port => port is not null && MatchesRole(port, role));

    private static void ReindexDynamicRole(GraphNode node, GraphDynamicPortRole role)
    {
        var rolePorts = (node.Ports ?? [])
            .Where(port => port is not null && MatchesRole(port, role))
            .OrderBy(port => port.Order)
            .ThenBy(port => port.Id, StringComparer.Ordinal)
            .ToArray();
        for (var index = 0; index < rolePorts.Length; index++)
            rolePorts[index].Order = index;
    }

    private static bool MatchesRole(GraphPort port, GraphDynamicPortRole role)
        => port.IsInput == role.IsInput && port.InterfaceKind == role.InterfaceKind;

    private bool TryGetDynamicRole(string nodeId, GraphPort port, out GraphDynamicPortRole? role)
    {
        role = null;
        if (!Scope.HasValue) return false;
        var node = (Document.Nodes ?? []).SingleOrDefault(candidate => candidate is not null
            && string.Equals(candidate.Id, nodeId, StringComparison.Ordinal));
        if (node is null || !GraphDynamicPortPolicy.TryGetRole(Scope.Value, node.Type,
            port.IsInput ? GraphPortDirection.Input : GraphPortDirection.Output,
            port.InterfaceKind, out var found))
            return false;
        role = found;
        return true;
    }

    private static bool MatchesDynamicRole(GraphPort candidate, GraphPort target)
        => candidate.IsInput == target.IsInput && candidate.InterfaceKind == target.InterfaceKind;

    private bool IsTaskPublicDisplayNameUsed(string nodeId, string displayName, GraphPort? ignoredPort = null)
    {
        foreach (var node in (Document.Nodes ?? []).Where(candidate => candidate is not null))
        {
            if (string.Equals(node.Type, "settle", StringComparison.Ordinal)
                && (node.Ports ?? []).Any(port => port is not null
                    && !ReferenceEquals(port, ignoredPort)
                    && string.Equals(port.DisplayName, displayName, StringComparison.Ordinal)))
                return true;

            if (!string.Equals(node.Type, "logic_output", StringComparison.Ordinal)
                || string.Equals(node.Id, nodeId, StringComparison.Ordinal))
                continue;
            if ((node.Properties ?? []).TryGetValue("display_name", out var value)
                && value.ValueKind == JsonValueKind.String
                && string.Equals(value.GetString(), displayName, StringComparison.Ordinal))
                return true;
        }

        return false;
    }

    private static ValidationIssue TaskPublicDisplayNameDuplicateIssue(string nodeId, string displayName)
        => new("graph.dynamic_port.label.duplicate",
            $"Public display name '{displayName}' is already used by another Task boundary.",
            "display_name", NodeId: nodeId);

    private static ValidationIssue TaskLogicOutputPortIdImmutableIssue(string nodeId)
        => new("graph.task.logic_output.port_id.immutable",
            "Task logic output public port_id is a stable identity and cannot be changed.",
            "properties.port_id", NodeId: nodeId);

    private static ValidationIssue TaskLogicOutputDisplayNameInvalidIssue(string nodeId)
        => new("graph.task.logic_output.display_name.invalid",
            "Task logic output display_name must be a nonblank JSON string.",
            "properties.display_name", NodeId: nodeId);

    private static ValidationIssue TaskLogicOutputDisplayNameImmutableIssue(string nodeId)
        => new("graph.task.logic_output.display_name.immutable",
            "Task logic output display_name cannot be removed.",
            "properties.display_name", NodeId: nodeId);

    private static ValidationIssue ReadOnlyDynamicPortIssue(string nodeId, GraphDynamicPortRole role)
        => new(
            "graph.dynamic_port.role.read_only",
            $"Dynamic role '{role.NodeType}/{role.InterfaceKind}/{role.Direction}' is projected from a bound resource and cannot be edited directly.",
            "role",
            NodeId: nodeId);

    private static ValidationIssue ObjectiveTypeAtomicIssue(string nodeId)
        => ObjectivePropertyIssue("graph.objective.type.atomic_required",
            "Objective type changes must use the atomic Objective type edit.", CanonicalTaskObjectiveSchema.TypeProperty, nodeId);

    private static ValidationIssue ObjectivePropertyIssue(string code, string message, string field, string? nodeId)
        => new(code, message, $"properties.{field}", NodeId: NullIfBlank(nodeId));

    private static IReadOnlyList<ValidationIssue> AllowUnselectedObjectiveTarget(
        GraphNode node, IReadOnlyList<ValidationIssue> issues)
    {
        return CanonicalTaskObjectiveSchema.AllowDraftIssues(node, issues);
    }

    private static bool IsObjectiveTargetIssue(ValidationIssue issue)
        => issue.Code == "graph.objective.target.invalid"
            && issue.Field is "properties.entity" or "properties.item" or "properties.actor_id";

    private string? AllocateDynamicPortId()
    {
        for (var attempt = 0; attempt < 256; attempt++)
        {
            var candidate = _dynamicPortIdSource();
            if (string.IsNullOrWhiteSpace(candidate)) continue;
            if (!_issuedDynamicPortIds.Add(candidate)) continue;
            if ((Document.Nodes ?? []).SelectMany(node => node?.Ports ?? [])
                .Any(port => port is not null && string.Equals(port.Id, candidate, StringComparison.Ordinal)))
            {
                continue;
            }
            return candidate;
        }
        return null;
    }

    private string? AllocateTaskSettlePortId()
    {
        for (var attempt = 0; attempt < 256; attempt++)
        {
            var candidate = AllocateDynamicPortId();
            if (candidate is null) return null;
            if (candidate is GraphAggregatePortProjection.FlowInputId
                or GraphAggregatePortProjection.LogicInputId)
                continue;
            if (IsTaskPublicPortIdUsed(candidate)) continue;
            return candidate;
        }

        return null;
    }

    private bool IsTaskPublicPortIdUsed(string portId)
        => (Document.Nodes ?? []).Where(node => node is not null).Any(node =>
            (node.Type == "settle" && (node.Ports ?? []).Any(port => port is not null
                && string.Equals(port.Id, portId, StringComparison.Ordinal)))
            || (node.Properties ?? []).TryGetValue("port_id", out var value)
                && value.ValueKind == JsonValueKind.String
                && string.Equals(value.GetString(), portId, StringComparison.Ordinal));

    private string NextDefaultDynamicPortId()
        => $"dynamic_port_{Guid.NewGuid():N}";

    public bool Undo()
    {
        if (_undo.Count == 0) return false;
        var edit = _undo.Pop();
        ReplaceContents(edit.Before);
        _redo.Push(edit);
        _lastValidationIssues = [];
        return true;
    }

    public bool Redo()
    {
        if (_redo.Count == 0) return false;
        var edit = _redo.Pop();
        ReplaceContents(edit.After);
        _undo.Push(edit);
        _lastValidationIssues = [];
        return true;
    }

    /// <summary>
    /// Replaces the retained live document with an externally persisted
    /// snapshot and starts a new edit-history baseline. This is intentionally
    /// not an undoable user edit: repository lifecycle transactions have
    /// already committed the supplied state.
    /// </summary>
    public void ResetToPersistedSnapshot(GraphDocument snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ReplaceContents(snapshot);
        _undo.Clear();
        _redo.Clear();
        _issuedDynamicPortIds.Clear();
        _lastValidationIssues = [];
    }

    /// <summary>
    /// Rolls back the most recently committed edit without turning it into a
    /// user-visible redo operation. Redo history displaced by that commit is
    /// restored, making this suitable for a failed outer persistence transaction.
    /// </summary>
    public bool RollbackLastEdit()
    {
        if (_undo.Count == 0) return false;
        var edit = _undo.Pop();
        ReplaceContents(edit.Before);
        _redo.Clear();
        foreach (var displaced in edit.DisplacedRedo.Reverse())
            _redo.Push(displaced);
        _lastValidationIssues = [];
        return true;
    }

    private GraphPort? FindUniquePort(string nodeId, string portId, out IReadOnlyList<ValidationIssue> issues)
    {
        var nodes = (Document.Nodes ?? []).Where(node => node is not null && node.Id == nodeId).ToArray();
        var result = new List<ValidationIssue>();
        if (nodes.Length == 0)
            result.Add(new("graph.connection.from.node.missing", $"Node '{nodeId}' does not exist.", "node_id", NodeId: NullIfBlank(nodeId)));
        else if (nodes.Length > 1)
            result.Add(new("graph.connection.from.node.ambiguous", $"Node '{nodeId}' is ambiguous.", "node_id", NodeId: NullIfBlank(nodeId)));
        else
        {
            var ports = (nodes[0].Ports ?? []).Where(port => port is not null && port.Id == portId).ToArray();
            if (ports.Length == 0)
                result.Add(new("graph.connection.from.port.missing", $"Port '{portId}' does not exist on node '{nodeId}'.", "port_id", NodeId: nodeId));
            else if (ports.Length > 1)
                result.Add(new("graph.connection.from.port.ambiguous", $"Port '{portId}' is ambiguous on node '{nodeId}'.", "port_id", NodeId: nodeId));
            else
            {
                issues = [];
                return ports[0];
            }
        }
        issues = result;
        return null;
    }

    private bool Fail(IReadOnlyList<ValidationIssue> issues)
    {
        _lastValidationIssues = issues.ToArray();
        return false;
    }

    private void Commit(GraphDocument before)
    {
        var displacedRedo = _redo.ToArray();
        _undo.Push(new EditHistory(before, DeepClone(Document), displacedRedo));
        _redo.Clear();
        _lastValidationIssues = [];
    }

    private void ReplaceContents(GraphDocument snapshot)
    {
        // Never expose an EditHistory-owned object through the live document:
        // callers are allowed to edit the document directly between commands.
        var restored = DeepClone(snapshot);
        Document.Nodes = restored.Nodes;
        Document.Connections = restored.Connections;
    }

    internal static GraphDocument DeepClone(GraphDocument source)
        => new((source.Nodes ?? []).Select(node => node is null ? null! : Clone(node)),
            (source.Connections ?? []).Select(connection => connection is null ? null! : Clone(connection)));

    private static GraphNode Clone(GraphNode source)
        => new(source.Id, source.Type, source.DisplayName,
            (source.Ports ?? []).Select(port => port is null ? null! : Clone(port)),
            (source.Properties ?? []).ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal));

    private static GraphPort Clone(GraphPort source)
        => new(source.Id, source.DisplayName, source.IsInput, source.InterfaceKind, source.Order);

    private static GraphConnection Clone(GraphConnection source)
        => new(source.FromNodeId, source.FromPortId, source.ToNodeId, source.ToPortId, source.InterfaceKind);

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private sealed record EditHistory(
        GraphDocument Before,
        GraphDocument After,
        IReadOnlyList<EditHistory> DisplacedRedo);

    private sealed record SessionChoiceOptionState(
        string OptionId,
        string DisplayText,
        string FlowPortId,
        bool HasLegacyLogicOutput);
}

/// <summary>Short alias for consumers that call the object an editor session.</summary>
public sealed class GraphEditorSession
{
    private readonly GraphEditSession _inner;
    public GraphEditorSession(GraphDocument document, GraphScope? scope = null, bool compatibilityMode = false,
        Func<string>? dynamicPortIdSource = null)
        => _inner = new(document, scope, compatibilityMode, dynamicPortIdSource);
    public GraphDocument Document => _inner.Document;
    public bool CanUndo => _inner.CanUndo;
    public bool CanRedo => _inner.CanRedo;
    public int UndoCount => _inner.UndoCount;
    public int RedoCount => _inner.RedoCount;
    public IReadOnlyList<ValidationIssue> LastValidationIssues => _inner.LastValidationIssues;
    public IReadOnlyList<ValidationIssue> ValidationIssues => _inner.ValidationIssues;
    public CanonicalAggregateSynchronizationPlan AnalyzeAggregateSynchronization(GraphResourceEnvelope resource)
        => _inner.AnalyzeAggregateSynchronization(resource);
    public CanonicalAggregateSynchronizationPlan AnalyzeCanonicalAggregateSynchronization(GraphResourceEnvelope resource)
        => _inner.AnalyzeCanonicalAggregateSynchronization(resource);
    public CanonicalAggregateSynchronizationPlan AnalyzeAggregate(GraphResourceEnvelope resource)
        => _inner.AnalyzeAggregate(resource);
    public bool ApplyAggregateSynchronization(CanonicalAggregateSynchronizationPlan plan, bool confirmReferencedRemoval = false)
        => _inner.ApplyAggregateSynchronization(plan, confirmReferencedRemoval);
    public bool ApplyCanonicalAggregateSynchronization(CanonicalAggregateSynchronizationPlan plan, bool confirmReferencedRemoval = false)
        => _inner.ApplyCanonicalAggregateSynchronization(plan, confirmReferencedRemoval);
    public bool ApplyAggregateSynchronization(GraphResourceEnvelope resource, bool confirmReferencedRemoval = false)
        => _inner.ApplyAggregateSynchronization(resource, confirmReferencedRemoval);
    public bool ApplyAggregateSynchronizationPlan(CanonicalAggregateSynchronizationPlan plan, bool confirmReferencedRemoval = false)
        => _inner.ApplyAggregateSynchronizationPlan(plan, confirmReferencedRemoval);
    public bool SynchronizeAggregate(GraphResourceEnvelope resource, bool confirmReferencedRemoval = false)
        => _inner.SynchronizeAggregate(resource, confirmReferencedRemoval);
    public bool Connect(GraphConnection connection) => _inner.Connect(connection);
    public bool AddNode(GraphNode node) => _inner.AddNode(node);
    public IReadOnlyList<GraphConnection> GetNodeReferences(string nodeId) => _inner.GetNodeReferences(nodeId);
    public bool RemoveNode(string nodeId, bool confirmReferencedRemoval = false)
        => _inner.RemoveNode(nodeId, confirmReferencedRemoval);
    public bool RemoveNodes(IReadOnlyList<string> nodeIds, bool confirmReferencedRemoval = false)
        => _inner.RemoveNodes(nodeIds, confirmReferencedRemoval);
    public bool Disconnect(GraphConnection connection) => _inner.Disconnect(connection);
    public bool Reconnect(GraphConnection original, GraphConnection replacement) => _inner.Reconnect(original, replacement);
    public bool RenamePortDisplayName(string nodeId, string portId, string displayName) => _inner.RenamePortDisplayName(nodeId, portId, displayName);
    public bool ReorderPort(string nodeId, string portId, int order) => _inner.ReorderPort(nodeId, portId, order);
    public bool MoveDynamicPort(string nodeId, string portId, int order) => _inner.MoveDynamicPort(nodeId, portId, order);
    public bool ReorderDynamicPort(string nodeId, string portId, int order) => _inner.ReorderDynamicPort(nodeId, portId, order);
    public bool SetNodeProperty(string nodeId, string property, JsonElement value) => _inner.SetNodeProperty(nodeId, property, value);
    public bool SetNodeProperty<T>(string nodeId, string property, T value) => _inner.SetNodeProperty(nodeId, property, value);
    public bool ChangeObjectiveType(string nodeId, string? type, string? actorId = null) => _inner.ChangeObjectiveType(nodeId, type, actorId);
    public bool SetObjectiveType(string nodeId, string? type, string? actorId = null) => _inner.SetObjectiveType(nodeId, type, actorId);
    public bool ChangeObjectiveTarget(string nodeId, string? targetId) => _inner.ChangeObjectiveTarget(nodeId, targetId);
    public bool SetObjectiveDescription(string nodeId, string? description) => _inner.SetObjectiveDescription(nodeId, description);
    public bool SetObjectiveRequired(string nodeId, int required) => _inner.SetObjectiveRequired(nodeId, required);
    public bool ChangeStoryActionType(string nodeId, string? type, string? itemId = null)
        => _inner.ChangeStoryActionType(nodeId, type, itemId);
    public bool RemoveNodeProperty(string nodeId, string property) => _inner.RemoveNodeProperty(nodeId, property);
    public bool AddDynamicPort(string nodeId, string displayName, GraphPortDirection direction, GraphInterfaceKind interfaceKind)
        => _inner.AddDynamicPort(nodeId, displayName, direction, interfaceKind);
    public bool AddDynamicPort(string nodeId, string displayName, bool isInput, GraphInterfaceKind interfaceKind)
        => _inner.AddDynamicPort(nodeId, displayName, isInput, interfaceKind);
    public bool AddDynamicPort(string nodeId, string displayName, GraphInterfaceKind interfaceKind, bool isInput)
        => _inner.AddDynamicPort(nodeId, displayName, interfaceKind, isInput);
    public bool AddDynamicPort(string nodeId, bool isInput, GraphInterfaceKind interfaceKind, string displayName)
        => _inner.AddDynamicPort(nodeId, isInput, interfaceKind, displayName);
    public bool AddDynamicPort(string nodeId, string displayName, GraphInterfaceKind interfaceKind)
        => _inner.AddDynamicPort(nodeId, displayName, interfaceKind);
    public bool AddDynamicPort(string nodeId, string displayName)
        => _inner.AddDynamicPort(nodeId, displayName);
    public IReadOnlyList<GraphConnection> GetPortReferences(string nodeId, string portId)
        => _inner.GetPortReferences(nodeId, portId);
    public bool RemoveDynamicPort(string nodeId, string portId, bool confirmReferencedRemoval = false)
        => _inner.RemoveDynamicPort(nodeId, portId, confirmReferencedRemoval);
    public bool AddStoryStartTrigger(string nodeId, string displayName, string triggerType,
        IReadOnlyDictionary<string, JsonElement>? triggerProperties = null)
        => _inner.AddStoryStartTrigger(nodeId, displayName, triggerType, triggerProperties);
    public bool AddStoryStartTriggerSlot(string nodeId, string displayName, string triggerType,
        IReadOnlyDictionary<string, JsonElement>? triggerProperties = null)
        => _inner.AddStoryStartTriggerSlot(nodeId, displayName, triggerType, triggerProperties);
    public bool SetStoryStartTrigger(string nodeId, string portId, string triggerType,
        IReadOnlyDictionary<string, JsonElement>? triggerProperties = null)
        => _inner.SetStoryStartTrigger(nodeId, portId, triggerType, triggerProperties);
    public bool SetStoryStartTriggerType(string nodeId, string portId, string triggerType,
        string? actorId = null)
        => _inner.SetStoryStartTriggerType(nodeId, portId, triggerType, actorId);
    public bool SetStoryStartTriggerProperties(string nodeId, string portId,
        IReadOnlyDictionary<string, JsonElement> triggerProperties)
        => _inner.SetStoryStartTriggerProperties(nodeId, portId, triggerProperties);
    public bool UpdateStoryStartTrigger(string nodeId, string portId, string triggerType,
        IReadOnlyDictionary<string, JsonElement>? triggerProperties = null)
        => _inner.UpdateStoryStartTrigger(nodeId, portId, triggerType, triggerProperties);
    public bool RenameStoryStartTrigger(string nodeId, string portId, string displayName)
        => _inner.RenameStoryStartTrigger(nodeId, portId, displayName);
    public bool RenameStoryStartTriggerSlot(string nodeId, string portId, string displayName)
        => _inner.RenameStoryStartTriggerSlot(nodeId, portId, displayName);
    public bool ReorderStoryStartTrigger(string nodeId, string portId, int order)
        => _inner.ReorderStoryStartTrigger(nodeId, portId, order);
    public bool ReorderStoryStartTriggerSlot(string nodeId, string portId, int order)
        => _inner.ReorderStoryStartTriggerSlot(nodeId, portId, order);
    public bool RemoveStoryStartTrigger(string nodeId, string portId, bool confirmReferencedRemoval = false)
        => _inner.RemoveStoryStartTrigger(nodeId, portId, confirmReferencedRemoval);
    public bool RemoveStoryStartTriggerSlot(string nodeId, string portId, bool confirmReferencedRemoval = false)
        => _inner.RemoveStoryStartTriggerSlot(nodeId, portId, confirmReferencedRemoval);
    public bool SetStoryStartRepeatPolicy(string nodeId, string repeatPolicy)
        => _inner.SetStoryStartRepeatPolicy(nodeId, repeatPolicy);
    public bool SetStoryStartRepeatMode(string nodeId, string repeatPolicy)
        => _inner.SetStoryStartRepeatMode(nodeId, repeatPolicy);
    public bool Undo() => _inner.Undo();
    public bool Redo() => _inner.Redo();
    public bool RollbackLastEdit() => _inner.RollbackLastEdit();
}
