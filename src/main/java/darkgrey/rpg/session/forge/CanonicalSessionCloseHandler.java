package darkgrey.rpg.session.forge;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.client.session.CanonicalSessionClientController;
import darkgrey.rpg.network.MainThreadScheduler;
import darkgrey.rpg.network.message.canonical.CanonicalSessionClose;

/** Marshals canonical Session close envelopes onto the client thread. */
public final class CanonicalSessionCloseHandler implements IMessageHandler<CanonicalSessionClose, IMessage> {

    @Override
    public IMessage onMessage(final CanonicalSessionClose message, MessageContext context) {
        MainThreadScheduler.scheduleClient(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionClientController.acceptClose(message);
            }
        });
        return null;
    }
}
