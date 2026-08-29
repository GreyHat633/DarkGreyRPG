using System.Text.Json;
using System.Text.Json.Serialization;

namespace DarkGreyRPG.Studio.Core.Graphs;

/// <summary>A graph node. Ports are addressed by ID, never by label or list order.</summary>
public sealed class GraphNode
{
    public GraphNode() { }

    public GraphNode(string id, string displayName, IEnumerable<GraphPort>? ports = null,
        IReadOnlyDictionary<string, JsonElement>? properties = null)
        : this(id, string.Empty, displayName, ports, properties) { }

    /// <summary>
    /// Creates a node with its canonical persisted type.  The two-argument
    /// constructor remains available for generic Graph Core callers which do
    /// not assign a scope-specific type.
    /// </summary>
    public GraphNode(string id, string type, string displayName, IEnumerable<GraphPort>? ports = null,
        IReadOnlyDictionary<string, JsonElement>? properties = null)
    {
        Id = id;
        Type = type;
        DisplayName = displayName;
        Ports = ports?.ToList() ?? [];
        Properties = CloneProperties(properties);
    }

    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>Stable, strict persisted node type (for example, <c>start</c>).</summary>
    [JsonPropertyName("type")]
    [JsonPropertyOrder(1)]
    public string Type { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    [JsonPropertyOrder(2)]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("ports")]
    [JsonPropertyOrder(3)]
    public List<GraphPort> Ports { get; set; } = [];

    private Dictionary<string, JsonElement> _properties = new(StringComparer.Ordinal);

    /// <summary>
    /// Untyped node parameters.  The graph core owns clones of all JSON values;
    /// scope-specific schemas are intentionally defined outside this model.
    /// </summary>
    [JsonPropertyName("properties")]
    [JsonPropertyOrder(4)]
    public Dictionary<string, JsonElement> Properties
    {
        get => _properties;
        set => _properties = CloneProperties(value);
    }

    [JsonIgnore]
    public string NodeId { get => Id; set => Id = value; }
    [JsonIgnore]
    public string Name { get => DisplayName; set => DisplayName = value; }
    [JsonIgnore]
    public string NodeType { get => Type; set => Type = value; }

    private static Dictionary<string, JsonElement> CloneProperties(
        IEnumerable<KeyValuePair<string, JsonElement>>? properties)
        => (properties ?? [])
            .OrderBy(pair => pair.Key, StringComparer.Ordinal)
            .ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal);
}
