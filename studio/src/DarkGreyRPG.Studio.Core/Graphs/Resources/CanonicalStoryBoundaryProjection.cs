using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;

namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

/// <summary>Public Story ports are derived from internal declarations, never stored copies.</summary>
public static class CanonicalStoryBoundaryProjection
{
    public static IReadOnlyList<GraphPort> Ports(GraphResourceEnvelope story)
    {
        var result = new List<GraphPort>();
        var graph = story.Graph;

        foreach (var node in graph?.Nodes ?? [])
        {
            if (node.Type == "start")
                foreach (var trigger in StoryStartSchema.ReadTriggers(node).Where(t => t.TriggerType == StoryStartSchema.FlowDriven))
                    result.Add(new(trigger.PortId, trigger.DisplayName, true, GraphInterfaceKind.Flow, trigger.Order));
            if (node.Type is not ("terminate" or "logic_input" or "logic_output")) continue;
            if (!node.Properties.TryGetValue("port_id", out var id) || id.ValueKind != JsonValueKind.String
                || string.IsNullOrWhiteSpace(id.GetString())) continue;
            var name = node.Properties.TryGetValue("display_name", out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? "" : "";
            result.Add(new(id.GetString()!, name, node.Type == "logic_input",
                node.Type == "terminate" ? GraphInterfaceKind.Flow : GraphInterfaceKind.Logic, node.Type == "logic_input" ? result.Count : PublicOutputSchema.Order(node)));
        }
        var sorted = result.OrderBy(port => port.IsInput ? 0 : 1).ThenBy(port => port.InterfaceKind).ThenBy(port => port.Order).ToArray();
        foreach (var direction in sorted.GroupBy(port => port.IsInput))
        {
            var order = 0;
            foreach (var port in direction) port.Order = order++;
        }
        return sorted;
    }
}
