package darkgrey.rpg.network.message.canonical;

import net.minecraft.entity.player.EntityPlayerMP;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.network.MainThreadScheduler;
import io.netty.buffer.ByteBuf;

/** Client response containing only the server-issued Task choice token/index. */
public final class CanonicalTaskSubmitChoiceSelection implements IMessage {

    private long token;
    private int optionIndex;

    public CanonicalTaskSubmitChoiceSelection() {}

    public CanonicalTaskSubmitChoiceSelection(long token, int optionIndex) {
        validate(token, optionIndex);
        this.token = token;
        this.optionIndex = optionIndex;
    }

    @Override
    public void fromBytes(ByteBuf buffer) {
        if (buffer.readableBytes() < 12) throw invalid("truncated choice selection");
        long decodedToken = buffer.readLong();
        int decodedIndex = buffer.readInt();
        if (buffer.isReadable()) throw invalid("trailing bytes");
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

    public static final class Handler implements IMessageHandler<CanonicalTaskSubmitChoiceSelection, IMessage> {

        @Override
        public IMessage onMessage(final CanonicalTaskSubmitChoiceSelection message, final MessageContext context) {
            final EntityPlayerMP player = context.getServerHandler().playerEntity;
            MainThreadScheduler.scheduleServer(new Runnable() {

                @Override
                public void run() {
                    DarkGreyRpg.getCanonicalTaskManager()
                        .selectSubmitCandidate(player, message.token, message.optionIndex);
                }
            });
            return null;
        }
    }

    private static void validate(long token, int optionIndex) {
        if (token <= 0 || optionIndex < -1 || optionIndex > 255) throw invalid("token or option index is invalid");
    }

    private static IllegalArgumentException invalid(String message) {
        return new IllegalArgumentException("Invalid Task submit choice: " + message);
    }
}
