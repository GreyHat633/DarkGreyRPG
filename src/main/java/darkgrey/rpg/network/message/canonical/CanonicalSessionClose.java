package darkgrey.rpg.network.message.canonical;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import io.netty.buffer.ByteBuf;

/** Author-facing close envelope; canonical End port and Logic outputs stay server-side. */
public final class CanonicalSessionClose implements IMessage {

    private long transportId;
    private String storyId;

    public CanonicalSessionClose() {}

    public CanonicalSessionClose(long transportId, String storyId) {
        validate(transportId, storyId);
        this.transportId = transportId;
        this.storyId = storyId;
    }

    @Override
    public void fromBytes(ByteBuf buffer) {
        if (buffer.readableBytes() < 8) throw CanonicalSessionNetworkCodec.invalid("truncated transport_id");
        long decodedTransportId = buffer.readLong();
        String decodedStoryId = CanonicalSessionNetworkCodec
            .readField(buffer, "story_id", CanonicalSessionNetworkCodec.MAX_ID_BYTES);
        CanonicalSessionNetworkCodec.requireNoTrailingBytes(buffer);
        validate(decodedTransportId, decodedStoryId);
        transportId = decodedTransportId;
        storyId = decodedStoryId;
    }

    @Override
    public void toBytes(ByteBuf buffer) {
        validate(transportId, storyId);
        buffer.writeLong(transportId);
        CanonicalSessionNetworkCodec.writeField(buffer, storyId, "story_id", CanonicalSessionNetworkCodec.MAX_ID_BYTES);
    }

    public long getTransportId() {
        return transportId;
    }

    public long getSessionId() {
        return transportId;
    }

    public String getStoryId() {
        return storyId;
    }

    private static void validate(long transportId, String storyId) {
        CanonicalSessionNetworkCodec.requirePositive(transportId, "transport_id");
        CanonicalSessionNetworkCodec.requireField(storyId, "story_id", CanonicalSessionNetworkCodec.MAX_ID_BYTES);
    }
}
