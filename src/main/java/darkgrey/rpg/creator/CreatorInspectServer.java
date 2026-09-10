package darkgrey.rpg.creator;

import java.util.Map;
import java.util.WeakHashMap;

import net.minecraft.entity.Entity;
import net.minecraft.entity.EntityLivingBase;
import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.item.ItemStack;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import cpw.mods.fml.common.gameevent.TickEvent;
import darkgrey.rpg.content.ModItems;
import darkgrey.rpg.identity.EntityDgrIdentityResolver;
import darkgrey.rpg.item.identity.ItemIdentitySavedData;
import darkgrey.rpg.network.DialogueNetwork;

/** One server update per second while inspecting; unchanged identities do not send packets. */
public final class CreatorInspectServer {

    private final Map<EntityPlayerMP, NBTTagCompound> previous = new WeakHashMap<EntityPlayerMP, NBTTagCompound>();
    private final Map<EntityPlayerMP, Long> catalogs = new WeakHashMap<EntityPlayerMP, Long>();

    @SubscribeEvent
    public void tick(TickEvent.PlayerTickEvent event) {
        if (event.phase != TickEvent.Phase.END || event.player.worldObj.isRemote
            || !(event.player instanceof EntityPlayerMP)
            || event.player.ticksExisted % 20 != 0) return;
        EntityPlayerMP player = (EntityPlayerMP) event.player;
        ItemStack helmet = player.getCurrentArmor(3);
        boolean enabled = CreatorInspectSavedData.effective(
            CreatorInspectSavedData.get()
                .enabled(player.getUniqueID()),
            helmet != null && helmet.getItem() == ModItems.inspectorGoggles);
        NBTTagCompound data = new NBTTagCompound();
        data.setBoolean("enabled", enabled);
        data.setInteger("dimension", player.dimension);
        NBTTagList entities = new NBTTagList();
        if (enabled) for (Object value : player.worldObj
            .getEntitiesWithinAABB(EntityLivingBase.class, player.boundingBox.expand(32, 32, 32))) {
                Entity entity = (Entity) value;
                if (entity.isDead || entity.getDistanceSqToEntity(player) > 1024) continue;
                EntityDgrIdentityResolver.Resolution identity = EntityDgrIdentityResolver.resolve(entity);
                if (!identity.isResolved()) continue;
                NBTTagCompound row = new NBTTagCompound();
                row.setInteger("entity", entity.getEntityId());
                StringBuilder text = new StringBuilder();
                int index = 0;
                for (String id : identity.getActorIds()) {
                    if (index > 0) text.append('\n');
                    boolean group = index > 0
                        || identity.getSource() == EntityDgrIdentityResolver.Source.NOMINATOR_GROUP;
                    text.append(group ? "[GroupID] " : "[NPCID] ");
                    text.append(id);
                    index++;
                }
                row.setString("text", text.toString());
                entities.appendTag(row);
            }
        data.setTag("entities", entities);
        ItemIdentitySavedData items = enabled ? ItemIdentitySavedData.get() : null;
        long revision = enabled ? items.getRevision() : -1L;
        data.setLong("revision", revision);
        boolean refreshCatalog = enabled && (!catalogs.containsKey(player) || catalogs.get(player)
            .longValue() != revision
            || previous.get(player) == null
            || previous.get(player)
                .getInteger("dimension") != player.dimension);
        if (!data.equals(previous.get(player)) || refreshCatalog) {
            previous.put(player, (NBTTagCompound) data.copy());
            if (refreshCatalog) {
                NBTTagCompound catalog = new NBTTagCompound();
                items.writeToNBT(catalog);
                data.setTag("catalog", catalog);
                catalogs.put(player, revision);
            }
            if (!enabled) catalogs.remove(player);
            DialogueNetwork.CHANNEL.sendTo(new CreatorSnapshot(0, data), player);
        }
    }
}
