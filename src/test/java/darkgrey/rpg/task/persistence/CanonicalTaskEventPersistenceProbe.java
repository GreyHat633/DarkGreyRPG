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
import darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus;
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
        packageGenerationRetirement(parallel);
        playerStoryDiscardBoundAndPending(parallel, settled);
        candidateBoundScale(many);
        System.out.println("TASK_EVENT_PERSISTENCE_PROBE_PASS");
        System.out.println("CANONICAL_TASK_GENERATION_RETIREMENT=PASS");
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

    private static void packageGenerationRetirement(CanonicalGraphResource resource) {
        CanonicalTaskSavedData data = new CanonicalTaskSavedData();
        data.start(PLAYER, "retired_story", "retired-placement", resource, 1L);
        data.start(PLAYER, "retained_story", "retained-placement", resource, 1L);
        require(
            data.discardByStoryIds(Collections.singleton("retired_story")) == 1,
            "Generation retirement did not remove the selected Task");
        require(
            data.getSnapshot(PLAYER, "retired_story", "retired-placement") == null,
            "Retired Task remained persisted");
        require(
            data.getSnapshot(PLAYER, "retained_story", "retained-placement") != null,
            "Generation retirement touched another Task");
        require(
            data.getSubscriptionIndex()
                .candidateCount(PLAYER, CanonicalTaskEvent.killEntity("slime")) == 1,
            "Generation retirement left a stale Task subscription");
    }

    private static void playerStoryDiscardBoundAndPending(CanonicalGraphResource parallel,
        CanonicalGraphResource settled) {
        CanonicalGraphResource active = resource(
            "purge-active",
            specs(spec("kill", CanonicalTaskEvent.KILL_ENTITY, "slime", null, 2)));

        CanonicalTaskSavedData bound = new CanonicalTaskSavedData();
        bound.start(PLAYER, "purge", "cancelled", active, 1L);
        bound.cancelByStory(PLAYER, "purge");
        bound.start(PLAYER, "purge", "error", active, 1L);
        bound.markError(PLAYER, "purge", "error");
        bound.start(PLAYER, "purge", "active", active, 1L);
        bound.start(PLAYER, "purge", "terminal", settled, 1L);
        bound.start(PLAYER, "keep", "placement", active, 1L);
        bound.start(OTHER, "purge", "placement", parallel, 1L);
        bound.dispatch(PLAYER, CanonicalTaskEvent.killEntity("slime"), 2L);
        require(
            bound.getSnapshot(PLAYER, "purge", "terminal")
                .getStatus() == CanonicalTaskInstanceStatus.SETTLED,
            "purge fixture terminal status");
        require(
            bound.getSnapshot(PLAYER, "purge", "terminal")
                .getRuntimeSnapshot()
                .getProgress()
                .get("kill")
                .intValue() == 1
                && bound.getSnapshot(PLAYER, "purge", "terminal")
                    .getResultPortId() != null,
            "purge fixture progress/result");
        require(
            bound.getSubscriptionIndex()
                .candidateCount(PLAYER, CanonicalTaskEvent.killEntity("slime")) == 2,
            "purge fixture indexed active Tasks");
        bound.setDirty(false);
        require(bound.discardByPlayerStory(PLAYER, "purge") == 4, "bound player/story purge count");
        require(bound.getSnapshot(PLAYER, "purge", "cancelled") == null, "bound cancelled Task remained");
        require(bound.getSnapshot(PLAYER, "purge", "error") == null, "bound error Task remained");
        require(bound.isDirty(), "bound player/story purge dirty");
        require(bound.getSnapshot(PLAYER, "purge", "active") == null, "bound active Task remained");
        require(bound.getSnapshot(PLAYER, "purge", "terminal") == null, "bound terminal Task remained");
        require(bound.getSnapshot(PLAYER, "keep", "placement") != null, "bound other Story was removed");
        require(bound.getSnapshot(OTHER, "purge", "placement") != null, "bound other player was removed");
        require(
            bound.getSubscriptionIndex()
                .candidateCount(PLAYER, CanonicalTaskEvent.killEntity("slime")) == 1,
            "bound purge left stale selected-player subscription");
        require(
            bound.getSubscriptionIndex()
                .candidateCount(OTHER, CanonicalTaskEvent.killEntity("slime")) == 1,
            "bound purge removed other-player subscription");

        CanonicalTaskSavedData source = new CanonicalTaskSavedData();
        source.start(PLAYER, "purge", "cancelled", active, 1L);
        source.cancelByStory(PLAYER, "purge");
        source.start(PLAYER, "purge", "error", active, 1L);
        source.markError(PLAYER, "purge", "error");
        source.start(PLAYER, "purge", "active", active, 1L);
        source.start(PLAYER, "purge", "terminal", settled, 1L);
        source.start(PLAYER, "keep", "placement", active, 1L);
        source.start(OTHER, "purge", "placement", parallel, 1L);
        source.dispatch(PLAYER, CanonicalTaskEvent.killEntity("slime"), 2L);
        net.minecraft.nbt.NBTTagCompound raw = new net.minecraft.nbt.NBTTagCompound();
        source.writeToNBT(raw);
        CanonicalTaskSavedData pending = new CanonicalTaskSavedData("purge-pending");
        pending.readFromNBT(raw);
        pending.setDirty(false);
        require(pending.discardByPlayerStory(PLAYER, "purge") == 4, "pending player/story purge count");
        require(pending.isDirty(), "pending player/story purge dirty");
        Map<String, CanonicalGraphResource> resources = new LinkedHashMap<String, CanonicalGraphResource>();
        resources.put(active.getId(), active);
        resources.put(settled.getId(), settled);
        resources.put(parallel.getId(), parallel);
        pending.bind(resolver(resources));
        require(pending.getSnapshot(PLAYER, "purge", "cancelled") == null, "pending cancelled Task remained");
        require(pending.getSnapshot(PLAYER, "purge", "error") == null, "pending error Task remained");
        require(pending.getSnapshot(PLAYER, "purge", "active") == null, "pending active Task remained");
        require(pending.getSnapshot(PLAYER, "purge", "terminal") == null, "pending terminal Task remained");
        require(pending.getSnapshot(PLAYER, "keep", "placement") != null, "pending other Story was removed");
        require(pending.getSnapshot(OTHER, "purge", "placement") != null, "pending other player was removed");
        require(
            pending.getSubscriptionIndex()
                .candidateCount(PLAYER, CanonicalTaskEvent.killEntity("slime")) == 1,
            "pending purge rebuilt stale selected-player subscription");
        require(
            pending.getSubscriptionIndex()
                .candidateCount(OTHER, CanonicalTaskEvent.killEntity("slime")) == 1,
            "pending purge removed other-player subscription");
        System.out.println("STORY_REPEAT_PLAYER_ISOLATION=PASS");
        System.out.println("STORY_REPEAT_OTHER_STORY_ISOLATION=PASS");
        System.out.println("STORY_REPEAT_ALL_TASK_STATUSES_PURGED=PASS");
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
                    "prerequisite",
                    CanonicalGraphInterfaceKind.LOGIC),
                new CanonicalGraphConnection(
                    "kill",
                    "logic_status",
                    "interact",
                    "prerequisite",
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
                    "prerequisite",
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
                Arrays.asList(port("prerequisite", true, 0), port("logic_status", false, 1)),
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
                "prerequisite",
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

    private static CanonicalTaskResourceResolver resolver(final Map<String, CanonicalGraphResource> resources) {
        return new CanonicalTaskResourceResolver() {

            @Override
            public CanonicalGraphResource resolve(String id) {
                return resources.get(id);
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
            result.put("prerequisite_enabled", new JsonParser().parse("true"));
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
