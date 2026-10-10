package darkgrey.rpg.network.message.canonical;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.network.MainThreadScheduler;
import io.netty.buffer.ByteBuf;

/** Opens the existing read-only Task view after the server has pushed its current cache. */
public final class CanonicalTaskViewOpen implements IMessage {

    public static final int PROTOCOL_MARKER = 0x44475240;
    private int dimension;

    public CanonicalTaskViewOpen() {}

    public CanonicalTaskViewOpen(int dimension) {
        this.dimension = dimension;
    }

    public int getDimension() {
        return dimension;
    }

    @Override
    public void toBytes(ByteBuf buffer) {
        buffer.writeInt(PROTOCOL_MARKER);
        buffer.writeInt(dimension);
    }

    @Override
    public void fromBytes(ByteBuf buffer) {
        if (buffer.readableBytes() != 8 || buffer.readInt() != PROTOCOL_MARKER)
            throw new IllegalArgumentException("Invalid current Task view message");
        dimension = buffer.readInt();
    }

    public static final class Handler implements IMessageHandler<CanonicalTaskViewOpen, IMessage> {

        @Override
        public IMessage onMessage(final CanonicalTaskViewOpen message, MessageContext context) {
            final Object connection = context.netHandler;
            MainThreadScheduler.scheduleClient(connection, () -> {
                if (DarkGreyRpg.proxy.isCurrentClientConnection(connection))
                    DarkGreyRpg.proxy.openCanonicalTaskView(message.dimension);
            }, () -> darkgrey.rpg.network.MainThreadScheduler.rejectClient(connection));
            return null;
        }
    }
}
