package darkgrey.rpg.task.forge;

import net.minecraft.entity.Entity;
import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraftforge.event.entity.living.LivingDeathEvent;
import net.minecraftforge.event.entity.player.EntityInteractEvent;
import net.minecraftforge.event.entity.player.EntityItemPickupEvent;

import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;

import cpw.mods.fml.common.eventhandler.SubscribeEvent;
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
            Entity attacker = event.source.getEntity();
            if (!(attacker instanceof EntityPlayerMP)) return;
            CanonicalTaskEvent taskEvent = CanonicalTaskForgeEventNormalizer.kill(event.entityLiving);
            if (taskEvent != null) dispatch((EntityPlayerMP) attacker, taskEvent);
        } catch (RuntimeException failure) {
            // Forge listeners must not let one malformed entity abort the event bus.
            LOG.warn("Canonical Task kill event was ignored: {}", failure.getMessage());
        }
    }

    @SubscribeEvent
    public void onItemPickup(EntityItemPickupEvent event) {
        try {
            if (event == null || !(event.entityPlayer instanceof EntityPlayerMP) || event.item == null) return;
            CanonicalTaskEvent taskEvent = CanonicalTaskForgeEventNormalizer.collect(event.item.getEntityItem());
            if (taskEvent != null) dispatch((EntityPlayerMP) event.entityPlayer, taskEvent);
        } catch (RuntimeException failure) {
            // Forge listeners must not let one malformed stack abort the event bus.
            LOG.warn("Canonical Task pickup event was ignored: {}", failure.getMessage());
        }
    }

    @SubscribeEvent(receiveCanceled = true)
    public void onEntityInteract(EntityInteractEvent event) {
        try {
            if (event == null || !(event.entityPlayer instanceof EntityPlayerMP)) return;
            CanonicalTaskEvent taskEvent = CanonicalTaskForgeEventNormalizer.interact(event.target);
            if (taskEvent != null) dispatch((EntityPlayerMP) event.entityPlayer, taskEvent);
        } catch (RuntimeException failure) {
            // Forge listeners must not let one malformed CustomNPC+ wrapper abort the event bus.
            LOG.warn("Canonical Task interaction event was ignored: {}", failure.getMessage());
        }
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
