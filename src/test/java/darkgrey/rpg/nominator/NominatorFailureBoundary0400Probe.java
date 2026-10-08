package darkgrey.rpg.nominator;

import java.lang.reflect.Field;
import java.util.ArrayList;
import java.util.List;

import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;
import net.minecraft.util.IChatComponent;

import darkgrey.rpg.identity.NpcIdentitySavedData;
import darkgrey.rpg.item.identity.ItemIdentitySavedData;
import darkgrey.rpg.network.MainThreadScheduler;

/** Runs the production open boundary and response builder through real failed-reader states. */
public final class NominatorFailureBoundary0400Probe {

    public static void main(String[] args) throws Exception {
        ProbePlayer player = Nominator0400Probe.allocate(ProbePlayer.class);
        player.messages = new ArrayList<String>();
        NominatorSavedData fresh = new NominatorSavedData();
        NBTTagCompound valid = new NBTTagCompound();
        fresh.writeToNBT(valid);
        List<NBTTagCompound> invalid = new ArrayList<NBTTagCompound>();
        NBTTagCompound legacy = (NBTTagCompound) valid.copy();
        legacy.setInteger("schema_version", 3);
        legacy.setTag("type_groups", new NBTTagList());
        invalid.add(legacy);
        NBTTagCompound damaged = (NBTTagCompound) valid.copy();
        damaged.removeTag("entities");
        invalid.add(damaged);
        NBTTagCompound badRevision = (NBTTagCompound) valid.copy();
        badRevision.setLong("revision", -1);
        invalid.add(badRevision);
        invalid.add(new NBTTagCompound());
        NpcIdentitySavedData identities = new NpcIdentitySavedData();
        long npcRevision = identities.getRevision();
        int[] normal = { 0 };
        for (NBTTagCompound root : invalid) {
            NominatorSavedData failed = new NominatorSavedData(NominatorSavedData.DATA_NAME);
            try {
                failed.readFromNBT(root);
                throw new AssertionError("Invalid root accepted");
            } catch (IllegalArgumentException expected) {}
            for (int attempt = 0; attempt < 3; attempt++) {
                MainThreadScheduler.scheduleServer(() -> NominatorOpenBoundary.run(player, failed::requireUsable));
                MainThreadScheduler.scheduleServer(() -> normal[0]++);
                MainThreadScheduler scheduler = new MainThreadScheduler();
                scheduler.onServerTick(
                    new cpw.mods.fml.common.gameevent.TickEvent.ServerTickEvent(
                        cpw.mods.fml.common.gameevent.TickEvent.Phase.START));
                NBTTagCompound request = new NBTTagCompound();
                darkgrey.rpg.client.gui.NominatorControls controls = new darkgrey.rpg.client.gui.NominatorControls();
                Field tokenField = controls.getClass()
                    .getDeclaredField("token");
                tokenField.setAccessible(true);
                String token = (String) tokenField.get(controls);
                Field sequenceField = controls.getClass()
                    .getDeclaredField("sequence");
                sequenceField.setAccessible(true);
                sequenceField.setInt(controls, attempt + 1);
                controls.pending = true;
                request.setString("token", token);
                request.setInteger("sequence", attempt + 1);
                request.setString("entityUuid", Nominator0400Probe.A.toString());
                NBTTagCompound response = NominatorActions
                    .response(request, NominatorResult.accepted(""), 17, failed, identities, null);
                require(
                    !response.getBoolean("accepted")
                        && NominatorDataUnavailableException.CODE.equals(response.getString("code")),
                    "failure response");
                require(
                    token.equals(response.getString("token")) && response.getInteger("sequence") == attempt + 1,
                    "correlation retained");
                require(
                    response.getLong("revision") == -1 && response.getLong("npcRevision") == -1
                        && !response.hasKey("individual"),
                    "no partial snapshot");
                require(
                    controls.accept(response) && !controls.pending && !controls.message.isEmpty(),
                    "client pending released with explanation");
                request.setBoolean("items", true);
                require(
                    NominatorActions
                        .response(request, NominatorResult.accepted(""), 17, failed, null, new ItemIdentitySavedData())
                        .getBoolean("accepted"),
                    "item response independent");
            }
        }
        require(normal[0] == 12 && player.messages.size() == 12, "subsequent tick tasks and player feedback");
        require(identities.getRevision() == npcRevision, "NPC registry unchanged");
        require(NominatorOpenBoundary.run(player, fresh::requireUsable), "fresh open works after failures");
        System.out.println(
            "NOMINATOR_FAILURE_BOUNDARY_0400=PASS legacy damaged repeated-open tick-continuation correlated-response item-independence no-partial-NPC");
    }

    private static void require(boolean condition, String label) {
        if (!condition) throw new AssertionError(label);
    }

    public static final class ProbePlayer extends EntityPlayerMP {

        List<String> messages;

        private ProbePlayer() {
            super(null, null, null, null);
        }

        @Override
        public void addChatMessage(IChatComponent component) {
            messages.add(component.getUnformattedText());
        }
    }
}
