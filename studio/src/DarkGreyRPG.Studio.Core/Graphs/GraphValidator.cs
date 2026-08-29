using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Graphs;

/// <summary>Validates canonical graph structure without any UI or resource-schema assumptions.</summary>
public static class GraphValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(GraphDocument graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return Validate(graph.Nodes, graph.Connections);
    }

    public static IReadOnlyList<ValidationIssue> Validate(
        IEnumerable<GraphNode>? nodes,
        IEnumerable<GraphConnection>? connections)
    {
        var issues = new List<ValidationIssue>();
        var nodeList = nodes?.ToArray() ?? [];
        var connectionList = connections?.ToArray() ?? [];
        var nodesById = new Dictionary<string, GraphNode>(StringComparer.Ordinal);

        foreach (var node in nodeList)
        {
            if (node is null)
            {
                issues.Add(new("graph.node.required", "Graph nodes cannot contain null entries.", "nodes"));
                continue;
            }

            if (string.IsNullOrWhiteSpace(node.Id))
            {
                issues.Add(new("graph.node.id.required", "Graph node ID is required.", "id"));
            }
            else if (!nodesById.TryAdd(node.Id, node))
            {
                issues.Add(new("graph.node.id.duplicate", $"Graph node ID '{node.Id}' is duplicated.", "id", NodeId: node.Id));
            }

            var portIds = new HashSet<string>(StringComparer.Ordinal);
            foreach (var port in node.Ports ?? [])
            {
                if (port is null)
                {
                    issues.Add(new("graph.port.required", $"Node '{node.Id}' cannot contain null ports.", "ports", NodeId: NullIfBlank(node.Id)));
                    continue;
                }

                if (string.IsNullOrWhiteSpace(port.Id))
                    issues.Add(new("graph.port.id.required", $"Node '{node.Id}' has a port without an ID.", "id", NodeId: NullIfBlank(node.Id)));
                else if (!portIds.Add(port.Id))
                    issues.Add(new("graph.port.id.duplicate", $"Port ID '{port.Id}' is duplicated on node '{node.Id}'.", "id", NodeId: NullIfBlank(node.Id)));

                if (port.InterfaceKind is not (GraphInterfaceKind.Flow or GraphInterfaceKind.Logic))
                    issues.Add(new("graph.port.interface_kind.invalid", $"Port '{port.Id}' on node '{node.Id}' has an invalid interface kind.", "interface_kind", NodeId: NullIfBlank(node.Id)));
            }
        }

        var duplicateEdges = new HashSet<GraphConnection>(new GraphConnectionComparer());
        var validLogicEdges = new List<(string From, string To, GraphConnection Connection)>();
        foreach (var connection in connectionList)
        {
            if (connection is null)
            {
                issues.Add(new("graph.connection.required", "Graph connections cannot contain null entries.", "connections"));
                continue;
            }

            if (!duplicateEdges.Add(connection))
                issues.Add(new("graph.connection.duplicate", "Duplicate graph edges are not allowed.", "connections", NodeId: NullIfBlank(connection.FromNodeId)));
            if (connection.InterfaceKind is not (GraphInterfaceKind.Flow or GraphInterfaceKind.Logic))
                issues.Add(new("graph.connection.interface_kind.invalid", "Connection interface_kind must be flow or logic.", "interface_kind", NodeId: NullIfBlank(connection.FromNodeId)));

            nodesById.TryGetValue(connection.FromNodeId, out var sourceNode);
            nodesById.TryGetValue(connection.ToNodeId, out var targetNode);
            if (sourceNode is null)
                issues.Add(new("graph.connection.from.node.missing", $"Connection source node '{connection.FromNodeId}' does not exist.", "from_node_id", NodeId: NullIfBlank(connection.FromNodeId)));
            if (targetNode is null)
                issues.Add(new("graph.connection.to.node.missing", $"Connection target node '{connection.ToNodeId}' does not exist.", "to_node_id", NodeId: NullIfBlank(connection.ToNodeId)));

            var sourcePort = FindPort(sourceNode, connection.FromPortId);
            var targetPort = FindPort(targetNode, connection.ToPortId);
            if (sourceNode is not null && sourcePort is null)
                issues.Add(new("graph.connection.from.port.missing", $"Connection source port '{connection.FromPortId}' does not exist on node '{connection.FromNodeId}'.", "from_port_id", NodeId: connection.FromNodeId));
            if (targetNode is not null && targetPort is null)
                issues.Add(new("graph.connection.to.port.missing", $"Connection target port '{connection.ToPortId}' does not exist on node '{connection.ToNodeId}'.", "to_port_id", NodeId: connection.ToNodeId));

            if (sourcePort is not null && sourcePort.IsInput)
                issues.Add(new("graph.connection.source.direction", $"Connection source port '{connection.FromPortId}' must be an output.", "from_port_id", NodeId: connection.FromNodeId));
            if (targetPort is not null && !targetPort.IsInput)
                issues.Add(new("graph.connection.target.direction", $"Connection target port '{connection.ToPortId}' must be an input.", "to_port_id", NodeId: connection.ToNodeId));
            if (sourcePort is not null && targetPort is not null && sourcePort.InterfaceKind != connection.InterfaceKind)
                issues.Add(new("graph.connection.source.kind.mismatch", "Connection interface_kind does not match the source port.", "interface_kind", NodeId: connection.FromNodeId));
            if (sourcePort is not null && targetPort is not null && targetPort.InterfaceKind != connection.InterfaceKind)
                issues.Add(new("graph.connection.target.kind.mismatch", "Connection interface_kind does not match the target port.", "interface_kind", NodeId: connection.ToNodeId));
            if (sourcePort is not null && targetPort is not null && sourcePort.InterfaceKind != targetPort.InterfaceKind)
                issues.Add(new("graph.connection.kind.mixed", "Flow and logic ports cannot be connected to each other.", "interface_kind", NodeId: connection.FromNodeId));

            if (connection.InterfaceKind == GraphInterfaceKind.Logic
                && sourceNode is not null && targetNode is not null
                && sourcePort is not null && targetPort is not null
                && !sourcePort.IsInput && targetPort.IsInput
                && sourcePort.InterfaceKind == targetPort.InterfaceKind
                && sourcePort.InterfaceKind == GraphInterfaceKind.Logic)
                validLogicEdges.Add((sourceNode.Id, targetNode.Id, connection));
        }

        foreach (var group in connectionList.Where(c => c is not null && c.InterfaceKind == GraphInterfaceKind.Flow)
                     .GroupBy(c => (c!.FromNodeId, c.FromPortId)))
        {
            if (group.Count() > 1)
                issues.Add(new("graph.connection.flow.output.multiple_targets", $"Flow output '{group.Key.FromPortId}' on node '{group.Key.FromNodeId}' may have at most one target.", "from_port_id", NodeId: group.Key.FromNodeId));
        }

        foreach (var group in connectionList.Where(c => c is not null && c.InterfaceKind == GraphInterfaceKind.Logic)
                     .GroupBy(c => (c!.ToNodeId, c.ToPortId)))
        {
            if (group.Count() > 1)
                issues.Add(new("graph.connection.logic.input.multiple_sources", $"Logic input '{group.Key.ToPortId}' on node '{group.Key.ToNodeId}' may have at most one source.", "to_port_id", NodeId: group.Key.ToNodeId));
        }

        ValidateLogicCycles(validLogicEdges, issues);
        return issues;
    }

    public static bool IsValid(GraphDocument graph) => Validate(graph).All(issue => issue.Severity != ValidationSeverity.Error);

    private static void ValidateLogicCycles(
        IEnumerable<(string From, string To, GraphConnection Connection)> edges,
        ICollection<ValidationIssue> issues)
    {
        var adjacency = edges.GroupBy(edge => edge.From, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.OrderBy(edge => edge.To, StringComparer.Ordinal).ToArray(), StringComparer.Ordinal);
        var state = new Dictionary<string, byte>(StringComparer.Ordinal);
        var reported = new HashSet<GraphConnection>(new GraphConnectionComparer());

        foreach (var node in adjacency.Keys.Order(StringComparer.Ordinal))
            Visit(node);

        void Visit(string node)
        {
            if (state.TryGetValue(node, out var current))
            {
                if (current == 1) return;
                if (current == 2) return;
            }
            state[node] = 1;
            if (adjacency.TryGetValue(node, out var outgoing))
            {
                foreach (var edge in outgoing)
                {
                    if (state.TryGetValue(edge.To, out var targetState) && targetState == 1)
                    {
                        if (reported.Add(edge.Connection))
                            issues.Add(new("graph.logic.cycle", "Directed logic connections must be acyclic.", "connections", NodeId: edge.From));
                    }
                    else if (!state.ContainsKey(edge.To))
                    {
                        Visit(edge.To);
                    }
                }
            }
            state[node] = 2;
        }
    }

    private static GraphPort? FindPort(GraphNode? node, string? id)
        => node?.Ports?.FirstOrDefault(port => port is not null && string.Equals(port.Id, id, StringComparison.Ordinal));

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private sealed class GraphConnectionComparer : IEqualityComparer<GraphConnection>
    {
        public bool Equals(GraphConnection? x, GraphConnection? y) => x?.Equals(y) == true;
        public int GetHashCode(GraphConnection obj) => obj.GetHashCode();
    }
}
