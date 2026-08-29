using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Editing;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Graphs.Definitions;

/// <summary>
/// Validates the canonical, definition-owned shape of graph nodes.
///
/// This validator intentionally does not validate edges or scope-wide
/// cardinality.  It is an opt-in check for callers that need to know whether
/// one node (or every node in a document) still matches the registered fixed
/// ports, dynamic-port roles, and property schema.
/// </summary>
public static class GraphNodeShapeValidator
{
    /// <summary>Validates one node against the definition for <paramref name="scope"/>.</summary>
    public static IReadOnlyList<ValidationIssue> Validate(
        GraphNode node,
        GraphScope scope,
        bool compatibilityMode = false)
    {
        ArgumentNullException.ThrowIfNull(node);

        var issues = new List<ValidationIssue>();
        if (!GraphNodeDefinitionRegistry.TryGet(scope, node.Type, out var definition))
        {
            // Definition-unavailable is deliberately terminal for this node.
            // In particular, do not report every persisted port/property as an
            // additional error when the type itself cannot be interpreted.
            issues.Add(new(
                "graph.node.shape.definition.unavailable",
                $"Node type '{node.Type}' is not registered for scope '{scope}'.",
                "type",
                NodeId: NullIfBlank(node.Id)));
            return issues;
        }

        if (definition.CompatibilityOnly && !compatibilityMode)
        {
            issues.Add(new(
                "graph.node.shape.compatibility.required",
                $"Node type '{node.Type}' can be validated only in compatibility mode.",
                "type",
                NodeId: NullIfBlank(node.Id)));
            return issues;
        }

        var ports = node.Ports ?? [];
        var fixedById = definition.FixedPorts.ToDictionary(port => port.Id, StringComparer.Ordinal);
        var fixedIds = new HashSet<string>(fixedById.Keys, StringComparer.Ordinal);

        // Validate each registered fixed identity exactly once.  Display name
        // and order are intentionally absent: both are presentation metadata.
        foreach (var fixedPort in definition.FixedPorts)
        {
            var matches = ports
                .Where(port => port is not null && string.Equals(port.Id, fixedPort.Id, StringComparison.Ordinal))
                .ToArray();
            if (matches.Length == 0)
            {
                issues.Add(new(
                    "graph.node.shape.fixed_port.missing",
                    $"Fixed port '{fixedPort.Id}' is missing from node '{node.Id}'.",
                    PortField(fixedPort.Id),
                    NodeId: NullIfBlank(node.Id)));
                continue;
            }

            if (matches.Length > 1)
            {
                issues.Add(new(
                    "graph.node.shape.fixed_port.duplicate",
                    $"Fixed port '{fixedPort.Id}' occurs more than once on node '{node.Id}'.",
                    PortField(fixedPort.Id),
                    NodeId: NullIfBlank(node.Id)));
                continue;
            }

            var actual = matches[0]!;
            if (actual.Direction != fixedPort.Direction)
            {
                issues.Add(new(
                    "graph.node.shape.fixed_port.direction",
                    $"Fixed port '{fixedPort.Id}' on node '{node.Id}' has direction '{actual.Direction}', expected '{fixedPort.Direction}'.",
                    PortField(fixedPort.Id),
                    NodeId: NullIfBlank(node.Id)));
            }

            if (actual.InterfaceKind != fixedPort.InterfaceKind)
            {
                issues.Add(new(
                    "graph.node.shape.fixed_port.kind",
                    $"Fixed port '{fixedPort.Id}' on node '{node.Id}' has kind '{actual.InterfaceKind}', expected '{fixedPort.InterfaceKind}'.",
                    PortField(fixedPort.Id),
                    NodeId: NullIfBlank(node.Id)));
            }
        }

        // Null entries are malformed graph data, but never make this opt-in
        // validator throw.  A blank identity is similarly terminal for that
        // port so it does not cascade into an "unexpected" dynamic-port error.
        foreach (var port in ports)
        {
            if (port is null)
            {
                issues.Add(new(
                    "graph.node.shape.port.required",
                    $"Node '{node.Id}' cannot contain a null port.",
                    "ports",
                    NodeId: NullIfBlank(node.Id)));
                continue;
            }

            if (string.IsNullOrWhiteSpace(port.Id))
            {
                issues.Add(new(
                    "graph.node.shape.port.id.required",
                    $"Node '{node.Id}' has a port without an ID.",
                    "ports",
                    NodeId: NullIfBlank(node.Id)));
                continue;
            }

            if (fixedIds.Contains(port.Id))
                continue;

            if (!GraphDynamicPortPolicy.TryGetRole(scope, node.Type,
                    port.Direction, port.InterfaceKind, out var role))
            {
                issues.Add(new(
                    "graph.node.shape.dynamic_port.unexpected",
                    $"Port '{port.Id}' on node '{node.Id}' is neither a registered fixed port nor an allowed dynamic port.",
                    PortField(port.Id),
                    NodeId: NullIfBlank(node.Id)));
            }
        }

        // Count by role rather than by display name or order.  This also makes
        // role minima robust to arbitrary dynamic IDs and list permutations.
        foreach (var role in GraphDynamicPortPolicy.ForNode(scope, node.Type))
        {
            var count = ports.Count(port => port is not null
                && !fixedIds.Contains(port.Id)
                && port.Direction == role.Direction
                && port.InterfaceKind == role.InterfaceKind);
            if (count < role.MinimumCount)
            {
                issues.Add(new(
                    "graph.node.shape.dynamic_port.minimum",
                    $"Dynamic role '{role.NodeType}/{role.InterfaceKind}/{role.Direction}' on node '{node.Id}' requires at least {role.MinimumCount} port(s), found {count}.",
                    "ports",
                    NodeId: NullIfBlank(node.Id)));
            }
        }

        var properties = node.Properties ?? [];
        foreach (var property in definition.PropertyDefinitions)
        {
            if (!properties.TryGetValue(property.Name, out var value))
            {
                if (property.Required)
                {
                    issues.Add(new(
                        "graph.node.shape.property.missing",
                        $"Required property '{property.Name}' is missing from node '{node.Id}'.",
                        PropertyField(property.Name),
                        NodeId: NullIfBlank(node.Id)));
                }

                continue;
            }

            if (value.ValueKind != property.Kind)
            {
                issues.Add(new(
                    "graph.node.shape.property.kind",
                    $"Property '{property.Name}' on node '{node.Id}' has kind '{value.ValueKind}', expected '{property.Kind}'.",
                    PropertyField(property.Name),
                    NodeId: NullIfBlank(node.Id)));
            }
        }

        if (scope == GraphScope.Task && string.Equals(node.Type, CanonicalTaskObjectiveSchema.NodeType, StringComparison.Ordinal))
            issues.AddRange(CanonicalTaskObjectiveSchema.Validate(node));

        if (scope == GraphScope.StoryFlow && string.Equals(node.Type, CanonicalStoryActionSchema.NodeType, StringComparison.Ordinal))
            issues.AddRange(CanonicalStoryActionSchema.Validate(node));

        if (scope == GraphScope.Session && string.Equals(node.Type, "choice", StringComparison.Ordinal))
            issues.AddRange(SessionChoiceSchema.Validate(node));

        // Older draft Story Start nodes intentionally remain loadable while
        // the canonical authoring path opts into the strict trigger schema.
        // Once either Story Start metadata field is present, validate the
        // complete metadata/Flow projection and fail closed on drift.
        if (scope == GraphScope.StoryFlow
            && string.Equals(node.Type, "start", StringComparison.Ordinal)
            && ((node.Properties ?? []).ContainsKey(StoryStartSchema.TriggersProperty)
                || (node.Properties ?? []).ContainsKey(StoryStartSchema.RepeatPolicyProperty)))
            issues.AddRange(StoryStartSchema.Validate(node, compatibilityMode));

        return issues;
    }

    /// <summary>Validates every non-null node in a graph in document order.</summary>
    public static IReadOnlyList<ValidationIssue> Validate(
        GraphDocument graph,
        GraphScope scope,
        bool compatibilityMode = false)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var issues = new List<ValidationIssue>();
        var nodes = graph.Nodes ?? [];
        for (var index = 0; index < nodes.Count; index++)
        {
            var node = nodes[index];
            if (node is null)
            {
                issues.Add(new(
                    "graph.node.shape.node.required",
                    "Graph nodes cannot contain null entries.",
                    $"nodes[{index}]"));
                continue;
            }

            issues.AddRange(Validate(node, scope, compatibilityMode));
        }

        return issues;
    }

    public static bool IsValid(GraphNode node, GraphScope scope, bool compatibilityMode = false)
        => Validate(node, scope, compatibilityMode).All(issue => issue.Severity != ValidationSeverity.Error);

    public static bool IsValid(GraphDocument graph, GraphScope scope, bool compatibilityMode = false)
        => Validate(graph, scope, compatibilityMode).All(issue => issue.Severity != ValidationSeverity.Error);

    private static string PortField(string portId) => $"ports[{portId}]";

    private static string PropertyField(string propertyName) => $"properties.{propertyName}";

    private static string? NullIfBlank(string? value)
        => string.IsNullOrWhiteSpace(value) ? null : value;
}
