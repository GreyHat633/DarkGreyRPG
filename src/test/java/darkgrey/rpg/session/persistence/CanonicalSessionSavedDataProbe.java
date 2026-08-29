package darkgrey.rpg.session.persistence;

import java.util.Arrays;
import java.util.HashMap;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;
import net.minecraft.world.storage.MapStorage;

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
import darkgrey.rpg.session.forge.CanonicalSessionForgeProbeProject;
import darkgrey.rpg.session.instance.CanonicalSessionInstanceSnapshot;
import darkgrey.rpg.session.instance.CanonicalSessionResourceResolver;
import darkgrey.rpg.session.server.CanonicalSessionCompletionResult;
import darkgrey.rpg.session.server.CanonicalSessionDispatch;
import darkgrey.rpg.session.server.CanonicalSessionServerService;
import darkgrey.rpg.story.canonical.CanonicalStoryFlowTransition;
import darkgrey.rpg.story.canonical.CanonicalStoryPublicLogicSnapshot;
import darkgrey.rpg.story.canonical.CanonicalStorySessionCompletionRoute;
import darkgrey.rpg.story.canonical.CanonicalStorySessionCompletionRouter;

/** Offline proof for Forge persistence, delayed resource binding, and dirty fencing. */
public final class CanonicalSessionSavedDataProbe {

    private static final UUID PLAYER = UUID.fromString("00000000-0000-0000-0000-000000000001");

    private CanonicalSessionSavedDataProbe() {}

    public static void main(String[] args) {
        MapStorage storage = new MapStorage(null);
        CanonicalSessionSavedData data = CanonicalSessionSavedData.get(storage);
        require(data == CanonicalSessionSavedData.get(storage), "MapStorage create/reuse");
        CanonicalGraphResource resource = linearResource("persisted-session");
        CanonicalSessionInstanceSnapshot instance = data.start(PLAYER, "story", "placement", resource);
        require(data.isDirty(), "start dirties");
        data.setDirty(false);

        NBTTagCompound persistedWrapper = new NBTTagCompound();
        data.writeToNBT(persistedWrapper);
        NBTTagCompound payload = (NBTTagCompound) persistedWrapper.getCompoundTag("sessions")
            .copy();
        CanonicalSessionSavedData beforeBind = new CanonicalSessionSavedData("ignored");
        beforeBind.readFromNBT(payload);
        NBTTagCompound exact = new NBTTagCompound();
        beforeBind.writeToNBT(exact);
        require(payload.equals(exact), "write-before-bind exact preservation");
        require(!beforeBind.isBound() && beforeBind.hasPendingData(), "pending state");

        reject(new Runnable() {

            @Override
            public void run() {
                beforeBind.bind(new CanonicalSessionResourceResolver() {

                    @Override
                    public CanonicalGraphResource resolve(String id) {
                        return null;
                    }
                });
            }
        }, "missing resource bind");
        require(beforeBind.hasPendingData() && !beforeBind.isBound(), "failed bind retry state");
        beforeBind.bind(resolver(resource));
        require(beforeBind.isBound() && !beforeBind.hasPendingData(), "successful bind");
        require(
            beforeBind.getSnapshot(PLAYER, "story")
                .getTransportId() == instance.getTransportId(),
            "active restart");
        require(
            beforeBind.getCurrentStep(PLAYER, "story")
                .getNodeId()
                .equals("line"),
            "detached current step");
        NBTTagCompound migrated = new NBTTagCompound();
        beforeBind.writeToNBT(migrated);
        require(migrated.hasKey("sessions", 10) && migrated.hasKey("continuations", 9), "legacy migrates to wrapper");
        beforeBind.setDirty(false);
        reject(new Runnable() {

            @Override
            public void run() {
                beforeBind.continueLine(PLAYER, "story", 999L, "line");
            }
        }, "stale action");
        require(!beforeBind.isDirty(), "rejected action is clean");

        beforeBind.continueLine(PLAYER, "story", instance.getTransportId(), "line");
        require(beforeBind.isDirty(), "successful action dirties");
        beforeBind.setDirty(false);
        require(beforeBind.consume(PLAYER, "story", instance.getTransportId()), "completed consume");
        require(beforeBind.isDirty(), "successful consume dirties");
        beforeBind.setDirty(false);
        require(!beforeBind.consume(PLAYER, "story", instance.getTransportId()), "false consume");
        require(!beforeBind.isDirty(), "false consume is clean");

        NBTTagCompound empty = new NBTTagCompound();
        beforeBind.writeToNBT(empty);
        CanonicalSessionSavedData emptyRestart = new CanonicalSessionSavedData();
        emptyRestart.readFromNBT(empty);
        emptyRestart.bind(resolver(resource));
        require(
            emptyRestart.start(PLAYER, "fresh", "fresh-placement", resource)
                .getTransportId() == 2L,
            "empty-store counter restart");

        NBTTagCompound malformed = new NBTTagCompound();
        malformed.setString("tampered", "preserve-me");
        CanonicalSessionSavedData malformedData = new CanonicalSessionSavedData();
        reject(new Runnable() {

            @Override
            public void run() {
                malformedData.readFromNBT(malformed);
            }
        }, "direct malformed read");
        NBTTagCompound malformedOut = new NBTTagCompound();
        malformedData.writeToNBT(malformedOut);
        require(
            malformed.equals(malformedOut) && !malformedData.isBound() && malformedData.hasPendingData(),
            "malformed raw preservation");
        reject(new Runnable() {

            @Override
            public void run() {
                malformedData.start(PLAYER, "blocked", "blocked-placement", resource);
            }
        }, "malformed object cannot mutate");
        reject(new Runnable() {

            @Override
            public void run() {
                malformedData.bind(resolver(resource));
            }
        }, "malformed bind");
        require(malformedData.hasPendingData(), "malformed retry state");

        MapStorage convenienceStorage = new MapStorage(null);
        CanonicalSessionSavedData pending = new CanonicalSessionSavedData();
        pending.readFromNBT(payload);
        convenienceStorage.setData(CanonicalSessionSavedData.DATA_NAME, pending);
        require(
            CanonicalSessionSavedData.get(convenienceStorage, resolver(resource)) == pending && pending.isBound()
                && !pending.hasPendingData(),
            "convenience get and reuse");

        CanonicalSessionSavedData failureData = new CanonicalSessionSavedData();
        CanonicalGraphResource cycle = cycleResource("cycle-session");
        CanonicalSessionInstanceSnapshot failure = failureData.start(PLAYER, "cycle-story", "cycle-placement", cycle);
        failureData.setDirty(false);
        reject(new Runnable() {

            @Override
            public void run() {
                failureData.continueLine(PLAYER, "cycle-story", failure.getTransportId(), "line");
            }
        }, "automatic cycle failure");
        require(failureData.isDirty(), "failed mutation dirties");
        require(
            failureData.getSnapshot(PLAYER, "cycle-story")
                .getRuntimeSnapshot()
                .getStatus()
                .name()
                .equals("FAILED"),
            "failed status retained");
        NBTTagCompound failedPayload = new NBTTagCompound();
        failureData.writeToNBT(failedPayload);
        CanonicalSessionSavedData failedRestart = new CanonicalSessionSavedData();
        failedRestart.readFromNBT(failedPayload);
        failedRestart.bind(resolver(cycle));
        require(
            failedRestart.getSnapshot(PLAYER, "cycle-story")
                .getRuntimeSnapshot()
                .getStatus()
                .name()
                .equals("FAILED"),
            "failed status restart");

        CanonicalSessionSavedData failedStart = new CanonicalSessionSavedData();
        failedStart.setDirty(false);
        NBTTagCompound failedStartBefore = new NBTTagCompound();
        failedStart.writeToNBT(failedStartBefore);
        reject(new Runnable() {

            @Override
            public void run() {
                failedStart.start(PLAYER, "invalid", "invalid-placement", null);
            }
        }, "failed start");
        NBTTagCompound failedStartAfter = new NBTTagCompound();
        failedStart.writeToNBT(failedStartAfter);
        require(!failedStart.isDirty() && failedStartBefore.equals(failedStartAfter), "failed start is clean");

        CanonicalSessionSavedData transferData = new CanonicalSessionSavedData();
        CanonicalSessionServerService transferService = new CanonicalSessionServerService(
            CanonicalSessionForgeProbeProject.create(),
            transferData);
        CanonicalSessionDispatch transferStart = transferService.start(PLAYER, "story_a", "place_a");
        CanonicalSessionDispatch transferCompletion = transferService.continueLine(
            PLAYER,
            "story_a",
            transferStart.getFrame()
                .getTransportId(),
            transferStart.getFrame()
                .getCurrentNodeId());
        CanonicalSessionCompletionResult completion = transferCompletion.getCompletionResult();
        CanonicalStorySessionCompletionRoute route = new CanonicalStorySessionCompletionRouter(
            CanonicalSessionForgeProbeProject.create(),
            "story_a").route(completion);
        require(transferData.acceptAndConsume(completion, route), "atomic completion transfer");
        require(transferData.getSnapshot(PLAYER, "story_a") == null, "completion consumed");
        require(transferData.getPendingContinuation(PLAYER, "story_a") != null, "continuation persisted");
        transferData.setDirty(false);
        reject(new Runnable() {

            @Override
            public void run() {
                transferData.start(
                    PLAYER,
                    "story_a",
                    "place_a",
                    CanonicalSessionForgeProbeProject.create()
                        .getCanonicalSession("session_a"));
            }
        }, "start blocked by pending continuation");
        require(!transferData.isDirty(), "blocked start is clean");
        NBTTagCompound world = new NBTTagCompound();
        transferData.writeToNBT(world);
        CanonicalSessionSavedData worldRestart = new CanonicalSessionSavedData();
        worldRestart.readFromNBT(world);
        worldRestart.bind(
            resolver(
                CanonicalSessionForgeProbeProject.create()
                    .getCanonicalSession("session_a")));
        NBTTagCompound worldRoundTrip = new NBTTagCompound();
        worldRestart.writeToNBT(worldRoundTrip);
        require(world.equals(worldRoundTrip), "continuation wrapper round trip");
        NBTTagCompound unknown = copyTag(world);
        unknown.setString("unknown", "x");
        rejectRead(unknown, "wrapper unknown key");
        NBTTagCompound missing = copyTag(world);
        missing.removeTag("continuations");
        rejectRead(missing, "wrapper missing key");
        NBTTagCompound wrongType = copyTag(world);
        wrongType.setString("continuations", "wrong");
        rejectRead(wrongType, "wrapper wrong continuation type");
        NBTTagCompound wrongElementType = copyTag(world);
        NBTTagList wrongContinuations = new NBTTagList();
        wrongContinuations.appendTag(new net.minecraft.nbt.NBTTagString("not-a-compound"));
        wrongElementType.setTag("continuations", wrongContinuations);
        rejectRead(wrongElementType, "wrapper wrong continuation element type");
        NBTTagCompound wrongLogicElementType = copyTag(world);
        NBTTagCompound continuation = wrongLogicElementType.getTagList("continuations", 10)
            .getCompoundTagAt(0);
        NBTTagList wrongLogic = new NBTTagList();
        wrongLogic.appendTag(new net.minecraft.nbt.NBTTagString("not-a-compound"));
        continuation.setTag("public_logic", wrongLogic);
        rejectRead(wrongLogicElementType, "wrapper wrong Logic element type");
        NBTTagCompound duplicate = copyTag(world);
        NBTTagList duplicateList = duplicate.getTagList("continuations", 10);
        duplicateList.appendTag(
            duplicateList.getCompoundTagAt(0)
                .copy());
        rejectRead(duplicate, "wrapper duplicate continuation");
        CanonicalSessionSavedData overlapActive = new CanonicalSessionSavedData();
        overlapActive.start(
            PLAYER,
            "story_a",
            "place_a",
            CanonicalSessionForgeProbeProject.create()
                .getCanonicalSession("session_a"));
        NBTTagCompound overlap = new NBTTagCompound();
        overlapActive.writeToNBT(overlap);
        overlap.setTag(
            "continuations",
            world.getTag("continuations")
                .copy());
        rejectRead(overlap, "Session/continuation overlap");
        transferData.setDirty(false);
        require(transferData.acceptAndConsume(completion, route), "exact completion replay idempotent");
        require(!transferData.isDirty(), "exact replay is clean");
        assertCompletionRejections(completion, route);
        System.out.println("CANONICAL_SESSION_SAVED_DATA_PROBE=PASS");
    }

    private static CanonicalSessionResourceResolver resolver(final CanonicalGraphResource... resources) {
        return new CanonicalSessionResourceResolver() {

            @Override
            public CanonicalGraphResource resolve(String id) {
                for (CanonicalGraphResource resource : resources) if (resource.getId()
                    .equals(id)) return resource;
                return null;
            }
        };
    }

    private static CanonicalGraphResource linearResource(String id) {
        CanonicalGraphNode start = node(
            "start",
            "start",
            ports(port("flow_out", false, false), port("logic_out", false, true)),
            new HashMap<String, JsonElement>());
        HashMap<String, JsonElement> lineProperties = new HashMap<String, JsonElement>();
        lineProperties.put("speaker_actor_id", json("actor"));
        lineProperties.put("text", json("Hello"));
        CanonicalGraphNode line = node(
            "line",
            "line",
            ports(port("flow_in", true, false), port("flow_out", false, false)),
            lineProperties);
        HashMap<String, JsonElement> endProperties = new HashMap<String, JsonElement>();
        endProperties.put("port_id", json("success"));
        endProperties.put("display_name", json("Success"));
        CanonicalGraphNode end = node("end", "end", ports(port("flow_in", true, false)), endProperties);
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.SESSION,
            id,
            id,
            new CanonicalGraph(
                Arrays.asList(start, line, end),
                Arrays.asList(
                    new CanonicalGraphConnection(
                        "start",
                        "flow_out",
                        "line",
                        "flow_in",
                        CanonicalGraphInterfaceKind.FLOW),
                    new CanonicalGraphConnection(
                        "line",
                        "flow_out",
                        "end",
                        "flow_in",
                        CanonicalGraphInterfaceKind.FLOW))));
    }

    private static CanonicalGraphResource cycleResource(String id) {
        CanonicalGraphNode start = node(
            "start",
            "start",
            ports(port("flow_out", false, false), port("logic_out", false, true)),
            new HashMap<String, JsonElement>());
        HashMap<String, JsonElement> lineProperties = new HashMap<String, JsonElement>();
        lineProperties.put("speaker_actor_id", json("actor"));
        lineProperties.put("text", json("Cycle"));
        CanonicalGraphNode line = node(
            "line",
            "line",
            ports(port("flow_in", true, false), port("flow_out", false, false)),
            lineProperties);
        CanonicalGraphNode jump = node(
            "jump",
            "legacy_jump",
            ports(port("flow_in", true, false), port("flow_out", false, false)),
            new HashMap<String, JsonElement>());
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.SESSION,
            id,
            id,
            new CanonicalGraph(
                Arrays.asList(start, line, jump),
                Arrays.asList(
                    new CanonicalGraphConnection(
                        "start",
                        "flow_out",
                        "line",
                        "flow_in",
                        CanonicalGraphInterfaceKind.FLOW),
                    new CanonicalGraphConnection(
                        "line",
                        "flow_out",
                        "jump",
                        "flow_in",
                        CanonicalGraphInterfaceKind.FLOW),
                    new CanonicalGraphConnection(
                        "jump",
                        "flow_out",
                        "jump",
                        "flow_in",
                        CanonicalGraphInterfaceKind.FLOW))));
    }

    private static CanonicalGraphNode node(String id, String type, java.util.List<CanonicalGraphPort> ports,
        java.util.Map<String, JsonElement> properties) {
        return new CanonicalGraphNode(id, type, id, ports, properties);
    }

    private static java.util.List<CanonicalGraphPort> ports(CanonicalGraphPort... ports) {
        return Arrays.asList(ports);
    }

    private static CanonicalGraphPort port(String id, boolean input, boolean logic) {
        return new CanonicalGraphPort(
            id,
            id,
            input ? CanonicalGraphPortDirection.INPUT : CanonicalGraphPortDirection.OUTPUT,
            logic ? CanonicalGraphInterfaceKind.LOGIC : CanonicalGraphInterfaceKind.FLOW,
            0);
    }

    private static JsonElement json(String text) {
        return new JsonParser().parse(text.startsWith("\"") ? text : "\"" + text + "\"");
    }

    private static void reject(Runnable action, String label) {
        try {
            action.run();
        } catch (RuntimeException expected) {
            return;
        }
        throw new IllegalStateException("Expected rejection: " + label);
    }

    private static void rejectRead(NBTTagCompound root, String label) {
        try {
            CanonicalSessionSavedData data = new CanonicalSessionSavedData();
            data.readFromNBT(root);
        } catch (RuntimeException expected) {
            return;
        }
        throw new IllegalStateException("Expected rejection: " + label);
    }

    private static void assertCompletionRejections(CanonicalSessionCompletionResult completion,
        CanonicalStorySessionCompletionRoute route) {
        CanonicalSessionSavedData missing = new CanonicalSessionSavedData();
        assertRejectPreserves(missing, completion, route, "missing Session");

        CanonicalSessionSavedData active = new CanonicalSessionSavedData();
        CanonicalSessionServerService service = new CanonicalSessionServerService(
            CanonicalSessionForgeProbeProject.create(),
            active);
        service.start(PLAYER, "story_a", "place_a");
        assertRejectPreserves(active, completion, route, "active Session");

        CanonicalSessionCompletionResult transportMismatch = new CanonicalSessionCompletionResult(
            completion.getPlayerUuid(),
            completion.getStoryId(),
            completion.getAggregatePlacementId(),
            completion.getSessionResourceId(),
            completion.getTransportId() + 1L,
            completion.getEndPortId(),
            completion.getPublicLogicOutputs());
        assertRejectPreserves(
            completionData(
                completion,
                completion.getAggregatePlacementId(),
                completion.getSessionResourceId(),
                completion.getTransportId(),
                completion.getEndPortId(),
                completion.getPublicLogicOutputs()),
            transportMismatch,
            route,
            "conflicting transport");
        CanonicalSessionCompletionResult aggregateMismatch = new CanonicalSessionCompletionResult(
            completion.getPlayerUuid(),
            completion.getStoryId(),
            "other_placement",
            completion.getSessionResourceId(),
            completion.getTransportId(),
            completion.getEndPortId(),
            completion.getPublicLogicOutputs());
        assertRejectPreserves(
            completionData(
                completion,
                completion.getAggregatePlacementId(),
                completion.getSessionResourceId(),
                completion.getTransportId(),
                completion.getEndPortId(),
                completion.getPublicLogicOutputs()),
            aggregateMismatch,
            route,
            "aggregate identity mismatch");
        CanonicalSessionCompletionResult resourceMismatch = new CanonicalSessionCompletionResult(
            completion.getPlayerUuid(),
            completion.getStoryId(),
            completion.getAggregatePlacementId(),
            "other_resource",
            completion.getTransportId(),
            completion.getEndPortId(),
            completion.getPublicLogicOutputs());
        assertRejectPreserves(
            completionData(
                completion,
                completion.getAggregatePlacementId(),
                completion.getSessionResourceId(),
                completion.getTransportId(),
                completion.getEndPortId(),
                completion.getPublicLogicOutputs()),
            resourceMismatch,
            route,
            "resource identity mismatch");
        CanonicalSessionCompletionResult endMismatch = new CanonicalSessionCompletionResult(
            completion.getPlayerUuid(),
            completion.getStoryId(),
            completion.getAggregatePlacementId(),
            completion.getSessionResourceId(),
            completion.getTransportId(),
            "other_end",
            completion.getPublicLogicOutputs());
        assertRejectPreserves(
            completionData(
                completion,
                completion.getAggregatePlacementId(),
                completion.getSessionResourceId(),
                completion.getTransportId(),
                completion.getEndPortId(),
                completion.getPublicLogicOutputs()),
            endMismatch,
            route,
            "End mismatch");
        java.util.LinkedHashMap<String, Boolean> logic = new java.util.LinkedHashMap<String, Boolean>(
            completion.getPublicLogicOutputs());
        logic.put("extra", Boolean.TRUE);
        CanonicalSessionCompletionResult logicMismatch = new CanonicalSessionCompletionResult(
            completion.getPlayerUuid(),
            completion.getStoryId(),
            completion.getAggregatePlacementId(),
            completion.getSessionResourceId(),
            completion.getTransportId(),
            completion.getEndPortId(),
            logic);
        assertRejectPreserves(
            completionData(
                completion,
                completion.getAggregatePlacementId(),
                completion.getSessionResourceId(),
                completion.getTransportId(),
                completion.getEndPortId(),
                completion.getPublicLogicOutputs()),
            logicMismatch,
            route,
            "Logic mismatch");
        CanonicalStorySessionCompletionRoute conflictingRoute = new CanonicalStorySessionCompletionRoute(
            new CanonicalStoryFlowTransition(
                completion.getStoryId(),
                completion.getAggregatePlacementId(),
                completion.getEndPortId(),
                "other_target",
                "flow_in"),
            new CanonicalStoryPublicLogicSnapshot(completion.getPublicLogicOutputs()));
        assertRejectPreserves(
            completionData(
                completion,
                completion.getAggregatePlacementId(),
                completion.getSessionResourceId(),
                completion.getTransportId(),
                completion.getEndPortId(),
                completion.getPublicLogicOutputs()),
            completion,
            conflictingRoute,
            "conflicting route");
    }

    private static void assertRejectPreserves(CanonicalSessionSavedData data,
        CanonicalSessionCompletionResult completion, CanonicalStorySessionCompletionRoute route, String label) {
        NBTTagCompound before = new NBTTagCompound();
        data.writeToNBT(before);
        data.setDirty(false);
        reject(new Runnable() {

            @Override
            public void run() {
                data.acceptAndConsume(completion, route);
            }
        }, label);
        NBTTagCompound after = new NBTTagCompound();
        data.writeToNBT(after);
        require(before.equals(after) && !data.isDirty(), label + " preserves world state");
    }

    private static CanonicalSessionSavedData completionData(CanonicalSessionCompletionResult completion,
        String placement, String resource, long transport, String end, java.util.Map<String, Boolean> logic) {
        CanonicalSessionSavedData data = new CanonicalSessionSavedData();
        CanonicalSessionServerService service = new CanonicalSessionServerService(
            CanonicalSessionForgeProbeProject.create(),
            data);
        CanonicalSessionDispatch start = service.start(PLAYER, "story_a", "place_a");
        service.continueLine(
            PLAYER,
            "story_a",
            start.getFrame()
                .getTransportId(),
            start.getFrame()
                .getCurrentNodeId());
        CanonicalSessionCompletionResult candidate = new CanonicalSessionCompletionResult(
            completion.getPlayerUuid(),
            completion.getStoryId(),
            placement,
            resource,
            transport,
            end,
            logic);
        CanonicalStorySessionCompletionRoute candidateRoute = new CanonicalStorySessionCompletionRouter(
            CanonicalSessionForgeProbeProject.create(),
            "story_a").route(completion);
        try {
            data.acceptAndConsume(candidate, candidateRoute);
        } catch (RuntimeException ignored) {
            // The caller only needs a completed-state fixture; leave it unconsumed for its rejection call.
        }
        return data;
    }

    private static NBTTagCompound copyTag(NBTTagCompound source) {
        return (NBTTagCompound) source.copy();
    }

    private static void require(boolean condition, String label) {
        if (!condition) throw new IllegalStateException("Probe failure: " + label);
    }
}
