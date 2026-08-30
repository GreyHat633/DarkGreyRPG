using System.Text.Json;
using System.Text.Json.Serialization;
using DarkGreyRPG.Studio.Core.IO;

namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

public sealed record CanonicalStoryLogicConnection(
    [property: JsonPropertyName("source_story_id")] string SourceStoryId,
    [property: JsonPropertyName("source_port_id")] string SourcePortId,
    [property: JsonPropertyName("target_story_id")] string TargetStoryId,
    [property: JsonPropertyName("target_port_id")] string TargetPortId);

public sealed record CanonicalStoryLogicGraph(
    [property: JsonPropertyName("schema_version")] int SchemaVersion,
    [property: JsonPropertyName("connections")] IReadOnlyList<CanonicalStoryLogicConnection> Connections)
{
    public static CanonicalStoryLogicGraph Empty { get; } = new(1, []);
}

public sealed class CanonicalStoryLogicGraphRepositoryException : Exception
{
    public CanonicalStoryLogicGraphRepositoryException(string code, string message, Exception? innerException = null)
        : base(message, innerException) => Code = code;

    public string Code { get; }
}

/// <summary>Atomic, strictly validated persistence for the project-level cross-Story Logic bridge.</summary>
public sealed class CanonicalStoryLogicGraphRepository
{
    public const string FileName = "story_logic_graph.json";
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    private readonly GraphResourceRepository _stories;
    private readonly IAtomicFileWriter _writer;
    private readonly object _writeGate = new();

    public CanonicalStoryLogicGraphRepository(string canonicalDirectory, GraphResourceRepository stories,
        IAtomicFileWriter? writer = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(canonicalDirectory);
        _stories = stories ?? throw new ArgumentNullException(nameof(stories));
        Path = System.IO.Path.Combine(System.IO.Path.GetFullPath(canonicalDirectory), FileName);
        _writer = writer ?? new AtomicFileWriter();
    }

    public string Path { get; }

    public CanonicalStoryLogicGraph Load()
    {
        if (!File.Exists(Path)) return CanonicalStoryLogicGraph.Empty;
        try
        {
            using var root = JsonDocument.Parse(File.ReadAllText(Path));
            var graph = Parse(root.RootElement);
            Validate(graph);
            return graph;
        }
        catch (CanonicalStoryLogicGraphRepositoryException) { throw; }
        catch (JsonException exception)
        {
            throw Failure("story.logic_graph.json.invalid", "Story Logic graph JSON is invalid.", exception);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            throw Failure("story.logic_graph.read.failed", "Could not read the Story Logic graph.", exception);
        }
    }

    public CanonicalStoryLogicGraph Save(IEnumerable<CanonicalStoryLogicConnection> connections)
    {
        ArgumentNullException.ThrowIfNull(connections);
        var graph = new CanonicalStoryLogicGraph(1, connections
            .OrderBy(item => item.SourceStoryId, StringComparer.Ordinal)
            .ThenBy(item => item.SourcePortId, StringComparer.Ordinal)
            .ThenBy(item => item.TargetStoryId, StringComparer.Ordinal)
            .ThenBy(item => item.TargetPortId, StringComparer.Ordinal)
            .ToArray());
        Validate(graph);
        lock (_writeGate)
        {
            try
            {
                Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
                _writer.Write(Path, JsonSerializer.Serialize(graph, JsonOptions), temporaryPath =>
                {
                    using var staged = JsonDocument.Parse(File.ReadAllText(temporaryPath));
                    Validate(Parse(staged.RootElement));
                });
            }
            catch (CanonicalStoryLogicGraphRepositoryException) { throw; }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or JsonException)
            {
                throw Failure("story.logic_graph.write.failed", "Could not save the Story Logic graph.", exception);
            }
        }
        return Load();
    }

    public void Validate(CanonicalStoryLogicGraph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (graph.SchemaVersion != 1)
            throw Failure("story.logic_graph.schema_version", "Story Logic graph schema_version must be 1.");
        var stories = _stories.List().Select(info => _stories.Load(info.Id))
            .ToDictionary(story => story.Id, StringComparer.Ordinal);
        var targets = new HashSet<(string StoryId, string PortId)>();
        var exact = new HashSet<CanonicalStoryLogicConnection>();
        foreach (var connection in graph.Connections)
        {
            if (connection is null || Blank(connection.SourceStoryId) || Blank(connection.SourcePortId)
                || Blank(connection.TargetStoryId) || Blank(connection.TargetPortId))
                throw Failure("story.logic_graph.connection.invalid", "Story Logic connection fields must be nonblank.");
            if (!exact.Add(connection))
                throw Failure("story.logic_graph.connection.duplicate", "Story Logic connection is duplicated.");
            RequireBoundary(stories, connection.SourceStoryId, connection.SourcePortId, "logic_output", "source");
            RequireBoundary(stories, connection.TargetStoryId, connection.TargetPortId, "logic_input", "target");
            if (!targets.Add((connection.TargetStoryId, connection.TargetPortId)))
                throw Failure("story.logic_graph.target.multiple_sources",
                    $"Story Logic input '{connection.TargetStoryId}.{connection.TargetPortId}' has multiple sources.");
        }
    }

    /// <summary>Rejects removal or ID mutation of a public Story port that is still connected.</summary>
    public void ValidateStoryReplacement(GraphResourceEnvelope candidate)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        if (candidate.ResourceKind != GraphResourceKind.Story)
            throw new ArgumentException("A Story replacement is required.", nameof(candidate));
        var graph = Load();
        foreach (var connection in graph.Connections)
        {
            if (string.Equals(connection.SourceStoryId, candidate.Id, StringComparison.Ordinal)
                && !HasBoundary(candidate, connection.SourcePortId, "logic_output"))
                throw Failure("story.logic_graph.port.referenced",
                    $"无法删除仍被 Story Graph 引用的逻辑输出 '{candidate.Id}.{connection.SourcePortId}'。");
            if (string.Equals(connection.TargetStoryId, candidate.Id, StringComparison.Ordinal)
                && !HasBoundary(candidate, connection.TargetPortId, "logic_input"))
                throw Failure("story.logic_graph.port.referenced",
                    $"无法删除仍被 Story Graph 引用的逻辑输入 '{candidate.Id}.{connection.TargetPortId}'。");
        }
    }

    private static CanonicalStoryLogicGraph Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !ExactKeys(root, "schema_version", "connections"))
            throw Failure("story.logic_graph.schema", "Story Logic graph requires exactly schema_version and connections.");
        if (!root.GetProperty("schema_version").TryGetInt32(out var version)
            || root.GetProperty("connections").ValueKind != JsonValueKind.Array)
            throw Failure("story.logic_graph.schema", "Story Logic graph root fields have invalid types.");
        var connections = new List<CanonicalStoryLogicConnection>();
        foreach (var item in root.GetProperty("connections").EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || !ExactKeys(item, "source_story_id", "source_port_id", "target_story_id", "target_port_id"))
                throw Failure("story.logic_graph.connection.schema", "Story Logic connection fields are invalid.");
            connections.Add(new(
                ReadString(item, "source_story_id"), ReadString(item, "source_port_id"),
                ReadString(item, "target_story_id"), ReadString(item, "target_port_id")));
        }
        return new(version, connections);
    }

    private static void RequireBoundary(IReadOnlyDictionary<string, GraphResourceEnvelope> stories,
        string storyId, string portId, string nodeType, string side)
    {
        if (!stories.TryGetValue(storyId, out var story))
            throw Failure("story.logic_graph.story.missing", $"Story Logic {side} Story '{storyId}' does not exist.");
        if (!HasBoundary(story, portId, nodeType))
            throw Failure("story.logic_graph.port.missing",
                $"Story Logic {side} port '{storyId}.{portId}' does not exist or is ambiguous.");
    }

    private static bool HasBoundary(GraphResourceEnvelope story, string portId, string nodeType)
        => (story.Graph?.Nodes ?? []).Count(node => node is not null && node.Type == nodeType
            && node.Properties.TryGetValue("port_id", out var value) && value.ValueKind == JsonValueKind.String
            && string.Equals(value.GetString(), portId, StringComparison.Ordinal)) == 1;

    private static bool ExactKeys(JsonElement element, params string[] keys)
    {
        var actual = element.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        return actual.SetEquals(keys);
    }

    private static string ReadString(JsonElement element, string name)
    {
        var value = element.GetProperty(name);
        if (value.ValueKind != JsonValueKind.String || Blank(value.GetString()))
            throw Failure("story.logic_graph.connection.value", $"Story Logic connection {name} must be nonblank.");
        return value.GetString()!;
    }

    private static bool Blank(string? value) => string.IsNullOrWhiteSpace(value);

    private static CanonicalStoryLogicGraphRepositoryException Failure(string code, string message,
        Exception? innerException = null) => new(code, message, innerException);
}
