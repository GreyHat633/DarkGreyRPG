using System.Buffers.Binary;

namespace DarkGreyRPG.Studio.Core.Media;

/// <summary>Container integrity checks before packaging; import additionally performs a full FFmpeg decode.</summary>
public static class MediaPayloadValidation
{
    public static void Validate(string reference, byte[] data)
    {
        void Invalid() => throw new InvalidDataException("媒体容器损坏或超过解码限制。");
        if (reference.EndsWith(".png", StringComparison.Ordinal))
        {
            int position = 8; bool header = false, pixels = false, end = false;
            while (position < data.Length)
            {
                if (data.Length - position < 12) { Invalid(); return; }
                uint length = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(position, 4));
                if (length > data.Length - position - 12) { Invalid(); return; }
                int count = (int)length;
                var kind = data.AsSpan(position + 4, 4);
                uint crc = 0xFFFFFFFF;
                for (int i = position + 4; i < position + 8 + count; i++)
                { crc ^= data[i]; for (int bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ ((crc & 1) == 1 ? 0xEDB88320u : 0); }
                if ((crc ^ 0xFFFFFFFF) != BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(position + 8 + count, 4))) Invalid();
                if (!header)
                {
                    if (!kind.SequenceEqual("IHDR"u8) || count != 13) { Invalid(); return; }
                    uint width = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(position + 8, 4)), height = BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(position + 12, 4));
                    if (width == 0 || height == 0 || (ulong)width * height > 33554432) Invalid();
                    header = true;
                }
                else if (kind.SequenceEqual("IHDR"u8)) Invalid();
                if (kind.SequenceEqual("IDAT"u8)) pixels = true;
                position += count + 12;
                if (kind.SequenceEqual("IEND"u8)) { if (count != 0 || position != data.Length) Invalid(); end = true; break; }
            }
            if (!header || !pixels || !end) Invalid();
        }
        else if (reference.EndsWith(".ogg", StringComparison.Ordinal))
        {
            int position = 0; bool first = true, end = false;
            while (position < data.Length)
            {
                if (data.Length - position < 27 || !data.AsSpan(position, 4).SequenceEqual("OggS"u8) || data[position + 4] != 0) { Invalid(); return; }
                int segments = data[position + 26], body = 0;
                if (data.Length - position < 27 + segments) { Invalid(); return; }
                for (int i = 0; i < segments; i++) body += data[position + 27 + i];
                int size = 27 + segments + body;
                if (size > data.Length - position || end) { Invalid(); return; }
                if (first && (body < 30 || data[position + 27 + segments] != 1 || !data.AsSpan(position + 28 + segments, 6).SequenceEqual("vorbis"u8) || (data[position + 5] & 2) == 0)) Invalid();
                uint crc = 0;
                for (int i = 0; i < size; i++)
                {
                    crc ^= (uint)(i is >= 22 and <= 25 ? 0 : data[position + i]) << 24;
                    for (int bit = 0; bit < 8; bit++) crc = (crc << 1) ^ ((crc & 0x80000000) != 0 ? 0x04C11DB7u : 0);
                }
                if (crc != BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(position + 22, 4))) Invalid();
                end = (data[position + 5] & 4) != 0; first = false; position += size;
            }
            if (first || !end) Invalid();
        }
        else if (data.Length < 20 || data[0] != 255 || data[1] != 216 || data[^2] != 255 || data[^1] != 217) Invalid();
    }
}
