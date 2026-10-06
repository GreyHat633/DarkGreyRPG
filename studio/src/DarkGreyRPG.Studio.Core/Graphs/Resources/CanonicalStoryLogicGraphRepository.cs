using System.Text.Json;
using System.Text.Json.Serialization;
using DarkGreyRPG.Studio.Core.IO;
using DarkGreyRPG.Studio.Core.Packaging;
using DarkGreyRPG.Studio.Core.Identity;

namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

public sealed record CanonicalStoryLogicConnection(
    [property: JsonPropertyName("source_story_id")] string SourceStoryId,
    [property: JsonPropertyName("source_port_id")] string SourcePortId,
    [property: JsonPropertyName("target_story_id")] string TargetStoryId,
    [property: JsonPropertyName("target_port_id")] string TargetPortId,
    [property: JsonPropertyName("interface_kind")] string InterfaceKind = "Logic");

public sealed record CanonicalStoryLogicGraph(
    [property: JsonPropertyName("schema_version")] int SchemaVersion,
    [property: JsonPropertyName("connections")] IReadOnlyList<CanonicalStoryLogicConnection> Connections)
{
    public static CanonicalStoryLogicGraph Empty { get; } = new(2, []);
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

    public CanonicalStoryLogicGraph Save(IEnumerable<CanonicalStoryLogicConnection> connections,
        IReadOnlyList<ProjectFileChange>? editorChanges = null)
    {
        ArgumentNullException.ThrowIfNull(connections);
        var graph = new CanonicalStoryLogicGraph(2, connections
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
                if (editorChanges is not null)
                {
                    var project = System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(Path)))!;
                    var changes = editorChanges.Append(new ProjectFileChange(System.IO.Path.GetRelativePath(project, Path),
                        File.Exists(Path) ? File.ReadAllBytes(Path) : null,
                        System.Text.Encoding.UTF8.GetBytes(JsonSerializer.Serialize(graph, JsonOptions)))).ToArray();
                    new ProjectFileTransaction().Apply(project, changes, () => Validate(graph));
                }
                else
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

    public void Validate(CanonicalStoryLogicGraph graph, GraphResourceEnvelope? replacement = null)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (graph.SchemaVersion != 2)
            throw Failure("story.logic_graph.schema_version", "Story connection schema_version must be 2.");
        var stories = _stories.List().Select(info => _stories.Load(info.Id))
            .ToDictionary(story => story.Id, StringComparer.Ordinal);
        var projectDirectory = System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(Path)))!;
        var providers = OfflineProviderCatalog.Load(projectDirectory);
        if (providers.Diagnostics.Count != 0) throw Failure("story.graph.provider.invalid", string.Join("; ", providers.Diagnostics.Select(issue => issue.Message)));
        foreach (var group in providers.Find(DgrResourceKind.Story).GroupBy(resource => resource.Id))
        {
            if (group.Count() != 1 || stories.ContainsKey(group.Key))
                throw Failure("story.graph.story.ambiguous", $"故事身份 '{group.Key}' 在本地或引用包中重复。");
            stories[group.Key] = group.Single().ReadGraphDefinition()!;
        }
        if (replacement is not null) stories[replacement.Id] = replacement;
        // Validate native records before deduplicating provider projections.
        ValidateDetached(graph, stories);
        ValidateDetached(new CanonicalStoryLogicGraph(2, graph.Connections
            .Concat(providers.Providers.SelectMany(provider => provider.ContainerConnections.Connections)).Distinct().ToArray()), stories);
    }

    public static void ValidateDetached(CanonicalStoryLogicGraph graph, IReadOnlyDictionary<string, GraphResourceEnvelope> stories)
    {
        ArgumentNullException.ThrowIfNull(graph);
        if (graph.SchemaVersion != 2)
            throw Failure("story.logic_graph.schema_version", "Story connection schema_version must be 2.");
        var targets = new HashSet<(string StoryId, string PortId)>();
        var flowSources = new HashSet<(string StoryId, string PortId)>();
        var exact = new HashSet<CanonicalStoryLogicConnection>();
        foreach (var connection in graph.Connections)
        {
            if (connection is null || !StoryUid.IsValid(connection.SourceStoryId) || Blank(connection.SourcePortId)
                || !StoryUid.IsValid(connection.TargetStoryId) || Blank(connection.TargetPortId))
                throw Failure("story.logic_graph.connection.invalid", "Story Logic connection fields must be nonblank.");
            if (!exact.Add(connection))
                throw Failure("story.logic_graph.connection.duplicate", "Story Logic connection is duplicated.");
            if (connection.InterfaceKind is not ("Flow" or "Logic"))
                throw Failure("story.logic_graph.kind", "连接类型必须为 Flow 或 Logic。");
            RequireBoundary(stories, connection.SourceStoryId, connection.SourcePortId, connection.InterfaceKind == "Flow" ? "terminate" : "logic_output", "source");
            RequireBoundary(stories, connection.TargetStoryId, connection.TargetPortId, connection.InterfaceKind == "Flow" ? "flow_driven" : "logic_input", "target");
            if (connection.InterfaceKind == "Flow" && !flowSources.Add((connection.SourceStoryId, connection.SourcePortId)))
                throw Failure("story.logic_graph.source.multiple_targets", "Flow 输出最多连接一个目标。");
            if (connection.InterfaceKind == "Logic" && !targets.Add((connection.TargetStoryId, connection.TargetPortId)))
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
                && !HasBoundary(candidate, connection.SourcePortId, connection.InterfaceKind == "Flow" ? "terminate" : "logic_output"))
                throw Failure("story.logic_graph.port.referenced",
                    $"无法删除仍被 Story Graph 引用的逻辑输出 '{candidate.Id}.{connection.SourcePortId}'。");
            if (string.Equals(connection.TargetStoryId, candidate.Id, StringComparison.Ordinal)
                && !HasBoundary(candidate, connection.TargetPortId, connection.InterfaceKind == "Flow" ? "flow_driven" : "logic_input"))
                throw Failure("story.logic_graph.port.referenced",
                    $"无法删除仍被 Story Graph 引用的逻辑输入 '{candidate.Id}.{connection.TargetPortId}'。");
        }
    }

    public static CanonicalStoryLogicGraph Parse(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object || !ExactKeys(root, "schema_version", "connections"))
            throw Failure("story.logic_graph.schema", "Story Logic graph requires exactly schema_version and connections.");
        if (!root.GetProperty("schema_version").TryGetInt32(out var version)
            || version != 2
            || root.GetProperty("connections").ValueKind != JsonValueKind.Array)
            throw Failure("story.logic_graph.schema", "Story Logic graph root fields have invalid types.");
        var connections = new List<CanonicalStoryLogicConnection>();
        foreach (var item in root.GetProperty("connections").EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object
                || !ExactKeys(item, "source_story_id", "source_port_id", "target_story_id", "target_port_id", "interface_kind"))
                throw Failure("story.logic_graph.connection.schema", "Story Logic connection fields are invalid.");
            connections.Add(new(
                ReadString(item, "source_story_id"), ReadString(item, "source_port_id"),
                ReadString(item, "target_story_id"), ReadString(item, "target_port_id"), ReadString(item, "interface_kind")));
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

    public static bool HasBoundary(GraphResourceEnvelope story, string portId, string nodeType)
        => CanonicalStoryBoundaryProjection.Ports(story).Count(port => port.Id == portId
            && port.IsInput == (nodeType is "flow_driven" or "logic_input")
            && port.InterfaceKind == (nodeType is "flow_driven" or "terminate" ? GraphInterfaceKind.Flow : GraphInterfaceKind.Logic)) == 1;

    private static bool ExactKeys(JsonElement element, params string[] keys)
    {
        var actual = element.EnumerateObject().Select(property => property.Name).ToHashSet(StringComparer.Ordinal);
        return element.EnumerateObject().Count() == keys.Length && actual.SetEquals(keys);
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
