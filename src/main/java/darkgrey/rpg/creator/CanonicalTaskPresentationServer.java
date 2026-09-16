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
        CanonicalTaskSavedData source = CanonicalTaskSavedData.get(player);
        Object project = DarkGreyRpg.getProjectRepository()
            .getSnapshot();
        long generation = source.getPresentationGeneration();
        boolean worldChanged = state.dimension != player.dimension || state.source != source;
        if (!force && !worldChanged && state.generation == generation && state.project == project) return;
        java.util.List<darkgrey.rpg.task.journal.CanonicalTaskJournalEntry> journal = DarkGreyRpg
            .getCanonicalTaskManager()
            .getJournal(player);
        net.minecraft.nbt.NBTTagList events = state.notifications
            .update(journal, worldChanged || state.project != project);
        NBTTagCompound data = CanonicalTaskUiProjection.project(journal);
        if (force || worldChanged || !data.equals(state.previous) || events.tagCount() > 0) {
            state.previous = (NBTTagCompound) data.copy();
            state.revision = ++nextRevision;
            data.setTag("notifications", events);
            data.setLong("revision", state.revision);
            data.setInteger("dimension", player.dimension);
            DialogueNetwork.CHANNEL.sendTo(new CreatorSnapshot(1, data), player);
        }
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

        final CanonicalTaskNotifications notifications = new CanonicalTaskNotifications();
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
