package darkgrey.rpg.project.packages;

import java.io.File;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.project.ProjectLoadException;

/** Read-only diagnostics for the merger's existing identity and byte-equality rules. */
public final class StoryPackageConflictDiagnostics {

    private StoryPackageConflictDiagnostics() {}

    public static List<String> describe(Map<String, LoadedStoryPackage> packages) throws ProjectLoadException {
        if (packages.size() < 2) return Collections.emptyList();
        Map<String, Definition> firstDefinitions = new LinkedHashMap<String, Definition>();
        List<String> lines = new ArrayList<String>();
        int conflicts = 0;
        for (LoadedStoryPackage value : packages.values()) {
            StoryPackageManifest.RequiredResources required = value.getManifest()
                .getRequiredResources();
            Map<String, List<String>> groups = new LinkedHashMap<String, List<String>>();
            groups.put("角色", required.getActors());
            groups.put("物品", required.getItems());
            groups.put("物品组", required.getItemGroups());
            groups.put("对话", required.getDialogues());
            groups.put("旧版任务", required.getQuests());
            groups.put("会话", required.getSessions());
            groups.put("任务", required.getTasks());
            groups.put(
                value.getManifest()
                    .isDgrsV1() ? "故事" : "旧版故事",
                Collections.singletonList(required.getStory()));
            groups.put("故事", union(groups.getOrDefault("故事", Collections.emptyList()), required.getCanonicalStories()));
            groups.put("故事资源归属", required.getCanonicalMemberships());
            for (Map.Entry<String, List<String>> group : groups.entrySet()) {
                for (String path : group.getValue()) {
                    Definition next = new Definition(value, group.getKey(), path);
                    String key = group.getKey() + "/" + next.id;
                    Definition first = firstDefinitions.get(key);
                    if (first == null) {
                        firstDefinitions.put(key, next);
                        continue;
                    }
                    if (first.owner == next.owner) continue;
                    boolean exclusive = "故事".equals(group.getKey()) || "旧版故事".equals(group.getKey())
                        || "故事资源归属".equals(group.getKey());
                    if (!exclusive && Arrays.equals(first.bytes, next.bytes)) continue;
                    conflicts++;
                    lines.add("冲突 " + conflicts + "：" + next.type + "「" + next.name + "」[" + next.id + "]");
                    lines.add(exclusive ? "原因：同一 ID 被多个故事包重复定义。" : "原因：同一 ID 的共享资源定义不一致。");
                    lines.add("A：" + first.context());
                    lines.add("B：" + next.context());
                    lines.add("文件 A：" + first.source());
                    lines.add("文件 B：" + next.source());
                    addTaskUses(lines, "A", first);
                    addTaskUses(lines, "B", next);
                    int before = lines.size();
                    difference(first.json, next.json, "", lines, 0);
                    if (lines.size() == before) {
                        lines.add(exclusive ? "内容相同也不能重复定义此类资源。" : "JSON 字段值相同，但字段顺序、空白或编码不同；共享资源文件必须完全一致。");
                    }
                }
            }
        }
        if (!lines.isEmpty()) {
            lines.add(0, "故事包资源冲突，共 " + conflicts + " 项；本次候选包集合未生效。");
            lines.add("处理：移出不再使用的旧包；需要共存时统一共享定义，或为独立资源使用不同 ID。改文件名不能解决 ID 冲突。");
        }
        return Collections.unmodifiableList(lines);
    }

    private static List<String> union(List<String> first, List<String> second) {
        Set<String> paths = new LinkedHashSet<String>(first);
        paths.addAll(second);
        return new ArrayList<String>(paths);
    }

    private static void addTaskUses(List<String> lines, String side, Definition definition) {
        List<String> uses = new ArrayList<String>();
        for (CanonicalGraphResource task : definition.owner.getSnapshot()
            .getCanonicalTasks()
            .values()) {
            for (CanonicalGraphNode node : task.getGraph()
                .getNodes()) {
                boolean used = false;
                for (JsonElement property : node.getProperties()
                    .values()) {
                    if (references(property, definition.id, 0)) {
                        used = true;
                        break;
                    }
                }
                if (used) uses.add(
                    "任务「" + task.getDisplayName()
                        + "」["
                        + task.getId()
                        + "] / 节点「"
                        + node.getDisplayName()
                        + "」["
                        + node.getId()
                        + "]");
            }
        }
        for (String use : uses) lines.add("使用位置 " + side + "：" + use);
    }

    private static boolean references(JsonElement json, String id, int depth) {
        if (json == null || json.isJsonNull() || depth > 64) return false;
        if (json.isJsonPrimitive()) return json.getAsJsonPrimitive()
            .isString() && id.equals(json.getAsString());
        if (json.isJsonArray()) {
            for (JsonElement child : json.getAsJsonArray()) if (references(child, id, depth + 1)) return true;
        } else if (json.isJsonObject()) {
            if (json.getAsJsonObject()
                .has("story_uid")) try {
                    if (id.equals(
                        darkgrey.rpg.identity.ResourceAddressJson.parse(json.toString())
                            .toKey()))
                        return true;
                } catch (java.io.IOException ignored) {}
            for (Map.Entry<String, JsonElement> child : json.getAsJsonObject()
                .entrySet()) if (references(child.getValue(), id, depth + 1)) return true;
        }
        return false;
    }

    private static void difference(JsonElement a, JsonElement b, String path, List<String> lines, int depth) {
        if (a == null ? b == null : a.equals(b)) return;
        if (depth < 32 && a != null && b != null && a.isJsonObject() && b.isJsonObject()) {
            Set<String> keys = new LinkedHashSet<String>();
            for (Map.Entry<String, JsonElement> e : a.getAsJsonObject()
                .entrySet()) keys.add(e.getKey());
            for (Map.Entry<String, JsonElement> e : b.getAsJsonObject()
                .entrySet()) keys.add(e.getKey());
            for (String key : keys) difference(
                a.getAsJsonObject()
                    .get(key),
                b.getAsJsonObject()
                    .get(key),
                path.isEmpty() ? key : path + "." + key,
                lines,
                depth + 1);
            return;
        }
        if (depth < 32 && a != null && b != null && a.isJsonArray() && b.isJsonArray()) {
            JsonArray left = a.getAsJsonArray(), right = b.getAsJsonArray();
            for (int i = 0; i < Math.max(left.size(), right.size()); i++) {
                JsonElement first = i < left.size() ? left.get(i) : null;
                JsonElement second = i < right.size() ? right.get(i) : null;
                String label = "[" + (i + 1) + "]";
                if (path.endsWith("nodes")) {
                    JsonElement node = first == null ? second : first;
                    if (node != null && node.isJsonObject())
                        label += "节点「" + text(node.getAsJsonObject(), "display_name", "")
                            + "」["
                            + text(node.getAsJsonObject(), "id", "")
                            + "]";
                }
                difference(first, second, path + label, lines, depth + 1);
            }
            return;
        }
        lines.add("不同字段：" + fieldLabel(path) + "；A=" + render(a) + "；B=" + render(b));
    }

    private static String fieldLabel(String path) {
        String label = null;
        if ("home_story_id".equals(path)) label = "所属故事";
        else if ("display_name".equals(path) || "title".equals(path)) label = "显示名称";
        else if ("default_portrait_ref".equals(path)) label = "默认头像";
        else if (path.startsWith("portrait_variants")) label = "头像变体";
        else if (path.endsWith(".required_count")) label = "目标所需数量";
        else if (path.endsWith(".description")) label = "描述";
        return label == null ? path : label + "（" + path + "）";
    }

    private static String render(JsonElement value) {
        return value == null ? "<字段不存在>" : value.toString();
    }

    private static String text(JsonObject json, String key, String fallback) {
        JsonElement value = json.get(key);
        return value != null && value.isJsonPrimitive() ? value.getAsString() : fallback;
    }

    private static final class Definition {

        final LoadedStoryPackage owner;
        final String type, path, id, name;
        final byte[] bytes;
        final JsonObject json;

        Definition(LoadedStoryPackage owner, String type, String path) throws ProjectLoadException {
            this.owner = owner;
            this.type = type;
            this.path = path;
            bytes = owner.getDeclaredResourceBytes(path);
            if (bytes == null) throw new ProjectLoadException("Cannot read declared resource: " + path);
            try {
                json = new JsonParser().parse(new String(bytes, StandardCharsets.UTF_8))
                    .getAsJsonObject();
                String key = "故事资源归属".equals(type) ? "story_id" : "id";
                if ("角色".equals(type) && json.has("type"))
                    key = "individual".equals(text(json, "type", "")) ? "npc_id" : "group_id";
                if ("物品".equals(type)) key = "item_id";
                if ("物品组".equals(type)) key = "group_id";
                id = json.has(key) && json.get(key)
                    .isJsonObject() ? darkgrey.rpg.identity.ResourceAddressJson
                        .parse(
                            json.get(key)
                                .toString())
                        .toKey() : text(json, key, "");
                name = text(json, "display_name", text(json, "title", id));
            } catch (java.io.IOException | RuntimeException failure) {
                throw new ProjectLoadException("Cannot describe declared resource: " + path, failure);
            }
        }

        String context() {
            CanonicalGraphResource story = owner.getSnapshot()
                .getCanonicalStory(owner.getStoryId());
            String storyName = story == null ? (owner.getSnapshot()
                .getStory(owner.getStoryId()) == null ? owner.getStoryId()
                    : owner.getSnapshot()
                        .getStory(owner.getStoryId())
                        .getTitle())
                : story.getDisplayName();
            return "故事「" + storyName + "」[" + owner.getStoryId() + "]；包 " + owner.getPackageId();
        }

        String source() {
            File file = owner.getSourceArchive() == null ? owner.getDirectory() : owner.getSourceArchive();
            return (file == null ? owner.getPackageId() : file.getName()) + "!/" + path;
        }
    }
}
