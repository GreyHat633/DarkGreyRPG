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
        return project(journal, null);
    }

    public static NBTTagCompound project(List<CanonicalTaskJournalEntry> journal,
        net.minecraft.entity.player.EntityPlayerMP player) {
        NBTTagCompound root = new NBTTagCompound();
        NBTTagList tasks = new NBTTagList();
        NBTTagList completedTasks = new NBTTagList();
        for (CanonicalTaskJournalEntry entry : journal) {
            if (entry.getStatus() != CanonicalTaskInstanceStatus.ACTIVE
                && entry.getStatus() != CanonicalTaskInstanceStatus.SETTLED
                && entry.getStatus() != CanonicalTaskInstanceStatus.ERROR) continue;
            NBTTagCompound task = new NBTTagCompound();
            task.setString("id", entry.getIdentity());
            task.setString("title", entry.getTitle());
            task.setString("description", entry.getDescription());
            task.setString("story", entry.getStoryInstanceId());
            task.setString("placement", entry.getTaskNodePlacementId());
            task.setLong("activation", entry.getActivationTime());
            task.setString("tracking_id", entry.getIdentity() + ":" + entry.getActivationTime());
            task.setString(
                "status",
                entry.getStatus()
                    .name());
            if (entry.getSettlementTime() != null) task.setLong("settlement", entry.getSettlementTime());
            darkgrey.rpg.graph.canonical.CanonicalGraphResource resource = player == null ? null
                : darkgrey.rpg.DarkGreyRpg.getProjectRepository()
                    .getSnapshot()
                    .getCanonicalTask(entry.getTaskResourceId());
            NBTTagList objectives = new NBTTagList();
            boolean completed = false;
            for (CanonicalTaskJournalObjectiveRow row : entry.getObjectives()) {
                completed |= row.getStatus() == CanonicalTaskObjectiveStatus.COMPLETED;
                if (row.getStatus() != CanonicalTaskObjectiveStatus.ACTIVE
                    && !(entry.getStatus() != CanonicalTaskInstanceStatus.ACTIVE
                        && row.getStatus() == CanonicalTaskObjectiveStatus.COMPLETED))
                    continue;
                NBTTagCompound objective = new NBTTagCompound();
                objective.setString("text", row.getDescription());
                objective.setString("id", row.getObjectiveId());
                objective.setString("type", row.getObjectiveType());
                objective.setBoolean("submit", "submit_item".equals(row.getObjectiveType()));
                objective.setInteger("current", row.getCurrent());
                objective.setInteger("required", row.getRequired());
                if (resource != null && row.getStatus() == CanonicalTaskObjectiveStatus.ACTIVE
                    && ("collect_item".equals(row.getObjectiveType())
                        || "submit_item".equals(row.getObjectiveType()))) {
                    for (darkgrey.rpg.graph.canonical.CanonicalGraphNode node : resource.getGraph()
                        .getNodes()) {
                        if (node.getId()
                            .equals(row.getObjectiveId())) {
                            objective.setInteger(
                                "held_count",
                                darkgrey.rpg.task.forge.CanonicalTaskInventory.count(
                                    player.inventory.mainInventory,
                                    node,
                                    darkgrey.rpg.item.identity.ItemIdentitySavedData.get()));
                            break;
                        }
                    }
                }
                if (row.getSubmitActorId() != null) objective.setString("submit_actor", row.getSubmitActorId());
                if (row.hasRegionCoordinates()) {
                    objective.setInteger(
                        "x",
                        row.getRegionX()
                            .intValue());
                    objective.setInteger(
                        "y",
                        row.getRegionY()
                            .intValue());
                    objective.setInteger(
                        "z",
                        row.getRegionZ()
                            .intValue());
                }
                objectives.appendTag(objective);
            }
            task.setTag("objectives", objectives);
            task.setBoolean("complete", objectives.tagCount() == 0 && completed && !entry.hasPendingRewards());
            task.setBoolean("pending_rewards", entry.hasPendingRewards());
            if (entry.getStatus() == CanonicalTaskInstanceStatus.ACTIVE) tasks.appendTag(task);
            else completedTasks.appendTag(task);
        }
        root.setTag("tasks", tasks);
        root.setTag("completed_tasks", completedTasks);
        return root;
    }
}
