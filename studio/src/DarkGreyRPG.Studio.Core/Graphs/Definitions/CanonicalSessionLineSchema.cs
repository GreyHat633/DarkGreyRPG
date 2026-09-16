using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Core.Graphs.Definitions;

public static class CanonicalSessionLineSchema
{
    private static readonly string[] PageFields = ["text", "portrait_variant", "voice_ref", "voice_volume", "text_speed", "custom_text_speed"];

    public static Dictionary<string, JsonElement> CreatePage(string? pageId = null) => new(StringComparer.Ordinal)
    {
        ["page_id"] = JsonSerializer.SerializeToElement(pageId ?? $"page_{Guid.NewGuid():N}"),
        ["text"] = JsonSerializer.SerializeToElement(""),
        ["voice_volume"] = JsonSerializer.SerializeToElement(1d),
        ["text_speed"] = JsonSerializer.SerializeToElement(30d),
        ["custom_text_speed"] = JsonSerializer.SerializeToElement(false),
    };

    /// <summary>Detached editable pages. Legacy identity is deterministic until first save.</summary>
    public static IReadOnlyList<Dictionary<string, JsonElement>> ReadPages(GraphNode node)
    {
        if (node.Properties.TryGetValue("pages", out var pages))
            return pages.EnumerateArray().Select(page => page.EnumerateObject()
                .ToDictionary(p => p.Name, p => p.Value.Clone(), StringComparer.Ordinal)).ToArray();
        var legacy = CreatePage(node.Id + ":page:0");
        foreach (var field in PageFields)
            if (node.Properties.TryGetValue(field, out var value)) legacy[field] = value.Clone();
        if (!node.Properties.ContainsKey("text")) legacy.Remove("text");
        return [legacy];
    }

    public static void Normalize(GraphNode node)
    {
        if (node.Type != "line") return;
        if (!node.Properties.ContainsKey("pages"))
            node.Properties["pages"] = JsonSerializer.SerializeToElement(ReadPages(node));
        foreach (var field in PageFields) node.Properties.Remove(field);
    }

    public static IReadOnlyList<ValidationIssue> Validate(GraphNode node)
    {
        var issues = new List<ValidationIssue>();
        void Invalid(string field, string message) => issues.Add(new("graph.session.line", message, field, NodeId: node.Id));
        foreach (var field in node.Properties.Keys)
            if (field != "speaker_actor_id" && field != "pages" && !PageFields.Contains(field)) Invalid(field, "台词包含不支持的字段。");
        if (node.Properties.TryGetValue("speaker_actor_id", out var speaker) && speaker.ValueKind is not (JsonValueKind.Null or JsonValueKind.String))
            Invalid("speaker_actor_id", "说话角色必须为角色引用或空值。");
        if (node.Properties.TryGetValue("pages", out var pages))
        {
            if (pages.ValueKind != JsonValueKind.Array || pages.GetArrayLength() == 0)
            { Invalid("pages", "台词至少需要一句。"); return issues; }
            var ids = new HashSet<string>(StringComparer.Ordinal);
            var index = 0;
            foreach (var page in pages.EnumerateArray())
            {
                var path = $"pages[{index++}]";
                if (page.ValueKind != JsonValueKind.Object) { Invalid(path, "每句台词必须为对象。"); continue; }
                var fields = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
                foreach (var field in page.EnumerateObject())
                    if (!fields.TryAdd(field.Name, field.Value)) Invalid(path + "." + field.Name, "句子字段不能重复。");
                if (!fields.TryGetValue("page_id", out var id) || id.ValueKind != JsonValueKind.String
                    || string.IsNullOrWhiteSpace(id.GetString()) || !ids.Add(id.GetString()!)) Invalid(path + ".page_id", "句子标识不能为空或重复。");
                foreach (var field in fields.Keys)
                    if (field != "page_id" && !PageFields.Contains(field)) Invalid(path + "." + field, "句子包含不支持的字段。");
                ValidatePage(fields, path + ".");
            }
        }
        else ValidatePage(node.Properties, "");
        return issues;

        void ValidatePage(IDictionary<string, JsonElement> fields, string path)
        {
            if (!fields.TryGetValue("text", out var text) || text.ValueKind != JsonValueKind.String) Invalid(path + "text", "台词正文必须为文本。");
            if (fields.TryGetValue("portrait_variant", out var variant) && variant.ValueKind != JsonValueKind.Null
                && (variant.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(variant.GetString()))) Invalid(path + "portrait_variant", "头像变体名称必须为非空文本。");
            if (fields.TryGetValue("voice_ref", out var voice) && voice.ValueKind != JsonValueKind.Null
                && (voice.ValueKind != JsonValueKind.String || !MediaReference.IsAudio(voice.GetString()))) Invalid(path + "voice_ref", "语音必须引用项目内的 OGG 媒体。");
            if (fields.TryGetValue("voice_volume", out var volume)
                && (!Number(volume) || volume.GetDouble() < 0 || volume.GetDouble() > 1)) Invalid(path + "voice_volume", "语音音量必须在 0 到 1 之间。");
            if (fields.TryGetValue("text_speed", out var speed)
                && (!Number(speed) || speed.GetDouble() < 0 || speed.GetDouble() > 120)) Invalid(path + "text_speed", "显示速度必须在 0 到 120 字/秒之间。");
            if (fields.TryGetValue("custom_text_speed", out var custom) && custom.ValueKind is not (JsonValueKind.True or JsonValueKind.False)) Invalid(path + "custom_text_speed", "自定义显示速度必须为布尔值。");
        }
        static bool Number(JsonElement value) => value.ValueKind == JsonValueKind.Number
            && value.TryGetDouble(out var number) && double.IsFinite(number);
    }
}
