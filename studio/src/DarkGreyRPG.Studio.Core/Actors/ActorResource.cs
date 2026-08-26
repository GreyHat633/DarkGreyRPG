using System.Text.Json.Serialization;

namespace DarkGreyRPG.Studio.Core.Actors;

public sealed class ActorResource
{
    public const int LegacySchemaVersion = 1;
    public const int CurrentSchemaVersion = 2;

    [JsonPropertyName("schema_version")]
    [JsonPropertyOrder(0)]
    // A default-constructed resource is still a legacy value until it is saved
    // through ActorDocument/ActorSerializer. This keeps callers that build a
    // schema-1 resource explicitly source-compatible with 2.0.
    public int SchemaVersion { get; init; } = LegacySchemaVersion;

    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("display_name")]
    [JsonPropertyOrder(2)]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("notes")]
    [JsonPropertyOrder(3)]
    public string Notes { get; init; } = string.Empty;

    [JsonPropertyName("tags")]
    [JsonPropertyOrder(4)]
    public List<string> Tags { get; init; } = [];

    [JsonPropertyName("home_story_id")]
    [JsonPropertyOrder(5)]
    public string? HomeStoryId { get; init; }

    public ActorResource WithId(string id) => new()
    {
        SchemaVersion = SchemaVersion,
        Id = id,
        DisplayName = DisplayName,
        Notes = Notes,
        Tags = [.. Tags],
        HomeStoryId = HomeStoryId,
    };
}
