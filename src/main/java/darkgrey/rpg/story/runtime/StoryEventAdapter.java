package darkgrey.rpg.story.runtime;

import java.util.List;

import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.item.ItemStack;
import net.minecraftforge.event.entity.player.EntityInteractEvent;

import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import cpw.mods.fml.common.gameevent.PlayerEvent;
import cpw.mods.fml.common.gameevent.TickEvent;
import darkgrey.rpg.compat.customnpcs.CustomNpcActorBinding;
import darkgrey.rpg.content.ModItems;
import darkgrey.rpg.story.canonical.forge.CanonicalStoryForgeManager;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryRegionEntryTracker;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryTriggerIndex;

public final class StoryEventAdapter {

    private final StoryEventBus eventBus;
    private final CanonicalStoryForgeManager canonicalStories;
    private final CanonicalStoryRegionEntryTracker regionEntries = new CanonicalStoryRegionEntryTracker();

    public StoryEventAdapter(StoryEventBus eventBus) {
        this(eventBus, null);
    }

    public StoryEventAdapter(StoryEventBus eventBus, CanonicalStoryForgeManager canonicalStories) {
        if (eventBus == null) throw new IllegalArgumentException("Story event bus is required.");
        this.eventBus = eventBus;
        this.canonicalStories = canonicalStories;
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
            if (canonicalStories != null) {
                EntityPlayerMP player = (EntityPlayerMP) event.entityPlayer;
                canonicalStories.handleActorInteraction(player, actorId);
            }
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
        if (canonicalStories == null) return;
        List<CanonicalStoryTriggerIndex.Match> matches = canonicalStories
            .matchingRegionTriggers(player.worldObj.provider.dimensionId, player.posX, player.posY, player.posZ);
        canonicalStories.handleRegionPosition(
            player,
            player.worldObj.provider.dimensionId,
            player.posX,
            player.posY,
            player.posZ,
            regionEntries.update(player.getUniqueID(), matches));
    }

    @SubscribeEvent
    public void onPlayerLogout(PlayerEvent.PlayerLoggedOutEvent event) {
        if (event.player != null) regionEntries.forget(event.player.getUniqueID());
    }
}
