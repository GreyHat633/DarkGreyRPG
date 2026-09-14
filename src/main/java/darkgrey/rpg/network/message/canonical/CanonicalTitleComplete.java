package darkgrey.rpg.network.message.canonical;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import io.netty.buffer.ByteBuf;

public final class CanonicalTitleComplete implements IMessage {

    private long token;

    public CanonicalTitleComplete() {}

    public CanonicalTitleComplete(long token) {
        if (token <= 0) throw new IllegalArgumentException("Invalid title token");
        this.token = token;
    }

    public long getToken() {
        return token;
    }

    @Override
    public void toBytes(ByteBuf buffer) {
        if (token <= 0) throw new IllegalArgumentException("Invalid title token");
        buffer.writeLong(token);
    }

    @Override
    public void fromBytes(ByteBuf buffer) {
        if (buffer.readableBytes() != 8) throw new IllegalArgumentException("Invalid title acknowledgement");
        token = buffer.readLong();
        if (token <= 0) throw new IllegalArgumentException("Invalid title token");
    }

    public static final class Handler implements IMessageHandler<CanonicalTitleComplete, IMessage> {

        @Override
        public IMessage onMessage(final CanonicalTitleComplete message, MessageContext context) {
            final net.minecraft.entity.player.EntityPlayerMP player = context.getServerHandler().playerEntity;
            darkgrey.rpg.title.CanonicalTitleServer.acknowledge(player, message.getToken());
            return null;
        }
    }
}
