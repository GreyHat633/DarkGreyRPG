package darkgrey.rpg.story.canonical.forge;

import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.item.ItemStack;
import net.minecraftforge.event.entity.player.EntityInteractEvent;

import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import cpw.mods.fml.common.gameevent.PlayerEvent;
import cpw.mods.fml.common.gameevent.TickEvent;
import darkgrey.rpg.content.ModItems;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryRegionEntryTracker;

/** The production Story event owner. There is no legacy event bus or fallback. */
public final class CanonicalStoryEventAdapter {

    private final CanonicalStoryForgeManager stories;
    private final CanonicalStoryRegionEntryTracker regions = new CanonicalStoryRegionEntryTracker();

    public CanonicalStoryEventAdapter(CanonicalStoryForgeManager stories) {
        if (stories == null) throw new IllegalArgumentException("Canonical Story manager is required.");
        this.stories = stories;
    }

    @SubscribeEvent
    public void onEntityInteract(EntityInteractEvent event) {
        if (!(event.entityPlayer instanceof EntityPlayerMP)) return;
        ItemStack held = event.entityPlayer.getHeldItem();
        if (held != null && held.getItem() == ModItems.editorTool) return;
        if (stories.handleActorInteraction((EntityPlayerMP) event.entityPlayer, event.target)) event.setCanceled(true);
    }

    @SubscribeEvent
    public void onPlayerTick(TickEvent.PlayerTickEvent event) {
        if (event.phase != TickEvent.Phase.END || !(event.player instanceof EntityPlayerMP)
            || event.player.ticksExisted % 10 != 0) return;
        EntityPlayerMP player = (EntityPlayerMP) event.player;
        stories.recoverPendingRoutes(player);
        stories.handleRegionPosition(
            player,
            player.dimension,
            player.posX,
            player.posY,
            player.posZ,
            regions.update(
                player.getUniqueID(),
                stories.matchingRegionTriggers(player, player.dimension, player.posX, player.posY, player.posZ)));
    }

    @SubscribeEvent
    public void onPlayerLogin(PlayerEvent.PlayerLoggedInEvent event) {
        if (event.player instanceof EntityPlayerMP) stories.recoverPendingRoutes((EntityPlayerMP) event.player);
    }

    @SubscribeEvent
    public void onPlayerLogout(PlayerEvent.PlayerLoggedOutEvent event) {
        if (event.player == null) return;
        regions.forget(event.player.getUniqueID());
        stories.forgetActorChoices(event.player.getUniqueID());
    }
}
