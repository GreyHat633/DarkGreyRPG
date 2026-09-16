using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;

namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

/// <summary>The persisted kind of a canonical graph resource.</summary>
[JsonConverter(typeof(GraphResourceKindJsonConverter))]
public enum GraphResourceKind
{
    Story,
    Session,
    Task,
}

/// <summary>
/// Canonical graph envelope with an optional, Task-only author metadata extension.
/// </summary>
public sealed class GraphResourceEnvelope
{
    public const int CurrentSchemaVersion = 1;

    private GraphDocument? _graph;

    public CanonicalTaskMetadata? TaskMetadata { get; set; }

    public GraphResourceEnvelope() { }

    public GraphResourceEnvelope(GraphResourceKind resourceKind, string id, string displayName, GraphDocument graph)
    {
        ResourceKind = resourceKind;
        Id = id;
        DisplayName = displayName;
        Graph = graph;
    }

    public GraphResourceEnvelope(string resourceKind, string id, string displayName, GraphDocument graph)
    {
        ResourceKind = GraphResourceEnvelopeSerializer.ParseResourceKind(resourceKind);
        Id = id;
        DisplayName = displayName;
        Graph = graph;
    }

    [JsonPropertyName("schema_version")]
    [JsonPropertyOrder(0)]
    public int SchemaVersion { get; set; } = CurrentSchemaVersion;

    [JsonPropertyName("resource_kind")]
    [JsonPropertyOrder(1)]
    public GraphResourceKind ResourceKind { get; set; }

    [JsonPropertyName("id")]
    [JsonPropertyOrder(2)]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("display_name")]
    [JsonPropertyOrder(3)]
    public string DisplayName { get; set; } = string.Empty;

    [JsonPropertyName("tags")]
    public IReadOnlyList<string> Tags { get; set; } = [];

    /// <summary>
    /// Gets or sets a detached graph snapshot.  The boundary owns its own
    /// nodes, ports, connections, and JSON property values.
    /// </summary>
    [JsonPropertyName("graph")]
    [JsonPropertyOrder(4)]
    public GraphDocument? Graph
    {
        get => _graph is null ? null : GraphResourceEnvelopeSerializer.CloneGraph(_graph);
        set => _graph = value is null ? null : GraphResourceEnvelopeSerializer.CloneGraph(value);
    }

    public string ToJson(bool indented = true) => GraphResourceEnvelopeSerializer.Serialize(this, indented);

    public static GraphResourceEnvelope FromJson(string json)
        => GraphResourceEnvelopeSerializer.Deserialize(json);

    internal GraphDocument? SnapshotGraph() => _graph is null ? null : GraphResourceEnvelopeSerializer.CloneGraph(_graph);
}

/// <summary>Stable diagnostics for strict canonical envelope failures.</summary>
public sealed class GraphResourceEnvelopeException : Exception
{
    public GraphResourceEnvelopeException(string code, string message, Exception? innerException = null)
        : base($"{code}: {message}", innerException)
    {
        Code = code;
    }

    public string Code { get; }
}

/// <summary>JSON converter preserving the frozen lowercase resource-kind values.</summary>
public sealed class GraphResourceKindJsonConverter : JsonConverter<GraphResourceKind>
{
    public override GraphResourceKind Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => GraphResourceEnvelopeSerializer.ParseResourceKind(reader.GetString());

    public override void Write(Utf8JsonWriter writer, GraphResourceKind value, JsonSerializerOptions options)
        => writer.WriteStringValue(GraphResourceEnvelopeSerializer.FormatResourceKind(value));
}

/// <summary>Strict schema-version-1 serializer for <see cref="GraphResourceEnvelope"/>.</summary>
public static class GraphResourceEnvelopeSerializer
{
    private static readonly HashSet<string> RootMembers =
        ["schema_version", "resource_kind", "id", "display_name", "tags", "task_metadata", "graph"];

    public static string Serialize(GraphResourceEnvelope envelope, bool indented = true)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        Validate(envelope);

        // Write the root explicitly to make the frozen member order apparent
        // and independent of reflection/property declaration behavior.
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream, new JsonWriterOptions { Indented = indented, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schema_version", GraphResourceEnvelope.CurrentSchemaVersion);
            writer.WriteString("resource_kind", FormatResourceKind(envelope.ResourceKind));
            writer.WriteString("id", envelope.Id);
            writer.WriteString("display_name", envelope.DisplayName);
            if (envelope.Tags.Count > 0)
            {
                writer.WritePropertyName("tags");
                JsonSerializer.Serialize(writer, envelope.Tags);
            }
            if (envelope.TaskMetadata is { } metadata)
            {
                writer.WritePropertyName("task_metadata");
                writer.WriteStartObject();
                writer.WriteString("description", metadata.Description);
                writer.WriteEndObject();
            }
            writer.WritePropertyName("graph");
            var graph = envelope.SnapshotGraph()!;
            if (envelope.ResourceKind == GraphResourceKind.Story) LegacyStoryBoundaryUpgrade.Apply(graph);
            if (envelope.ResourceKind == GraphResourceKind.Session)
                foreach (var node in graph.Nodes) Definitions.CanonicalSessionLineSchema.Normalize(node);
            JsonSerializer.Serialize(writer, graph, GraphSerializer.Options);
            writer.WriteEndObject();
        }

        return NormalizeLineEnding(System.Text.Encoding.UTF8.GetString(stream.ToArray())) + "\n";
    }

    public static GraphResourceEnvelope Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json))
            throw Failure("graph.resource.json.required", "Canonical graph resource JSON is required.");
        try
        {
            using var document = JsonDocument.Parse(json, new JsonDocumentOptions
            {
                AllowTrailingCommas = false,
                CommentHandling = JsonCommentHandling.Disallow,
            });
            var root = document.RootElement;
            if (root.ValueKind != JsonValueKind.Object)
                throw Failure("graph.resource.root.invalid", "Canonical graph resource root must be an object.");

            EnsureRootMembers(root);
            var version = Required(root, "schema_version").GetInt32();
            if (version != GraphResourceEnvelope.CurrentSchemaVersion)
                throw Failure("graph.resource.schema_version.unsupported", $"Unsupported canonical graph resource schema_version {version}; expected {GraphResourceEnvelope.CurrentSchemaVersion}.");

            var kindValue = RequiredString(root, "resource_kind");
            var kind = ParseResourceKind(kindValue);
            var id = RequiredString(root, "id");
            var displayName = RequiredString(root, "display_name");
            if (string.IsNullOrWhiteSpace(id))
                throw Failure("graph.resource.id.required", "Canonical graph resource id cannot be blank.");
            if (string.IsNullOrWhiteSpace(displayName))
                throw Failure("graph.resource.display_name.required", "Canonical graph resource display_name cannot be blank.");

            var graphElement = Required(root, "graph");
            if (graphElement.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
                throw Failure("graph.resource.graph.required", "Canonical graph resource graph cannot be null.");
            if (graphElement.ValueKind != JsonValueKind.Object)
                throw Failure("graph.resource.graph.invalid", "Canonical graph resource graph must be an object.");

            var graph = JsonSerializer.Deserialize<GraphDocument>(graphElement.GetRawText(), GraphSerializer.Options)
                ?? throw Failure("graph.resource.graph.required", "Canonical graph resource graph cannot be null.");
            RejectRetiredStandaloneNodes(kind, graph);
            if (kind == GraphResourceKind.Story) LegacyStoryBoundaryUpgrade.Apply(graph);
            if (kind == GraphResourceKind.Session)
                foreach (var node in graph.Nodes) Definitions.CanonicalSessionLineSchema.Normalize(node);
            var tags = root.TryGetProperty("tags", out var tagsValue)
                ? tagsValue.EnumerateArray().Select(tag => tag.ValueKind == JsonValueKind.String ? tag.GetString()! : throw new JsonException("Tags must be strings.")).ToArray() : [];
            return new GraphResourceEnvelope(kind, id, displayName, graph) { SchemaVersion = version, Tags = tags, TaskMetadata = CanonicalTaskMetadata.Read(root, kind) };
        }
        catch (GraphResourceEnvelopeException)
        {
            throw;
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException or ArgumentException)
        {
            throw new GraphResourceEnvelopeException("graph.resource.invalid", "Canonical graph resource JSON is invalid or contains unsupported fields.", exception);
        }
    }

    public static GraphResourceKind ParseResourceKind(string? value)
        => value switch
        {
            "story" => GraphResourceKind.Story,
            "session" => GraphResourceKind.Session,
            "task" => GraphResourceKind.Task,
            _ => throw Failure("graph.resource.kind.unsupported", $"Unsupported resource_kind '{value ?? "null"}'; expected 'story', 'session', or 'task'."),
        };

    public static string FormatResourceKind(GraphResourceKind value)
        => value switch
        {
            GraphResourceKind.Story => "story",
            GraphResourceKind.Session => "session",
            GraphResourceKind.Task => "task",
            _ => throw Failure("graph.resource.kind.unsupported", $"Unsupported resource kind value '{value}'."),
        };

    internal static GraphDocument CloneGraph(GraphDocument graph)
    {
        ArgumentNullException.ThrowIfNull(graph);
        return new GraphDocument(
            (graph.Nodes ?? throw Failure("graph.resource.graph.nodes.required", "Canonical graph nodes cannot be null."))
                .Select((node, index) => node is null
                    ? throw Failure("graph.resource.graph.node.required", $"Canonical graph node at index {index} cannot be null.")
                    : CloneNode(node)),
            (graph.Connections ?? throw Failure("graph.resource.graph.connections.required", "Canonical graph connections cannot be null."))
                .Select((connection, index) => connection is null
                    ? throw Failure("graph.resource.graph.connection.required", $"Canonical graph connection at index {index} cannot be null.")
                    : CloneConnection(connection)));
    }

    private static GraphNode CloneNode(GraphNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        return new GraphNode(node.Id, node.Type, node.DisplayName,
            (node.Ports ?? throw Failure("graph.resource.graph.ports.required", "Canonical graph node ports cannot be null."))
                .Select((port, index) => port is null
                    ? throw Failure("graph.resource.graph.port.required", $"Canonical graph port at index {index} on node '{node.Id}' cannot be null.")
                    : ClonePort(port)),
            node.Properties);
    }

    private static GraphPort ClonePort(GraphPort port)
    {
        ArgumentNullException.ThrowIfNull(port);
        return new GraphPort(port.Id, port.DisplayName, port.IsInput, port.InterfaceKind, port.Order);
    }

    private static GraphConnection CloneConnection(GraphConnection connection)
    {
        ArgumentNullException.ThrowIfNull(connection);
        return new GraphConnection(connection.FromNodeId, connection.FromPortId, connection.ToNodeId, connection.ToPortId, connection.InterfaceKind);
    }

    internal static void Validate(GraphResourceEnvelope envelope)
    {
        if (envelope.SchemaVersion != GraphResourceEnvelope.CurrentSchemaVersion)
            throw Failure("graph.resource.schema_version.unsupported", $"Unsupported canonical graph resource schema_version {envelope.SchemaVersion}; expected {GraphResourceEnvelope.CurrentSchemaVersion}.");
        _ = FormatResourceKind(envelope.ResourceKind);
        if (string.IsNullOrWhiteSpace(envelope.Id))
            throw Failure("graph.resource.id.required", "Canonical graph resource id cannot be blank.");
        if (string.IsNullOrWhiteSpace(envelope.DisplayName))
            throw Failure("graph.resource.display_name.required", "Canonical graph resource display_name cannot be blank.");
        if (envelope.Tags is null || envelope.Tags.Any(tag => tag is null))
            throw Failure("graph.resource.tags.invalid", "Resource tags must be a string array.");
        if (envelope.SnapshotGraph() is null)
            throw Failure("graph.resource.graph.required", "Canonical graph resource graph cannot be null.");
        if (envelope.TaskMetadata is not null && envelope.ResourceKind != GraphResourceKind.Task)
            throw Failure("graph.resource.task_metadata.scope", "任务说明只能用于 Task 资源。");
        RejectRetiredStandaloneNodes(envelope.ResourceKind, envelope.SnapshotGraph()!);
    }

    private static void RejectRetiredStandaloneNodes(GraphResourceKind kind, GraphDocument graph)
    {
        if (kind != GraphResourceKind.Story) return;
        var retired = graph.Nodes?.FirstOrDefault(node => node is not null
            && node.Type is "interact_actor" or "enter_region" or "enter_story");
        if (retired is not null)
            throw Failure("graph.resource.story.standalone_node.removed",
                $"旧独立触发器节点 '{retired.Type}' 已删除，请先手工转换节点 '{retired.Id}'；开始配置不受影响。");
    }

    private static void EnsureRootMembers(JsonElement root)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var property in root.EnumerateObject())
        {
            if (!RootMembers.Contains(property.Name))
                throw Failure("graph.resource.root.member.unsupported", $"Unsupported canonical graph resource root field '{property.Name}'.");
            if (!seen.Add(property.Name))
                throw Failure("graph.resource.root.member.duplicate", $"Duplicate canonical graph resource root field '{property.Name}'.");
        }

        foreach (var member in RootMembers)
            if (member is not ("tags" or "task_metadata") && !seen.Contains(member))
                throw Failure("graph.resource.root.member.required", $"Canonical graph resource root field '{member}' is required.");
    }

    private static JsonElement Required(JsonElement root, string name)
        => root.TryGetProperty(name, out var value)
            ? value
            : throw Failure("graph.resource.root.member.required", $"Canonical graph resource root field '{name}' is required.");

    private static string RequiredString(JsonElement root, string name)
    {
        var value = Required(root, name);
        if (value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            throw Failure("graph.resource.root.member.null", $"Canonical graph resource root field '{name}' cannot be null.");
        if (value.ValueKind != JsonValueKind.String)
            throw Failure("graph.resource.root.member.type", $"Canonical graph resource root field '{name}' must be a string.");
        return value.GetString() ?? string.Empty;
    }

    private static GraphResourceEnvelopeException Failure(string code, string message)
        => new(code, message);

    private static string NormalizeLineEnding(string value)
        => value.Replace("\r\n", "\n", StringComparison.Ordinal);
}

/// <summary>Compatibility-free aliases for callers that name the boundary serializer generically.</summary>
public static class GraphResourceSerializer
{
    public static string Serialize(GraphResourceEnvelope envelope, bool indented = true)
        => GraphResourceEnvelopeSerializer.Serialize(envelope, indented);

    public static GraphResourceEnvelope Deserialize(string json)
        => GraphResourceEnvelopeSerializer.Deserialize(json);
}

/// <summary>Maps envelope kinds to the one and only graph scope they may open.</summary>
public static class GraphResourceScopeAdapter
{
    public static GraphScope GetScope(GraphResourceKind kind)
        => kind switch
        {
            GraphResourceKind.Story => GraphScope.StoryFlow,
            GraphResourceKind.Session => GraphScope.Session,
            GraphResourceKind.Task => GraphScope.Task,
            _ => throw new GraphResourceEnvelopeException("graph.resource.kind.unsupported", $"Unsupported resource kind value '{kind}'."),
        };

    public static GraphScope GetScope(string resourceKind)
        => GetScope(GraphResourceEnvelopeSerializer.ParseResourceKind(resourceKind));

    public static bool TryGetScope(GraphResourceKind kind, out GraphScope scope)
    {
        if (kind is GraphResourceKind.Story or GraphResourceKind.Session or GraphResourceKind.Task)
        {
            scope = GetScope(kind);
            return true;
        }

        scope = default;
        return false;
    }

    public static GraphDocument Open(GraphResourceEnvelope envelope, GraphScope expectedScope)
    {
        ArgumentNullException.ThrowIfNull(envelope);
        GraphResourceEnvelopeSerializer.Validate(envelope);
        var actualScope = GetScope(envelope.ResourceKind);
        if (actualScope != expectedScope)
            throw new GraphResourceEnvelopeException("graph.resource.scope.mismatch", $"Resource kind '{GraphResourceEnvelopeSerializer.FormatResourceKind(envelope.ResourceKind)}' maps to scope '{actualScope}', not expected scope '{expectedScope}'.");
        return envelope.SnapshotGraph()
            ?? throw new GraphResourceEnvelopeException("graph.resource.graph.required", "Canonical graph resource graph cannot be null.");
    }

    public static GraphDocument Adapt(GraphResourceEnvelope envelope, GraphScope expectedScope)
        => Open(envelope, expectedScope);

    public static GraphDocument Open(GraphResourceEnvelope envelope, GraphResourceKind expectedKind)
        => Open(envelope, GetScope(expectedKind));

    public static GraphDocument Open(GraphResourceEnvelope envelope, string expectedResourceKind)
        => Open(envelope, GetScope(expectedResourceKind));

    public static GraphResourceDocument OpenDocument(GraphResourceEnvelope envelope, GraphScope expectedScope)
        => new(envelope, expectedScope, Open(envelope, expectedScope));
}

/// <summary>A detached, UI-independent opened document at a verified scope.</summary>
public sealed class GraphResourceDocument
{
    private GraphDocument _graph;

    internal GraphResourceDocument(GraphResourceEnvelope envelope, GraphScope scope, GraphDocument graph)
    {
        ResourceKind = envelope.ResourceKind;
        Scope = scope;
        Id = envelope.Id;
        DisplayName = envelope.DisplayName;
        Tags = envelope.Tags.ToArray();
        TaskMetadata = envelope.TaskMetadata;
        _graph = GraphResourceEnvelopeSerializer.CloneGraph(graph);
    }

    public GraphResourceKind ResourceKind { get; }
    public GraphScope Scope { get; }
    public string Id { get; }
    public string DisplayName { get; private set; }
    public IReadOnlyList<string> Tags { get; private set; } = [];
    public void SetTags(IEnumerable<string> tags) => Tags = tags.ToArray();
    public CanonicalTaskMetadata? TaskMetadata { get; private set; }
    public void SetTaskMetadata(CanonicalTaskMetadata? metadata)
    {
        if (ResourceKind != GraphResourceKind.Task && metadata is not null)
            throw new ArgumentException("任务说明只能用于 Task 资源。");
        TaskMetadata = metadata;
    }
    /// <summary>
    /// The document-owned mutable graph used by an editor host. It is detached
    /// from the source envelope when the document is opened.
    /// </summary>
    public GraphDocument Graph => _graph;

    /// <summary>Replaces the editable graph with a detached caller snapshot.</summary>
    public void ReplaceGraph(GraphDocument graph)
        => _graph = GraphResourceEnvelopeSerializer.CloneGraph(
            graph ?? throw new ArgumentNullException(nameof(graph)));

    public void SetDisplayName(string displayName)
    {
        if (string.IsNullOrWhiteSpace(displayName))
            throw new ArgumentException("Display name is required.", nameof(displayName));
        DisplayName = displayName.Trim();
    }

    /// <summary>Creates a detached persistence snapshot of the current edit state.</summary>
    public GraphResourceEnvelope ToEnvelope()
        => new(ResourceKind, Id, DisplayName, _graph) { Tags = Tags.ToArray(), TaskMetadata = TaskMetadata };

    public GraphResourceEnvelope CreateSnapshot() => ToEnvelope();
}
