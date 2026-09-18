package darkgrey.rpg.gramophone;

import java.nio.charset.StandardCharsets;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import io.netty.buffer.ByteBuf;
import io.netty.buffer.Unpooled;

/** Stop-and-wait transfer protocol. A chunk can never grow into a whole-song packet. */
public final class GramophoneMediaPacket implements IMessage {

    public static final int UPLOAD = 0, CHUNK = 1, CANCEL = 2, FETCH = 3, ACK = 4, DATA = 5, ERROR = 6, COMMITTED = 7;
    public static final int CHUNK_BYTES = 16384;
    public int operation, offset, total;
    public String token = "", hash = "", message = "";
    public byte[] data = new byte[0];
    public GramophonePacket device = new GramophonePacket();

    @Override
    public void toBytes(ByteBuf out) {
        out.writeByte(operation)
            .writeInt(offset)
            .writeInt(total);
        text(out, token, 64);
        text(out, hash, 64);
        text(out, message, 1024);
        ByteBuf config = Unpooled.buffer();
        try {
            device.toBytes(config);
            out.writeInt(config.readableBytes());
            out.writeBytes(config);
        } finally {
            config.release();
        }
        if (data.length > CHUNK_BYTES) throw new IllegalArgumentException("Media chunk too large");
        out.writeInt(data.length)
            .writeBytes(data);
    }

    @Override
    public void fromBytes(ByteBuf in) {
        if (in.readableBytes() > CHUNK_BYTES + 5400) throw new IllegalArgumentException("Media packet too large");
        operation = in.readUnsignedByte();
        offset = in.readInt();
        total = in.readInt();
        token = text(in, 64);
        hash = text(in, 64);
        message = text(in, 1024);
        int size = in.readInt();
        if (size < 0 || size > 4096 || size > in.readableBytes())
            throw new IllegalArgumentException("Invalid device length");
        device.fromBytes(in.readSlice(size));
        size = in.readInt();
        if (size < 0 || size > CHUNK_BYTES || size != in.readableBytes())
            throw new IllegalArgumentException("Invalid chunk length");
        data = new byte[size];
        in.readBytes(data);
        if (total < 0 || total > OnlineMusicResolver.MAX_BYTES
            || offset < 0
            || offset > total
            || !token.matches("[0-9a-f-]{36}")
            || !hash.matches("[0-9a-f]{64}")) throw new IllegalArgumentException("Invalid media transfer");
    }

    private static void text(ByteBuf out, String value, int limit) {
        byte[] bytes = value.getBytes(StandardCharsets.UTF_8);
        if (bytes.length > limit) throw new IllegalArgumentException("Media text too large");
        out.writeShort(bytes.length)
            .writeBytes(bytes);
    }

    private static String text(ByteBuf in, int limit) {
        int size = in.readUnsignedShort();
        if (size > limit || size > in.readableBytes()) throw new IllegalArgumentException("Invalid media text");
        byte[] bytes = new byte[size];
        in.readBytes(bytes);
        return new String(bytes, StandardCharsets.UTF_8);
    }
}
