package darkgrey.rpg.creator;

import net.minecraft.nbt.CompressedStreamTools;
import net.minecraft.nbt.NBTSizeTracker;
import net.minecraft.nbt.NBTTagCompound;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.network.MainThreadScheduler;
import io.netty.buffer.ByteBuf;

/** Bounded presentation-only snapshot. Kinds: Inspect=0, Canonical Tasks=1. */
public final class CreatorSnapshot implements IMessage {

    public int kind;
    public NBTTagCompound data;

    public CreatorSnapshot() {}

    public CreatorSnapshot(int kind, NBTTagCompound data) {
        this.kind = kind;
        this.data = (NBTTagCompound) data.copy();
    }

    @Override
    public void fromBytes(ByteBuf b) {
        if (b.readableBytes() < 5 || b.readableBytes() > 1048581)
            throw new IllegalArgumentException("Invalid creator snapshot size");
        kind = b.readUnsignedByte();
        if (kind > 1) throw new IllegalArgumentException("Invalid creator snapshot kind");
        int length = b.readInt();
        if (length <= 0 || length > 1048576 || length != b.readableBytes())
            throw new IllegalArgumentException("Invalid creator snapshot length");
        byte[] bytes = new byte[length];
        b.readBytes(bytes);
        try {
            data = CompressedStreamTools.func_152457_a(bytes, new NBTSizeTracker(2097152L));
        } catch (java.io.IOException e) {
            throw new IllegalArgumentException("Invalid creator NBT", e);
        }
        if (data == null || b.isReadable()) throw new IllegalArgumentException("Invalid creator snapshot payload");
    }

    @Override
    public void toBytes(ByteBuf b) {
        if (kind < 0 || kind > 1 || data == null) throw new IllegalArgumentException("Invalid creator snapshot");
        try {
            byte[] bytes = CompressedStreamTools.compress(data);
            if (bytes.length > 1048576) throw new IllegalArgumentException("Creator snapshot too large");
            CompressedStreamTools.func_152457_a(bytes, new NBTSizeTracker(2097152L));
            b.writeByte(kind);
            b.writeInt(bytes.length);
            b.writeBytes(bytes);
        } catch (java.io.IOException e) {
            throw new IllegalArgumentException("Cannot encode creator NBT", e);
        }
    }

    public static final class Handler implements IMessageHandler<CreatorSnapshot, IMessage> {

        @Override
        public IMessage onMessage(final CreatorSnapshot message, MessageContext context) {
            MainThreadScheduler.scheduleClient(new Runnable() {

                @Override
                public void run() {
                    DarkGreyRpg.proxy.acceptCreatorSnapshot(message.kind, message.data);
                }
            });
            return null;
        }
    }
}
