using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Identity;

namespace DarkGreyRPG.Studio.Core.Packaging;

/// <summary>Runtime-only records, selected for one Story; container and editor metadata never enter this API.</summary>
public sealed record StoryMemberContent(string Role, string Identity, JsonElement Content);

/// <summary>Shared C#/Java v1 canonical token encoding, followed by SHA-256.</summary>
public static class StoryMemberFingerprint
{
    private static readonly UTF8Encoding Utf8 = new(false, true);
    public const int MaximumRecords = 4096;
    public const int MaximumBytes = 64 * 1024 * 1024;

    public static string Compute(StoryUid uid, IEnumerable<StoryMemberContent> content)
    {
        ArgumentNullException.ThrowIfNull(uid);
        ArgumentNullException.ThrowIfNull(content);
        var records = content.Take(MaximumRecords + 1).ToArray();
        if (records.Length > MaximumRecords) throw new InvalidDataException("Story fingerprint record budget exceeded.");
        if (records.Any(record => record is null || string.IsNullOrEmpty(record.Role) || string.IsNullOrEmpty(record.Identity)))
            throw new InvalidDataException("Fingerprint records require role and identity.");
        if (records.Select(record => (record.Role, record.Identity)).Distinct().Count() != records.Length)
            throw new InvalidDataException("Duplicate Story fingerprint record.");
        using var buffer = new MemoryStream();
        buffer.Write("DGR-STORY-CONTENT-V1\0"u8);
        Text(uid.Value);
        Integer(records.Length);
        foreach (var record in records.OrderBy(record => record.Role, StringComparer.Ordinal)
                     .ThenBy(record => record.Identity, StringComparer.Ordinal))
        {
            Text(record.Role); Text(record.Identity); Value(record.Content, 0);
        }
        return Convert.ToHexStringLower(SHA256.HashData(buffer.GetBuffer().AsSpan(0, checked((int)buffer.Length))));

        void Budget(long additional)
        {
            if (additional > MaximumBytes - buffer.Length) throw new InvalidDataException("Story fingerprint byte budget exceeded.");
        }
        void Tag(byte value) { Budget(1); buffer.WriteByte(value); }
        void Integer(int value)
        {
            Budget(4);
            Span<byte> bytes = stackalloc byte[4]; BinaryPrimitives.WriteInt32BigEndian(bytes, value); buffer.Write(bytes);
        }
        void Text(string value)
        {
            var count = Utf8.GetByteCount(value); Budget(4L + count); Integer(count); buffer.Write(Utf8.GetBytes(value));
        }
        void Value(JsonElement value, int depth)
        {
            if (depth > 64) throw new InvalidDataException("Story fingerprint nesting budget exceeded.");
            switch (value.ValueKind)
            {
                case JsonValueKind.Null: Tag((byte)'n'); break;
                case JsonValueKind.False: Tag((byte)'f'); break;
                case JsonValueKind.True: Tag((byte)'t'); break;
                case JsonValueKind.String: Tag((byte)'s'); Text(value.GetString()!); break;
                case JsonValueKind.Number:
                    var number = value.GetDouble();
                    if (!double.IsFinite(number) || Math.Abs(number) > 9007199254740991d)
                        throw new InvalidDataException("Fingerprint numbers must be finite and within the exact integer range.");
                    Tag((byte)'d'); Budget(8);
                    Span<byte> bytes = stackalloc byte[8];
                    BinaryPrimitives.WriteInt64BigEndian(bytes, BitConverter.DoubleToInt64Bits(number == 0 ? 0 : number));
                    buffer.Write(bytes); break;
                case JsonValueKind.Array:
                    Tag((byte)'a'); Integer(value.GetArrayLength());
                    foreach (var item in value.EnumerateArray()) Value(item, depth + 1);
                    break;
                case JsonValueKind.Object:
                    var properties = value.EnumerateObject().OrderBy(property => property.Name, StringComparer.Ordinal).ToArray();
                    if (properties.Select(property => property.Name).Distinct(StringComparer.Ordinal).Count() != properties.Length)
                        throw new InvalidDataException("Duplicate JSON field in Story fingerprint content.");
                    Tag((byte)'o'); Integer(properties.Length);
                    foreach (var property in properties) { Text(property.Name); Value(property.Value, depth + 1); }
                    break;
                default: throw new InvalidDataException("Undefined Story fingerprint content.");
            }
        }
    }
}
