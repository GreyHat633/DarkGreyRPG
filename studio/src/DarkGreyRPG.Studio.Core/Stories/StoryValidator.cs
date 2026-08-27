using System.Text.Json;
using DarkGreyRPG.Studio.Core.Stories.Definitions;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Stories;

/// <summary>Structural validation for the Story Flow graph.</summary>
public static class StoryValidator
{
    // Compatibility facade for the 2.1.1 Core/WPF surface. The registry is the source of truth.
    public static IReadOnlyCollection<string> SupportedTypes => StoryNodeDefinitionRegistry.SupportedTypes;
    public static string? CanonicalizeType(string? type) => StoryNodeDefinitionRegistry.CanonicalizeType(type);

    public static StoryNodeDescriptor? DescribeType(string? type)
    {
        var definition = StoryNodeDefinitionRegistry.Get(type);
        return definition is null ? null : new StoryNodeDescriptor(definition.CanonicalType, definition.RequiredProperties.Select(property => property.Name).ToArray());
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
        var definitions = new Dictionary<string, StoryNodeDefinition>(StringComparer.Ordinal);
        foreach (var node in nodes)
        {
            if (node is null)
            {
                issues.Add(new("story.node.required", "Story nodes cannot contain null entries.", nameof(resource.Nodes)));
                continue;
            }
            if (string.IsNullOrWhiteSpace(node.Id) || !ids.Add(node.Id))
                issues.Add(new("story.node.id.duplicate", $"Story node ID '{node.Id}' is missing or duplicated.", nameof(StoryNodeResource.Id), NodeId: NullIfBlank(node.Id)));

            var definition = StoryNodeDefinitionRegistry.Get(node.Type);
            if (definition is null)
                issues.Add(new("story.node.type.invalid", $"Story node '{node.Id}' has unsupported type '{node.Type}'.", nameof(StoryNodeResource.Type), NodeId: NullIfBlank(node.Id)));
            else if (!string.IsNullOrWhiteSpace(node.Id))
            {
                definitions[node.Id] = definition;
                ValidateRequiredProperties(node, definition, issues);
            }
            if (node.Position is null || !double.IsFinite(node.Position.X) || !double.IsFinite(node.Position.Y))
                issues.Add(new("story.node.position.invalid", $"Story node '{node.Id}' position must contain finite coordinates.", nameof(StoryNodeResource.Position), NodeId: NullIfBlank(node.Id)));
        }

        if (string.IsNullOrWhiteSpace(resource.Entry) || !ids.Contains(resource.Entry))
            issues.Add(new("story.entry.invalid", "Story entry must target an existing node.", nameof(resource.Entry)));

        var nodesById = nodes.Where(node => node is not null)
            .GroupBy(node => node.Id, StringComparer.Ordinal)
            .ToDictionary(group => group.Key, group => group.First(), StringComparer.Ordinal);
        var connections = resource.Connections ?? [];
        var connectionKeys = new HashSet<string>(StringComparer.Ordinal);
        foreach (var connection in connections)
        {
            if (connection is null)
            {
                issues.Add(new("story.connection.required", "Story connections cannot contain null entries.", nameof(resource.Connections)));
                continue;
            }
            definitions.TryGetValue(connection.From, out var sourceDefinition);
            definitions.TryGetValue(connection.To, out var targetDefinition);
            nodesById.TryGetValue(connection.From, out var sourceNode);
            if (string.IsNullOrWhiteSpace(connection.From) || !ids.Contains(connection.From))
                issues.Add(new("story.connection.from.invalid", $"Connection source '{connection.From}' does not target an existing node.", nameof(resource.Connections)));
            if (string.IsNullOrWhiteSpace(connection.To) || !ids.Contains(connection.To))
                issues.Add(new("story.connection.to.invalid", $"Connection target '{connection.To}' does not target an existing node.", "input", NodeId: NullIfBlank(connection.From)));
            else if (targetDefinition is not null && !targetDefinition.AllowsInput)
                issues.Add(new("story.connection.to.input.invalid", $"Connection target '{connection.To}' does not accept an input.", "input", NodeId: connection.To));
            if (string.IsNullOrWhiteSpace(connection.Output))
                issues.Add(new("story.connection.output.required", "Connection output is required.", "output", NodeId: NullIfBlank(connection.From)));
            else if (!connectionKeys.Add($"{connection.From}\u001f{connection.Output}"))
                issues.Add(new("story.connection.output.duplicate", $"Node '{connection.From}' has more than one connection for output '{connection.Output}'.", connection.Output, NodeId: NullIfBlank(connection.From)));
            else if (sourceDefinition is not null && sourceNode is not null)
            {
                if (sourceDefinition.IsTerminal)
                    issues.Add(new("story.connection.from.terminal", $"Terminal node '{connection.From}' cannot be used as a connection source.", connection.Output, NodeId: connection.From));
                else if (!StoryNodeDefinitionRegistry.IsOutputAllowed(sourceNode, connection.Output, connections))
                    issues.Add(new("story.connection.output.invalid", $"Output '{connection.Output}' is not valid for node '{connection.From}'.", connection.Output, NodeId: connection.From));
            }
        }
        return issues;
    }

    private static void ValidateRequiredProperties(StoryNodeResource node, StoryNodeDefinition definition, ICollection<ValidationIssue> issues)
    {
        foreach (var property in definition.RequiredProperties)
        {
            if (HasScalarValue(node.Properties, new[] { property.Name }.Concat(property.Aliases))) continue;
            issues.Add(new(RequiredPropertyCode(definition.CanonicalType, property.Name), $"Story node '{node.Id}' requires a non-empty resource reference.", property.Name, NodeId: node.Id));
        }
    }

    private static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

    private static bool HasScalarValue(IReadOnlyDictionary<string, JsonElement>? properties, IEnumerable<string> keys)
    {
        if (properties is null) return false;
        foreach (var key in keys)
            if (properties.TryGetValue(key, out var value)
                && (value.ValueKind == JsonValueKind.Number || value.ValueKind is JsonValueKind.True or JsonValueKind.False
                    || value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString()))) return true;
        return false;
    }

    private static string RequiredPropertyCode(string canonicalType, string property) => (canonicalType, property) switch
    {
        ("ActorInteract", "actor_id") => "story.node.actor.required",
        ("PlayDialogue", "dialogue_id") => "story.node.dialogue.required",
        ("StartQuest", "quest_id") or ("WaitQuestComplete", "quest_id") or ("CompleteQuest", "quest_id") or ("QuestState", "quest_id") => "story.node.quest.required",
        ("EnterStory", "target_story_id") => "story.node.enter_story.target.required",
        _ => $"story.node.{canonicalType.ToLowerInvariant()}.{property}.required",
    };
}

public sealed record StoryNodeDescriptor(string Type, IReadOnlyList<string> RequiredProperties);

public sealed class StoryValidationException : Exception
{
    public StoryValidationException(IEnumerable<ValidationIssue> issues)
        : base("Story validation failed: " + string.Join("; ", issues.Where(i => i.Severity == ValidationSeverity.Error).Select(i => i.Message)))
        => Issues = issues.ToArray();
    public IReadOnlyList<ValidationIssue> Issues { get; }
}
