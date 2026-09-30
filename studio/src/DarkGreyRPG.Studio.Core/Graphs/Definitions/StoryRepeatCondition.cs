using System.Globalization;
using System.Text.Json;

namespace DarkGreyRPG.Studio.Core.Graphs.Definitions;

public sealed record StoryRepeatCondition(string Type = "none", long Value = 24, string Unit = "hours",
    string Period = "daily", int Weekday = 1, int Month = 1, int Day = 1, string Time = "12:00")
{
    public string? Error => Type switch
    {
        "none" => null,
        "cooldown" when Unit is not ("seconds" or "minutes" or "hours") => "冷却单位必须为秒、分钟或小时。",
        "cooldown" when Value <= 0 || Value > long.MaxValue / (Unit == "hours" ? 3600000 : Unit == "minutes" ? 60000 : 1000) => "冷却时间必须是有效范围内的正整数。",
        "cooldown" => null,
        "scheduled" when Period is not ("daily" or "weekly" or "yearly") => "请选择每日、每周或每年。",
        "scheduled" when !TimeOnly.TryParseExact(Time, "HH:mm", CultureInfo.InvariantCulture, DateTimeStyles.None, out _) => "时间格式为 HH:mm（00:00～23:59）。",
        "scheduled" when Period == "weekly" && (Weekday < 1 || Weekday > 7) => "请选择星期一至星期日。",
        "scheduled" when Period == "yearly" && (Month < 1 || Month > 12 || Day < 1 || Day > DateTime.DaysInMonth(2000, Month)) => "请选择有效月日；2 月 29 日仅在闰年生效。",
        "scheduled" => null,
        _ => "未知的重复方式。"
    };
    public JsonElement ToJson() => Type switch
    {
        "cooldown" => JsonSerializer.SerializeToElement(new { type = Type, value = Value, unit = Unit }),
        "scheduled" when Period == "weekly" => JsonSerializer.SerializeToElement(new { type = Type, period = Period, weekday = Weekday, time = Time }),
        "scheduled" when Period == "yearly" => JsonSerializer.SerializeToElement(new { type = Type, period = Period, month = Month, day = Day, time = Time }),
        "scheduled" => JsonSerializer.SerializeToElement(new { type = Type, period = Period, time = Time }),
        _ => JsonSerializer.SerializeToElement(new { type = Type })
    };
    public static StoryRepeatCondition Parse(JsonElement json)
    {
        if (json.ValueKind != JsonValueKind.Object) throw new FormatException("重复条件必须为对象。");
        string S(string key) => json.GetProperty(key).GetString() ?? throw new FormatException("重复字段不能为空。");
        var type = S("type");
        var result = type switch
        {
            "none" => new(),
            "cooldown" => new StoryRepeatCondition(type, json.GetProperty("value").GetInt64(), S("unit")),
            "scheduled" => new StoryRepeatCondition(type, Period: S("period"), Time: S("time"),
                Weekday: S("period") == "weekly" ? json.GetProperty("weekday").GetInt32() : 1,
                Month: S("period") == "yearly" ? json.GetProperty("month").GetInt32() : 1,
                Day: S("period") == "yearly" ? json.GetProperty("day").GetInt32() : 1),
            _ => throw new FormatException("未知的重复方式。")
        };
        if (result.Error is { } error) throw new FormatException(error);
        if (!json.EnumerateObject().Select(p => p.Name).Order().SequenceEqual(result.ToJson().EnumerateObject().Select(p => p.Name).Order()))
            throw new FormatException("重复条件包含未知或重复字段。");
        return result;
    }
}
