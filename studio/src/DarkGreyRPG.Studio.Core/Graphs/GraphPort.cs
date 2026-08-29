using System.Text.Json.Serialization;

namespace DarkGreyRPG.Studio.Core.Graphs;

/// <summary>A stable, node-local port identity and its presentation metadata.</summary>
public sealed class GraphPort
{
    public GraphPort() { }

    public GraphPort(string id, string displayName, bool isInput, GraphInterfaceKind interfaceKind, int order = 0)
    {
        Id = id;
        DisplayName = displayName;
        IsInput = isInput;
        Kind = interfaceKind;
        Order = order;
    }

    public GraphPort(string id, string displayName, GraphInterfaceKind interfaceKind, bool isInput, int order = 0)
        : this(id, displayName, isInput, interfaceKind, order) { }

    [JsonPropertyName("port_id")]
    [JsonPropertyOrder(0)]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    [JsonPropertyOrder(1)]
    public string DisplayName { get; set; } = string.Empty;

    [JsonIgnore]
    public bool IsInput { get; set; }

    [JsonPropertyName("kind")]
    [JsonPropertyOrder(3)]
    public GraphInterfaceKind Kind { get; set; }

    [JsonPropertyName("direction")]
    [JsonPropertyOrder(4)]
    public GraphPortDirection Direction
    {
        get => IsInput ? GraphPortDirection.Input : GraphPortDirection.Output;
        set => IsInput = value == GraphPortDirection.Input;
    }

    [JsonPropertyName("order")]
    [JsonPropertyOrder(2)]
    public int Order { get; set; }

    // Short aliases keep the model convenient for editor and test callers while
    // retaining one canonical persisted identity.
    [JsonIgnore]
    public string PortId { get => Id; set => Id = value; }
    [JsonIgnore]
    public string Name { get => DisplayName; set => DisplayName = value; }
    [JsonIgnore]
    public bool IsOutput { get => !IsInput; set => IsInput = !value; }
    [JsonIgnore]
    public GraphInterfaceKind InterfaceKind { get => Kind; set => Kind = value; }
}
