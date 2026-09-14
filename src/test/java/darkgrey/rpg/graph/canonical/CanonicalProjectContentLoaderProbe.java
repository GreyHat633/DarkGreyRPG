package darkgrey.rpg.graph.canonical;

import java.io.DataInputStream;
import java.io.IOException;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.Arrays;
import java.util.Collections;
import java.util.Comparator;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.stream.Stream;

import com.google.gson.JsonElement;
import com.google.gson.JsonPrimitive;

/** Executable acceptance matrix for the complete canonical project-content loader. */
public final class CanonicalProjectContentLoaderProbe {

    private CanonicalProjectContentLoaderProbe() {}

    public static void main(String[] args) throws Exception {
        require(args.length == 1, "Expected one repository-local probe directory argument");
        Path project = Paths.get(args[0])
            .toAbsolutePath()
            .normalize();
        delete(project);
        try {
            verifyAbsentDoesNotCreate(project);
            verifyRootMustBeDirectory(project);
            prepare(project);
            CanonicalProjectContentLoader loader = new CanonicalProjectContentLoader();
            verifyValid(loader, project);
            verifyStoryLogicGraph(loader, project);
            verifyMissingDirectory(loader, project);
            prepare(project);
            verifyMalformedPropagation(loader, project);
            prepare(project);
            verifyPairs(loader, project);
            prepare(project);
            verifyTargets(loader, project);
            prepare(project);
            verifyDuplicateOwnership(loader, project);
            verifyJava8Bytecode();
            System.out.println("CANONICAL_PROJECT_CONTENT_PROBE=PASS");
        } finally {
            delete(project);
        }
    }

    private static void verifyAbsentDoesNotCreate(Path project) {
        CanonicalProjectContent empty = new CanonicalProjectContentLoader()
            .load(project, Collections.singleton("actor_a"));
        require(empty == CanonicalProjectContent.empty(), "Absent canonical root did not return stable empty snapshot");
        require(!Files.exists(project), "Absent canonical load created a path");
    }

    private static void prepare(Path project) throws IOException {
        Path canonical = project.resolve("resources")
            .resolve("canonical");
        Files.createDirectories(canonical.resolve("stories"));
        Files.createDirectories(canonical.resolve("sessions"));
        Files.createDirectories(canonical.resolve("tasks"));
        Files.createDirectories(canonical.resolve("memberships"));
        write(canonical.resolve("stories/story_b.json"), storyJson("story_b"));
        write(canonical.resolve("stories/story_a.json"), storyJson("story_a"));
        write(canonical.resolve("sessions/session_b.json"), sessionJson("session_b"));
        write(canonical.resolve("sessions/session_a.json"), sessionJson("session_a"));
        write(canonical.resolve("tasks/task_b.json"), taskJson("task_b"));
        write(canonical.resolve("tasks/task_a.json"), taskJson("task_a"));
        write(
            canonical.resolve("memberships/story_b.json"),
            membershipJson("story_b", "actor_c", "session_b", "task_b", "actor_a", "session_a", "task_a"));
        write(
            canonical.resolve("memberships/story_a.json"),
            membershipJson("story_a", "actor_a", "session_a", "task_a", "actor_b", "session_b", "task_b"));
    }

    private static void verifyRootMustBeDirectory(Path project) throws IOException {
        Path root = project.resolve("resources")
            .resolve("canonical");
        Files.createDirectories(root.getParent());
        write(root, "not a directory");
        expect(new CanonicalProjectContentLoader(), project, "project.content.root.invalid");
        require(Files.isRegularFile(root), "Invalid canonical root was changed");
        delete(project);
    }

    private static CanonicalProjectContent load(CanonicalProjectContentLoader loader, Path project) {
        return loader.load(project, new HashSet<String>(Arrays.asList("actor_a", "actor_b", "actor_c")));
    }

    private static void verifyValid(CanonicalProjectContentLoader loader, Path project) {
        CanonicalProjectContent content = load(loader, project);
        require(
            Arrays.asList("story_a", "story_b")
                .equals(
                    Arrays.asList(
                        content.getStories()
                            .keySet()
                            .toArray(new String[0]))),
            "Story order changed");
        require(
            Arrays.asList("session_a", "session_b")
                .equals(
                    Arrays.asList(
                        content.getSessions()
                            .keySet()
                            .toArray(new String[0]))),
            "Session order changed");
        require(
            Arrays.asList("task_a", "task_b")
                .equals(
                    Arrays.asList(
                        content.getTasks()
                            .keySet()
                            .toArray(new String[0]))),
            "Task order changed");
        require(
            Arrays.asList("story_a", "story_b")
                .equals(
                    Arrays.asList(
                        content.getMemberships()
                            .keySet()
                            .toArray(new String[0]))),
            "Membership order changed");
        require(
            content.getStory("story_a") != null && content.getMembership("story_a") != null,
            "Canonical lookup failed");
        expectUnsupported(new Runnable() {

            @Override
            public void run() {
                content.getStories()
                    .clear();
            }
        });
        expectUnsupported(new Runnable() {

            @Override
            public void run() {
                content.getMemberships()
                    .clear();
            }
        });
        expectUnsupported(new Runnable() {

            @Override
            public void run() {
                content.getSessions()
                    .clear();
            }
        });
        expectUnsupported(new Runnable() {

            @Override
            public void run() {
                content.getTasks()
                    .clear();
            }
        });
    }

    private static void verifyMissingDirectory(CanonicalProjectContentLoader loader, Path project) throws IOException {
        Path tasks = project.resolve("resources/canonical/tasks");
        delete(tasks);
        expect(loader, project, "project.content.directory.missing");
        require(!Files.exists(tasks), "Missing canonical directory was created");
    }

    private static void verifyStoryLogicGraph(CanonicalProjectContentLoader loader, Path project) throws IOException {
        CanonicalProjectContent absent = load(loader, project);
        require(
            absent.getStoryLogicGraph() == CanonicalStoryLogicGraph.empty(),
            "Absent Story logic graph did not return stable empty graph");

        Path graphFile = project.resolve("resources/canonical/story_logic_graph.json");
        write(graphFile, "{\"schema_version\":2,\"connections\":[]}");
        CanonicalProjectContent present = load(loader, project);
        require(
            present.getStoryLogicGraph()
                .getSchemaVersion() == 2,
            "Story logic graph schema changed");
        require(
            present.getStoryLogicGraph()
                .getConnections()
                .isEmpty(),
            "Empty Story logic graph changed");
        expectStoryLogicFailure(loader, project, "{\"schema_version\":2,\"connections\":[],\"extra\":0}");
        expectStoryLogicFailure(loader, project, "{\"schema_version\":1,\"connections\":[]}");
        Files.deleteIfExists(graphFile);

        Map<String, CanonicalGraphResource> stories = new LinkedHashMap<String, CanonicalGraphResource>();
        stories.put("source", boundaryStory("source", "out", "in"));
        stories.put("source2", boundaryStory("source2", "out", "in"));
        stories.put("target", boundaryStory("target", "out", "in"));
        CanonicalStoryLogicGraphLoader direct = new CanonicalStoryLogicGraphLoader();
        write(
            graphFile,
            "{\"schema_version\":2,\"connections\":[" + "{\"source_story_id\":\"source\",\"source_port_id\":\"out\","
                + "\"target_story_id\":\"target\",\"target_port_id\":\"in\",\"interface_kind\":\"Logic\"}]}");
        CanonicalStoryLogicGraph graph = direct.load(graphFile, stories);
        require(
            graph.getConnections()
                .size() == 1,
            "Story logic edge was not loaded");
        expectDirectStoryLogicFailure(
            direct,
            graphFile,
            stories,
            "{\"schema_version\":2,\"connections\":[" + "{\"source_story_id\":\"source\",\"source_port_id\":\"out\","
                + "\"target_story_id\":\"target\",\"target_port_id\":\"in\",\"interface_kind\":\"Logic\"},"
                + "{\"source_story_id\":\"source2\",\"source_port_id\":\"out\","
                + "\"target_story_id\":\"target\",\"target_port_id\":\"in\",\"interface_kind\":\"Logic\"}]}",
            "story.logic.graph.target.multiple_sources");
        Files.delete(graphFile);
    }

    private static void expectStoryLogicFailure(CanonicalProjectContentLoader loader, Path project, String json)
        throws IOException {
        Path file = project.resolve("resources/canonical/story_logic_graph.json");
        write(file, json);
        try {
            load(loader, project);
            throw new AssertionError("Expected Story logic graph rejection");
        } catch (CanonicalProjectContentException exception) {
            require(
                "project.content.story_logic_graph.load".equals(exception.getCode()),
                "Story logic graph wrapper code changed");
            require(
                exception.getCause() instanceof CanonicalGraphResourceException,
                "Story logic graph cause was not retained");
        } finally {
            Files.deleteIfExists(file);
        }
    }

    private static void expectDirectStoryLogicFailure(CanonicalStoryLogicGraphLoader loader, Path file,
        Map<String, CanonicalGraphResource> stories, String json, String code) throws IOException {
        write(file, json);
        try {
            loader.load(file, stories);
            throw new AssertionError("Expected " + code);
        } catch (CanonicalGraphResourceException exception) {
            require(code.equals(exception.getCode()), "Expected " + code + ", got " + exception.getCode());
        }
    }

    private static CanonicalGraphResource boundaryStory(String id, String outputId, String inputId) {
        Map<String, JsonElement> outputProperties = new LinkedHashMap<String, JsonElement>();
        outputProperties.put("port_id", new JsonPrimitive(outputId));
        outputProperties.put("display_name", new JsonPrimitive(outputId));
        Map<String, JsonElement> inputProperties = new LinkedHashMap<String, JsonElement>();
        inputProperties.put("port_id", new JsonPrimitive(inputId));
        inputProperties.put("display_name", new JsonPrimitive(inputId));
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.STORY,
            id,
            id,
            new CanonicalGraph(
                Arrays.asList(
                    new CanonicalGraphNode(
                        "output",
                        "logic_output",
                        outputId,
                        Collections.singletonList(
                            new CanonicalGraphPort(
                                "logic_in",
                                "Logic In",
                                CanonicalGraphPortDirection.INPUT,
                                CanonicalGraphInterfaceKind.LOGIC,
                                0)),
                        outputProperties),
                    new CanonicalGraphNode(
                        "input",
                        "logic_input",
                        inputId,
                        Collections.singletonList(
                            new CanonicalGraphPort(
                                "logic_out",
                                "Logic Out",
                                CanonicalGraphPortDirection.OUTPUT,
                                CanonicalGraphInterfaceKind.LOGIC,
                                0)),
                        inputProperties)),
                Collections.<CanonicalGraphConnection>emptyList()));
    }

    private static void verifyMalformedPropagation(CanonicalProjectContentLoader loader, Path project)
        throws IOException {
        Path malformed = project.resolve("resources/canonical/stories/story_a.json");
        write(malformed, "{bad");
        try {
            load(loader, project);
            throw new AssertionError("Expected malformed child failure");
        } catch (CanonicalProjectContentException exception) {
            require("project.content.stories.load".equals(exception.getCode()), "Malformed wrapper code changed");
            require(
                exception.getCause() instanceof CanonicalGraphResourceException,
                "Malformed cause was not retained");
        }
    }

    private static void verifyPairs(CanonicalProjectContentLoader loader, Path project) throws IOException {
        Path memberships = project.resolve("resources/canonical/memberships");
        Files.delete(memberships.resolve("story_b.json"));
        expect(loader, project, "project.content.membership.missing");
        prepare(project);
        Files.delete(project.resolve("resources/canonical/stories/story_b.json"));
        expect(loader, project, "project.content.story.missing");
    }

    private static void verifyTargets(CanonicalProjectContentLoader loader, Path project) throws IOException {
        Path file = project.resolve("resources/canonical/memberships/story_a.json");
        String original = new String(Files.readAllBytes(file), StandardCharsets.UTF_8);
        expectTarget(
            loader,
            project,
            file,
            original.replace("actor_b", "actor_missing"),
            "project.content.actor.missing");
        expectTarget(
            loader,
            project,
            file,
            original.replace("session_b", "session_missing"),
            "project.content.session.missing");
        expectTarget(loader, project, file, original.replace("task_b", "task_missing"), "project.content.task.missing");
        write(file, original);
    }

    private static void expectTarget(CanonicalProjectContentLoader loader, Path project, Path file, String text,
        String code) throws IOException {
        write(file, text);
        expect(loader, project, code);
    }

    private static void verifyDuplicateOwnership(CanonicalProjectContentLoader loader, Path project)
        throws IOException {
        Path file = project.resolve("resources/canonical/memberships/story_b.json");
        String original = new String(Files.readAllBytes(file), StandardCharsets.UTF_8);
        expectTarget(
            loader,
            project,
            file,
            original.replace("\"actors\":[\"actor_c\"],\"sessions\"", "\"actors\":[\"actor_a\"],\"sessions\"")
                .replace(
                    "\"referenced_resources\":{\"actors\":[\"actor_a\"]",
                    "\"referenced_resources\":{\"actors\":[\"actor_b\"]"),
            "project.content.actor.ownership.duplicate");
        expectTarget(
            loader,
            project,
            file,
            original.replace("\"sessions\":[\"session_b\"],\"tasks\"", "\"sessions\":[\"session_a\"],\"tasks\"")
                .replace(
                    "\"referenced_resources\":{\"actors\":[\"actor_a\"],\"sessions\":[\"session_a\"]",
                    "\"referenced_resources\":{\"actors\":[\"actor_a\"],\"sessions\":[\"session_b\"]"),
            "project.content.session.ownership.duplicate");
        expectTarget(
            loader,
            project,
            file,
            original
                .replace(
                    "\"tasks\":[\"task_b\"]},\"referenced_resources\"",
                    "\"tasks\":[\"task_a\"]},\"referenced_resources\"")
                .replace(
                    "\"referenced_resources\":{\"actors\":[\"actor_a\"],\"sessions\":[\"session_a\"],\"tasks\":[\"task_a\"]",
                    "\"referenced_resources\":{\"actors\":[\"actor_a\"],\"sessions\":[\"session_a\"],\"tasks\":[\"task_b\"]"),
            "project.content.task.ownership.duplicate");
        write(file, original);
    }

    private static void verifyJava8Bytecode() throws IOException {
        String name = CanonicalProjectContentLoader.class.getName()
            .replace('.', '/') + ".class";
        InputStream stream = CanonicalProjectContentLoader.class.getClassLoader()
            .getResourceAsStream(name);
        require(stream != null, "Loader bytecode resource is missing");
        DataInputStream input = new DataInputStream(stream);
        try {
            require(input.readInt() == 0xCAFEBABE, "Loader class magic changed");
            input.readUnsignedShort();
            require(input.readUnsignedShort() == 52, "Loader is not Java 8 bytecode");
        } finally {
            input.close();
        }
    }

    private static void expect(CanonicalProjectContentLoader loader, Path project, String code) {
        try {
            load(loader, project);
            throw new AssertionError("Expected " + code);
        } catch (CanonicalProjectContentException exception) {
            require(code.equals(exception.getCode()), "Expected " + code + ", got " + exception.getCode());
        }
    }

    private static void expectUnsupported(Runnable action) {
        try {
            action.run();
            throw new AssertionError("Expected immutable map");
        } catch (UnsupportedOperationException expected) {}
    }

    private static String membershipJson(String story, String ownedActor, String ownedSession, String ownedTask,
        String referencedActor, String referencedSession, String referencedTask) {
        return "{\"schema_version\":1,\"story_id\":\"" + story
            + "\",\"owned_resources\":{\"actors\":[\""
            + ownedActor
            + "\"],\"sessions\":[\""
            + ownedSession
            + "\"],\"tasks\":[\""
            + ownedTask
            + "\"]},\"referenced_resources\":{\"actors\":[\""
            + referencedActor
            + "\"],\"sessions\":[\""
            + referencedSession
            + "\"],\"tasks\":[\""
            + referencedTask
            + "\"]}}";
    }

    private static String storyJson(String id) {
        return "{\"schema_version\":1,\"resource_kind\":\"story\",\"id\":\"" + id
            + "\",\"display_name\":\"Story\",\"graph\":{\"nodes\":[{\"id\":\"start\",\"type\":\"start\",\"display_name\":\"Start\",\"ports\":[],\"properties\":{}}],\"connections\":[]}}";
    }

    private static String sessionJson(String id) {
        return storyJson(id).replace("\"resource_kind\":\"story\"", "\"resource_kind\":\"session\"");
    }

    private static String taskJson(String id) {
        return "{\"schema_version\":1,\"resource_kind\":\"task\",\"id\":\"" + id
            + "\",\"display_name\":\"Task\",\"graph\":{\"nodes\":[{\"id\":\"settle\",\"type\":\"settle\",\"display_name\":\"Settle\",\"ports\":[],\"properties\":{}}],\"connections\":[]}}";
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
