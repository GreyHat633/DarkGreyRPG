using System.Text.Json;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Graphs.Definitions;

/// <summary>Public identity is stable; order is explicit and local to the output kind.</summary>
public static class PublicOutputSchema
{
    public static bool IsOutput(GraphNode node) => node.Type is "end" or "terminate" or "settle" or "logic_output";
    public static GraphInterfaceKind Kind(GraphNode node) => node.Type == "logic_output" ? GraphInterfaceKind.Logic : GraphInterfaceKind.Flow;
    public static int Order(GraphNode node)
    {
        if (!node.Properties.TryGetValue("display_order", out var value) || value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var order) || order < 0)
            throw new FormatException($"Public output '{node.Id}' requires a nonnegative display_order.");
        return order;
    }

    public static IReadOnlyList<ValidationIssue> Validate(IEnumerable<GraphNode> nodes)
    {
        var issues = new List<ValidationIssue>();
        var orders = new HashSet<(GraphInterfaceKind, int)>();
        var identities = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in nodes.Where(IsOutput))
        {
            if (!node.Properties.TryGetValue("port_id", out var identity) || identity.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(identity.GetString()))
                issues.Add(new("graph.output.port_id.required", "输出端口缺少稳定 port_id。", "properties.port_id", NodeId: node.Id));
            else if (!identities.Add(identity.GetString()!))
                issues.Add(new("graph.output.port_id.duplicate", "输出端口的稳定 port_id 不能重复。", "properties.port_id", NodeId: node.Id));
            if (!node.Properties.TryGetValue("display_name", out var name) || name.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(name.GetString()))
                issues.Add(new("graph.output.name.required", "输出端口名称不能为空。", "properties.display_name", NodeId: node.Id));
            if (!node.Properties.TryGetValue("display_order", out var value) || value.ValueKind != JsonValueKind.Number
                || !value.TryGetInt32(out var order) || order < 0)
                issues.Add(new("graph.output.order.required", "输出端口必须具有明确的非负排序。", "properties.display_order", NodeId: node.Id));
            else if (!orders.Add((Kind(node), order)))
                issues.Add(new("graph.output.order.duplicate", "同类输出端口的排序不能重复。", "properties.display_order", NodeId: node.Id));
        }
        return issues;
    }
}
