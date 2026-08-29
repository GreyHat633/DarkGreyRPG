package darkgrey.rpg.network.message.canonical;

import java.nio.charset.Charset;
import java.nio.charset.StandardCharsets;
import java.util.HashSet;
import java.util.Set;

import io.netty.buffer.ByteBuf;

/** Shared strict wire primitives for the server-authoritative Session messages. */
final class CanonicalSessionNetworkCodec {

    static final Charset UTF8 = StandardCharsets.UTF_8;
    static final int MAX_ID_BYTES = 96;
    static final int MAX_SPEAKER_BYTES = 256;
    static final int MAX_TEXT_BYTES = 32767;
    static final int MAX_OPTION_TEXT_BYTES = 2048;
    static final int MAX_OPTIONS = 32;

    private CanonicalSessionNetworkCodec() {}

    static void requirePositive(long value, String name) {
        if (value <= 0) throw invalid(name + " must be positive");
    }

    static String requireField(String value, String name, int maxBytes) {
        if (value == null || value.trim()
            .isEmpty()) throw invalid(name + " must be non-blank");
        byte[] encoded = value.getBytes(UTF8);
        if (encoded.length > maxBytes) throw invalid(name + " exceeds " + maxBytes + " UTF-8 bytes");
        return value;
    }

    static String readField(ByteBuf buffer, String name, int maxBytes) {
        if (buffer.readableBytes() < 2) throw invalid("truncated " + name + " length");
        int length = buffer.readUnsignedShort();
        if (length == 0 || length > maxBytes || buffer.readableBytes() < length)
            throw invalid("invalid " + name + " length");
        byte[] encoded = new byte[length];
        buffer.readBytes(encoded);
        String value = decodeUtf8(encoded, name);
        return requireField(value, name, maxBytes);
    }

    static String readOptionalField(ByteBuf buffer, String name, int maxBytes) {
        if (buffer.readableBytes() < 2) throw invalid("truncated " + name + " length");
        int length = buffer.readUnsignedShort();
        if (length > maxBytes || buffer.readableBytes() < length) throw invalid("invalid " + name + " length");
        if (length == 0) return "";
        byte[] encoded = new byte[length];
        buffer.readBytes(encoded);
        String value = decodeUtf8(encoded, name);
        if (value.trim()
            .isEmpty()) throw invalid(name + " must be empty or non-blank");
        return value;
    }

    static void writeField(ByteBuf buffer, String value, String name, int maxBytes) {
        byte[] encoded = requireField(value, name, maxBytes).getBytes(UTF8);
        if (encoded.length > 65535) throw invalid(name + " cannot fit wire length");
        buffer.writeShort(encoded.length);
        buffer.writeBytes(encoded);
    }

    static void writeOptionalEmptyField(ByteBuf buffer, String value, String name, int maxBytes) {
        if (value == null || !value.isEmpty()) throw invalid(name + " must be explicitly empty");
        buffer.writeShort(0);
    }

    static void requireNoTrailingBytes(ByteBuf buffer) {
        if (buffer.isReadable()) throw invalid("trailing unread bytes");
    }

    static int readEnum(ByteBuf buffer, int maximum, String name) {
        if (!buffer.isReadable()) throw invalid("truncated " + name);
        int value = buffer.readUnsignedByte();
        if (value >= maximum) throw invalid("invalid " + name);
        return value;
    }

    static void requireUnique(String optionId, Set<String> ids) {
        if (!ids.add(optionId)) throw invalid("duplicate option_id: " + optionId);
    }

    static Set<String> newIdSet() {
        return new HashSet<String>();
    }

    static IllegalArgumentException invalid(String message) {
        return new IllegalArgumentException("Invalid canonical Session payload: " + message);
    }

    private static String decodeUtf8(byte[] encoded, String name) {
        String value = new String(encoded, UTF8);
        if (!java.util.Arrays.equals(encoded, value.getBytes(UTF8))) throw invalid(name + " is not valid UTF-8");
        return value;
    }
}
