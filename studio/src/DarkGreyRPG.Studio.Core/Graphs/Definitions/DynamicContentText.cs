using DarkGreyRPG.Studio.Core.Identity;
using System.Text;
using System.Text.Json;

namespace DarkGreyRPG.Studio.Core.Graphs.Definitions;

/// <summary>Versioned string envelope: old unmarked text is always literal.</summary>
public static class DynamicContentText
{
    public const string Prefix = "\u001eDGR2\u001f";
    public sealed record Part(string? Text = null, string? Type = null, string? ItemId = null, string? ActorId = null)
    {
        public string Label => Type switch { "player_name" => "玩家名称", "player_level" => "玩家经验等级", "item_count" => "持有数量：" + ItemId, "item_name" => "物品名称：" + ItemId, "actor_name" => "角色名称：" + ActorId, _ => Text ?? "动态内容无效" };
    }
    public static IReadOnlyList<Part> Parse(string? value)
    {
        value ??= "";
        if (value.StartsWith("\u001eDGR1\u001f", StringComparison.Ordinal)) throw new FormatException("旧动态内容身份格式不受支持。");
        if (!value.StartsWith(Prefix, StringComparison.Ordinal)) return [new(Text: value)];
        if (value.Length > 1048576) throw new FormatException("动态内容结构过长。");
        using var json = JsonDocument.Parse(value[Prefix.Length..]);
        if (json.RootElement.ValueKind != JsonValueKind.Array || json.RootElement.GetArrayLength() > 4096)
            throw new FormatException("动态内容结构无效或过长。");
        var parts = new List<Part>();
        foreach (var element in json.RootElement.EnumerateArray())
        {
            if (element.ValueKind == JsonValueKind.String) { parts.Add(new(Text: element.GetString())); continue; }
            if (element.ValueKind != JsonValueKind.Object) throw new FormatException("动态内容块必须为对象。");
            var type = element.GetProperty("type").GetString();
            if (type is not ("player_name" or "player_level" or "item_count" or "item_name" or "actor_name")) throw new FormatException("未知动态内容。");
            var names = element.EnumerateObject().Select(p => p.Name).Order().ToArray();
            var expected = type is "item_count" or "item_name" ? new[] { "item_id", "type" } : type == "actor_name" ? new[] { "actor_id", "type" } : new[] { "type" };
            if (!names.SequenceEqual(expected)) throw new FormatException("动态内容参数无效。");
            var address = expected.Length == 2 ? element.GetProperty(expected[0]).Deserialize<ResourceAddress>() : null;
            var id = address?.ToKey();
            if (address is not null && (type == "actor_name" ? address.Kind != ResourceKind.Actor : address.Kind is not (ResourceKind.Item or ResourceKind.ItemGroup)))
                throw new FormatException("动态内容引用类型不匹配。");
            if (expected.Length == 2 && string.IsNullOrWhiteSpace(id)) throw new FormatException("动态内容缺少资源引用。");
            parts.Add(new(Type: type, ItemId: type == "actor_name" ? null : id, ActorId: type == "actor_name" ? id : null));
        }
        return parts;
    }
    public static string Encode(IEnumerable<Part> source)
    {
        var parts = source.ToArray();
        if (parts.All(p => p.Type is null))
        {
            var text = string.Concat(parts.Select(p => p.Text));
            if (!text.StartsWith(Prefix, StringComparison.Ordinal) && !text.StartsWith("\u001eDGR1\u001f", StringComparison.Ordinal)) return text;
        }
        return Prefix + JsonSerializer.Serialize(parts.Select(p => p.Type is null ? (object)(p.Text ?? "")
            : p.Type is "item_count" or "item_name" ? new { type = p.Type, item_id = Address(p.ItemId, actor: false) }
            : p.Type == "actor_name" ? (object)new { type = p.Type, actor_id = Address(p.ActorId, actor: true) } : new { type = p.Type }));
    }
    private static ResourceAddress Address(string? key, bool actor)
    {
        var address = ResourceAddress.FromKey(key ?? "");
        if (actor ? address.Kind != ResourceKind.Actor : address.Kind is not (ResourceKind.Item or ResourceKind.ItemGroup))
            throw new FormatException("动态内容引用类型不匹配。");
        return address;
    }
    public static string Display(string? value) => string.Concat(Parse(value).Select(p => p.Type is null ? p.Text : "〔" + p.Label + "〕"));
    public static IEnumerable<string> ItemReferences(string? value) => Parse(value).Where(p => p.Type is "item_count" or "item_name").Select(p => p.ItemId!);
    public static IEnumerable<string> ActorReferences(string? value) => Parse(value).Where(p => p.Type == "actor_name").Select(p => p.ActorId!);
    public static IEnumerable<string> ActorReferences(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String) return ActorReferences(value.GetString());
        if (value.ValueKind == JsonValueKind.Array) return value.EnumerateArray().SelectMany(ActorReferences);
        if (value.ValueKind == JsonValueKind.Object) return value.EnumerateObject().SelectMany(p => ActorReferences(p.Value));
        return [];
    }
    public static IEnumerable<string> ItemReferences(JsonElement value)
    {
        if (value.ValueKind == JsonValueKind.String) return ItemReferences(value.GetString());
        if (value.ValueKind == JsonValueKind.Array) return value.EnumerateArray().SelectMany(ItemReferences);
        if (value.ValueKind == JsonValueKind.Object) return value.EnumerateObject().SelectMany(p => ItemReferences(p.Value));
        return [];
    }
    public static JsonElement Rewrite(JsonElement value, Func<string, string> resolve, Func<string, string>? resolveActor = null)
    {
        if (value.ValueKind == JsonValueKind.String && value.GetString() is { } text && text.StartsWith(Prefix, StringComparison.Ordinal))
            return JsonSerializer.SerializeToElement(Encode(Parse(text).Select(p => p.Type is "item_count" or "item_name" ? p with { ItemId = resolve(p.ItemId!) } : p.Type == "actor_name" && resolveActor is not null ? p with { ActorId = resolveActor(p.ActorId!) } : p)));
        if (value.ValueKind == JsonValueKind.Array) return JsonSerializer.SerializeToElement(value.EnumerateArray().Select(v => Rewrite(v, resolve, resolveActor)));
        if (value.ValueKind == JsonValueKind.Object) return JsonSerializer.SerializeToElement(value.EnumerateObject().ToDictionary(p => p.Name, p => Rewrite(p.Value, resolve, resolveActor)));
        return value.Clone();
    }
    public static string Resolve(string? value, Func<Part, string> resolver)
    {
        var result = new StringBuilder();
        foreach (var part in Parse(value)) result.Append(part.Type is null ? part.Text : resolver(part));
        return result.ToString();
    }
}
