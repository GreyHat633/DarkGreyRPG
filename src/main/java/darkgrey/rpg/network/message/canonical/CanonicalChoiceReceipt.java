package darkgrey.rpg.network.message.canonical;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import io.netty.buffer.ByteBuf;

/** Read-only acknowledgement emitted only after the server accepts a choice. */
public final class CanonicalChoiceReceipt implements IMessage {

    private CanonicalSessionAction action;

    public CanonicalChoiceReceipt() {}

    public CanonicalChoiceReceipt(CanonicalSessionAction action) {
        this.action = action;
    }

    @Override
    public void toBytes(ByteBuf buffer) {
        action.toBytes(buffer);
    }

    @Override
    public void fromBytes(ByteBuf buffer) {
        action = new CanonicalSessionAction();
        action.fromBytes(buffer);
        if (action.getKind() != CanonicalSessionAction.Kind.CHOICE)
            throw new IllegalArgumentException("Choice receipt required");
    }

    public static final class Handler implements IMessageHandler<CanonicalChoiceReceipt, IMessage> {

        @Override
        public IMessage onMessage(final CanonicalChoiceReceipt message, MessageContext context) {
            final Object connection = context.netHandler;
            darkgrey.rpg.network.MainThreadScheduler.scheduleClient(new Runnable() {

                @Override
                public void run() {
                    if (darkgrey.rpg.DarkGreyRpg.proxy.isCurrentClientConnection(connection))
                        darkgrey.rpg.client.session.DialogueHistoryClient.acceptChoice(message.action);
                }
            });
            return null;
        }
    }
}
