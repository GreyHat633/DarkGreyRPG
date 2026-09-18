package darkgrey.rpg.session.forge;

import net.minecraft.entity.player.EntityPlayerMP;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.network.MainThreadScheduler;
import darkgrey.rpg.network.message.canonical.CanonicalSessionAction;

/** Schedules canonical Session actions onto the server thread. */
public final class CanonicalSessionActionHandler implements IMessageHandler<CanonicalSessionAction, IMessage> {

    @Override
    public IMessage onMessage(final CanonicalSessionAction message, final MessageContext context) {
        final EntityPlayerMP player = context == null || context.getServerHandler() == null ? null
            : context.getServerHandler().playerEntity;
        MainThreadScheduler.scheduleServer(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionForgeManager manager = DarkGreyRpg.getCanonicalSessionManager();
                if (manager != null && manager.handleAction(player, message)
                    && message.getKind() == CanonicalSessionAction.Kind.CHOICE)
                    darkgrey.rpg.network.DialogueNetwork.CHANNEL
                        .sendTo(new darkgrey.rpg.network.message.canonical.CanonicalChoiceReceipt(message), player);
            }
        });
        return null;
    }
}
