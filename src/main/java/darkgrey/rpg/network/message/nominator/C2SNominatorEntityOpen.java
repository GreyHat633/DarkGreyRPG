package darkgrey.rpg.network.message.nominator;

import java.util.UUID;

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
    private UUID entityUuid;

    public C2SNominatorEntityOpen() {}

    public C2SNominatorEntityOpen(int entityId, UUID entityUuid) {
        if (entityUuid == null) throw new IllegalArgumentException("Entity UUID is required.");
        this.entityId = entityId;
        this.entityUuid = entityUuid;
    }

    @Override
    public void fromBytes(ByteBuf buffer) {
        entityId = buffer.readInt();
        entityUuid = new UUID(buffer.readLong(), buffer.readLong());
        if (buffer.isReadable()) throw new IllegalArgumentException("Trailing nominator open data.");
    }

    @Override
    public void toBytes(ByteBuf buffer) {
        if (entityUuid == null) throw new IllegalArgumentException("Entity UUID is required.");
        buffer.writeInt(entityId);
        buffer.writeLong(entityUuid.getMostSignificantBits());
        buffer.writeLong(entityUuid.getLeastSignificantBits());
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
                        || !message.entityUuid.equals(entity.getUniqueID())
                        || entity.dimension != player.dimension
                        || player.getDistanceSqToEntity(entity) > 64.0D) return;
                    DialogueNetwork.CHANNEL.sendTo(
                        S2CNominatorEntityOpen.from(
                            entity,
                            NominatorSavedData.get(),
                            darkgrey.rpg.nominator.NominatorCatalog.from(
                                darkgrey.rpg.DarkGreyRpg.getProjectRepository()
                                    .getSnapshot())),
                        player);
                }
            });
            return null;
        }
    }
}
