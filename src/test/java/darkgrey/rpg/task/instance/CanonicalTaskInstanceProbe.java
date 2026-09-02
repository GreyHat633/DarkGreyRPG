package darkgrey.rpg.task.instance;

import java.util.Arrays;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.UUID;
import java.util.function.LongSupplier;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

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
import darkgrey.rpg.task.runtime.CanonicalTaskEvent;
import darkgrey.rpg.task.runtime.CanonicalTaskObjectiveStatus;
import darkgrey.rpg.task.runtime.CanonicalTaskSnapshot;
import darkgrey.rpg.task.runtime.CanonicalTaskStatus;

/** Focused server-neutral TaskInstance lifecycle and persistence probe. */
public final class CanonicalTaskInstanceProbe {

    public static void main(String[] args) {
        final long[] time = new long[] { 100L };
        CanonicalTaskInstanceStore store = new CanonicalTaskInstanceStore(new LongSupplier() {

            @Override
            public long getAsLong() {
                return time[0];
            }
        });
        UUID player = UUID.fromString("11111111-1111-1111-1111-111111111111");
        UUID other = UUID.fromString("22222222-2222-2222-2222-222222222222");
        CanonicalGraphResource activeResource = resource("task_probe", 2);
        CanonicalTaskInstance active = store.start(player, "story-a", "placement-a", activeResource, 101L);
        require(active.isActive() && active.getActivationTime() == 101L, "first entry active");
        time[0] = 110L;
        store.acceptEvent(player, "story-a", "placement-a", CanonicalTaskEvent.killEntity("slime"));
        NBTTagCompound activeNbt = store.writeToNbt();
        CanonicalTaskInstanceStore restored = new CanonicalTaskInstanceStore(new LongSupplier() {

            @Override
            public long getAsLong() {
                return 999L;
            }
        });
        restored.readFromNbt(activeNbt, resolver(activeResource));
        require(
            restored.size() == 1 && restored.get(player, "story-a", "placement-a")
                .getRuntime()
                .getProgress()
                .get("kill")
                .intValue() == 1,
            "active round trip");
        require(store.start(player, "story-a", "placement-a", activeResource) == active, "same instance re-entry");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                store.start(player, "story-a", "placement-a", resource("other", 2));
            }
        }, "different resource");
        time[0] = 130L;
        require(
            store.acceptEvent(player, "story-a", "placement-a", CanonicalTaskEvent.killEntity("slime")),
            "settling event");
        CanonicalTaskInstanceSnapshot settled = store.get(player, "story-a", "placement-a")
            .snapshot();
        require(
            settled.getStatus() == CanonicalTaskInstanceStatus.SETTLED && settled.getSettlementTime()
                .longValue() == 130L,
            "settlement timestamp");
        require(
            settled.getRuntimeSnapshot()
                .getPublicLogicOutputs()
                .get("done")
                .booleanValue(),
            "retained public logic");
        require(
            store.start(player, "story-a", "placement-a", activeResource) == active
                && !store.acceptEvent(player, "story-a", "placement-a", CanonicalTaskEvent.killEntity("slime")),
            "settled re-entry");
        require(!store.markError(player, "story-a", "placement-a"), "settled error transition ignored");
        CanonicalTaskInstanceStore settledRestored = new CanonicalTaskInstanceStore();
        settledRestored.readFromNbt(store.writeToNbt(), resolver(activeResource));
        require(
            "success".equals(
                settledRestored.get(player, "story-a", "placement-a")
                    .getResultPortId()),
            "settled round trip result");
        CanonicalTaskInstance cancelled = store
            .start(other, "story-a", "placement-b", resource("other-player", 10), 140L);
        require(store.cancelByStory(player, "story-a") == 0, "unrelated placement only");
        require(
            store.cancelByStory(other, "story-a") == 1
                && cancelled.getStatus() == CanonicalTaskInstanceStatus.CANCELLED_BY_STORY_TERMINATION,
            "cancel by story");
        require(!store.markError(other, "story-a", "placement-b"), "cancelled error transition ignored");
        require(
            !store.acceptEvent(other, "story-a", "placement-b", CanonicalTaskEvent.killEntity("slime")),
            "terminal event ignore");
        prerequisitePersistence(player);
        malformedChecks(store, activeResource, player);
        System.out.println("TASK_INSTANCE_PROBE_PASS");
    }

    private static void prerequisitePersistence(UUID player) {
        CanonicalGraphResource resource = prerequisiteResource();
        CanonicalTaskInstanceStore source = new CanonicalTaskInstanceStore();
        CanonicalTaskInstance instance = source.start(player, "prerequisite", "inactive", resource, 400L);
        require(
            instance.getRuntime()
                .getObjectiveStatuses()
                .get("objective") == CanonicalTaskObjectiveStatus.INACTIVE,
            "prerequisite instance starts inactive");
        CanonicalTaskInstanceStore inactiveRestored = new CanonicalTaskInstanceStore();
        inactiveRestored.readFromNbt(source.writeToNbt(), resolver(resource));
        require(
            inactiveRestored.get(player, "prerequisite", "inactive")
                .getRuntime()
                .getObjectiveStatuses()
                .get("objective") == CanonicalTaskObjectiveStatus.INACTIVE,
            "inactive objective survives NBT restart");

        require(instance.setLogicInput("prerequisite_source", true, 401L), "prerequisite activates instance objective");
        require(instance.setLogicInput("prerequisite_source", false, 402L), "prerequisite false transition persists");
        require(
            instance.getRuntime()
                .isObjectiveActive("objective"),
            "activated instance objective is sticky");
        CanonicalTaskInstanceStore activeRestored = new CanonicalTaskInstanceStore();
        activeRestored.readFromNbt(source.writeToNbt(), resolver(resource));
        CanonicalTaskInstance restored = activeRestored.get(player, "prerequisite", "inactive");
        require(
            restored.getRuntime()
                .isObjectiveActive("objective"),
            "active objective survives NBT restart");
        require(restored.accept(CanonicalTaskEvent.killEntity("slime"), 403L), "restored objective completes");
        require(
            restored.getRuntime()
                .getObjectiveStatuses()
                .get("objective") == CanonicalTaskObjectiveStatus.COMPLETED,
            "completed objective remains completed");
        CanonicalTaskInstanceStore completedRestored = new CanonicalTaskInstanceStore();
        completedRestored.readFromNbt(activeRestored.writeToNbt(), resolver(resource));
        require(
            completedRestored.get(player, "prerequisite", "inactive")
                .getRuntime()
                .getObjectiveStatuses()
                .get("objective") == CanonicalTaskObjectiveStatus.COMPLETED,
            "completed objective survives NBT restart");
    }

    private static void malformedChecks(CanonicalTaskInstanceStore store, CanonicalGraphResource resource,
        UUID player) {
        CanonicalTaskInstanceStore activeSource = new CanonicalTaskInstanceStore();
        CanonicalTaskInstance active = activeSource.start(player, "malformed", "active", resource, 300L);
        activeSource.acceptEvent(player, "malformed", "active", CanonicalTaskEvent.killEntity("slime"), 305L);
        CanonicalTaskInstanceStore settledSource = new CanonicalTaskInstanceStore();
        settledSource.start(player, "malformed", "settled", resource("task_probe", 1), 300L);
        settledSource.acceptEvent(player, "malformed", "settled", CanonicalTaskEvent.killEntity("slime"), 305L);
        CanonicalTaskInstanceStore cancelledSource = new CanonicalTaskInstanceStore();
        cancelledSource.start(player, "malformed", "cancelled", resource, 300L);
        cancelledSource.acceptEvent(player, "malformed", "cancelled", CanonicalTaskEvent.killEntity("slime"), 305L);
        cancelledSource.cancelByStory(player, "malformed");
        CanonicalTaskInstanceStore errorSource = new CanonicalTaskInstanceStore();
        errorSource.start(player, "malformed", "error", resource, 300L);
        errorSource.acceptEvent(player, "malformed", "error", CanonicalTaskEvent.killEntity("slime"), 305L);
        require(errorSource.markError(player, "malformed", "error"), "active error transition");
        require(!errorSource.markError(player, "malformed", "error"), "error transition idempotence");
        require(
            !errorSource.acceptEvent(player, "malformed", "error", CanonicalTaskEvent.killEntity("slime"), 310L),
            "error event ignore");
        CanonicalTaskInstanceStore terminalRestored = new CanonicalTaskInstanceStore();
        terminalRestored.readFromNbt(cancelledSource.writeToNbt(), resolver(resource));
        require(
            terminalRestored.get(player, "malformed", "cancelled")
                .isCancelled(),
            "cancelled round trip");
        require(
            terminalRestored.get(player, "malformed", "cancelled")
                .getRuntime()
                .getProgress()
                .get("kill")
                .intValue() == 1 && terminalRestored.get(player, "malformed", "cancelled")
                    .getRuntime()
                    .getPublicLogicOutputs()
                    .containsKey("done"),
            "cancelled round trip retains runtime snapshot");
        CanonicalTaskInstanceStore errorRestored = new CanonicalTaskInstanceStore();
        errorRestored.readFromNbt(errorSource.writeToNbt(), resolver(resource));
        require(
            errorRestored.get(player, "malformed", "error")
                .isError()
                && errorRestored.get(player, "malformed", "error")
                    .getRuntime()
                    .getProgress()
                    .get("kill")
                    .intValue() == 1,
            "error round trip retains progress");
        require(
            errorRestored.get(player, "malformed", "error")
                .getRuntime()
                .getPublicLogicOutputs()
                .containsKey("done"),
            "error round trip retains public logic");
        require(
            !errorRestored.acceptEvent(player, "malformed", "error", CanonicalTaskEvent.killEntity("slime"), 310L),
            "restored error event ignore");

        NBTTagCompound unknown = CanonicalTaskInstanceNbtCodec.encode(Collections.singletonList(active.snapshot()));
        unknown.setString("unknown", "x");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalTaskInstanceNbtCodec.decode(unknown);
            }
        }, "unknown NBT field");
        NBTTagCompound wrongType = CanonicalTaskInstanceNbtCodec.encode(Collections.singletonList(active.snapshot()));
        wrongType.setString("schema_version", "1");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalTaskInstanceNbtCodec.decode(wrongType);
            }
        }, "wrong NBT type");
        NBTTagCompound duplicate = CanonicalTaskInstanceNbtCodec.encode(Collections.singletonList(active.snapshot()));
        NBTTagList duplicateEntries = (NBTTagList) duplicate.getTag("instances");
        duplicateEntries.appendTag(duplicateEntries.getCompoundTagAt(0));
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalTaskInstanceNbtCodec.decode(duplicate);
            }
        }, "duplicate identity");
        expectMalformed(activeSource, new NbtMutation() {

            @Override
            public void apply(NBTTagCompound root) {
                entry(root).setString(
                    "player_uuid",
                    "abcdefab-cdef-abcd-efab-cdefabcdefab".toUpperCase(java.util.Locale.ROOT));
            }
        }, "uppercase UUID");
        expectMalformed(activeSource, new NbtMutation() {

            @Override
            public void apply(NBTTagCompound root) {
                entry(root).setString("status", "BOGUS");
            }
        }, "invalid instance status");
        expectMalformed(activeSource, new NbtMutation() {

            @Override
            public void apply(NBTTagCompound root) {
                entry(root).setString("status", "NOT_STARTED");
            }
        }, "not started status");
        expectMalformed(activeSource, new NbtMutation() {

            @Override
            public void apply(NBTTagCompound root) {
                entry(root).setLong("settlement_time", 305L);
            }
        }, "settlement on active");
        expectMalformed(settledSource, new NbtMutation() {

            @Override
            public void apply(NBTTagCompound root) {
                entry(root).removeTag("settlement_time");
            }
        }, "missing settled time");
        expectMalformed(settledSource, new NbtMutation() {

            @Override
            public void apply(NBTTagCompound root) {
                entry(root).setLong("settlement_time", 299L);
            }
        }, "settlement before activation");
        expectMalformed(activeSource, new NbtMutation() {

            @Override
            public void apply(NBTTagCompound root) {
                NBTTagList list = (NBTTagList) entry(root).getTag("progress");
                list.appendTag(list.getCompoundTagAt(0));
            }
        }, "duplicate map key");
        expectMalformed(activeSource, new NbtMutation() {

            @Override
            public void apply(NBTTagCompound root) {
                ((NBTTagList) entry(root).getTag("progress")).getCompoundTagAt(0)
                    .setInteger("value", -1);
            }
        }, "negative progress");
        expectMalformed(activeSource, new NbtMutation() {

            @Override
            public void apply(NBTTagCompound root) {
                entry(root).setString("runtime_status", "BOGUS");
            }
        }, "invalid runtime enum");
        expectMalformed(activeSource, new NbtMutation() {

            @Override
            public void apply(NBTTagCompound root) {
                ((NBTTagList) entry(root).getTag("objective_statuses")).getCompoundTagAt(0)
                    .setString("value", "BOGUS");
            }
        }, "invalid objective enum");
        expectMalformed(activeSource, new NbtMutation() {

            @Override
            public void apply(NBTTagCompound root) {
                entry(root).setByte("activation_logic", (byte) 2);
            }
        }, "invalid boolean");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                new CanonicalTaskInstanceSnapshot(
                    player,
                    "malformed",
                    "cancelled-time",
                    resource.getId(),
                    CanonicalTaskInstanceStatus.CANCELLED_BY_STORY_TERMINATION,
                    300L,
                    Long.valueOf(305L),
                    active.snapshot()
                        .getRuntimeSnapshot());
            }
        }, "cancelled settlement invariant");
        Map<String, Integer> nullProgress = new LinkedHashMap<String, Integer>();
        nullProgress.put("null", null);
        CanonicalTaskSnapshot malformedRuntime = new CanonicalTaskSnapshot(
            resource.getId(),
            active.snapshot()
                .getRuntimeSnapshot()
                .getResourceFingerprint(),
            CanonicalTaskStatus.ACTIVE,
            nullProgress,
            Collections.<String, CanonicalTaskObjectiveStatus>emptyMap(),
            Collections.<String, Boolean>emptyMap(),
            Collections.<String, Boolean>emptyMap(),
            true,
            null);
        final CanonicalTaskInstanceSnapshot malformedSnapshot = new CanonicalTaskInstanceSnapshot(
            player,
            "malformed",
            "null-map",
            resource.getId(),
            CanonicalTaskInstanceStatus.ACTIVE,
            300L,
            null,
            malformedRuntime);
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalTaskInstanceNbtCodec.encode(Collections.singletonList(malformedSnapshot));
            }
        }, "stable encode failure");

        CanonicalTaskInstanceStore target = new CanonicalTaskInstanceStore();
        target.start(player, "keep", "placement", resource, 200L);
        String targetBefore = target.writeToNbt()
            .toString();
        NBTTagCompound missing = CanonicalTaskInstanceNbtCodec.encode(Collections.singletonList(active.snapshot()));
        expectFailure(new Runnable() {

            @Override
            public void run() {
                target.readFromNbt(missing, new CanonicalTaskResourceResolver() {

                    @Override
                    public CanonicalGraphResource resolve(String id) {
                        return null;
                    }
                });
            }
        }, "missing resource");
        require(
            targetBefore.equals(
                target.writeToNbt()
                    .toString()),
            "atomic restore byte stability");
        final NBTTagCompound malformedEntry = activeSource.writeToNbt();
        entry(malformedEntry).setString("unexpected", "x");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                target.readFromNbt(malformedEntry, resolver(resource));
            }
        }, "malformed entry restore");
        require(
            targetBefore.equals(
                target.writeToNbt()
                    .toString()),
            "malformed entry atomicity");
        CanonicalTaskInstanceStore fingerprintTarget = new CanonicalTaskInstanceStore();
        fingerprintTarget.start(player, "keep-fingerprint", "placement", resource, 200L);
        expectFailure(new Runnable() {

            @Override
            public void run() {
                fingerprintTarget.readFromNbt(
                    CanonicalTaskInstanceNbtCodec.encode(
                        Collections.singletonList(
                            store.snapshots()
                                .get(0))),
                    resolver(resource("task_probe", 2, "drifted display")));
            }
        }, "fingerprint mismatch");
        require(fingerprintTarget.size() == 1, "fingerprint restore atomicity");
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

    private static CanonicalGraphResource resource(String id, int required) {
        return resource(id, required, id);
    }

    private static CanonicalGraphResource resource(String id, int required, String display) {
        Map<String, JsonElement> objective = new LinkedHashMap<String, JsonElement>();
        objective.put("objective_type", json("kill_entity"));
        objective.put("description", json("kill"));
        objective.put("required", new JsonParser().parse(Integer.toString(required)));
        objective.put("entity", json("slime"));
        CanonicalGraphPort activateOut = port("logic_out", false, 0);
        CanonicalGraphPort status = port("logic_status", false, 0);
        CanonicalGraphPort settle = port("success", true, 0);
        CanonicalGraph graph = new CanonicalGraph(
            Arrays.asList(
                node(
                    "activate",
                    "activate",
                    Collections.singletonList(activateOut),
                    Collections.<String, JsonElement>emptyMap()),
                node("kill", "objective", Collections.singletonList(status), objective),
                node(
                    "settle",
                    "settle",
                    Collections.singletonList(settle),
                    Collections.<String, JsonElement>emptyMap()),
                node(
                    "published",
                    "logic_output",
                    Collections.singletonList(port("logic_in", true, 0)),
                    props("port_id", "done", "display_name", "Done"))),
            Arrays.asList(
                edge("kill", "logic_status", "settle", "success"),
                edge("kill", "logic_status", "published", "logic_in")));
        return new CanonicalGraphResource(1, CanonicalGraphResourceKind.TASK, id, display, graph);
    }

    private static CanonicalGraphResource prerequisiteResource() {
        Map<String, JsonElement> objective = new LinkedHashMap<String, JsonElement>();
        objective.put("objective_type", json("kill_entity"));
        objective.put("description", json("kill"));
        objective.put("required", new JsonParser().parse("1"));
        objective.put("entity", json("slime"));
        objective.put("prerequisite_enabled", new JsonParser().parse("true"));
        CanonicalGraph graph = new CanonicalGraph(
            Arrays.asList(
                node(
                    "prerequisite_source",
                    "logic_input",
                    Collections.singletonList(port("logic_out", false, 0)),
                    props("port_id", "prerequisite_source", "display_name", "Prerequisite")),
                node(
                    "objective",
                    "objective",
                    Arrays.asList(port("prerequisite", true, 0), port("logic_status", false, 1)),
                    objective),
                node(
                    "settle",
                    "settle",
                    Collections.singletonList(port("done", true, 0)),
                    Collections.<String, JsonElement>emptyMap())),
            Arrays.asList(
                edge("prerequisite_source", "logic_out", "objective", "prerequisite"),
                edge("objective", "logic_status", "settle", "done")));
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.TASK,
            "prerequisite_instance",
            "Prerequisite",
            graph);
    }

    private static CanonicalGraphNode node(String id, String type, java.util.List<CanonicalGraphPort> ports,
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

    private static JsonElement json(String value) {
        return new JsonParser().parse("\"" + value + "\"");
    }

    private static Map<String, JsonElement> props(String... values) {
        Map<String, JsonElement> result = new LinkedHashMap<String, JsonElement>();
        for (int i = 0; i < values.length; i += 2) result.put(values[i], json(values[i + 1]));
        return result;
    }

    private interface NbtMutation {

        void apply(NBTTagCompound root);
    }

    private static NBTTagCompound entry(NBTTagCompound root) {
        return ((NBTTagList) root.getTag("instances")).getCompoundTagAt(0);
    }

    private static void expectMalformed(final CanonicalTaskInstanceStore source, final NbtMutation mutation,
        String label) {
        final NBTTagCompound root = source.writeToNbt();
        mutation.apply(root);
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalTaskInstanceNbtCodec.decode(root);
            }
        }, label);
    }

    private static void expectFailure(Runnable action, String label) {
        try {
            action.run();
            throw new AssertionError("Expected " + label);
        } catch (RuntimeException expected) {}
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
