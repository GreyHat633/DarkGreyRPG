using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DarkGreyRPG.Studio.Core.Packaging;

/// <summary>Stable metadata for one server-installed Story package.</summary>
public sealed class StoryPackageManifest
{
    public const int CurrentSchemaVersion = 1;

    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    [JsonPropertyName("package_id")]
    public string PackageId { get; init; } = string.Empty;
    [JsonPropertyName("package_version")]
    public string PackageVersion { get; init; } = string.Empty;
    [JsonPropertyName("story_id")]
    public string StoryId { get; init; } = string.Empty;
    [JsonPropertyName("story_schema_version")]
    public int StorySchemaVersion { get; init; }
    [JsonPropertyName("required_resources")]
    public StoryPackageRequiredResources RequiredResources { get; init; } = new();

    public static StoryPackageManifest Read(string path)
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = false, UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow };
        try
        {
            var result = JsonSerializer.Deserialize<StoryPackageManifest>(File.ReadAllText(path), options)
                ?? throw new StoryPackageException("Package manifest is empty.");
            Validate(result);
            return result;
        }
        catch (StoryPackageException) { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
        { throw new StoryPackageException($"Could not read package manifest '{path}'.", exception); }
    }

    public string ToJson()
    {
        Validate(this);
        var options = new JsonSerializerOptions { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };
        return JsonSerializer.Serialize(this, options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    public static void Validate(StoryPackageManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.SchemaVersion != CurrentSchemaVersion) throw new StoryPackageException($"Unsupported package schema_version {manifest.SchemaVersion}.");
        RequireId(manifest.PackageId, "package_id");
        if (string.IsNullOrWhiteSpace(manifest.PackageVersion)) throw new StoryPackageException("package_version is required.");
        RequireId(manifest.StoryId, "story_id");
        if (manifest.StorySchemaVersion <= 0) throw new StoryPackageException("story_schema_version must be positive.");
        if (manifest.RequiredResources is null) throw new StoryPackageException("required_resources is required.");
        manifest.RequiredResources.Validate();
    }

    private static void RequireId(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Any(ch => !(char.IsLetterOrDigit(ch) || ch is '_' or '-' or '.')) || !char.IsLetterOrDigit(value[0]))
            throw new StoryPackageException($"{field} must be a stable resource ID.");
    }
}

public sealed class StoryPackageRequiredResources
{
    [JsonPropertyName("story")] public string Story { get; init; } = string.Empty;
    [JsonPropertyName("actors")] public List<string> Actors { get; init; } = [];
    [JsonPropertyName("items")] public List<string> Items { get; init; } = [];
    [JsonPropertyName("item_groups")] public List<string> ItemGroups { get; init; } = [];
    [JsonPropertyName("dialogues")] public List<string> Dialogues { get; init; } = [];
    [JsonPropertyName("quests")] public List<string> Quests { get; init; } = [];
    [JsonPropertyName("canonical_stories")] public List<string> CanonicalStories { get; init; } = [];
    [JsonPropertyName("canonical_memberships")] public List<string> CanonicalMemberships { get; init; } = [];
    [JsonPropertyName("sessions")] public List<string> Sessions { get; init; } = [];
    [JsonPropertyName("tasks")] public List<string> Tasks { get; init; } = [];

    internal void Validate()
    {
        if (string.IsNullOrWhiteSpace(Story)) throw new StoryPackageException("required_resources.story is required.");
        foreach (var list in new[] { Actors, Items, ItemGroups, Dialogues, Quests, CanonicalStories, CanonicalMemberships, Sessions, Tasks })
        {
            if (list is null || list.Any(string.IsNullOrWhiteSpace) || list.Count != list.Distinct(StringComparer.Ordinal).Count())
                throw new StoryPackageException("required_resources contains a null or duplicate resource path.");
        }
    }
}

public sealed class StoryPackageException : Exception
{
    public StoryPackageException(string message) : base(message) { }
    public StoryPackageException(string message, Exception innerException) : base(message, innerException) { }
}
