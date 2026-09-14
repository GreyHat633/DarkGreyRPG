using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Graphs.Definitions;

public static class CanonicalSessionLineSchema
{
    public static IReadOnlyList<ValidationIssue> Validate(GraphNode node)
    {
        var issues = new List<ValidationIssue>();
        void Invalid(string field, string message) => issues.Add(new("graph.session.line", message, field, NodeId: node.Id));
        foreach (var field in node.Properties.Keys)
            if (field is not ("speaker_actor_id" or "text" or "portrait_variant" or "voice_ref")) Invalid(field, "台词包含不支持的字段。");
        if (!node.Properties.TryGetValue("text", out var text) || text.ValueKind != JsonValueKind.String) Invalid("text", "台词正文必须为文本。");
        if (node.Properties.TryGetValue("speaker_actor_id", out var speaker) && speaker.ValueKind is not (JsonValueKind.Null or JsonValueKind.String))
            Invalid("speaker_actor_id", "说话角色必须为角色引用或空值。");
        if (node.Properties.TryGetValue("portrait_variant", out var variant) && variant.ValueKind != JsonValueKind.Null
            && (variant.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(variant.GetString()))) Invalid("portrait_variant", "头像变体名称必须为非空文本。");
        if (node.Properties.TryGetValue("voice_ref", out var voice) && voice.ValueKind != JsonValueKind.Null
            && (voice.ValueKind != JsonValueKind.String || !MediaReference.IsAudio(voice.GetString()))) Invalid("voice_ref", "语音必须引用项目内的 OGG 媒体。");
        return issues;
    }
}
