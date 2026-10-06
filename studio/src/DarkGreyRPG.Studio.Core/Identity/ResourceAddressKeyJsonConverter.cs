using System.Text.Json;
using System.Text.Json.Serialization;

namespace DarkGreyRPG.Studio.Core.Identity;

/// <summary>Structured wire addresses for the editor's lossless string indexes.</summary>
public abstract class ResourceAddressKeyJsonConverter(ResourceKind kind) : JsonConverter<string>
{
    public override bool HandleNull => true;

    public override string Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var address = JsonSerializer.Deserialize<ResourceAddress>(ref reader, options)
            ?? throw new JsonException("Resource address is required.");
        if (address.Kind != kind) throw new JsonException("Resource address kind does not match its field.");
        return address.ToKey();
    }

    public override void Write(Utf8JsonWriter writer, string value, JsonSerializerOptions options)
    {
        if (!ResourceAddress.IsKey(value)) throw new JsonException("Resource address is required.");
        var address = ResourceAddress.FromKey(value);
        if (address.Kind != kind) throw new JsonException("Resource address kind does not match its field.");
        JsonSerializer.Serialize(writer, address, options);
    }
}

public sealed class ActorAddressKeyJsonConverter() : ResourceAddressKeyJsonConverter(ResourceKind.Actor);
public sealed class ItemAddressKeyJsonConverter() : ResourceAddressKeyJsonConverter(ResourceKind.Item);
public sealed class ItemGroupAddressKeyJsonConverter() : ResourceAddressKeyJsonConverter(ResourceKind.ItemGroup);
