package darkgrey.rpg.task.persistence;

import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.Set;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;
import net.minecraft.world.WorldSavedData;
import net.minecraft.world.storage.MapStorage;

import darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus;
import darkgrey.rpg.task.journal.CanonicalTaskJournalEntry;
import darkgrey.rpg.task.journal.CanonicalTaskJournalObjectiveRow;
import darkgrey.rpg.task.runtime.CanonicalTaskObjectiveStatus;

/** Read-only completion summaries, separate from task execution and reward receipts. */
public final class CanonicalTaskCompletionHistory extends WorldSavedData {

    public static final String DATA_NAME = "darkgrey_rpg_task_completion_history";
    private final Map<String, NBTTagCompound> groups = new LinkedHashMap<String, NBTTagCompound>();
    private final Map<String, Long> observed = new HashMap<String, Long>();

    public CanonicalTaskCompletionHistory() {
        this(DATA_NAME);
    }

    public CanonicalTaskCompletionHistory(String name) {
        super(name);
    }

    public static CanonicalTaskCompletionHistory get(MapStorage storage) {
        CanonicalTaskCompletionHistory history = (CanonicalTaskCompletionHistory) storage
            .loadData(CanonicalTaskCompletionHistory.class, DATA_NAME);
        if (history == null) {
            history = new CanonicalTaskCompletionHistory();
            storage.setData(DATA_NAME, history);
        }
        return history;
    }

    private static String part(String text) {
        return text.length() + ":" + text;
    }

    public synchronized void observe(CanonicalTaskJournalEntry entry) {
        if (entry.getStatus() != CanonicalTaskInstanceStatus.SETTLED) return;
        String identity = entry.getIdentity();
        Long seen = observed.get(identity);
        String key = part(
            entry.getPlayerUuid()
                .toString())
            + part(entry.getStoryInstanceId())
            + part(entry.getTaskNodePlacementId())
            + part(entry.getTaskResourceId());
        NBTTagCompound previous = groups.get(key);
        if (seen != null && seen.longValue() == entry.getActivationTime()) {
            // A matching retained runtime can repair an old summary without counting it again.
            // Never reconstruct a historical route from the current authoring definition alone.
            if (previous != null && previous.getTagList("objectives", 10)
                .tagCount() == 0
                && previous.getLong("settlement") == entry.getSettlementTime()
                    .longValue()
                && previous.getString("result")
                    .equals(entry.getSettledResultSlot())) {
                NBTTagList objectives = completedObjectives(entry);
                if (objectives.tagCount() > 0) {
                    previous.setTag("objectives", objectives);
                    markDirty();
                }
            }
            return;
        }
        NBTTagCompound row = new NBTTagCompound();
        row.setString("id", "history:" + key);
        row.setString("task_resource_id", entry.getTaskResourceId());
        row.setString(
            "player",
            entry.getPlayerUuid()
                .toString());
        row.setString("title", entry.getTitle());
        darkgrey.rpg.creator.TaskStoryPresentation.fill(row, entry.getStoryInstanceId());
        row.setString("description", entry.getDescription());
        row.setString("status", "SETTLED");
        row.setString("result", entry.getSettledResultSlot());
        row.setLong("settlement", entry.getSettlementTime());
        row.setLong(
            "completion_count",
            previous == null ? 1 : Math.min(Long.MAX_VALUE - 1, previous.getLong("completion_count")) + 1);
        row.setTag("objectives", completedObjectives(entry));
        groups.put(key, row);
        observed.put(identity, entry.getActivationTime());
        markDirty();
    }

    private static NBTTagList completedObjectives(CanonicalTaskJournalEntry entry) {
        NBTTagList result = new NBTTagList();
        for (CanonicalTaskJournalObjectiveRow objective : entry.getObjectives()) {
            if (objective.getStatus() != CanonicalTaskObjectiveStatus.COMPLETED) continue;
            NBTTagCompound row = new NBTTagCompound();
            row.setString("text", objective.getDescription());
            row.setString("type", objective.getObjectiveType());
            row.setInteger("current", objective.getCurrent());
            row.setInteger("required", objective.getRequired());
            result.appendTag(row);
        }
        return result;
    }

    /** Removed runtime identities can occur again even in the same clock millisecond. */
    public synchronized void retainObserved(Set<String> liveIdentities) {
        if (observed.keySet()
            .retainAll(liveIdentities)) markDirty();
    }

    public synchronized NBTTagList forPlayer(UUID player) {
        NBTTagList result = new NBTTagList();
        for (NBTTagCompound row : groups.values()) if (player.toString()
            .equals(row.getString("player"))) {
                // A legacy save may load before its story packages. Persist labels once they become available.
                NBTTagCompound before = (NBTTagCompound) row.copy();
                darkgrey.rpg.creator.TaskStoryPresentation.recover(row);
                if (!before.equals(row)) markDirty();
                result.appendTag(row.copy());
            }
        return result;
    }

    @Override
    public synchronized void writeToNBT(NBTTagCompound root) {
        NBTTagList rows = new NBTTagList();
        for (Map.Entry<String, NBTTagCompound> entry : groups.entrySet()) {
            NBTTagCompound row = (NBTTagCompound) entry.getValue()
                .copy();
            row.setString("group", entry.getKey());
            row.setTag(
                "task_resource_id",
                darkgrey.rpg.identity.ResourceAddressNbt
                    .write(row.getString("task_resource_id"), darkgrey.rpg.identity.ResourceAddress.Kind.TASK));
            rows.appendTag(row);
        }
        NBTTagList seen = new NBTTagList();
        for (Map.Entry<String, Long> entry : observed.entrySet()) {
            NBTTagCompound row = new NBTTagCompound();
            row.setString("identity", entry.getKey());
            row.setLong("activation", entry.getValue());
            seen.appendTag(row);
        }
        root.setInteger("schema", 2);
        root.setString("identity_format", darkgrey.rpg.identity.ResourceAddressNbt.IDENTITY_FORMAT);
        root.setTag("summaries", rows);
        root.setTag("observed", seen);
    }

    @Override
    public synchronized void readFromNBT(NBTTagCompound root) {
        darkgrey.rpg.identity.ResourceAddressNbt.requireFormat(root);
        if (!root.hasKey("schema", 3) || root.getInteger("schema") != 2)
            throw new IllegalArgumentException("Unsupported completion history schema");
        Map<String, NBTTagCompound> next = new LinkedHashMap<String, NBTTagCompound>();
        NBTTagList rows = darkgrey.rpg.identity.ResourceAddressNbt.compounds(root, "summaries");
        for (int i = 0; i < rows.tagCount(); i++) {
            NBTTagCompound row = (NBTTagCompound) rows.getCompoundTagAt(i)
                .copy();
            String group = row.getString("group");
            row.removeTag("group");
            row.setString(
                "task_resource_id",
                darkgrey.rpg.identity.ResourceAddressNbt
                    .read(row, "task_resource_id", darkgrey.rpg.identity.ResourceAddress.Kind.TASK));
            darkgrey.rpg.creator.TaskStoryPresentation.recover(row);
            if (group.isEmpty() || row.getLong("completion_count") <= 0)
                throw new IllegalArgumentException("Invalid completion summary");
            UUID.fromString(row.getString("player"));
            darkgrey.rpg.identity.StoryUid.parse(row.getString("story"));
            if (!row.hasKey("task_resource_id", 8)
                || darkgrey.rpg.identity.ResourceAddress.fromKey(row.getString("task_resource_id"))
                    .getKind() != darkgrey.rpg.identity.ResourceAddress.Kind.TASK)
                throw new IllegalArgumentException("Invalid completion Task address");
            if (next.put(group, row) != null) throw new IllegalArgumentException("Duplicate completion summary");
        }
        Map<String, Long> nextObserved = new HashMap<String, Long>();
        NBTTagList seen = darkgrey.rpg.identity.ResourceAddressNbt.compounds(root, "observed");
        for (int i = 0; i < seen.tagCount(); i++) {
            NBTTagCompound row = seen.getCompoundTagAt(i);
            if (!row.hasKey("identity", 8) || row.getString("identity")
                .isEmpty()
                || !row.hasKey("activation", 4)
                || row.getLong("activation") <= 0
                || nextObserved.put(row.getString("identity"), row.getLong("activation")) != null)
                throw new IllegalArgumentException("Invalid completion observation");
        }
        groups.clear();
        groups.putAll(next);
        observed.clear();
        observed.putAll(nextObserved);
    }
}
