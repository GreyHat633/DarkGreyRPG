package darkgrey.rpg.quest.runtime;

import net.minecraft.entity.Entity;
import net.minecraft.entity.EntityList;
import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.item.ItemStack;
import net.minecraftforge.event.entity.living.LivingDeathEvent;
import net.minecraftforge.event.entity.player.EntityInteractEvent;
import net.minecraftforge.event.entity.player.EntityItemPickupEvent;

import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import cpw.mods.fml.common.gameevent.TickEvent;
import darkgrey.rpg.identity.EntityDgrIdentityResolver;
import darkgrey.rpg.quest.ObjectiveType;

public final class QuestEventAdapter {

    private final QuestRuntimeService runtime;

    public QuestEventAdapter(QuestRuntimeService runtime) {
        this.runtime = runtime;
    }

    @SubscribeEvent
    public void onLivingDeath(LivingDeathEvent event) {
        Entity attacker = event.source.getEntity();
        if (!(attacker instanceof EntityPlayerMP)) {
            return;
        }
        String entityId = EntityList.getEntityString(event.entityLiving);
        if (entityId != null) {
            runtime.accept((EntityPlayerMP) attacker, QuestEvent.target(ObjectiveType.KILL_ENTITY, entityId, -1, 1));
        }
    }

    @SubscribeEvent
    public void onItemPickup(EntityItemPickupEvent event) {
        if (!(event.entityPlayer instanceof EntityPlayerMP)) {
            return;
        }
        ItemStack stack = event.item.getEntityItem();
        Object registryName = net.minecraft.item.Item.itemRegistry.getNameForObject(stack.getItem());
        if (registryName != null) {
            runtime.accept(
                (EntityPlayerMP) event.entityPlayer,
                QuestEvent.target(
                    ObjectiveType.COLLECT_ITEM,
                    String.valueOf(registryName),
                    stack.getItemDamage(),
                    stack.stackSize));
        }
    }

    @SubscribeEvent
    public void onEntityInteract(EntityInteractEvent event) {
        if (!(event.entityPlayer instanceof EntityPlayerMP)) {
            return;
        }
        for (String actorId : EntityDgrIdentityResolver.resolveActorIds(event.target)) {
            runtime.accept(
                (EntityPlayerMP) event.entityPlayer,
                QuestEvent.target(ObjectiveType.INTERACT_ACTOR, actorId, -1, 1));
        }
    }

    @SubscribeEvent
    public void onPlayerTick(TickEvent.PlayerTickEvent event) {
        if (event.phase != TickEvent.Phase.END || !(event.player instanceof EntityPlayerMP)
            || event.player.ticksExisted % 10 != 0) {
            return;
        }
        EntityPlayerMP player = (EntityPlayerMP) event.player;
        runtime.accept(
            player,
            QuestEvent.location(player.worldObj.provider.dimensionId, player.posX, player.posY, player.posZ));
    }
}
