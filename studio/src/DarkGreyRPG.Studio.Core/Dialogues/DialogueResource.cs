using System.Text.Json.Serialization;

namespace DarkGreyRPG.Studio.Core.Dialogues;

public sealed class DialogueResource
{
    public const int LegacySchemaVersion = 1;
    public const int CurrentSchemaVersion = 2;

    [JsonPropertyName("schema_version")]
    [JsonPropertyOrder(0)] public int SchemaVersion { get; init; } = CurrentSchemaVersion;
    [JsonPropertyName("id")]
    [JsonPropertyOrder(1)] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("title")]
    [JsonPropertyOrder(2)] public string Title { get; init; } = string.Empty;
    [JsonPropertyName("display_name")]
    [JsonPropertyOrder(3)] public string DisplayName { get; init; } = string.Empty;
    [JsonPropertyName("home_story_id")]
    [JsonPropertyOrder(4)] public string HomeStoryId { get; init; } = "uncategorized";
    [JsonPropertyName("speakers")]
    [JsonPropertyOrder(5)] public List<string> Speakers { get; init; } = [];
    [JsonPropertyName("entry")]
    [JsonPropertyOrder(6)] public string Entry { get; init; } = string.Empty;
    [JsonPropertyName("nodes")]
    [JsonPropertyOrder(7)] public List<DialogueNodeResource> Nodes { get; init; } = [];
    [JsonPropertyName("metadata")]
    [JsonPropertyOrder(8)] public DialogueMetadata Metadata { get; init; } = new();

    public DialogueResource WithId(string id) => new()
    {
        SchemaVersion = SchemaVersion, Id = id, Title = Title, DisplayName = DisplayName,
        HomeStoryId = HomeStoryId, Speakers = [.. Speakers], Entry = Entry,
        Nodes = Nodes.Select(n => n.Clone()).ToList(), Metadata = Metadata.Clone()
    };
}

public sealed class DialogueNodeResource
{
    [JsonPropertyName("id")] [JsonPropertyOrder(0)] public string Id { get; init; } = string.Empty;
    [JsonPropertyName("type")] [JsonPropertyOrder(1)] public string Type { get; init; } = string.Empty;
    [JsonPropertyName("speaker")] [JsonPropertyOrder(2)] public string? Speaker { get; init; }
    [JsonPropertyName("text")] [JsonPropertyOrder(3)] public string? Text { get; init; }
    [JsonPropertyName("next")] [JsonPropertyOrder(4)] public string? Next { get; init; }
    [JsonPropertyName("prompt")] [JsonPropertyOrder(5)] public string? Prompt { get; init; }
    [JsonPropertyName("choices")] [JsonPropertyOrder(6)] public List<DialogueChoiceResource>? Choices { get; init; }
    [JsonPropertyName("target")] [JsonPropertyOrder(7)] public string? Target { get; init; }
    [JsonPropertyName("result")] [JsonPropertyOrder(8)] public string? Result { get; init; }

    public DialogueNodeResource Clone() => new()
    {
        Id = Id, Type = Type, Speaker = Speaker, Text = Text, Next = Next, Prompt = Prompt,
        Choices = Choices?.Select(c => c.Clone()).ToList(), Target = Target, Result = Result
    };
    public static DialogueNodeResource Line(string id, string speaker, string text, string? next = null) => new() { Id = id, Type = "line", Speaker = speaker, Text = text, Next = next };
    public static DialogueNodeResource Choice(string id, string prompt, IEnumerable<DialogueChoiceResource> choices) => new() { Id = id, Type = "choice", Prompt = prompt, Choices = choices.Select(c => c.Clone()).ToList() };
    public static DialogueNodeResource Jump(string id, string target) => new() { Id = id, Type = "jump", Target = target };
    public static DialogueNodeResource End(string id, string result) => new() { Id = id, Type = "end", Result = result };
}

public sealed class DialogueChoiceResource
{
    [JsonPropertyName("text")] [JsonPropertyOrder(0)] public string Text { get; init; } = string.Empty;
    [JsonPropertyName("next")] [JsonPropertyOrder(1)] public string Next { get; init; } = string.Empty;
    public DialogueChoiceResource Clone() => new() { Text = Text, Next = Next };
}

public sealed class DialogueMetadata
{
    [JsonPropertyName("notes")] public string Notes { get; init; } = string.Empty;
    [JsonPropertyName("tags")] public List<string> Tags { get; init; } = [];
    public DialogueMetadata Clone() => new() { Notes = Notes, Tags = [.. Tags] };
}

public sealed record DialogueResourceInfo(string Id, string DisplayName, string Path, IReadOnlyList<string> Speakers);
public class DialogueException : Exception { public DialogueException(string message, Exception? inner = null) : base(message, inner) { } }
public sealed class DialogueDataException : DialogueException { public DialogueDataException(string message, Exception? inner = null) : base(message, inner) { } }
public class DialogueRepositoryException : DialogueException { public DialogueRepositoryException(string message, Exception? inner = null) : base(message, inner) { } }
public sealed class DialogueNotFoundException : DialogueRepositoryException { public DialogueNotFoundException(string id) : base($"Dialogue '{id}' was not found.") { } }
public sealed class DialogueCollisionException : DialogueRepositoryException { public DialogueCollisionException(string id) : base($"Dialogue '{id}' already exists.") { } }
public sealed class DialogueValidationException : DialogueException
{
    public DialogueValidationException(IEnumerable<DarkGreyRPG.Studio.Core.Validation.ValidationIssue> issues) : base(string.Join(" ", issues.Select(i => i.Message))) => Issues = issues.ToArray();
    public IReadOnlyList<DarkGreyRPG.Studio.Core.Validation.ValidationIssue> Issues { get; }
}
