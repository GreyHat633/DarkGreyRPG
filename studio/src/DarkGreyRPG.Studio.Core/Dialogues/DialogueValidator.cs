using System.Text.RegularExpressions;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Dialogues;

public static partial class DialogueValidator
{
    public static IReadOnlyList<ValidationIssue> Validate(DialogueResource resource, ActorIdPolicy policy = ActorIdPolicy.ExistingResource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        var issues = new List<ValidationIssue>();
        if (resource.SchemaVersion is not (1 or 2)) issues.Add(new("dialogue.schema.unsupported", "Dialogue schema_version must be 1 or 2.", nameof(resource.SchemaVersion)));
        ValidateId(resource.Id, policy, issues, "dialogue");
        if (string.IsNullOrWhiteSpace(resource.Title)) issues.Add(new("dialogue.title.required", "Dialogue title is required.", nameof(resource.Title)));
        if (resource.SchemaVersion >= 2 && string.IsNullOrWhiteSpace(resource.DisplayName)) issues.Add(new("dialogue.display_name.required", "Dialogue display_name is required.", nameof(resource.DisplayName)));
        if (resource.SchemaVersion >= 2 && string.IsNullOrWhiteSpace(resource.HomeStoryId)) issues.Add(new("dialogue.home_story_id.required", "Dialogue home_story_id is required.", nameof(resource.HomeStoryId)));
        if (resource.Speakers is null || resource.Speakers.Any(string.IsNullOrWhiteSpace) || resource.Speakers.Distinct(StringComparer.Ordinal).Count() != resource.Speakers.Count)
            issues.Add(new("dialogue.speakers.invalid", "Dialogue speakers must be unique, non-empty IDs.", nameof(resource.Speakers)));
        if (resource.Nodes is null || resource.Nodes.Count == 0) issues.Add(new("dialogue.nodes.required", "Dialogue must contain nodes.", nameof(resource.Nodes)));
        else
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var node in resource.Nodes)
            {
                if (string.IsNullOrWhiteSpace(node.Id) || !ids.Add(node.Id)) issues.Add(new("dialogue.node.id.duplicate", $"Dialogue node ID '{node.Id}' is missing or duplicated.", "nodes"));
                var type = node.Type?.ToLowerInvariant();
                if (type is not ("line" or "choice" or "jump" or "end")) { issues.Add(new("dialogue.node.type.invalid", $"Dialogue node '{node.Id}' has unsupported type '{node.Type}'.", "nodes")); continue; }
                if (type == "line") { Require(node.Speaker, "dialogue.line.speaker.required", node.Id, issues); Require(node.Text, "dialogue.line.text.required", node.Id, issues); Target(node.Next, ids, resource.Nodes, "dialogue.line.next.invalid", node.Id, issues); Speaker(node.Speaker, resource.Speakers ?? [], node.Id, issues); }
                if (type == "choice")
                {
                    Require(node.Prompt, "dialogue.choice.prompt.required", node.Id, issues);
                    if (node.Choices is null || node.Choices.Count == 0) issues.Add(new("dialogue.choice.options.required", $"Choice node '{node.Id}' must have options.", "nodes"));
                    else foreach (var option in node.Choices) { Require(option.Text, "dialogue.choice.text.required", node.Id, issues); Target(option.Next, ids, resource.Nodes, "dialogue.choice.next.invalid", node.Id, issues); }
                }
                if (type == "jump") { Require(node.Target, "dialogue.jump.target.required", node.Id, issues); Target(node.Target, ids, resource.Nodes, "dialogue.jump.target.invalid", node.Id, issues); }
                if (type == "end") Require(node.Result, "dialogue.end.result.required", node.Id, issues);
            }
            if (string.IsNullOrWhiteSpace(resource.Entry) || !resource.Nodes.Any(n => n.Id == resource.Entry)) issues.Add(new("dialogue.entry.invalid", "Dialogue entry must target an existing node.", nameof(resource.Entry)));
        }
        return issues;
    }
    public static IReadOnlyList<ValidationIssue> ValidateId(string? id, ActorIdPolicy policy = ActorIdPolicy.NewResource) { var i = new List<ValidationIssue>(); ValidateId(id, policy, i, "dialogue"); return i; }
    private static void ValidateId(string? id, ActorIdPolicy policy, ICollection<ValidationIssue> issues, string prefix)
    { if (string.IsNullOrWhiteSpace(id)) issues.Add(new($"{prefix}.id.required", "Dialogue ID is required.", "id")); else if (!IdRegex().IsMatch(id) || (policy == ActorIdPolicy.NewResource && !NewRegex().IsMatch(id))) issues.Add(new($"{prefix}.id.invalid", $"Dialogue ID '{id}' is invalid.", "id")); }
    private static void Require(string? value, string code, string id, ICollection<ValidationIssue> issues) { if (string.IsNullOrWhiteSpace(value)) issues.Add(new(code, $"Dialogue node '{id}' has a required field missing.", "nodes")); }
    private static void Speaker(string? value, IEnumerable<string> speakers, string id, ICollection<ValidationIssue> issues) { if (!string.IsNullOrWhiteSpace(value) && !speakers.Contains(value, StringComparer.Ordinal)) issues.Add(new("dialogue.speaker.undeclared", $"Dialogue node '{id}' uses undeclared speaker '{value}'.", "speakers")); }
    private static void Target(string? value, IEnumerable<string> ids, IEnumerable<DialogueNodeResource> nodes, string code, string id, ICollection<ValidationIssue> issues) { if (!string.IsNullOrWhiteSpace(value) && !nodes.Any(n => n.Id == value)) issues.Add(new(code, $"Dialogue node '{id}' targets missing node '{value}'.", "nodes")); }
    [GeneratedRegex("^[a-z0-9][a-z0-9_.-]*$", RegexOptions.CultureInvariant)] private static partial Regex IdRegex();
    [GeneratedRegex("^[a-z0-9][a-z0-9_-]*$", RegexOptions.CultureInvariant)] private static partial Regex NewRegex();
}
