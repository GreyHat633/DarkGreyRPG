using System.Text.Json;
using System.Text.Json.Serialization;
using DarkGreyRPG.Studio.Core.IO;
using DarkGreyRPG.Studio.Core.Stories;

namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

/// <summary>
/// Persists Studio-only node positions for the canonical Story, Session, and
/// Task graphs. This sidecar is deliberately separate from Runtime resources.
/// </summary>
public sealed class CanonicalGraphLayoutStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
    };

    public CanonicalGraphLayoutStore(string projectDirectory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(projectDirectory);
        ProjectDirectory = Path.GetFullPath(projectDirectory);
        LayoutPath = Path.Combine(ProjectDirectory, "resources", "editor", "studio_layout.json");
    }

    public string ProjectDirectory { get; }

    public string LayoutPath { get; }

    public IReadOnlyList<GraphCommentFrame> LoadFrames(string graphKey)
        => ReadDocument()?.Frames?.GetValueOrDefault(graphKey)?.Where(frame => frame is not null && frame.IsValid).ToArray() ?? [];

    public void SaveFrames(string graphKey, IEnumerable<GraphCommentFrame> frames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(graphKey);
        var document = ReadDocument() ?? new CanonicalGraphLayoutDocument();
        document.Frames ??= new(StringComparer.Ordinal);
        var members = new HashSet<string>(StringComparer.Ordinal);
        document.Frames[graphKey] = frames.Where(frame => frame.IsValid).DistinctBy(frame => frame.Id)
            .Select(frame => frame with { Members = frame.Members.Where(id => !string.IsNullOrWhiteSpace(id) && members.Add(id)).ToArray() }).ToList();
        new AtomicFileWriter().Write(LayoutPath, JsonSerializer.Serialize(document, JsonOptions));
    }

    /// <summary>Builds the stable sidecar key from kind and resource ID.</summary>
    public static string BuildGraphKey(GraphResourceKind resourceKind, string resourceId)
    {
        if (string.IsNullOrWhiteSpace(resourceId))
            throw new ArgumentException("Resource ID is required.", nameof(resourceId));

        return $"{GraphResourceEnvelopeSerializer.FormatResourceKind(resourceKind)}:{resourceId}";
    }

    /// <summary>
    /// Loads one graph's valid finite positions. Invalid or unreadable sidecar
    /// content fails closed to an empty map.
    /// </summary>
    public IReadOnlyDictionary<string, ProjectGraphNodeLayout> Load(
        GraphResourceKind resourceKind,
        string resourceId)
    {
        var graphKey = BuildGraphKey(resourceKind, resourceId);
        var document = ReadDocument();
        if (document is null || document.Graphs is null ||
            !document.Graphs.TryGetValue(graphKey, out var nodes) || nodes is null)
            return EmptyPositions();

        return FilterPositions(nodes);
    }

    /// <summary>
    /// Replaces one graph's node set while retaining all other valid graphs in
    /// the project sidecar. The write is staged and committed atomically.
    /// </summary>
    public void Save(
        GraphResourceKind resourceKind,
        string resourceId,
        IReadOnlyDictionary<string, ProjectGraphNodeLayout> positions)
    {
        ArgumentNullException.ThrowIfNull(positions);
        var graphKey = BuildGraphKey(resourceKind, resourceId);
        var document = ReadDocument() ?? new CanonicalGraphLayoutDocument();
        document.Graphs ??= new Dictionary<string, Dictionary<string, ProjectGraphNodeLayout>>(StringComparer.Ordinal);
        document.Graphs[graphKey] = FilterPositions(positions).ToDictionary(
            pair => pair.Key,
            pair => new ProjectGraphNodeLayout { X = pair.Value.X, Y = pair.Value.Y },
            StringComparer.Ordinal);

        var json = JsonSerializer.Serialize(document, JsonOptions);
        new AtomicFileWriter().Write(LayoutPath, json);
    }

    private CanonicalGraphLayoutDocument? ReadDocument()
    {
        if (!File.Exists(LayoutPath))
            return null;

        try
        {
            var document = JsonSerializer.Deserialize<CanonicalGraphLayoutDocument>(
                File.ReadAllText(LayoutPath), JsonOptions);
            if (document is null || document.SchemaVersion != CanonicalGraphLayoutDocument.CurrentSchemaVersion)
                return null;

            document.Graphs = FilterGraphs(document.Graphs);
            return document;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException or NotSupportedException or InvalidOperationException or FormatException or OverflowException)
        {
            return null;
        }
    }

    private static Dictionary<string, Dictionary<string, ProjectGraphNodeLayout>> FilterGraphs(
        Dictionary<string, Dictionary<string, ProjectGraphNodeLayout>>? graphs)
    {
        var valid = new Dictionary<string, Dictionary<string, ProjectGraphNodeLayout>>(StringComparer.Ordinal);
        if (graphs is null)
            return valid;

        foreach (var pair in graphs)
        {
            if (!TryParseGraphKey(pair.Key, out var kind, out var resourceId) || pair.Value is null)
                continue;

            var key = BuildGraphKey(kind, resourceId);
            valid[key] = FilterPositions(pair.Value).ToDictionary(
                node => node.Key,
                node => new ProjectGraphNodeLayout { X = node.Value.X, Y = node.Value.Y },
                StringComparer.Ordinal);
        }

        return valid;
    }

    private static Dictionary<string, ProjectGraphNodeLayout> FilterPositions(
        IEnumerable<KeyValuePair<string, ProjectGraphNodeLayout>> positions)
    {
        var valid = new Dictionary<string, ProjectGraphNodeLayout>(StringComparer.Ordinal);
        foreach (var pair in positions)
        {
            if (string.IsNullOrWhiteSpace(pair.Key) || pair.Value is null ||
                !double.IsFinite(pair.Value.X) || !double.IsFinite(pair.Value.Y))
                continue;

            valid[pair.Key] = new ProjectGraphNodeLayout { X = pair.Value.X, Y = pair.Value.Y };
        }

        return valid;
    }

    private static bool TryParseGraphKey(
        string? graphKey,
        out GraphResourceKind resourceKind,
        out string resourceId)
    {
        resourceKind = default;
        resourceId = string.Empty;
        if (string.IsNullOrWhiteSpace(graphKey))
            return false;

        var separator = graphKey.IndexOf(':');
        if (separator <= 0 || separator == graphKey.Length - 1)
            return false;

        try
        {
            resourceKind = GraphResourceEnvelopeSerializer.ParseResourceKind(graphKey[..separator]);
        }
        catch (GraphResourceEnvelopeException)
        {
            return false;
        }

        resourceId = graphKey[(separator + 1)..];
        return !string.IsNullOrWhiteSpace(resourceId);
    }

    private static IReadOnlyDictionary<string, ProjectGraphNodeLayout> EmptyPositions()
        => new Dictionary<string, ProjectGraphNodeLayout>(StringComparer.Ordinal);
}

/// <summary>Schema-versioned root of the Studio-only canonical graph sidecar.</summary>
public sealed class CanonicalGraphLayoutDocument
{
    public Dictionary<string, List<GraphCommentFrame>> Frames { get; set; } = new(StringComparer.Ordinal);
    public const int CurrentSchemaVersion = 1;

    [JsonPropertyName("schema_version")]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    [JsonPropertyName("graphs")]
    public Dictionary<string, Dictionary<string, ProjectGraphNodeLayout>> Graphs { get; set; } =
        new(StringComparer.Ordinal);
}
