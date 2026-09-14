using System.Text.Json;
using DarkGreyRPG.Studio.Core.Identity;

namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

/// <summary>Task feature reference traversal using the existing Item identity rules.</summary>
public static class CanonicalTaskRewardReferences
{
    public static void Validate(GraphNode node, GraphResourceKind kind, IReadOnlySet<DgrResourceKey> declared)
    {
        if (kind != GraphResourceKind.Task || node.Type != "reward") return;
        foreach (var entry in node.Properties["entries"].EnumerateArray())
            if (entry.GetProperty("type").GetString() == "item")
            {
                var id = entry.GetProperty("item").GetString()!;
                if (!declared.Contains(new(DgrResourceKind.Item, id)))
                    throw new InvalidDataException($"奖励节点 '{node.Id}' 引用了未声明物品 '{id}'。");
            }
    }

    public static void Rewrite(GraphNode node, GraphResourceKind kind, Func<DgrResourceKind, string, string> resolve)
    {
        if (kind != GraphResourceKind.Task || node.Type != "reward") return;
        var entries = node.Properties["entries"].EnumerateArray().Select(entry =>
        {
            var fields = entry.EnumerateObject().ToDictionary(field => field.Name, field => field.Value.Clone());
            if (entry.GetProperty("type").GetString() == "item")
                fields["item"] = JsonSerializer.SerializeToElement(resolve(DgrResourceKind.Item, entry.GetProperty("item").GetString()!));
            return fields;
        }).ToArray();
        node.Properties["entries"] = JsonSerializer.SerializeToElement(entries);
    }
}
