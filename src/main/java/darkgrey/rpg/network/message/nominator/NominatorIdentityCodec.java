package darkgrey.rpg.network.message.nominator;

import java.nio.charset.StandardCharsets;

import darkgrey.rpg.identity.ResourceAddress;
import darkgrey.rpg.identity.StoryUid;
import io.netty.buffer.ByteBuf;

/** Current wire addresses retain owner, kind and local identity as separate fields. */
public final class NominatorIdentityCodec {

    private NominatorIdentityCodec() {}

    public static void write(ByteBuf buffer, String key, ResourceAddress.Kind expected) {
        if (key == null || key.isEmpty()) {
            buffer.writeByte(0);
            return;
        }
        ResourceAddress address = ResourceAddress.fromKey(key);
        if (address.getKind() != expected) throw new IllegalArgumentException("Resource kind mismatch");
        buffer.writeByte(1);
        text(
            buffer,
            address.getStoryUid()
                .getValue());
        text(
            buffer,
            address.getKind()
                .getToken());
        text(buffer, address.getLocalId());
    }

    public static String read(ByteBuf buffer, ResourceAddress.Kind expected) {
        int present = buffer.readUnsignedByte();
        if (present == 0) return null;
        if (present != 1) throw new IllegalArgumentException("Invalid address presence");
        ResourceAddress address = new ResourceAddress(
            StoryUid.parse(text(buffer)),
            ResourceAddress.Kind.parse(text(buffer)),
            text(buffer));
        if (address.getKind() != expected) throw new IllegalArgumentException("Resource kind mismatch");
        return address.toKey();
    }

    public static void story(ByteBuf buffer, String uid) {
        text(
            buffer,
            uid == null || uid.isEmpty() ? ""
                : StoryUid.parse(uid)
                    .getValue());
    }

    public static String story(ByteBuf buffer) {
        String uid = text(buffer);
        return uid.isEmpty() ? null
            : StoryUid.parse(uid)
                .getValue();
    }

    private static void text(ByteBuf buffer, String value) {
        byte[] bytes = value.getBytes(StandardCharsets.US_ASCII);
        if (bytes.length > 63) throw new IllegalArgumentException("Identity field too long");
        buffer.writeByte(bytes.length);
        buffer.writeBytes(bytes);
    }

    private static String text(ByteBuf buffer) {
        int length = buffer.readUnsignedByte();
        if (length > 63 || length > buffer.readableBytes())
            throw new IllegalArgumentException("Invalid identity field length");
        byte[] bytes = new byte[length];
        buffer.readBytes(bytes);
        for (byte value : bytes) if (value < 0) throw new IllegalArgumentException("Identity must be ASCII");
        return new String(bytes, StandardCharsets.US_ASCII);
    }
}
