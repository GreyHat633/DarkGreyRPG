using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using DarkGreyRPG.Studio.Core.IO;

namespace DarkGreyRPG.Studio.Core.Stories;

/// <summary>
/// Stores editor-only Story Flow drafts separately from the Runtime story directory.
/// Recovery data is intentionally not passed through StoryValidator or StoryRepository.
/// </summary>
public sealed class StoryFlowRecoveryStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        AllowTrailingCommas = false,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = false,
        ReadCommentHandling = JsonCommentHandling.Disallow,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        WriteIndented = true,
    };

    private static readonly Regex ValidStoryId = new(
        "^[a-z0-9][a-z0-9_.-]*\\z",
        RegexOptions.CultureInvariant | RegexOptions.NonBacktracking | RegexOptions.Compiled);

    public const int CurrentSchemaVersion = 1;

    private readonly IAtomicFileWriter _atomicFileWriter;

    public StoryFlowRecoveryStore(string projectDirectory, IAtomicFileWriter? atomicFileWriter = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        ProjectDirectory = Path.GetFullPath(projectDirectory);
        RecoveryDirectory = Path.Combine(ProjectDirectory, "resources", "editor", "recovery");
        _atomicFileWriter = atomicFileWriter ?? new AtomicFileWriter();
    }

    public string ProjectDirectory { get; }
    public string RecoveryDirectory { get; }

    /// <summary>Enumerates valid snapshots. Invalid files are isolated and skipped.</summary>
    public IReadOnlyList<StoryFlowRecoverySnapshot> Enumerate()
    {
        if (!Directory.Exists(RecoveryDirectory)) return [];

        var snapshots = new List<StoryFlowRecoverySnapshot>();
        IEnumerable<string> paths;
        try
        {
            paths = Directory.EnumerateFiles(RecoveryDirectory, "*.json", SearchOption.TopDirectoryOnly)
                .OrderBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            return [];
        }

        foreach (var path in paths)
        {
            if (TryRead(path, out var snapshot)) snapshots.Add(snapshot!);
        }

        return snapshots;
    }

    public IReadOnlyList<StoryFlowRecoverySnapshot> List() => Enumerate();

    /// <summary>Loads one valid snapshot, or null when it is absent or malformed.</summary>
    public StoryFlowRecoverySnapshot? Load(string storyId)
    {
        var normalizedId = ValidateStoryId(storyId);
        var path = GetSnapshotPath(normalizedId);
        return TryRead(path, out var snapshot) ? snapshot : null;
    }

    public StoryResource? LoadResource(string storyId) => Load(storyId)?.Resource;

    /// <summary>
    /// Atomically saves a deep clone of the draft in an editor-only envelope.
    /// Story validation is deliberately not performed here.
    /// </summary>
    public StoryFlowRecoverySnapshot Save(StoryResource resource, string? sourcePath = null)
    {
        ArgumentNullException.ThrowIfNull(resource);
        var storyId = ValidateStoryId(resource.Id);
        var snapshot = new StoryFlowRecoverySnapshot
        {
            SchemaVersion = CurrentSchemaVersion,
            StoryId = storyId,
            CapturedUtc = DateTimeOffset.UtcNow,
            SourcePath = sourcePath,
            Resource = Clone(resource),
        };

        var path = GetSnapshotPath(storyId);
        var json = JsonSerializer.Serialize(snapshot, JsonOptions);
        _atomicFileWriter.Write(path, json, temporaryPath =>
        {
            if (!TryRead(temporaryPath, out _, requireCanonicalFileName: false))
            {
                throw new InvalidDataException("The staged Story Flow recovery snapshot is invalid.");
            }
        });

        return snapshot;
    }

    /// <summary>Deletes exactly one valid snapshot; malformed files are left untouched.</summary>
    public bool Delete(string storyId)
    {
        var normalizedId = ValidateStoryId(storyId);
        var path = GetSnapshotPath(normalizedId);
        if (!File.Exists(path) || !TryRead(path, out _)) return false;
        File.Delete(path);
        return true;
    }

    private string GetSnapshotPath(string storyId) => Path.Combine(RecoveryDirectory, storyId + ".json");

    private static string ValidateStoryId(string? storyId)
    {
        if (string.IsNullOrWhiteSpace(storyId) || !ValidStoryId.IsMatch(storyId))
        {
            throw new ArgumentException("Story ID must be a non-blank Runtime-compatible ID.", nameof(storyId));
        }

        return storyId;
    }

    private static bool TryRead(string path, out StoryFlowRecoverySnapshot? snapshot, bool requireCanonicalFileName = true)
    {
        snapshot = null;
        try
        {
            if (!File.Exists(path)) return false;
            var candidate = JsonSerializer.Deserialize<StoryFlowRecoverySnapshot>(File.ReadAllText(path), JsonOptions);
            if (candidate is null || candidate.SchemaVersion != CurrentSchemaVersion
                || string.IsNullOrWhiteSpace(candidate.StoryId)
                || candidate.CapturedUtc == default
                || !ValidStoryId.IsMatch(candidate.StoryId)
                || candidate.Resource is null
                || !string.Equals(candidate.StoryId, candidate.Resource.Id, StringComparison.Ordinal)
                || (requireCanonicalFileName
                    && !string.Equals(Path.GetFileName(path), candidate.StoryId + ".json", StringComparison.Ordinal)))
            {
                return false;
            }

            snapshot = new StoryFlowRecoverySnapshot
            {
                SchemaVersion = candidate.SchemaVersion,
                StoryId = candidate.StoryId,
                CapturedUtc = candidate.CapturedUtc,
                SourcePath = candidate.SourcePath,
                Resource = Clone(candidate.Resource),
            };
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException)
        {
            return false;
        }
    }

    private static StoryResource Clone(StoryResource resource) => new()
    {
        SchemaVersion = resource.SchemaVersion,
        Id = resource.Id,
        DisplayName = resource.DisplayName,
        Description = resource.Description,
        Tags = resource.Tags is null ? null! : [.. resource.Tags],
        EntryPresentation = resource.EntryPresentation is null ? null! : new StoryEntryPresentation
        {
            Mode = resource.EntryPresentation.Mode,
            Eyebrow = resource.EntryPresentation.Eyebrow,
            Title = resource.EntryPresentation.Title,
            DurationSeconds = resource.EntryPresentation.DurationSeconds,
        },
        OwnedResources = Clone(resource.OwnedResources)!,
        ReferencedResources = Clone(resource.ReferencedResources)!,
        FlowRef = resource.FlowRef,
        Title = resource.Title,
        Entry = resource.Entry,
        Nodes = resource.Nodes is null ? null! : [.. resource.Nodes.Select(node => node is null ? null! : Clone(node))],
        Connections = resource.Connections is null ? null! : [.. resource.Connections.Select(connection => connection is null ? null! : Clone(connection))],
        Metadata = resource.Metadata is null ? null! : new StoryMetadata
        {
            Notes = resource.Metadata.Notes,
            Tags = resource.Metadata.Tags is null ? null! : [.. resource.Metadata.Tags],
        },
    };

    private static StoryMembership? Clone(StoryMembership? membership) => membership is null ? null : new StoryMembership
    {
        Actors = membership.Actors is null ? null! : [.. membership.Actors],
        Dialogues = membership.Dialogues is null ? null! : [.. membership.Dialogues],
        Quests = membership.Quests is null ? null! : [.. membership.Quests],
    };

    private static StoryNodeResource Clone(StoryNodeResource node) => new()
    {
        Id = node.Id,
        Type = node.Type,
        Position = node.Position is null ? null! : new StoryNodePosition { X = node.Position.X, Y = node.Position.Y },
        Properties = node.Properties is null
            ? null!
            : node.Properties.ToDictionary(pair => pair.Key, pair => pair.Value.Clone(), StringComparer.Ordinal),
    };

    private static StoryConnectionResource Clone(StoryConnectionResource connection) => new()
    {
        From = connection.From!,
        Output = connection.Output!,
        To = connection.To!,
    };
}

public sealed class StoryFlowRecoverySnapshot
{
    [JsonPropertyName("schema_version")]
    [JsonPropertyOrder(0)]
    public int SchemaVersion { get; init; }

    [JsonPropertyName("story_id")]
    [JsonPropertyOrder(1)]
    public string StoryId { get; init; } = string.Empty;

    [JsonPropertyName("captured_utc")]
    [JsonPropertyOrder(2)]
    public DateTimeOffset CapturedUtc { get; init; }

    [JsonPropertyName("source_path")]
    [JsonPropertyOrder(3)]
    public string? SourcePath { get; init; }

    [JsonPropertyName("resource")]
    [JsonPropertyOrder(4)]
    public StoryResource? Resource { get; init; }

    [JsonIgnore]
    public StoryResource? Story => Resource;
}
