using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Graphs.Editing;

/// <summary>Validates one prospective edge without requiring the rest of a draft to be valid.</summary>
public static class CandidateEdgeValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(
        GraphDocument graph,
        GraphConnection candidate,
        GraphScope? scope = null,
        GraphConnection? excludedConnection = null,
        bool compatibilityMode = false)
    {
        ArgumentNullException.ThrowIfNull(graph);
        ArgumentNullException.ThrowIfNull(candidate);

        var issues = new List<ValidationIssue>();
        var nodes = (graph.Nodes ?? []).Where(node => node is not null).ToArray();
        var sourceNodes = nodes.Where(node => string.Equals(node.Id, candidate.FromNodeId, StringComparison.Ordinal)).ToArray();
        var targetNodes = nodes.Where(node => string.Equals(node.Id, candidate.ToNodeId, StringComparison.Ordinal)).ToArray();
        var source = OneNode(sourceNodes, candidate.FromNodeId, "from", issues);
        var target = OneNode(targetNodes, candidate.ToNodeId, "to", issues);

        var sourcePort = OnePort(source, candidate.FromPortId, "from", issues);
        var targetPort = OnePort(target, candidate.ToPortId, "to", issues);

        if (source is not null && target is not null
            && string.Equals(source.Id, target.Id, StringComparison.Ordinal))
        {
            issues.Add(new("graph.connection.nodes.same", "A graph connection must join two different nodes.", "to_node_id", NodeId: source.Id));
        }

        if (sourcePort is not null && sourcePort.IsInput)
            issues.Add(new("graph.connection.source.direction", $"Connection source port '{candidate.FromPortId}' must be an output.", "from_port_id", NodeId: candidate.FromNodeId));
        if (targetPort is not null && !targetPort.IsInput)
            issues.Add(new("graph.connection.target.direction", $"Connection target port '{candidate.ToPortId}' must be an input.", "to_port_id", NodeId: candidate.ToNodeId));

        if (candidate.InterfaceKind is not (GraphInterfaceKind.Flow or GraphInterfaceKind.Logic))
            issues.Add(new("graph.connection.interface_kind.invalid", "Connection interface_kind must be flow or logic.", "interface_kind", NodeId: candidate.FromNodeId));
        if (sourcePort is not null && sourcePort.InterfaceKind != candidate.InterfaceKind)
            issues.Add(new("graph.connection.source.kind.mismatch", "Connection interface_kind does not match the source port.", "interface_kind", NodeId: candidate.FromNodeId));
        if (targetPort is not null && targetPort.InterfaceKind != candidate.InterfaceKind)
            issues.Add(new("graph.connection.target.kind.mismatch", "Connection interface_kind does not match the target port.", "interface_kind", NodeId: candidate.ToNodeId));
        if (sourcePort is not null && targetPort is not null && sourcePort.InterfaceKind != targetPort.InterfaceKind)
            issues.Add(new("graph.connection.kind.mixed", "Flow and logic ports cannot be mixed.", "interface_kind", NodeId: candidate.FromNodeId));

        var remaining = ExistingConnections(graph, excludedConnection);
        if (remaining.Any(connection => connection.Equals(candidate)))
            issues.Add(new("graph.connection.duplicate", "Duplicate graph edges are not allowed.", "connections", NodeId: candidate.FromNodeId));

        // Cardinality is deliberately limited to the two endpoints affected by this edge.
        if (candidate.InterfaceKind == GraphInterfaceKind.Flow
            && remaining.Count(connection => string.Equals(connection.FromNodeId, candidate.FromNodeId, StringComparison.Ordinal)
                && string.Equals(connection.FromPortId, candidate.FromPortId, StringComparison.Ordinal)
                && connection.InterfaceKind == GraphInterfaceKind.Flow) > 0)
        {
            issues.Add(new("graph.connection.flow.output.multiple_targets", $"Flow output '{candidate.FromPortId}' on node '{candidate.FromNodeId}' already has a target.", "from_port_id", NodeId: candidate.FromNodeId));
        }
        if (candidate.InterfaceKind == GraphInterfaceKind.Logic
            && remaining.Count(connection => string.Equals(connection.ToNodeId, candidate.ToNodeId, StringComparison.Ordinal)
                && string.Equals(connection.ToPortId, candidate.ToPortId, StringComparison.Ordinal)
                && connection.InterfaceKind == GraphInterfaceKind.Logic) > 0)
        {
            issues.Add(new("graph.connection.logic.input.multiple_sources", $"Logic input '{candidate.ToPortId}' on node '{candidate.ToNodeId}' already has a source.", "to_port_id", NodeId: candidate.ToNodeId));
        }

        if (scope.HasValue)
            ValidateScope(scope.Value, compatibilityMode, candidate, source, target, sourcePort, targetPort, issues);

        if (candidate.InterfaceKind == GraphInterfaceKind.Logic
            && source is not null && target is not null
            && sourcePort is not null && targetPort is not null
            && !sourcePort.IsInput && targetPort.IsInput
            && sourcePort.InterfaceKind == GraphInterfaceKind.Logic
            && targetPort.InterfaceKind == GraphInterfaceKind.Logic
            && CreatesLogicCycle(nodes, remaining, candidate))
        {
            issues.Add(new("graph.logic.cycle", "Directed logic connections must be acyclic.", "connections", NodeId: candidate.FromNodeId));
        }

        return issues;
    }

    public static bool IsValid(GraphDocument graph, GraphConnection candidate, GraphScope? scope = null,
        GraphConnection? excludedConnection = null, bool compatibilityMode = false)
        => Validate(graph, candidate, scope, excludedConnection, compatibilityMode).Count == 0;

    // Friendly aliases for callers that describe this operation as a local edit validation.
    public static IReadOnlyList<ValidationIssue> ValidateCandidate(GraphDocument graph, GraphConnection candidate,
        GraphScope? scope = null, GraphConnection? excludedConnection = null, bool compatibilityMode = false)
        => Validate(graph, candidate, scope, excludedConnection, compatibilityMode);

    private static GraphNode? OneNode(GraphNode[] matches, string id, string endpoint, ICollection<ValidationIssue> issues)
    {
        if (matches.Length == 0)
        {
            issues.Add(new($"graph.connection.{endpoint}.node.missing", $"Connection {endpoint} node '{id}' does not exist.", $"{endpoint}_node_id", NodeId: NullIfBlank(id)));
            return null;
        }
        if (matches.Length > 1)
        {
            issues.Add(new($"graph.connection.{endpoint}.node.ambiguous", $"Connection {endpoint} node '{id}' is ambiguous.", $"{endpoint}_node_id", NodeId: NullIfBlank(id)));
            return null;
        }
        return matches[0];
    }

    private static GraphPort? OnePort(GraphNode? node, string id, string endpoint, ICollection<ValidationIssue> issues)
    {
        if (node is null) return null;
        var matches = (node.Ports ?? []).Where(port => port is not null && string.Equals(port.Id, id, StringComparison.Ordinal)).ToArray();
        if (matches.Length == 0)
        {
            issues.Add(new($"graph.connection.{endpoint}.port.missing", $"Connection {endpoint} port '{id}' does not exist on node '{node.Id}'.", $"{endpoint}_port_id", NodeId: node.Id));
            return null;
        }
        if (matches.Length > 1)
        {
            issues.Add(new($"graph.connection.{endpoint}.port.ambiguous", $"Connection {endpoint} port '{id}' is ambiguous on node '{node.Id}'.", $"{endpoint}_port_id", NodeId: node.Id));
            return null;
        }
        return matches[0];
    }

    private static List<GraphConnection> ExistingConnections(GraphDocument graph, GraphConnection? excluded)
    {
        var result = new List<GraphConnection>();
        var excludedOnce = false;
        foreach (var connection in graph.Connections ?? [])
        {
            if (connection is null) continue;
            if (!excludedOnce && excluded is not null && connection.Equals(excluded))
            {
                excludedOnce = true;
                continue;
            }
            result.Add(connection);
        }
        return result;
    }

    private static void ValidateScope(GraphScope scope, bool compatibilityMode, GraphConnection candidate,
        GraphNode? source, GraphNode? target, GraphPort? sourcePort, GraphPort? targetPort, ICollection<ValidationIssue> issues)
    {
        if (scope == GraphScope.Task && candidate.InterfaceKind == GraphInterfaceKind.Flow)
            issues.Add(new("graph.scope.task.flow_connection.disallowed", "Task graphs cannot contain flow connections.", "interface_kind", NodeId: NullIfBlank(candidate.FromNodeId)));

        foreach (var (node, endpoint) in new[] { (source, "from"), (target, "to") })
        {
            if (node is null) continue;
            if (!GraphNodeDefinitionRegistry.TryGet(scope, node.Type, out var definition))
            {
                var known = GraphNodeDefinitionRegistry.TryGet(node.Type, out var other);
                issues.Add(new(
                    known ? "graph.scope.node_type.wrong_scope" : "graph.scope.node_type.unknown",
                    known ? $"Node type '{node.Type}' belongs to scope '{other.Scope}', not '{scope}'." : $"Node type '{node.Type}' is not registered for scope '{scope}'.",
                    "type", NodeId: NullIfBlank(node.Id)));
                continue;
            }
            if (definition.CompatibilityOnly && !compatibilityMode)
                issues.Add(new("graph.scope.node_type.compatibility_only", $"Node type '{node.Type}' is compatibility-only.", "type", NodeId: NullIfBlank(node.Id)));
        }
        if (scope == GraphScope.Task)
        {
            if (sourcePort?.InterfaceKind == GraphInterfaceKind.Flow)
                issues.Add(new("graph.scope.task.flow_port.disallowed", "Task graphs cannot contain flow ports.", "kind", NodeId: NullIfBlank(candidate.FromNodeId)));
            if (targetPort?.InterfaceKind == GraphInterfaceKind.Flow)
                issues.Add(new("graph.scope.task.flow_port.disallowed", "Task graphs cannot contain flow ports.", "kind", NodeId: NullIfBlank(candidate.ToNodeId)));
        }
        ValidateAllowedKind(scope, source, sourcePort, issues);
        ValidateAllowedKind(scope, target, targetPort, issues);
    }

    private static void ValidateAllowedKind(GraphScope scope, GraphNode? node, GraphPort? port, ICollection<ValidationIssue> issues)
    {
        if (node is null || port is null || !GraphNodeDefinitionRegistry.TryGet(scope, node.Type, out var definition)
            || definition.AllowedInterfaceKinds.Contains(port.InterfaceKind)) return;
        issues.Add(new("graph.scope.port.interface_kind.disallowed", $"Port '{port.Id}' on node '{node.Id}' is not allowed for node type '{node.Type}'.", "kind", NodeId: NullIfBlank(node.Id)));
    }

    private static bool CreatesLogicCycle(IReadOnlyList<GraphNode> nodes, IReadOnlyList<GraphConnection> existing, GraphConnection candidate)
    {
        var adjacency = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        // Only a path already present from candidate target back to candidate
        // source makes this edge cyclic.  A pre-existing cycle elsewhere is an
        // unrelated Problem and must not prevent a local repair.
        foreach (var connection in existing)
        {
            if (connection.InterfaceKind != GraphInterfaceKind.Logic || !StructurallyValidLogicEdge(nodes, connection)) continue;
            if (!adjacency.TryGetValue(connection.FromNodeId, out var targets))
                adjacency[connection.FromNodeId] = targets = new(StringComparer.Ordinal);
            targets.Add(connection.ToNodeId);
        }

        var visited = new HashSet<string>(StringComparer.Ordinal);
        return Reachable(candidate.ToNodeId, candidate.FromNodeId);

        bool Reachable(string node, string goal)
        {
            if (string.Equals(node, goal, StringComparison.Ordinal)) return true;
            if (!visited.Add(node)) return false;
            if (adjacency.TryGetValue(node, out var next))
                foreach (var target in next)
                    if (Reachable(target, goal)) return true;
            return false;
        }
    }

    private static bool StructurallyValidLogicEdge(IReadOnlyList<GraphNode> nodes, GraphConnection connection)
    {
        var fromNodes = nodes.Where(node => node.Id == connection.FromNodeId).ToArray();
        var toNodes = nodes.Where(node => node.Id == connection.ToNodeId).ToArray();
        if (fromNodes.Length != 1 || toNodes.Length != 1) return false;
        var from = (fromNodes[0].Ports ?? []).Where(port => port is not null && port.Id == connection.FromPortId).ToArray();
        var to = (toNodes[0].Ports ?? []).Where(port => port is not null && port.Id == connection.ToPortId).ToArray();
        return from.Length == 1 && to.Length == 1 && !from[0].IsInput && to[0].IsInput
            && from[0].InterfaceKind == GraphInterfaceKind.Logic && to[0].InterfaceKind == GraphInterfaceKind.Logic;
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

/// <summary>Alias retained for code that uses the shorter graph terminology.</summary>
public static class GraphEditValidator
{
    public static IReadOnlyList<ValidationIssue> ValidateCandidate(GraphDocument graph, GraphConnection candidate,
        GraphScope? scope = null, GraphConnection? excludedConnection = null, bool compatibilityMode = false)
        => CandidateEdgeValidator.Validate(graph, candidate, scope, excludedConnection, compatibilityMode);

    public static bool IsValidCandidate(GraphDocument graph, GraphConnection candidate, GraphScope? scope = null,
        GraphConnection? excludedConnection = null, bool compatibilityMode = false)
        => CandidateEdgeValidator.IsValid(graph, candidate, scope, excludedConnection, compatibilityMode);
}
