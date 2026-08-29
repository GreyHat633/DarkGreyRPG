package darkgrey.rpg.network.message.nominator;

import net.minecraft.entity.player.EntityPlayerMP;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.content.ModItems;
import darkgrey.rpg.network.MainThreadScheduler;
import darkgrey.rpg.nominator.NominatorPermission;

/** Empty client request; the server supplies the inventory/catalog snapshot. */
public final class C2SNominatorInventoryOpen implements IMessage {

    @Override
    public void fromBytes(io.netty.buffer.ByteBuf buffer) {
        if (buffer.isReadable()) throw new IllegalArgumentException("Trailing nominator open data.");
    }

    @Override
    public void toBytes(io.netty.buffer.ByteBuf buffer) {}

    public static final class Handler implements IMessageHandler<C2SNominatorInventoryOpen, IMessage> {

        @Override
        public IMessage onMessage(final C2SNominatorInventoryOpen message, MessageContext context) {
            final EntityPlayerMP player = context.getServerHandler().playerEntity;
            MainThreadScheduler.scheduleServer(new Runnable() {

                @Override
                public void run() {
                    if (!NominatorPermission.canUse(player) || player.getHeldItem() == null
                        || player.getHeldItem()
                            .getItem() != ModItems.nominator)
                        return;
                    darkgrey.rpg.network.DialogueNetwork.CHANNEL.sendTo(S2CNominatorInventoryOpen.from(player), player);
                }
            });
            return null;
        }
    }
}
