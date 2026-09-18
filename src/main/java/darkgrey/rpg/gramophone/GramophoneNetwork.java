package darkgrey.rpg.gramophone;

import net.minecraft.entity.player.EntityPlayerMP;

import cpw.mods.fml.common.network.NetworkRegistry;
import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import cpw.mods.fml.common.network.simpleimpl.SimpleNetworkWrapper;
import cpw.mods.fml.relauncher.Side;
import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.network.MainThreadScheduler;

public final class GramophoneNetwork {

    private static final java.util.concurrent.atomic.AtomicInteger PENDING = new java.util.concurrent.atomic.AtomicInteger();
    public static final SimpleNetworkWrapper CHANNEL = NetworkRegistry.INSTANCE.newSimpleChannel("dgr_gramophone");

    public static void register() {
        CHANNEL.registerMessage(Server.class, GramophonePacket.class, 0, Side.SERVER);
        CHANNEL.registerMessage(Client.class, GramophonePacket.class, 1, Side.CLIENT);
        CHANNEL.registerMessage(MediaServer.class, GramophoneMediaPacket.class, 2, Side.SERVER);
        CHANNEL.registerMessage(MediaClient.class, GramophoneMediaPacket.class, 3, Side.CLIENT);
    }

    public static final class Server implements IMessageHandler<GramophonePacket, IMessage> {

        @Override
        public IMessage onMessage(final GramophonePacket packet, MessageContext context) {
            if (packet.operation != GramophonePacket.SAVE && packet.operation != GramophonePacket.DELETE) return null;
            if (PENDING.incrementAndGet() > 128) {
                PENDING.decrementAndGet();
                return null;
            }
            final EntityPlayerMP player = context.getServerHandler().playerEntity;
            MainThreadScheduler.scheduleServer(() -> {
                try {
                    if (!player.playerNetServerHandler.netManager.isChannelOpen()) return;
                    GramophoneServer.save(player, packet);
                } finally {
                    PENDING.decrementAndGet();
                }
            });
            return null;
        }
    }

    public static final class Client implements IMessageHandler<GramophonePacket, IMessage> {

        @Override
        public IMessage onMessage(final GramophonePacket packet, MessageContext context) {
            final Object connection = context.getClientHandler();
            MainThreadScheduler.scheduleClient(
                () -> {
                    if (DarkGreyRpg.proxy.isCurrentClientConnection(connection))
                        DarkGreyRpg.proxy.acceptGramophone(packet);
                });
            return null;
        }
    }

    public static final class MediaServer implements IMessageHandler<GramophoneMediaPacket, IMessage> {

        @Override
        public IMessage onMessage(final GramophoneMediaPacket packet, MessageContext context) {
            if (packet.operation < 0 || packet.operation > GramophoneMediaPacket.FETCH) return null;
            if (PENDING.incrementAndGet() > 128) {
                PENDING.decrementAndGet();
                return null;
            }
            EntityPlayerMP player = context.getServerHandler().playerEntity;
            MainThreadScheduler.scheduleServer(() -> {
                try {
                    GramophoneLocalServer.accept(player, packet);
                } finally {
                    PENDING.decrementAndGet();
                }
            });
            return null;
        }
    }

    public static final class MediaClient implements IMessageHandler<GramophoneMediaPacket, IMessage> {

        @Override
        public IMessage onMessage(final GramophoneMediaPacket packet, MessageContext context) {
            Object connection = context.getClientHandler();
            MainThreadScheduler.scheduleClient(
                () -> {
                    if (DarkGreyRpg.proxy.isCurrentClientConnection(connection))
                        DarkGreyRpg.proxy.acceptGramophoneMedia(packet);
                });
            return null;
        }
    }
}
