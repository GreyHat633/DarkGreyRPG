package darkgrey.rpg.network.message.canonical;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.graph.canonical.CanonicalMediaReference;
import io.netty.buffer.ByteBuf;

/** Fixed-size payload envelope; total zero is a bounded unavailable response. */
public final class CanonicalMediaChunk implements IMessage {

    public static final int CHUNK_BYTES = 32768;
    public static final int MAX_MEDIA_BYTES = 64 * 1024 * 1024;
    private long requestId;
    private String mediaRef;
    private int total;
    private int offset;
    private byte[] data;

    public CanonicalMediaChunk() {}

    public CanonicalMediaChunk(long requestId, String mediaRef, int total, int offset, byte[] data) {
        validate(requestId, mediaRef, total, offset, data.length);
        this.requestId = requestId;
        this.mediaRef = mediaRef;
        this.total = total;
        this.offset = offset;
        this.data = data.clone();
    }

    private static void validate(long id, String ref, int total, int offset, int count) {
        if (id <= 0 || !CanonicalMediaReference.isValid(ref)
            || total < 0
            || total > MAX_MEDIA_BYTES
            || offset < 0
            || offset % CHUNK_BYTES != 0
            || count < 0
            || count > CHUNK_BYTES) throw new IllegalArgumentException("Invalid media chunk");
        if (total == 0 ? offset != 0 || count != 0 : offset >= total || count != Math.min(CHUNK_BYTES, total - offset))
            throw new IllegalArgumentException("Incoherent media chunk bounds");
    }

    public long getRequestId() {
        return requestId;
    }

    public String getMediaRef() {
        return mediaRef;
    }

    public int getTotal() {
        return total;
    }

    public int getOffset() {
        return offset;
    }

    public byte[] getData() {
        return data.clone();
    }

    @Override
    public void toBytes(ByteBuf buffer) {
        validate(requestId, mediaRef, total, offset, data.length);
        buffer.writeLong(requestId);
        CanonicalSessionNetworkCodec.writeField(buffer, mediaRef, "media_ref", 80);
        buffer.writeInt(total);
        buffer.writeInt(offset);
        buffer.writeInt(data.length);
        buffer.writeBytes(data);
    }

    @Override
    public void fromBytes(ByteBuf buffer) {
        if (buffer.readableBytes() < 8) throw new IllegalArgumentException("Truncated media chunk");
        long id = buffer.readLong();
        String ref = CanonicalSessionNetworkCodec.readField(buffer, "media_ref", 80);
        if (buffer.readableBytes() < 12) throw new IllegalArgumentException("Truncated chunk bounds");
        int size = buffer.readInt();
        int start = buffer.readInt();
        int count = buffer.readInt();
        validate(id, ref, size, start, count);
        if (buffer.readableBytes() != count) throw new IllegalArgumentException("Invalid chunk payload length");
        byte[] payload = new byte[count];
        buffer.readBytes(payload);
        requestId = id;
        mediaRef = ref;
        total = size;
        offset = start;
        data = payload;
    }

    public static final class Handler implements IMessageHandler<CanonicalMediaChunk, IMessage> {

        @Override
        public IMessage onMessage(final CanonicalMediaChunk message, MessageContext context) {
            final Object connection = context.netHandler;
            darkgrey.rpg.network.MainThreadScheduler.scheduleClient(new Runnable() {

                @Override
                public void run() {
                    if (darkgrey.rpg.DarkGreyRpg.proxy.isCurrentClientConnection(connection))
                        darkgrey.rpg.media.CanonicalMediaClient.accept(message);
                }
            });
            return null;
        }
    }
}
