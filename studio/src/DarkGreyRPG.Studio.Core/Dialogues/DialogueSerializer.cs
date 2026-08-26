using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Dialogues;

public static class DialogueSerializer
{
    private static readonly JsonSerializerOptions Options = new()
    {
        AllowTrailingCommas = false, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        PropertyNameCaseInsensitive = false, ReadCommentHandling = JsonCommentHandling.Disallow,
        UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow, WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };
    public static string Serialize(DialogueResource resource, ActorIdPolicy policy = ActorIdPolicy.NewResource)
    {
        var normalized = new DialogueResource { SchemaVersion = 2, Id = resource.Id, Title = resource.Title, DisplayName = string.IsNullOrWhiteSpace(resource.DisplayName) ? resource.Title : resource.DisplayName, HomeStoryId = string.IsNullOrWhiteSpace(resource.HomeStoryId) ? "uncategorized" : resource.HomeStoryId, Speakers = [.. resource.Speakers], Entry = resource.Entry, Nodes = resource.Nodes.Select(n => n.Clone()).ToList(), Metadata = resource.Metadata.Clone() };
        ThrowIfInvalid(DialogueValidator.Validate(normalized, policy));
        return JsonSerializer.Serialize(normalized, Options).Replace("\r\n", "\n", StringComparison.Ordinal) + "\n";
    }
    public static DialogueResource Deserialize(string json, string? sourcePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(json);
        try
        {
            using var root = JsonDocument.Parse(json, new JsonDocumentOptions { AllowTrailingCommas = false, CommentHandling = JsonCommentHandling.Disallow });
            if (root.RootElement.ValueKind != JsonValueKind.Object) throw new JsonException("Dialogue JSON root must be an object.");
            var resource = ParseRoot(root.RootElement);
            var issues = DialogueValidator.Validate(resource, ActorIdPolicy.ExistingResource).ToList();
            if (!string.IsNullOrWhiteSpace(sourcePath) && !string.Equals(Path.GetFileName(sourcePath), resource.Id + ".json", StringComparison.Ordinal)) issues.Add(new("dialogue.filename.mismatch", "Dialogue file name must match its ID.", "id"));
            ThrowIfInvalid(issues);
            return resource;
        }
        catch (DialogueValidationException) { throw; }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or InvalidOperationException or FormatException)
        { throw new DialogueDataException("Dialogue JSON is invalid or contains unsupported fields.", ex); }
    }
    public static DialogueResource Read(string path)
    { try { return Deserialize(File.ReadAllText(path), path); } catch (DialogueException) { throw; } catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { throw new DialogueDataException($"Could not read Dialogue file '{path}'.", ex); } }
    private static DialogueResource ParseRoot(JsonElement e)
    {
        var allowed = new HashSet<string>(StringComparer.Ordinal) { "schema_version", "id", "title", "display_name", "home_story_id", "speakers", "entry", "nodes", "metadata" };
        EnsureAllowed(e, allowed);
        var version = Required(e, "schema_version").GetInt32();
        if (version is not (1 or 2)) throw new JsonException("Unsupported Dialogue schema_version.");
        var title = Required(e, "title").GetString() ?? string.Empty;
        var metadata = ParseMetadata(e.TryGetProperty("metadata", out var m) ? m : throw new JsonException("metadata is required."));
        return new DialogueResource { SchemaVersion = version, Id = Required(e, "id").GetString() ?? string.Empty, Title = title, DisplayName = version == 1 ? title : Required(e, "display_name").GetString() ?? string.Empty, HomeStoryId = version == 1 ? "uncategorized" : Required(e, "home_story_id").GetString() ?? string.Empty, Speakers = ParseStrings(Required(e, "speakers"), "speakers"), Entry = Required(e, "entry").GetString() ?? string.Empty, Nodes = ParseNodes(Required(e, "nodes")), Metadata = metadata };
    }
    private static List<DialogueNodeResource> ParseNodes(JsonElement e)
    { if (e.ValueKind != JsonValueKind.Array) throw new JsonException("nodes must be an array."); var result = new List<DialogueNodeResource>(); foreach (var n in e.EnumerateArray()) { EnsureAllowed(n, ["id", "type", "speaker", "text", "next", "prompt", "choices", "target", "result"]); var node = new DialogueNodeResource { Id = Required(n, "id").GetString() ?? string.Empty, Type = Required(n, "type").GetString() ?? string.Empty, Speaker = OptionalString(n, "speaker"), Text = OptionalString(n, "text"), Next = OptionalString(n, "next"), Prompt = OptionalString(n, "prompt"), Target = OptionalString(n, "target"), Result = OptionalString(n, "result") }; if (n.TryGetProperty("choices", out var choices) && choices.ValueKind != JsonValueKind.Null) { if (choices.ValueKind != JsonValueKind.Array) throw new JsonException("choices must be an array."); node = new DialogueNodeResource { Id = node.Id, Type = node.Type, Speaker = node.Speaker, Text = node.Text, Next = node.Next, Prompt = node.Prompt, Target = node.Target, Result = node.Result, Choices = choices.EnumerateArray().Select(c => { EnsureAllowed(c, ["text", "next"]); return new DialogueChoiceResource { Text = Required(c, "text").GetString() ?? string.Empty, Next = Required(c, "next").GetString() ?? string.Empty }; }).ToList() }; } result.Add(node); } return result; }
    private static DialogueMetadata ParseMetadata(JsonElement e) { if (e.ValueKind != JsonValueKind.Object) throw new JsonException("metadata must be an object."); EnsureAllowed(e, ["notes", "tags"]); return new DialogueMetadata { Notes = e.TryGetProperty("notes", out var n) ? n.GetString() ?? string.Empty : string.Empty, Tags = e.TryGetProperty("tags", out var t) ? ParseStrings(t, "metadata.tags") : [] }; }
    private static List<string> ParseStrings(JsonElement e, string name) { if (e.ValueKind != JsonValueKind.Array) throw new JsonException($"{name} must be an array."); return e.EnumerateArray().Select(x => x.ValueKind == JsonValueKind.String ? x.GetString() ?? string.Empty : throw new JsonException($"{name} must contain strings.")).ToList(); }
    private static JsonElement Required(JsonElement e, string name) => e.TryGetProperty(name, out var value) ? value : throw new JsonException($"{name} is required.");
    private static string? OptionalString(JsonElement e, string name) => e.TryGetProperty(name, out var value) ? value.ValueKind == JsonValueKind.Null ? null : value.GetString() : null;
    private static void EnsureAllowed(JsonElement e, IEnumerable<string> names) { var allowed = names.ToHashSet(StringComparer.Ordinal); foreach (var p in e.EnumerateObject()) if (!allowed.Contains(p.Name)) throw new JsonException($"Unsupported Dialogue field '{p.Name}'."); }
    private static void ThrowIfInvalid(IEnumerable<ValidationIssue> issues) { var all = issues.ToArray(); if (all.Any(i => i.Severity == ValidationSeverity.Error)) throw new DialogueValidationException(all); }
}
