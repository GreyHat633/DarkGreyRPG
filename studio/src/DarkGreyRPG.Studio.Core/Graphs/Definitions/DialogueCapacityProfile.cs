using System.Globalization;
using System.Text.Json;

namespace DarkGreyRPG.Studio.Core.Graphs.Definitions;

/// <summary>Numeric standard-font profile; contains no Minecraft font textures.</summary>
public static class DialogueCapacityProfile
{
    private static readonly JsonDocument Profile = JsonDocument.Parse(typeof(DialogueCapacityProfile).Assembly.GetManifestResourceStream("DialogueCapacityProfile.json")!);
    private static readonly int[] Normal = Profile.RootElement.GetProperty("normal").EnumerateArray().Select(x => x.GetInt32()).ToArray();
    private static readonly int[] Unicode = Profile.RootElement.GetProperty("unicode").EnumerateArray().Select(x => x.GetInt32()).ToArray();
    public static int Width => Profile.RootElement.GetProperty("wrap_width").GetInt32();
    public static int Raw => Profile.RootElement.GetProperty("raw").GetInt32();
    public static int Safe => Profile.RootElement.GetProperty("safe").GetInt32();
    public sealed record Result(int Used, int Maximum, int Lines, bool Unsupported, bool ManualNewline, bool Dynamic = false)
    {
        public bool Over => Used > Maximum || Unsupported || ManualNewline;
        public string Caption => $"容量 {Used} / {Maximum}" + (Dynamic ? " · 含动态估算" : "");
        public string Warning => ManualNewline ? "单句不支持手动换行，请拆分为多句"
            : Unsupported ? "含标准字体无法测量的字符；请检查游戏显示。"
            : Used > Maximum ? "超过推荐显示容量；游戏可能分屏显示。建议拆成多句。"
            : Used >= Maximum * .9 ? "接近推荐显示容量。" : Dynamic ? "动态内容为估算；游戏按实际文本测量并分屏。" : "";
    }
    public static Result Measure(string text)
    {
        bool dynamic = text.StartsWith(DynamicContentText.Prefix, StringComparison.Ordinal);
        if (dynamic)
        {
            try { text = DynamicContentText.Resolve(text, p => p.Type == "player_name" ? new string('界', 16) : new string('8', p.Type == "player_level" ? 10 : 19)); }
            catch (Exception error) when (error is JsonException or FormatException or InvalidOperationException or KeyNotFoundException)
            { return new(0, Safe, 1, true, false, true); }
        }
        // The supported standard modes share one conservative recommendation.
        var results = Profile.RootElement.GetProperty("geometries").EnumerateArray()
            .SelectMany(shape => new[] { Measure(text, Normal, shape.GetProperty("wrap_width").GetInt32(), shape.GetProperty("safe").GetInt32()),
                Measure(text, Unicode, shape.GetProperty("wrap_width").GetInt32(), shape.GetProperty("safe").GetInt32()) }).ToArray();
        var worst = results.MaxBy(result => (double)result.Used / result.Maximum)!;
        return worst with { Unsupported = results.Any(result => result.Unsupported), Dynamic = dynamic };
    }
    private static Result Measure(string text, int[] advances, int width, int safe)
    {
        var breaks = StringInfo.ParseCombiningCharacters(text);
        int at = 0, row = 1, used = 0, budget = 0; bool bold = false, unsupported = false, newline = false;
        while (at < text.Length)
        {
            if (text[at] == '§' && at + 1 < text.Length)
            {
                var code = char.ToLowerInvariant(text[at + 1]);
                if ("0123456789abcdefr".Contains(code)) bold = false; else if (code == 'l') bold = true;
                at += 2; continue;
            }
            int next = Array.BinarySearch(breaks, at);
            next = next >= 0 ? next + 1 : ~next;
            int end = next < breaks.Length ? breaks[next] : text.Length;
            int advance = 0;
            bool linebreak = false;
            for (int i = at; i < end; i++)
            {
                if (text[i] is '\r' or '\n') { newline = linebreak = true; continue; }
                var advanceWidth = advances[text[i]];
                if (advanceWidth == 0 && !char.IsControl(text[i])) unsupported = true;
                advance += advanceWidth + (bold && advanceWidth > 0 ? 1 : 0);
            }
            if (advance > width) unsupported = true;
            if (used > 0 && used + advance > width || linebreak) { budget += width; row++; used = 0; }
            if (!linebreak) used += advance;
            at = end;
        }
        return new(budget + used, safe, row, unsupported, newline);
    }
}
