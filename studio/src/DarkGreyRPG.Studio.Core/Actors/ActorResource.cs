using System.Text.Json.Serialization;

namespace DarkGreyRPG.Studio.Core.Actors;

/// <summary>
/// The legacy Actor shape and common in-memory fields for all Actor resources.
/// Schema 4 resources should normally use one of the typed resource classes.
/// </summary>
public class ActorResource
{
    public const int LegacySchemaVersion = 1;
    public const int StorySchemaVersion = 2;
    public const int CurrentSchemaVersion = 4;
    public const string LegacyResourceType = "legacy";

    [JsonPropertyName("schema_version")]
    [JsonPropertyOrder(0)]
    // A default-constructed resource remains the schema 1 legacy shape for
    // source compatibility with Studio 2.x callers.
    public int SchemaVersion { get; set; } = LegacySchemaVersion;

    [JsonPropertyName("type")]
    [JsonPropertyOrder(1)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Type { get; set; }

    private string _legacyId = string.Empty;

    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public string Id
    {
        get => !string.IsNullOrEmpty(_legacyId) ? _legacyId : NpcId ?? GroupId ?? string.Empty;
        set => _legacyId = value ?? string.Empty;
    }

    [JsonIgnore]
    internal bool HasExplicitLegacyId => !string.IsNullOrEmpty(_legacyId);

    [JsonPropertyName("display_name")]
    [JsonPropertyOrder(2)]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("notes")]
    [JsonPropertyOrder(3)]
    public string Notes { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    [JsonPropertyOrder(4)]
    public List<string> Tags { get; set; } = [];

    [JsonPropertyName("home_story_id")]
    [JsonPropertyOrder(5)]
    public string? HomeStoryId { get; set; }

    [JsonPropertyName("npc_id")]
    [JsonPropertyOrder(2)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? NpcId { get; set; }

    [JsonPropertyName("group_id")]
    [JsonPropertyOrder(2)]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? GroupId { get; set; }

    [JsonPropertyName("default_portrait_ref")]
    public string? DefaultPortraitRef { get; set; }

    [JsonPropertyName("portrait_variants")]
    public List<ActorPortraitVariant> PortraitVariants { get; set; } = [];

    public virtual ActorResource WithId(string id) => new()
    {
        SchemaVersion = SchemaVersion,
        Type = Type,
        Id = id,
        DisplayName = DisplayName,
        Notes = Notes,
        Tags = [.. Tags],
        HomeStoryId = HomeStoryId,
        DefaultPortraitRef = DefaultPortraitRef,
        PortraitVariants = PortraitVariants.Select(value => value with { }).ToList(),
        NpcId = NpcId,
        GroupId = GroupId,
    };
}

public sealed class IndividualActorResource : ActorResource
{
    public const string ResourceType = "individual";

    public IndividualActorResource()
    {
        SchemaVersion = CurrentSchemaVersion;
        Type = ResourceType;
    }

    public override ActorResource WithId(string id) => new IndividualActorResource
    {
        NpcId = id,
        DisplayName = DisplayName,
        Tags = Tags is null ? [] : [.. Tags],
        HomeStoryId = HomeStoryId,
        DefaultPortraitRef = DefaultPortraitRef,
        PortraitVariants = PortraitVariants.Select(value => value with { }).ToList(),
    };
}

public sealed class CollectiveActorResource : ActorResource
{
    public const string ResourceType = "collective";

    public CollectiveActorResource()
    {
        SchemaVersion = CurrentSchemaVersion;
        Type = ResourceType;
    }

    public override ActorResource WithId(string id) => new CollectiveActorResource
    {
        GroupId = id,
        DisplayName = DisplayName,
        Tags = Tags is null ? [] : [.. Tags],
        HomeStoryId = HomeStoryId,
        DefaultPortraitRef = DefaultPortraitRef,
        PortraitVariants = PortraitVariants.Select(value => value with { }).ToList(),
    };
}

public sealed record ActorPortraitVariant(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("media_ref")] string MediaRef);
