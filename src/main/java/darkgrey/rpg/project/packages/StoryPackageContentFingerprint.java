package darkgrey.rpg.project.packages;

import java.io.StringReader;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;

import darkgrey.rpg.graph.canonical.CanonicalGraphInterfaceKind;
import darkgrey.rpg.graph.canonical.CanonicalStoryLogicConnection;
import darkgrey.rpg.graph.canonical.CanonicalStoryLogicGraph;
import darkgrey.rpg.graph.canonical.CanonicalStoryLogicGraphLoader;
import darkgrey.rpg.identity.ResourceAddressJson;
import darkgrey.rpg.identity.StoryUid;
import darkgrey.rpg.project.ProjectLoadException;

/** Semantic Story content; archive identity, Group name and editor state are excluded. */
public final class StoryPackageContentFingerprint {

    private StoryPackageContentFingerprint() {}

    public static String compute(StoryPackageManifest manifest, DgrsArchiveReader archive) throws ProjectLoadException {
        Map<String, byte[]> bytes = new LinkedHashMap<String, byte[]>();
        for (List<String> paths : roles(manifest).values())
            for (String path : paths) bytes.put(path, archive.readBytes(path));
        String graphPath = manifest.getRequiredResources()
            .getStoryLogicGraph();
        if (graphPath != null) bytes.put(graphPath, archive.readBytes(graphPath));
        return compute(manifest, bytes, null);
    }

    public static String compute(StoryPackageManifest manifest, Map<String, byte[]> bytes) throws ProjectLoadException {
        return compute(manifest, bytes, null);
    }

    public static String compute(StoryPackageManifest manifest, Map<String, byte[]> bytes,
        CanonicalStoryLogicGraph graph) throws ProjectLoadException {
        try {
            String uid = manifest.getStoryId();
            List<StoryMemberFingerprint.Content> records = new ArrayList<StoryMemberFingerprint.Content>();
            records.add(
                new StoryMemberFingerprint.Content(
                    "runtime_schema",
                    uid,
                    "{\"identity\":\"story-uid-v1\",\"project\":3,\"graph\":2,\"membership\":4,\"actor\":5,\"item\":2}"));
            for (Map.Entry<String, List<String>> role : roles(manifest).entrySet())
                for (String path : role.getValue()) {
                    if (!bytes.containsKey(path))
                        throw new ProjectLoadException("Missing fingerprint definition: " + path);
                    JsonObject json = StrictPackageJson
                        .read(new StringReader(new String(bytes.get(path), StandardCharsets.UTF_8)))
                        .getAsJsonObject();
                    String field = "membership".equals(role.getKey()) ? "story_id"
                        : "actor".equals(role.getKey()) ? ("individual".equals(
                            json.get("type")
                                .getAsString()) ? "npc_id" : "group_id")
                            : "item".equals(role.getKey()) ? "item_id"
                                : "item_group".equals(role.getKey()) ? "group_id" : "id";
                    String identity = "story".equals(role.getKey()) || "membership".equals(role.getKey())
                        ? json.get(field)
                            .getAsString()
                        : ResourceAddressJson.parse(
                            json.get(field)
                                .toString())
                            .toKey();
                    json.remove("tags");
                    json.remove("display_order");
                    if ("membership".equals(role.getKey()))
                        for (String section : new String[] { "owned_resources", "referenced_resources" }) {
                            JsonObject sets = json.getAsJsonObject(section);
                            for (Map.Entry<String, JsonElement> set : new ArrayList<Map.Entry<String, JsonElement>>(
                                sets.entrySet())) {
                                List<JsonElement> values = list(
                                    set.getValue()
                                        .getAsJsonArray());
                                Collections
                                    .sort(values, Comparator.comparing(StoryPackageContentFingerprint::addressKey));
                                sets.add(set.getKey(), array(values));
                            }
                        }
                    if (json.has("graph")) {
                        JsonObject content = json.getAsJsonObject("graph");
                        List<JsonElement> nodes = list(content.getAsJsonArray("nodes"));
                        for (JsonElement entry : nodes) {
                            JsonObject node = entry.getAsJsonObject();
                            String type = node.get("type")
                                .getAsString();
                            if ("end".equals(type) || "terminate".equals(type) || "logic_output".equals(type))
                                node.getAsJsonObject("properties")
                                    .remove("display_order");
                            if ("session".equals(type) || "task".equals(type) || "story".equals(type)) {
                                List<JsonElement> ports = list(node.getAsJsonArray("ports"));
                                for (JsonElement port : ports) port.getAsJsonObject()
                                    .remove("order");
                                Collections.sort(
                                    ports,
                                    Comparator.comparing(
                                        port -> port.getAsJsonObject()
                                            .get("port_id")
                                            .getAsString()));
                                node.add("ports", array(ports));
                            }
                        }
                        Collections.sort(
                            nodes,
                            Comparator.comparing(
                                value -> value.getAsJsonObject()
                                    .get("id")
                                    .getAsString()));
                        content.add("nodes", array(nodes));
                        List<JsonElement> edges = list(content.getAsJsonArray("connections"));
                        Collections.sort(
                            edges,
                            (left, right) -> compareFields(
                                left.getAsJsonObject(),
                                right.getAsJsonObject(),
                                "from_node_id",
                                "from_port_id",
                                "to_node_id",
                                "to_port_id",
                                "interface_kind"));
                        content.add("connections", array(edges));
                    }
                    records.add(new StoryMemberFingerprint.Content(role.getKey(), identity, json.toString()));
                }
            for (String media : manifest.getRequiredResources()
                .getMedia())
                records.add(new StoryMemberFingerprint.Content("media", media, "\"" + media.substring(6, 70) + "\""));
            if (graph == null && manifest.getRequiredResources()
                .getStoryLogicGraph() != null)
                graph = new CanonicalStoryLogicGraphLoader().loadUnresolved(
                    bytes.get(
                        manifest.getRequiredResources()
                            .getStoryLogicGraph()),
                    "fingerprint");
            List<JsonElement> edges = new ArrayList<JsonElement>();
            if (graph != null) for (CanonicalStoryLogicConnection edge : graph.getConnections()) {
                if (!uid.equals(edge.getSourceStoryId()) && !uid.equals(edge.getTargetStoryId())) continue;
                JsonObject value = new JsonObject();
                value.addProperty("source_story_id", edge.getSourceStoryId());
                value.addProperty("source_port_id", edge.getSourcePortId());
                value.addProperty("target_story_id", edge.getTargetStoryId());
                value.addProperty("target_port_id", edge.getTargetPortId());
                value.addProperty(
                    "interface_kind",
                    edge.getInterfaceKind() == CanonicalGraphInterfaceKind.FLOW ? "Flow" : "Logic");
                edges.add(value);
            }
            Collections.sort(
                edges,
                (left, right) -> compareFields(
                    left.getAsJsonObject(),
                    right.getAsJsonObject(),
                    "source_story_id",
                    "source_port_id",
                    "target_story_id",
                    "target_port_id",
                    "interface_kind"));
            for (int i = 0; i < edges.size(); i++) records.add(
                new StoryMemberFingerprint.Content(
                    "story_edge",
                    String.format(java.util.Locale.ROOT, "%08d", i),
                    edges.get(i)
                        .toString()));
            return StoryMemberFingerprint.compute(StoryUid.parse(uid), records);
        } catch (ProjectLoadException exception) {
            throw exception;
        } catch (Exception exception) {
            throw new ProjectLoadException("Cannot fingerprint current Story semantics", exception);
        }
    }

    private static int compareFields(JsonObject left, JsonObject right, String... fields) {
        for (String field : fields) {
            int result = left.get(field)
                .getAsString()
                .compareTo(
                    right.get(field)
                        .getAsString());
            if (result != 0) return result;
        }
        return 0;
    }

    private static String addressKey(JsonElement value) {
        try {
            return ResourceAddressJson.parse(value.toString())
                .toKey();
        } catch (java.io.IOException exception) {
            throw new IllegalArgumentException("Invalid member address", exception);
        }
    }

    private static List<JsonElement> list(JsonArray array) {
        List<JsonElement> result = new ArrayList<JsonElement>();
        for (JsonElement value : array) result.add(value);
        return result;
    }

    private static JsonArray array(List<JsonElement> values) {
        JsonArray result = new JsonArray();
        for (JsonElement value : values) result.add(value);
        return result;
    }

    private static Map<String, List<String>> roles(StoryPackageManifest manifest) {
        StoryPackageManifest.RequiredResources required = manifest.getRequiredResources();
        Map<String, List<String>> roles = new LinkedHashMap<String, List<String>>();
        roles.put("story", required.getCanonicalStories());
        roles.put("actor", required.getActors());
        roles.put("item", required.getItems());
        roles.put("item_group", required.getItemGroups());
        roles.put("session", required.getSessions());
        roles.put("task", required.getTasks());
        roles.put("membership", required.getCanonicalMemberships());
        return roles;
    }
}
