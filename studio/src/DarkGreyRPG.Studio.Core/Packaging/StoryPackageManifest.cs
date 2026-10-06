using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DarkGreyRPG.Studio.Core.Packaging;

/// <summary>Stable metadata for one server-installed Story package.</summary>
public sealed class StoryPackageManifest
{
    public const int CurrentSchemaVersion = 2;
    public const string CurrentFormat = "dgrs";
    public const int CurrentFormatVersion = 2;

    [JsonPropertyName("identity_format")]
    public string IdentityFormat { get; init; } = "story-uid-v1";

    [JsonPropertyName("format")]
    [JsonPropertyOrder(0)]
    public string Format { get; init; } = CurrentFormat;
    [JsonPropertyName("format_version")]
    [JsonPropertyOrder(1)]
    public int FormatVersion { get; init; } = CurrentFormatVersion;
    [JsonPropertyName("producer")]
    [JsonPropertyOrder(2)]
    public string Producer { get; init; } = "DarkGreyRPGStudio";
    [JsonPropertyName("producer_version")]
    [JsonPropertyOrder(3)]
    public string ProducerVersion { get; init; } = string.Empty;

    [JsonPropertyName("schema_version")]
    [JsonPropertyOrder(4)]
    public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    [JsonPropertyName("package_id")]
    [JsonPropertyOrder(5)]
    public string PackageId { get; init; } = string.Empty;
    [JsonPropertyName("package_version")]
    [JsonPropertyOrder(6)]
    public string PackageVersion { get; init; } = string.Empty;
    [JsonPropertyName("story_id")]
    [JsonPropertyOrder(7)]
    public string StoryId { get; init; } = string.Empty;
    [JsonPropertyName("story_schema_version")]
    [JsonPropertyOrder(8)]
    public int StorySchemaVersion { get; init; }
    [JsonPropertyName("required_resources")]
    [JsonPropertyOrder(9)]
    public StoryPackageRequiredResources RequiredResources { get; init; } = new();

    public static StoryPackageManifest Read(string path)
    {
        try
        {
            return Parse(File.ReadAllText(path), path);
        }
        catch (StoryPackageException) { throw; }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        { throw new StoryPackageException($"Could not read package manifest '{path}'.", exception); }
    }

    public static StoryPackageManifest Parse(string json, string source = "manifest.json")
    {
        var options = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = false,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
        try
        {
            using var document = JsonDocument.Parse(json);
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object
                || root.EnumerateObject().Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != root.EnumerateObject().Count()
                || !root.TryGetProperty("identity_format", out var identity) || identity.ValueKind != JsonValueKind.String || identity.GetString() != "story-uid-v1"
                || !root.TryGetProperty("format_version", out _)
                || !root.TryGetProperty("schema_version", out _))
                throw new StoryPackageException("Current DGRS identity and version fields are required.");
            var result = JsonSerializer.Deserialize<StoryPackageManifest>(json, options)
                ?? throw new StoryPackageException("Package manifest is empty.");
            Validate(result);
            return result;
        }
        catch (StoryPackageException) { throw; }
        catch (JsonException exception)
        {
            throw new StoryPackageException($"Could not parse package manifest '{source}'.", exception);
        }
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
        if (manifest.IdentityFormat != "story-uid-v1") throw new StoryPackageException("Unsupported package identity format.");
        if (!string.Equals(manifest.Format, CurrentFormat, StringComparison.Ordinal))
            throw new StoryPackageException($"Unsupported package format '{manifest.Format}'.");
        if (manifest.FormatVersion != CurrentFormatVersion)
            throw new StoryPackageException($"Unsupported DGRS format_version {manifest.FormatVersion}.");
        if (!string.Equals(manifest.Producer, "DarkGreyRPGStudio", StringComparison.Ordinal))
            throw new StoryPackageException("producer must be 'DarkGreyRPGStudio'.");
        if (string.IsNullOrWhiteSpace(manifest.ProducerVersion))
            throw new StoryPackageException("producer_version is required.");
        if (manifest.SchemaVersion != CurrentSchemaVersion) throw new StoryPackageException($"Unsupported package schema_version {manifest.SchemaVersion}.");
        RequireId(manifest.PackageId, "package_id");
        if (string.IsNullOrWhiteSpace(manifest.PackageVersion)) throw new StoryPackageException("package_version is required.");
        RequireId(manifest.StoryId, "story_id");
        if (manifest.StorySchemaVersion != 2) throw new StoryPackageException("story_schema_version must be positive.");
        if (manifest.RequiredResources is null) throw new StoryPackageException("required_resources is required.");
        manifest.RequiredResources.Validate();
        if (manifest.PackageId != manifest.StoryId
            || manifest.RequiredResources.Dialogues.Count != 0 || manifest.RequiredResources.Quests.Count != 0
            || manifest.RequiredResources.CanonicalStories.Count != 1
            || manifest.RequiredResources.CanonicalStories[0] != manifest.RequiredResources.Story
            || manifest.RequiredResources.CanonicalMemberships.Count != 1
            || !manifest.RequiredResources.Story.StartsWith("resources/canonical/stories/", StringComparison.Ordinal))
            throw new StoryPackageException("A current single Story container requires exactly one canonical Story and membership.");
    }

    private static void RequireId(string value, string field)
    {
        if (!DarkGreyRPG.Studio.Core.Identity.StoryUid.IsValid(value))
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
    [JsonPropertyName("media")] public List<string> Media { get; init; } = [];
    [JsonPropertyName("story_logic_graph")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? StoryLogicGraph { get; init; }

    internal void Validate()
    {
        ValidatePath(Story, "required_resources.story");
        foreach (var list in new[] { Actors, Items, ItemGroups, Dialogues, Quests, CanonicalStories, CanonicalMemberships, Sessions, Tasks, Media })
        {
            if (list is null || list.Any(string.IsNullOrWhiteSpace) || list.Count != list.Distinct(StringComparer.Ordinal).Count())
                throw new StoryPackageException("required_resources contains a null or duplicate resource path.");
            foreach (var path in list) ValidatePath(path, "required_resources");
        }
        if (Media.Any(path => !DarkGreyRPG.Studio.Core.Graphs.Resources.MediaReference.IsValid(path))) throw new StoryPackageException("Invalid media reference path.");
        if (StoryLogicGraph is not null && string.IsNullOrWhiteSpace(StoryLogicGraph))
            throw new StoryPackageException("required_resources.story_logic_graph must be a nonblank path when present.");
        if (StoryLogicGraph is not null) ValidatePath(StoryLogicGraph, "required_resources.story_logic_graph");
    }

    private static void ValidatePath(string path, string field)
    {
        if (string.IsNullOrWhiteSpace(path)
            || path.Contains('\\', StringComparison.Ordinal)
            || path.StartsWith("/", StringComparison.Ordinal)
            || path.Contains(':', StringComparison.Ordinal)
            || path.Split('/').Any(segment => segment.Length == 0 || segment is "." or ".."))
            throw new StoryPackageException($"{field} contains unsafe resource path '{path}'.");
    }
}

public sealed class StoryPackageException : Exception
{
    public IReadOnlyList<StoryPackageGraphIssue> GraphIssues { get; } = [];

    public StoryPackageException(string message) : base(message) { }
    public StoryPackageException(string message, Exception innerException) : base(message, innerException) { }

    internal StoryPackageException(IReadOnlyList<StoryPackageGraphIssue> issues)
        : base($"Canonical resource '{issues[0].ResourcePath}' failed validation: {issues[0].Issue.Code}: {issues[0].Issue.Message}")
        => GraphIssues = issues.ToArray();
}
