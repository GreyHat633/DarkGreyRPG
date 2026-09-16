package darkgrey.rpg.creator;

import java.util.*;

import net.minecraft.nbt.*;

import darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus;
import darkgrey.rpg.task.journal.*;
import darkgrey.rpg.task.runtime.CanonicalTaskObjectiveStatus;

/** Server-only comparison of authoritative states. Quantities never create notifications. */
public final class CanonicalTaskNotifications {

    private Map<String, CanonicalTaskJournalEntry> previous;

    public NBTTagList update(List<CanonicalTaskJournalEntry> journal, boolean initial) {
        Map<String, CanonicalTaskJournalEntry> next = new LinkedHashMap<String, CanonicalTaskJournalEntry>();
        NBTTagList events = new NBTTagList();
        for (CanonicalTaskJournalEntry task : journal) {
            String identity = task.getIdentity() + ":" + task.getActivationTime();
            next.put(identity, task);
            if (initial || previous == null) continue;
            CanonicalTaskJournalEntry old = previous.get(identity);
            if (old == null && task.getStatus() != CanonicalTaskInstanceStatus.NOT_STARTED)
                add(events, task, identity, "received", "接到任务", "", "");
            if ((old == null || old.getStatus() != task.getStatus())) {
                if (task.getStatus() == CanonicalTaskInstanceStatus.SETTLED)
                    add(events, task, identity, "completed", "任务完成", "", "");
                else if (task.getStatus() == CanonicalTaskInstanceStatus.ERROR)
                    add(events, task, identity, "failed", "任务失败", "", "");
            }
            Map<String, CanonicalTaskObjectiveStatus> prior = new HashMap<String, CanonicalTaskObjectiveStatus>();
            if (old != null) for (CanonicalTaskJournalObjectiveRow row : old.getObjectiveRows())
                prior.put(row.getObjectiveId(), row.getStatus());
            for (CanonicalTaskJournalObjectiveRow row : task.getObjectiveRows()) {
                if (prior.get(row.getObjectiveId()) == row.getStatus()) continue;
                if (row.getStatus() == CanonicalTaskObjectiveStatus.ACTIVE)
                    add(events, task, identity, "objective_active", "新目标", row.getObjectiveId(), row.getDescription());
                else if (row.getStatus() == CanonicalTaskObjectiveStatus.COMPLETED) add(
                    events,
                    task,
                    identity,
                    "objective_completed",
                    "目标完成",
                    row.getObjectiveId(),
                    row.getDescription());
            }
        }
        previous = next;
        return events;
    }

    private static void add(NBTTagList events, CanonicalTaskJournalEntry task, String identity, String kind,
        String label, String objective, String text) {
        NBTTagCompound event = new NBTTagCompound();
        event.setString("task", identity);
        event.setString("event", identity + ":" + kind + ":" + objective);
        event.setString("kind", kind);
        event.setString("label", label);
        event.setString("title", task.getTitle());
        event.setString("objective", objective);
        event.setString("text", text);
        events.appendTag(event);
    }
}
