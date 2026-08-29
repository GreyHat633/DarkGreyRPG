using DarkGreyRPG.Studio.Core.Graphs;

namespace DarkGreyRPG.Studio.Core.Graphs.Definitions;

/// <summary>
/// Immutable metadata for a port that is present on every newly-created node
/// of a definition.  Dynamic ports are deliberately outside this type.
/// </summary>
public sealed class GraphPortDefinition
{
    public GraphPortDefinition(
        string id,
        string displayName,
        GraphPortDirection direction,
        GraphInterfaceKind interfaceKind,
        int order = 0)
    {
        if (string.IsNullOrWhiteSpace(id))
            throw new ArgumentException("A fixed port ID is required.", nameof(id));
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("A fixed port display name is required.", nameof(displayName));
        if (order < 0)
            throw new ArgumentOutOfRangeException(nameof(order), "A port order cannot be negative.");

        Id = id;
        DisplayName = displayName;
        Direction = direction;
        InterfaceKind = interfaceKind;
        Order = order;
    }

    public GraphPortDefinition(
        string id,
        string displayName,
        bool isInput,
        GraphInterfaceKind interfaceKind,
        int order = 0)
        : this(id, displayName, isInput ? GraphPortDirection.Input : GraphPortDirection.Output, interfaceKind, order) { }

    public string Id { get; }
    public string PortId => Id;
    public string DisplayName { get; }
    public string Name => DisplayName;
    public GraphPortDirection Direction { get; }
    public bool IsInput => Direction == GraphPortDirection.Input;
    public bool IsOutput => !IsInput;
    public GraphInterfaceKind InterfaceKind { get; }
    public GraphInterfaceKind Kind => InterfaceKind;
    public int Order { get; }

    /// <summary>Materializes an independent mutable graph-core port.</summary>
    public GraphPort CreatePort()
        => new(Id, DisplayName, Direction == GraphPortDirection.Input, InterfaceKind, Order);

    public GraphPort Materialize() => CreatePort();
    public GraphPort ToPort() => CreatePort();
}
