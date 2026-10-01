using System.Text;

namespace DarkGreyRPG.Studio.Core.Graphs.Definitions;

/// <summary>Protocol risk estimate; never rewrites or rejects author input.</summary>
public static class DynamicTextBudget
{
    public sealed record Result(int Bytes, int DynamicParts, int Maximum, bool Unknown)
    {
        public string Caption => Bytes >= Maximum * .9 || Unknown
            ? "选项展开后可能超过可传输长度，请缩短文本或减少动态内容。"
            : DynamicParts > 0 ? $"动态内容 {DynamicParts} 项" : "";
        public string Detail => $"展开预算 {Bytes} / {Maximum} UTF-8 字节。名称按当前资源精确计算；玩家名称按原版 16 字符保守估算，第三方 Mod 扩展值仍由服务器最终验证。" + (Unknown ? "缺失引用无法确定预算。" : "");
    }
    public static Result Measure(string text, Func<string, string?> actorName, Func<string, string?> itemName, int maximum = 2048)
    {
        int bytes = 0, count = 0; bool unknown = false;
        foreach (var part in DynamicContentText.Parse(text))
        {
            if (part.Type is null) { bytes += Encoding.UTF8.GetByteCount(part.Text ?? ""); continue; }
            count++;
            string? value = part.Type switch
            {
                "actor_name" => actorName(part.ActorId!), "item_name" => itemName(part.ItemId!),
                "player_name" => new string('界', 16), "player_level" => "-2147483648",
                "item_count" => "9223372036854775807", _ => null
            };
            if (value is null) unknown = true;
            bytes += Encoding.UTF8.GetByteCount(value ?? "引用缺失");
        }
        return new(bytes, count, maximum, unknown);
    }
}
