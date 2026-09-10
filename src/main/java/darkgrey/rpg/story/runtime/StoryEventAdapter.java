package darkgrey.rpg.story.runtime;

import java.util.List;

import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.item.ItemStack;
import net.minecraftforge.event.entity.player.EntityInteractEvent;

import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import cpw.mods.fml.common.gameevent.PlayerEvent;
import cpw.mods.fml.common.gameevent.TickEvent;
import darkgrey.rpg.content.ModItems;
import darkgrey.rpg.identity.EntityDgrIdentityResolver;
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
        if (eventBus == null && canonicalStories == null)
            throw new IllegalArgumentException("Story runtime is required.");
        this.eventBus = eventBus;
        this.canonicalStories = canonicalStories;
    }

    @SubscribeEvent
    public void onEntityInteract(EntityInteractEvent event) {
        if (!(event.entityPlayer instanceof EntityPlayerMP)) {
            return;
        }
        ItemStack held = event.entityPlayer.getHeldItem();
        if (held != null && held.getItem() == ModItems.editorTool) {
            return;
        }
        List<String> actorIds = EntityDgrIdentityResolver.resolveActorIds(event.target);
        if (canonicalStories != null) {
            if (canonicalStories.handleActorInteraction((EntityPlayerMP) event.entityPlayer, event.target))
                event.setCanceled(true);
            return;
        }
        if (!actorIds.isEmpty()) {
            event.setCanceled(true);
            EntityPlayerMP player = (EntityPlayerMP) event.entityPlayer;
            for (String actorId : actorIds) {
                eventBus.post(player, StoryEvent.target(StoryEvent.Type.INTERACT_ACTOR, actorId));
                if (canonicalStories != null) canonicalStories.handleActorInteraction(player, actorId);
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
        if (canonicalStories == null) eventBus.post(
            player,
            StoryEvent.position(player.worldObj.provider.dimensionId, player.posX, player.posY, player.posZ));
        if (canonicalStories == null) return;
        List<CanonicalStoryTriggerIndex.Match> matches = canonicalStories.matchingRegionTriggers(
            player,
            player.worldObj.provider.dimensionId,
            player.posX,
            player.posY,
            player.posZ);
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
        if (event.player != null) {
            regionEntries.forget(event.player.getUniqueID());
            if (canonicalStories != null) canonicalStories.forgetActorChoices(event.player.getUniqueID());
        }
    }
}
