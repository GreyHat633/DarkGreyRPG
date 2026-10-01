using System.Text.Json;
using System.Text.Json.Nodes;

namespace DarkGreyRPG.Studio.Core.Graphs.Definitions;

public sealed record ScreenAnimationStep(string Kind, string Type, string Direction, double Duration, double Delay)
{
    public ScreenLayerEffect Effect => new(Type, Direction, Duration);
    public static ScreenAnimationStep Read(JsonElement value) => new(value.GetProperty("kind").GetString()!, value.GetProperty("type").GetString()!, value.GetProperty("direction").GetString()!, value.GetProperty("duration").GetDouble(), value.GetProperty("delay").GetDouble());
}

public static class ScreenAnimationSequence
{
    public static void NormalizeGraph(GraphDocument graph)
    {
        foreach (var node in graph.Nodes)
        {
            if (node.Type == "screen" && node.Properties.TryGetValue("layers", out var layers))
            {
                var fallback = node.Properties.TryGetValue("transition", out var transition) ? ScreenLayerEffect.Read(transition) : ScreenLayerEffect.None;
                node.Properties["layers"] = JsonSerializer.SerializeToElement(layers.EnumerateArray().Select(l => Normalize(JsonNode.Parse(l.GetRawText())!.AsObject(), fallback)).ToArray());
                node.Properties.Remove("transition");
            }
            if (node.Type == "choice" && node.Properties.TryGetValue("options", out var options))
                node.Properties["options"] = JsonSerializer.SerializeToElement(options.EnumerateArray().Select(option =>
                {
                    var value = JsonNode.Parse(option.GetRawText())!.AsObject();
                    if (value.ContainsKey("condition_port_id"))
                    {
                        bool enabled = SessionChoiceSchema.ConditionEnabled(option, graph, node.Id);
                        value["condition_enabled"] = enabled;
                        if (!enabled) graph.Connections.RemoveAll(edge => edge.ToNodeId == node.Id && edge.ToPortId == value["condition_port_id"]!.GetValue<string>());
                    }
                    return value;
                }).ToArray());
        }
    }

    public static JsonObject Normalize(JsonObject layer, ScreenLayerEffect fallback)
    {
        var result = (JsonObject)layer.DeepClone();
        if (result["animations"] is null)
        {
            var steps = new JsonArray();
            ScreenLayerEffect Read(string field, ScreenLayerEffect defaultValue) => result[field] is JsonObject effect
                ? ScreenLayerEffect.Read(JsonSerializer.SerializeToElement(effect)) : defaultValue;
            var enter = Read("enter", ScreenLayerAnimation.LegacyEntry(fallback));
            var exit = Read("exit", ScreenLayerEffect.None);
            foreach (var (kind, effect) in new[] { ("enter", enter), ("exit", exit) })
                if (effect.Type is not ("none" or "morph")) steps.Add(new JsonObject { ["kind"] = kind, ["type"] = effect.Type, ["direction"] = effect.Direction, ["duration"] = effect.Duration, ["delay"] = 0 });
            result["animations"] = steps;
            result["morph_duration"] = enter.Type == "morph" ? enter.Duration : 0;
        }
        result.Remove("enter"); result.Remove("exit");
        return result;
    }

    public static IReadOnlyList<ScreenAnimationStep> Steps(ScreenTransitionPreview.Sprite sprite, ScreenLayerEffect fallback)
    {
        if (sprite.Animations is not null) return sprite.Animations;
        var steps = new List<ScreenAnimationStep>();
        var enter = sprite.Enter ?? ScreenLayerAnimation.LegacyEntry(fallback);
        if (enter.Type is not ("none" or "morph")) steps.Add(new("enter", enter.Type, enter.Direction, enter.Duration, 0));
        if (sprite.Exit is { Type: not ("none" or "morph") } exit) steps.Add(new("exit", exit.Type, exit.Direction, exit.Duration, 0));
        return steps;
    }

    public static double Morph(ScreenTransitionPreview.Sprite sprite, ScreenLayerEffect fallback)
        => sprite.Animations is not null ? sprite.MorphDuration : (sprite.Enter ?? fallback) is { Type: "morph" } effect ? effect.Duration : 0;
}
