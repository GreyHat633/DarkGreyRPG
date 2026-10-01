package darkgrey.rpg.creator;

import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.item.identity.ItemIdentitySavedData;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus;
import darkgrey.rpg.task.journal.CanonicalTaskJournalEntry;
import darkgrey.rpg.task.journal.CanonicalTaskJournalObjectiveRow;

public final class TaskPageServer {

    private TaskPageServer() {}

    public static NBTTagCompound project(EntityPlayerMP player, NBTTagCompound request) {
        if (request.getLong("sequence") <= 0 || request.getInteger("dimension") != player.dimension) return null;
        int cursor = request.getInteger("cursor");
        if (cursor < 0) return null;
        NBTTagCompound result = (NBTTagCompound) request.copy();
        if (request.getInteger("operation") == 1) {
            ItemIdentitySavedData bindings = ItemIdentitySavedData.get();
            if (request.getLong("bindings") != bindings.getRevision()
                || request.getLong("package_revision") != DarkGreyRpg.getProjectRepository()
                    .getSnapshotRevision())
                return null;
            for (CanonicalTaskJournalEntry entry : DarkGreyRpg.getCanonicalTaskManager()
                .getJournal(player)) {
                if (entry.getStatus() != CanonicalTaskInstanceStatus.ACTIVE
                    || entry.getActivationTime() != request.getLong("activation")
                    || !entry.getStoryInstanceId()
                        .equals(request.getString("story"))
                    || !entry.getTaskNodePlacementId()
                        .equals(request.getString("placement")))
                    continue;
                boolean active = false;
                for (CanonicalTaskJournalObjectiveRow row : entry.getObjectives()) if (row.getObjectiveId()
                    .equals(request.getString("objective"))
                    && row.getStatus() == darkgrey.rpg.task.runtime.CanonicalTaskObjectiveStatus.ACTIVE) active = true;
                if (!active) return null;
                CanonicalGraphResource resource = DarkGreyRpg.getProjectRepository()
                    .getSnapshot()
                    .getCanonicalTask(entry.getTaskResourceId());
                if (resource == null) return null;
                for (CanonicalGraphNode node : resource.getGraph()
                    .getNodes())
                    if (node.getId()
                        .equals(request.getString("objective"))) {
                            String type = node.getProperties()
                                .get("objective_type")
                                .getAsString();
                            if (!"collect_item".equals(type) && !"submit_item".equals(type)) return null;
                            NBTTagCompound page = TaskCandidateIndex.page(node, bindings, cursor, 20);
                            result.setTag("items", page.getTag("items"));
                            result.setInteger("next", page.getInteger("next"));
                            result.setInteger("total", page.getInteger("total"));
                            return result;
                        }
            }
            return null;
        }
        int operation = request.getInteger("operation");
        if (operation != 2 && operation != 3 && operation != 4) return null;
        darkgrey.rpg.task.persistence.CanonicalTaskSavedData saved = darkgrey.rpg.task.persistence.CanonicalTaskSavedData
            .get(player);
        if (request.getLong("generation") != saved.getPresentationGeneration()) {
            result.setBoolean("restart", true);
            result.setLong("next_generation", saved.getPresentationGeneration());
            return result;
        }
        NBTTagList history = saved.completedHistory(player.getUniqueID());
        if (operation == 3 || operation == 4) {
            for (int i = 0; i < history.tagCount(); i++) {
                NBTTagCompound record = history.getCompoundTagAt(i);
                if (!record.getString("id")
                    .equals(request.getString("history"))) continue;
                CanonicalGraphResource resource = DarkGreyRpg.getProjectRepository()
                    .getSnapshot()
                    .getCanonicalTask(CanonicalTaskHistoryProjection.resourceId(record));
                if (operation == 4) {
                    ItemIdentitySavedData bindings = ItemIdentitySavedData.get();
                    if (resource == null || record.getTagList("objectives", 10)
                        .tagCount() > 0
                        || request.getLong("bindings") != bindings.getRevision()
                        || request.getLong("package_revision") != DarkGreyRpg.getProjectRepository()
                            .getSnapshotRevision())
                        return null;
                    for (CanonicalGraphNode node : resource.getGraph()
                        .getNodes()) {
                        if (!node.getId()
                            .equals(request.getString("objective")) || !"objective".equals(node.getType())) continue;
                        String type = node.getProperties()
                            .get("objective_type")
                            .getAsString();
                        if (!"collect_item".equals(type) && !"submit_item".equals(type)) return null;
                        NBTTagCompound page = TaskCandidateIndex.page(node, bindings, cursor, 20);
                        result.setTag("items", page.getTag("items"));
                        result.setInteger("next", page.getInteger("next"));
                        result.setInteger("total", page.getInteger("total"));
                        return result;
                    }
                    return null;
                }
                NBTTagCompound details;
                try {
                    details = CanonicalTaskHistoryProjection.details(projectHistory(player, record), cursor);
                } catch (IllegalArgumentException invalid) {
                    result.setString("error", "此记录的目标或说明超过单页预算，请联系作者检查");
                    return result;
                }
                NBTTagCompound paged = details.getCompoundTag("record");
                String field = "current_definition".equals(paged.getString("content_source")) ? "reference_objectives"
                    : "objectives";
                NBTTagList objectives = paged.getTagList(field, 10);
                for (int j = 0; j < objectives.tagCount(); j++) {
                    NBTTagCompound goal = objectives.getCompoundTagAt(j);
                    if (!goal.hasKey("item_preview", 10)) continue;
                    NBTTagCompound preview = goal.getCompoundTag("item_preview");
                    preview.setInteger("operation", 4);
                    preview.setInteger("dimension", player.dimension);
                    preview.setString("history", record.getString("id"));
                    preview.setString("objective", goal.getString("objective_id"));
                    preview.setLong("generation", saved.getPresentationGeneration());
                }
                result.setTag("record", paged);
                result.setInteger("next", details.getInteger("next"));
                result.setInteger("total", details.getInteger("total"));
                return result;
            }
            return null;
        }
        if (cursor > history.tagCount()) return null;
        NBTTagList rows = new NBTTagList();
        int used = 0, next = cursor;
        while (next < history.tagCount() && rows.tagCount() < 20) {
            NBTTagCompound row = (NBTTagCompound) history.getCompoundTagAt(next)
                .copy();
            row.removeTag("objectives");
            TaskStoryPresentation.recover(row);
            row.setString(
                "description",
                darkgrey.rpg.session.forge.DynamicContentResolver.resolve(row.getString("description"), player));
            int weight = TaskCandidateIndex.measured(row);
            if (rows.tagCount() > 0 && used + weight > 65536) break;
            if (weight > 131072) {
                result.setString("error", "此记录的显示数据超过单页预算，请联系作者检查");
                return result;
            }
            rows.appendTag(row);
            used += weight;
            next++;
        }
        result.setTag("rows", rows);
        result.setInteger("next", next);
        result.setInteger("total", history.tagCount());
        return result;
    }

    private static NBTTagCompound projectHistory(EntityPlayerMP player, NBTTagCompound record) {
        return CanonicalTaskHistoryProjection.project(
            record,
            id -> DarkGreyRpg.getProjectRepository()
                .getSnapshot()
                .getCanonicalTask(id),
            text -> darkgrey.rpg.session.forge.DynamicContentResolver.resolve(text, player),
            ItemIdentitySavedData.get());
    }
}
