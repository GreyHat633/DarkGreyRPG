package darkgrey.rpg.task.runtime;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

import com.google.gson.JsonElement;
import com.google.gson.JsonParser;
import com.google.gson.JsonPrimitive;

import darkgrey.rpg.graph.canonical.CanonicalGraph;
import darkgrey.rpg.graph.canonical.CanonicalGraphConnection;
import darkgrey.rpg.graph.canonical.CanonicalGraphInterfaceKind;
import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphPort;
import darkgrey.rpg.graph.canonical.CanonicalGraphPortDirection;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceKind;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceLoader;

/** Explicit multi-boundary priority, stable identity and terminal receipt regression. */
public final class PublicOutputPriority0336Probe {

    public static void main(String[] args) {
        noSettlementRemainsActive();
        emptyObjectiveDescriptions();
        for (boolean reversed : new boolean[] { false, true }) {
            CanonicalGraphResource resource = resource(reversed, false);
            CanonicalTaskRuntime runtime = CanonicalTaskRuntime.start(resource);
            require(runtime.isActive(), "No true condition must remain active");
            require(runtime.setLogicInput("gate", true), "Input must change");
            require(runtime.isActive(), "Pending reward must be acknowledged before final commit");
            require(runtime.grantReward("reward"), "First receipt must succeed");
            String winner = reversed ? "ordinary" : "perfect";
            require(winner.equals(runtime.getResultPortId()), "Explicit order must choose " + winner);
            require(!runtime.grantReward("reward"), "Repeated receipt must not grant twice");
            require(!runtime.setLogicInput("gate", false), "Final input cannot rewrite the settlement snapshot");
            require(!runtime.accept(CanonicalTaskEvent.killEntity("slime")), "Late events must be ignored");
            CanonicalTaskRuntime restored = CanonicalTaskRuntime.restore(resource(reversed, true), runtime.snapshot());
            require(winner.equals(restored.getResultPortId()), "JSON node/edge order must not change restoration");
            require(!restored.grantReward("reward"), "Restore must retain the reward receipt");
        }
        if (args.length >= 1)
            exportedPackagesRemainRunnable(new java.io.File(args[0]), args.length >= 2 ? Integer.parseInt(args[1]) : 4);
        System.out.println("PUBLIC_OUTPUT_PRIORITY_0336_PASS");
    }

    private static void exportedPackagesRemainRunnable(java.io.File directory, int expectedStories) {
        darkgrey.rpg.project.packages.StoryPackageLoader loader = new darkgrey.rpg.project.packages.StoryPackageLoader(
            directory);
        require(
            loader.reload()
                .isSuccessful(),
            "Studio export rejected: " + loader.getLastReload()
                .getSummary());
        require(
            loader.getPackages()
                .size() == expectedStories,
            "Expected all exported local and external stories");
        int taskCount = 0;
        for (darkgrey.rpg.project.packages.LoadedStoryPackage pack : loader.getPackages()
            .values())
            for (CanonicalGraphResource task : pack.getSnapshot()
                .getCanonicalTasks()
                .values()) {
                    CanonicalTaskRuntime runtime = CanonicalTaskRuntime.start(task);
                    require(
                        runtime.isActive() && runtime.getResultPortId() == null,
                        "Unconnected or empty Task must stay ACTIVE");
                    require(
                        CanonicalTaskRuntime.restore(task, runtime.snapshot())
                            .isActive(),
                        "ACTIVE export must restore");
                    taskCount++;
                }
        require(taskCount == 4, "Expected all four exported Task definitions");
        System.out.println("STUDIO_REFERENCE_EXPORT_SINGLE_GROUP_TASK_RUNTIME_PASS");
    }

    private static void noSettlementRemainsActive() {
        String json = "{\"schema_version\":3,\"identity_format\":\"story-uid-v1\",\"resource_kind\":\"task\","
            + "\"id\":{\"story_uid\":\"ST-2345-6789-ABCD-EFGH\",\"kind\":\"task\",\"local_id\":\"empty\"},"
            + "\"display_name\":\"Empty\",\"graph\":{\"nodes\":[],\"connections\":[]}}";
        CanonicalGraphResource empty = new CanonicalGraphResourceLoader().load(
            json.getBytes(java.nio.charset.StandardCharsets.UTF_8),
            "empty.json",
            CanonicalGraphResourceKind.TASK);
        CanonicalTaskRuntime runtime = CanonicalTaskRuntime.start(empty);
        require(runtime.isActive() && runtime.getResultPortId() == null, "Empty Task must remain ACTIVE");
        require(
            CanonicalTaskRuntime.restore(empty, runtime.snapshot())
                .isActive(),
            "Empty ACTIVE snapshot must restore");
        CanonicalGraphResource withConditions = resource(false, false);
        List<CanonicalGraphNode> nodes = new ArrayList<CanonicalGraphNode>();
        for (CanonicalGraphNode node : withConditions.getGraph()
            .getNodes()) if (!"settle".equals(node.getType())) nodes.add(node);
        List<CanonicalGraphConnection> edges = new ArrayList<CanonicalGraphConnection>();
        for (CanonicalGraphConnection edge : withConditions.getGraph()
            .getConnections()) if ("reward".equals(edge.getToNodeId())) edges.add(edge);
        CanonicalGraphResource noSettle = new CanonicalGraphResource(
            CanonicalGraphResource.CURRENT_SCHEMA_VERSION,
            CanonicalGraphResourceKind.TASK,
            "no_settle",
            "No settle",
            new CanonicalGraph(nodes, edges));
        runtime = CanonicalTaskRuntime.start(noSettle);
        require(runtime.setLogicInput("gate", true), "Condition must evaluate without settlements");
        require(runtime.grantReward("reward"), "Normal reward logic still works");
        require(runtime.isActive() && runtime.getResultPortId() == null, "True conditions cannot invent a result");
        CanonicalTaskRuntime restored = CanonicalTaskRuntime.restore(noSettle, runtime.snapshot());
        require(restored.isActive() && !restored.grantReward("reward"), "ACTIVE restore retains reward receipts");
    }

    private static void emptyObjectiveDescriptions() {
        for (String description : Arrays.asList("", " \r\n ")) {
            CanonicalGraphNode objective = new CanonicalGraphNode(
                "objective",
                "objective",
                "消灭史莱姆",
                Arrays.asList(port("logic_status", false)),
                properties(
                    "objective_type",
                    new JsonPrimitive("kill_entity"),
                    "description",
                    new JsonPrimitive(description),
                    "entity",
                    new JsonPrimitive("ST-2345-6789-ABCD-EFGH~actor~slime"),
                    "required",
                    new JsonPrimitive(1)));
            final CanonicalGraphResource task = new CanonicalGraphResource(
                CanonicalGraphResource.CURRENT_SCHEMA_VERSION,
                CanonicalGraphResourceKind.TASK,
                "optional_description",
                "Optional description",
                new CanonicalGraph(
                    Arrays.asList(objective, settle("done", 0)),
                    Arrays.asList(
                        new CanonicalGraphConnection(
                            "objective",
                            "logic_status",
                            "done",
                            "logic_in",
                            CanonicalGraphInterfaceKind.LOGIC))));
            CanonicalTaskRuntime runtime = CanonicalTaskRuntime.start(task);
            require("消灭史莱姆".equals(CanonicalTaskRuntime.objectiveDescription(objective)), "Display fallback");
            require(
                description.equals(
                    objective.getProperties()
                        .get("description")
                        .getAsString()),
                "Fallback must not rewrite authored text");
            java.util.UUID player = java.util.UUID.fromString("12345678-1234-1234-1234-123456789abc");
            darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot snapshot = new darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot(
                player,
                "story",
                "placement",
                task.getId(),
                darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus.ACTIVE,
                1L,
                null,
                runtime.snapshot());
            List<darkgrey.rpg.task.journal.CanonicalTaskJournalEntry> journal = darkgrey.rpg.task.journal.CanonicalTaskJournalProjector
                .project(
                    player,
                    Arrays.asList(snapshot),
                    new darkgrey.rpg.task.instance.CanonicalTaskResourceResolver() {

                        @Override
                        public CanonicalGraphResource resolve(String id) {
                            return task;
                        }
                    });
            require(
                "消灭史莱姆".equals(
                    journal.get(0)
                        .getObjectives()
                        .get(0)
                        .getDescription()),
                "Journal must use the same display fallback");
            require(
                runtime.accept(CanonicalTaskEvent.killEntity("ST-2345-6789-ABCD-EFGH~actor~slime")),
                "Blank description must execute");
            require("done".equals(runtime.getResultPortId()), "Objective completion must settle once");
            require(
                !runtime.accept(CanonicalTaskEvent.killEntity("ST-2345-6789-ABCD-EFGH~actor~slime")),
                "Duplicate must not settle again");
            require(
                "done".equals(
                    CanonicalTaskRuntime.restore(task, runtime.snapshot())
                        .getResultPortId()),
                "Blank-description settlement must restore");
        }
        for (String invalid : Arrays.asList("null", "false", "1", "[]", "{}")) {
            CanonicalGraphNode node = new CanonicalGraphNode(
                "objective",
                "objective",
                "目标",
                Arrays.asList(port("logic_status", false)),
                properties(
                    "objective_type",
                    new JsonPrimitive("kill_entity"),
                    "description",
                    new JsonParser().parse(invalid),
                    "entity",
                    new JsonPrimitive("slime"),
                    "required",
                    new JsonPrimitive(1)));
            CanonicalGraphResource task = new CanonicalGraphResource(
                CanonicalGraphResource.CURRENT_SCHEMA_VERSION,
                CanonicalGraphResourceKind.TASK,
                "bad_description",
                "Bad description",
                new CanonicalGraph(Arrays.asList(node), Collections.<CanonicalGraphConnection>emptyList()));
            boolean rejected = false;
            try {
                CanonicalTaskRuntime.start(task);
            } catch (darkgrey.rpg.graph.canonical.CanonicalGraphResourceException error) {
                rejected = "task.objective.description.invalid".equals(error.getCode());
            }
            require(rejected, "Malformed description must remain rejected: " + invalid);
        }
        System.out.println("OPTIONAL_OBJECTIVE_DESCRIPTION_RUNTIME_JOURNAL_RESTORE_PASS");
    }

    private static CanonicalGraphResource resource(boolean reversedPriority, boolean shuffled) {
        List<CanonicalGraphNode> nodes = new ArrayList<CanonicalGraphNode>();
        nodes.add(
            new CanonicalGraphNode(
                "gate",
                "logic_input",
                "Gate",
                Arrays.asList(port("logic_out", false)),
                properties("port_id", new JsonPrimitive("gate"), "display_name", new JsonPrimitive("Gate"))));
        nodes.add(settle("ordinary", reversedPriority ? 0 : 1));
        nodes.add(settle("perfect", reversedPriority ? 1 : 0));
        nodes.add(
            new CanonicalGraphNode(
                "reward",
                "reward",
                "Reward",
                Arrays.asList(port("logic_in", true)),
                properties("entries", new JsonParser().parse("[{\"type\":\"xp\",\"amount\":3}]"))));
        List<CanonicalGraphConnection> edges = new ArrayList<CanonicalGraphConnection>();
        for (String target : Arrays.asList("ordinary", "perfect", "reward")) edges.add(
            new CanonicalGraphConnection("gate", "logic_out", target, "logic_in", CanonicalGraphInterfaceKind.LOGIC));
        if (shuffled) {
            Collections.reverse(nodes);
            Collections.reverse(edges);
        }
        return new CanonicalGraphResource(
            CanonicalGraphResource.CURRENT_SCHEMA_VERSION,
            CanonicalGraphResourceKind.TASK,
            "priority",
            "Priority",
            new CanonicalGraph(nodes, edges));
    }

    private static CanonicalGraphNode settle(String id, int order) {
        return new CanonicalGraphNode(
            id,
            "settle",
            id,
            Arrays.asList(port("logic_in", true)),
            properties(
                "port_id",
                new JsonPrimitive(id),
                "display_name",
                new JsonPrimitive(id),
                "display_order",
                new JsonPrimitive(order)));
    }

    private static CanonicalGraphPort port(String id, boolean input) {
        return new CanonicalGraphPort(
            id,
            id,
            input ? CanonicalGraphPortDirection.INPUT : CanonicalGraphPortDirection.OUTPUT,
            CanonicalGraphInterfaceKind.LOGIC,
            0);
    }

    private static Map<String, JsonElement> properties(Object... values) {
        Map<String, JsonElement> result = new LinkedHashMap<String, JsonElement>();
        for (int i = 0; i < values.length; i += 2) result.put((String) values[i], (JsonElement) values[i + 1]);
        return result;
    }

    private static void require(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }
}
