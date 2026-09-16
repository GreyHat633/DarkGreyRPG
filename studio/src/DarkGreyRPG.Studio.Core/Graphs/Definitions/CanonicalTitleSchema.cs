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
        if (!fields.All(node.Properties.ContainsKey) || node.Properties.Keys.Any(key => !fields.Contains(key) && key != "wait_for_completion")) { Invalid("properties"); return issues; }
        if (node.Properties.TryGetValue("wait_for_completion", out var wait) && wait.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) Invalid("wait_for_completion");
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
