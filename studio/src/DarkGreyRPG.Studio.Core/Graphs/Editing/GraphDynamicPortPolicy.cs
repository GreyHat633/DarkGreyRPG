using DarkGreyRPG.Studio.Core.Graphs.Definitions;

namespace DarkGreyRPG.Studio.Core.Graphs.Editing;

/// <summary>One canonical role in which a node may own dynamic ports.</summary>
public sealed record GraphDynamicPortRole(
    GraphScope Scope,
    string NodeType,
    GraphPortDirection Direction,
    GraphInterfaceKind InterfaceKind,
    int MinimumCount,
    bool UserEditable = true)
{
    public int Minimum => MinimumCount;
    public bool IsInput => Direction == GraphPortDirection.Input;
    public bool IsOutput => !IsInput;
    public bool IsUserEditable => UserEditable;
    public bool CanEdit => UserEditable;
}

/// <summary>
/// The non-persisted dynamic-port capability policy.  A port's display name
/// is deliberately absent from this policy: labels are presentation only.
/// </summary>
public static class GraphDynamicPortPolicy
{
    private static readonly IReadOnlyList<GraphDynamicPortRole> _roles =
    [
        new(GraphScope.StoryFlow, "start", GraphPortDirection.Output, GraphInterfaceKind.Flow, 1),
        // These are projections of child-resource public boundaries. They are
        // shape-valid dynamic ports, but are never local authoring slots.
        new(GraphScope.StoryFlow, "session", GraphPortDirection.Output, GraphInterfaceKind.Flow, 0, false),
        new(GraphScope.StoryFlow, "session", GraphPortDirection.Output, GraphInterfaceKind.Logic, 0, false),
        new(GraphScope.StoryFlow, "task", GraphPortDirection.Output, GraphInterfaceKind.Flow, 0, false),
        new(GraphScope.StoryFlow, "task", GraphPortDirection.Output, GraphInterfaceKind.Logic, 0, false),
        new(GraphScope.StoryFlow, "and", GraphPortDirection.Input, GraphInterfaceKind.Logic, 2),
        new(GraphScope.StoryFlow, "or", GraphPortDirection.Input, GraphInterfaceKind.Logic, 2),
        new(GraphScope.Session, "and", GraphPortDirection.Input, GraphInterfaceKind.Logic, 2),
        new(GraphScope.Session, "or", GraphPortDirection.Input, GraphInterfaceKind.Logic, 2),
        // Choice options own paired Flow/Logic outputs through their semantic
        // options[] contract. Generic dynamic-port editing must not desync them.
        new(GraphScope.Session, "choice", GraphPortDirection.Output, GraphInterfaceKind.Flow, 1, false),
        new(GraphScope.Session, "choice", GraphPortDirection.Output, GraphInterfaceKind.Logic, 1, false),
        new(GraphScope.Task, "and", GraphPortDirection.Input, GraphInterfaceKind.Logic, 2),
        new(GraphScope.Task, "or", GraphPortDirection.Input, GraphInterfaceKind.Logic, 2),
        new(GraphScope.Task, "settle", GraphPortDirection.Input, GraphInterfaceKind.Logic, 1),
    ];

    public static IReadOnlyList<GraphDynamicPortRole> Roles => _roles;

    public static bool TryGetRole(
        GraphScope scope,
        string? nodeType,
        GraphPortDirection direction,
        GraphInterfaceKind interfaceKind,
        out GraphDynamicPortRole role)
    {
        role = _roles.FirstOrDefault(item => item.Scope == scope
            && string.Equals(item.NodeType, nodeType, StringComparison.Ordinal)
            && item.Direction == direction
            && item.InterfaceKind == interfaceKind)!;
        return role is not null;
    }

    public static IReadOnlyList<GraphDynamicPortRole> ForNode(GraphScope scope, string? nodeType)
        => _roles.Where(item => item.Scope == scope
            && string.Equals(item.NodeType, nodeType, StringComparison.Ordinal)).ToArray();
}

/// <summary>Short alias for callers which use the graph terminology.</summary>
public static class DynamicPortRolePolicy
{
    public static IReadOnlyList<GraphDynamicPortRole> Roles => GraphDynamicPortPolicy.Roles;

    public static bool TryGetRole(GraphScope scope, string? nodeType, GraphPortDirection direction,
        GraphInterfaceKind interfaceKind, out GraphDynamicPortRole role)
        => GraphDynamicPortPolicy.TryGetRole(scope, nodeType, direction, interfaceKind, out role);
}
