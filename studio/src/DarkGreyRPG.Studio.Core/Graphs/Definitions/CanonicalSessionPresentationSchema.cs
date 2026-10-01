using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Graphs.Definitions;

public static class CanonicalSessionPresentationSchema
{
    public static IReadOnlyList<ValidationIssue> Validate(GraphNode node)
    {
        var issues = new List<ValidationIssue>();
        void Invalid(string field) => issues.Add(new("graph.session.presentation", "音乐或画面字段无效。", field, NodeId: node.Id));
        bool Number(JsonElement value, double min, double max) => value.ValueKind == JsonValueKind.Number
            && value.TryGetDouble(out var number) && double.IsFinite(number) && number >= min && number <= max;
        if (node.Type == "music")
        {
            string[] fields = ["operation", "media_ref", "loop", "volume", "fade_in", "fade_out"];
            var legacyFields = new[] { "operation", "media_ref", "loop", "fade_in", "fade_out" };
            var propertyNames = node.Properties.Keys.ToHashSet(StringComparer.Ordinal);
            if (!propertyNames.SetEquals(fields) && !propertyNames.SetEquals(legacyFields)) { Invalid("properties"); return issues; }
            var op = node.Properties["operation"];
            if (op.ValueKind != JsonValueKind.String || op.GetString() is not ("play" or "stop")) Invalid("operation");
            var media = node.Properties["media_ref"];
            if (op.ValueKind == JsonValueKind.String && op.GetString() == "play")
            {
                if (media.ValueKind != JsonValueKind.String || !MediaReference.IsAudio(media.GetString())) Invalid("media_ref");
            }
            else if (media.ValueKind != JsonValueKind.Null) Invalid("media_ref");
            if (node.Properties["loop"].ValueKind is not (JsonValueKind.True or JsonValueKind.False)) Invalid("loop");
            if (node.Properties.TryGetValue("volume", out var volume) && !Number(volume, 0, 1)) Invalid("volume");
            foreach (var field in new[] { "fade_in", "fade_out" }) if (!Number(node.Properties[field], 0, 60)) Invalid(field);
        }
        else if (node.Type == "screen")
        {
            if (node.Properties.Keys.Any(k => k is not ("layers" or "transition")) || !node.Properties.TryGetValue("layers", out var layers)
                || layers.ValueKind != JsonValueKind.Array || layers.GetArrayLength() > 32) { Invalid("layers"); return issues; }
            string[] fields = ["media_ref", "x", "y", "width", "height", "anchor_x", "anchor_y", "z"];
            if (node.Properties.TryGetValue("transition", out var transition))
            {
                if (transition.ValueKind != JsonValueKind.Object || !transition.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { "direction", "duration", "type" })
                    || transition.GetProperty("type").ValueKind != JsonValueKind.String || transition.GetProperty("type").GetString() is not ("none" or "fade" or "slide" or "wipe" or "random_lines" or "morph")
                    || transition.GetProperty("direction").ValueKind != JsonValueKind.String || transition.GetProperty("direction").GetString() is not ("left" or "right" or "up" or "down" or "horizontal" or "vertical")
                    || !Number(transition.GetProperty("duration"), 0, 60)) Invalid("transition");
                else if (transition.GetProperty("type").GetString() is "slide" or "wipe"
                    && transition.GetProperty("direction").GetString() is not ("left" or "right" or "up" or "down")) Invalid("transition.direction");
                else if (transition.GetProperty("type").GetString() == "random_lines"
                    && transition.GetProperty("direction").GetString() is not ("horizontal" or "vertical")) Invalid("transition.direction");
            }
            if (System.Text.Encoding.UTF8.GetByteCount(JsonSerializer.Serialize(layers)) > 30000) issues.Add(new("graph.session.presentation.budget", "画面动画数据超出传输容量，请减少图片或动画步骤。", "layers", NodeId: node.Id));
            var morphKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var layer in layers.EnumerateArray())
            {
                if (layer.ValueKind != JsonValueKind.Object
                    || !layer.EnumerateObject().Select(p => p.Name).Where(n => n is not ("morph_key" or "enter" or "exit" or "animations" or "morph_duration")).ToHashSet(StringComparer.Ordinal).SetEquals(fields)
                    || layer.EnumerateObject().Count() != fields.Length + new[] { "morph_key", "enter", "exit", "animations", "morph_duration" }.Count(name => layer.TryGetProperty(name, out _))) { Invalid("layers"); continue; }
                foreach (var effectName in new[] { "enter", "exit" })
                    if (layer.TryGetProperty(effectName, out var effect))
                    {
                        var proxy = new GraphNode("effect", "screen", "画面", [], new Dictionary<string, JsonElement> { ["layers"] = JsonSerializer.SerializeToElement(Array.Empty<object>()), ["transition"] = effect });
                        if (Validate(proxy).Count > 0 || effectName == "exit" && effect.TryGetProperty("type", out var effectType) && effectType.GetString() == "morph") Invalid(effectName);
                    }
                if (layer.TryGetProperty("morph_duration", out var morph) && !Number(morph, 0, 60)) Invalid("morph_duration");
                if (layer.TryGetProperty("animations", out var sequence))
                {
                    if (layer.TryGetProperty("enter", out _) || layer.TryGetProperty("exit", out _)) Invalid("animations");
                    if (sequence.ValueKind != JsonValueKind.Array) Invalid("animations");
                    else foreach (var step in sequence.EnumerateArray())
                    {
                        if (step.ValueKind != JsonValueKind.Object || !step.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(new[] { "delay", "direction", "duration", "kind", "type" })
                            || step.GetProperty("kind").ValueKind != JsonValueKind.String || step.GetProperty("kind").GetString() is not ("enter" or "exit")
                            || !Number(step.GetProperty("delay"), 0, 60)) { Invalid("animations"); continue; }
                        var effect = JsonSerializer.SerializeToElement(new { type = step.GetProperty("type"), direction = step.GetProperty("direction"), duration = step.GetProperty("duration") });
                        var proxy = new GraphNode("effect", "screen", "画面", [], new Dictionary<string, JsonElement> { ["layers"] = JsonSerializer.SerializeToElement(Array.Empty<object>()), ["transition"] = effect });
                        if (Validate(proxy).Count > 0 || step.GetProperty("type").GetString() is "none" or "morph") Invalid("animations");
                    }
                }
                if (layer.TryGetProperty("morph_key", out var key) && (key.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(key.GetString()) || key.GetString()!.Length > 96 || !morphKeys.Add(key.GetString()!))) Invalid("morph_key");
                var media = layer.GetProperty("media_ref");
                if (media.ValueKind != JsonValueKind.String || !MediaReference.IsImage(media.GetString())) Invalid("media_ref");
                foreach (var field in new[] { "x", "y" }) if (!Number(layer.GetProperty(field), -2, 3)) Invalid(field);
                foreach (var field in new[] { "width", "height" })
                    if (!Number(layer.GetProperty(field), 0, 4) || layer.GetProperty(field).GetDouble() <= 0) Invalid(field);
                foreach (var field in new[] { "anchor_x", "anchor_y" }) if (!Number(layer.GetProperty(field), 0, 1)) Invalid(field);
                if (!Number(layer.GetProperty("z"), -32768, 32767) || !layer.GetProperty("z").TryGetInt32(out _)) Invalid("z");
            }
        }
        return issues;
    }
}
