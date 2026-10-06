using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace DarkGreyRPG.Studio.Core.Identity;

/// <summary>The immutable identity of a Story, independent of its name and location.</summary>
[JsonConverter(typeof(StoryUidJsonConverter))]
public sealed record StoryUid
{
    public const string Alphabet = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
    public string Value { get; }

    private StoryUid(string value) => Value = value;

    public static bool IsValid(string? value)
    {
        if (value is null || value.Length != 22 || !value.StartsWith("ST-", StringComparison.Ordinal)) return false;
        for (var index = 3; index < value.Length; index++)
        {
            if (index is 7 or 12 or 17)
            {
                if (value[index] != '-') return false;
            }
            else if (!Alphabet.Contains(value[index])) return false;
        }
        return true;
    }

    public static StoryUid Parse(string value) => IsValid(value)
        ? new(value) : throw new ArgumentException("Invalid Story UID; expected ST-XXXX-XXXX-XXXX-XXXX.", nameof(value));

    /// <summary>Create an identity for a new Story only. Existing Stories never change UID.</summary>
    public static StoryUid Create(IReadOnlySet<StoryUid> visibleIdentities)
    {
        ArgumentNullException.ThrowIfNull(visibleIdentities);
        // A bounded retry prevents a broken allocation context from hanging the editor.
        for (var attempt = 0; attempt < 128; attempt++)
        {
            var data = new char[16];
            for (var index = 0; index < data.Length; index++)
                data[index] = Alphabet[RandomNumberGenerator.GetInt32(Alphabet.Length)];
            var uid = new StoryUid($"ST-{new string(data, 0, 4)}-{new string(data, 4, 4)}-{new string(data, 8, 4)}-{new string(data, 12, 4)}");
            if (!visibleIdentities.Contains(uid)) return uid;
        }
        throw new InvalidOperationException("Could not allocate an unused Story UID.");
    }

    public override string ToString() => Value;
}

public sealed class StoryUidJsonConverter : JsonConverter<StoryUid>
{
    public override bool HandleNull => true;
    public override StoryUid Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String || !StoryUid.IsValid(reader.GetString()))
            throw new JsonException("A current-format Story UID is required.");
        return StoryUid.Parse(reader.GetString()!);
    }

    public override void Write(Utf8JsonWriter writer, StoryUid value, JsonSerializerOptions options)
    {
        if (value is null) throw new JsonException("A Story UID cannot be null.");
        writer.WriteStringValue(value.Value);
    }
}
