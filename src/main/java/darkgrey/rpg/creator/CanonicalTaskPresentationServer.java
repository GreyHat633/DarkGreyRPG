package darkgrey.rpg.creator;

import java.lang.ref.WeakReference;
import java.util.Map;
import java.util.WeakHashMap;

import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.nbt.NBTTagCompound;

import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import cpw.mods.fml.common.gameevent.TickEvent;
import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.task.persistence.CanonicalTaskSavedData;

/** Coalesces dirty canonical state into player-specific full presentation snapshots. */
public final class CanonicalTaskPresentationServer {

    private static final Map<EntityPlayerMP, State> STATES = new WeakHashMap<EntityPlayerMP, State>();
    private static long nextRevision;

    @SubscribeEvent
    public void tick(TickEvent.PlayerTickEvent event) {
        if (event.phase != TickEvent.Phase.END || !(event.player instanceof EntityPlayerMP)) return;
        EntityPlayerMP player = (EntityPlayerMP) event.player;
        if (player.playerNetServerHandler == null || player.ticksExisted < 5) return;
        State state = stateFor(player);
        darkgrey.rpg.title.CanonicalTitleServer.tick(player);
        if (!state.sessionProjected || state.dimension != player.dimension) {
            DarkGreyRpg.getCanonicalStoryManager()
                .reprojectTitles(player);
            DarkGreyRpg.getCanonicalSessionManager()
                .reprojectActive(player);
            state.sessionProjected = true;
        }
        push(player, false);
    }

    public static void push(EntityPlayerMP player, boolean force) {
        State state = stateFor(player);
        if (player.ticksExisted < state.retryTick) return;
        try {
            pushPrepared(player, force, state);
        } catch (RuntimeException failure) {
            state.retryTick = player.ticksExisted + 20;
            if (player.ticksExisted >= state.diagnosticTick) {
                state.diagnosticTick = player.ticksExisted + 200;
                DarkGreyRpg.LOG.warn("Task presentation was not submitted; retry remains pending", failure);
            }
        }
    }

    private static void pushPrepared(EntityPlayerMP player, boolean force, State state) {
        CanonicalTaskSavedData source = CanonicalTaskSavedData.get(player);
        Object project = DarkGreyRpg.getProjectRepository()
            .getSnapshot();
        long generation = source.getPresentationGeneration();
        boolean worldChanged = state.dimension != player.dimension || state.source != source;
        if (!force && !worldChanged
            && state.generation == generation
            && state.project == project
            && player.ticksExisted % 10 != 0) return;
        java.util.List<darkgrey.rpg.task.journal.CanonicalTaskJournalEntry> journal;
        try {
            journal = DarkGreyRpg.getCanonicalTaskManager()
                .getJournal(player);
        } catch (darkgrey.rpg.graph.canonical.CanonicalGraphResourceException error) {
            if (!source.hasPendingData()) throw error;
            journal = darkgrey.rpg.task.journal.CanonicalTaskJournalProjector
                .projectUnavailable(player.getUniqueID(), source.getPendingRaw(), error.getCode());
        } catch (IllegalArgumentException error) {
            if (!source.hasPendingData()) throw error;
            journal = darkgrey.rpg.task.journal.CanonicalTaskJournalProjector
                .projectUnavailable(player.getUniqueID(), source.getPendingRaw(), "task.data.unavailable");
        }
        CanonicalTaskNotifications notifications = state.notifications.detached();
        net.minecraft.nbt.NBTTagList events = notifications.update(
            journal,
            worldChanged || state.project != project,
            text -> darkgrey.rpg.session.forge.DynamicContentResolver.resolve(text, player));
        NBTTagCompound data = CanonicalTaskUiProjection.project(journal, player);
        data.setLong("generation", source.getPresentationGeneration());
        // Persisted completion records are projected only for an explicit history-page request.
        if (force || worldChanged || !data.equals(state.previous) || events.tagCount() > 0) {
            NBTTagCompound previous = (NBTTagCompound) data.copy();
            long revision = ++nextRevision;
            data.setTag("notifications", events);
            data.setLong("revision", revision);
            data.setInteger("dimension", player.dimension);
            java.util.List<CreatorSnapshot> packets = TaskSnapshotTransport.encode(data);
            for (CreatorSnapshot packet : packets) DialogueNetwork.CHANNEL.sendTo(packet, player);
            state.previous = previous;
            state.revision = revision;
        }
        state.notifications = notifications;
        state.generation = source.getPresentationGeneration();
        state.source = source;
        state.project = project;
        state.dimension = player.dimension;
    }

    /**
     * Legacy menu-submit entry point retained for wire compatibility. It is
     * intentionally inert: only a server EntityInteractEvent may submit.
     */
    public static boolean submit(EntityPlayerMP player, long revision, String taskId, String objectiveId) {
        if (player != null) push(player, true);
        return false;
    }

    private static State stateFor(EntityPlayerMP player) {
        State state = STATES.get(player);
        // Entity.equals/hashCode use entityId, which is reused by the respawn replacement.
        if (state == null || state.player.get() != player) {
            STATES.remove(player);
            state = new State(player);
            STATES.put(player, state);
        }
        return state;
    }

    private static final class State {

        final WeakReference<EntityPlayerMP> player;

        State(EntityPlayerMP player) {
            this.player = new WeakReference<EntityPlayerMP>(player);
        }

        CanonicalTaskNotifications notifications = new CanonicalTaskNotifications();
        int retryTick;
        int diagnosticTick;
        long generation = -1;
        long revision;
        int lastSubmitTick = Integer.MIN_VALUE;
        int dimension = Integer.MIN_VALUE;
        Object project;
        CanonicalTaskSavedData source;
        NBTTagCompound previous;
        boolean sessionProjected;
    }
}
