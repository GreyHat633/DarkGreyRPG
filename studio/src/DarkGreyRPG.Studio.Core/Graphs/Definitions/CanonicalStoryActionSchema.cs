using System.Text.Json;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Graphs.Definitions;

/// <summary>Frozen persisted contract for the enabled Stage 5 Story action subset.</summary>
public static class CanonicalStoryActionSchema
{
    public const string NodeType = "action";
    public const string TypeProperty = "action_type";
    public const string ItemIdProperty = "item_id";
    public const string ItemProperty = ItemIdProperty;
    public const string AmountProperty = "amount";
    public const string MessageProperty = "message";

    public const string GiveItem = "give_item";
    public const string GiveXp = "give_xp";
    public const string SendMessage = "send_message";

    public static IReadOnlyList<string> ActionTypes { get; } = [GiveItem, GiveXp, SendMessage];

    public static string AuthoringDisplayNameFor(string? type) => type switch
    {
        GiveItem => "物品给予",
        GiveXp => "经验给予",
        SendMessage => "消息发送",
        _ => "动作",
    };

    public static IReadOnlySet<string> AllProperties { get; } = new HashSet<string>(
        [TypeProperty, ItemIdProperty, AmountProperty, MessageProperty], StringComparer.Ordinal);

    public static IReadOnlySet<string> PropertiesFor(string type) => type switch
    {
        GiveItem => new HashSet<string>([TypeProperty, ItemIdProperty, AmountProperty], StringComparer.Ordinal),
        GiveXp => new HashSet<string>([TypeProperty, AmountProperty], StringComparer.Ordinal),
        SendMessage => new HashSet<string>([TypeProperty, MessageProperty], StringComparer.Ordinal),
        _ => new HashSet<string>(StringComparer.Ordinal),
    };

    public static void InitializeDefault(GraphNode node) => InitializeType(node, SendMessage);

    public static bool TryInitializeType(GraphNode node, string? type, out IReadOnlyList<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(node);
        issues = ValidateType(type, node.Id);
        if (issues.Count != 0) return false;
        InitializeType(node, type!);
        issues = [];
        return true;
    }

    public static IReadOnlyList<ValidationIssue> Validate(GraphNode node)
    {
        ArgumentNullException.ThrowIfNull(node);
        var issues = new List<ValidationIssue>();
        var properties = node.Properties ?? [];
        var type = ReadString(properties, TypeProperty);
        issues.AddRange(ValidateType(type, node.Id));
        if (issues.Count != 0) return issues;

        var expected = PropertiesFor(type!);
        foreach (var key in properties.Keys)
            if (!expected.Contains(key))
                issues.Add(Issue("graph.story.action.property.unsupported",
                    $"Action property '{key}' is not valid for type '{type}'.", $"properties.{key}", node.Id));
        foreach (var key in expected)
            if (!properties.ContainsKey(key))
                issues.Add(Issue("graph.story.action.property.missing",
                    $"Action property '{key}' is required for type '{type}'.", $"properties.{key}", node.Id));

        switch (type)
        {
            case GiveItem:
                ValidateString(properties, ItemIdProperty, issues, node.Id);
                ValidateInteger(properties, AmountProperty, 1, issues, node.Id);
                break;
            case GiveXp:
                ValidateInteger(properties, AmountProperty, 1, issues, node.Id);
                break;
            case SendMessage:
                ValidateString(properties, MessageProperty, issues, node.Id);
                break;
        }
        return issues;
    }

    private static void InitializeType(GraphNode node, string type)
    {
        if (!string.Equals(node.Type, NodeType, StringComparison.Ordinal))
            throw new ArgumentException("The node must be a Story action.", nameof(node));
        var properties = node.Properties ??= new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        properties.Clear();
        properties[TypeProperty] = JsonSerializer.SerializeToElement(type);
        switch (type)
        {
            case GiveItem:
                properties[ItemIdProperty] = JsonSerializer.SerializeToElement("starter_reward");
                properties[AmountProperty] = JsonSerializer.SerializeToElement(10);
                break;
            case GiveXp:
                properties[AmountProperty] = JsonSerializer.SerializeToElement(10);
                break;
            case SendMessage:
                properties[MessageProperty] = JsonSerializer.SerializeToElement("任务完成");
                break;
        }
    }

    private static IReadOnlyList<ValidationIssue> ValidateType(string? type, string? nodeId)
        => ActionTypes.Contains(type ?? string.Empty, StringComparer.Ordinal)
            ? []
            : [Issue("graph.story.action.type.invalid",
                "Action type must be give_item, give_xp, or send_message.", $"properties.{TypeProperty}", nodeId)];

    private static void ValidateString(IReadOnlyDictionary<string, JsonElement> properties, string name,
        ICollection<ValidationIssue> issues, string? nodeId)
    {
        if (!properties.TryGetValue(name, out var value) || value.ValueKind != JsonValueKind.String
            || string.IsNullOrWhiteSpace(value.GetString()))
            issues.Add(Issue("graph.story.action.string.invalid", $"Action '{name}' must be a nonblank string.",
                $"properties.{name}", nodeId));
    }

    private static void ValidateInteger(IReadOnlyDictionary<string, JsonElement> properties, string name, int minimum,
        ICollection<ValidationIssue> issues, string? nodeId)
    {
        if (!properties.TryGetValue(name, out var value) || value.ValueKind != JsonValueKind.Number
            || !value.TryGetInt32(out var parsed) || parsed < minimum)
            issues.Add(Issue("graph.story.action.integer.invalid",
                $"Action '{name}' must be an integer greater than or equal to {minimum}.",
                $"properties.{name}", nodeId));
    }

    private static string? ReadString(IReadOnlyDictionary<string, JsonElement> properties, string name)
        => properties.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() : null;

    private static ValidationIssue Issue(string code, string message, string field, string? nodeId)
        => new(code, message, field, NodeId: string.IsNullOrWhiteSpace(nodeId) ? null : nodeId);
}
