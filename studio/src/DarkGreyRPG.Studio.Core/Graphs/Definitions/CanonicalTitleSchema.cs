using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Graphs.Definitions;

public static class CanonicalTitleSchema
{
    public static IReadOnlyList<ValidationIssue> Validate(GraphNode node)
    {
        var issues = new List<ValidationIssue>();
        void Invalid(string field) => issues.Add(new("graph.story.title", "标题字段无效：主标题不能为空，时间为 0–60 秒。", field, NodeId: node.Id));
        string[] fields = ["main", "subtitle", "fade_in", "stay", "fade_out"];
        if (!node.Properties.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(fields)) { Invalid("properties"); return issues; }
        foreach (var field in new[] { "main", "subtitle" })
        {
            var value = node.Properties[field];
            if (value.ValueKind != JsonValueKind.String || value.GetString()!.Length > 1024
                || (field == "main" && string.IsNullOrWhiteSpace(value.GetString()))) Invalid(field);
        }
        foreach (var field in new[] { "fade_in", "stay", "fade_out" })
        {
            var value = node.Properties[field];
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number) || !double.IsFinite(number) || number < 0 || number > 60) Invalid(field);
        }
        return issues;
    }
}
