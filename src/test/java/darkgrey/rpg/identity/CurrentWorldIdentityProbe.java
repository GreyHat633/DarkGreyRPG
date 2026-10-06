package darkgrey.rpg.identity;

import java.util.Collections;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;

import darkgrey.rpg.player.PlayerRpgSavedData;
import darkgrey.rpg.session.instance.CanonicalSessionInstanceSnapshot;
import darkgrey.rpg.session.persistence.CanonicalSessionSavedData;
import darkgrey.rpg.session.persistence.CanonicalSessionWorldStateNbtCodec;
import darkgrey.rpg.session.runtime.CanonicalSessionSnapshot;
import darkgrey.rpg.session.runtime.CanonicalSessionStatus;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceNbtCodec;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus;
import darkgrey.rpg.task.runtime.CanonicalTaskSnapshot;
import darkgrey.rpg.task.runtime.CanonicalTaskStatus;

/** Actual codec boundaries: current round trips and rejected candidates preserve accepted state. */
public final class CurrentWorldIdentityProbe {

    public static void run() {
        String story = "ST-2345-6789-ABCD-EFGH";
        UUID player = UUID.randomUUID();
        String session = new ResourceAddress(StoryUid.parse(story), ResourceAddress.Kind.SESSION, "session").toKey();
        CanonicalSessionSnapshot runtime = new CanonicalSessionSnapshot(
            session,
            "line",
            CanonicalSessionStatus.ACTIVE,
            Collections.emptyList(),
            Collections.emptyMap(),
            null,
            Collections.emptyMap());
        CanonicalSessionInstanceSnapshot instance = new CanonicalSessionInstanceSnapshot(
            player,
            story,
            "placement",
            session,
            1,
            runtime);
        NBTTagCompound encoded = CanonicalSessionWorldStateNbtCodec
            .encode(Collections.singletonList(instance), 2, Collections.emptyList());
        if (!CanonicalSessionWorldStateNbtCodec.decode(encoded)
            .getSessions()
            .get(0)
            .getSessionResourceId()
            .equals(session)) throw new AssertionError("Session address roundtrip");
        CanonicalSessionSavedData data = new CanonicalSessionSavedData();
        data.readFromNBT(encoded);
        NBTTagCompound accepted = new NBTTagCompound();
        data.writeToNBT(accepted);
        for (int version = 1; version < CanonicalSessionWorldStateNbtCodec.SCHEMA_VERSION; version++) {
            NBTTagCompound legacy = (NBTTagCompound) encoded.copy();
            legacy.setInteger("schema_version", version);
            reject(() -> data.readFromNBT(legacy));
        }
        NBTTagCompound bad = (NBTTagCompound) encoded.copy();
        bad.getCompoundTag("sessions")
            .getTagList("instances", 10)
            .getCompoundTagAt(0)
            .setString("session_resource_id", "Old:session");
        reject(() -> data.readFromNBT(bad));
        NBTTagCompound after = new NBTTagCompound();
        data.writeToNBT(after);
        if (!accepted.equals(after)) throw new AssertionError("Bad Session candidate changed accepted state");

        String task = new ResourceAddress(StoryUid.parse(story), ResourceAddress.Kind.TASK, "task").toKey();
        CanonicalTaskSnapshot taskRuntime = new CanonicalTaskSnapshot(
            task,
            "fingerprint",
            CanonicalTaskStatus.ACTIVE,
            Collections.emptyMap(),
            Collections.emptyMap(),
            Collections.emptyMap(),
            Collections.emptyMap(),
            true,
            null);
        CanonicalTaskInstanceSnapshot taskInstance = new CanonicalTaskInstanceSnapshot(
            player,
            story,
            "placement",
            task,
            CanonicalTaskInstanceStatus.ACTIVE,
            1,
            null,
            taskRuntime);
        NBTTagCompound tasks = CanonicalTaskInstanceNbtCodec.encode(Collections.singletonList(taskInstance));
        if (!task.equals(
            CanonicalTaskInstanceNbtCodec.decode(tasks)
                .get(0)
                .getTaskResourceId()))
            throw new AssertionError("Task address roundtrip");
        tasks.getTagList("instances", 10)
            .getCompoundTagAt(0)
            .getCompoundTag("task_resource_id")
            .setString("kind", "actor");
        reject(() -> CanonicalTaskInstanceNbtCodec.decode(tasks));

        PlayerRpgSavedData facts = new PlayerRpgSavedData();
        facts.claimReward(player, story, "once");
        NBTTagCompound savedFacts = new NBTTagCompound();
        facts.writeToNBT(savedFacts);
        PlayerRpgSavedData restored = new PlayerRpgSavedData();
        restored.readFromNBT(savedFacts);
        if (restored.claimReward(player, story, "once")) throw new AssertionError("Reward receipt replayed");
        savedFacts.removeTag("identity_format");
        reject(() -> restored.readFromNBT(savedFacts));
        if (!restored.hasClaimedReward(player, story, "once"))
            throw new AssertionError("Bad facts candidate erased receipt");
    }

    private static void reject(Runnable action) {
        try {
            action.run();
        } catch (IllegalArgumentException expected) {
            return;
        }
        throw new AssertionError("Legacy or malformed identity accepted");
    }
}
