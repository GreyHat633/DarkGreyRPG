using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;

namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

/// <summary>Completes pre-0.3.3.1 terminate declarations in detached snapshots, without writing source files.</summary>
internal static class LegacyStoryBoundaryUpgrade
{
    public static void Apply(GraphDocument graph)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (var node in graph.Nodes)
        {
            if (node.Properties.TryGetValue("port_id", out var value) && value.ValueKind == JsonValueKind.String)
                used.Add(value.GetString()!);
            if (node.Type == "start")
                foreach (var trigger in StoryStartSchema.ReadTriggers(node)) used.Add(trigger.PortId);
        }
        foreach (var node in graph.Nodes.Where(node => node.Type == "terminate").OrderBy(node => node.Id, StringComparer.Ordinal))
        {
            // An explicit malformed ID remains a validation error; only upgrade absent legacy fields.
            if (!node.Properties.ContainsKey("port_id"))
            {
                var stem = "story_exit_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(node.Id))).ToLowerInvariant();
                var id = stem;
                for (var suffix = 1; !used.Add(id); suffix++) id = stem + "_" + suffix;
                node.Properties["port_id"] = JsonSerializer.SerializeToElement(id);
            }
            if (!node.Properties.ContainsKey("display_name"))
                node.Properties["display_name"] = JsonSerializer.SerializeToElement("终止");
        }
    }
}
