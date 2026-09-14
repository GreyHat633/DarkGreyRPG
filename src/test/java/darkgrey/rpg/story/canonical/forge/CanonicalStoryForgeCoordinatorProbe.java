package darkgrey.rpg.story.canonical.forge;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;

import com.google.gson.JsonElement;
import com.google.gson.JsonParser;

import darkgrey.rpg.dialogue.DialogueDefinition;
import darkgrey.rpg.graph.canonical.CanonicalGraph;
import darkgrey.rpg.graph.canonical.CanonicalGraphConnection;
import darkgrey.rpg.graph.canonical.CanonicalGraphInterfaceKind;
import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphPort;
import darkgrey.rpg.graph.canonical.CanonicalGraphPortDirection;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceKind;
import darkgrey.rpg.graph.canonical.CanonicalProjectContent;
import darkgrey.rpg.graph.canonical.CanonicalStoryLogicConnection;
import darkgrey.rpg.graph.canonical.CanonicalStoryLogicGraph;
import darkgrey.rpg.graph.canonical.CanonicalStoryMembership;
import darkgrey.rpg.graph.canonical.CanonicalStoryMembershipSet;
import darkgrey.rpg.project.ActorDefinition;
import darkgrey.rpg.project.ProjectDefinition;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.quest.QuestDefinition;
import darkgrey.rpg.session.persistence.CanonicalSessionSavedData;
import darkgrey.rpg.session.server.CanonicalSessionDispatch;
import darkgrey.rpg.session.server.CanonicalSessionServerService;
import darkgrey.rpg.story.StoryDefinition;
import darkgrey.rpg.story.canonical.CanonicalStorySessionCompletionRoute;
import darkgrey.rpg.story.canonical.CanonicalStorySessionCompletionRouter;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryActionConfiguration;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryStatus;
import darkgrey.rpg.story.canonical.server.CanonicalStoryDispatch;
import darkgrey.rpg.story.canonical.server.CanonicalStoryServerService;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceStore;
import darkgrey.rpg.task.instance.CanonicalTaskResourceResolver;
import darkgrey.rpg.task.runtime.CanonicalTaskEvent;

/** Server-neutral proof for the Forge Story aggregate coordinator and failure cleanup. */
public final class CanonicalStoryForgeCoordinatorProbe {

    private static final UUID PLAYER = UUID.fromString("40000000-0000-0000-0000-000000000005");

    private CanonicalStoryForgeCoordinatorProbe() {}

    public static void main(String[] args) {
        sessionActionTermination();
        bartenderVerticalSlice();
        repeatableRuns();
        activeChildrenCleanup();
        failedActionMarksErrorAndCleans();
        crossStoryPublicLogicAndDynamicResume();
        System.out.println("CANONICAL_STORY_FORGE_AGGREGATE_ROUTE=PASS");
        System.out.println("CANONICAL_STORY_FORGE_TRANSFER_ROUTE=PASS");
        System.out.println("CANONICAL_STORY_FORGE_FAILURE_CLEANUP=PASS");
        System.out.println("CANONICAL_STORY_BARTENDER_VERTICAL_SLICE=PASS");
        System.out.println("CANONICAL_STORY_BARTENDER_PERSISTENCE_RELOAD=PASS");
        System.out.println("CANONICAL_STORY_ACTIVE_CHILD_CLEANUP=PASS");
        System.out.println("CANONICAL_STORY_RUNTIME_EXECUTION_LOOP=PASS");
        System.out.println("CANONICAL_STORY_CROSS_STORY_PUBLIC_LOGIC=PASS");
        System.out.println("CANONICAL_STORY_CROSS_STORY_DYNAMIC_RESUME=PASS");
    }

    private static void crossStoryPublicLogicAndDynamicResume() {
        ProjectSnapshot project = crossStoryLogicProject();
        CanonicalSessionSavedData data = new CanonicalSessionSavedData();
        CanonicalStoryServerService stories = new CanonicalStoryServerService(project, data);
        RecordingGateway gateway = new RecordingGateway(project, data, false);
        UUID other = UUID.fromString("40000000-0000-0000-0000-000000000006");

        CanonicalStoryForgeManager
            .routeTrusted(PLAYER, stories, data, stories.startByEntry(PLAYER, "logic_source", 400L), gateway);
        CanonicalStoryForgeManager
            .routeTrusted(PLAYER, stories, data, stories.startByEntry(PLAYER, "logic_wait", 401L), gateway);
        check(
            data.getStorySnapshot(PLAYER, "logic_wait")
                .getRuntimeSnapshot()
                .getWaitKind() == darkgrey.rpg.story.canonical.runtime.CanonicalStoryWaitKind.CONDITION,
            "Cross-Story target did not enter durable Condition wait");
        check(
            !CanonicalStoryForgeManager.propagateStoryLogicTrusted(PLAYER, project, stories, data, gateway),
            "False public Logic unexpectedly routed a target");
        check(data.getStorySnapshot(PLAYER, "logic_start") == null, "False public Logic started target Story");

        CanonicalStoryForgeManager.routeTrusted(
            PLAYER,
            stories,
            data,
            stories.setLogicInput(PLAYER, "logic_source", "source_gate", true, 410L),
            gateway);
        check(
            CanonicalStoryForgeManager.propagateStoryLogicTrusted(PLAYER, project, stories, data, gateway),
            "True public Logic did not propagate");
        check(
            data.getStorySnapshot(PLAYER, "logic_start")
                .getRuntimeSnapshot()
                .getStatus() == CanonicalStoryStatus.TERMINATED,
            "Public Logic did not activate the connected target Story");
        check(
            data.getStorySnapshot(PLAYER, "logic_wait")
                .getRuntimeSnapshot()
                .getStatus() == CanonicalStoryStatus.TERMINATED,
            "Public Logic change did not resume the waiting Condition outlet");
        check(
            !CanonicalStoryForgeManager.propagateStoryLogicTrusted(PLAYER, project, stories, data, gateway),
            "Stable public Logic repeated a completed propagation");

        CanonicalStoryServerService otherStories = new CanonicalStoryServerService(project, data);
        check(
            !CanonicalStoryForgeManager.propagateStoryLogicTrusted(other, project, otherStories, data, gateway),
            "One player's public Logic leaked into another player");
        check(data.getStorySnapshot(other, "logic_start") == null, "Cross-player target Story was created");
    }

    private static void bartenderVerticalSlice() {
        ProjectSnapshot project = bartenderProject();
        CanonicalSessionSavedData data = new CanonicalSessionSavedData();
        CanonicalStoryServerService stories = new CanonicalStoryServerService(project, data);
        BartenderGateway gateway = new BartenderGateway(project, data);

        check(
            CanonicalStoryForgeManager.routeTrusted(
                PLAYER,
                stories,
                data,
                stories.startByActor(PLAYER, "bartender_story", "bartender", 100L),
                gateway),
            "Bartender interaction did not route offer Session");
        check(gateway.sessionStarts == 1, "Offer Session did not start exactly once");

        data = reloadSessionWorld(data);
        stories = new CanonicalStoryServerService(project, data);
        gateway.rebind(project, data);
        check(
            data.getStorySnapshot(PLAYER, "bartender_story")
                .getRuntimeSnapshot()
                .getStatus() == CanonicalStoryStatus.ACTIVE,
            "Active Story did not survive reload");
        check(
            data.getStorySnapshot(PLAYER, "bartender_story")
                .getRuntimeSnapshot()
                .getWaitKind() == darkgrey.rpg.story.canonical.runtime.CanonicalStoryWaitKind.SESSION,
            "Reloaded Story did not retain Session wait");
        check(
            gateway.sessions.resume(PLAYER, "bartender_story")
                .getFrame()
                .getCurrentNodeId()
                .equals("line"),
            "Active Session did not survive reload");

        completeSession(project, data, gateway, "bartender_story", "line");
        check(
            CanonicalStoryForgeManager
                .routeTrusted(PLAYER, stories, data, stories.resumeSession(PLAYER, "bartender_story", 110L), gateway),
            "Offer completion did not route slime Task");
        check(gateway.taskStarts == 1, "Slime Task did not start exactly once");
        CanonicalTaskInstanceSnapshot activeTask = gateway.tasks.get(PLAYER, "bartender_story", "slime_task_place")
            .snapshot();
        check(
            activeTask.getStatus() == darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus.ACTIVE
                && activeTask.getRuntimeSnapshot()
                    .getProgress()
                    .get("kill")
                    .intValue() == 0,
            "Slime Task was not activated with zero progress");
        for (int kill = 0; kill < 5; kill++) gateway.tasks.acceptEvent(
            PLAYER,
            "bartender_story",
            "slime_task_place",
            CanonicalTaskEvent.killEntity("slime"),
            120L + kill);
        gateway.reloadTasks();
        check(
            gateway.tasks.get(PLAYER, "bartender_story", "slime_task_place")
                .snapshot()
                .getRuntimeSnapshot()
                .getProgress()
                .get("kill")
                .intValue() == 5
                && gateway.tasks.get(PLAYER, "bartender_story", "slime_task_place")
                    .snapshot()
                    .getStatus() == darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus.ACTIVE,
            "Active Task progress did not survive reload");
        for (int kill = 5; kill < 10; kill++) gateway.tasks.acceptEvent(
            PLAYER,
            "bartender_story",
            "slime_task_place",
            CanonicalTaskEvent.killEntity("slime"),
            120L + kill);
        CanonicalTaskInstanceSnapshot settled = gateway.tasks.get(PLAYER, "bartender_story", "slime_task_place")
            .snapshot();
        check(
            settled.getStatus() == darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus.SETTLED,
            "Ten slime kills did not settle Task");
        check(
            CanonicalStoryForgeManager.routeTrusted(
                PLAYER,
                stories,
                data,
                stories.resumeTask(PLAYER, "bartender_story", settled, 140L),
                gateway),
            "Task settlement did not route completion Session");
        check(gateway.sessionStarts == 2, "Completion Session did not start exactly once");

        completeSession(project, data, gateway, "bartender_story", "line");
        check(
            CanonicalStoryForgeManager
                .routeTrusted(PLAYER, stories, data, stories.resumeSession(PLAYER, "bartender_story", 150L), gateway),
            "Completion Session did not route reward and termination");
        check(gateway.actions == 1, "Reward Action did not execute exactly once");
        check(
            "starter_reward".equals(gateway.rewardItem) && gateway.rewardAmount == 10,
            "Reward Action was not the authored ten-coin payload");
        check(
            gateway.cleaned.equals(Collections.singletonList("bartender_story")),
            "Terminated bartender Story was not cleaned exactly once");
        check(
            data.getStorySnapshot(PLAYER, "bartender_story")
                .getRuntimeSnapshot()
                .getStatus() == CanonicalStoryStatus.TERMINATED,
            "Bartender Story did not terminate");
        check(data.getSnapshot(PLAYER, "bartender_story") == null, "Terminated Story retained an active Session");
        check(!stories.isStartEligible(PLAYER, "bartender_story"), "Once terminal Story retained Start eligibility");
        System.out.println("STORY_ONCE_TERMINAL_START_NOT_ELIGIBLE=PASS");
        check(
            !gateway.tasks.get(PLAYER, "bartender_story", "slime_task_place")
                .isActive(),
            "Terminated Story retained an active Task");
    }

    private static CanonicalSessionSavedData reloadSessionWorld(CanonicalSessionSavedData source) {
        NBTTagCompound checkpoint = new NBTTagCompound();
        source.writeToNBT(checkpoint);
        CanonicalSessionSavedData restored = new CanonicalSessionSavedData();
        restored.readFromNBT(checkpoint);
        return restored;
    }

    private static void activeChildrenCleanup() {
        ProjectSnapshot project = bartenderProject();
        CanonicalSessionSavedData data = new CanonicalSessionSavedData();
        CanonicalStoryServerService stories = new CanonicalStoryServerService(project, data);
        BartenderGateway gateway = new BartenderGateway(project, data);
        check(
            CanonicalStoryForgeManager.routeTrusted(
                PLAYER,
                stories,
                data,
                stories.startByActor(PLAYER, "bartender_story", "bartender", 300L),
                gateway),
            "Cleanup fixture did not start Session");
        CanonicalTaskInstanceSnapshot task = gateway.startTask("bartender_story", "slime_task_place", "slime_task");
        check(
            task.getStatus() == darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus.ACTIVE,
            "Cleanup fixture Task was not active");
        check(data.getSnapshot(PLAYER, "bartender_story") != null, "Cleanup fixture Session was not active");
        check(data.markStoryError(PLAYER, "bartender_story", 301L), "Cleanup fixture Story did not enter ERROR");
        gateway.cleanup("bartender_story");
        check(
            data.getStorySnapshot(PLAYER, "bartender_story")
                .getRuntimeSnapshot()
                .getStatus() == CanonicalStoryStatus.ERROR,
            "Cleanup fixture Story error state was not retained");
        check(data.getSnapshot(PLAYER, "bartender_story") == null, "Cleanup fixture retained active Session");
        check(
            gateway.tasks.get(PLAYER, "bartender_story", "slime_task_place")
                .snapshot()
                .getStatus() == darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus.CANCELLED_BY_STORY_TERMINATION,
            "Cleanup fixture retained active Task");
    }

    private static void completeSession(ProjectSnapshot project, CanonicalSessionSavedData data,
        BartenderGateway gateway, String storyId, String lineNodeId) {
        CanonicalSessionDispatch completion = gateway.sessions.continueLine(
            PLAYER,
            storyId,
            gateway.frame.getFrame()
                .getTransportId(),
            lineNodeId);
        CanonicalStorySessionCompletionRoute route = new CanonicalStorySessionCompletionRouter(project, storyId)
            .route(completion.getCompletionResult());
        check(data.acceptAndConsume(completion.getCompletionResult(), route), "Session completion was not accepted");
    }

    private static void sessionActionTermination() {
        ProjectSnapshot project = project();
        CanonicalSessionSavedData data = new CanonicalSessionSavedData();
        CanonicalStoryServerService stories = new CanonicalStoryServerService(project, data);
        RecordingGateway gateway = new RecordingGateway(project, data, false);
        CanonicalStoryDispatch first = stories.startByEntry(PLAYER, "story_a", 10L);
        check(
            CanonicalStoryForgeManager.routeTrusted(PLAYER, stories, data, first, gateway),
            "Session boundary was not routed");
        check(gateway.sessionStarts == 1, "Session was not started exactly once");

        CanonicalSessionDispatch completion = gateway.sessions.continueLine(
            PLAYER,
            "story_a",
            gateway.frame.getFrame()
                .getTransportId(),
            "line");
        CanonicalStorySessionCompletionRoute route = new CanonicalStorySessionCompletionRouter(project, "story_a")
            .route(completion.getCompletionResult());
        data.acceptAndConsume(completion.getCompletionResult(), route);
        CanonicalStoryDispatch resumed = stories.resumeSession(PLAYER, "story_a", 20L);
        check(
            CanonicalStoryForgeManager.routeTrusted(PLAYER, stories, data, resumed, gateway),
            "Action/transfer chain was not routed");
        check(gateway.actions == 1, "Action was not executed exactly once");
        check(gateway.cleaned.equals(Collections.singletonList("story_a")), "Terminated Story was not cleaned");
        check(
            data.getStorySnapshot(PLAYER, "story_a")
                .getRuntimeSnapshot()
                .getStatus() == CanonicalStoryStatus.TERMINATED,
            "Source Story did not retain terminal state");
        check(data.getStorySnapshot(PLAYER, "story_b") == null, "Unrelated Story was started by termination");
    }

    private static void failedActionMarksErrorAndCleans() {
        ProjectSnapshot project = project();
        CanonicalSessionSavedData data = new CanonicalSessionSavedData();
        CanonicalStoryServerService stories = new CanonicalStoryServerService(project, data);
        RecordingGateway gateway = new RecordingGateway(project, data, true);
        CanonicalStoryForgeManager
            .routeTrusted(PLAYER, stories, data, stories.startByEntry(PLAYER, "story_a", 30L), gateway);
        CanonicalSessionDispatch completion = gateway.sessions.continueLine(
            PLAYER,
            "story_a",
            gateway.frame.getFrame()
                .getTransportId(),
            "line");
        data.acceptAndConsume(
            completion.getCompletionResult(),
            new CanonicalStorySessionCompletionRouter(project, "story_a").route(completion.getCompletionResult()));
        try {
            CanonicalStoryForgeManager
                .routeTrusted(PLAYER, stories, data, stories.resumeSession(PLAYER, "story_a", 40L), gateway);
            throw new AssertionError("Failed action was accepted");
        } catch (IllegalStateException expected) {
            check(
                data.getStorySnapshot(PLAYER, "story_a")
                    .getRuntimeSnapshot()
                    .getStatus() == CanonicalStoryStatus.ERROR,
                "Failed action did not mark Story ERROR");
            check(gateway.cleaned.contains("story_a"), "Failed Story was not cleaned");
        }
    }

    private static final class RecordingGateway implements CanonicalStoryForgeManager.AggregateGateway {

        private final CanonicalSessionServerService sessions;
        private final boolean failAction;
        private final List<String> cleaned = new ArrayList<String>();
        private CanonicalSessionDispatch frame;
        private int sessionStarts;
        private int actions;

        RecordingGateway(ProjectSnapshot project, CanonicalSessionSavedData data, boolean failAction) {
            this.sessions = new CanonicalSessionServerService(project, data);
            this.failAction = failAction;
        }

        @Override
        public boolean startSession(String storyId, String placementId, boolean activationLogic) {
            sessionStarts++;
            frame = sessions.start(PLAYER, storyId, placementId, activationLogic);
            return true;
        }

        @Override
        public CanonicalTaskInstanceSnapshot startTask(String storyId, String placementId, String resourceId) {
            throw new AssertionError("Task route was not expected");
        }

        @Override
        public boolean executeAction(CanonicalStoryDispatch action) {
            actions++;
            return !failAction;
        }

        @Override
        public void cleanup(String storyId) {
            cleaned.add(storyId);
        }

        @Override
        public void resetPreviousRun(String storyId) {
            throw new AssertionError("Repeat reset was not expected");
        }
    }

    private static final class BartenderGateway implements CanonicalStoryForgeManager.AggregateGateway {

        private final ProjectSnapshot project;
        private CanonicalSessionServerService sessions;
        private CanonicalTaskInstanceStore tasks = new CanonicalTaskInstanceStore();
        private final List<String> cleaned = new ArrayList<String>();
        private CanonicalSessionDispatch frame;
        private int sessionStarts;
        private int taskStarts;
        private int actions;
        private String rewardItem;
        private int rewardAmount;

        BartenderGateway(ProjectSnapshot project, CanonicalSessionSavedData data) {
            this.project = project;
            this.sessions = new CanonicalSessionServerService(project, data);
        }

        void rebind(ProjectSnapshot project, CanonicalSessionSavedData data) {
            this.sessions = new CanonicalSessionServerService(project, data);
            this.frame = sessions.resume(PLAYER, "bartender_story");
        }

        void reloadTasks() {
            NBTTagCompound checkpoint = tasks.writeToNbt();
            CanonicalTaskInstanceStore restored = new CanonicalTaskInstanceStore();
            restored.readFromNbt(checkpoint, new CanonicalTaskResourceResolver() {

                @Override
                public CanonicalGraphResource resolve(String resourceId) {
                    return project.getCanonicalTask(resourceId);
                }
            });
            tasks = restored;
        }

        @Override
        public boolean startSession(String storyId, String placementId, boolean activationLogic) {
            sessionStarts++;
            frame = sessions.start(PLAYER, storyId, placementId, activationLogic);
            return true;
        }

        @Override
        public CanonicalTaskInstanceSnapshot startTask(String storyId, String placementId, String resourceId) {
            taskStarts++;
            return tasks.start(PLAYER, storyId, placementId, project.getCanonicalTask(resourceId), 115L)
                .snapshot();
        }

        @Override
        public boolean executeAction(CanonicalStoryDispatch action) {
            actions++;
            CanonicalStoryActionConfiguration configuration = CanonicalStoryActionConfiguration
                .parse(action.getActionProperties());
            rewardItem = configuration.getItemId();
            rewardAmount = configuration.getAmount();
            return true;
        }

        @Override
        public void cleanup(String storyId) {
            cleaned.add(storyId);
            tasks.cancelByStory(PLAYER, storyId);
        }

        @Override
        public void resetPreviousRun(String storyId) {
            tasks.discardByPlayerStory(PLAYER, storyId);
        }
    }

    private static ProjectSnapshot project() {
        Map<String, CanonicalGraphResource> stories = new LinkedHashMap<String, CanonicalGraphResource>();
        stories.put("story_a", storyA());
        stories.put("story_b", storyB());
        Map<String, CanonicalGraphResource> sessions = new LinkedHashMap<String, CanonicalGraphResource>();
        sessions.put("session", session());
        Map<String, CanonicalStoryMembership> memberships = new LinkedHashMap<String, CanonicalStoryMembership>();
        memberships.put("story_a", membership("story_a", Collections.singletonList("session")));
        memberships.put("story_b", membership("story_b", Collections.<String>emptyList()));
        return new ProjectSnapshot(
            new ProjectDefinition(1, "forge-story", "Forge Story"),
            Collections.singletonMap(
                "actor",
                new ActorDefinition(1, "actor", "Actor", "", Collections.<String>emptyList(), "")),
            Collections.<String, DialogueDefinition>emptyMap(),
            Collections.<String, QuestDefinition>emptyMap(),
            Collections.<String, StoryDefinition>emptyMap(),
            new CanonicalProjectContent(
                stories,
                sessions,
                Collections.<String, CanonicalGraphResource>emptyMap(),
                memberships));
    }

    private static ProjectSnapshot crossStoryLogicProject() {
        Map<String, CanonicalGraphResource> stories = new LinkedHashMap<String, CanonicalGraphResource>();
        stories.put("logic_source", logicConditionStory("logic_source", "source_gate", "signal"));
        stories.put("logic_wait", logicConditionStory("logic_wait", "target_gate", null));
        stories.put("logic_start", logicStartStory());
        List<CanonicalStoryLogicConnection> connections = Arrays.asList(
            new CanonicalStoryLogicConnection("logic_source", "signal", "logic_wait", "target_gate"),
            new CanonicalStoryLogicConnection("logic_source", "signal", "logic_start", "start_gate"));
        return new ProjectSnapshot(
            new ProjectDefinition(1, "cross-story-logic", "Cross Story Logic"),
            Collections.<String, ActorDefinition>emptyMap(),
            Collections.<String, DialogueDefinition>emptyMap(),
            Collections.<String, QuestDefinition>emptyMap(),
            Collections.<String, StoryDefinition>emptyMap(),
            new CanonicalProjectContent(
                stories,
                Collections.<String, CanonicalGraphResource>emptyMap(),
                Collections.<String, CanonicalGraphResource>emptyMap(),
                Collections.<String, CanonicalStoryMembership>emptyMap(),
                new CanonicalStoryLogicGraph(connections)));
    }

    private static CanonicalGraphResource logicConditionStory(String storyId, String inputPortId, String outputPortId) {
        CanonicalGraphNode start = start(storyId);
        CanonicalGraphNode input = node(
            "input",
            "logic_input",
            ports(logicOut("logic_out", 0)),
            props("port_id", inputPortId, "display_name", inputPortId));
        CanonicalGraphNode condition = node(
            "condition",
            "condition",
            ports(in("flow_in", 0), logicIn("logic_in", 1), out("flow_true", 2), out("flow_false", 3)),
            empty());
        CanonicalGraphNode end = node("end", "terminate", ports(in("flow_in", 0)), empty());
        List<CanonicalGraphNode> nodes = new ArrayList<CanonicalGraphNode>(Arrays.asList(start, input, condition, end));
        List<CanonicalGraphConnection> edges = new ArrayList<CanonicalGraphConnection>(
            Arrays.asList(
                edge("start", "entry", "condition", "flow_in"),
                logicEdge("input", "logic_out", "condition", "logic_in"),
                edge("condition", "flow_true", "end", "flow_in")));
        if (outputPortId != null) {
            nodes.add(
                node(
                    "output",
                    "logic_output",
                    ports(logicIn("logic_in", 0)),
                    props("port_id", outputPortId, "display_name", outputPortId)));
            edges.add(logicEdge("input", "logic_out", "output", "logic_in"));
        }
        return resource(storyId, CanonicalGraphResourceKind.STORY, nodes, edges);
    }

    private static CanonicalGraphResource logicStartStory() {
        Map<String, JsonElement> properties = new LinkedHashMap<String, JsonElement>();
        properties.put("repeat_policy", json("\"once\""));
        properties.put(
            "triggers",
            json(
                "[{\"port_id\":\"logic_start\",\"display_name\":\"logic_start\",\"trigger_type\":\"logic\","
                    + "\"trigger_properties\":{},\"order\":0,\"logic_port_id\":\"logic_condition\"}]"));
        CanonicalGraphNode start = node(
            "start",
            "start",
            ports(out("logic_start", 0), logicIn("logic_condition", 1)),
            properties);
        CanonicalGraphNode input = node(
            "input",
            "logic_input",
            ports(logicOut("logic_out", 0)),
            props("port_id", "start_gate", "display_name", "start_gate"));
        CanonicalGraphNode end = node("end", "terminate", ports(in("flow_in", 0)), empty());
        return resource(
            "logic_start",
            CanonicalGraphResourceKind.STORY,
            Arrays.asList(start, input, end),
            Arrays.asList(
                logicEdge("input", "logic_out", "start", "logic_condition"),
                edge("start", "logic_start", "end", "flow_in")));
    }

    private static void repeatableRuns() {
        ProjectSnapshot project = bartenderProject(true);
        CanonicalSessionSavedData data = new CanonicalSessionSavedData();
        CanonicalStoryServerService stories = new CanonicalStoryServerService(project, data);
        BartenderGateway gateway = new BartenderGateway(project, data);
        UUID otherPlayer = UUID.fromString("40000000-0000-0000-0000-000000000007");
        long otherPlayerSession = data
            .start(otherPlayer, "bartender_story", "offer_place", project.getCanonicalSession("offer"))
            .getTransportId();
        long otherStorySession = data.start(PLAYER, "other_story", "offer_place", project.getCanonicalSession("offer"))
            .getTransportId();
        for (int run = 1; run <= 3; run++) {
            darkgrey.rpg.session.instance.CanonicalSessionInstanceSnapshot previousSession = data
                .getSnapshot(PLAYER, "bartender_story");
            CanonicalStoryForgeManager.routeTrusted(
                PLAYER,
                stories,
                data,
                stories.startByActor(PLAYER, "bartender_story", "bartender", run * 1000L),
                gateway);
            check(
                previousSession == null
                    || previousSession.getTransportId() != data.getSnapshot(PLAYER, "bartender_story")
                        .getTransportId(),
                "Repeat reused previous-run Session transport");
            check(
                data.getSnapshot(otherPlayer, "bartender_story")
                    .getTransportId() == otherPlayerSession,
                "Repeat reset altered another player's Session");
            check(
                data.getSnapshot(PLAYER, "other_story")
                    .getTransportId() == otherStorySession,
                "Repeat reset altered another Story's Session");
            check(
                data.getPendingContinuation(PLAYER, "bartender_story") == null,
                "Repeat reset retained previous-run continuation");
            check(!stories.isStartEligible(PLAYER, "bartender_story"), "ACTIVE Start remained eligible");
            check(
                darkgrey.rpg.story.canonical.runtime.CanonicalStoryTriggerIndex.build(project)
                    .matchActor("bartender", stories.eligibleStartStoryIds(PLAYER))
                    .isEmpty(),
                "ACTIVE Start entered actor candidates");
            int sessionStartsBefore = gateway.sessionStarts;
            check(
                !CanonicalStoryForgeManager.routeTrusted(
                    PLAYER,
                    stories,
                    data,
                    stories.startByActor(PLAYER, "bartender_story", "bartender", run * 1000L + 1),
                    gateway),
                "ACTIVE Start consumed event");
            check(gateway.sessionStarts == sessionStartsBefore, "ACTIVE Start rerouted Session");
            completeSession(project, data, gateway, "bartender_story", "line");
            CanonicalStoryForgeManager.routeTrusted(
                PLAYER,
                stories,
                data,
                stories.resumeSession(PLAYER, "bartender_story", run * 1000L + 1),
                gateway);
            CanonicalTaskInstanceSnapshot task = gateway.tasks.get(PLAYER, "bartender_story", "slime_task_place")
                .snapshot();
            check(
                task.getStatus() == darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus.ACTIVE
                    && task.getRuntimeSnapshot()
                        .getProgress()
                        .get("kill")
                        .intValue() == 0,
                "Repeat Run " + run + " must start at zero with an ACTIVE Task");
            if (run == 3) break;
            for (int kill = 0; kill < 2; kill++) gateway.tasks.acceptEvent(
                PLAYER,
                "bartender_story",
                "slime_task_place",
                CanonicalTaskEvent.killEntity("slime"),
                run * 1000L + 10 + kill);
            int taskStartsBefore = gateway.taskStarts;
            CanonicalStoryForgeManager.routeTrusted(
                PLAYER,
                stories,
                data,
                stories.startByActor(PLAYER, "bartender_story", "bartender", run * 1000L + 12),
                gateway);
            check(
                gateway.taskStarts == taskStartsBefore && gateway.sessionStarts == sessionStartsBefore,
                "ACTIVE Start rerouted an aggregate");
            check(
                gateway.tasks.get(PLAYER, "bartender_story", "slime_task_place")
                    .snapshot()
                    .getRuntimeSnapshot()
                    .getProgress()
                    .get("kill")
                    .intValue() == 2,
                "ACTIVE Start reset progress");
            for (int kill = 2; kill < 10; kill++) gateway.tasks.acceptEvent(
                PLAYER,
                "bartender_story",
                "slime_task_place",
                CanonicalTaskEvent.killEntity("slime"),
                run * 1000L + 10 + kill);
            task = gateway.tasks.get(PLAYER, "bartender_story", "slime_task_place")
                .snapshot();
            CanonicalStoryForgeManager.routeTrusted(
                PLAYER,
                stories,
                data,
                stories.resumeTask(PLAYER, "bartender_story", task, run * 1000L + 30),
                gateway);
            completeSession(project, data, gateway, "bartender_story", "line");
            CanonicalStoryForgeManager.routeTrusted(
                PLAYER,
                stories,
                data,
                stories.resumeSession(PLAYER, "bartender_story", run * 1000L + 40),
                gateway);
            check(
                data.getStorySnapshot(PLAYER, "bartender_story")
                    .getRuntimeSnapshot()
                    .getStatus() == CanonicalStoryStatus.TERMINATED,
                "Repeat run did not terminate");
            check(
                gateway.tasks.get(PLAYER, "bartender_story", "slime_task_place")
                    .snapshot()
                    .getStatus() == darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus.SETTLED,
                "Terminal Task result was lost");
            // A previous-run child must survive a rejected trigger, then be removed on a valid new run.
            gateway.frame = gateway.sessions.start(PLAYER, "bartender_story", "offer_place", false);
            if (run == 2) completeSession(project, data, gateway, "bartender_story", "line");
            NBTTagCompound beforeRejected = new NBTTagCompound();
            data.writeToNBT(beforeRejected);
            try {
                stories.startByActor(PLAYER, "bartender_story", "wrong_actor", run * 1000L + 41);
                throw new AssertionError("Invalid repeat Start was accepted");
            } catch (darkgrey.rpg.graph.canonical.CanonicalGraphResourceException expected) {
                NBTTagCompound afterRejected = new NBTTagCompound();
                data.writeToNBT(afterRejected);
                check(beforeRejected.equals(afterRejected), "Invalid repeat Start changed previous-run state");
            }
        }
        System.out.println("STORY_REPEAT_RUN1_TASK_SETTLES=PASS");
        System.out.println("STORY_REPEAT_TERMINAL_RESULT_RETAINED=PASS");
        System.out.println("STORY_REPEAT_RUN2_RESETS_OLD_TASK=PASS");
        System.out.println("STORY_REPEAT_RUN2_STARTS_ZERO=PASS");
        System.out.println("STORY_REPEAT_RUN2_SETTLES_NORMALLY=PASS");
        System.out.println("STORY_REPEAT_RUN3_STARTS_ZERO=PASS");
        System.out.println("STORY_REPEAT_SESSION_CONTINUATION_RESET=PASS");
        System.out.println("STORY_REPEAT_REJECTED_START_PRESERVES_STATE=PASS");
        System.out.println("STORY_ACTIVE_START_NOT_ELIGIBLE=PASS");
        System.out.println("STORY_ACTIVE_START_DOES_NOT_REROUTE_TASK=PASS");
        System.out.println("STORY_ACTIVE_START_DOES_NOT_RESTART_SESSION=PASS");
    }

    private static ProjectSnapshot bartenderProject() {
        return bartenderProject(false);
    }

    private static ProjectSnapshot bartenderProject(boolean repeatable) {
        Map<String, CanonicalGraphResource> stories = new LinkedHashMap<String, CanonicalGraphResource>();
        stories.put("bartender_story", bartenderStory(repeatable));
        Map<String, CanonicalGraphResource> sessions = new LinkedHashMap<String, CanonicalGraphResource>();
        sessions.put("offer", session("offer", "offer_done", "酒馆老板说明史莱姆泛滥"));
        sessions.put("thanks", session("thanks", "thanks_done", "酒馆老板表示感谢"));
        Map<String, CanonicalGraphResource> tasks = new LinkedHashMap<String, CanonicalGraphResource>();
        tasks.put("slime_task", slimeTask());
        Map<String, CanonicalStoryMembership> memberships = new LinkedHashMap<String, CanonicalStoryMembership>();
        memberships.put(
            "bartender_story",
            new CanonicalStoryMembership(
                "bartender_story",
                new CanonicalStoryMembershipSet(
                    Collections.singletonList("bartender"),
                    Arrays.asList("offer", "thanks"),
                    Collections.singletonList("slime_task"))));
        return new ProjectSnapshot(
            new ProjectDefinition(1, "bartender-probe", "Bartender Probe"),
            Collections.singletonMap(
                "bartender",
                new ActorDefinition(1, "bartender", "酒馆老板", "", Collections.<String>emptyList(), "")),
            Collections.<String, DialogueDefinition>emptyMap(),
            Collections.<String, QuestDefinition>emptyMap(),
            Collections.<String, StoryDefinition>emptyMap(),
            new CanonicalProjectContent(stories, sessions, tasks, memberships));
    }

    private static CanonicalGraphResource bartenderStory(boolean repeatable) {
        Map<String, JsonElement> startProperties = new LinkedHashMap<String, JsonElement>();
        startProperties.put("repeat_policy", json(repeatable ? "\"repeatable\"" : "\"once\""));
        startProperties.put(
            "triggers",
            json(
                "[{\"port_id\":\"bartender\",\"display_name\":\"角色交互：酒馆老板\",\"trigger_type\":\"interact_actor\",\"trigger_properties\":{\"actor_id\":\"bartender\"},\"order\":0}]"));
        CanonicalGraphNode start = node(
            "start",
            "start",
            Collections.singletonList(
                new CanonicalGraphPort(
                    "bartender",
                    "角色交互：酒馆老板",
                    CanonicalGraphPortDirection.OUTPUT,
                    CanonicalGraphInterfaceKind.FLOW,
                    0)),
            startProperties);
        CanonicalGraphNode offer = node(
            "offer_place",
            "session",
            ports(in("flow_in", 0), logicIn("logic_in", 1), out("offer_done", 2)),
            props("resource_id", "offer"));
        CanonicalGraphNode task = node(
            "slime_task_place",
            "task",
            ports(in("flow_in", 0), out("done", 1)),
            props("resource_id", "slime_task"));
        CanonicalGraphNode thanks = node(
            "thanks_place",
            "session",
            ports(in("flow_in", 0), logicIn("logic_in", 1), out("thanks_done", 2)),
            props("resource_id", "thanks"));
        Map<String, JsonElement> rewardProperties = new LinkedHashMap<String, JsonElement>();
        rewardProperties.put("action_type", json("\"give_item\""));
        rewardProperties.put("item_id", json("\"starter_reward\""));
        rewardProperties.put("amount", json("10"));
        CanonicalGraphNode reward = node(
            "reward",
            "action",
            ports(in("flow_in", 0), out("flow_out", 1)),
            rewardProperties);
        CanonicalGraphNode end = node("end", "terminate", ports(in("flow_in", 0)), empty());
        return resource(
            "bartender_story",
            CanonicalGraphResourceKind.STORY,
            Arrays.asList(start, offer, task, thanks, reward, end),
            Arrays.asList(
                edge("start", "bartender", "offer_place", "flow_in"),
                edge("offer_place", "offer_done", "slime_task_place", "flow_in"),
                edge("slime_task_place", "done", "thanks_place", "flow_in"),
                edge("thanks_place", "thanks_done", "reward", "flow_in"),
                edge("reward", "flow_out", "end", "flow_in")));
    }

    private static CanonicalGraphResource session(String id, String resultPort, String text) {
        return resource(
            id,
            CanonicalGraphResourceKind.SESSION,
            Arrays.asList(
                node("start", "start", ports(out("flow_out", 0), logicOut("logic_out", 1)), empty()),
                node(
                    "line",
                    "line",
                    ports(in("flow_in", 0), out("flow_out", 1)),
                    props("speaker_actor_id", "bartender", "text", text)),
                node("done", "end", ports(in("flow_in", 0)), props("port_id", resultPort, "display_name", resultPort))),
            Arrays.asList(edge("start", "flow_out", "line", "flow_in"), edge("line", "flow_out", "done", "flow_in")));
    }

    private static CanonicalGraphResource slimeTask() {
        Map<String, JsonElement> objective = new LinkedHashMap<String, JsonElement>();
        objective.put("objective_type", json("\"kill_entity\""));
        objective.put("description", json("\"击杀 10 只史莱姆\""));
        objective.put("required", json("10"));
        objective.put("entity", json("\"slime\""));
        objective.put("prerequisite_enabled", json("true"));
        CanonicalGraphNode activate = node(
            "activate",
            "activate",
            Collections.singletonList(logicOut("logic_out", 0)),
            empty());
        CanonicalGraphNode kill = node(
            "kill",
            "objective",
            ports(logicIn("prerequisite", 0), logicOut("logic_status", 1)),
            objective);
        CanonicalGraphNode settle = node("settle", "settle", Collections.singletonList(logicIn("done", 0)), empty());
        return resource(
            "slime_task",
            CanonicalGraphResourceKind.TASK,
            Arrays.asList(activate, kill, settle),
            Arrays.asList(
                logicEdge("activate", "logic_out", "kill", "prerequisite"),
                logicEdge("kill", "logic_status", "settle", "done")));
    }

    private static CanonicalStoryMembership membership(String storyId, List<String> sessions) {
        return new CanonicalStoryMembership(
            storyId,
            new CanonicalStoryMembershipSet(
                "story_a".equals(storyId) ? Collections.singletonList("actor") : Collections.<String>emptyList(),
                sessions,
                Collections.<String>emptyList()));
    }

    private static CanonicalGraphResource storyA() {
        CanonicalGraphNode start = start("story_a");
        CanonicalGraphNode session = node(
            "session_place",
            "session",
            ports(in("flow_in", 0), logicIn("logic_in", 1), out("done", 2)),
            props("resource_id", "session"));
        CanonicalGraphNode action = node(
            "reward",
            "action",
            ports(in("flow_in", 0), out("flow_out", 1)),
            props("action_type", "send_message", "message", "Reward"));
        CanonicalGraphNode enter = node("enter_b", "terminate", ports(in("flow_in", 0)), empty());
        return resource(
            "story_a",
            CanonicalGraphResourceKind.STORY,
            Arrays.asList(start, session, action, enter),
            Arrays.asList(
                edge("start", "entry", "session_place", "flow_in"),
                edge("session_place", "done", "reward", "flow_in"),
                edge("reward", "flow_out", "enter_b", "flow_in")));
    }

    private static CanonicalGraphResource storyB() {
        CanonicalGraphNode start = start("story_b");
        CanonicalGraphNode end = node("end", "terminate", ports(in("flow_in", 0)), empty());
        return resource(
            "story_b",
            CanonicalGraphResourceKind.STORY,
            Arrays.asList(start, end),
            Collections.singletonList(edge("start", "entry", "end", "flow_in")));
    }

    private static CanonicalGraphNode start(String storyId) {
        Map<String, JsonElement> properties = new LinkedHashMap<String, JsonElement>();
        properties.put("repeat_policy", json("\"once\""));
        properties.put(
            "triggers",
            json(
                "[{\"port_id\":\"entry\",\"display_name\":\"entry\",\"trigger_type\":\"enter_story\",\"trigger_properties\":{},\"order\":0}]"));
        return node("start", "start", ports(out("entry", 0)), properties);
    }

    private static CanonicalGraphResource session() {
        return resource(
            "session",
            CanonicalGraphResourceKind.SESSION,
            Arrays.asList(
                node("start", "start", ports(out("flow_out", 0), logicOut("logic_out", 1)), empty()),
                node(
                    "line",
                    "line",
                    ports(in("flow_in", 0), out("flow_out", 1)),
                    props("speaker_actor_id", "actor", "text", "Hello")),
                node("done", "end", ports(in("flow_in", 0)), props("port_id", "done", "display_name", "Done"))),
            Arrays.asList(edge("start", "flow_out", "line", "flow_in"), edge("line", "flow_out", "done", "flow_in")));
    }

    private static CanonicalGraphResource resource(String id, CanonicalGraphResourceKind kind,
        List<CanonicalGraphNode> nodes, List<CanonicalGraphConnection> edges) {
        return new CanonicalGraphResource(1, kind, id, id, new CanonicalGraph(nodes, edges));
    }

    private static CanonicalGraphNode node(String id, String type, List<CanonicalGraphPort> ports,
        Map<String, JsonElement> properties) {
        return new CanonicalGraphNode(id, type, id, ports, properties);
    }

    private static CanonicalGraphPort in(String id, int order) {
        return port(id, CanonicalGraphPortDirection.INPUT, CanonicalGraphInterfaceKind.FLOW, order);
    }

    private static CanonicalGraphPort out(String id, int order) {
        return port(id, CanonicalGraphPortDirection.OUTPUT, CanonicalGraphInterfaceKind.FLOW, order);
    }

    private static CanonicalGraphPort logicIn(String id, int order) {
        return port(id, CanonicalGraphPortDirection.INPUT, CanonicalGraphInterfaceKind.LOGIC, order);
    }

    private static CanonicalGraphPort logicOut(String id, int order) {
        return port(id, CanonicalGraphPortDirection.OUTPUT, CanonicalGraphInterfaceKind.LOGIC, order);
    }

    private static CanonicalGraphPort port(String id, CanonicalGraphPortDirection direction,
        CanonicalGraphInterfaceKind kind, int order) {
        return new CanonicalGraphPort(id, id, direction, kind, order);
    }

    private static CanonicalGraphConnection edge(String fromNode, String fromPort, String toNode, String toPort) {
        return new CanonicalGraphConnection(fromNode, fromPort, toNode, toPort, CanonicalGraphInterfaceKind.FLOW);
    }

    private static CanonicalGraphConnection logicEdge(String fromNode, String fromPort, String toNode, String toPort) {
        return new CanonicalGraphConnection(fromNode, fromPort, toNode, toPort, CanonicalGraphInterfaceKind.LOGIC);
    }

    private static List<CanonicalGraphPort> ports(CanonicalGraphPort... values) {
        return Arrays.asList(values);
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

    private static void check(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
