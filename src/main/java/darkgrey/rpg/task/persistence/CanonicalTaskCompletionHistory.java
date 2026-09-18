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
        if (seen != null && seen.longValue() == entry.getActivationTime()) return;
        String key = part(
            entry.getPlayerUuid()
                .toString())
            + part(entry.getStoryInstanceId())
            + part(entry.getTaskNodePlacementId())
            + part(entry.getTaskResourceId());
        NBTTagCompound previous = groups.get(key);
        NBTTagCompound row = new NBTTagCompound();
        row.setString("id", "history:" + key);
        row.setString(
            "player",
            entry.getPlayerUuid()
                .toString());
        row.setString("title", entry.getTitle());
        row.setString("description", entry.getDescription());
        row.setString("status", "SETTLED");
        row.setString("result", entry.getSettledResultSlot());
        row.setLong("settlement", entry.getSettlementTime());
        row.setLong(
            "completion_count",
            previous == null ? 1 : Math.min(Long.MAX_VALUE - 1, previous.getLong("completion_count")) + 1);
        row.setTag("objectives", new NBTTagList());
        groups.put(key, row);
        observed.put(identity, entry.getActivationTime());
        markDirty();
    }

    /** Removed runtime identities can occur again even in the same clock millisecond. */
    public synchronized void retainObserved(Set<String> liveIdentities) {
        if (observed.keySet()
            .retainAll(liveIdentities)) markDirty();
    }

    public synchronized NBTTagList forPlayer(UUID player) {
        NBTTagList result = new NBTTagList();
        for (NBTTagCompound row : groups.values()) if (player.toString()
            .equals(row.getString("player"))) result.appendTag(row.copy());
        return result;
    }

    @Override
    public synchronized void writeToNBT(NBTTagCompound root) {
        NBTTagList rows = new NBTTagList();
        for (Map.Entry<String, NBTTagCompound> entry : groups.entrySet()) {
            NBTTagCompound row = (NBTTagCompound) entry.getValue()
                .copy();
            row.setString("group", entry.getKey());
            rows.appendTag(row);
        }
        NBTTagList seen = new NBTTagList();
        for (Map.Entry<String, Long> entry : observed.entrySet()) {
            NBTTagCompound row = new NBTTagCompound();
            row.setString("identity", entry.getKey());
            row.setLong("activation", entry.getValue());
            seen.appendTag(row);
        }
        root.setInteger("schema", 1);
        root.setTag("summaries", rows);
        root.setTag("observed", seen);
    }

    @Override
    public synchronized void readFromNBT(NBTTagCompound root) {
        if (root.getInteger("schema") != 1) throw new IllegalArgumentException("Unsupported completion history schema");
        Map<String, NBTTagCompound> next = new LinkedHashMap<String, NBTTagCompound>();
        NBTTagList rows = root.getTagList("summaries", 10);
        for (int i = 0; i < rows.tagCount(); i++) {
            NBTTagCompound row = (NBTTagCompound) rows.getCompoundTagAt(i)
                .copy();
            String group = row.getString("group");
            row.removeTag("group");
            if (group.isEmpty() || row.getLong("completion_count") <= 0)
                throw new IllegalArgumentException("Invalid completion summary");
            UUID.fromString(row.getString("player"));
            next.put(group, row);
        }
        Map<String, Long> nextObserved = new HashMap<String, Long>();
        NBTTagList seen = root.getTagList("observed", 10);
        for (int i = 0; i < seen.tagCount(); i++) {
            NBTTagCompound row = seen.getCompoundTagAt(i);
            nextObserved.put(row.getString("identity"), row.getLong("activation"));
        }
        groups.clear();
        groups.putAll(next);
        observed.clear();
        observed.putAll(nextObserved);
    }
}
