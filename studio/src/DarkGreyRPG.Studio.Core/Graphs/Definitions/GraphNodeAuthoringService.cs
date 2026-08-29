using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Graphs.Definitions;

/// <summary>
/// The result of a non-mutating node-authoring request.  A successful result
/// contains one detached candidate and no issues; a failed result contains no
/// candidate and an ordinal-stable issue list.
/// </summary>
public sealed class GraphNodeAuthoringResult
{
    public GraphNodeAuthoringResult(GraphNode? candidate, IEnumerable<ValidationIssue>? issues = null)
    {
        Candidate = candidate;
        Issues = (issues ?? []).ToArray();
    }

    public GraphNode? Candidate { get; }
    public GraphNode? Node => Candidate;
    public IReadOnlyList<ValidationIssue> Issues { get; }
    public IReadOnlyList<ValidationIssue> ValidationIssues => Issues;
    public bool IsSuccess => Candidate is not null && Issues.Count == 0;
    public bool Succeeded => IsSuccess;
    public bool Success => IsSuccess;

    public static GraphNodeAuthoringResult FromCandidate(GraphNode candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        return new(candidate);
    }

    public static GraphNodeAuthoringResult FromIssues(IEnumerable<ValidationIssue> issues)
        => new(null, issues ?? throw new ArgumentNullException(nameof(issues)));
}

/// <summary>
/// Builds a detached, shape-valid graph-node candidate without changing the
/// supplied document. Semantic trigger/result initialization remains
/// deliberately fail-closed until those schemas are available; Session Choice
/// uses its frozen paired Flow/Logic option contract. Task settlement and
/// public Logic Output boundaries are initialized here because their opaque
/// identities are part of the authored node shape.
/// </summary>
public sealed class GraphNodeAuthoringService
{
    private readonly Func<string?> _dynamicPortIdSource;

    public GraphNodeAuthoringService(Func<string?>? dynamicPortIdSource = null)
    {
        _dynamicPortIdSource = dynamicPortIdSource ?? NextDynamicPortId;
    }

    /// <summary>
    /// Creates one detached candidate.  The graph is used only for identity
    /// and uniqueness checks; it is never changed by this method.
    /// </summary>
    public GraphNodeAuthoringResult Create(
        GraphDocument graph,
        GraphScope scope,
        string? type,
        string? id,
        string? displayName = null)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var basicIssues = new List<ValidationIssue>();
        if (string.IsNullOrWhiteSpace(id))
            basicIssues.Add(new(
                "graph.node.create.id.required",
                "Graph node ID is required.",
                "id"));
        if (string.IsNullOrWhiteSpace(type))
            basicIssues.Add(new(
                "graph.node.create.type.required",
                "Graph node type is required.",
                "type",
                NodeId: NullIfBlank(id)));
        if (basicIssues.Count != 0)
            return FailureIssues(basicIssues);

        // The checks above establish non-null/non-blank values while retaining
        // the caller's exact (non-blank) identity for ordinal comparisons.
        var nodeId = id!;
        var nodeType = type!;
        if (!GraphNodeDefinitionRegistry.TryGet(scope, nodeType, out var definition))
        {
            if (GraphNodeDefinitionRegistry.TryGet(nodeType, out var known))
                return FailureIssue(new(
                    "graph.node.create.type.wrong_scope",
                    $"Node type '{nodeType}' belongs to scope '{known.Scope}', not '{scope}'.",
                    "type",
                    NodeId: nodeId));

            return FailureIssue(new(
                "graph.node.create.type.unknown",
                $"Node type '{nodeType}' is not registered for scope '{scope}'.",
                "type",
                NodeId: nodeId));
        }

        // Compatibility nodes are load/migration inputs, never authoring
        // candidates.  There is intentionally no compatibility escape hatch
        // on this service.
        if (definition.CompatibilityOnly)
            return FailureIssue(new(
                "graph.node.create.type.compatibility_only",
                $"Node type '{nodeType}' is compatibility-only and cannot be authored normally.",
                "type",
                NodeId: nodeId));

        var existingNodes = graph.Nodes ?? [];
        if (existingNodes.Any(node => node is not null
            && string.Equals(node.Id, nodeId, StringComparison.Ordinal)))
            return FailureIssue(new(
                "graph.node.create.id.duplicate",
                $"Graph node ID '{nodeId}' is already used.",
                "id",
                NodeId: nodeId));

        if (definition.Unique && existingNodes.Any(node => node is not null
            && string.Equals(node.Type, nodeType, StringComparison.Ordinal)))
            return FailureIssue(new(
                "graph.node.create.type.unique",
                $"Scope '{scope}' allows only one '{nodeType}' node.",
                "type",
                NodeId: nodeId));

        // The current schemas do not name the persisted initializer fields for
        // these dynamic roles.  Creating a partial node would imply a schema
        // that does not exist, so fail closed before consuming an ID. Identity
        // and uniqueness errors above remain the more actionable diagnostics.
        if (RequiresSemanticInitializer(scope, nodeType))
            return FailureIssue(new(
                "graph.node.create.semantic_initializer.required",
                $"Node type '{nodeType}' in scope '{scope}' requires a semantic initializer before authoring.",
                "semantic_initializer",
                NodeId: nodeId));

        GraphNode candidate;
        try
        {
            candidate = GraphNodeFactory.Create(scope, nodeType, nodeId, displayName);
        }
        catch (ArgumentException exception)
        {
            return FailureIssue(new(
                "graph.node.create.invalid",
                exception.Message,
                "node",
                NodeId: nodeId));
        }
        catch (InvalidOperationException exception)
        {
            return FailureIssue(new(
                "graph.node.create.invalid",
                exception.Message,
                "node",
                NodeId: nodeId));
        }
        catch (KeyNotFoundException exception)
        {
            return FailureIssue(new(
                "graph.node.create.invalid",
                exception.Message,
                "node",
                NodeId: nodeId));
        }

        if (IsAndOr(scope, nodeType))
        {
            var dynamicIds = new string?[2];
            Exception? sourceFailure = null;
            for (var index = 0; index < dynamicIds.Length; index++)
            {
                try
                {
                    // Exactly two source calls are made.  There is no retry
                    // loop: opaque IDs are supplied by the caller and must be
                    // valid on their first use.
                    dynamicIds[index] = _dynamicPortIdSource();
                }
                catch (Exception exception) when (exception is not
                    (OutOfMemoryException or StackOverflowException))
                {
                    sourceFailure = exception;
                    break;
                }
            }

            if (sourceFailure is not null)
                return FailureIssue(new(
                    "graph.node.create.dynamic_port_id.unavailable",
                    "No opaque dynamic port ID was available.",
                    "ports",
                    NodeId: nodeId));

            var generatedIssue = ValidateDynamicIds(candidate, dynamicIds!, nodeId);
            if (generatedIssue is not null)
                return FailureIssue(generatedIssue);

            candidate.Ports.Add(new(dynamicIds[0]!, "输入 1", true,
                GraphInterfaceKind.Logic, 0));
            candidate.Ports.Add(new(dynamicIds[1]!, "输入 2", true,
                GraphInterfaceKind.Logic, 1));
        }
        else if (scope == GraphScope.Session && nodeType == "choice")
        {
            var dynamicIds = new string?[2];
            Exception? sourceFailure = null;
            for (var index = 0; index < dynamicIds.Length; index++)
            {
                try { dynamicIds[index] = _dynamicPortIdSource(); }
                catch (Exception exception) when (exception is not
                    (OutOfMemoryException or StackOverflowException))
                {
                    sourceFailure = exception;
                    break;
                }
            }
            if (sourceFailure is not null)
                return FailureIssue(new(
                    "graph.node.create.dynamic_port_id.unavailable",
                    "No opaque Session Choice option/port ID was available.",
                    "ports",
                    NodeId: nodeId));
            var generatedIssue = ValidateDynamicIds(candidate, dynamicIds!, nodeId);
            if (generatedIssue is not null)
                return FailureIssue(generatedIssue);
            try { SessionChoiceSchema.InitializeDefault(candidate, dynamicIds[0]!, dynamicIds[1]!); }
            catch (ArgumentException exception)
            {
                return FailureIssue(new(
                    "graph.node.create.choice.invalid",
                    exception.Message,
                    "properties.options",
                NodeId: nodeId));
            }
        }
        else if ((scope == GraphScope.Session && nodeType is "end" or "logic_output")
            || (scope == GraphScope.Task && nodeType == "logic_output"))
        {
            string? publicPortId;
            try { publicPortId = _dynamicPortIdSource(); }
            catch (Exception exception) when (exception is not
                (OutOfMemoryException or StackOverflowException))
            {
                return FailureIssue(new(
                    "graph.node.create.public_port_id.unavailable",
                    "No opaque public boundary port ID was available.",
                    "properties.port_id",
                    NodeId: nodeId));
            }
            if (string.IsNullOrWhiteSpace(publicPortId))
                return FailureIssue(new(
                    "graph.node.create.public_port_id.required",
                    "Public boundary port ID is required.",
                    "properties.port_id",
                    NodeId: nodeId));
            if (publicPortId is GraphAggregatePortProjection.FlowInputId
                or GraphAggregatePortProjection.LogicInputId)
                return FailureIssue(new(
                    "graph.node.create.public_port_id.reserved",
                    $"Public boundary port ID '{publicPortId}' is reserved.",
                    "properties.port_id",
                    NodeId: nodeId));
            var duplicatePublicId = scope == GraphScope.Task
                ? IsTaskPublicPortIdUsed(existingNodes, publicPortId)
                : existingNodes.Where(node => node is not null)
                    .Select(node => node!.Properties ?? [])
                    .Any(properties => properties.TryGetValue("port_id", out var value)
                        && value.ValueKind == System.Text.Json.JsonValueKind.String
                        && string.Equals(value.GetString(), publicPortId, StringComparison.Ordinal));
            if (duplicatePublicId)
            {
                return FailureIssue(new(
                    "graph.node.create.public_port_id.duplicate",
                    $"Public boundary port ID '{publicPortId}' is already used in this graph.",
                    "properties.port_id",
                    NodeId: nodeId));
            }

            candidate.Properties["port_id"] = System.Text.Json.JsonSerializer.SerializeToElement(publicPortId);
            var publicDisplayName = nodeType == "end" ? "结束" : "逻辑输出";
            if (scope == GraphScope.Task)
                publicDisplayName = NextTaskPublicDisplayName(existingNodes, publicDisplayName);
            candidate.Properties["display_name"] = System.Text.Json.JsonSerializer.SerializeToElement(publicDisplayName);
        }
        else if (scope == GraphScope.Task && nodeType == "settle")
        {
            string? resultPortId;
            try { resultPortId = _dynamicPortIdSource(); }
            catch (Exception exception) when (exception is not
                (OutOfMemoryException or StackOverflowException))
            {
                return FailureIssue(new(
                    "graph.node.create.dynamic_port_id.unavailable",
                    "No opaque Task settlement result port ID was available.",
                    "ports[0]",
                    NodeId: nodeId));
            }

            var generatedIssue = ValidateDynamicIds(candidate, [resultPortId], nodeId);
            if (generatedIssue is not null)
                return FailureIssue(generatedIssue);
            if (IsReservedPublicBoundaryId(resultPortId))
                return FailureIssue(new(
                    "graph.node.create.public_port_id.reserved",
                    $"Public boundary port ID '{resultPortId}' is reserved.",
                    "ports[0]",
                    NodeId: nodeId));
            if (IsTaskPublicPortIdUsed(existingNodes, resultPortId))
                return FailureIssue(new(
                    "graph.node.create.public_port_id.duplicate",
                    $"Public boundary port ID '{resultPortId}' is already used in this graph.",
                    "ports[0]",
                    NodeId: nodeId));
            candidate.Ports.Add(new(resultPortId!, "结果 1", true,
                GraphInterfaceKind.Logic, 0));
        }

        // This final check protects the factory/service boundary if a fixed
        // schema or dynamic-role policy changes later.  The graph still sees
        // no mutation if the candidate is rejected.
        var shapeIssues = GraphNodeShapeValidator.Validate(candidate, scope);
        if (shapeIssues.Count != 0)
            return FailureIssues(shapeIssues);

        return GraphNodeAuthoringResult.FromCandidate(candidate);
    }

    public GraphNodeAuthoringResult TryCreate(
        GraphDocument graph,
        GraphScope scope,
        string? type,
        string? id,
        string? displayName = null)
        => Create(graph, scope, type, id, displayName);

    public GraphNodeAuthoringResult CreateCandidate(
        GraphDocument graph,
        GraphScope scope,
        string? type,
        string? id,
        string? displayName = null)
        => Create(graph, scope, type, id, displayName);

    public GraphNodeAuthoringResult CreateNode(
        GraphDocument graph,
        GraphScope scope,
        string? type,
        string? id,
        string? displayName = null)
        => Create(graph, scope, type, id, displayName);

    /// <summary>
    /// Creates a detached, fully initialized canonical Story Start node. The
    /// legacy generic Create path remains fail-closed for semantic roles.
    /// </summary>
    public GraphNodeAuthoringResult CreateStoryStart(
        GraphDocument graph,
        string id,
        string? displayName = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (!string.IsNullOrWhiteSpace(id)
            && (graph.Nodes ?? []).Any(node => node is not null && string.Equals(node.Id, id, StringComparison.Ordinal)))
            return FailureIssue(new("graph.node.create.id.duplicate", $"Graph node ID '{id}' is already used.", "id", NodeId: id));
        if (string.IsNullOrWhiteSpace(id))
            return FailureIssue(new("graph.node.create.id.required", "Graph node ID is required.", "id"));
        if ((graph.Nodes ?? []).Any(node => node is not null && string.Equals(node.Type, "start", StringComparison.Ordinal)))
            return FailureIssue(new("graph.node.create.type.unique", "Scope 'StoryFlow' allows only one 'start' node.", "type", NodeId: id));
        GraphNode candidate;
        try { candidate = GraphNodeFactory.Create(GraphScope.StoryFlow, "start", id, displayName); }
        catch (Exception exception) when (exception is ArgumentException or InvalidOperationException or KeyNotFoundException)
        { return FailureIssue(new("graph.node.create.invalid", exception.Message, "node", NodeId: id)); }
        string? portId;
        try { portId = _dynamicPortIdSource(); }
        catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException))
        { return FailureIssue(new("graph.node.create.dynamic_port_id.unavailable", "No opaque Story Start trigger port ID was available.", "port_id", NodeId: id)); }
        if (string.IsNullOrWhiteSpace(portId))
            return FailureIssue(new("graph.node.create.dynamic_port_id.required", "Story Start trigger port_id is required.", "port_id", NodeId: id));
        if ((graph.Nodes ?? []).SelectMany(node => node?.Ports ?? []).Any(port => port is not null && string.Equals(port.Id, portId, StringComparison.Ordinal)))
            return FailureIssue(new("graph.node.create.dynamic_port_id.duplicate", $"Dynamic port ID '{portId}' is already used.", "port_id", NodeId: id));
        StoryStartSchema.InitializeDefault(candidate, portId);
        var shapeIssues = GraphNodeShapeValidator.Validate(candidate, GraphScope.StoryFlow);
        return shapeIssues.Count == 0 ? GraphNodeAuthoringResult.FromCandidate(candidate) : GraphNodeAuthoringResult.FromIssues(shapeIssues);
    }

    public GraphNodeAuthoringResult CreateStoryStartCandidate(GraphDocument graph, string id, string? displayName = null)
        => CreateStoryStart(graph, id, displayName);

    /// <summary>Boolean/out seam for callers that prefer Try-style APIs.</summary>
    public bool TryCreate(
        GraphDocument graph,
        GraphScope scope,
        string? type,
        string? id,
        out GraphNode? candidate,
        out IReadOnlyList<ValidationIssue> issues,
        string? displayName = null)
    {
        var result = Create(graph, scope, type, id, displayName);
        candidate = result.Candidate;
        issues = result.Issues;
        return result.IsSuccess;
    }

    private static ValidationIssue? ValidateDynamicIds(
        GraphNode candidate,
        IReadOnlyList<string?> ids,
        string nodeId)
    {
        var fixedIds = candidate.Ports
            .Where(port => port is not null)
            .Select(port => port.Id)
            .ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < ids.Count; index++)
        {
            var dynamicId = ids[index];
            if (string.IsNullOrWhiteSpace(dynamicId))
                return new(
                    "graph.node.create.dynamic_port_id.required",
                    $"Dynamic port ID {index + 1} is required.",
                    $"ports[{index}]",
                    NodeId: nodeId);
            if (fixedIds.Contains(dynamicId))
                return new(
                    "graph.node.create.dynamic_port_id.fixed_conflict",
                    $"Dynamic port ID '{dynamicId}' conflicts with a fixed port.",
                    $"ports[{index}]",
                    NodeId: nodeId);
            if (!seen.Add(dynamicId))
                return new(
                    "graph.node.create.dynamic_port_id.duplicate",
                    $"Dynamic port ID '{dynamicId}' is duplicated.",
                    $"ports[{index}]",
                    NodeId: nodeId);
        }

        return null;
    }

    private static bool IsAndOr(GraphScope scope, string type)
        => (scope is (GraphScope.StoryFlow or GraphScope.Session or GraphScope.Task))
            && (type is "and" or "or");

    private static bool IsReservedPublicBoundaryId(string? portId)
        => portId is GraphAggregatePortProjection.FlowInputId
            or GraphAggregatePortProjection.LogicInputId;

    private static bool IsTaskPublicPortIdUsed(IEnumerable<GraphNode> nodes, string? portId)
    {
        if (string.IsNullOrWhiteSpace(portId)) return false;
        return nodes.Where(node => node is not null).Any(node =>
            (node.Type == "settle" && (node.Ports ?? []).Any(port => port is not null
                && string.Equals(port.Id, portId, StringComparison.Ordinal)))
            || (node.Properties ?? []).TryGetValue("port_id", out var value)
                && value.ValueKind == System.Text.Json.JsonValueKind.String
                && string.Equals(value.GetString(), portId, StringComparison.Ordinal));
    }

    private static string NextTaskPublicDisplayName(IEnumerable<GraphNode> nodes, string baseName)
    {
        var used = nodes.Where(node => node is not null).SelectMany(node =>
        {
            var names = new List<string>();
            if (node.Type == "settle")
                names.AddRange((node.Ports ?? []).Where(port => port is not null)
                    .Select(port => port.DisplayName));
            if (node.Type == "logic_output"
                && (node.Properties ?? []).TryGetValue("display_name", out var value)
                && value.ValueKind == System.Text.Json.JsonValueKind.String)
                names.Add(value.GetString() ?? string.Empty);
            return names;
        }).ToHashSet(StringComparer.Ordinal);
        if (!used.Contains(baseName)) return baseName;
        for (var suffix = 2; suffix < int.MaxValue; suffix++)
        {
            var candidate = $"{baseName} {suffix}";
            if (!used.Contains(candidate)) return candidate;
        }
        return $"{baseName} {Guid.NewGuid():N}";
    }

    private static bool RequiresSemanticInitializer(GraphScope scope, string type)
        => (scope, type) is
            (GraphScope.StoryFlow, "start") or
            (GraphScope.StoryFlow, "session") or
            (GraphScope.StoryFlow, "task");

    private static GraphNodeAuthoringResult FailureIssue(ValidationIssue issue)
        => GraphNodeAuthoringResult.FromIssues([issue]);

    private static GraphNodeAuthoringResult FailureIssues(IEnumerable<ValidationIssue> issues)
        => GraphNodeAuthoringResult.FromIssues(issues);

    private static string NextDynamicPortId()
        => $"dynamic_port_{Guid.NewGuid():N}";

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
