using System.Text.Json;
using System.Text.Json.Serialization;

namespace DarkGreyRPG.Studio.Core.Graphs;

/// <summary>UI-independent graph document used by Story, Session, and Task scopes.</summary>
public sealed class GraphDocument
{
    public GraphDocument() { }

    public GraphDocument(IEnumerable<GraphNode>? nodes, IEnumerable<GraphConnection>? connections = null)
    {
        Nodes = nodes?.ToList() ?? [];
        Connections = connections?.ToList() ?? [];
    }

    [JsonPropertyName("nodes")]
    [JsonPropertyOrder(0)]
    public List<GraphNode> Nodes { get; set; } = [];

    [JsonPropertyName("connections")]
    [JsonPropertyOrder(1)]
    public List<GraphConnection> Connections { get; set; } = [];

    [JsonIgnore]
    public List<GraphConnection> Edges { get => Connections; set => Connections = value; }

    public string ToJson(bool indented = false) => GraphSerializer.Serialize(this, indented);

    public static GraphDocument FromJson(string json) => GraphSerializer.Deserialize(json);
}

/// <summary>Canonical JSON entry point for graph documents and individual edges.</summary>
public static class GraphSerializer
{
    public static JsonSerializerOptions Options { get; } = new(JsonSerializerDefaults.General)
    {
        PropertyNameCaseInsensitive = false,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        AllowTrailingCommas = false,
        WriteIndented = false,
    };

    public static string Serialize(GraphDocument graph, bool indented = false)
    {
        ArgumentNullException.ThrowIfNull(graph);
        var options = new JsonSerializerOptions(Options) { WriteIndented = indented };
        return JsonSerializer.Serialize(graph, options);
    }

    public static GraphDocument Deserialize(string json)
        => JsonSerializer.Deserialize<GraphDocument>(json, Options)
            ?? throw new JsonException("Graph document JSON cannot be null.");

    public static string SerializeConnection(GraphConnection connection, bool indented = false)
    {
        ArgumentNullException.ThrowIfNull(connection);
        var options = new JsonSerializerOptions(Options) { WriteIndented = indented };
        return JsonSerializer.Serialize(connection, options);
    }

    public static GraphConnection DeserializeConnection(string json)
        => JsonSerializer.Deserialize<GraphConnection>(json, Options)
            ?? throw new JsonException("Graph connection JSON cannot be null.");
}

/// <summary>Explicitly named facade for callers serializing only connections.</summary>
public static class GraphConnectionSerializer
{
    public static string Serialize(GraphConnection connection, bool indented = false)
        => GraphSerializer.SerializeConnection(connection, indented);

    public static GraphConnection Deserialize(string json)
        => GraphSerializer.DeserializeConnection(json);
}
