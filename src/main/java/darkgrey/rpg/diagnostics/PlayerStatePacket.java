package darkgrey.rpg.diagnostics;

import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.nbt.CompressedStreamTools;
import net.minecraft.nbt.NBTSizeTracker;
import net.minecraft.nbt.NBTTagCompound;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.MainThreadScheduler;
import io.netty.buffer.ByteBuf;

public final class PlayerStatePacket implements IMessage {

    public int kind;
    public long request;
    public NBTTagCompound data;

    public PlayerStatePacket() {}

    public PlayerStatePacket(int kind, long request, NBTTagCompound data) {
        this.kind = kind;
        this.request = request;
        this.data = (NBTTagCompound) data.copy();
    }

    @Override
    public void fromBytes(ByteBuf buffer) {
        if (buffer.readableBytes() < 13 || buffer.readableBytes() > 262157)
            throw new IllegalArgumentException("Invalid inspection packet size");
        kind = buffer.readUnsignedByte();
        request = buffer.readLong();
        int length = buffer.readInt();
        if (kind > 2 || length <= 0 || length != buffer.readableBytes() || kind == 1 && length > 256)
            throw new IllegalArgumentException("Invalid inspection packet");
        byte[] bytes = new byte[length];
        buffer.readBytes(bytes);
        try {
            data = CompressedStreamTools.func_152457_a(bytes, new NBTSizeTracker(kind == 1 ? 2048 : 1048576));
        } catch (java.io.IOException invalid) {
            throw new IllegalArgumentException("Invalid inspection data", invalid);
        }
    }

    @Override
    public void toBytes(ByteBuf buffer) {
        try {
            byte[] bytes = CompressedStreamTools.compress(data);
            if (bytes.length > 262144) throw new IllegalArgumentException("Inspection response too large");
            buffer.writeByte(kind);
            buffer.writeLong(request);
            buffer.writeInt(bytes.length);
            buffer.writeBytes(bytes);
        } catch (java.io.IOException invalid) {
            throw new IllegalArgumentException(invalid);
        }
    }

    public static final class Server implements IMessageHandler<PlayerStatePacket, IMessage> {

        private static final java.util.Map<EntityPlayerMP, Long> LAST = java.util.Collections
            .synchronizedMap(new java.util.WeakHashMap<EntityPlayerMP, Long>());

        @Override
        public IMessage onMessage(final PlayerStatePacket message, MessageContext context) {
            final EntityPlayerMP player = context.getServerHandler().playerEntity;
            if (message.kind != 1) return null;
            synchronized (LAST) {
                long now = System.nanoTime();
                Long previous = LAST.get(player);
                if (previous != null && now - previous.longValue() < 150000000L) return null;
                LAST.put(player, Long.valueOf(now));
            }
            MainThreadScheduler.scheduleServer(new Runnable() {

                @Override
                public void run() {
                    if (!player.canCommandSenderUseCommand(2, "dgr")) {
                        NBTTagCompound denied = new NBTTagCompound();
                        denied.setString("error", "没有管理员查询权限。");
                        DialogueNetwork.CHANNEL.sendTo(new PlayerStatePacket(2, message.request, denied), player);
                        return;
                    }
                    NBTTagCompound response;
                    try {
                        response = PlayerStateInspection
                            .query(message.data.getString("name"), message.data.getInteger("page"));
                    } catch (RuntimeException failure) {
                        response = new NBTTagCompound();
                        response.setString("error", "读取失败；未修改玩家状态。");
                    }
                    DialogueNetwork.CHANNEL.sendTo(new PlayerStatePacket(2, message.request, response), player);
                }
            });
            return null;
        }
    }

    public static final class Client implements IMessageHandler<PlayerStatePacket, IMessage> {

        @Override
        public IMessage onMessage(final PlayerStatePacket message, MessageContext context) {
            final Object connection = context.netHandler;
            if (message.kind == 1) return null;
            MainThreadScheduler.scheduleClient(new Runnable() {

                @Override
                public void run() {
                    if (DarkGreyRpg.proxy.isCurrentClientConnection(connection))
                        DarkGreyRpg.proxy.acceptPlayerInspection(message.kind, message.request, message.data);
                }
            });
            return null;
        }
    }
}
