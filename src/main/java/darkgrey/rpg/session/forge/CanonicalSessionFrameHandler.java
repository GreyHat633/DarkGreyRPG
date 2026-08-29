package darkgrey.rpg.session.forge;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.client.session.CanonicalSessionClientController;
import darkgrey.rpg.network.MainThreadScheduler;
import darkgrey.rpg.network.message.canonical.CanonicalSessionFrame;

/** Marshals canonical Session frames onto the client thread. */
public final class CanonicalSessionFrameHandler implements IMessageHandler<CanonicalSessionFrame, IMessage> {

    @Override
    public IMessage onMessage(final CanonicalSessionFrame message, MessageContext context) {
        MainThreadScheduler.scheduleClient(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionClientController.acceptFrame(message);
            }
        });
        return null;
    }
}
