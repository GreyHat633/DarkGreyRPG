using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

public sealed class CanonicalStoryMembershipSet
{
    public List<string> Actors { get; set; } = [];
    public List<string> Items { get; set; } = [];
    public List<string> ItemGroups { get; set; } = [];
    public List<string> Sessions { get; set; } = [];
    public List<string> Tasks { get; set; } = [];

    public CanonicalStoryMembershipSet Clone() => new()
    {
        Actors = [.. (Actors ?? [])],
        Items = [.. (Items ?? [])],
        ItemGroups = [.. (ItemGroups ?? [])],
        Sessions = [.. (Sessions ?? [])],
        Tasks = [.. (Tasks ?? [])],
    };
}

/// <summary>Strict membership identity beside canonical graph envelopes.</summary>
public sealed class CanonicalStoryMembershipManifest
{
    public const int LegacySchemaVersion = 1;
    public const int CurrentSchemaVersion = 2;

    private CanonicalStoryMembershipSet _ownedResources = new();
    private CanonicalStoryMembershipSet _referencedResources = new();

    public CanonicalStoryMembershipManifest() { }

    public CanonicalStoryMembershipManifest(
        string storyId,
        CanonicalStoryMembershipSet? ownedResources = null,
        CanonicalStoryMembershipSet? referencedResources = null)
    {
        StoryId = storyId;
        OwnedResources = ownedResources ?? new();
        ReferencedResources = referencedResources ?? new();
    }

    public int SchemaVersion { get; set; } = CurrentSchemaVersion;
    public string StoryId { get; set; } = string.Empty;
    public CanonicalStoryMembershipSet OwnedResources
    {
        get => _ownedResources.Clone();
        set => _ownedResources = (value ?? throw new ArgumentNullException(nameof(value))).Clone();
    }
    public CanonicalStoryMembershipSet ReferencedResources
    {
        get => _referencedResources.Clone();
        set => _referencedResources = (value ?? throw new ArgumentNullException(nameof(value))).Clone();
    }

    public string ToJson(bool indented = true)
        => CanonicalStoryMembershipSerializer.Serialize(this, indented);

    public static CanonicalStoryMembershipManifest FromJson(string json)
        => CanonicalStoryMembershipSerializer.Deserialize(json);

    internal CanonicalStoryMembershipSet SnapshotOwned() => _ownedResources.Clone();
    internal CanonicalStoryMembershipSet SnapshotReferenced() => _referencedResources.Clone();
}

public sealed class CanonicalStoryMembershipException : Exception
{
    public CanonicalStoryMembershipException(string code, string message, Exception? innerException = null)
        : base($"{code}: {message}", innerException)
    {
        Code = code;
    }

    public string Code { get; }
}

public static class CanonicalStoryMembershipSerializer
{
    private static readonly Regex IdPattern = new(
        "^[a-z0-9][a-z0-9_-]*$",
        RegexOptions.CultureInvariant | RegexOptions.Compiled);
    private static readonly string[] RootMembers =
        ["schema_version", "story_id", "owned_resources", "referenced_resources"];

    public static string Serialize(CanonicalStoryMembershipManifest manifest, bool indented = true)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        Validate(manifest);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions
        {
            Indented = indented,
            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schema_version", manifest.SchemaVersion);
            writer.WriteString("story_id", manifest.StoryId);
            WriteSet(writer, "owned_resources", manifest.SnapshotOwned(), manifest.SchemaVersion);
            WriteSet(writer, "referenced_resources", manifest.SnapshotReferenced(), manifest.SchemaVersion);
            writer.WriteEndObject();
        }
        return System.Text.Encoding.UTF8.GetString(stream.ToArray())
            .Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }

    public static CanonicalStoryMembershipManifest Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw Failure("story.membership.json.required", "Story membership JSON is required.");
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw Failure("story.membership.root.invalid", "Story membership root must be an object.");
            EnsureExactMembers(root, RootMembers, "story.membership.root");
            var version = Required(root, "schema_version").GetInt32();
            if (version is not (CanonicalStoryMembershipManifest.LegacySchemaVersion or CanonicalStoryMembershipManifest.CurrentSchemaVersion))
                throw Failure("story.membership.schema_version.unsupported",
                    $"Unsupported Story membership schema_version {version}.");
            var manifest = new CanonicalStoryMembershipManifest(
                RequiredString(root, "story_id"),
                ReadSet(Required(root, "owned_resources"), "owned_resources", version),
                ReadSet(Required(root, "referenced_resources"), "referenced_resources", version))
            {
                SchemaVersion = version,
            };
            Validate(manifest);
            return manifest;
        }
        catch (CanonicalStoryMembershipException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException
            or FormatException or ArgumentException)
        {
            throw new CanonicalStoryMembershipException(
                "story.membership.invalid",
                "Story membership JSON is invalid or contains unsupported fields.",
                exception);
        }
    }

    public static void Validate(CanonicalStoryMembershipManifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        if (manifest.SchemaVersion is not (CanonicalStoryMembershipManifest.LegacySchemaVersion or CanonicalStoryMembershipManifest.CurrentSchemaVersion))
            throw Failure("story.membership.schema_version.unsupported",
                $"Unsupported Story membership schema_version {manifest.SchemaVersion}.");
        ValidateId(manifest.StoryId, "story.membership.story_id.invalid", "Story ID");
        var owned = manifest.SnapshotOwned();
        var referenced = manifest.SnapshotReferenced();
        ValidateList(owned.Actors, "actor", "owned_resources.actors");
        ValidateList(owned.Items, "item", "owned_resources.items");
        ValidateList(owned.ItemGroups, "item_group", "owned_resources.item_groups");
        ValidateList(owned.Sessions, "session", "owned_resources.sessions");
        ValidateList(owned.Tasks, "task", "owned_resources.tasks");
        ValidateList(referenced.Actors, "actor", "referenced_resources.actors");
        ValidateList(referenced.Items, "item", "referenced_resources.items");
        ValidateList(referenced.ItemGroups, "item_group", "referenced_resources.item_groups");
        ValidateList(referenced.Sessions, "session", "referenced_resources.sessions");
        ValidateList(referenced.Tasks, "task", "referenced_resources.tasks");
        if (manifest.SchemaVersion == CanonicalStoryMembershipManifest.LegacySchemaVersion
            && (owned.Items.Count != 0 || owned.ItemGroups.Count != 0
                || referenced.Items.Count != 0 || referenced.ItemGroups.Count != 0))
            throw Failure("story.membership.schema_version.legacy_items",
                "Schema_version 1 membership cannot contain items or item_groups.");
        ValidateNoOverlap(owned.Actors, referenced.Actors, "actor");
        ValidateNoOverlap(owned.Items, referenced.Items, "item");
        ValidateNoOverlap(owned.ItemGroups, referenced.ItemGroups, "item_group");
        ValidateNoOverlap(owned.Sessions, referenced.Sessions, "session");
        ValidateNoOverlap(owned.Tasks, referenced.Tasks, "task");
    }

    private static void WriteSet(Utf8JsonWriter writer, string name, CanonicalStoryMembershipSet set, int schemaVersion)
    {
        writer.WritePropertyName(name);
        writer.WriteStartObject();
        WriteIds(writer, "actors", set.Actors);
        if (schemaVersion == CanonicalStoryMembershipManifest.CurrentSchemaVersion)
        {
            WriteIds(writer, "items", set.Items);
            WriteIds(writer, "item_groups", set.ItemGroups);
        }
        WriteIds(writer, "sessions", set.Sessions);
        WriteIds(writer, "tasks", set.Tasks);
        writer.WriteEndObject();
    }

    private static void WriteIds(Utf8JsonWriter writer, string name, IEnumerable<string> ids)
    {
        writer.WriteStartArray(name);
        foreach (var id in ids) writer.WriteStringValue(id);
        writer.WriteEndArray();
    }

    private static CanonicalStoryMembershipSet ReadSet(JsonElement element, string path, int schemaVersion)
    {
        if (element.ValueKind != JsonValueKind.Object)
            throw Failure("story.membership.set.invalid", $"'{path}' must be an object.");
        var members = schemaVersion == CanonicalStoryMembershipManifest.LegacySchemaVersion
            ? LegacyMembershipMembers
            : CurrentMembershipMembers;
        EnsureExactMembers(element, members, "story.membership.set");
        return new CanonicalStoryMembershipSet
        {
            Actors = ReadIds(Required(element, "actors"), path + ".actors"),
            Items = schemaVersion == CanonicalStoryMembershipManifest.CurrentSchemaVersion
                ? ReadIds(Required(element, "items"), path + ".items")
                : [],
            ItemGroups = schemaVersion == CanonicalStoryMembershipManifest.CurrentSchemaVersion
                ? ReadIds(Required(element, "item_groups"), path + ".item_groups")
                : [],
            Sessions = ReadIds(Required(element, "sessions"), path + ".sessions"),
            Tasks = ReadIds(Required(element, "tasks"), path + ".tasks"),
        };
    }

    private static List<string> ReadIds(JsonElement element, string path)
    {
        if (element.ValueKind != JsonValueKind.Array)
            throw Failure("story.membership.list.invalid", $"'{path}' must be an array.");
        var result = new List<string>();
        foreach (var value in element.EnumerateArray())
        {
            if (value.ValueKind != JsonValueKind.String)
                throw Failure("story.membership.id.type", $"'{path}' entries must be strings.");
            result.Add(value.GetString() ?? string.Empty);
        }
        return result;
    }

    private static void ValidateList(IReadOnlyList<string>? ids, string kind, string path)
    {
        if (ids is null)
            throw Failure($"story.membership.{kind}.list.required", $"'{path}' cannot be null.");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var id in ids)
        {
            ValidateId(id, $"story.membership.{kind}.id.invalid", $"{kind} ID");
            if (!seen.Add(id))
                throw Failure($"story.membership.{kind}.id.duplicate",
                    $"'{path}' contains duplicate ID '{id}'.");
        }
    }

    private static void ValidateNoOverlap(
        IEnumerable<string> owned,
        IEnumerable<string> referenced,
        string kind)
    {
        var ownedSet = owned.ToHashSet(StringComparer.Ordinal);
        var overlap = referenced.FirstOrDefault(ownedSet.Contains);
        if (overlap is not null)
            throw Failure($"story.membership.{kind}.ownership.overlap",
                $"{kind} ID '{overlap}' cannot be both owned and referenced.");
    }

    private static void ValidateId(string? id, string code, string label)
    {
        if (string.IsNullOrWhiteSpace(id) || !IdPattern.IsMatch(id))
            throw Failure(code, $"{label} '{id}' must match [a-z0-9][a-z0-9_-]*.");
    }

    private static readonly string[] LegacyMembershipMembers = ["actors", "sessions", "tasks"];
    private static readonly string[] CurrentMembershipMembers = ["actors", "items", "item_groups", "sessions", "tasks"];

    private static void EnsureExactMembers(JsonElement element, IReadOnlyList<string> expected, string codePrefix)
    {
        var expectedSet = expected.ToHashSet(StringComparer.Ordinal);
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in element.EnumerateObject())
        {
            if (!expectedSet.Contains(property.Name))
                throw Failure(codePrefix + ".member.unsupported", $"Unsupported field '{property.Name}'.");
            if (!seen.Add(property.Name))
                throw Failure(codePrefix + ".member.duplicate", $"Duplicate field '{property.Name}'.");
        }
        foreach (var member in expectedSet)
            if (!seen.Contains(member))
                throw Failure(codePrefix + ".member.required", $"Required field '{member}' is missing.");
    }

    private static JsonElement Required(JsonElement element, string name)
        => element.TryGetProperty(name, out var value)
            ? value
            : throw Failure("story.membership.member.required", $"Required field '{name}' is missing.");

    private static string RequiredString(JsonElement element, string name)
    {
        var value = Required(element, name);
        if (value.ValueKind != JsonValueKind.String)
            throw Failure("story.membership.member.type", $"Field '{name}' must be a string.");
        return value.GetString() ?? string.Empty;
    }

    private static CanonicalStoryMembershipException Failure(string code, string message)
        => new(code, message);
}
