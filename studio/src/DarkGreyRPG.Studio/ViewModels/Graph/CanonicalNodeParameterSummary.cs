using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;

namespace DarkGreyRPG.Studio.ViewModels.Graph;

/// <summary>Concise author-facing parameters rendered inside canonical nodes.</summary>
internal static class CanonicalNodeParameterSummary
{
    public static string Format(string type, IReadOnlyDictionary<string, JsonElement> properties)
        => type switch
        {
            "start" => FormatStart(properties),
            CanonicalTaskObjectiveSchema.NodeType => FormatObjective(properties),
            CanonicalStoryActionSchema.NodeType => FormatAction(properties),
            "line" => FormatLine(properties),
            _ => string.Empty,
        };

    private static string FormatStart(IReadOnlyDictionary<string, JsonElement> properties)
    {
        if (!properties.TryGetValue(StoryStartSchema.TriggersProperty, out var triggers)
            || triggers.ValueKind != JsonValueKind.Array)
            return string.Empty;

        var values = new List<string>();
        foreach (var trigger in triggers.EnumerateArray())
        {
            if (trigger.ValueKind != JsonValueKind.Object) continue;
            var type = Read(trigger, "trigger_type");
            var payload = trigger.TryGetProperty("trigger_properties", out var raw)
                && raw.ValueKind == JsonValueKind.Object ? raw : default;
            values.Add(type switch
            {
                StoryStartSchema.ActorInteraction => $"角色交互：{Read(payload, StoryStartSchema.ActorIdProperty)}",
                StoryStartSchema.RegionEntry => $"进入区域：维度 {ReadNumber(payload, StoryStartSchema.DimensionProperty)}，"
                    + $"({ReadNumber(payload, StoryStartSchema.XProperty)}, {ReadNumber(payload, StoryStartSchema.YProperty)}, "
                    + $"{ReadNumber(payload, StoryStartSchema.ZProperty)})，半径 {ReadNumber(payload, StoryStartSchema.RadiusProperty)}",
                StoryStartSchema.Logic => "逻辑条件",
                StoryStartSchema.EnterStory => "进入故事（旧版兼容）",
                _ => Read(trigger, "display_name"),
            });
        }
        return string.Join(Environment.NewLine, values.Where(value => !string.IsNullOrWhiteSpace(value)));
    }

    private static string FormatObjective(IReadOnlyDictionary<string, JsonElement> properties)
    {
        var type = Read(properties, CanonicalTaskObjectiveSchema.TypeProperty);
        var target = type switch
        {
            CanonicalTaskObjectiveSchema.KillEntity => Read(properties, CanonicalTaskObjectiveSchema.EntityProperty),
            CanonicalTaskObjectiveSchema.CollectItem => Read(properties, CanonicalTaskObjectiveSchema.ItemProperty),
            CanonicalTaskObjectiveSchema.InteractActor => Read(properties, CanonicalTaskObjectiveSchema.ActorIdProperty),
            _ => string.Empty,
        };
        var label = type switch
        {
            CanonicalTaskObjectiveSchema.KillEntity => "击杀实体",
            CanonicalTaskObjectiveSchema.CollectItem => "收集物品",
            CanonicalTaskObjectiveSchema.InteractActor => "角色交互",
            _ => "目标",
        };
        var count = ReadNumber(properties, CanonicalTaskObjectiveSchema.RequiredProperty);
        return $"{label}：{target}{(string.IsNullOrWhiteSpace(count) ? string.Empty : $" × {count}")}";
    }

    private static string FormatAction(IReadOnlyDictionary<string, JsonElement> properties)
    {
        var type = Read(properties, CanonicalStoryActionSchema.TypeProperty);
        return type switch
        {
            CanonicalStoryActionSchema.GiveItem => $"物品给予：{Read(properties, CanonicalStoryActionSchema.ItemProperty)}"
                + $" × {ReadNumber(properties, CanonicalStoryActionSchema.AmountProperty)}",
            CanonicalStoryActionSchema.GiveXp => $"经验给予：{ReadNumber(properties, CanonicalStoryActionSchema.AmountProperty)}",
            CanonicalStoryActionSchema.SendMessage => $"消息发送：{Shorten(Read(properties, CanonicalStoryActionSchema.MessageProperty))}",
            _ => string.Empty,
        };
    }

    private static string FormatLine(IReadOnlyDictionary<string, JsonElement> properties)
        => $"说话者：{Read(properties, "speaker_actor_id")}{Environment.NewLine}台词：{Shorten(Read(properties, "text"))}";

    private static string Read(IReadOnlyDictionary<string, JsonElement> properties, string name)
        => properties.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty : string.Empty;

    private static string Read(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.String ? value.GetString() ?? string.Empty : string.Empty;

    private static string ReadNumber(IReadOnlyDictionary<string, JsonElement> properties, string name)
        => properties.TryGetValue(name, out var value) && value.ValueKind == JsonValueKind.Number
            ? value.ToString() : string.Empty;

    private static string ReadNumber(JsonElement element, string name)
        => element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value)
            && value.ValueKind == JsonValueKind.Number ? value.ToString() : string.Empty;

    private static string Shorten(string value)
        => value.Length <= 44 ? value : $"{value[..41]}…";
}
