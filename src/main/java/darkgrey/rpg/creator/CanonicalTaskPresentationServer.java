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
        NBTTagCompound data = CanonicalTaskUiProjection.project(
            DarkGreyRpg.getCanonicalTaskManager()
                .getJournal(player));
        if (force || worldChanged || !data.equals(state.previous)) {
            state.previous = (NBTTagCompound) data.copy();
            state.revision = ++nextRevision;
            data.setLong("revision", state.revision);
            data.setInteger("dimension", player.dimension);
            DialogueNetwork.CHANNEL.sendTo(new CreatorSnapshot(1, data), player);
        }
        state.generation = source.getPresentationGeneration();
        state.source = source;
        state.project = project;
        state.dimension = player.dimension;
    }

    /** Validates an action against the exact read-only snapshot last sent to this connection. */
    public static boolean submit(EntityPlayerMP player, long revision, String taskId, String objectiveId) {
        State state = STATES.get(player);
        if (state == null || state.player.get() != player
            || state.previous == null
            || state.lastSubmitTick == player.ticksExisted) return false;
        state.lastSubmitTick = player.ticksExisted;
        if (state.revision != revision || state.source != CanonicalTaskSavedData.get(player)
            || state.generation != state.source.getPresentationGeneration()
            || state.dimension != player.dimension
            || state.project != DarkGreyRpg.getProjectRepository()
                .getSnapshot()) {
            push(player, true);
            return false;
        }
        net.minecraft.nbt.NBTTagList tasks = state.previous.getTagList("tasks", 10);
        for (int i = 0; i < tasks.tagCount(); i++) {
            NBTTagCompound task = tasks.getCompoundTagAt(i);
            if (!taskId.equals(task.getString("id"))) continue;
            net.minecraft.nbt.NBTTagList objectives = task.getTagList("objectives", 10);
            for (int j = 0; j < objectives.tagCount(); j++) {
                NBTTagCompound objective = objectives.getCompoundTagAt(j);
                if (!objectiveId.equals(objective.getString("id")) || !objective.getBoolean("submit")) continue;
                boolean accepted = DarkGreyRpg.getCanonicalTaskManager()
                    .submitItem(
                        player,
                        task.getString("story"),
                        task.getString("placement"),
                        objectiveId,
                        task.getLong("activation"));
                push(player, true);
                return accepted;
            }
        }
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
