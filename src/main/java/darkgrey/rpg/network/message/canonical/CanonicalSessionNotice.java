package darkgrey.rpg.network.message.canonical;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.network.MainThreadScheduler;
import io.netty.buffer.ByteBuf;

/** Independent non-story failure feedback, scoped to a live transport. */
public final class CanonicalSessionNotice implements IMessage {

    private long transport;
    private String story;

    public CanonicalSessionNotice() {}

    public CanonicalSessionNotice(long transport, String story) {
        this.transport = transport;
        this.story = story;
    }

    public void toBytes(ByteBuf out) {
        CanonicalSessionNetworkCodec.requirePositive(transport, "transport");
        out.writeLong(transport);
        CanonicalSessionNetworkCodec.writeField(out, story, "story", 96);
    }

    public void fromBytes(ByteBuf in) {
        transport = in.readLong();
        CanonicalSessionNetworkCodec.requirePositive(transport, "transport");
        story = CanonicalSessionNetworkCodec.readField(in, "story", 96);
        CanonicalSessionNetworkCodec.requireNoTrailingBytes(in);
    }

    public static final class Handler implements IMessageHandler<CanonicalSessionNotice, IMessage> {

        public IMessage onMessage(final CanonicalSessionNotice notice, MessageContext context) {
            final Object connection = context.netHandler;
            MainThreadScheduler.scheduleClient(connection, new Runnable() {

                public void run() {
                    if (!darkgrey.rpg.DarkGreyRpg.proxy.isCurrentClientConnection(connection)) return;
                    darkgrey.rpg.client.session.CanonicalSessionClientController.notice(notice.transport, notice.story);
                }
            }, () -> darkgrey.rpg.network.MainThreadScheduler.rejectClient(connection));
            return null;
        }
    }
}
