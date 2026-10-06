using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DarkGreyRPG.Studio.Core.Identity;

public enum ResourceKind { Actor, Item, ItemGroup, Session, Task }

/// <summary>One owned definition. References retain this address, including its owner.</summary>
[JsonConverter(typeof(ResourceAddressJsonConverter))]
public sealed record ResourceAddress
{
    public StoryUid StoryUid { get; }
    public ResourceKind Kind { get; }
    public string LocalId { get; }

    public ResourceAddress(StoryUid storyUid, ResourceKind kind, string localId)
    {
        ArgumentNullException.ThrowIfNull(storyUid);
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind));
        if (!IsValidLocalId(localId)) throw new ArgumentException("Invalid local resource ID.", nameof(localId));
        StoryUid = storyUid;
        Kind = kind;
        LocalId = localId;
    }

    // Lowercase ASCII prevents case aliases on the portable Windows filesystem.
    public static bool IsValidLocalId(string? value) => value is { Length: >= 1 and <= 63 }
        && value[0] is >= 'a' and <= 'z'
        && value.All(c => c is (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_');

    public static ResourceAddress Create(StoryUid owner, ResourceKind kind, IReadOnlySet<ResourceAddress> occupied)
    {
        ArgumentNullException.ThrowIfNull(occupied);
        for (var attempt = 0; attempt < 128; attempt++)
        {
            var address = new ResourceAddress(owner, kind, "r" + Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(16)));
            if (!occupied.Contains(address)) return address;
        }
        throw new InvalidOperationException("Could not allocate an unused resource address.");
    }

    public string KindToken => FormatKind(Kind);

    /// <summary>Lossless internal key for graph parameters and existing string-keyed indexes; not an author-editable ID.</summary>
    public string ToKey() => $"{StoryUid.Value}~{KindToken}~{LocalId}";

    public static ResourceAddress FromKey(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        var parts = value.Split('~');
        if (parts.Length != 3) throw new ArgumentException("A current resource address key is required.", nameof(value));
        return new(StoryUid.Parse(parts[0]), ParseKind(parts[1]), parts[2]);
    }

    public static bool IsKey(string? value)
    {
        if (value is null) return false;
        try { _ = FromKey(value); return true; }
        catch (ArgumentException) { return false; }
    }

    // The prefix also makes Windows device names such as con/prn safe.
    public string RelativeDefinitionPath => $"stories/{StoryUid.Value}/resources/{KindToken}/r-{LocalId}.json";
    public string RelativeReferencePath => $"referenced_resources/{StoryUid.Value}/{KindToken}/r-{LocalId}.json";

    public static string FormatKind(ResourceKind kind) => kind switch
    {
        ResourceKind.Actor => "actor", ResourceKind.Item => "item", ResourceKind.ItemGroup => "item_group",
        ResourceKind.Session => "session", ResourceKind.Task => "task",
        _ => throw new ArgumentOutOfRangeException(nameof(kind)),
    };

    public static ResourceKind ParseKind(string token) => token switch
    {
        "actor" => ResourceKind.Actor, "item" => ResourceKind.Item, "item_group" => ResourceKind.ItemGroup,
        "session" => ResourceKind.Session, "task" => ResourceKind.Task,
        _ => throw new ArgumentException("Unknown resource kind.", nameof(token)),
    };
}

/// <summary>Only structured current-format addresses; no string or Namespace fallback.</summary>
public sealed class ResourceAddressJsonConverter : JsonConverter<ResourceAddress>
{
    public override bool HandleNull => true;
    public override ResourceAddress Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartObject) throw new JsonException("A resource address object is required.");
        string? uid = null, kind = null, local = null;
        var seen = new HashSet<string>(StringComparer.Ordinal);
        while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
        {
            if (reader.TokenType != JsonTokenType.PropertyName) throw new JsonException("Invalid resource address.");
            var name = reader.GetString()!;
            if (!seen.Add(name)) throw new JsonException("Duplicate resource address field.");
            if (!reader.Read() || reader.TokenType != JsonTokenType.String) throw new JsonException("Address fields must be strings.");
            switch (name)
            {
                case "story_uid": uid = reader.GetString(); break;
                case "kind": kind = reader.GetString(); break;
                case "local_id": local = reader.GetString(); break;
                default: throw new JsonException("Unknown resource address field.");
            }
        }
        if (reader.TokenType != JsonTokenType.EndObject || uid is null || kind is null || local is null)
            throw new JsonException("Incomplete resource address.");
        try { return new(StoryUid.Parse(uid), ResourceAddress.ParseKind(kind), local); }
        catch (ArgumentException exception) { throw new JsonException("Invalid resource address.", exception); }
    }

    public override void Write(Utf8JsonWriter writer, ResourceAddress value, JsonSerializerOptions options)
    {
        if (value is null) throw new JsonException("A resource address cannot be null.");
        writer.WriteStartObject();
        writer.WriteString("story_uid", value.StoryUid.Value);
        writer.WriteString("kind", value.KindToken);
        writer.WriteString("local_id", value.LocalId);
        writer.WriteEndObject();
    }
}
