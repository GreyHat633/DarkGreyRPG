using System.Text.Json;
using System.Text.Json.Serialization;

namespace DarkGreyRPG.Studio.Core.Stories;

public sealed class StoryResource
{
    public const int LegacySchemaVersion = 1;
    public const int CurrentSchemaVersion = 2;

    [JsonPropertyName("schema_version")]
    [JsonPropertyOrder(0)]
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;

    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("display_name")]
    [JsonPropertyOrder(2)]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    [JsonPropertyOrder(3)]
    public string Description { get; init; } = string.Empty;

    [JsonPropertyName("tags")]
    [JsonPropertyOrder(4)]
    public List<string> Tags { get; init; } = [];

    [JsonPropertyName("entry_presentation")]
    [JsonPropertyOrder(5)]
    public StoryEntryPresentation EntryPresentation { get; init; } = new();

    [JsonPropertyName("owned_resources")]
    [JsonPropertyOrder(6)]
    public StoryMembership OwnedResources { get; init; } = new();

    [JsonPropertyName("referenced_resources")]
    [JsonPropertyOrder(7)]
    public StoryMembership ReferencedResources { get; init; } = new();

    [JsonPropertyName("flow_ref")]
    [JsonPropertyOrder(8)]
    public string FlowRef { get; init; } = string.Empty;

    // These fields deliberately mirror StoryLoader's runtime contract.
    [JsonPropertyName("title")]
    [JsonPropertyOrder(9)]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("entry")]
    [JsonPropertyOrder(10)]
    public string Entry { get; init; } = string.Empty;

    [JsonPropertyName("nodes")]
    [JsonPropertyOrder(11)]
    public List<StoryNodeResource> Nodes { get; init; } = [];

    [JsonPropertyName("connections")]
    [JsonPropertyOrder(12)]
    public List<StoryConnectionResource> Connections { get; init; } = [];

    [JsonPropertyName("metadata")]
    [JsonPropertyOrder(13)]
    public StoryMetadata Metadata { get; init; } = new();

    public static StoryResource CreateUncategorized() => new()
    {
        Id = "uncategorized",
        DisplayName = "未分类",
        Description = string.Empty,
        Tags = [],
        EntryPresentation = new(),
        OwnedResources = new(),
        ReferencedResources = new(),
        FlowRef = "uncategorized",
        Title = "未分类",
        Entry = "end",
        Nodes = [new StoryNodeResource
        {
            Id = "end",
            Type = "END",
            Position = new StoryNodePosition(),
            Properties = new(),
        }],
        Connections = [],
    };
}

public sealed class StoryEntryPresentation
{
    [JsonPropertyName("mode")]
    [JsonPropertyOrder(0)]
    public string Mode { get; init; } = "none";

    [JsonPropertyName("eyebrow")]
    [JsonPropertyOrder(1)]
    public string Eyebrow { get; init; } = string.Empty;

    [JsonPropertyName("title")]
    [JsonPropertyOrder(2)]
    public string Title { get; init; } = string.Empty;

    [JsonPropertyName("duration_seconds")]
    [JsonPropertyOrder(3)]
    public double DurationSeconds { get; init; } = 4.0;
}

public sealed class StoryMetadata
{
    [JsonPropertyName("notes")]
    public string Notes { get; init; } = string.Empty;

    [JsonPropertyName("tags")]
    public List<string> Tags { get; init; } = [];
}

public sealed class StoryMembership
{
    [JsonPropertyName("actors")]
    [JsonPropertyOrder(0)]
    public List<string> Actors { get; init; } = [];

    [JsonPropertyName("dialogues")]
    [JsonPropertyOrder(1)]
    public List<string> Dialogues { get; init; } = [];

    [JsonPropertyName("quests")]
    [JsonPropertyOrder(2)]
    public List<string> Quests { get; init; } = [];

    public StoryMembership Clone() => new()
    {
        Actors = [.. Actors],
        Dialogues = [.. Dialogues],
        Quests = [.. Quests],
    };
}

public sealed class StoryNodeResource
{
    [JsonPropertyName("id")]
    [JsonPropertyOrder(0)]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    [JsonPropertyOrder(1)]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("position")]
    [JsonPropertyOrder(2)]
    public StoryNodePosition Position { get; init; } = new();

    [JsonPropertyName("properties")]
    [JsonPropertyOrder(3)]
    public Dictionary<string, JsonElement> Properties { get; init; } = new(StringComparer.Ordinal);
}

public sealed class StoryNodePosition
{
    [JsonPropertyName("x")]
    public double X { get; init; }

    [JsonPropertyName("y")]
    public double Y { get; init; }
}

public sealed class StoryConnectionResource
{
    [JsonPropertyName("from")]
    [JsonPropertyOrder(0)]
    public string From { get; init; } = string.Empty;

    [JsonPropertyName("output")]
    [JsonPropertyOrder(1)]
    public string Output { get; init; } = string.Empty;

    [JsonPropertyName("to")]
    [JsonPropertyOrder(2)]
    public string To { get; init; } = string.Empty;
}
