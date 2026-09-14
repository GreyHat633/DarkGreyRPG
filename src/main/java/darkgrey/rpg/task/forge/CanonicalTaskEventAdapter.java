package darkgrey.rpg.task.forge;

import java.util.List;

import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraftforge.event.entity.living.LivingDeathEvent;
import net.minecraftforge.event.entity.player.EntityInteractEvent;

import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;

import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import cpw.mods.fml.common.gameevent.TickEvent;
import darkgrey.rpg.task.runtime.CanonicalTaskEvent;

/** One Forge event listener for canonical Task kill, collect, and CustomNPC+ interaction events. */
public final class CanonicalTaskEventAdapter {

    private static final Logger LOG = LogManager.getLogger(CanonicalTaskEventAdapter.class);
    private final CanonicalTaskForgeManager manager;

    public CanonicalTaskEventAdapter(CanonicalTaskForgeManager manager) {
        if (manager == null) throw new IllegalArgumentException("Canonical Task manager is required.");
        this.manager = manager;
    }

    @SubscribeEvent
    public void onLivingDeath(LivingDeathEvent event) {
        try {
            if (event == null || event.source == null) return;
            EntityPlayerMP attacker = CanonicalTaskForgeEventNormalizer.creditedKiller(event.source);
            if (attacker == null) return;
            List<CanonicalTaskEvent> taskEvents = CanonicalTaskForgeEventNormalizer.killEvents(event.entityLiving);
            for (CanonicalTaskEvent taskEvent : taskEvents) dispatch(attacker, taskEvent);
        } catch (RuntimeException failure) {
            // Forge listeners must not let one malformed entity abort the event bus.
            LOG.warn("Canonical Task kill event was ignored: {}", failure.getMessage());
        }
    }

    @SubscribeEvent(receiveCanceled = true)
    public void onEntityInteract(EntityInteractEvent event) {
        try {
            if (event == null || event.isCanceled() || !(event.entityPlayer instanceof EntityPlayerMP)) return;
            EntityPlayerMP player = (EntityPlayerMP) event.entityPlayer;
            // A successful submit consumes this physical interaction. Do not
            // dispatch the same click as a second actor event, which could
            // activate a newly unlocked objective in the same tick.
            if (manager.handleEntityInteraction(player, event.target)) {
                event.setCanceled(true);
                return;
            }
            List<CanonicalTaskEvent> taskEvents = CanonicalTaskForgeEventNormalizer.interactEvents(event.target);
            for (CanonicalTaskEvent taskEvent : taskEvents) dispatch((EntityPlayerMP) event.entityPlayer, taskEvent);
        } catch (RuntimeException failure) {
            // Forge listeners must not let one malformed CustomNPC+ wrapper abort the event bus.
            LOG.warn("Canonical Task interaction event was ignored: {}", failure.getMessage());
        }
    }

    @SubscribeEvent
    public void onPlayerTick(TickEvent.PlayerTickEvent event) {
        if (event == null || event.phase != TickEvent.Phase.END
            || !(event.player instanceof EntityPlayerMP)
            || event.player instanceof net.minecraftforge.common.util.FakePlayer
            || event.player.ticksExisted % 20 != 0) return;
        try {
            CanonicalTaskPlayerTransactions.recover((EntityPlayerMP) event.player);
            manager.synchronizeWorldLogic((EntityPlayerMP) event.player);
            manager.synchronizeObjectives((EntityPlayerMP) event.player);
            manager.synchronizeRewards((EntityPlayerMP) event.player);
        } catch (RuntimeException failure) {
            LOG.warn("Canonical Task world Logic synchronization failed: {}", failure.getMessage());
        }
    }

    @SubscribeEvent
    public void onPlayerLogout(cpw.mods.fml.common.gameevent.PlayerEvent.PlayerLoggedOutEvent event) {
        if (event != null && event.player != null) manager.forgetSubmitChoices(event.player.getUniqueID());
    }

    private void dispatch(EntityPlayerMP player, CanonicalTaskEvent event) {
        try {
            manager.dispatch(player, event);
        } catch (RuntimeException failure) {
            // A malformed instance/event is isolated at the manager/SavedData boundary.
            // Do not cancel Forge events or advance Story state.
            LOG.warn("Canonical Task dispatch failed: {}", failure.getMessage());
        }
    }
}
