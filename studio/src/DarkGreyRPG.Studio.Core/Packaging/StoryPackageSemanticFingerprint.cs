using System.Text.Json;
using System.Text.Json.Nodes;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;

namespace DarkGreyRPG.Studio.Core.Packaging;

/// <summary>Current member inputs for the shared canonical fingerprint protocol.</summary>
public static class StoryPackageSemanticFingerprint
{
    public static string Compute(StoryPackageManifest manifest, IReadOnlyDictionary<string, byte[]> entries,
        CanonicalStoryLogicGraph? connections = null)
    {
        var uid = StoryUid.Parse(manifest.StoryId);
        var records = new List<StoryMemberContent>();
        Add("runtime_schema", uid.Value, JsonNode.Parse("""{"identity":"story-uid-v1","project":3,"graph":2,"membership":4,"actor":5,"item":2}""")!);
        var required = manifest.RequiredResources;
        foreach (var (role, paths) in new[]
        {
            ("story", required.CanonicalStories), ("actor", required.Actors), ("item", required.Items),
            ("item_group", required.ItemGroups), ("session", required.Sessions), ("task", required.Tasks),
            ("membership", required.CanonicalMemberships),
        })
        foreach (var path in paths)
        {
            var json = JsonNode.Parse(entries[path])!.AsObject();
            var field = role switch { "membership" => "story_id", "actor" => json["type"]!.GetValue<string>() == "individual" ? "npc_id" : "group_id",
                "item" => "item_id", "item_group" => "group_id", _ => "id" };
            var identity = role is "story" or "membership" ? json[field]!.GetValue<string>()
                : JsonSerializer.Deserialize<ResourceAddress>(json[field]!.ToJsonString())!.ToKey();
            json.Remove("tags"); json.Remove("display_order");
            if (role == "membership")
                foreach (var section in new[] { "owned_resources", "referenced_resources" })
                    foreach (var entry in json[section]!.AsObject().ToArray())
                        json[section]![entry.Key] = Sorted(entry.Value!.AsArray(), node => JsonSerializer.Deserialize<ResourceAddress>(node!.ToJsonString())!.ToKey());
            if (json["graph"] is JsonObject graph)
            {
                foreach (var entry in graph["nodes"]!.AsArray())
                {
                    var type = entry!["type"]!.GetValue<string>();
                    if (type is "end" or "terminate" or "logic_output") entry["properties"]!.AsObject().Remove("display_order");
                    if (type is "session" or "task" or "story")
                    {
                        var ports = entry["ports"]!.AsArray();
                        foreach (var port in ports) port!.AsObject().Remove("order");
                        entry["ports"] = Sorted(ports, port => port!["port_id"]!.GetValue<string>());
                    }
                }
                graph["nodes"] = Sorted(graph["nodes"]!.AsArray(), node => node!["id"]!.GetValue<string>());
                var edges = graph["connections"]!.AsArray().OrderBy(node => node!["from_node_id"]!.GetValue<string>(), StringComparer.Ordinal)
                    .ThenBy(node => node!["from_port_id"]!.GetValue<string>(), StringComparer.Ordinal)
                    .ThenBy(node => node!["to_node_id"]!.GetValue<string>(), StringComparer.Ordinal)
                    .ThenBy(node => node!["to_port_id"]!.GetValue<string>(), StringComparer.Ordinal)
                    .ThenBy(node => node!["interface_kind"]!.GetValue<string>(), StringComparer.Ordinal);
                graph["connections"] = new JsonArray(edges.Select(node => node!.DeepClone()).ToArray());
            }
            Add(role, identity, json);
        }
        foreach (var media in required.Media) Add("media", media, JsonValue.Create(media[6..70])!);
        if (connections is null && required.StoryLogicGraph is { } graphPath)
        {
            using var json = JsonDocument.Parse(entries[graphPath]);
            connections = CanonicalStoryLogicGraphRepository.Parse(json.RootElement);
        }
        var relevant = (connections ?? CanonicalStoryLogicGraph.Empty).Connections
            .Where(edge => edge.SourceStoryId == uid.Value || edge.TargetStoryId == uid.Value)
            .OrderBy(edge => edge.SourceStoryId, StringComparer.Ordinal).ThenBy(edge => edge.SourcePortId, StringComparer.Ordinal)
            .ThenBy(edge => edge.TargetStoryId, StringComparer.Ordinal).ThenBy(edge => edge.TargetPortId, StringComparer.Ordinal)
            .ThenBy(edge => edge.InterfaceKind, StringComparer.Ordinal).ToArray();
        for (var index = 0; index < relevant.Length; index++) Add("story_edge", index.ToString("D8"), JsonSerializer.SerializeToNode(relevant[index])!);
        return StoryMemberFingerprint.Compute(uid, records);

        void Add(string role, string id, JsonNode content) => records.Add(new(role, id, JsonSerializer.SerializeToElement(content)));
    }

    private static JsonArray Sorted(JsonArray array, Func<JsonNode?, string> key)
        => new(array.OrderBy(key, StringComparer.Ordinal).Select(node => node!.DeepClone()).ToArray());
}
