package darkgrey.rpg.creator;

import java.util.Map;
import java.util.function.Function;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

import com.google.gson.JsonElement;

import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.item.identity.ItemIdentitySavedData;

/** Detached current-definition reference. It is never a historical completion or runtime. */
public final class CanonicalTaskHistoryProjection {

    private CanonicalTaskHistoryProjection() {}

    /** Every objective remains reachable through a byte-bounded detail page. */
    public static NBTTagCompound details(NBTTagCompound projected, int cursor) {
        String field = "current_definition".equals(projected.getString("content_source")) ? "reference_objectives"
            : "objectives";
        NBTTagList source = projected.getTagList(field, 10);
        if (cursor < 0 || cursor > source.tagCount())
            throw new IllegalArgumentException("Invalid history detail cursor");
        NBTTagList rows = new NBTTagList();
        int next = cursor, used = 0;
        while (next < source.tagCount() && rows.tagCount() < 20) {
            NBTTagCompound row = source.getCompoundTagAt(next);
            int weight = TaskCandidateIndex.measured(row);
            if (rows.tagCount() > 0 && used + weight > 65536) break;
            if (weight > 65536) throw new IllegalArgumentException("History objective exceeds detail budget");
            rows.appendTag(row.copy());
            used += weight;
            next++;
        }
        NBTTagCompound record = new NBTTagCompound();
        for (Object name : projected.func_150296_c())
            if (!"objectives".equals(name) && !"reference_objectives".equals(name)) record.setTag(
                (String) name,
                projected.getTag((String) name)
                    .copy());
        record.setTag(field, rows);
        NBTTagCompound result = new NBTTagCompound();
        result.setTag("record", record);
        result.setInteger("next", next);
        result.setInteger("total", source.tagCount());
        if (TaskCandidateIndex.measured(result) > 100000)
            throw new IllegalArgumentException("History metadata exceeds detail budget");
        return result;
    }

    public static String resourceId(NBTTagCompound row) {
        if (row.hasKey("task_resource_id", 8)) return row.getString("task_resource_id");
        String id = row.getString("id");
        if (!id.startsWith("history:")) return "";
        int offset = 8;
        String[] parts = new String[4];
        try {
            for (int i = 0; i < parts.length; i++) {
                int separator = id.indexOf(':', offset);
                if (separator < offset || separator - offset > 9) return "";
                int length = Integer.parseInt(id.substring(offset, separator));
                offset = separator + 1;
                if (length < 0 || length > id.length() - offset) return "";
                parts[i] = id.substring(offset, offset + length);
                offset += length;
            }
            return offset == id.length() && parts[0].equals(row.getString("player")) ? parts[3] : "";
        } catch (NumberFormatException invalid) {
            return "";
        }
    }

    public static NBTTagCompound project(NBTTagCompound history, Function<String, CanonicalGraphResource> resources,
        Function<String, String> text, ItemIdentitySavedData bindings) {
        NBTTagCompound row = (NBTTagCompound) history.copy();
        row.setString("description", text.apply(row.getString("description")));
        NBTTagList historical = row.getTagList("objectives", 10);
        if (historical.tagCount() > 0) {
            row.setString("content_source", "snapshot");
            for (int i = 0; i < historical.tagCount(); i++) {
                NBTTagCompound goal = historical.getCompoundTagAt(i);
                goal.setString("text", text.apply(goal.getString("text")));
            }
            return row;
        }
        String id = resourceId(row);
        CanonicalGraphResource resource = id.isEmpty() ? null : resources.apply(id);
        if (resource == null) {
            row.setString("content_source", "missing");
            return row;
        }
        row.setString("content_source", "current_definition");
        row.setString(
            "reference_description",
            resource.getTaskMetadata() == null ? ""
                : text.apply(
                    resource.getTaskMetadata()
                        .getDescription()));
        NBTTagList goals = new NBTTagList();
        for (CanonicalGraphNode node : resource.getGraph()
            .getNodes()) {
            if (!"objective".equals(node.getType())) continue;
            Map<String, JsonElement> properties = node.getProperties();
            NBTTagCompound goal = new NBTTagCompound();
            goal.setString("objective_id", node.getId());
            String type = properties.get("objective_type")
                .getAsString();
            goal.setString("type", type);
            goal.setString(
                "text",
                text.apply(
                    properties.get("description")
                        .getAsString()));
            goal.setInteger(
                "required",
                properties.containsKey("required") ? properties.get("required")
                    .getAsInt() : 1);
            if ("collect_item".equals(type) || "submit_item".equals(type))
                goal.setTag("item_preview", TaskItemPreview.project(node, bindings));
            goal.setTag("reward_preview", TaskRewardPreview.project(resource, node.getId(), bindings));
            goals.appendTag(goal);
        }
        row.setTag("reference_objectives", goals);
        return row;
    }
}
