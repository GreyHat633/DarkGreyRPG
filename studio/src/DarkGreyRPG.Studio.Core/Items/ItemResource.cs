using System.Text.Json.Serialization;

namespace DarkGreyRPG.Studio.Core.Items;

/// <summary>The two resource shapes supported by the Studio item registry.</summary>
public abstract class ItemResource
{
    public const int CurrentSchemaVersion = 1;

    [JsonPropertyName("schema_version")]
    [JsonPropertyOrder(0)]
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    [JsonPropertyName("type")]
    [JsonPropertyOrder(1)]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("display_name")]
    [JsonPropertyOrder(2)]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("tags")]
    [JsonPropertyOrder(3)]
    public List<string> Tags { get; init; } = [];

    /// <summary>Stable key regardless of whether this is an Item ID or Group ID.</summary>
    [JsonIgnore]
    public abstract string Id { get; }
}

public sealed class IndividualItemResource : ItemResource
{
    public const string ResourceType = "individual";

    public IndividualItemResource()
    {
        Type = ResourceType;
    }

    [JsonPropertyName("item_id")]
    [JsonPropertyOrder(4)]
    public string ItemId { get; init; } = string.Empty;

    [JsonIgnore]
    public override string Id => ItemId;
}

public sealed class CollectiveItemResource : ItemResource
{
    public const string ResourceType = "collective";

    public CollectiveItemResource()
    {
        Type = ResourceType;
    }

    [JsonPropertyName("group_id")]
    [JsonPropertyOrder(4)]
    public string GroupId { get; init; } = string.Empty;

    [JsonIgnore]
    public override string Id => GroupId;
}
