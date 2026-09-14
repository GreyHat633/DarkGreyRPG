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
    public const string GiveHealth = "give_health";
    public const string Teleport = "teleport_player";
    public const string GiveBuff = "give_buff";
    public const string ExecuteCommand = "execute_command";

    public static IReadOnlyList<string> ActionTypes { get; } = [GiveItem, GiveXp, GiveBuff, GiveHealth, Teleport, SendMessage, ExecuteCommand];

    public static string AuthoringDisplayNameFor(string? type) => type switch
    {
        GiveItem => "物品给予",
        GiveXp => "经验给予",
        SendMessage => "消息发送",
        GiveBuff => "BUFF给予",
        GiveHealth => "生命给予",
        Teleport => "玩家传送",
        ExecuteCommand => "指令执行",
        _ => "执行",
    };

    public static IReadOnlySet<string> AllProperties { get; } = new HashSet<string>(
        [TypeProperty, ItemIdProperty, AmountProperty, MessageProperty, "dimension_id", "x", "y", "z",
         "mod_extension", "buff", "mod_id", "buff_name", "duration_delta", "level_delta", "command"], StringComparer.Ordinal);

    public static IReadOnlySet<string> PropertiesFor(string type) => type switch
    {
        GiveItem => new HashSet<string>([TypeProperty, ItemIdProperty, AmountProperty], StringComparer.Ordinal),
        GiveHealth => new HashSet<string>([TypeProperty, AmountProperty], StringComparer.Ordinal),
        Teleport => new HashSet<string>([TypeProperty, "dimension_id", "x", "y", "z"], StringComparer.Ordinal),
        GiveBuff => new HashSet<string>([TypeProperty, "mod_extension", "buff", "duration_delta", "level_delta"], StringComparer.Ordinal),
        ExecuteCommand => new HashSet<string>([TypeProperty, "command"], StringComparer.Ordinal),
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

        var expected = type == GiveBuff && properties.TryGetValue("mod_extension", out var extension) && extension.ValueKind == JsonValueKind.True
            ? new HashSet<string>([TypeProperty, "mod_extension", "mod_id", "buff_name", "duration_delta", "level_delta"], StringComparer.Ordinal)
            : PropertiesFor(type!);
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
                ValidateInteger(properties, AmountProperty, int.MinValue, issues, node.Id);
                break;
            case GiveXp:
                ValidateInteger(properties, AmountProperty, int.MinValue, issues, node.Id);
                break;
            case GiveHealth:
                ValidateFinite(properties, AmountProperty, issues, node.Id); break;
            case Teleport:
                ValidateInteger(properties, "dimension_id", int.MinValue, issues, node.Id);
                foreach (var field in new[] { "x", "y", "z" }) ValidateFinite(properties, field, issues, node.Id);
                foreach (var field in new[] { "x", "y", "z" })
                    if (properties.TryGetValue(field, out var coordinate) && coordinate.ValueKind == JsonValueKind.Number
                        && coordinate.TryGetDouble(out var number) && Math.Abs(number) > (field == "y" ? 30000000 : 29999984))
                        issues.Add(Issue("graph.story.action.teleport.bounds", "传送坐标超出 Minecraft 世界范围。", field, node.Id));
                break;
            case GiveBuff:
                if (!properties.TryGetValue("mod_extension", out var mod) || mod.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
                    issues.Add(Issue("graph.story.action.buff", "MOD扩展必须为开关值。", "mod_extension", node.Id));
                if (mod.ValueKind == JsonValueKind.True)
                {
                    ValidateString(properties, "mod_id", issues, node.Id); ValidateString(properties, "buff_name", issues, node.Id);
                }
                else if (!VanillaBuffs.Any(buff => buff.Value == ReadString(properties, "buff")))
                    issues.Add(Issue("graph.story.action.buff", "请选择 Vanilla BUFF。", "buff", node.Id));
                ValidateInteger(properties, "duration_delta", int.MinValue, issues, node.Id);
                ValidateInteger(properties, "level_delta", int.MinValue, issues, node.Id);
                break;
            case ExecuteCommand:
                ValidateString(properties, "command", issues, node.Id);
                var command = ReadString(properties, "command") ?? "";
                if (command.Length > 2048 || command.IndexOfAny(['\r', '\n', '\0']) >= 0 || !command.StartsWith('/') || string.IsNullOrWhiteSpace(command.TrimStart('/')))
                    issues.Add(Issue("graph.story.action.command", "请输入以 / 开头的完整单行指令，最多 2048 字符。", "command", node.Id));
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
                properties[ItemIdProperty] = JsonSerializer.SerializeToElement("");
                properties[AmountProperty] = JsonSerializer.SerializeToElement(10);
                break;
            case GiveXp:
                properties[AmountProperty] = JsonSerializer.SerializeToElement(10);
                break;
            case GiveHealth:
                properties[AmountProperty] = JsonSerializer.SerializeToElement(0); break;
            case Teleport:
                foreach (var field in new[] { "dimension_id", "x", "y", "z" }) properties[field] = JsonSerializer.SerializeToElement(0);
                break;
            case GiveBuff:
                properties["mod_extension"] = JsonSerializer.SerializeToElement(false);
                properties["buff"] = JsonSerializer.SerializeToElement("speed");
                properties["duration_delta"] = JsonSerializer.SerializeToElement(30);
                properties["level_delta"] = JsonSerializer.SerializeToElement(1); break;
            case ExecuteCommand:
                properties["command"] = JsonSerializer.SerializeToElement(""); break;
            case SendMessage:
                properties[MessageProperty] = JsonSerializer.SerializeToElement("");
                break;
        }
    }

    private static IReadOnlyList<ValidationIssue> ValidateType(string? type, string? nodeId)
        => ActionTypes.Contains(type ?? string.Empty, StringComparer.Ordinal)
            ? []
            : [Issue("graph.story.action.type.invalid",
                "请选择支持的执行类型。", $"properties.{TypeProperty}", nodeId)];

    public static IReadOnlyList<ValidationIssue> AllowDraftIssues(GraphNode node, IReadOnlyList<ValidationIssue> issues)
        => issues.Where(issue =>
        {
            var field = issue.Field?.Replace("properties.", "", StringComparison.Ordinal);
            return issue.Code is "graph.story.action.property.unsupported" or "graph.story.action.property.missing"
                || field is not ("message" or "command" or "item_id" or "mod_id" or "buff_name")
                || !node.Properties.TryGetValue(field, out var value)
                || value.ValueKind != JsonValueKind.String || !string.IsNullOrEmpty(value.GetString());
        }).ToArray();

    public sealed record VanillaBuffOption(string Value, string DisplayName);
    public static IReadOnlyList<VanillaBuffOption> VanillaBuffs { get; } =
    [new("speed", "速度"), new("slowness", "缓慢"), new("haste", "急迫"), new("mining_fatigue", "挖掘疲劳"),
     new("strength", "力量"), new("instant_health", "瞬间治疗"), new("instant_damage", "瞬间伤害"),
     new("jump_boost", "跳跃提升"), new("nausea", "反胃"), new("regeneration", "生命恢复"), new("resistance", "抗性提升"),
     new("fire_resistance", "防火"), new("water_breathing", "水下呼吸"), new("invisibility", "隐身"), new("blindness", "失明"),
     new("night_vision", "夜视"), new("hunger", "饥饿"), new("weakness", "虚弱"), new("poison", "中毒"), new("wither", "凋零"),
     new("health_boost", "生命提升"), new("absorption", "伤害吸收"), new("saturation", "饱和")];

    private static void ValidateFinite(IReadOnlyDictionary<string, JsonElement> properties, string name,
        ICollection<ValidationIssue> issues, string? nodeId)
    {
        if (!properties.TryGetValue(name, out var value) || value.ValueKind != JsonValueKind.Number
            || !value.TryGetDouble(out var number) || !double.IsFinite(number))
            issues.Add(Issue("graph.story.action.number", "请输入有限数值。", name, nodeId));
    }

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
