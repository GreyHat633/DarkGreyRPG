package darkgrey.rpg.creator;

import java.util.Map;
import java.util.WeakHashMap;

import net.minecraft.entity.player.EntityPlayerMP;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.network.MainThreadScheduler;
import io.netty.buffer.ByteBuf;

public final class CanonicalTaskUiRequest implements IMessage {

    public int request;

    public CanonicalTaskUiRequest() {}

    public CanonicalTaskUiRequest(int request) {
        this.request = request;
    }

    @Override
    public void fromBytes(ByteBuf b) {
        if (b.readableBytes() != 4) throw new IllegalArgumentException("Invalid task UI request");
        request = b.readInt();
    }

    @Override
    public void toBytes(ByteBuf b) {
        b.writeInt(request);
    }

    public static final class Handler implements IMessageHandler<CanonicalTaskUiRequest, IMessage> {

        private static final Map<EntityPlayerMP, Long> LAST = new WeakHashMap<EntityPlayerMP, Long>();

        @Override
        public IMessage onMessage(final CanonicalTaskUiRequest message, MessageContext context) {
            final EntityPlayerMP player = context.getServerHandler().playerEntity;
            MainThreadScheduler.scheduleServer(new Runnable() {

                @Override
                public void run() {
                    long now = System.nanoTime();
                    Long last = LAST.get(player);
                    if (last != null && now - last < 250000000L) return;
                    LAST.put(player, now);
                    if (player.playerNetServerHandler == null || player.isDead) return;
                    CanonicalTaskPresentationServer.push(player, true);
                }
            });
            return null;
        }
    }
}
