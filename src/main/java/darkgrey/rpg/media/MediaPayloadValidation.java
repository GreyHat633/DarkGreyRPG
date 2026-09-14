package darkgrey.rpg.media;

/** Container integrity gate; actual image/audio decoding stays off the game thread. */
public final class MediaPayloadValidation {

    private MediaPayloadValidation() {}

    public static void validate(String ref, byte[] data) {
        try {
            validate(ref, new java.io.ByteArrayInputStream(data), data.length);
        } catch (java.io.IOException exception) {
            throw new IllegalArgumentException("Corrupt media container or truncated payload", exception);
        }
    }

    /** Streaming container validation. At most one PNG chunk or OGG page is retained. */
    public static void validate(String ref, java.io.InputStream input, long expectedLength) throws java.io.IOException {
        if (input == null || expectedLength <= 0L || expectedLength > 64L * 1024L * 1024L)
            throw new IllegalArgumentException("Invalid media stream bounds");
        if (ref.endsWith(".png")) validatePng(input, expectedLength);
        else if (ref.endsWith(".ogg")) validateOgg(input, expectedLength);
        else validateJpeg(input, expectedLength);
    }

    private static void validatePng(java.io.InputStream input, long expected) throws java.io.IOException {
        byte[] signature = new byte[8];
        readFully(input, signature, 0, signature.length);
        require(matches(signature, "\u0089PNG\r\n\u001a\n"));
        long consumed = 8L;
        boolean header = false, pixels = false, end = false;
        byte[] headerBytes = new byte[8];
        byte[] buffer = new byte[32768];
        while (consumed < expected) {
            readFully(input, headerBytes, 0, 8);
            consumed += 8;
            long countLong = big(headerBytes, 0);
            require(countLong >= 0 && countLong <= expected - consumed - 4);
            int count = (int) countLong;
            byte[] type = new byte[4];
            System.arraycopy(headerBytes, 4, type, 0, 4);
            java.util.zip.CRC32 crc = new java.util.zip.CRC32();
            crc.update(type);
            long remaining = count;
            if (!header) {
                require(matches(type, "IHDR") && count == 13);
                byte[] ihdr = new byte[13];
                readFully(input, ihdr, 0, 13);
                consumed += 13;
                crc.update(ihdr);
                long width = big(ihdr, 0), height = big(ihdr, 4);
                require(
                    width > 0 && height > 0
                        && width <= 33554432L
                        && height <= 33554432L
                        && width * height <= 33554432L);
                remaining = 0;
                header = true;
            } else {
                if (count > 0) {
                    while (remaining > 0) {
                        int step = (int) Math.min((long) buffer.length, remaining);
                        readFully(input, buffer, 0, step);
                        crc.update(buffer, 0, step);
                        consumed += step;
                        remaining -= step;
                    }
                }
            }
            byte[] crcBytes = new byte[4];
            readFully(input, crcBytes, 0, 4);
            consumed += 4;
            require(crc.getValue() == big(crcBytes, 0));
            if (matches(type, "IDAT")) pixels = true;
            if (matches(type, "IEND")) {
                require(count == 0 && consumed == expected);
                end = true;
                break;
            }
        }
        require(header && pixels && end && consumed == expected);
    }

    private static void validateOgg(java.io.InputStream input, long expected) throws java.io.IOException {
        long consumed = 0L;
        boolean first = true, end = false;
        byte[] header = new byte[27];
        while (consumed < expected) {
            readFully(input, header, 0, header.length);
            consumed += 27;
            require(matches(header, 0, "OggS") && header[4] == 0 && !end);
            int segments = header[26] & 255;
            byte[] lacing = new byte[segments];
            readFully(input, lacing, 0, segments);
            consumed += segments;
            int body = 0;
            for (byte value : lacing) body += value & 255;
            require(consumed + body <= expected);
            byte[] page = new byte[27 + segments + body];
            System.arraycopy(header, 0, page, 0, 27);
            System.arraycopy(lacing, 0, page, 27, segments);
            readFully(input, page, 27 + segments, body);
            consumed += body;
            if (first) require(
                body >= 30 && page[27 + segments] == 1 && matches(page, 28 + segments, "vorbis") && (page[5] & 2) != 0);
            int crc = 0;
            for (int i = 0; i < page.length; i++) {
                crc ^= (i >= 22 && i <= 25 ? 0 : page[i] & 255) << 24;
                for (int bit = 0; bit < 8; bit++) crc = (crc << 1) ^ ((crc & 0x80000000) != 0 ? 0x04C11DB7 : 0);
            }
            int expectedCrc = (page[22] & 255) | (page[23] & 255) << 8
                | (page[24] & 255) << 16
                | (page[25] & 255) << 24;
            require(crc == expectedCrc);
            end = (page[5] & 4) != 0;
            first = false;
        }
        require(!first && end && consumed == expected);
    }

    private static void validateJpeg(java.io.InputStream input, long expected) throws java.io.IOException {
        require(expected >= 20L);
        int first = input.read();
        int second = input.read();
        require(first == 255 && second == 216);
        long count = 2;
        int beforePrevious = first, previous = second, current;
        while ((current = input.read()) != -1) {
            count++;
            beforePrevious = previous;
            previous = current;
        }
        require(count == expected && beforePrevious == 255 && previous == 217); // baseline structural guard remains
                                                                                // intentionally minimal
    }

    private static void readFully(java.io.InputStream input, byte[] data, int offset, int length)
        throws java.io.IOException {
        int position = offset;
        while (position < offset + length) {
            int count = input.read(data, position, offset + length - position);
            if (count < 0) throw new java.io.EOFException();
            if (count == 0) continue;
            position += count;
        }
    }

    private static boolean matches(byte[] data, String value) {
        if (data.length != value.length()) return false;
        for (int i = 0; i < data.length; i++) if ((data[i] & 255) != value.charAt(i)) return false;
        return true;
    }

    /* Legacy byte-array parser retained below only for source compatibility. */
    private static void validateLegacy(String ref, byte[] data) {
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
