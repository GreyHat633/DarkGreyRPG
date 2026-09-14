package darkgrey.rpg.graph.canonical;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.Comparator;
import java.util.List;
import java.util.Map;
import java.util.stream.Stream;

import com.google.gson.JsonElement;

/** Executable acceptance matrix for the schema-version-1 canonical graph loader. */
public final class CanonicalGraphResourceLoaderProbe {

    private CanonicalGraphResourceLoaderProbe() {}

    public static void main(String[] args) throws Exception {
        require(args.length == 1, "Expected one repository-local probe directory argument");
        Path root = Paths.get(args[0])
            .toAbsolutePath()
            .normalize();
        delete(root);
        try {
            Path stories = Files.createDirectories(root.resolve("stories"));
            Path sessions = Files.createDirectories(root.resolve("sessions"));
            Path tasks = Files.createDirectories(root.resolve("tasks"));
            write(stories.resolve("story_b.json"), storyJson("story_b"));
            write(stories.resolve("story_a.json"), storyJson("story_a"));
            write(sessions.resolve("session_a.json"), sessionJson("session_a"));
            write(tasks.resolve("task_a.json"), taskJson("task_a"));

            CanonicalGraphResourceLoader loader = new CanonicalGraphResourceLoader();
            verifyValidResources(loader, stories, sessions, tasks);
            Path tagged = stories.resolve("story_a.json");
            write(tagged, storyJson("story_a").replace("\"graph\":", "\"tags\":[\"metadata\"],\"graph\":"));
            require(
                loader.load(tagged, CanonicalGraphResourceKind.STORY)
                    .getSchemaVersion() == 1,
                "Tags changed schema");
            write(tagged, storyJson("story_a"));
            for (String retired : new String[] { "interact_actor", "enter_region", "enter_story" }) {
                expect(
                    loader,
                    stories.resolve("bad.json"),
                    storyJson("bad").replace("\"type\":\"terminate\"", "\"type\":\"" + retired + "\""),
                    "graph.node.type.scope");
            }
            System.out.println("CANONICAL_LOADER_RETIRED_STANDALONE_NODES_REJECTED=PASS");
            verifyEnvelopeFailures(loader, stories);
            verifyGraphFailures(loader, stories, sessions, tasks);
            verifyDirectoryBehavior(loader, root, stories);
            System.out.println("CANONICAL_GRAPH_RESOURCE_PROBE=PASS");
        } finally {
            delete(root);
        }
    }

    private static void verifyValidResources(CanonicalGraphResourceLoader loader, Path stories, Path sessions,
        Path tasks) throws Exception {
        CanonicalGraphResource story = loader.load(stories.resolve("story_a.json"), CanonicalGraphResourceKind.STORY);
        require(story.getSchemaVersion() == 1, "Story schema identity changed");
        require(story.getResourceKind() == CanonicalGraphResourceKind.STORY, "Story kind identity changed");
        require("story_a".equals(story.getId()), "Story id identity changed");
        require("Story story_a".equals(story.getDisplayName()), "Story display name identity changed");
        require(
            story.getGraph()
                .getNodes()
                .size() == 2,
            "Story node count changed");
        require(
            "start".equals(
                story.getGraph()
                    .getNodes()
                    .get(0)
                    .getId()),
            "Story node order changed");
        require(
            "out".equals(
                story.getGraph()
                    .getNodes()
                    .get(0)
                    .getPorts()
                    .get(0)
                    .getId()),
            "Story port identity changed");
        require(
            story.getGraph()
                .getConnections()
                .size() == 1,
            "Story edge count changed");

        CanonicalGraphResource session = loader
            .load(sessions.resolve("session_a.json"), CanonicalGraphResourceKind.SESSION);
        require(session.getResourceKind() == CanonicalGraphResourceKind.SESSION, "Session kind identity changed");
        require(
            "end".equals(
                session.getGraph()
                    .getNodes()
                    .get(1)
                    .getType()),
            "Session node type changed");

        CanonicalGraphResource task = loader.load(tasks.resolve("task_a.json"), CanonicalGraphResourceKind.TASK);
        require(task.getResourceKind() == CanonicalGraphResourceKind.TASK, "Task kind identity changed");
        require(
            task.getGraph()
                .getNodes()
                .size() == 1,
            "Task node count changed");
        require(
            "settle".equals(
                task.getGraph()
                    .getNodes()
                    .get(0)
                    .getType()),
            "Task settle identity changed");
        requireImmutable(story);
    }

    private static void verifyEnvelopeFailures(CanonicalGraphResourceLoader loader, Path stories) throws Exception {
        expect(loader, stories.resolve("bad.json"), storyJson("story_a"), "graph.resource.filename.mismatch");
        expect(
            loader,
            stories.resolve("bad.json"),
            storyJson("bad").replace("\"story\"", "\"dialogue\""),
            "graph.resource.kind.unsupported");
        expect(
            loader,
            stories.resolve("bad.json"),
            storyJson("bad").replace("\"schema_version\":1", "\"schema_version\":2"),
            "graph.resource.schema_version.unsupported");
        expect(
            loader,
            stories.resolve("bad.json"),
            storyJson("bad").replace("\"display_name\":\"Story bad\",", ""),
            "graph.resource.root.member.required");
        expect(
            loader,
            stories.resolve("bad.json"),
            storyJson("bad")
                .replace("\"display_name\":\"Story bad\"", "\"display_name\":\"Story bad\",\"display_name\":\"Again\""),
            "graph.json.member.duplicate");
        expect(
            loader,
            stories.resolve("bad.json"),
            storyJson("bad").replace("\"graph\":", "\"extra\":0,\"graph\":"),
            "graph.resource.root.member.unsupported");
        expect(
            loader,
            stories.resolve("bad.json"),
            storyJson("bad").replace("\"id\":\"bad\"", "\"id\":\" \""),
            "graph.resource.id.required");
        expect(
            loader,
            stories.resolve("bad.json"),
            storyJson("bad").replace("\"display_name\":\"Story bad\"", "\"display_name\":\" \""),
            "graph.resource.display_name.required");
        Path scope = stories.resolve("scope.json");
        write(scope, storyJson("scope").replace("\"story\"", "\"session\""));
        expectExisting(loader, scope, CanonicalGraphResourceKind.STORY, "graph.resource.scope.mismatch");
    }

    private static void verifyGraphFailures(CanonicalGraphResourceLoader loader, Path stories, Path sessions,
        Path tasks) throws Exception {
        String story = storyJson("bad");
        expect(
            loader,
            stories.resolve("bad.json"),
            story.replace("\"nodes\"", "\"extra\""),
            "graph.resource.graph.member.unsupported");
        expect(
            loader,
            stories.resolve("bad.json"),
            story.replace("\"connections\":", "\"extra\":[],\"connections\":"),
            "graph.resource.graph.member.unsupported");
        expect(
            loader,
            stories.resolve("bad.json"),
            story.replace("\"display_name\":\"Start\",", ""),
            "graph.node.member.required");
        expect(
            loader,
            stories.resolve("bad.json"),
            story.replace("\"order\":0", "\"extra\":0"),
            "graph.port.member.unsupported");
        expect(
            loader,
            stories.resolve("bad.json"),
            story.replace("\"interface_kind\":\"flow\"", "\"extra\":\"flow\""),
            "graph.connection.member.unsupported");
        expect(
            loader,
            stories.resolve("bad.json"),
            story.replace(
                "\"nodes\":[",
                "\"nodes\":[{\"id\":\"start\",\"type\":\"start\",\"display_name\":\"Duplicate\",\"ports\":[],\"properties\":{}},"),
            "graph.node.id.duplicate");
        expect(
            loader,
            stories.resolve("bad.json"),
            story.replace(
                "\"ports\":[{",
                "\"ports\":[{\"port_id\":\"out\",\"display_name\":\"Duplicate\",\"direction\":\"output\",\"kind\":\"flow\",\"order\":1},{"),
            "graph.port.id.duplicate");
        expect(
            loader,
            stories.resolve("bad.json"),
            story.replace("\"to_node_id\":\"end\"", "\"to_node_id\":\"missing\""),
            "graph.connection.to.node.missing");
        expect(
            loader,
            stories.resolve("bad.json"),
            story.replace("\"from_node_id\":\"start\"", "\"from_node_id\":\"missing\""),
            "graph.connection.from.node.missing");
        expect(
            loader,
            stories.resolve("bad.json"),
            story.replace("\"from_port_id\":\"out\"", "\"from_port_id\":\"missing\""),
            "graph.connection.from.port.missing");
        expect(
            loader,
            stories.resolve("bad.json"),
            story.replace("\"to_port_id\":\"in\"", "\"to_port_id\":\"missing\""),
            "graph.connection.to.port.missing");
        expect(
            loader,
            stories.resolve("bad.json"),
            story.replaceFirst("\"direction\":\"output\"", "\"direction\":\"input\""),
            "graph.connection.source.direction");
        expect(
            loader,
            stories.resolve("bad.json"),
            replaceLast(story, "\"direction\":\"input\"", "\"direction\":\"output\""),
            "graph.connection.target.direction");
        expect(
            loader,
            stories.resolve("bad.json"),
            story.replaceFirst("\"kind\":\"flow\"", "\"kind\":\"logic\""),
            "graph.connection.source.kind.mismatch");
        expect(
            loader,
            stories.resolve("bad.json"),
            replaceLast(story, "\"kind\":\"flow\"", "\"kind\":\"logic\""),
            "graph.connection.target.kind.mismatch");
        expect(
            loader,
            stories.resolve("bad.json"),
            story.replace("\"interface_kind\":\"flow\"", "\"interface_kind\":\"other\""),
            "graph.port.kind.invalid");
        expect(loader, stories.resolve("bad.json"), duplicateStoryEdge(story), "graph.connection.duplicate");
        expect(
            loader,
            stories.resolve("bad.json"),
            flowCardinalityStory("bad"),
            "graph.connection.flow.output.multiple_targets");
        expect(
            loader,
            stories.resolve("bad.json"),
            story.replace("\"type\":\"terminate\"", "\"type\":\"line\""),
            "graph.node.type.scope");
        expect(
            loader,
            stories.resolve("bad.json"),
            story.replace("\"type\":\"start\"", "\"type\":\"action\""),
            "graph.node.required.unique");
        expect(loader, stories.resolve("bad.json"), duplicateRequiredStory("bad"), "graph.node.required.unique");

        String task = taskJson("bad_task");
        expect(
            loader,
            tasks.resolve("bad_task.json"),
            legacyTaskJson("bad_task"),
            "graph.resource.task.legacy_activate");
        expect(
            loader,
            tasks.resolve("bad_task.json"),
            task.replace("\"settle\"", "\"objective\""),
            "graph.node.required.unique");
        expect(loader, tasks.resolve("bad_task.json"), duplicateTaskSettle("bad_task"), "graph.node.required.unique");
        expect(
            loader,
            tasks.resolve("bad_task.json"),
            taskFlowJson("bad_task"),
            "graph.resource.task.flow.unsupported");
        expect(
            loader,
            tasks.resolve("bad_task.json"),
            logicMultipleSourcesTask("bad_task"),
            "graph.connection.logic.input.multiple_sources");
        expect(loader, tasks.resolve("bad_task.json"), logicCycleTask("bad_task"), "graph.logic.cycle");

        String compatibility = sessionJson("compatibility").replace("\"type\":\"end\"", "\"type\":\"legacy_jump\"");
        write(sessions.resolve("compatibility.json"), compatibility);
        require(
            loader.load(sessions.resolve("compatibility.json"), CanonicalGraphResourceKind.SESSION)
                .getGraph()
                .getNodes()
                .get(1)
                .getType()
                .equals("legacy_jump"),
            "legacy_jump compatibility load failed");
    }

    private static void verifyDirectoryBehavior(CanonicalGraphResourceLoader loader, Path root, Path stories)
        throws Exception {
        Files.deleteIfExists(stories.resolve("bad.json"));
        Files.deleteIfExists(stories.resolve("scope.json"));
        List<CanonicalGraphResource> loaded = loader.loadDirectory(stories, CanonicalGraphResourceKind.STORY);
        require(loaded.size() == 2, "Directory resource count changed");
        require(
            "story_a".equals(
                loaded.get(0)
                    .getId())
                && "story_b".equals(
                    loaded.get(1)
                        .getId()),
            "Directory filename order is not ordinal");
        try {
            loaded.clear();
            throw new AssertionError("directory result is mutable");
        } catch (UnsupportedOperationException expected) {}

        Path absent = root.resolve("absent");
        expectDirectory(loader, absent, CanonicalGraphResourceKind.STORY);
        require(!Files.exists(absent), "Missing directory was created");
    }

    private static void requireImmutable(CanonicalGraphResource resource) {
        try {
            resource.getGraph()
                .getNodes()
                .clear();
            throw new AssertionError("nodes are mutable");
        } catch (UnsupportedOperationException expected) {}
        try {
            resource.getGraph()
                .getConnections()
                .clear();
            throw new AssertionError("connections are mutable");
        } catch (UnsupportedOperationException expected) {}
        try {
            resource.getGraph()
                .getNodes()
                .get(0)
                .getPorts()
                .clear();
            throw new AssertionError("ports are mutable");
        } catch (UnsupportedOperationException expected) {}
        Map<String, JsonElement> first = resource.getGraph()
            .getNodes()
            .get(0)
            .getProperties();
        try {
            first.clear();
            throw new AssertionError("properties are mutable");
        } catch (UnsupportedOperationException expected) {}
        first.get("metadata")
            .getAsJsonObject()
            .addProperty("label", "mutated");
        String restored = resource.getGraph()
            .getNodes()
            .get(0)
            .getProperties()
            .get("metadata")
            .getAsJsonObject()
            .get("label")
            .getAsString();
        require("source".equals(restored), "nested property JSON aliases mutable loader state");
    }

    private static void expect(CanonicalGraphResourceLoader loader, Path file, String json, String code)
        throws Exception {
        write(file, json);
        expectExisting(loader, file, null, code);
    }

    private static void expectExisting(CanonicalGraphResourceLoader loader, Path file,
        CanonicalGraphResourceKind expectedKind, String code) throws Exception {
        try {
            if (expectedKind == null) loader.load(file);
            else loader.load(file, expectedKind);
            throw new AssertionError("Expected " + code);
        } catch (CanonicalGraphResourceException exception) {
            require(code.equals(exception.getCode()), "Expected " + code + ", got " + exception.getCode());
        }
    }

    private static void expectDirectory(CanonicalGraphResourceLoader loader, Path directory,
        CanonicalGraphResourceKind kind) throws Exception {
        try {
            loader.loadDirectory(directory, kind);
            throw new AssertionError("Expected missing directory");
        } catch (CanonicalGraphResourceException exception) {
            require("graph.resource.directory.missing".equals(exception.getCode()), "Unexpected directory code");
        }
    }

    private static String storyJson(String id) {
        return "{\"schema_version\":1,\"resource_kind\":\"story\",\"id\":\"" + id
            + "\",\"display_name\":\"Story "
            + id
            + "\",\"graph\":{\"nodes\":[{\"id\":\"start\",\"type\":\"start\",\"display_name\":\"Start\",\"ports\":[{\"port_id\":\"out\",\"display_name\":\"Out\",\"direction\":\"output\",\"kind\":\"flow\",\"order\":0}],\"properties\":{\"metadata\":{\"label\":\"source\"}}},{\"id\":\"end\",\"type\":\"terminate\",\"display_name\":\"End\",\"ports\":[{\"port_id\":\"in\",\"display_name\":\"In\",\"direction\":\"input\",\"kind\":\"flow\",\"order\":0}],\"properties\":{}}],\"connections\":["
            + edge("start", "out", "end", "in", "flow")
            + "]}}";
    }

    private static String sessionJson(String id) {
        return storyJson(id).replace("\"resource_kind\":\"story\"", "\"resource_kind\":\"session\"")
            .replace("\"display_name\":\"Story " + id + "\"", "\"display_name\":\"Session " + id + "\"")
            .replace("\"type\":\"terminate\"", "\"type\":\"end\"");
    }

    private static String taskJson(String id) {
        return "{\"schema_version\":1,\"resource_kind\":\"task\",\"id\":\"" + id
            + "\",\"display_name\":\"Task "
            + id
            + "\",\"graph\":{\"nodes\":[{\"id\":\"settle\",\"type\":\"settle\",\"display_name\":\"Settle\",\"ports\":[],\"properties\":{}}],\"connections\":[]}}";
    }

    private static String legacyTaskJson(String id) {
        return taskJson(id).replace(
            "\"nodes\":[",
            "\"nodes\":[{\"id\":\"activate\",\"type\":\"activate\",\"display_name\":\"Activate\",\"ports\":[{\"port_id\":\"logic_out\",\"display_name\":\"Out\",\"direction\":\"output\",\"kind\":\"logic\",\"order\":0}],\"properties\":{}},");
    }

    private static String duplicateStoryEdge(String json) {
        String value = edge("start", "out", "end", "in", "flow");
        return json.replace("\"connections\":[" + value + "]", "\"connections\":[" + value + "," + value + "]");
    }

    private static String flowCardinalityStory(String id) {
        String json = storyJson(id);
        String second = "{\"id\":\"end_two\",\"type\":\"terminate\",\"display_name\":\"End Two\",\"ports\":[{\"port_id\":\"in\",\"display_name\":\"In\",\"direction\":\"input\",\"kind\":\"flow\",\"order\":0}],\"properties\":{}}";
        return json.replace("],\"connections\":[", "," + second + "],\"connections\":[")
            .replace("]}}", "," + edge("start", "out", "end_two", "in", "flow") + "]}}");
    }

    private static String duplicateRequiredStory(String id) {
        String duplicate = "{\"id\":\"start_two\",\"type\":\"start\",\"display_name\":\"Start Two\",\"ports\":[],\"properties\":{}}";
        return storyJson(id).replace("\"nodes\":[", "\"nodes\":[" + duplicate + ",");
    }

    private static String duplicateTaskSettle(String id) {
        String duplicate = "{\"id\":\"settle_two\",\"type\":\"settle\",\"display_name\":\"Settle Two\",\"ports\":[],\"properties\":{}}";
        return taskJson(id).replace("\"nodes\":[", "\"nodes\":[" + duplicate + ",");
    }

    private static String taskFlowJson(String id) {
        return taskJson(id).replace(
            "\"ports\":[]",
            "\"ports\":[{\"port_id\":\"out\",\"display_name\":\"Out\",\"direction\":\"output\",\"kind\":\"flow\",\"order\":0}]");
    }

    private static String logicMultipleSourcesTask(String id) {
        String edges = edge("input", "out", "settle", "in", "logic") + ","
            + edge("other", "out", "settle", "in", "logic");
        return taskEnvelope(id, taskLogicNodes(), edges);
    }

    private static String logicCycleTask(String id) {
        String edges = edge("input", "out", "settle", "in", "logic") + ","
            + edge("other", "out", "third", "in", "logic")
            + ","
            + edge("third", "out", "other", "in", "logic");
        return taskEnvelope(id, taskLogicNodes(), edges);
    }

    private static String taskLogicNodes() {
        return "{\"id\":\"input\",\"type\":\"logic_input\",\"display_name\":\"Input\",\"ports\":[{\"port_id\":\"out\",\"display_name\":\"Out\",\"direction\":\"output\",\"kind\":\"logic\",\"order\":0}],\"properties\":{\"port_id\":\"input\",\"display_name\":\"Input\"}},"
            + "{\"id\":\"other\",\"type\":\"not\",\"display_name\":\"Other\",\"ports\":[{\"port_id\":\"in\",\"display_name\":\"In\",\"direction\":\"input\",\"kind\":\"logic\",\"order\":0},{\"port_id\":\"out\",\"display_name\":\"Out\",\"direction\":\"output\",\"kind\":\"logic\",\"order\":1}],\"properties\":{}},"
            + "{\"id\":\"third\",\"type\":\"not\",\"display_name\":\"Third\",\"ports\":[{\"port_id\":\"in\",\"display_name\":\"In\",\"direction\":\"input\",\"kind\":\"logic\",\"order\":0},{\"port_id\":\"out\",\"display_name\":\"Out\",\"direction\":\"output\",\"kind\":\"logic\",\"order\":1}],\"properties\":{}},"
            + "{\"id\":\"settle\",\"type\":\"settle\",\"display_name\":\"Settle\",\"ports\":[{\"port_id\":\"in\",\"display_name\":\"In\",\"direction\":\"input\",\"kind\":\"logic\",\"order\":0}],\"properties\":{}}";
    }

    private static String taskEnvelope(String id, String nodes, String edges) {
        return "{\"schema_version\":1,\"resource_kind\":\"task\",\"id\":\"" + id
            + "\",\"display_name\":\"Task "
            + id
            + "\",\"graph\":{\"nodes\":["
            + nodes
            + "],\"connections\":["
            + edges
            + "]}}";
    }

    private static String edge(String fromNode, String fromPort, String toNode, String toPort, String kind) {
        return "{\"from_node_id\":\"" + fromNode
            + "\",\"from_port_id\":\""
            + fromPort
            + "\",\"to_node_id\":\""
            + toNode
            + "\",\"to_port_id\":\""
            + toPort
            + "\",\"interface_kind\":\""
            + kind
            + "\"}";
    }

    private static String replaceLast(String text, String oldValue, String newValue) {
        int index = text.lastIndexOf(oldValue);
        require(index >= 0, "Replacement source was not found");
        return text.substring(0, index) + newValue + text.substring(index + oldValue.length());
    }

    private static void write(Path path, String text) throws IOException {
        Files.write(path, text.getBytes(StandardCharsets.UTF_8));
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }

    private static void delete(Path path) throws IOException {
        if (!Files.exists(path)) return;
        try (Stream<Path> paths = Files.walk(path)) {
            Path[] ordered = paths.sorted(Comparator.reverseOrder())
                .toArray(Path[]::new);
            for (Path item : ordered) Files.delete(item);
        }
    }
}
