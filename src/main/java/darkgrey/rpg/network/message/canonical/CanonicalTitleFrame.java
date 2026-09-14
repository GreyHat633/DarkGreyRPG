package darkgrey.rpg.network.message.canonical;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.story.canonical.runtime.CanonicalTitleConfiguration;
import io.netty.buffer.ByteBuf;

public final class CanonicalTitleFrame implements IMessage {

    private long token;
    private CanonicalTitleConfiguration title;

    public CanonicalTitleFrame() {}

    public CanonicalTitleFrame(long token, CanonicalTitleConfiguration title) {
        if (token <= 0) throw new IllegalArgumentException("Invalid title token");
        this.token = token;
        this.title = title;
    }

    public long getToken() {
        return token;
    }

    public CanonicalTitleConfiguration getTitle() {
        return title;
    }

    @Override
    public void toBytes(ByteBuf buffer) {
        if (token <= 0) throw new IllegalArgumentException("Invalid title token");
        buffer.writeLong(token);
        buffer.writeBoolean(title != null);
        if (title != null) {
            CanonicalSessionNetworkCodec.writeField(buffer, title.main, "main", 4096);
            CanonicalSessionNetworkCodec.writeOptionalField(buffer, title.subtitle, "subtitle", 4096);
            buffer.writeDouble(title.fadeIn);
            buffer.writeDouble(title.stay);
            buffer.writeDouble(title.fadeOut);
        }
    }

    @Override
    public void fromBytes(ByteBuf buffer) {
        if (buffer.readableBytes() < 9) throw new IllegalArgumentException("Truncated title");
        long id = buffer.readLong();
        int present = buffer.readUnsignedByte();
        if (id <= 0 || present > 1) throw new IllegalArgumentException("Invalid title header");
        CanonicalTitleConfiguration value = null;
        if (present == 1) {
            String main = CanonicalSessionNetworkCodec.readField(buffer, "main", 4096);
            String sub = CanonicalSessionNetworkCodec.readOptionalField(buffer, "subtitle", 4096);
            if (buffer.readableBytes() != 24) throw new IllegalArgumentException("Invalid title timings payload");
            value = new CanonicalTitleConfiguration(
                main,
                sub,
                buffer.readDouble(),
                buffer.readDouble(),
                buffer.readDouble());
        }
        CanonicalSessionNetworkCodec.requireNoTrailingBytes(buffer);
        token = id;
        title = value;
    }

    public static final class Handler implements IMessageHandler<CanonicalTitleFrame, IMessage> {

        @Override
        public IMessage onMessage(final CanonicalTitleFrame message, MessageContext context) {
            final Object connection = context.netHandler;
            darkgrey.rpg.network.MainThreadScheduler.scheduleClient(new Runnable() {

                @Override
                public void run() {
                    if (darkgrey.rpg.DarkGreyRpg.proxy.isCurrentClientConnection(connection))
                        darkgrey.rpg.title.CanonicalTitleClient.accept(message);
                }
            });
            return null;
        }
    }
}
