package darkgrey.rpg.story.canonical.server;

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
import darkgrey.rpg.graph.canonical.CanonicalProjectContent;
import darkgrey.rpg.graph.canonical.CanonicalStoryMembership;
import darkgrey.rpg.graph.canonical.CanonicalStoryMembershipSet;
import darkgrey.rpg.project.ActorDefinition;
import darkgrey.rpg.project.ProjectDefinition;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.session.persistence.CanonicalSessionSavedData;
import darkgrey.rpg.session.server.CanonicalSessionDispatch;
import darkgrey.rpg.session.server.CanonicalSessionServerService;
import darkgrey.rpg.story.canonical.CanonicalStorySessionCompletionRoute;
import darkgrey.rpg.story.canonical.CanonicalStorySessionCompletionRouter;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryRegionEntryTracker;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryStartConfiguration;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryTriggerIndex;

/** Pure Stage 5 vertical probe from Start metadata through Session completion to Story termination. */
public final class CanonicalStoryServerServiceProbe {

    private static final UUID PLAYER = UUID.fromString("12345678-1234-5678-9abc-def012345678");

    private CanonicalStoryServerServiceProbe() {}

    public static void main(String[] args) {
        entrySessionTerminationAndRestart();
        System.out.println("BEHAVIOR entrySessionTerminationAndRestart=PASS");
        typedTriggerSelection();
        System.out.println("BEHAVIOR typedTriggerSelection=PASS");
        durableConditionResume();
        System.out.println("BEHAVIOR durableConditionResume=PASS");
        errorCancelsSessionChildren();
        System.out.println("BEHAVIOR errorCancelsSessionChildren=PASS");
        packageUninstallClearsRuntimeInstances();
        System.out.println("BEHAVIOR packageUninstallClearsRuntimeInstances=PASS");
        repeatEntryGates();
        System.out.println("BEHAVIOR repeatEntryGates=PASS");
        System.out.println("CANONICAL_STORY_START_TRIGGER_SCHEMA=PASS");
        System.out.println("CANONICAL_STORY_SESSION_SERVICE=PASS");
        System.out.println("CANONICAL_STORY_ATOMIC_RESTART=PASS");
        System.out.println("CANONICAL_STORY_ERROR_CHILD_CLEANUP=PASS");
        System.out.println("CANONICAL_STORY_DYNAMIC_LOGIC_RESUME=PASS");
        System.out.println("CANONICAL_STORY_PACKAGE_UNINSTALL_RUNTIME_CLEAR=PASS");
        System.out.println("REPEAT_0334_ALL_ENTRY_GATES=PASS");
    }

    private static void repeatEntryGates() {
        String[] rules = { "{\"type\":\"cooldown\",\"value\":1,\"unit\":\"hours\"}",
            "{\"type\":\"scheduled\",\"period\":\"daily\",\"time\":\"00:00\"}" };
        for (String rule : rules) for (int entry = 0; entry < 8; entry++) {
            ProjectSnapshot project = repeatEntryProject(rule);
            CanonicalSessionSavedData data = new CanonicalSessionSavedData();
            CanonicalStoryServerService service = new CanonicalStoryServerService(project, data);
            CanonicalActorCandidate candidate = service
                .actorCandidates(PLAYER, Collections.singleton("ST-AAAA-BBBB-CCCC-DDDD~actor~bartender"))
                .get(0);
            long now = System.currentTimeMillis();
            CanonicalStoryDispatch first = repeatEntry(service, entry, candidate, now - 200000000L);
            check(
                first != null && data.getStorySnapshot(PLAYER, "ST-AAAA-BBBB-CCCC-DDDD") != null,
                "Entry did not start: " + entry);
            assertRepeatRequestsPreserve(service, data, candidate, now);
            service.setLogicInput(PLAYER, "ST-AAAA-BBBB-CCCC-DDDD", "finish", true, now);
            check(!service.isStartEligible(PLAYER, "ST-AAAA-BBBB-CCCC-DDDD"), "Future repeat was eligible: " + entry);
            assertRepeatRequestsPreserve(service, data, candidate, now + 1);

            // Separate expired instance: no sleep or machine-clock changes are needed.
            CanonicalSessionSavedData expired = new CanonicalSessionSavedData();
            CanonicalStoryServerService ready = new CanonicalStoryServerService(project, expired);
            repeatEntry(
                ready,
                entry,
                ready.actorCandidates(PLAYER, Collections.singleton("ST-AAAA-BBBB-CCCC-DDDD~actor~bartender"))
                    .get(0),
                now - 300000000L);
            ready.setLogicInput(PLAYER, "ST-AAAA-BBBB-CCCC-DDDD", "finish", true, now - 200000000L);
            net.minecraft.nbt.NBTTagCompound persisted = new net.minecraft.nbt.NBTTagCompound();
            expired.writeToNBT(persisted);
            CanonicalSessionSavedData restored = new CanonicalSessionSavedData();
            restored.readFromNBT(persisted);
            ready = new CanonicalStoryServerService(project, restored);
            check(ready.isStartEligible(PLAYER, "ST-AAAA-BBBB-CCCC-DDDD"), "Expired persisted repeat remained blocked");
            CanonicalActorCandidate restart = ready
                .actorCandidates(PLAYER, Collections.singleton("ST-AAAA-BBBB-CCCC-DDDD~actor~bartender"))
                .get(0);
            check(repeatEntry(ready, entry, restart, now) != null, "Eligible restart rejected: " + entry);
            check(
                restored.getStorySnapshot(PLAYER, "ST-AAAA-BBBB-CCCC-DDDD")
                    .getActivationTime() == now,
                "Restart retained old activation");
            assertRepeatRequestsPreserve(ready, restored, restart, now + 1);
            restored.markStoryError(PLAYER, "ST-AAAA-BBBB-CCCC-DDDD", now + 2);
            assertRepeatRequestsPreserve(ready, restored, restart, now + 3);
        }
    }

    private static void assertRepeatRequestsPreserve(CanonicalStoryServerService service,
        CanonicalSessionSavedData data, CanonicalActorCandidate stale, long now) {
        net.minecraft.nbt.NBTTagCompound before = new net.minecraft.nbt.NBTTagCompound();
        data.writeToNBT(before);
        for (int index = 0; index < 8; index++) repeatEntry(service, index, stale, now + index);
        for (CanonicalActorCandidate current : service
            .actorCandidates(PLAYER, Collections.singleton("ST-AAAA-BBBB-CCCC-DDDD~actor~bartender")))
            check(!"ST-AAAA-BBBB-CCCC-DDDD".equals(current.getStoryId()), "Blocked actor candidate remained visible");
        net.minecraft.nbt.NBTTagCompound after = new net.minecraft.nbt.NBTTagCompound();
        data.writeToNBT(after);
        check(before.equals(after), "Rejected/repeated entry changed persisted state");
    }

    private static CanonicalStoryDispatch repeatEntry(CanonicalStoryServerService service, int entry,
        CanonicalActorCandidate candidate, long now) {
        switch (entry) {
            case 0:
                return service.startByEntry(PLAYER, "ST-AAAA-BBBB-CCCC-DDDD", now);
            case 1:
                return service
                    .startByActor(PLAYER, "ST-AAAA-BBBB-CCCC-DDDD", "ST-AAAA-BBBB-CCCC-DDDD~actor~bartender", now);
            case 2:
                return service.startByRegion(PLAYER, "ST-AAAA-BBBB-CCCC-DDDD", 0, 10D, 64D, 10D, now);
            case 3:
                return service.startByLogic(
                    PLAYER,
                    "ST-AAAA-BBBB-CCCC-DDDD",
                    Collections.singletonMap("launch", Boolean.TRUE),
                    now);
            case 4:
                return service.startByLogicTrigger(
                    PLAYER,
                    "ST-AAAA-BBBB-CCCC-DDDD",
                    "logic",
                    Collections.singletonMap("launch", Boolean.TRUE),
                    now);
            case 5:
                return service
                    .startByFlow(PLAYER, "ST-JKLM-NPQR-STUV-WXYZ", "end", "ST-AAAA-BBBB-CCCC-DDDD", "boundary", now);
            case 6:
                return service.startFlowFromTerminal(PLAYER, "ST-JKLM-NPQR-STUV-WXYZ", "end", now);
            case 7:
                return service.executeActorCandidate(
                    PLAYER,
                    Collections.singleton("ST-AAAA-BBBB-CCCC-DDDD~actor~bartender"),
                    candidate,
                    now);
            default:
                throw new AssertionError(entry);
        }
    }

    private static ProjectSnapshot repeatEntryProject(String rule) {
        Map<String, JsonElement> properties = new LinkedHashMap<String, JsonElement>();
        properties.put("repeat_policy", json("\"repeatable\""));
        properties.put("repeat_condition", json(rule));
        properties.put(
            "triggers",
            json(
                "[" + "{\"port_id\":\"entry\",\"display_name\":\"Entry\",\"trigger_type\":\"enter_story\",\"trigger_properties\":{},\"order\":0},"
                    + "{\"port_id\":\"actor\",\"display_name\":\"Actor\",\"trigger_type\":\"interact_actor\",\"trigger_properties\":{\"actor_id\":\"ST-AAAA-BBBB-CCCC-DDDD~actor~bartender\"},\"order\":1},"
                    + "{\"port_id\":\"region\",\"display_name\":\"Region\",\"trigger_type\":\"enter_region\",\"trigger_properties\":{\"dimension\":0,\"x\":10,\"y\":64,\"z\":10,\"radius\":2},\"order\":2},"
                    + "{\"port_id\":\"logic\",\"display_name\":\"Logic\",\"trigger_type\":\"logic\",\"trigger_properties\":{},\"logic_port_id\":\"launch_in\",\"order\":3},"
                    + "{\"port_id\":\"boundary\",\"display_name\":\"Boundary\",\"trigger_type\":\"flow_driven\",\"trigger_properties\":{},\"order\":4}]"));
        CanonicalGraphNode start = node(
            "start",
            "start",
            ports(
                flowOut("entry", "Entry", 0),
                flowOut("actor", "Actor", 1),
                flowOut("region", "Region", 2),
                flowOut("logic", "Logic", 3),
                flowOut("boundary", "Boundary", 4),
                logicIn("launch_in", 5)),
            properties);
        CanonicalGraphNode launch = node(
            "launch",
            "logic_input",
            ports(logicOut("logic_out", 0)),
            props("port_id", "launch", "display_name", "Launch"));
        CanonicalGraphNode finish = node(
            "finish",
            "logic_input",
            ports(logicOut("logic_out", 0)),
            props("port_id", "finish", "display_name", "Finish"));
        CanonicalGraphNode wait = node(
            "wait",
            "condition",
            ports(
                flowIn("flow_in", 0),
                logicIn("logic_in", 1),
                flowOut("flow_true", "True", 2),
                flowOut("flow_false", "False", 3)),
            empty());
        CanonicalGraphNode end = node("end", "terminate", ports(flowIn("flow_in", 0)), empty());
        List<CanonicalGraphConnection> edges = new java.util.ArrayList<CanonicalGraphConnection>();
        for (String port : Arrays.asList("entry", "actor", "region", "logic", "boundary"))
            edges.add(flow("start", port, "wait", "flow_in"));
        edges.add(logic("launch", "logic_out", "start", "launch_in"));
        edges.add(logic("finish", "logic_out", "wait", "logic_in"));
        edges.add(flow("wait", "flow_true", "end", "flow_in"));
        Map<String, CanonicalGraphResource> stories = new LinkedHashMap<String, CanonicalGraphResource>();
        stories.put(
            "ST-AAAA-BBBB-CCCC-DDDD",
            resource(
                "ST-AAAA-BBBB-CCCC-DDDD",
                CanonicalGraphResourceKind.STORY,
                Arrays.asList(start, launch, finish, wait, end),
                edges));
        stories.put(
            "ST-JKLM-NPQR-STUV-WXYZ",
            resource(
                "ST-JKLM-NPQR-STUV-WXYZ",
                CanonicalGraphResourceKind.STORY,
                Arrays.asList(
                    startNode(
                        "once",
                        "[{\"port_id\":\"entry\",\"display_name\":\"Entry\",\"trigger_type\":\"enter_story\",\"trigger_properties\":{},\"order\":0}]",
                        ports(flowOut("entry", "Entry", 0))),
                    end),
                Collections.singletonList(flow("start", "entry", "end", "flow_in"))));
        return new ProjectSnapshot(
            new ProjectDefinition(1, "repeat-entry", "Repeat Entry"),
            Collections.<String, ActorDefinition>emptyMap(),
            Collections.emptyMap(),
            Collections.emptyMap(),
            new CanonicalProjectContent(
                stories,
                Collections.<String, CanonicalGraphResource>emptyMap(),
                Collections.<String, CanonicalGraphResource>emptyMap(),
                Collections.<String, CanonicalStoryMembership>emptyMap(),
                new darkgrey.rpg.graph.canonical.CanonicalStoryLogicGraph(
                    Collections.singletonList(
                        new darkgrey.rpg.graph.canonical.CanonicalStoryLogicConnection(
                            "ST-JKLM-NPQR-STUV-WXYZ",
                            "end",
                            "ST-AAAA-BBBB-CCCC-DDDD",
                            "boundary",
                            CanonicalGraphInterfaceKind.FLOW)))));
    }

    private static void packageUninstallClearsRuntimeInstances() {
        ProjectSnapshot installed = project();
        CanonicalSessionSavedData data = new CanonicalSessionSavedData();
        new CanonicalStoryServerService(installed, data).startByEntry(PLAYER, "ST-2345-6789-ABCD-EFGH", 1000L);
        new CanonicalSessionServerService(installed, data).start(PLAYER, "ST-2345-6789-ABCD-EFGH", "session_place");
        check(
            data.getStorySnapshot(PLAYER, "ST-2345-6789-ABCD-EFGH") != null,
            "Installed Story cursor was not created");
        check(data.getSnapshot(PLAYER, "ST-2345-6789-ABCD-EFGH") != null, "Installed Session child was not created");

        new CanonicalStoryServerService(emptyProject(), data);
        check(
            data.getStorySnapshot(PLAYER, "ST-2345-6789-ABCD-EFGH") == null,
            "Uninstalled Story cursor remained active");
        check(data.getSnapshot(PLAYER, "ST-2345-6789-ABCD-EFGH") == null, "Uninstalled Session child remained active");
        check(
            data.getPendingContinuations()
                .isEmpty(),
            "Uninstalled Story handoff remained active");

        CanonicalStoryDispatch restarted = new CanonicalStoryServerService(installed, data)
            .startByEntry(PLAYER, "ST-2345-6789-ABCD-EFGH", 1100L);
        check(restarted == null, "Reinstall must preserve the retired once-run history");
        check(
            !new CanonicalStoryServerService(installed, data).isStartEligible(PLAYER, "ST-2345-6789-ABCD-EFGH"),
            "Reinstalled once Story became eligible for the same player");
        CanonicalStoryDispatch otherPlayer = new CanonicalStoryServerService(installed, data)
            .startByEntry(new UUID(0, 401), "ST-2345-6789-ABCD-EFGH", 1100L);
        check(
            otherPlayer.getKind() == CanonicalStoryDispatchKind.SESSION,
            "Reinstalled resource must resolve for a new player");
        check(
            otherPlayer.getSnapshot()
                .getActivationTime() == 1100L,
            "Reinstalled Story reused the uninstalled runtime cursor");
    }

    private static void durableConditionResume() {
        Map<String, CanonicalGraphResource> stories = new LinkedHashMap<String, CanonicalGraphResource>();
        stories.put("ST-2222-3333-4444-5555", dynamicConditionStory());
        stories.put("ST-6666-7777-8888-9999", dynamicFalseConditionStory());
        ProjectSnapshot project = new ProjectSnapshot(
            new ProjectDefinition(1, "dynamic-logic", "Dynamic Logic"),
            Collections.<String, ActorDefinition>emptyMap(),
            Collections.emptyMap(),
            Collections.emptyMap(),
            new CanonicalProjectContent(
                stories,
                Collections.<String, CanonicalGraphResource>emptyMap(),
                Collections.<String, CanonicalGraphResource>emptyMap(),
                Collections.<String, CanonicalStoryMembership>emptyMap()));
        CanonicalSessionSavedData data = new CanonicalSessionSavedData();
        CanonicalStoryServerService service = new CanonicalStoryServerService(project, data);
        CanonicalStoryDispatch waiting = service.startByEntry(PLAYER, "ST-2222-3333-4444-5555", 600L);
        check(
            waiting.getSnapshot()
                .getRuntimeSnapshot()
                .getWaitKind() == darkgrey.rpg.story.canonical.runtime.CanonicalStoryWaitKind.CONDITION,
            "False Condition did not enter a durable wait");

        net.minecraft.nbt.NBTTagCompound persisted = new net.minecraft.nbt.NBTTagCompound();
        data.writeToNBT(persisted);
        CanonicalSessionSavedData restored = new CanonicalSessionSavedData();
        restored.readFromNBT(persisted);
        CanonicalStoryServerService restoredService = new CanonicalStoryServerService(project, restored);
        CanonicalStoryDispatch terminated = restoredService
            .setLogicInput(PLAYER, "ST-2222-3333-4444-5555", "gate", true, 700L);
        check(
            terminated.getKind() == CanonicalStoryDispatchKind.TERMINATED,
            "Dynamic Logic did not resume the waiting Condition through its connected outlet");
        CanonicalStoryDispatch stable = restoredService
            .setLogicInput(PLAYER, "ST-2222-3333-4444-5555", "gate", true, 701L);
        check(
            stable.getKind() == CanonicalStoryDispatchKind.TERMINATED,
            "Repeated Logic value changed a terminal Story");

        CanonicalStoryDispatch inverseWaiting = restoredService
            .startByLogic(PLAYER, "ST-6666-7777-8888-9999", Collections.singletonMap("gate", Boolean.TRUE), 800L);
        check(
            inverseWaiting != null && inverseWaiting.getSnapshot()
                .getRuntimeSnapshot()
                .getWaitKind() == darkgrey.rpg.story.canonical.runtime.CanonicalStoryWaitKind.CONDITION,
            "True Condition with only a false outlet did not enter a durable wait");
        net.minecraft.nbt.NBTTagCompound inversePersisted = new net.minecraft.nbt.NBTTagCompound();
        restored.writeToNBT(inversePersisted);
        CanonicalSessionSavedData inverseRestored = new CanonicalSessionSavedData();
        inverseRestored.readFromNBT(inversePersisted);
        CanonicalStoryDispatch inverseTerminated = new CanonicalStoryServerService(project, inverseRestored)
            .setLogicInput(PLAYER, "ST-6666-7777-8888-9999", "gate", false, 900L);
        check(
            inverseTerminated.getKind() == CanonicalStoryDispatchKind.TERMINATED,
            "Dynamic Logic did not resume the waiting Condition through its connected false outlet");
    }

    private static void errorCancelsSessionChildren() {
        ProjectSnapshot project = project();
        CanonicalSessionSavedData activeData = new CanonicalSessionSavedData();
        CanonicalStoryServerService activeStories = new CanonicalStoryServerService(project, activeData);
        activeStories.startByEntry(PLAYER, "ST-2345-6789-ABCD-EFGH", 400L);
        new CanonicalSessionServerService(project, activeData).start(PLAYER, "ST-2345-6789-ABCD-EFGH", "session_place");
        check(activeData.getSnapshot(PLAYER, "ST-2345-6789-ABCD-EFGH") != null, "Session child was not created");
        check(activeData.markStoryError(PLAYER, "ST-2345-6789-ABCD-EFGH", 401L), "Active Story was not marked ERROR");
        check(activeData.getSnapshot(PLAYER, "ST-2345-6789-ABCD-EFGH") == null, "ERROR retained active Session child");

        CanonicalSessionSavedData completedData = new CanonicalSessionSavedData();
        CanonicalStoryServerService completedStories = new CanonicalStoryServerService(project, completedData);
        completedStories.startByEntry(PLAYER, "ST-2345-6789-ABCD-EFGH", 500L);
        CanonicalSessionServerService sessions = new CanonicalSessionServerService(project, completedData);
        CanonicalSessionDispatch frame = sessions.start(PLAYER, "ST-2345-6789-ABCD-EFGH", "session_place");
        CanonicalSessionDispatch completion = sessions.continueLine(
            PLAYER,
            "ST-2345-6789-ABCD-EFGH",
            frame.getFrame()
                .getTransportId(),
            "line");
        CanonicalStorySessionCompletionRoute route = new CanonicalStorySessionCompletionRouter(
            project,
            "ST-2345-6789-ABCD-EFGH").route(completion.getCompletionResult());
        completedData.acceptAndConsume(completion.getCompletionResult(), route);
        check(
            completedData.getPendingContinuation(PLAYER, "ST-2345-6789-ABCD-EFGH") != null,
            "Handoff was not created");
        check(
            completedData.markStoryError(PLAYER, "ST-2345-6789-ABCD-EFGH", 501L),
            "Waiting Story was not marked ERROR");
        check(
            completedData.getPendingContinuation(PLAYER, "ST-2345-6789-ABCD-EFGH") == null,
            "ERROR retained Session handoff");
    }

    private static void entrySessionTerminationAndRestart() {
        ProjectSnapshot project = project();
        CanonicalSessionSavedData data = new CanonicalSessionSavedData();
        CanonicalStoryServerService stories = new CanonicalStoryServerService(project, data);
        CanonicalStoryDispatch started = stories.startByEntry(PLAYER, "ST-2345-6789-ABCD-EFGH", 100L);
        check(started.getKind() == CanonicalStoryDispatchKind.SESSION, "Entry trigger did not reach Session");
        check("session_place".equals(started.getPlacementId()), "Wrong Session placement");
        check("ST-2345-6789-ABCD-EFGH~session~session".equals(started.getResourceId()), "Wrong Session resource");

        CanonicalSessionServerService sessions = new CanonicalSessionServerService(project, data);
        CanonicalSessionDispatch frame = sessions.start(PLAYER, "ST-2345-6789-ABCD-EFGH", "session_place");
        CanonicalSessionDispatch completion = sessions.continueLine(
            PLAYER,
            "ST-2345-6789-ABCD-EFGH",
            frame.getFrame()
                .getTransportId(),
            "line");
        CanonicalStorySessionCompletionRoute route = new CanonicalStorySessionCompletionRouter(
            project,
            "ST-2345-6789-ABCD-EFGH").route(completion.getCompletionResult());
        check(
            data.acceptAndConsume(completion.getCompletionResult(), route),
            "Session completion was not checkpointed");

        CanonicalStoryDispatch terminated = stories.resumeSession(PLAYER, "ST-2345-6789-ABCD-EFGH", 200L);
        check(terminated.getKind() == CanonicalStoryDispatchKind.TERMINATED, "Story did not terminate after Session");
        check(data.getPendingContinuation(PLAYER, "ST-2345-6789-ABCD-EFGH") == null, "Story handoff was not consumed");

        net.minecraft.nbt.NBTTagCompound persisted = new net.minecraft.nbt.NBTTagCompound();
        data.writeToNBT(persisted);
        CanonicalSessionSavedData restart = new CanonicalSessionSavedData();
        restart.readFromNBT(persisted);
        CanonicalStoryDispatch restored = new CanonicalStoryServerService(project, restart)
            .snapshot(PLAYER, "ST-2345-6789-ABCD-EFGH");
        check(restored.getKind() == CanonicalStoryDispatchKind.TERMINATED, "Terminated Story did not restore");
        CanonicalStoryDispatch onceAgain = new CanonicalStoryServerService(project, restart)
            .startByEntry(PLAYER, "ST-2345-6789-ABCD-EFGH", 300L);
        check(onceAgain.getKind() == CanonicalStoryDispatchKind.TERMINATED, "Once Story restarted");
        check(
            onceAgain.getSnapshot()
                .getActivationTime() == 100L,
            "Once Story activation was replaced");
    }

    private static void typedTriggerSelection() {
        CanonicalGraphResource typed = typedTriggerStory();
        CanonicalStoryStartConfiguration configuration = CanonicalStoryStartConfiguration.parse(typed);
        check(
            "actor_port".equals(
                configuration.selectActor("ST-AAAA-BBBB-CCCC-DDDD~actor~bartender")
                    .getPortId()),
            "Actor trigger mismatch");
        check(
            "region_port".equals(
                configuration.selectRegion(0, 11D, 64D, 10D)
                    .getPortId()),
            "Region trigger mismatch");
        check(
            "entry_port".equals(
                configuration.selectEnterStory()
                    .getPortId()),
            "Entry trigger mismatch");
        check(configuration.findActor("missing") == null, "Missing Actor trigger did not remain unmatched");
        check(configuration.findRegion(0, 50D, 64D, 50D) == null, "Outside position matched a region");

        Map<String, CanonicalGraphResource> indexedStories = new LinkedHashMap<String, CanonicalGraphResource>();
        indexedStories.put(typed.getId(), typed);
        ProjectSnapshot indexedProject = new ProjectSnapshot(
            new ProjectDefinition(1, "trigger-index", "Trigger Index"),
            Collections.<String, ActorDefinition>emptyMap(),
            Collections.emptyMap(),
            Collections.emptyMap(),
            new CanonicalProjectContent(
                indexedStories,
                Collections.<String, CanonicalGraphResource>emptyMap(),
                Collections.<String, CanonicalGraphResource>emptyMap(),
                Collections.<String, CanonicalStoryMembership>emptyMap()));
        CanonicalStoryTriggerIndex index = CanonicalStoryTriggerIndex.build(indexedProject);
        CanonicalStoryTriggerIndex.Match actorMatch = index.matchActor("ST-AAAA-BBBB-CCCC-DDDD~actor~bartender")
            .get(0);
        check(
            "ST-JKLM-NPQR-STUV-WXYZ".equals(actorMatch.getStoryId()) && "actor_port".equals(actorMatch.getPortId()),
            "Actor trigger index identity mismatch");
        CanonicalStoryTriggerIndex.Match regionMatch = index.matchRegion(0, 11D, 64D, 10D)
            .get(0);
        check(
            "ST-JKLM-NPQR-STUV-WXYZ".equals(regionMatch.getStoryId()) && "region_port".equals(regionMatch.getPortId()),
            "Region trigger index identity mismatch");
        check(
            index.matchRegion(0, 50D, 64D, 50D)
                .isEmpty(),
            "Trigger index matched an outside position");

        CanonicalStoryRegionEntryTracker entries = new CanonicalStoryRegionEntryTracker();
        List<CanonicalStoryTriggerIndex.Match> inside = Collections.singletonList(regionMatch);
        check(
            entries.update(PLAYER, inside)
                .size() == 1,
            "Initial inside sample did not enter region");
        check(
            entries.update(PLAYER, inside)
                .isEmpty(),
            "Region fired again while player remained inside");
        check(
            entries.update(PLAYER, Collections.<CanonicalStoryTriggerIndex.Match>emptyList())
                .isEmpty(),
            "Leaving region produced a false entry");
        check(
            entries.update(PLAYER, inside)
                .size() == 1,
            "Region did not fire after leaving and re-entering");
        entries.forget(PLAYER);
        check(
            entries.update(PLAYER, inside)
                .size() == 1,
            "Logout reset did not clear region membership");
    }

    public static ProjectSnapshot project() {
        Map<String, ActorDefinition> actors = new LinkedHashMap<String, ActorDefinition>();
        actors.put(
            "ST-2345-6789-ABCD-EFGH~actor~actor",
            new ActorDefinition(
                5,
                ActorDefinition.TYPE_INDIVIDUAL,
                "ST-2345-6789-ABCD-EFGH~actor~actor",
                "Actor",
                "",
                Collections.<String>emptyList(),
                "ST-2345-6789-ABCD-EFGH"));
        Map<String, CanonicalGraphResource> stories = new LinkedHashMap<String, CanonicalGraphResource>();
        stories.put("ST-2345-6789-ABCD-EFGH", story());
        Map<String, CanonicalGraphResource> sessions = new LinkedHashMap<String, CanonicalGraphResource>();
        sessions.put("ST-2345-6789-ABCD-EFGH~session~session", session());
        Map<String, CanonicalStoryMembership> memberships = new LinkedHashMap<String, CanonicalStoryMembership>();
        memberships.put(
            "ST-2345-6789-ABCD-EFGH",
            new CanonicalStoryMembership(
                "ST-2345-6789-ABCD-EFGH",
                new CanonicalStoryMembershipSet(
                    Collections.singletonList("ST-2345-6789-ABCD-EFGH~actor~actor"),
                    Collections.singletonList("ST-2345-6789-ABCD-EFGH~session~session"),
                    Collections.<String>emptyList())));
        return new ProjectSnapshot(
            new ProjectDefinition(1, "story-probe", "Story Probe"),
            actors,
            Collections.emptyMap(),
            Collections.emptyMap(),
            new CanonicalProjectContent(
                stories,
                sessions,
                Collections.<String, CanonicalGraphResource>emptyMap(),
                memberships));
    }

    private static ProjectSnapshot emptyProject() {
        return new ProjectSnapshot(
            new ProjectDefinition(1, "empty-story-probe", "Empty Story Probe"),
            Collections.<String, ActorDefinition>emptyMap(),
            Collections.emptyMap(),
            Collections.emptyMap(),
            new CanonicalProjectContent(
                Collections.<String, CanonicalGraphResource>emptyMap(),
                Collections.<String, CanonicalGraphResource>emptyMap(),
                Collections.<String, CanonicalGraphResource>emptyMap(),
                Collections.<String, CanonicalStoryMembership>emptyMap()));
    }

    private static CanonicalGraphResource story() {
        CanonicalGraphNode start = startNode(
            "once",
            "[{\"port_id\":\"entry\",\"display_name\":\"进入故事\",\"trigger_type\":\"enter_story\",\"trigger_properties\":{},\"order\":0}]",
            ports(flowOut("entry", "进入故事", 0)));
        CanonicalGraphNode aggregate = node(
            "session_place",
            "ST-2345-6789-ABCD-EFGH~session~session",
            ports(flowIn("flow_in", 0), logicIn("logic_in", 1), flowOut("done", "done", 2)),
            props("resource_id", "ST-2345-6789-ABCD-EFGH~session~session"));
        CanonicalGraphNode end = node("end", "terminate", ports(flowIn("flow_in", 0)), empty());
        return resource(
            "ST-2345-6789-ABCD-EFGH",
            CanonicalGraphResourceKind.STORY,
            Arrays.asList(start, aggregate, end),
            Arrays.asList(
                flow("start", "entry", "session_place", "flow_in"),
                flow("session_place", "done", "end", "flow_in")));
    }

    private static CanonicalGraphResource typedTriggerStory() {
        String triggers = "["
            + "{\"port_id\":\"entry_port\",\"display_name\":\"进入故事\",\"trigger_type\":\"enter_story\",\"trigger_properties\":{},\"order\":0},"
            + "{\"port_id\":\"actor_port\",\"display_name\":\"酒馆老板\",\"trigger_type\":\"interact_actor\",\"trigger_properties\":{\"actor_id\":\"ST-AAAA-BBBB-CCCC-DDDD~actor~bartender\"},\"order\":1},"
            + "{\"port_id\":\"region_port\",\"display_name\":\"酒馆入口\",\"trigger_type\":\"enter_region\",\"trigger_properties\":{\"dimension\":0,\"x\":10,\"y\":64,\"z\":10,\"radius\":2},\"order\":2}]";
        CanonicalGraphNode start = startNode(
            "repeatable",
            triggers,
            ports(
                flowOut("entry_port", "进入故事", 0),
                flowOut("actor_port", "酒馆老板", 1),
                flowOut("region_port", "酒馆入口", 2)));
        CanonicalGraphNode end = node("end", "terminate", ports(flowIn("flow_in", 0)), empty());
        return resource(
            "ST-JKLM-NPQR-STUV-WXYZ",
            CanonicalGraphResourceKind.STORY,
            Arrays.asList(start, end),
            Arrays.asList(flow("start", "entry_port", "end", "flow_in")));
    }

    private static CanonicalGraphResource dynamicConditionStory() {
        CanonicalGraphNode start = startNode(
            "once",
            "[{\"port_id\":\"entry\",\"display_name\":\"进入故事\",\"trigger_type\":\"enter_story\",\"trigger_properties\":{},\"order\":0}]",
            ports(flowOut("entry", "进入故事", 0)));
        CanonicalGraphNode input = node(
            "gate_input",
            "logic_input",
            ports(logicOut("logic_out", 0)),
            props("port_id", "gate", "display_name", "Gate"));
        CanonicalGraphNode condition = node(
            "condition",
            "condition",
            ports(
                flowIn("flow_in", 0),
                logicIn("logic_in", 1),
                flowOut("flow_true", "True", 2),
                flowOut("flow_false", "False", 3)),
            empty());
        CanonicalGraphNode end = node("end", "terminate", ports(flowIn("flow_in", 0)), empty());
        return resource(
            "ST-2222-3333-4444-5555",
            CanonicalGraphResourceKind.STORY,
            Arrays.asList(start, input, condition, end),
            Arrays.asList(
                flow("start", "entry", "condition", "flow_in"),
                logic("gate_input", "logic_out", "condition", "logic_in"),
                flow("condition", "flow_true", "end", "flow_in")));
    }

    private static CanonicalGraphResource dynamicFalseConditionStory() {
        Map<String, JsonElement> startProperties = new LinkedHashMap<String, JsonElement>();
        startProperties.put("repeat_policy", json("\"once\""));
        startProperties.put(
            "triggers",
            json(
                "[{\"port_id\":\"logic_start\",\"display_name\":\"Logic\",\"trigger_type\":\"logic\","
                    + "\"trigger_properties\":{},\"order\":0,\"logic_port_id\":\"logic_condition\"}]"));
        CanonicalGraphNode start = node(
            "start",
            "start",
            ports(flowOut("logic_start", "Logic", 0), logicIn("logic_condition", 1)),
            startProperties);
        CanonicalGraphNode input = node(
            "gate_input",
            "logic_input",
            ports(logicOut("logic_out", 0)),
            props("port_id", "gate", "display_name", "Gate"));
        CanonicalGraphNode condition = node(
            "condition",
            "condition",
            ports(
                flowIn("flow_in", 0),
                logicIn("logic_in", 1),
                flowOut("flow_true", "True", 2),
                flowOut("flow_false", "False", 3)),
            empty());
        CanonicalGraphNode end = node("end", "terminate", ports(flowIn("flow_in", 0)), empty());
        return resource(
            "ST-6666-7777-8888-9999",
            CanonicalGraphResourceKind.STORY,
            Arrays.asList(start, input, condition, end),
            Arrays.asList(
                logic("gate_input", "logic_out", "start", "logic_condition"),
                flow("start", "logic_start", "condition", "flow_in"),
                logic("gate_input", "logic_out", "condition", "logic_in"),
                flow("condition", "flow_false", "end", "flow_in")));
    }

    private static CanonicalGraphNode startNode(String repeat, String triggers, List<CanonicalGraphPort> ports) {
        Map<String, JsonElement> properties = new LinkedHashMap<String, JsonElement>();
        properties.put("repeat_policy", json("\"" + repeat + "\""));
        properties.put("triggers", json(triggers));
        return new CanonicalGraphNode("start", "start", "Start", ports, properties);
    }

    private static CanonicalGraphResource session() {
        CanonicalGraphNode start = node(
            "start",
            "start",
            ports(flowOut("flow_out", "flow_out", 0), logicOut("logic_out", 1)),
            empty());
        CanonicalGraphNode line = node(
            "line",
            "line",
            ports(flowIn("flow_in", 0), flowOut("flow_out", "flow_out", 1)),
            props("speaker_actor_id", "ST-2345-6789-ABCD-EFGH~actor~actor", "text", "Hello"));
        CanonicalGraphNode end = node(
            "done",
            "end",
            ports(flowIn("flow_in", 0)),
            props("port_id", "done", "display_name", "Done"));
        return resource(
            "ST-2345-6789-ABCD-EFGH~session~session",
            CanonicalGraphResourceKind.SESSION,
            Arrays.asList(start, line, end),
            Arrays.asList(flow("start", "flow_out", "line", "flow_in"), flow("line", "flow_out", "done", "flow_in")));
    }

    private static CanonicalGraphResource resource(String id, CanonicalGraphResourceKind kind,
        List<CanonicalGraphNode> nodes, List<CanonicalGraphConnection> edges) {
        return new CanonicalGraphResource(3, kind, id, id, new CanonicalGraph(nodes, edges));
    }

    private static CanonicalGraphNode node(String id, String type, List<CanonicalGraphPort> ports,
        Map<String, JsonElement> properties) {
        if ("ST-2345-6789-ABCD-EFGH~session~session".equals(type)) type = "session";
        // Fixture upgrade: public termination metadata belongs in test data, not runtime fallback.
        if ("terminate".equals(type)) {
            properties = new java.util.LinkedHashMap<String, JsonElement>(properties);
            if (!properties.containsKey("port_id")) properties.put("port_id", new com.google.gson.JsonPrimitive(id));
            if (!properties.containsKey("display_name"))
                properties.put("display_name", new com.google.gson.JsonPrimitive(id));
            properties.put("display_order", new com.google.gson.JsonPrimitive(0));
        }
        return new CanonicalGraphNode(id, type, id, ports, properties);
    }

    private static List<CanonicalGraphPort> ports(CanonicalGraphPort... ports) {
        return Arrays.asList(ports);
    }

    private static CanonicalGraphPort flowIn(String id, int order) {
        return new CanonicalGraphPort(
            id,
            id,
            CanonicalGraphPortDirection.INPUT,
            CanonicalGraphInterfaceKind.FLOW,
            order);
    }

    private static CanonicalGraphPort logicIn(String id, int order) {
        return new CanonicalGraphPort(
            id,
            id,
            CanonicalGraphPortDirection.INPUT,
            CanonicalGraphInterfaceKind.LOGIC,
            order);
    }

    private static CanonicalGraphPort flowOut(String id, String displayName, int order) {
        return new CanonicalGraphPort(
            id,
            displayName,
            CanonicalGraphPortDirection.OUTPUT,
            CanonicalGraphInterfaceKind.FLOW,
            order);
    }

    private static CanonicalGraphPort logicOut(String id, int order) {
        return new CanonicalGraphPort(
            id,
            id,
            CanonicalGraphPortDirection.OUTPUT,
            CanonicalGraphInterfaceKind.LOGIC,
            order);
    }

    private static CanonicalGraphConnection flow(String fromNode, String fromPort, String toNode, String toPort) {
        return new CanonicalGraphConnection(fromNode, fromPort, toNode, toPort, CanonicalGraphInterfaceKind.FLOW);
    }

    private static CanonicalGraphConnection logic(String fromNode, String fromPort, String toNode, String toPort) {
        return new CanonicalGraphConnection(fromNode, fromPort, toNode, toPort, CanonicalGraphInterfaceKind.LOGIC);
    }

    private static Map<String, JsonElement> props(String... values) {
        Map<String, JsonElement> result = new LinkedHashMap<String, JsonElement>();
        for (int index = 0; index < values.length; index += 2)
            result.put(values[index], json("\"" + values[index + 1] + "\""));
        return result;
    }

    private static Map<String, JsonElement> empty() {
        return Collections.emptyMap();
    }

    private static JsonElement json(String source) {
        return new JsonParser().parse(source);
    }

    private static void check(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }
}
