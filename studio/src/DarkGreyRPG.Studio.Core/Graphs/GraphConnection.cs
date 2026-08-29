using System.Text.Json.Serialization;

namespace DarkGreyRPG.Studio.Core.Graphs;

/// <summary>The canonical persisted edge. Node and port IDs are stable identities.</summary>
public sealed class GraphConnection : IEquatable<GraphConnection>
{
    public GraphConnection() { }

    public GraphConnection(string fromNodeId, string fromPortId, string toNodeId, string toPortId, GraphInterfaceKind interfaceKind)
    {
        FromNodeId = fromNodeId;
        FromPortId = fromPortId;
        ToNodeId = toNodeId;
        ToPortId = toPortId;
        InterfaceKind = interfaceKind;
    }

    [JsonPropertyName("from_node_id")]
    [JsonPropertyOrder(0)]
    public string FromNodeId { get; set; } = string.Empty;

    [JsonPropertyName("from_port_id")]
    [JsonPropertyOrder(1)]
    public string FromPortId { get; set; } = string.Empty;

    [JsonPropertyName("to_node_id")]
    [JsonPropertyOrder(2)]
    public string ToNodeId { get; set; } = string.Empty;

    [JsonPropertyName("to_port_id")]
    [JsonPropertyOrder(3)]
    public string ToPortId { get; set; } = string.Empty;

    [JsonPropertyName("interface_kind")]
    [JsonPropertyOrder(4)]
    public GraphInterfaceKind InterfaceKind { get; set; }

    [JsonIgnore]
    public string FromNode { get => FromNodeId; set => FromNodeId = value; }
    [JsonIgnore]
    public string FromPort { get => FromPortId; set => FromPortId = value; }
    [JsonIgnore]
    public string ToNode { get => ToNodeId; set => ToNodeId = value; }
    [JsonIgnore]
    public string ToPort { get => ToPortId; set => ToPortId = value; }
    [JsonIgnore]
    public GraphInterfaceKind Kind { get => InterfaceKind; set => InterfaceKind = value; }

    public string ToJson(bool indented = false) => GraphSerializer.SerializeConnection(this, indented);
    public static GraphConnection FromJson(string json) => GraphSerializer.DeserializeConnection(json);

    public bool Equals(GraphConnection? other) => other is not null
        && string.Equals(FromNodeId, other.FromNodeId, StringComparison.Ordinal)
        && string.Equals(FromPortId, other.FromPortId, StringComparison.Ordinal)
        && string.Equals(ToNodeId, other.ToNodeId, StringComparison.Ordinal)
        && string.Equals(ToPortId, other.ToPortId, StringComparison.Ordinal)
        && InterfaceKind == other.InterfaceKind;

    public override bool Equals(object? obj) => Equals(obj as GraphConnection);
    public override int GetHashCode() => HashCode.Combine(FromNodeId, FromPortId, ToNodeId, ToPortId, InterfaceKind);
}
