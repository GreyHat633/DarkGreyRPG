package darkgrey.rpg.network.message.canonical;

import net.minecraft.entity.player.EntityPlayerMP;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.network.MainThreadScheduler;
import io.netty.buffer.ByteBuf;

/** Client response containing only the server-issued chooser token and index. */
public final class CanonicalStoryChooserSelection implements IMessage {

    private long token;
    private int optionIndex;

    public CanonicalStoryChooserSelection() {}

    public CanonicalStoryChooserSelection(long token, int optionIndex) {
        validate(token, optionIndex);
        this.token = token;
        this.optionIndex = optionIndex;
    }

    @Override
    public void fromBytes(ByteBuf buffer) {
        if (buffer.readableBytes() < 12) throw invalid("truncated chooser selection");
        long decodedToken = buffer.readLong();
        int decodedIndex = buffer.readInt();
        if (buffer.isReadable()) throw invalid("trailing unread bytes");
        validate(decodedToken, decodedIndex);
        token = decodedToken;
        optionIndex = decodedIndex;
    }

    @Override
    public void toBytes(ByteBuf buffer) {
        validate(token, optionIndex);
        buffer.writeLong(token);
        buffer.writeInt(optionIndex);
    }

    public long getToken() {
        return token;
    }

    public int getOptionIndex() {
        return optionIndex;
    }

    /** Marshals the indexed candidate operation to the server main thread. */
    public static final class Handler implements IMessageHandler<CanonicalStoryChooserSelection, IMessage> {

        @Override
        public IMessage onMessage(final CanonicalStoryChooserSelection message, final MessageContext context) {
            final EntityPlayerMP player = context.getServerHandler().playerEntity;
            MainThreadScheduler.scheduleServer(new Runnable() {

                @Override
                public void run() {
                    DarkGreyRpg.getCanonicalStoryManager()
                        .selectActorCandidate(player, message.getToken(), message.getOptionIndex());
                }
            });
            return null;
        }
    }

    private static void validate(long value, int index) {
        if (value <= 0) throw invalid("token must be positive");
        if (index < -1 || index > 255) throw invalid("option_index must be -1 through 255");
    }

    private static IllegalArgumentException invalid(String message) {
        return new IllegalArgumentException("Invalid canonical Story chooser payload: " + message);
    }
}
