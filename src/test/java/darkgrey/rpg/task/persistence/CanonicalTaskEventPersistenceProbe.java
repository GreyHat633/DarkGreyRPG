package darkgrey.rpg.task.persistence;

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
import darkgrey.rpg.task.event.CanonicalTaskDispatchResult;
import darkgrey.rpg.task.event.CanonicalTaskInstanceIdentity;
import darkgrey.rpg.task.instance.CanonicalTaskResourceResolver;
import darkgrey.rpg.task.runtime.CanonicalTaskEvent;
import darkgrey.rpg.task.runtime.CanonicalTaskObjectiveStatus;

/** Focused offline proof for Task SavedData, indexed dispatch, and pending-byte fencing. */
public final class CanonicalTaskEventPersistenceProbe {

    private static final UUID PLAYER = UUID.fromString("00000000-0000-0000-0000-000000000001");
    private static final UUID OTHER = UUID.fromString("00000000-0000-0000-0000-000000000002");

    private CanonicalTaskEventPersistenceProbe() {}

    public static void main(String[] args) {
        CanonicalGraphResource parallel = resource(
            "parallel",
            specs(
                spec("kill_a", CanonicalTaskEvent.KILL_ENTITY, "slime", null, 1),
                spec("kill_b", CanonicalTaskEvent.KILL_ENTITY, "slime", null, 1)));
        CanonicalGraphResource sequential = sequentialResource("sequential");
        CanonicalGraphResource collect = resource(
            "collect",
            specs(spec("collect", CanonicalTaskEvent.COLLECT_ITEM, "iron", "raw", 1)));
        CanonicalGraphResource interact = resource(
            "interact",
            specs(spec("interact", CanonicalTaskEvent.INTERACT_ACTOR, "actor_7", null, 1)));
        CanonicalGraphResource settled = settledResource("settled");
        CanonicalGraphResource many = resource("many", manySpecs(100));
        parallelAndDuplicate(parallel, sequential);
        collectAndInteract(collect, interact);
        settlementCancellationAndErrors(settled, parallel);
        persistenceAndAtomicBind(parallel);
        playerAndRetainedResourceIsolation(parallel);
        candidateBoundScale(many);
        System.out.println("TASK_EVENT_PERSISTENCE_PROBE_PASS");
    }

    private static void parallelAndDuplicate(CanonicalGraphResource parallel, CanonicalGraphResource sequential) {
        CanonicalTaskSavedData data = new CanonicalTaskSavedData();
        data.start(PLAYER, "parallel", "placement", parallel, 1L);
        require(
            data.getSubscriptionIndex()
                .candidateCount(PLAYER, CanonicalTaskEvent.killEntity("slime")) == 1,
            "duplicate candidate");
        data.setDirty(false);
        CanonicalTaskDispatchResult first = data.dispatch(PLAYER, CanonicalTaskEvent.killEntity("slime"), 2L);
        require(first.getCandidateCount() == 1 && first.getChangedInstanceCount() == 1, "parallel candidate");
        require(
            data.getSnapshot(PLAYER, "parallel", "placement")
                .getRuntimeSnapshot()
                .getProgress()
                .get("kill_a")
                .intValue() == 1,
            "parallel first");
        require(
            data.getSnapshot(PLAYER, "parallel", "placement")
                .getRuntimeSnapshot()
                .getProgress()
                .get("kill_b")
                .intValue() == 1,
            "parallel second");
        CanonicalTaskSavedData sequenceData = new CanonicalTaskSavedData();
        sequenceData.start(PLAYER, "sequential", "placement", sequential, 1L);
        require(
            sequenceData.dispatch(PLAYER, CanonicalTaskEvent.killEntity("slime"), 2L)
                .getChangedInstanceCount() == 1,
            "sequential first");
        require(
            sequenceData.getSnapshot(PLAYER, "sequential", "placement")
                .getRuntimeSnapshot()
                .getObjectiveStatuses()
                .get("interact") == CanonicalTaskObjectiveStatus.ACTIVE,
            "sequential enabled");
        require(
            sequenceData.getSnapshot(PLAYER, "sequential", "placement")
                .getRuntimeSnapshot()
                .getProgress()
                .get("interact")
                .intValue() == 0,
            "new objective does not consume event");
        require(
            sequenceData.dispatch(PLAYER, CanonicalTaskEvent.interactActor("actor_7"), 3L)
                .getChangedInstanceCount() == 1,
            "sequential next");
    }

    private static void collectAndInteract(CanonicalGraphResource collect, CanonicalGraphResource interact) {
        CanonicalTaskSavedData data = new CanonicalTaskSavedData();
        data.start(PLAYER, "collect", "placement", collect, 1L);
        require(
            data.getSubscriptionIndex()
                .candidateCount(PLAYER, CanonicalTaskEvent.collectItem("iron", 1)) == 1,
            "collect coarse index");
        require(
            data.dispatch(PLAYER, CanonicalTaskEvent.collectItem("iron", 1), 2L)
                .getChangedInstanceCount() == 0,
            "collect metadata filter");
        require(
            data.dispatch(PLAYER, CanonicalTaskEvent.collectItem("iron", metadata("grade", "raw"), 1), 3L)
                .getChangedInstanceCount() == 1,
            "collect metadata accepted");
        data.start(PLAYER, "interact", "placement", interact, 1L);
        require(
            data.dispatch(PLAYER, CanonicalTaskEvent.interactActor("other"), 4L)
                .getCandidateCount() == 0,
            "interact target filter");
        require(
            data.dispatch(PLAYER, CanonicalTaskEvent.interactActor("actor_7"), 5L)
                .getCandidateCount() == 1,
            "interact target");
    }

    private static void settlementCancellationAndErrors(CanonicalGraphResource settled,
        CanonicalGraphResource parallel) {
        CanonicalTaskSavedData data = new CanonicalTaskSavedData();
        data.start(PLAYER, "story-a", "settled", settled, 1L);
        data.setDirty(false);
        require(
            data.dispatch(PLAYER, CanonicalTaskEvent.killEntity("slime"), 2L)
                .getSettledInstances()
                .size() == 1
                && data.getSubscriptionIndex()
                    .subscriptionCount() == 0,
            "settlement unsubscribe");
        data.setDirty(false);
        require(
            data.dispatch(PLAYER, CanonicalTaskEvent.killEntity("slime"), 3L)
                .getCandidateCount() == 0 && !data.isDirty(),
            "terminal no-op");
        data.start(PLAYER, "story-a", "cancelled", parallel, 1L);
        data.start(PLAYER, "story-b", "retained", parallel, 1L);
        require(data.cancelByStory(PLAYER, "story-a") == 1, "exact cancellation");
        require(
            data.getSubscriptionIndex()
                .candidateCount(PLAYER, CanonicalTaskEvent.killEntity("slime")) == 1,
            "other story indexed");
        require(
            data.dispatch(PLAYER, CanonicalTaskEvent.killEntity("slime"), 4L)
                .getCandidateCount() == 1,
            "other story receives event");
        CanonicalTaskSavedData errors = new CanonicalTaskSavedData();
        CanonicalGraphResource errorResource = resource(
            "error",
            specs(
                spec("kill_a", CanonicalTaskEvent.KILL_ENTITY, "slime", null, 2),
                spec("kill_b", CanonicalTaskEvent.KILL_ENTITY, "slime", null, 2)));
        errors.start(PLAYER, "a-error", "placement", errorResource, 20L);
        errors.start(PLAYER, "b-ok", "placement", errorResource, 1L);
        CanonicalTaskDispatchResult result = errors.dispatch(PLAYER, CanonicalTaskEvent.killEntity("slime"), 10L);
        require(
            result.getCandidateCount() == 2 && result.getErroredInstances()
                .size() == 1 && result.getChangedInstanceCount() == 1,
            "error isolation");
        CanonicalTaskInstanceIdentity identity = result.getErroredInstances()
            .get(0);
        require(
            "a-error".equals(identity.getStoryInstanceId()) && errors.getSubscriptionIndex()
                .candidateCount(PLAYER, CanonicalTaskEvent.killEntity("slime")) == 1,
            "error unsubscribe");
    }

    private static void persistenceAndAtomicBind(CanonicalGraphResource resource) {
        resource = resource(
            "partial",
            specs(
                spec("kill_a", CanonicalTaskEvent.KILL_ENTITY, "slime", null, 2),
                spec("kill_b", CanonicalTaskEvent.KILL_ENTITY, "slime", null, 2)));
        CanonicalTaskSavedData data = new CanonicalTaskSavedData();
        data.start(PLAYER, "story", "placement", resource, 1L);
        data.setDirty(false);
        require(
            data.start(PLAYER, "story", "placement", resource)
                .getTaskNodePlacementId()
                .equals("placement"),
            "re-entry");
        require(!data.isDirty(), "re-entry clean");
        data.dispatch(PLAYER, CanonicalTaskEvent.killEntity("slime"), 2L);
        net.minecraft.nbt.NBTTagCompound raw = new net.minecraft.nbt.NBTTagCompound();
        data.writeToNBT(raw);
        CanonicalTaskSavedData pending = new CanonicalTaskSavedData("pending");
        pending.readFromNBT(raw);
        net.minecraft.nbt.NBTTagCompound roundTrip = new net.minecraft.nbt.NBTTagCompound();
        pending.writeToNBT(roundTrip);
        require(raw.equals(roundTrip) && !pending.isBound(), "pending round trip");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                pending.bind(new CanonicalTaskResourceResolver() {

                    @Override
                    public CanonicalGraphResource resolve(String id) {
                        return null;
                    }
                });
            }
        }, "missing bind");
        require(raw.equals(pending.getPendingRaw()) && !pending.isBound(), "missing bind preserves raw");
        pending.bind(resolver(resource));
        require(
            pending.isBound() && pending.getSubscriptionIndex()
                .subscriptionCount() == 1,
            "restart reindex");
        CanonicalTaskSavedData bound = new CanonicalTaskSavedData();
        bound.start(PLAYER, "story", "placement", resource, 1L);
        net.minecraft.nbt.NBTTagCompound before = new net.minecraft.nbt.NBTTagCompound();
        bound.writeToNBT(before);
        CanonicalGraphResource drift = resource(
            "partial",
            specs(
                spec("kill_a", CanonicalTaskEvent.KILL_ENTITY, "skeleton", null, 2),
                spec("kill_b", CanonicalTaskEvent.KILL_ENTITY, "skeleton", null, 2)));
        expectFailure(new Runnable() {

            @Override
            public void run() {
                bound.bind(resolver(drift));
            }
        }, "fingerprint bind");
        net.minecraft.nbt.NBTTagCompound after = new net.minecraft.nbt.NBTTagCompound();
        bound.writeToNBT(after);
        require(
            before.equals(after) && bound.isBound()
                && bound.getSubscriptionIndex()
                    .subscriptionCount() == 1,
            "failed bound bind preserves state");
        net.minecraft.nbt.NBTTagCompound malformed = new net.minecraft.nbt.NBTTagCompound();
        malformed.setInteger("schema_version", 99);
        expectFailure(new Runnable() {

            @Override
            public void run() {
                pending.readFromNBT(malformed);
            }
        }, "malformed read");
        require(
            pending.isBound() && pending.snapshots()
                .size() == 1,
            "malformed read preserves state");
    }

    private static void candidateBoundScale(CanonicalGraphResource many) {
        CanonicalTaskSavedData data = new CanonicalTaskSavedData();
        data.start(PLAYER, "many", "placement", many, 1L);
        require(
            data.getSubscriptionIndex()
                .getKeyCount() == 1
                && data.getSubscriptionIndex()
                    .getSubscriptionCount() == 1,
            "100 objectives one candidate");
        require(
            data.dispatch(PLAYER, CanonicalTaskEvent.killEntity("slime"), 2L)
                .getCandidateCount() == 1,
            "candidate bounded");
    }

    private static void playerAndRetainedResourceIsolation(CanonicalGraphResource original) {
        CanonicalTaskSavedData isolated = new CanonicalTaskSavedData();
        isolated.start(PLAYER, "story", "placement", original, 1L);
        isolated.start(OTHER, "story", "placement", original, 1L);
        require(
            isolated.getSubscriptionIndex()
                .candidateCount(PLAYER, CanonicalTaskEvent.killEntity("slime")) == 1,
            "player key excludes other player");
        require(
            isolated.getSubscriptionIndex()
                .candidateCount(OTHER, CanonicalTaskEvent.killEntity("slime")) == 1,
            "other player remains independently queryable");
        require(
            isolated.dispatch(PLAYER, CanonicalTaskEvent.killEntity("slime"), 2L)
                .getCandidateCount() == 1,
            "dispatch remains player scoped");
        require(
            isolated.getSnapshot(OTHER, "story", "placement")
                .getRuntimeSnapshot()
                .getProgress()
                .get("kill_a")
                .intValue() == 0,
            "other player progress untouched");

        CanonicalTaskSavedData retained = new CanonicalTaskSavedData();
        retained.start(PLAYER, "story", "placement", original, 1L);
        retained.setDirty(false);
        CanonicalGraphResource drift = resource(
            "parallel",
            specs(
                spec("kill_a", CanonicalTaskEvent.KILL_ENTITY, "skeleton", null, 1),
                spec("kill_b", CanonicalTaskEvent.KILL_ENTITY, "skeleton", null, 1)));
        retained.start(PLAYER, "story", "placement", drift);
        require(!retained.isDirty(), "same-id semantic drift re-entry is no-op");
        require(
            retained.getSubscriptionIndex()
                .candidateCount(PLAYER, CanonicalTaskEvent.killEntity("slime")) == 1,
            "retained runtime target remains indexed");
        require(
            retained.getSubscriptionIndex()
                .candidateCount(PLAYER, CanonicalTaskEvent.killEntity("skeleton")) == 0,
            "drifted target is not indexed");
    }

    private static CanonicalGraphResource sequentialResource(String id) {
        List<Spec> values = specs(
            spec("kill", CanonicalTaskEvent.KILL_ENTITY, "slime", null, 1),
            spec("interact", CanonicalTaskEvent.INTERACT_ACTOR, "actor_7", null, 1));
        return resource(
            id,
            values,
            Arrays.asList(
                new CanonicalGraphConnection(
                    "activate",
                    "logic_out",
                    "kill",
                    "logic_enable",
                    CanonicalGraphInterfaceKind.LOGIC),
                new CanonicalGraphConnection(
                    "kill",
                    "logic_status",
                    "interact",
                    "logic_enable",
                    CanonicalGraphInterfaceKind.LOGIC)));
    }

    private static CanonicalGraphResource settledResource(String id) {
        return resource(
            id,
            specs(spec("kill", CanonicalTaskEvent.KILL_ENTITY, "slime", null, 1)),
            Arrays.asList(
                new CanonicalGraphConnection(
                    "activate",
                    "logic_out",
                    "kill",
                    "logic_enable",
                    CanonicalGraphInterfaceKind.LOGIC),
                new CanonicalGraphConnection(
                    "kill",
                    "logic_status",
                    "settle",
                    "done",
                    CanonicalGraphInterfaceKind.LOGIC)));
    }

    private static CanonicalGraphResource resource(String id, List<Spec> specs) {
        return resource(id, specs, activationEdges(specs));
    }

    private static CanonicalGraphResource resource(String id, List<Spec> specs, List<CanonicalGraphConnection> edges) {
        List<CanonicalGraphNode> nodes = new ArrayList<CanonicalGraphNode>();
        nodes.add(node("activate", "activate", Collections.singletonList(port("logic_out", false, 0)), empty()));
        for (Spec spec : specs) nodes.add(
            node(
                spec.id,
                "objective",
                Arrays.asList(port("logic_enable", true, 0), port("logic_status", false, 1)),
                spec.properties()));
        nodes.add(node("settle", "settle", Collections.singletonList(port("done", true, 0)), empty()));
        return new CanonicalGraphResource(1, CanonicalGraphResourceKind.TASK, id, id, new CanonicalGraph(nodes, edges));
    }

    private static List<CanonicalGraphConnection> activationEdges(List<Spec> specs) {
        List<CanonicalGraphConnection> edges = new ArrayList<CanonicalGraphConnection>();
        for (Spec spec : specs) edges.add(
            new CanonicalGraphConnection(
                "activate",
                "logic_out",
                spec.id,
                "logic_enable",
                CanonicalGraphInterfaceKind.LOGIC));
        return edges;
    }

    private static List<Spec> manySpecs(int count) {
        List<Spec> specs = new ArrayList<Spec>();
        for (int i = 0; i < count; i++)
            specs.add(spec("objective_" + i, CanonicalTaskEvent.KILL_ENTITY, "slime", null, 1));
        return specs;
    }

    private static List<Spec> specs(Spec... values) {
        return Arrays.asList(values);
    }

    private static Spec spec(String id, String type, String target, String metadata, int required) {
        return new Spec(id, type, target, metadata, required);
    }

    private static Map<String, String> metadata(String key, String value) {
        Map<String, String> result = new LinkedHashMap<String, String>();
        result.put(key, value);
        return result;
    }

    private static Map<String, JsonElement> empty() {
        return Collections.emptyMap();
    }

    private static CanonicalGraphNode node(String id, String type, List<CanonicalGraphPort> ports,
        Map<String, JsonElement> props) {
        return new CanonicalGraphNode(id, type, id, ports, props);
    }

    private static CanonicalGraphPort port(String id, boolean input, int order) {
        return new CanonicalGraphPort(
            id,
            id,
            input ? CanonicalGraphPortDirection.INPUT : CanonicalGraphPortDirection.OUTPUT,
            CanonicalGraphInterfaceKind.LOGIC,
            order);
    }

    private static JsonElement json(String value) {
        return new JsonParser().parse("\"" + value + "\"");
    }

    private static CanonicalTaskResourceResolver resolver(final CanonicalGraphResource resource) {
        return new CanonicalTaskResourceResolver() {

            @Override
            public CanonicalGraphResource resolve(String id) {
                return resource.getId()
                    .equals(id) ? resource : null;
            }
        };
    }

    private static void expectFailure(Runnable action, String label) {
        try {
            action.run();
            throw new AssertionError("Expected failure: " + label);
        } catch (RuntimeException expected) {}
    }

    private static void require(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }

    private static final class Spec {

        private final String id, type, target, metadata;
        private final int required;

        private Spec(String id, String type, String target, String metadata, int required) {
            this.id = id;
            this.type = type;
            this.target = target;
            this.metadata = metadata;
            this.required = required;
        }

        private Map<String, JsonElement> properties() {
            Map<String, JsonElement> result = new LinkedHashMap<String, JsonElement>();
            result.put("objective_type", json(type));
            result.put("description", json(id));
            result.put("required", new JsonParser().parse(Integer.toString(required)));
            if (CanonicalTaskEvent.KILL_ENTITY.equals(type)) result.put("entity", json(target));
            else if (CanonicalTaskEvent.COLLECT_ITEM.equals(type)) {
                result.put("item", json(target));
                result.put(
                    "metadata",
                    metadata == null ? new JsonParser().parse("{}")
                        : new JsonParser().parse("{\"grade\":\"" + metadata + "\"}"));
            } else result.put("actor_id", json(target));
            return result;
        }
    }
}
