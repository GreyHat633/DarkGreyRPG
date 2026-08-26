using System.Text.Json.Serialization;

namespace DarkGreyRPG.Studio.Core.Projects;

public sealed class ProjectResource
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
}
