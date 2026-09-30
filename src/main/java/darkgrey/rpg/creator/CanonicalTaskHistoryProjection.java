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
