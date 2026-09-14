using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Graphs.Definitions;

/// <summary>Applies scope-specific rules after generic graph validation.</summary>
public static class GraphScopePolicy
{
    public static IReadOnlyList<ValidationIssue> Validate(
        GraphDocument graph,
        GraphScope scope,
        bool compatibilityMode = false)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return Validate(graph.Nodes, graph.Connections, scope, compatibilityMode);
    }

    public static IReadOnlyList<ValidationIssue> Validate(
        IEnumerable<GraphNode>? nodes,
        IEnumerable<GraphConnection>? connections,
        GraphScope scope,
        bool compatibilityMode = false)
    {
        var nodeList = nodes?.Where(node => node is not null).ToArray() ?? [];
        var connectionList = connections?.Where(connection => connection is not null).ToArray() ?? [];
        var issues = new List<ValidationIssue>();

        // Scope validation deliberately composes generic validation, so a
        // caller gets both malformed graph and scope diagnostics in one pass.
        issues.AddRange(GraphValidator.Validate(nodeList, connectionList));

        var counts = nodeList
            .Where(node => node is not null)
            .GroupBy(node => node.Type, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.Count(), StringComparer.Ordinal);

        foreach (var node in nodeList)
        {
            if (!GraphNodeDefinitionRegistry.TryGet(scope, node.Type, out var definition))
            {
                if (GraphNodeDefinitionRegistry.TryGet(node.Type, out var otherDefinition))
                {
                    issues.Add(new(
                        "graph.scope.node_type.wrong_scope",
                        $"Node type '{node.Type}' belongs to scope '{otherDefinition.Scope}', not '{scope}'.",
                        "type",
                        NodeId: NullIfBlank(node.Id)));
                }
                else
                {
                    issues.Add(new(
                        "graph.scope.node_type.unknown",
                        $"Node type '{node.Type}' is not registered for scope '{scope}'.",
                        "type",
                        NodeId: NullIfBlank(node.Id)));
                }

                continue;
            }

            if (definition.CompatibilityOnly && !compatibilityMode)
            {
                issues.Add(new(
                    "graph.scope.node_type.compatibility_only",
                    $"Node type '{node.Type}' is loadable only in compatibility mode and cannot be authored normally.",
                    "type",
                    NodeId: NullIfBlank(node.Id)));
            }

            foreach (var port in node.Ports ?? [])
            {
                if (port is null || definition.AllowedInterfaceKinds.Contains(port.InterfaceKind))
                    continue;

                issues.Add(new(
                    "graph.scope.port.interface_kind.disallowed",
                    $"Port '{port.Id}' on node '{node.Id}' is not allowed for node type '{node.Type}'.",
                    "kind",
                    NodeId: NullIfBlank(node.Id)));
            }
        }

        if (scope == GraphScope.Task)
        {
            foreach (var node in nodeList)
            {
                foreach (var port in node.Ports ?? [])
                {
                    if (port is not null && port.InterfaceKind == GraphInterfaceKind.Flow)
                        issues.Add(new(
                            "graph.scope.task.flow_port.disallowed",
                            "Task graphs cannot contain flow ports.",
                            "kind",
                            NodeId: NullIfBlank(node.Id)));
                }
            }

            foreach (var connection in connectionList.Where(connection => connection.InterfaceKind == GraphInterfaceKind.Flow))
            {
                issues.Add(new(
                    "graph.scope.task.flow_connection.disallowed",
                    "Task graphs cannot contain flow connections.",
                    "interface_kind",
                    NodeId: NullIfBlank(connection.FromNodeId)));
            }
        }

        foreach (var definition in GraphNodeDefinitionRegistry.ForScope(scope))
        {
            if (!definition.Required)
                continue;

            var count = counts.GetValueOrDefault(definition.Type);
            if (count == 0)
            {
                issues.Add(new(
                    "graph.scope.required_node.missing",
                    $"Scope '{scope}' requires one '{definition.Type}' node.",
                    "nodes"));
            }
            else if (definition.Unique && count > 1)
            {
                issues.Add(new(
                    "graph.scope.required_node.duplicate",
                    $"Scope '{scope}' allows only one '{definition.Type}' node.",
                    "nodes"));
            }
        }

        return issues;
    }

    public static bool IsValid(GraphDocument graph, GraphScope scope, bool compatibilityMode = false)
        => Validate(graph, scope, compatibilityMode).All(issue => issue.Severity != ValidationSeverity.Error);

    public static bool CanCreateNode(GraphScope scope, string? type, bool compatibilityMode = false)
        => scope != GraphScope.Project && GraphNodeDefinitionRegistry.TryGet(scope, type, out var definition)
            && (!definition.CompatibilityOnly || compatibilityMode);

    public static bool CanDeleteNode(GraphScope scope, string? type, GraphDocument? graph = null)
    {
        if (!GraphNodeDefinitionRegistry.TryGet(scope, type, out var definition) || definition.NonDeletable)
            return false;
        return graph is null || !definition.Required;
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;
}

/// <summary>Alias emphasizing that this API is the graph's scope validator.</summary>
public static class GraphScopeValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(GraphDocument graph, GraphScope scope, bool compatibilityMode = false)
        => GraphScopePolicy.Validate(graph, scope, compatibilityMode);

    public static bool IsValid(GraphDocument graph, GraphScope scope, bool compatibilityMode = false)
        => GraphScopePolicy.IsValid(graph, scope, compatibilityMode);

    public static bool CanCreateNode(GraphScope scope, string? type, bool compatibilityMode = false)
        => GraphScopePolicy.CanCreateNode(scope, type, compatibilityMode);
}
