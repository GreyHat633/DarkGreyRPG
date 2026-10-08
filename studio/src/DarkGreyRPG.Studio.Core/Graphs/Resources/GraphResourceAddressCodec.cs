using System.Text.Json;
using System.Text.Json.Nodes;
using DarkGreyRPG.Studio.Core.Identity;

namespace DarkGreyRPG.Studio.Core.Graphs.Resources;

/// <summary>Converts only schema-declared references, never arbitrary text or node IDs.</summary>
internal static class GraphResourceAddressCodec
{
    public static void Transform(GraphDocument graph, GraphResourceKind scope, bool writing)
    {
        foreach (var node in graph.Nodes)
        {
            var properties = JsonSerializer.SerializeToNode(node.Properties)!.AsObject();
            void Field(string name, params ResourceKind[] kinds) => Address(properties, name, writing, kinds);
            if (scope == GraphResourceKind.Story)
            {
                if (node.Type == "session") Field("resource_id", ResourceKind.Session);
                if (node.Type == "task") Field("resource_id", ResourceKind.Task);
                if (node.Type == "interact_actor") Field("actor_id", ResourceKind.Actor);
                if (node.Type == "action" && Text(properties, "action_type") == "give_item")
                {
                    if (properties.ContainsKey("item") || properties.ContainsKey("metadata"))
                        throw new JsonException($"Node '{node.Id}': give_item requires a DGR Item address in item_id.");
                    Field("item_id", ResourceKind.Item);
                }
                if (node.Type == "enter_story" && Text(properties, "target_story_id") is { Length: > 0 } uid && !StoryUid.IsValid(uid))
                    throw new JsonException("A current target Story UID is required.");
                if (node.Type == "start" && properties["triggers"] is JsonArray triggers)
                    foreach (var trigger in triggers.OfType<JsonObject>())
                        if (Text(trigger, "trigger_type") == "interact_actor" && trigger["trigger_properties"] is JsonObject fields)
                            Address(fields, "actor_id", writing, ResourceKind.Actor);
            }
            if (scope == GraphResourceKind.Session && node.Type == "line") Field("speaker_actor_id", ResourceKind.Actor);
            if (scope == GraphResourceKind.Task && node.Type == "objective")
            {
                var type = Text(properties, "objective_type");
                if (type == "kill_entity") Field("entity", ResourceKind.Actor);
                if (type is "interact_actor" or "submit_item") Field("actor_id", ResourceKind.Actor);
                if (type is "collect_item" or "submit_item") Field("item", ResourceKind.Item, ResourceKind.ItemGroup);
            }
            if (scope == GraphResourceKind.Task && node.Type == "reward" && properties["entries"] is JsonArray entries)
                foreach (var entry in entries.OfType<JsonObject>())
                    if (Text(entry, "type") == "item") Address(entry, "item", writing, ResourceKind.Item);
            foreach (var property in properties)
                node.Properties[property.Key] = JsonSerializer.SerializeToElement(property.Value);
        }
    }

    private static string? Text(JsonObject fields, string name) => fields[name]?.GetValue<string>();

    private static void Address(JsonObject fields, string name, bool writing, params ResourceKind[] kinds)
    {
        // Unconfigured draft fields stay unconfigured; a nonempty reference must be current-format.
        if (fields[name] is null || fields[name] is JsonValue value && value.TryGetValue<string>(out var text) && text.Length == 0) return;
        var address = writing ? ResourceAddress.FromKey(fields[name]!.GetValue<string>())
            : fields[name]!.Deserialize<ResourceAddress>() ?? throw new JsonException("Resource address is required.");
        if (!kinds.Contains(address.Kind)) throw new JsonException($"Resource kind mismatch at '{name}'.");
        fields[name] = writing ? JsonSerializer.SerializeToNode(address) : JsonValue.Create(address.ToKey());
    }
}
