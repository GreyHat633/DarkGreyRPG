package darkgrey.rpg.story.runtime;

import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.item.ItemStack;
import net.minecraftforge.event.entity.player.EntityInteractEvent;

import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import cpw.mods.fml.common.gameevent.TickEvent;
import darkgrey.rpg.compat.customnpcs.CustomNpcActorBinding;
import darkgrey.rpg.content.ModItems;

public final class StoryEventAdapter {

    private final StoryEventBus eventBus;

    public StoryEventAdapter(StoryEventBus eventBus) {
        this.eventBus = eventBus;
    }

    @SubscribeEvent
    public void onEntityInteract(EntityInteractEvent event) {
        if (!(event.entityPlayer instanceof EntityPlayerMP) || !CustomNpcActorBinding.isCustomNpc(event.target)) {
            return;
        }
        ItemStack held = event.entityPlayer.getHeldItem();
        if (held != null && held.getItem() == ModItems.editorTool) {
            return;
        }
        String actorId = CustomNpcActorBinding.getActorId(event.target);
        if (actorId != null) {
            event.setCanceled(true);
            eventBus
                .post((EntityPlayerMP) event.entityPlayer, StoryEvent.target(StoryEvent.Type.INTERACT_ACTOR, actorId));
        }
    }

    @SubscribeEvent
    public void onPlayerTick(TickEvent.PlayerTickEvent event) {
        if (event.phase != TickEvent.Phase.END || !(event.player instanceof EntityPlayerMP)
            || event.player.ticksExisted % 10 != 0) {
            return;
        }
        EntityPlayerMP player = (EntityPlayerMP) event.player;
        eventBus.post(
            player,
            StoryEvent.position(player.worldObj.provider.dimensionId, player.posX, player.posY, player.posZ));
    }
}
