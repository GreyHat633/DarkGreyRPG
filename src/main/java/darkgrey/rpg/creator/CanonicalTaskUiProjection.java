package darkgrey.rpg.creator;

import java.util.List;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

import darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus;
import darkgrey.rpg.task.journal.CanonicalTaskJournalEntry;
import darkgrey.rpg.task.journal.CanonicalTaskJournalObjectiveRow;
import darkgrey.rpg.task.runtime.CanonicalTaskObjectiveStatus;

/** Read-only player presentation over the canonical Journal, never the legacy DTO. */
public final class CanonicalTaskUiProjection {

    private CanonicalTaskUiProjection() {}

    public static NBTTagCompound project(List<CanonicalTaskJournalEntry> journal) {
        NBTTagCompound root = new NBTTagCompound();
        NBTTagList tasks = new NBTTagList();
        for (CanonicalTaskJournalEntry entry : journal) {
            if (entry.getStatus() != CanonicalTaskInstanceStatus.ACTIVE) continue;
            NBTTagCompound task = new NBTTagCompound();
            task.setString("id", entry.getIdentity());
            task.setString("title", entry.getTitle());
            NBTTagList objectives = new NBTTagList();
            boolean completed = false;
            for (CanonicalTaskJournalObjectiveRow row : entry.getObjectives()) {
                completed |= row.getStatus() == CanonicalTaskObjectiveStatus.COMPLETED;
                if (row.getStatus() != CanonicalTaskObjectiveStatus.ACTIVE) continue;
                NBTTagCompound objective = new NBTTagCompound();
                objective.setString("text", row.getDescription());
                objective.setInteger("current", row.getCurrent());
                objective.setInteger("required", row.getRequired());
                objectives.appendTag(objective);
            }
            task.setTag("objectives", objectives);
            task.setBoolean("complete", objectives.tagCount() == 0 && completed);
            tasks.appendTag(task);
        }
        root.setTag("tasks", tasks);
        return root;
    }
}
