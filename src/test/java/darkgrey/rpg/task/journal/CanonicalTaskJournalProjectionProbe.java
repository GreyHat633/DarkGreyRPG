package darkgrey.rpg.task.journal;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;

import com.google.gson.JsonElement;
import com.google.gson.JsonParser;

import darkgrey.rpg.graph.canonical.CanonicalGraph;
import darkgrey.rpg.graph.canonical.CanonicalGraphConnection;
import darkgrey.rpg.graph.canonical.CanonicalGraphInterfaceKind;
import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphPort;
import darkgrey.rpg.graph.canonical.CanonicalGraphPortDirection;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceKind;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceStore;
import darkgrey.rpg.task.instance.CanonicalTaskResourceResolver;
import darkgrey.rpg.task.runtime.CanonicalTaskEvent;
import darkgrey.rpg.task.runtime.CanonicalTaskObjectiveStatus;
import darkgrey.rpg.task.runtime.CanonicalTaskSnapshot;

/** Focused pure canonical Task Journal projection probe. */
public final class CanonicalTaskJournalProjectionProbe {

    public static void main(String[] args) {
        UUID player = UUID.fromString("11111111-1111-1111-1111-111111111111");
        UUID other = UUID.fromString("22222222-2222-2222-2222-222222222222");
        CanonicalGraphResource parallel = resource("parallel", 3, false, "Parallel Task");
        CanonicalGraphResource settledResource = resource("settled", 1, false, "Settled Task");
        CanonicalGraphResource sequential = resource("sequential", 3, true, "Sequential Task");
        final Map<String, CanonicalGraphResource> resources = new LinkedHashMap<String, CanonicalGraphResource>();
        resources.put(parallel.getId(), parallel);
        resources.put(settledResource.getId(), settledResource);
        resources.put(sequential.getId(), sequential);

        CanonicalTaskInstanceStore store = new CanonicalTaskInstanceStore();
        store.start(player, "z-story", "active", parallel, 20L);
        store.acceptEvent(player, "z-story", "active", CanonicalTaskEvent.killEntity("slime"), 21L);
        store.start(player, "a-story", "settled", settledResource, 10L);
        store.acceptEvent(player, "a-story", "settled", CanonicalTaskEvent.killEntity("slime"), 11L);
        store.start(player, "b-story", "cancelled", parallel, 20L);
        store.cancelByStory(player, "b-story");
        store.start(player, "c-story", "error", parallel, 30L);
        require(store.markError(player, "c-story", "error"), "error transition");
        store.start(player, "d-story", "sequential", sequential, 40L);
        store.start(other, "other", "ignored", parallel, 1L);

        List<CanonicalTaskInstanceSnapshot> sourceSnapshots = new ArrayList<CanonicalTaskInstanceSnapshot>(
            store.snapshots());
        List<CanonicalTaskJournalEntry> entries = CanonicalTaskJournalProjector
            .project(player, sourceSnapshots, resolver(resources));
        require(entries.size() == 5, "player filtering and entry count");
        require(
            entries.get(0)
                .getStatus() == CanonicalTaskInstanceStatus.SETTLED,
            "settled entry");
        require(
            entries.get(0)
                .getSettlementTime()
                .longValue() == 11L,
            "settled timestamp");
        require(
            "result".equals(
                entries.get(0)
                    .getSettledResultSlot()),
            "settled result slot");
        require(
            entries.get(0)
                .getPublicLogicState()
                .containsKey("done"),
            "retained public Logic");
        require(
            entries.get(1)
                .getStoryInstanceId()
                .equals("b-story"),
            "identity tie ordering first");
        require(
            entries.get(2)
                .getStoryInstanceId()
                .equals("z-story"),
            "identity tie ordering second");
        require(
            entries.get(2)
                .getStatus() == CanonicalTaskInstanceStatus.ACTIVE,
            "active entry");
        require(
            entries.get(2)
                .getObjectiveRows()
                .get(0)
                .getCurrentProgress() == 1,
            "active progress");
        require(
            entries.get(3)
                .getStatus() == CanonicalTaskInstanceStatus.ERROR,
            "error entry");
        require(
            entries.get(4)
                .getStatus() == CanonicalTaskInstanceStatus.ACTIVE,
            "sequential active entry");
        require(
            entries.get(4)
                .getObjectiveRows()
                .get(0)
                .getRuntimeStatus() == CanonicalTaskObjectiveStatus.ACTIVE,
            "sequential first objective active");
        require(
            entries.get(4)
                .getObjectiveRows()
                .get(1)
                .getRuntimeStatus() == CanonicalTaskObjectiveStatus.INACTIVE,
            "sequential second objective inactive");
        require(
            entries.get(4)
                .getObjectiveRows()
                .get(2)
                .getCurrentProgress() == 0,
            "sequential third objective inactive");
        require(
            "kill_entity".equals(
                entries.get(2)
                    .getObjectiveRows()
                    .get(0)
                    .getObjectiveType()),
            "kill row");
        require(
            "collect_item".equals(
                entries.get(2)
                    .getObjectiveRows()
                    .get(1)
                    .getObjectiveType()),
            "collect row");
        require(
            "interact_actor".equals(
                entries.get(2)
                    .getObjectiveRows()
                    .get(2)
                    .getObjectiveType()),
            "interact row");
        require(
            entries.get(2)
                .getObjectiveRows()
                .get(0)
                .getDisplayLine()
                .contains("1/3"),
            "display line");

        // Results are detached and immutable, including the source list and every exposed collection.
        List<CanonicalTaskJournalEntry> beforeSourceMutation = entries;
        sourceSnapshots.clear();
        require(
            beforeSourceMutation.size() == 5 && beforeSourceMutation.get(2)
                .getObjectiveRows()
                .size() == 3,
            "source detachment");
        expectUnsupported(beforeSourceMutation);
        expectUnsupported(
            beforeSourceMutation.get(2)
                .getObjectiveRows());
        expectUnsupported(
            beforeSourceMutation.get(2)
                .getPublicLogicState());

        // Strict resource and snapshot corruption gates.
        CanonicalTaskInstanceSnapshot activeSnapshot = sourceSnapshot(player, "z-story", "active", parallel);
        expectFailure(single(player, activeSnapshot, resolver(Collections.<String, CanonicalGraphResource>emptyMap())));
        expectFailure(
            single(
                player,
                activeSnapshot,
                resolver(
                    Collections.singletonMap(
                        parallel.getId(),
                        new CanonicalGraphResource(
                            1,
                            CanonicalGraphResourceKind.SESSION,
                            parallel.getId(),
                            parallel.getDisplayName(),
                            parallel.getGraph())))));
        expectFailure(
            single(
                player,
                activeSnapshot,
                resolver(
                    Collections.singletonMap(parallel.getId(), resource("different-id", 3, false, "Parallel Task")))));
        expectFailure(
            single(
                player,
                activeSnapshot,
                resolver(
                    Collections
                        .singletonMap(parallel.getId(), resource("parallel", 3, false, "Changed fingerprint")))));
        expectFailure(
            single(
                player,
                activeSnapshot,
                resolver(Collections.singletonMap(parallel.getId(), malformedResource(parallel, "unsupported-type")))));
        expectFailure(
            single(
                player,
                activeSnapshot,
                resolver(Collections.singletonMap(parallel.getId(), malformedResource(parallel, "metadata-object")))));
        expectFailure(
            single(
                player,
                activeSnapshot,
                resolver(Collections.singletonMap(parallel.getId(), malformedResource(parallel, "metadata-value")))));

        CanonicalTaskSnapshot runtime = activeSnapshot.getRuntimeSnapshot();
        Map<String, Integer> extraProgress = new LinkedHashMap<String, Integer>(runtime.getProgress());
        extraProgress.put("extra", Integer.valueOf(0));
        Map<String, CanonicalTaskObjectiveStatus> extraStatuses = new LinkedHashMap<String, CanonicalTaskObjectiveStatus>(
            runtime.getObjectiveStatuses());
        extraStatuses.put("extra", CanonicalTaskObjectiveStatus.INACTIVE);
        expectFailure(
            single(
                player,
                replaceRuntime(
                    activeSnapshot,
                    new CanonicalTaskSnapshot(
                        runtime.getResourceId(),
                        runtime.getResourceFingerprint(),
                        runtime.getStatus(),
                        extraProgress,
                        extraStatuses,
                        runtime.getLogicValues(),
                        runtime.getPublicLogicOutputs(),
                        runtime.getActivationLogic(),
                        runtime.getResultPortId())),
                resolver(resources)));
        Map<String, Integer> completeProgress = new LinkedHashMap<String, Integer>(runtime.getProgress());
        completeProgress.put("kill", Integer.valueOf(3));
        expectFailure(
            single(
                player,
                replaceRuntime(
                    activeSnapshot,
                    new CanonicalTaskSnapshot(
                        runtime.getResourceId(),
                        runtime.getResourceFingerprint(),
                        runtime.getStatus(),
                        completeProgress,
                        runtime.getObjectiveStatuses(),
                        runtime.getLogicValues(),
                        runtime.getPublicLogicOutputs(),
                        runtime.getActivationLogic(),
                        runtime.getResultPortId())),
                resolver(resources)));
        Map<String, CanonicalTaskObjectiveStatus> badStatuses = new LinkedHashMap<String, CanonicalTaskObjectiveStatus>(
            runtime.getObjectiveStatuses());
        badStatuses.put("kill", CanonicalTaskObjectiveStatus.COMPLETED);
        expectFailure(
            single(
                player,
                replaceRuntime(
                    activeSnapshot,
                    new CanonicalTaskSnapshot(
                        runtime.getResourceId(),
                        runtime.getResourceFingerprint(),
                        runtime.getStatus(),
                        runtime.getProgress(),
                        badStatuses,
                        runtime.getLogicValues(),
                        runtime.getPublicLogicOutputs(),
                        runtime.getActivationLogic(),
                        runtime.getResultPortId())),
                resolver(resources)));

        // Duplicate identity must fail before producing a second entry.
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalTaskJournalProjector
                    .project(player, Arrays.asList(activeSnapshot, activeSnapshot), resolver(resources));
            }
        });

        CanonicalGraphResource hundred = resourceWithObjectives("hundred", 100);
        CanonicalTaskInstanceStore hundredStore = new CanonicalTaskInstanceStore();
        hundredStore.start(player, "many", "objectives", hundred, 100L);
        List<CanonicalTaskJournalEntry> hundredEntries = CanonicalTaskJournalProjector
            .project(player, hundredStore.snapshots(), resolver(Collections.singletonMap(hundred.getId(), hundred)));
        require(
            hundredEntries.size() == 1 && hundredEntries.get(0)
                .getObjectiveRows()
                .size() == 100,
            "100 objective projection");
        System.out.println("CANONICAL_TASK_JOURNAL_PROJECTION_PASS");
    }

    private static Runnable single(final UUID player, final CanonicalTaskInstanceSnapshot snapshot,
        final CanonicalTaskResourceResolver resolver) {
        return new Runnable() {

            @Override
            public void run() {
                CanonicalTaskJournalProjector.project(player, Collections.singletonList(snapshot), resolver);
            }
        };
    }

    private static CanonicalTaskInstanceSnapshot sourceSnapshot(UUID player, String story, String placement,
        CanonicalGraphResource resource) {
        CanonicalTaskInstanceStore source = new CanonicalTaskInstanceStore();
        source.start(player, story, placement, resource, 20L);
        source.acceptEvent(player, story, placement, CanonicalTaskEvent.killEntity("slime"), 21L);
        return source.get(player, story, placement)
            .snapshot();
    }

    private static CanonicalTaskInstanceSnapshot replaceRuntime(CanonicalTaskInstanceSnapshot source,
        CanonicalTaskSnapshot runtime) {
        return new CanonicalTaskInstanceSnapshot(
            source.getPlayerUuid(),
            source.getStoryInstanceId(),
            source.getTaskNodePlacementId(),
            source.getTaskResourceId(),
            source.getStatus(),
            source.getActivationTime(),
            source.getSettlementTime(),
            runtime);
    }

    private static CanonicalTaskResourceResolver resolver(final Map<String, CanonicalGraphResource> resources) {
        return new CanonicalTaskResourceResolver() {

            @Override
            public CanonicalGraphResource resolve(String id) {
                return resources.get(id);
            }
        };
    }

    private static CanonicalGraphResource resource(String id, int required, boolean sequential, String title) {
        List<CanonicalGraphNode> nodes = Arrays.asList(
            node("activate", "activate", Collections.singletonList(port("logic_out", false, 0)), empty()),
            node(
                "kill",
                "objective",
                Arrays.asList(port("prerequisite", true, 0), port("logic_status", false, 1)),
                objective("kill_entity", required, "entity", "slime")),
            node(
                "collect",
                "objective",
                Arrays.asList(port("prerequisite", true, 0), port("logic_status", false, 1)),
                objective("collect_item", required, "item", "iron", "metadata", "{\"grade\":\"raw\"}")),
            node(
                "interact",
                "objective",
                Arrays.asList(port("prerequisite", true, 0), port("logic_status", false, 1)),
                objective("interact_actor", required, "actor_id", "guard")),
            node("settle", "settle", Collections.singletonList(port("result", true, 0)), empty()),
            node(
                "done",
                "logic_output",
                Collections.singletonList(port("logic_in", true, 0)),
                props("port_id", "done", "display_name", "Done")));
        List<CanonicalGraphConnection> edges = new ArrayList<CanonicalGraphConnection>();
        edges.add(edge("activate", "logic_out", "kill", "prerequisite"));
        if (sequential) {
            edges.add(edge("kill", "logic_status", "collect", "prerequisite"));
            edges.add(edge("collect", "logic_status", "interact", "prerequisite"));
            edges.add(edge("interact", "logic_status", "settle", "result"));
            edges.add(edge("interact", "logic_status", "done", "logic_in"));
        } else {
            edges.add(edge("activate", "logic_out", "collect", "prerequisite"));
            edges.add(edge("activate", "logic_out", "interact", "prerequisite"));
            edges.add(edge("kill", "logic_status", "settle", "result"));
            edges.add(edge("kill", "logic_status", "done", "logic_in"));
        }
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.TASK,
            id,
            title,
            new CanonicalGraph(nodes, edges));
    }

    private static CanonicalGraphResource resourceWithObjectives(String id, int count) {
        List<CanonicalGraphNode> nodes = new ArrayList<CanonicalGraphNode>();
        nodes.add(node("activate", "activate", Collections.singletonList(port("logic_out", false, 0)), empty()));
        for (int index = 0; index < count; index++) nodes.add(
            node(
                "objective-" + index,
                "objective",
                Arrays.asList(port("prerequisite", true, 0), port("logic_status", false, 1)),
                objective("kill_entity", 1, "entity", "entity-" + index)));
        nodes.add(node("settle", "settle", Collections.singletonList(port("result", true, 0)), empty()));
        List<CanonicalGraphConnection> edges = new ArrayList<CanonicalGraphConnection>();
        for (int index = 0; index < count; index++)
            edges.add(edge("activate", "logic_out", "objective-" + index, "prerequisite"));
        edges.add(edge("objective-0", "logic_status", "settle", "result"));
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.TASK,
            id,
            "Many Objectives",
            new CanonicalGraph(nodes, edges));
    }

    private static CanonicalGraphResource malformedResource(CanonicalGraphResource source, String mode) {
        List<CanonicalGraphNode> nodes = new ArrayList<CanonicalGraphNode>(
            source.getGraph()
                .getNodes());
        Map<String, JsonElement> properties = new LinkedHashMap<String, JsonElement>(
            nodes.get(2)
                .getProperties());
        if ("unsupported-type".equals(mode)) properties.put("objective_type", json("unsupported"));
        else if ("metadata-object".equals(mode)) properties.put("metadata", json("not-an-object"));
        else properties.put("metadata", new JsonParser().parse("{\"grade\":7}"));
        nodes.set(
            2,
            node(
                "collect",
                "objective",
                nodes.get(2)
                    .getPorts(),
                properties));
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.TASK,
            source.getId(),
            source.getDisplayName(),
            new CanonicalGraph(
                nodes,
                source.getGraph()
                    .getConnections()));
    }

    private static Map<String, JsonElement> objective(String type, int required, String key, String value) {
        return objective(type, required, key, value, null, null);
    }

    private static Map<String, JsonElement> objective(String type, int required, String key, String value,
        String extraKey, String extraValue) {
        Map<String, JsonElement> result = new LinkedHashMap<String, JsonElement>();
        result.put("objective_type", json(type));
        result.put("description", json(type + " objective"));
        if (!"interact_actor".equals(type)) result.put("required", new JsonParser().parse(Integer.toString(required)));
        result.put("prerequisite_enabled", new JsonParser().parse("true"));
        result.put(key, json(value));
        if (extraKey != null) result.put(extraKey, new JsonParser().parse(extraValue));
        return result;
    }

    private static CanonicalGraphNode node(String id, String type, List<CanonicalGraphPort> ports,
        Map<String, JsonElement> properties) {
        return new CanonicalGraphNode(id, type, id, ports, properties);
    }

    private static CanonicalGraphPort port(String id, boolean input, int order) {
        return new CanonicalGraphPort(
            id,
            id,
            input ? CanonicalGraphPortDirection.INPUT : CanonicalGraphPortDirection.OUTPUT,
            CanonicalGraphInterfaceKind.LOGIC,
            order);
    }

    private static CanonicalGraphConnection edge(String a, String b, String c, String d) {
        return new CanonicalGraphConnection(a, b, c, d, CanonicalGraphInterfaceKind.LOGIC);
    }

    private static Map<String, JsonElement> props(String... values) {
        Map<String, JsonElement> result = new LinkedHashMap<String, JsonElement>();
        for (int i = 0; i < values.length; i += 2) result.put(values[i], json(values[i + 1]));
        return result;
    }

    private static Map<String, JsonElement> empty() {
        return Collections.emptyMap();
    }

    private static JsonElement json(String value) {
        return new JsonParser().parse("\"" + value + "\"");
    }

    private static void expectUnsupported(List<?> values) {
        try {
            values.clear();
            throw new AssertionError("Expected immutable list");
        } catch (UnsupportedOperationException expected) {}
    }

    private static void expectUnsupported(Map<?, ?> values) {
        try {
            values.clear();
            throw new AssertionError("Expected immutable map");
        } catch (UnsupportedOperationException expected) {}
    }

    private static void expectFailure(Runnable action) {
        try {
            action.run();
            throw new AssertionError("Expected projection failure");
        } catch (RuntimeException expected) {}
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
