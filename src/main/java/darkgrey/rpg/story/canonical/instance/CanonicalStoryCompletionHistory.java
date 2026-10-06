package darkgrey.rpg.story.canonical.instance;

import java.util.LinkedHashMap;
import java.util.Map;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;
import net.minecraft.world.WorldSavedData;
import net.minecraft.world.storage.MapStorage;

import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.identity.StoryUid;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryRepeatCondition;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryRepeatEligibility;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryRepeatPolicy;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryStartConfiguration;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryStartDisposition;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryStatus;

/** Durable summaries independent of retired graph cursors and package availability. */
public final class CanonicalStoryCompletionHistory extends WorldSavedData {

    public static final String DATA_NAME = "darkgrey_rpg_story_completion_history";
    private Map<String, NBTTagCompound> records = new LinkedHashMap<String, NBTTagCompound>();

    public CanonicalStoryCompletionHistory() {
        this(DATA_NAME);
    }

    public CanonicalStoryCompletionHistory(String name) {
        super(name);
    }

    public static CanonicalStoryCompletionHistory get(MapStorage storage) {
        WorldSavedData loaded = storage.loadData(CanonicalStoryCompletionHistory.class, DATA_NAME);
        if (loaded instanceof CanonicalStoryCompletionHistory) return (CanonicalStoryCompletionHistory) loaded;
        CanonicalStoryCompletionHistory created = new CanonicalStoryCompletionHistory();
        storage.setData(DATA_NAME, created);
        return created;
    }

    public synchronized void observe(CanonicalStoryInstanceSnapshot snapshot) {
        if (snapshot.getStatus() == CanonicalStoryStatus.ACTIVE) return;
        record(
            snapshot,
            snapshot.getStatus(),
            snapshot.getTerminalTime()
                .longValue());
    }

    /** An interrupted run cannot silently become NEW when its container reappears. */
    public synchronized void retire(CanonicalStoryInstanceSnapshot snapshot) {
        if (snapshot.getStatus() != CanonicalStoryStatus.ACTIVE) {
            observe(snapshot);
            return;
        }
        record(
            snapshot,
            CanonicalStoryStatus.ERROR,
            Math.max(System.currentTimeMillis(), snapshot.getActivationTime()));
    }

    private void record(CanonicalStoryInstanceSnapshot snapshot, CanonicalStoryStatus status, long terminalTime) {
        StoryUid.parse(snapshot.getStoryId());
        String key = key(snapshot.getPlayerUuid(), snapshot.getStoryId());
        NBTTagCompound prior = records.get(key);
        if (prior != null && prior.getLong("activation") == snapshot.getActivationTime()
            && prior.getString("status")
                .equals(status.name()))
            return;
        NBTTagCompound row = new NBTTagCompound();
        row.setString(
            "player",
            snapshot.getPlayerUuid()
                .toString());
        row.setString("story_uid", snapshot.getStoryId());
        row.setString("status", status.name());
        row.setString(
            "repeat_policy",
            snapshot.getRuntimeSnapshot()
                .getRepeatPolicy()
                .name());
        row.setLong("activation", snapshot.getActivationTime());
        row.setLong("terminal", terminalTime);
        long completed = prior == null ? 0 : prior.getLong("completed_count");
        if (status == CanonicalStoryStatus.TERMINATED && completed < Long.MAX_VALUE) completed++;
        row.setLong("completed_count", completed);
        records.put(key, row);
        markDirty();
    }

    public synchronized CanonicalStoryStartDisposition disposition(UUID player, String uid,
        CanonicalGraphResource resource, java.time.Clock clock) {
        NBTTagCompound row = records.get(key(player, uid));
        if (row == null) return CanonicalStoryStartDisposition.NEW;
        if (resource == null) return CanonicalStoryStartDisposition.CONTAINER_BLOCKED;
        if (row.getBoolean("reset_pending")) return CanonicalStoryStartDisposition.NEW;
        // The summary describes the completed run. An installed replacement's
        // Start rules govern new runs; do not rewrite the historical policy.
        CanonicalStoryStartConfiguration current = CanonicalStoryStartConfiguration.parse(resource);
        return CanonicalStoryRepeatEligibility.evaluate(
            CanonicalStoryStatus.valueOf(row.getString("status")),
            current.getRepeatPolicy(),
            Long.valueOf(row.getLong("terminal")),
            CanonicalStoryRepeatCondition.forResource(resource),
            clock).disposition;
    }

    public synchronized NBTTagCompound summary(UUID player, String uid) {
        NBTTagCompound row = records.get(key(player, uid));
        return row == null ? null : (NBTTagCompound) row.copy();
    }

    /** Explicit administrator reset permits a new run without erasing durable history. */
    public synchronized void resetAdmission(UUID player, String uid) {
        StoryUid.parse(uid);
        NBTTagCompound row = records.get(key(player, uid));
        if (row != null && !row.getBoolean("reset_pending")) {
            row.setBoolean("reset_pending", true);
            markDirty();
        }
    }

    @Override
    public synchronized void writeToNBT(NBTTagCompound root) {
        root.setInteger("schema_version", 1);
        root.setString("identity_format", "story-uid-v1");
        NBTTagList list = new NBTTagList();
        for (NBTTagCompound row : new java.util.TreeMap<String, NBTTagCompound>(records).values())
            list.appendTag(row.copy());
        root.setTag("summaries", list);
    }

    @Override
    public synchronized void readFromNBT(NBTTagCompound root) {
        if (!root.hasKey("schema_version", 3) || root.getInteger("schema_version") != 1
            || !root.hasKey("identity_format", 8)
            || !"story-uid-v1".equals(root.getString("identity_format"))
            || !(root.getTag("summaries") instanceof NBTTagList)) throw malformed();
        NBTTagList list = (NBTTagList) root.getTag("summaries");
        if (list.tagCount() > 0 && list.func_150303_d() != 10) throw malformed();
        Map<String, NBTTagCompound> candidate = new LinkedHashMap<String, NBTTagCompound>();
        for (int i = 0; i < list.tagCount(); i++) {
            NBTTagCompound row = list.getCompoundTagAt(i);
            for (String field : new String[] { "player", "story_uid", "status", "repeat_policy" })
                if (!row.hasKey(field, 8)) throw malformed();
            for (String field : new String[] { "activation", "terminal", "completed_count" })
                if (!row.hasKey(field, 4)) throw malformed();
            if (row.hasKey("reset_pending") && !row.hasKey("reset_pending", 1)) throw malformed();
            UUID player = UUID.fromString(row.getString("player"));
            String uid = StoryUid.parse(row.getString("story_uid"))
                .getValue();
            CanonicalStoryStatus status = CanonicalStoryStatus.valueOf(row.getString("status"));
            CanonicalStoryRepeatPolicy.valueOf(row.getString("repeat_policy"));
            if (status == CanonicalStoryStatus.ACTIVE || row.getLong("activation") <= 0
                || row.getLong("terminal") < row.getLong("activation")
                || row.getLong("completed_count") < 0
                || (status == CanonicalStoryStatus.TERMINATED && row.getLong("completed_count") == 0)
                || candidate.put(key(player, uid), (NBTTagCompound) row.copy()) != null) throw malformed();
        }
        records = candidate;
    }

    private static String key(UUID player, String uid) {
        return player.toString() + "/" + uid;
    }

    private static IllegalArgumentException malformed() {
        return new IllegalArgumentException("Malformed Story completion history");
    }
}
