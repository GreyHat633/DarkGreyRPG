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
            string[] fields = ["operation", "media_ref", "loop", "fade_in", "fade_out"];
            if (!node.Properties.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(fields)) { Invalid("properties"); return issues; }
            var op = node.Properties["operation"];
            if (op.ValueKind != JsonValueKind.String || op.GetString() is not ("play" or "stop")) Invalid("operation");
            var media = node.Properties["media_ref"];
            if (op.ValueKind == JsonValueKind.String && op.GetString() == "play")
            {
                if (media.ValueKind != JsonValueKind.String || !MediaReference.IsAudio(media.GetString())) Invalid("media_ref");
            }
            else if (media.ValueKind != JsonValueKind.Null) Invalid("media_ref");
            if (node.Properties["loop"].ValueKind is not (JsonValueKind.True or JsonValueKind.False)) Invalid("loop");
            foreach (var field in new[] { "fade_in", "fade_out" }) if (!Number(node.Properties[field], 0, 60)) Invalid(field);
        }
        else if (node.Type == "screen")
        {
            if (node.Properties.Count != 1 || !node.Properties.TryGetValue("layers", out var layers)
                || layers.ValueKind != JsonValueKind.Array || layers.GetArrayLength() > 32) { Invalid("layers"); return issues; }
            string[] fields = ["media_ref", "x", "y", "width", "height", "anchor_x", "anchor_y", "z"];
            foreach (var layer in layers.EnumerateArray())
            {
                if (layer.ValueKind != JsonValueKind.Object || layer.EnumerateObject().Count() != fields.Length
                    || !layer.EnumerateObject().Select(p => p.Name).ToHashSet(StringComparer.Ordinal).SetEquals(fields)) { Invalid("layers"); continue; }
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
