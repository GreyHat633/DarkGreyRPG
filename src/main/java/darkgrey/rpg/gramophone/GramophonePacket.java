package darkgrey.rpg.gramophone;

import java.nio.charset.StandardCharsets;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import io.netty.buffer.ByteBuf;

public final class GramophonePacket implements IMessage {

    public static final int STATE = 0, BEGIN = 1, END = 2, OPEN = 3, SAVE = 4, DELETE = 5, RESULT = 6;
    public int operation, dimension, x, y, z, radius = 16;
    public long revision;
    public String instance = "", source = "", status = "";
    public boolean enabled = true, redstone, powered;

    @Override
    public void toBytes(ByteBuf buffer) {
        buffer.writeByte(operation)
            .writeInt(dimension)
            .writeInt(x)
            .writeInt(y)
            .writeInt(z)
            .writeLong(revision)
            .writeInt(radius)
            .writeBoolean(enabled)
            .writeBoolean(redstone)
            .writeBoolean(powered);
        text(buffer, instance, 64);
        text(buffer, source, 2048);
        text(buffer, status, 1024);
    }

    @Override
    public void fromBytes(ByteBuf buffer) {
        if (buffer.readableBytes() > 4096) throw new IllegalArgumentException("Gramophone packet too large");
        operation = buffer.readUnsignedByte();
        dimension = buffer.readInt();
        x = buffer.readInt();
        y = buffer.readInt();
        z = buffer.readInt();
        revision = buffer.readLong();
        radius = buffer.readInt();
        enabled = buffer.readBoolean();
        redstone = buffer.readBoolean();
        powered = buffer.readBoolean();
        instance = text(buffer, 64);
        source = text(buffer, 2048);
        status = text(buffer, 1024);
        if (buffer.isReadable()) throw new IllegalArgumentException("Trailing gramophone bytes");
    }

    private static void text(ByteBuf buffer, String value, int limit) {
        byte[] bytes = value.getBytes(StandardCharsets.UTF_8);
        if (bytes.length > limit) throw new IllegalArgumentException("Gramophone field too large");
        buffer.writeShort(bytes.length)
            .writeBytes(bytes);
    }

    private static String text(ByteBuf buffer, int limit) {
        int size = buffer.readUnsignedShort();
        if (size > limit || size > buffer.readableBytes())
            throw new IllegalArgumentException("Invalid gramophone field");
        byte[] bytes = new byte[size];
        buffer.readBytes(bytes);
        return new String(bytes, StandardCharsets.UTF_8);
    }

    public String key() {
        return dimension + ":" + x + ":" + y + ":" + z + ":" + instance;
    }
}
