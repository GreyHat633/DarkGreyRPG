package darkgrey.rpg.network.message.canonical;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.graph.canonical.CanonicalMediaReference;
import io.netty.buffer.ByteBuf;

/** Pulls one bounded chunk of media referenced by the player's current presentation. */
public final class CanonicalMediaRequest implements IMessage {

    private long requestId;
    private String mediaRef;
    private int offset;

    public CanonicalMediaRequest() {}

    public CanonicalMediaRequest(long requestId, String mediaRef, int offset) {
        validate(requestId, mediaRef, offset);
        this.requestId = requestId;
        this.mediaRef = mediaRef;
        this.offset = offset;
    }

    private static void validate(long id, String ref, int offset) {
        if (id <= 0 || !CanonicalMediaReference.isValid(ref)
            || offset < 0
            || offset >= CanonicalMediaChunk.MAX_MEDIA_BYTES
            || offset % CanonicalMediaChunk.CHUNK_BYTES != 0)
            throw new IllegalArgumentException("Invalid media chunk request");
    }

    public long getRequestId() {
        return requestId;
    }

    public String getMediaRef() {
        return mediaRef;
    }

    public int getOffset() {
        return offset;
    }

    @Override
    public void toBytes(ByteBuf buffer) {
        validate(requestId, mediaRef, offset);
        buffer.writeLong(requestId);
        CanonicalSessionNetworkCodec.writeField(buffer, mediaRef, "media_ref", 80);
        buffer.writeInt(offset);
    }

    @Override
    public void fromBytes(ByteBuf buffer) {
        if (buffer.readableBytes() < 8) throw new IllegalArgumentException("Truncated media request");
        long id = buffer.readLong();
        String ref = CanonicalSessionNetworkCodec.readField(buffer, "media_ref", 80);
        if (buffer.readableBytes() != 4) throw new IllegalArgumentException("Invalid media request size");
        int start = buffer.readInt();
        validate(id, ref, start);
        requestId = id;
        mediaRef = ref;
        offset = start;
    }

    public static final class Handler implements IMessageHandler<CanonicalMediaRequest, IMessage> {

        @Override
        public IMessage onMessage(final CanonicalMediaRequest message, final MessageContext context) {
            final net.minecraft.entity.player.EntityPlayerMP player = context.getServerHandler().playerEntity;
            darkgrey.rpg.media.CanonicalMediaServer.enqueue(player, message);
            return null;
        }
    }
}
