package darkgrey.rpg.creator;

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
        State state = STATES.get(player);
        if (state == null) {
            state = new State();
            STATES.put(player, state);
        }
        if (!state.sessionProjected || state.dimension != player.dimension) {
            DarkGreyRpg.getCanonicalSessionManager()
                .reprojectActive(player);
            state.sessionProjected = true;
        }
        push(player, false);
    }

    public static void push(EntityPlayerMP player, boolean force) {
        State state = STATES.get(player);
        if (state == null) {
            state = new State();
            STATES.put(player, state);
        }
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
            data.setLong("revision", ++nextRevision);
            data.setInteger("dimension", player.dimension);
            DialogueNetwork.CHANNEL.sendTo(new CreatorSnapshot(1, data), player);
        }
        state.generation = source.getPresentationGeneration();
        state.source = source;
        state.project = project;
        state.dimension = player.dimension;
    }

    private static final class State {

        long generation = -1;
        int dimension = Integer.MIN_VALUE;
        Object project;
        CanonicalTaskSavedData source;
        NBTTagCompound previous;
        boolean sessionProjected;
    }
}
