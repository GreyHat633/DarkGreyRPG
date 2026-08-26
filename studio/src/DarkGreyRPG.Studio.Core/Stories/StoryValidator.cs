using System.Text.Json;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Stories;

/// <summary>Structural validation for the Story Flow graph.</summary>
public static class StoryValidator
{
    private static readonly IReadOnlyDictionary<string, string> TypeAliases =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["storystart"] = "StoryStart", ["story_start"] = "StoryStart", ["start"] = "StoryStart",
            ["actorinteract"] = "ActorInteract", ["actor_interact"] = "ActorInteract", ["interact"] = "ActorInteract", ["interact_actor"] = "ActorInteract",
            ["playdialogue"] = "PlayDialogue", ["play_dialogue"] = "PlayDialogue", ["dialogue"] = "PlayDialogue",
            ["startquest"] = "StartQuest", ["start_quest"] = "StartQuest", ["quest_start"] = "StartQuest",
            ["waitquestcomplete"] = "WaitQuestComplete", ["wait_quest_complete"] = "WaitQuestComplete", ["wait_quest"] = "WaitQuestComplete", ["quest_wait"] = "WaitQuestComplete", ["quest_completed"] = "WaitQuestComplete",
            ["dialogueexitbranch"] = "DialogueExitBranch", ["dialogue_exit_branch"] = "DialogueExitBranch", ["dialogue_branch"] = "DialogueExitBranch", ["exit_branch"] = "DialogueExitBranch",
            ["enterstory"] = "EnterStory", ["enter_story"] = "EnterStory", ["enter"] = "EnterStory",
            ["endstory"] = "EndStory", ["end_story"] = "EndStory", ["story_end"] = "EndStory",
            ["end"] = "End",
            ["enter_region"] = "EnterRegion", ["complete_quest"] = "CompleteQuest", ["branch"] = "Branch",
            ["sequence"] = "Sequence", ["quest_state"] = "QuestState", ["has_item"] = "HasItem",
            ["variable_compare"] = "VariableCompare", ["give_item"] = "GiveItem", ["give_xp"] = "GiveXp",
            ["send_message"] = "SendMessage", ["set_variable"] = "SetVariable",
        };

    public static IReadOnlyCollection<string> SupportedTypes { get; } =
        ["StoryStart", "ActorInteract", "PlayDialogue", "StartQuest", "WaitQuestComplete", "DialogueExitBranch", "EnterStory", "EndStory", "End",
         "EnterRegion", "CompleteQuest", "Branch", "Sequence", "QuestState", "HasItem", "VariableCompare", "GiveItem", "GiveXp", "SendMessage", "SetVariable"];

    public static string? CanonicalizeType(string? type) =>
        type is not null && TypeAliases.TryGetValue(type.Trim(), out var canonical) ? canonical : null;

    public static StoryNodeDescriptor? DescribeType(string? type)
    {
        var canonical = CanonicalizeType(type);
        if (canonical is null) return null;
        var required = canonical switch
        {
            "ActorInteract" => ["actor_id"],
            "PlayDialogue" => ["dialogue_id"],
            "StartQuest" or "WaitQuestComplete" => ["quest_id"],
            "EnterStory" => ["target_story_id"],
            _ => Array.Empty<string>(),
        };
        return new StoryNodeDescriptor(canonical, required);
    }

    public static IReadOnlyList<ValidationIssue> Validate(StoryResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        var issues = new List<ValidationIssue>();
        if (resource.SchemaVersion is not (StoryResource.LegacySchemaVersion or StoryResource.CurrentSchemaVersion))
            issues.Add(new("story.schema.unsupported", "Story schema_version must be 1 or 2.", nameof(resource.SchemaVersion)));
        if (string.IsNullOrWhiteSpace(resource.Id))
            issues.Add(new("story.id.required", "Story ID is required.", nameof(resource.Id)));

        var nodes = resource.Nodes ?? [];
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            if (node is null)
            {
                issues.Add(new("story.node.required", "Story nodes cannot contain null entries.", nameof(resource.Nodes)));
                continue;
            }
            if (string.IsNullOrWhiteSpace(node.Id) || !ids.Add(node.Id))
                issues.Add(new("story.node.id.duplicate", $"Story node ID '{node.Id}' is missing or duplicated.", nameof(resource.Nodes)));

            var canonical = CanonicalizeType(node.Type);
            if (canonical is null)
            {
                issues.Add(new("story.node.type.invalid", $"Story node '{node.Id}' has unsupported type '{node.Type}'.", nameof(resource.Nodes)));
            }

            if (node.Position is null || !double.IsFinite(node.Position.X) || !double.IsFinite(node.Position.Y))
                issues.Add(new("story.node.position.invalid", $"Story node '{node.Id}' position must contain finite coordinates.", nameof(resource.Nodes)));

            if (canonical is not null)
            {
                switch (canonical)
                {
                    case "ActorInteract": RequireReference(node, "actor_id", "actor", "actorId", "story.node.actor.required", issues); break;
                    case "PlayDialogue": RequireReference(node, "dialogue_id", "dialogue", "dialogueId", "story.node.dialogue.required", issues); break;
                    case "StartQuest": RequireReference(node, "quest_id", "quest", "questId", "story.node.quest.required", issues); break;
                    case "WaitQuestComplete": RequireReference(node, "quest_id", "quest", "questId", "story.node.quest.required", issues); break;
                    case "EnterStory": RequireReference(node, "target_story_id", "story_id", "story", "target", "story.node.enter_story.target.required", issues); break;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(resource.Entry) || !ids.Contains(resource.Entry))
            issues.Add(new("story.entry.invalid", "Story entry must target an existing node.", nameof(resource.Entry)));

        var connectionKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var connection in resource.Connections ?? [])
        {
            if (connection is null)
            {
                issues.Add(new("story.connection.required", "Story connections cannot contain null entries.", nameof(resource.Connections)));
                continue;
            }
            if (string.IsNullOrWhiteSpace(connection.From) || !ids.Contains(connection.From))
                issues.Add(new("story.connection.from.invalid", $"Connection source '{connection.From}' does not target an existing node.", nameof(resource.Connections)));
            if (string.IsNullOrWhiteSpace(connection.To) || !ids.Contains(connection.To))
                issues.Add(new("story.connection.to.invalid", $"Connection target '{connection.To}' does not target an existing node.", nameof(resource.Connections)));
            if (string.IsNullOrWhiteSpace(connection.Output))
                issues.Add(new("story.connection.output.required", "Connection output is required.", nameof(resource.Connections)));
            else if (!connectionKeys.Add($"{connection.From}\u001f{connection.Output}"))
                issues.Add(new("story.connection.output.duplicate", $"Node '{connection.From}' has more than one connection for output '{connection.Output}'.", nameof(resource.Connections)));
        }
        return issues;
    }

    private static void RequireReference(StoryNodeResource node, string key1, string key2, string key3, string code, ICollection<ValidationIssue> issues)
        => RequireReference(node, [key1, key2, key3], code, issues);

    private static void RequireReference(StoryNodeResource node, string key1, string key2, string key3, string key4, string code, ICollection<ValidationIssue> issues)
        => RequireReference(node, [key1, key2, key3, key4], code, issues);

    private static void RequireReference(StoryNodeResource node, IEnumerable<string> keys, string code, ICollection<ValidationIssue> issues)
    {
        foreach (var key in keys)
        {
            if (node.Properties is not null && node.Properties.TryGetValue(key, out var value)
                && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())) return;
        }
        issues.Add(new(code, $"Story node '{node.Id}' requires a non-empty resource reference.", nameof(StoryNodeResource.Properties)));
    }
}

public sealed record StoryNodeDescriptor(string Type, IReadOnlyList<string> RequiredProperties);

public sealed class StoryValidationException : Exception
{
    public StoryValidationException(IEnumerable<ValidationIssue> issues)
        : base("Story validation failed: " + string.Join("; ", issues.Where(i => i.Severity == ValidationSeverity.Error).Select(i => i.Message)))
        => Issues = issues.ToArray();

    public IReadOnlyList<ValidationIssue> Issues { get; }
}
