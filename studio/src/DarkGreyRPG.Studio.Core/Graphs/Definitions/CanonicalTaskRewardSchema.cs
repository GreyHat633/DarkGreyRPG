using System.Text.Json;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Graphs.Definitions;

public static class CanonicalTaskRewardSchema
{
    public const string NodeType = "reward";
    public const string EntriesProperty = "entries";

    public static IReadOnlyList<ValidationIssue> Validate(GraphNode node)
    {
        var issues = new List<ValidationIssue>();
        void Invalid(string field, string message) => issues.Add(new("graph.reward.package", message, field, NodeId: node.Id));
        if (node.Properties.Count != 1 || !node.Properties.TryGetValue(EntriesProperty, out var entries)
            || entries.ValueKind != JsonValueKind.Array)
        {
            Invalid("properties.entries", "奖励节点只允许奖励列表 entries。");
            return issues;
        }
        var index = 0;
        foreach (var entry in entries.EnumerateArray())
        {
            var field = $"properties.entries[{index++}]";
            if (entry.ValueKind != JsonValueKind.Object)
            {
                Invalid(field, "每条奖励必须为物品或经验。");
                continue;
            }
            var properties = entry.EnumerateObject().ToArray();
            if (properties.Select(p => p.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length)
                Invalid(field, "奖励字段不能重复。");
            var type = entry.TryGetProperty("type", out var typeValue) && typeValue.ValueKind == JsonValueKind.String
                ? typeValue.GetString() : null;
            if (type is not ("item" or "xp")) Invalid(field, "奖励类型只能为物品或经验。");
            var expected = type == "item" ? new[] { "type", "item", "amount" } : new[] { "type", "amount" };
            if (!properties.Select(p => p.Name).ToHashSet(StringComparer.Ordinal).SetEquals(expected))
                Invalid(field, "奖励字段不符合所选类型。");
            if (!entry.TryGetProperty("amount", out var amount) || amount.ValueKind != JsonValueKind.Number || !amount.TryGetInt32(out _))
                Invalid(field + ".amount", "数量必须为有符号整数；正数给予、负数扣除、零不操作。");
            if (type == "item" && (!entry.TryGetProperty("item", out var item) || item.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(item.GetString())))
                Invalid(field + ".item", "请选择物品。");
        }
        return issues;
    }
}
