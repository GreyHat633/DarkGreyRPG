package darkgrey.rpg.media;

/** Container integrity gate; actual image/audio decoding stays off the game thread. */
public final class MediaPayloadValidation {

    private MediaPayloadValidation() {}

    public static void validate(String ref, byte[] data) {
        if (ref.endsWith(".png")) {
            int position = 8;
            boolean header = false, pixels = false, end = false;
            while (position < data.length) {
                require(data.length - position >= 12);
                long length = big(data, position);
                require(length <= data.length - position - 12);
                int count = (int) length;
                java.util.zip.CRC32 crc = new java.util.zip.CRC32();
                crc.update(data, position + 4, count + 4);
                require(crc.getValue() == big(data, position + 8 + count));
                if (!header) {
                    require(matches(data, position + 4, "IHDR") && count == 13);
                    long width = big(data, position + 8), height = big(data, position + 12);
                    require(
                        width > 0 && height > 0
                            && width <= 33554432
                            && height <= 33554432
                            && width * height <= 33554432);
                    header = true;
                } else require(!matches(data, position + 4, "IHDR"));
                if (matches(data, position + 4, "IDAT")) pixels = true;
                boolean last = matches(data, position + 4, "IEND");
                position += count + 12;
                if (last) {
                    require(count == 0 && position == data.length);
                    end = true;
                    break;
                }
            }
            require(header && pixels && end);
        } else if (ref.endsWith(".ogg")) {
            int position = 0;
            boolean first = true, end = false;
            while (position < data.length) {
                require(data.length - position >= 27 && matches(data, position, "OggS") && data[position + 4] == 0);
                int segments = data[position + 26] & 255, body = 0;
                require(data.length - position >= 27 + segments);
                for (int i = 0; i < segments; i++) body += data[position + 27 + i] & 255;
                int size = 27 + segments + body;
                require(size <= data.length - position && !end);
                if (first) require(
                    body >= 30 && data[position + 27 + segments] == 1
                        && matches(data, position + 28 + segments, "vorbis")
                        && (data[position + 5] & 2) != 0);
                int crc = 0;
                for (int i = 0; i < size; i++) {
                    crc ^= (i >= 22 && i <= 25 ? 0 : data[position + i] & 255) << 24;
                    for (int bit = 0; bit < 8; bit++) crc = (crc << 1) ^ ((crc & 0x80000000) != 0 ? 0x04C11DB7 : 0);
                }
                int expected = (data[position + 22] & 255) | (data[position + 23] & 255) << 8
                    | (data[position + 24] & 255) << 16
                    | (data[position + 25] & 255) << 24;
                require(crc == expected);
                end = (data[position + 5] & 4) != 0;
                first = false;
                position += size;
            }
            require(!first && end);
        } else require(
            data.length >= 20 && (data[0] & 255) == 255
                && (data[1] & 255) == 216
                && (data[data.length - 2] & 255) == 255
                && (data[data.length - 1] & 255) == 217);
    }

    private static boolean matches(byte[] data, int offset, String value) {
        if (offset < 0 || offset + value.length() > data.length) return false;
        for (int i = 0; i < value.length(); i++) if (data[offset + i] != value.charAt(i)) return false;
        return true;
    }

    private static long big(byte[] data, int offset) {
        return ((long) (data[offset] & 255) << 24) | ((long) (data[offset + 1] & 255) << 16)
            | ((long) (data[offset + 2] & 255) << 8)
            | (data[offset + 3] & 255);
    }

    private static void require(boolean valid) {
        if (!valid) throw new IllegalArgumentException("Corrupt media container or excessive decoded dimensions");
    }
}
