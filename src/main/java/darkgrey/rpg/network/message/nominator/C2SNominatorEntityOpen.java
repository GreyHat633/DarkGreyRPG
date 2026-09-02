package darkgrey.rpg.network.message.nominator;

import net.minecraft.entity.Entity;
import net.minecraft.entity.player.EntityPlayerMP;

import cpw.mods.fml.common.network.simpleimpl.IMessage;
import cpw.mods.fml.common.network.simpleimpl.IMessageHandler;
import cpw.mods.fml.common.network.simpleimpl.MessageContext;
import darkgrey.rpg.content.ModItems;
import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.MainThreadScheduler;
import darkgrey.rpg.nominator.NominatorPermission;
import darkgrey.rpg.nominator.NominatorSavedData;
import io.netty.buffer.ByteBuf;

/** Client request for a server-authoritative entity nominator snapshot. */
public final class C2SNominatorEntityOpen implements IMessage {

    private int entityId;

    public C2SNominatorEntityOpen() {}

    public C2SNominatorEntityOpen(int entityId) {
        this.entityId = entityId;
    }

    @Override
    public void fromBytes(ByteBuf buffer) {
        entityId = buffer.readInt();
        if (buffer.isReadable()) throw new IllegalArgumentException("Trailing nominator open data.");
    }

    @Override
    public void toBytes(ByteBuf buffer) {
        buffer.writeInt(entityId);
    }

    public static final class Handler implements IMessageHandler<C2SNominatorEntityOpen, IMessage> {

        @Override
        public IMessage onMessage(final C2SNominatorEntityOpen message, MessageContext context) {
            final EntityPlayerMP player = context.getServerHandler().playerEntity;
            MainThreadScheduler.scheduleServer(new Runnable() {

                @Override
                public void run() {
                    Entity entity = player.worldObj.getEntityByID(message.entityId);
                    if (!NominatorPermission.canUse(player) || player.getHeldItem() == null
                        || player.getHeldItem()
                            .getItem() != ModItems.nominator
                        || entity == null
                        || entity.dimension != player.dimension
                        || player.getDistanceSqToEntity(entity) > 64.0D) return;
                    DialogueNetwork.CHANNEL.sendTo(
                        S2CNominatorEntityOpen.from(
                            entity,
                            NominatorSavedData.get(),
                            darkgrey.rpg.nominator.NominatorCatalog.from(
                                darkgrey.rpg.DarkGreyRpg.getProjectRepository()
                                    .getSnapshot(),
                                darkgrey.rpg.DarkGreyRpg.getStoryPackageLoader()
                                    .getPackages())),
                        player);
                }
            });
            return null;
        }
    }
}
