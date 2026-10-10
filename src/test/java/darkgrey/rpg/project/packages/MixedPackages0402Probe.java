package darkgrey.rpg.project.packages;

import java.io.File;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.Arrays;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.zip.ZipEntry;
import java.util.zip.ZipOutputStream;

import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonPrimitive;

import darkgrey.rpg.graph.canonical.CanonicalGraphConnection;
import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphPort;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalStoryMembership;
import darkgrey.rpg.graph.canonical.CanonicalStoryMembershipSet;
import darkgrey.rpg.persistence.StabilityMixed0402Fixtures;
import darkgrey.rpg.project.ProjectSnapshot;

/** Exports and reads legal current single containers for S load; not a Studio UI export claim. */
public final class MixedPackages0402Probe {

    private MixedPackages0402Probe() {}

    public static void main(String[] args) throws Exception {
        File root = new File(args[0]).getCanonicalFile();
        if (root.exists()) throw new IllegalStateException("Preserve prior generated package evidence");
        Files.createDirectories(root.toPath());
        ProjectSnapshot project = StabilityMixed0402Fixtures.project();
        JsonObject actor = CurrentPackageProbeFixtures.actor(StabilityMixed0402Fixtures.ACTOR, "Load clerk", "");
        actor.addProperty("home_story_id", StabilityMixed0402Fixtures.STORY);
        JsonObject item = new JsonObject();
        item.addProperty("schema_version", 2);
        item.addProperty("identity_format", "story-uid-v1");
        item.addProperty("type", "individual");
        item.addProperty("display_name", "Load coin");
        item.add("item_id", CurrentPackageProbeFixtures.address(StabilityMixed0402Fixtures.ITEM));
        item.add("tags", new JsonArray());
        for (int kind = 0; kind < 3; kind++) {
            String uid = StabilityMixed0402Fixtures.story(kind);
            String taskPath = "resources/canonical/tasks/" + uid + "/work.json";
            Map<String, byte[]> entries = new LinkedHashMap<>();
            JsonObject manifest = CurrentPackageProbeFixtures.manifest(uid);
            manifest.addProperty("producer_version", "0.4.0.2");
            manifest.addProperty("package_version", "0.4.0.2");
            JsonObject resources = manifest.getAsJsonObject("required_resources");
            resources.add("actors", CurrentPackageProbeFixtures.list("actors/clerk.json"));
            resources.add("items", CurrentPackageProbeFixtures.list("items/coin.json"));
            resources.add("tasks", CurrentPackageProbeFixtures.list(taskPath));
            put(entries, "manifest.json", manifest);
            put(entries, "project.json", CurrentPackageProbeFixtures.project());
            put(entries, "actors/clerk.json", actor);
            put(entries, "items/coin.json", item);
            put(entries, CurrentPackageProbeFixtures.storyPath(uid), resource(project.getCanonicalStory(uid)));
            put(entries, taskPath, resource(project.getCanonicalTask(uid + "~task~work")));
            CanonicalStoryMembership member = project.getCanonicalStoryMembership(uid);
            JsonObject membership = CurrentPackageProbeFixtures.membership(uid);
            membership.add("owned_resources", memberSet(member.getOwned()));
            membership.add("referenced_resources", memberSet(member.getReferenced()));
            JsonObject order = new JsonObject();
            for (String type : Arrays.asList("actors", "items", "sessions", "tasks")) order.add(type, new JsonArray());
            membership.add("display_order", order);
            put(entries, CurrentPackageProbeFixtures.membershipPath(uid), membership);
            File file = new File(root, uid + ".dgrs");
            try (ZipOutputStream archive = new ZipOutputStream(Files.newOutputStream(file.toPath()))) {
                for (Map.Entry<String, byte[]> entry : entries.entrySet()) {
                    ZipEntry zip = new ZipEntry(entry.getKey());
                    zip.setTime(0);
                    archive.putNextEntry(zip);
                    archive.write(entry.getValue());
                    archive.closeEntry();
                }
            }
        }
        StoryPackageLoader loader = new StoryPackageLoader(root);
        StoryPackageLoader.ReloadResult loaded = loader.reload();
        if (!loaded.isSuccessful() || loader.getPackages()
            .size() != 3) throw new AssertionError(loaded.getSummary() + " " + loaded.getErrors());
        ProjectSnapshot restored = StoryPackageSnapshotMerger.merge(loader.getPackages());
        for (int kind = 0; kind < 3; kind++) if (!loader.allowsNewStart(StabilityMixed0402Fixtures.story(kind))
            || restored.getCanonicalTask(StabilityMixed0402Fixtures.story(kind) + "~task~work") == null)
            throw new AssertionError("Generated admission/closure");
        System.out.println(
            "MIXED_PACKAGES_0402=PASS current typed references, ownership, external metadata, real loader admission packages=3 layer=A generated-load-only");
    }

    private static JsonObject resource(CanonicalGraphResource resource) {
        JsonObject root = new JsonObject();
        root.addProperty("schema_version", 3);
        root.addProperty("identity_format", "story-uid-v1");
        root.addProperty(
            "resource_kind",
            resource.getKind()
                .name()
                .toLowerCase(java.util.Locale.ROOT));
        root.add("id", wire(new JsonPrimitive(resource.getId())));
        root.addProperty("display_name", resource.getDisplayName());
        JsonObject graph = new JsonObject();
        JsonArray nodes = new JsonArray(), connections = new JsonArray();
        for (CanonicalGraphNode node : resource.getGraph()
            .getNodes()) {
            JsonObject value = new JsonObject();
            value.addProperty("id", node.getId());
            value.addProperty("type", node.getType());
            value.addProperty("display_name", node.getDisplayName());
            JsonArray ports = new JsonArray();
            for (CanonicalGraphPort port : node.getPorts()) {
                JsonObject field = new JsonObject();
                field.addProperty("port_id", port.getPortId());
                field.addProperty("display_name", port.getDisplayName());
                field.addProperty(
                    "direction",
                    port.getDirection()
                        .name()
                        .toLowerCase(java.util.Locale.ROOT));
                field.addProperty(
                    "kind",
                    port.getInterfaceKind()
                        .name()
                        .toLowerCase(java.util.Locale.ROOT));
                field.addProperty("order", port.getOrder());
                ports.add(field);
            }
            value.add("ports", ports);
            JsonObject fields = new JsonObject();
            for (Map.Entry<String, JsonElement> field : node.getProperties()
                .entrySet()) fields.add(field.getKey(), wire(field.getValue()));
            value.add("properties", fields);
            nodes.add(value);
        }
        for (CanonicalGraphConnection edge : resource.getGraph()
            .getConnections()) {
            JsonObject value = new JsonObject();
            value.addProperty("from_node_id", edge.getFromNodeId());
            value.addProperty("from_port_id", edge.getFromPortId());
            value.addProperty("to_node_id", edge.getToNodeId());
            value.addProperty("to_port_id", edge.getToPortId());
            value.addProperty(
                "interface_kind",
                edge.getInterfaceKind()
                    .name()
                    .toLowerCase(java.util.Locale.ROOT));
            connections.add(value);
        }
        graph.add("nodes", nodes);
        graph.add("connections", connections);
        root.add("graph", graph);
        return root;
    }

    private static JsonObject memberSet(CanonicalStoryMembershipSet member) {
        JsonObject result = new JsonObject();
        List<List<String>> values = Arrays.asList(
            member.getActors(),
            member.getItems(),
            member.getItemGroups(),
            member.getSessions(),
            member.getTasks());
        List<String> names = Arrays.asList("actors", "items", "item_groups", "sessions", "tasks");
        for (int i = 0; i < names.size(); i++) {
            JsonArray array = new JsonArray();
            for (String id : values.get(i)) array.add(CurrentPackageProbeFixtures.address(id));
            result.add(names.get(i), array);
        }
        return result;
    }

    private static JsonElement wire(JsonElement value) {
        if (value.isJsonPrimitive() && value.getAsJsonPrimitive()
            .isString()
            && value.getAsString()
                .startsWith("ST-")
            && value.getAsString()
                .contains("~"))
            return CurrentPackageProbeFixtures.address(value.getAsString());
        if (value.isJsonObject()) {
            JsonObject copy = new JsonObject();
            for (Map.Entry<String, JsonElement> field : value.getAsJsonObject()
                .entrySet()) copy.add(field.getKey(), wire(field.getValue()));
            return copy;
        }
        if (value.isJsonArray()) {
            JsonArray copy = new JsonArray();
            for (JsonElement entry : value.getAsJsonArray()) copy.add(wire(entry));
            return copy;
        }
        return value;
    }

    private static void put(Map<String, byte[]> entries, String name, JsonObject data) {
        entries.put(
            name,
            data.toString()
                .getBytes(StandardCharsets.UTF_8));
    }
}
