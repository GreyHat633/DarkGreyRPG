package darkgrey.rpg.story.canonical.runtime;

import java.util.Arrays;
import java.util.Collections;
import java.util.HashMap;
import java.util.LinkedHashMap;
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
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceException;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceKind;
import darkgrey.rpg.story.canonical.CanonicalStoryPendingContinuation;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus;
import darkgrey.rpg.task.runtime.CanonicalTaskObjectiveStatus;
import darkgrey.rpg.task.runtime.CanonicalTaskSnapshot;
import darkgrey.rpg.task.runtime.CanonicalTaskStatus;

/** Executable pure-Java probe for PLAN Stage 5 single-cursor Story semantics. */
public final class CanonicalStoryRuntimeProbe {

    private static final UUID PLAYER = UUID.fromString("11111111-2222-3333-4444-555555555555");

    private CanonicalStoryRuntimeProbe() {}

    public static void main(String[] args) {
        fullSingleCursorPathAndRestore();
        flowJudgmentPathAndRestore();
        retiredStandaloneNodesRejected();
        terminalAndFailurePaths();
        strictGraphValidation();
        System.out.println("CANONICAL_STORY_RUNTIME_SINGLE_CURSOR=PASS");
        System.out.println("CANONICAL_STORY_FLOW_JUDGMENT=PASS");
        System.out.println("CANONICAL_STORY_RETIRED_STANDALONE_NODES_REJECTED=PASS");
        System.out.println("CANONICAL_STORY_RUNTIME_AGGREGATE_HANDOFF=PASS");
        System.out.println("CANONICAL_STORY_RUNTIME_STRICT_VALIDATION=PASS");
    }

    private static void flowJudgmentPathAndRestore() {
        CanonicalGraphResource story = flowJudgmentStory();
        CanonicalStoryRuntime runtime = CanonicalStoryRuntime
            .start(story, "trigger_accept", CanonicalStoryRepeatPolicy.ONCE);
        check(
            runtime.getWaitKind() == CanonicalStoryWaitKind.ACTION,
            "Flow Judgment setup did not pause before judgment");
        check(
            runtime.getExecutedFlowJudgmentNodeIds()
                .isEmpty(),
            "Flow Judgment was initially executed");
        check(
            !runtime.getPublicLogicOutputs()
                .get("judgment_executed")
                .booleanValue(),
            "Initial executed Logic was not false");

        CanonicalStoryRuntime restored = CanonicalStoryRuntime.restore(story, runtime.snapshot());
        restored.completeAction("gate");
        check(restored.getStatus() == CanonicalStoryStatus.TERMINATED, "Flow Judgment did not continue via flow_out");
        check(
            restored.getExecutedFlowJudgmentNodeIds()
                .equals(Collections.singletonList("judgment")),
            "Flow Judgment execution was not sticky");
        check(
            restored.getPublicLogicOutputs()
                .get("judgment_executed")
                .booleanValue(),
            "Executed Logic was not true");
        CanonicalStoryRuntime roundTrip = CanonicalStoryRuntime.restore(story, restored.snapshot());
        check(
            roundTrip.getExecutedFlowJudgmentNodeIds()
                .equals(Collections.singletonList("judgment")),
            "Flow Judgment execution did not survive snapshot restore");
        check(
            roundTrip.getPublicLogicOutputs()
                .get("judgment_executed")
                .booleanValue(),
            "Restored executed Logic was not true");
    }

    private static void retiredStandaloneNodesRejected() {
        for (final String type : Arrays.asList("interact_actor", "enter_region", "enter_story")) {
            final CanonicalGraphResource retired = story(
                "retired",
                Arrays.asList(start(), node("old", type, ports(flowIn("flow_in"), flowOut("flow_out", 1)), empty())),
                Collections.singletonList(flow("start", "trigger_accept", "old", "flow_in")));
            expectFailure("story.node.unsupported", new Runnable() {

                @Override
                public void run() {
                    CanonicalStoryRuntime.start(retired, "trigger_accept", CanonicalStoryRepeatPolicy.ONCE);
                }
            });
        }
    }

    private static void fullSingleCursorPathAndRestore() {
        CanonicalGraphResource story = fullStory();
        CanonicalStoryRuntime runtime = CanonicalStoryRuntime
            .start(story, "trigger_accept", CanonicalStoryRepeatPolicy.ONCE);
        check(runtime.getStatus() == CanonicalStoryStatus.ACTIVE, "Story did not stay active at Session wait");
        check(runtime.getWaitKind() == CanonicalStoryWaitKind.SESSION, "Story did not wait on Session");
        check("offer".equals(runtime.getCurrentNodeId()), "Wrong Session placement");
        check(!runtime.getSessionActivationLogic(), "Unconnected Session Logic input must default false");

        CanonicalStoryRuntime restored = CanonicalStoryRuntime.restore(story, runtime.snapshot());
        check(restored.getWaitKind() == CanonicalStoryWaitKind.SESSION, "Session wait did not restore");
        restored.resumeSession(
            new CanonicalStoryPendingContinuation(
                PLAYER,
                "tavern_story",
                "offer",
                "offer_session",
                7L,
                "accepted",
                "accepted_condition",
                "flow_in",
                bools("accepted_logic", true)));
        check(restored.getWaitKind() == CanonicalStoryWaitKind.TASK, "True Session Logic did not select Task");
        check("slime_task".equals(restored.getCurrentNodeId()), "Wrong Task placement");

        CanonicalStoryRuntime restoredTask = CanonicalStoryRuntime.restore(story, restored.snapshot());
        restoredTask.resumeTask(settledTask("complete", true));
        check(restoredTask.getWaitKind() == CanonicalStoryWaitKind.ACTION, "Task result did not reach Action");
        check("reward".equals(restoredTask.getCurrentNodeId()), "Wrong Action node");
        check(
            "give_currency".equals(
                restoredTask.getPendingActionProperties()
                    .get("action_type")
                    .getAsString()),
            "Action properties were not retained");

        CanonicalStoryRuntime restoredAction = CanonicalStoryRuntime.restore(story, restoredTask.snapshot());
        restoredAction.completeAction("reward");
        check(restoredAction.getStatus() == CanonicalStoryStatus.TERMINATED, "Terminal node was not reached");
        check(restoredAction.getWaitKind() == CanonicalStoryWaitKind.NONE, "Terminal Story retained a wait");
        CanonicalStoryRuntime.restore(story, restoredAction.snapshot());

        expectFailure("story.wait.state", new Runnable() {

            @Override
            public void run() {
                restoredAction.completeAction("reward");
            }
        });
    }

    private static void terminalAndFailurePaths() {
        CanonicalStoryRuntime rejected = CanonicalStoryRuntime
            .start(fullStory(), "trigger_accept", CanonicalStoryRepeatPolicy.REPEATABLE);
        rejected.resumeSession(
            new CanonicalStoryPendingContinuation(
                PLAYER,
                "tavern_story",
                "offer",
                "offer_session",
                8L,
                "rejected",
                "rejected_end",
                "flow_in",
                bools("accepted_logic", false)));
        check(rejected.getStatus() == CanonicalStoryStatus.TERMINATED, "Rejected branch did not terminate");
        check(
            rejected.snapshot()
                .getRepeatPolicy() == CanonicalStoryRepeatPolicy.REPEATABLE,
            "Repeat policy was not retained");

        final CanonicalStoryRuntime wrongSession = CanonicalStoryRuntime
            .start(fullStory(), "trigger_accept", CanonicalStoryRepeatPolicy.ONCE);
        expectFailure("story.session.identity", new Runnable() {

            @Override
            public void run() {
                wrongSession.resumeSession(
                    new CanonicalStoryPendingContinuation(
                        PLAYER,
                        "tavern_story",
                        "other",
                        "offer_session",
                        9L,
                        "accepted",
                        "accepted_condition",
                        "flow_in",
                        bools("accepted_logic", true)));
            }
        });

        final CanonicalStoryRuntime wrongLogic = CanonicalStoryRuntime
            .start(fullStory(), "trigger_accept", CanonicalStoryRepeatPolicy.ONCE);
        expectFailure("story.aggregate.logic", new Runnable() {

            @Override
            public void run() {
                wrongLogic.resumeSession(
                    new CanonicalStoryPendingContinuation(
                        PLAYER,
                        "tavern_story",
                        "offer",
                        "offer_session",
                        10L,
                        "accepted",
                        "accepted_condition",
                        "flow_in",
                        Collections.<String, Boolean>emptyMap()));
            }
        });
    }

    private static void strictGraphValidation() {
        CanonicalGraphResource directionScopedOrders = story(
            "direction-scoped-orders",
            Arrays.asList(
                start(),
                node(
                    "offer",
                    "session",
                    ports(flowIn("flow_in"), flowOut("accepted", 0)),
                    props("resource_id", "offer_session")),
                node("end", "terminate", ports(flowIn("flow_in")), empty())),
            Arrays.asList(
                flow("start", "trigger_accept", "offer", "flow_in"),
                flow("offer", "accepted", "end", "flow_in")));
        check(
            CanonicalStoryRuntime.start(directionScopedOrders, "trigger_accept", CanonicalStoryRepeatPolicy.ONCE)
                .getWaitKind() == CanonicalStoryWaitKind.SESSION,
            "Input and output ports may independently start their order at zero");

        final CanonicalGraphResource duplicateOutputOrders = story(
            "duplicate-output-orders",
            Arrays.asList(
                start(),
                node(
                    "offer",
                    "session",
                    ports(flowIn("flow_in"), flowOut("accepted", 0), flowOut("rejected", 0)),
                    props("resource_id", "offer_session")),
                node("accepted_end", "terminate", ports(flowIn("flow_in")), empty()),
                node("rejected_end", "terminate", ports(flowIn("flow_in")), empty())),
            Arrays.asList(
                flow("start", "trigger_accept", "offer", "flow_in"),
                flow("offer", "accepted", "accepted_end", "flow_in"),
                flow("offer", "rejected", "rejected_end", "flow_in")));
        expectFailure("story.port.order", new Runnable() {

            @Override
            public void run() {
                CanonicalStoryRuntime.start(duplicateOutputOrders, "trigger_accept", CanonicalStoryRepeatPolicy.ONCE);
            }
        });

        final CanonicalGraphResource multipleTargets = story(
            "multiple",
            Arrays.asList(
                start(),
                node("one", "terminate", ports(flowIn("flow_in")), empty()),
                node("two", "terminate", ports(flowIn("flow_in")), empty())),
            Arrays.asList(
                flow("start", "trigger_accept", "one", "flow_in"),
                flow("start", "trigger_accept", "two", "flow_in")));
        expectFailure("story.flow.output.multiple_targets", new Runnable() {

            @Override
            public void run() {
                CanonicalStoryRuntime.start(multipleTargets, "trigger_accept", CanonicalStoryRepeatPolicy.ONCE);
            }
        });

        final CanonicalGraphResource logicCycle = story(
            "cycle",
            Arrays.asList(
                start(),
                node("one", "not", ports(logicIn("logic_in"), logicOut("logic_out")), empty()),
                node("two", "not", ports(logicIn("logic_in"), logicOut("logic_out")), empty()),
                node("end", "terminate", ports(flowIn("flow_in")), empty())),
            Arrays.asList(
                flow("start", "trigger_accept", "end", "flow_in"),
                logic("one", "logic_out", "two", "logic_in"),
                logic("two", "logic_out", "one", "logic_in")));
        expectFailure("story.logic.cycle", new Runnable() {

            @Override
            public void run() {
                CanonicalStoryRuntime.start(logicCycle, "trigger_accept", CanonicalStoryRepeatPolicy.ONCE);
            }
        });

        final CanonicalGraphResource noTriggers = story(
            "no-trigger",
            Arrays.asList(node("start", "start", Collections.<CanonicalGraphPort>emptyList(), empty())),
            Collections.<CanonicalGraphConnection>emptyList());
        expectFailure("story.start.trigger.required", new Runnable() {

            @Override
            public void run() {
                CanonicalStoryRuntime.start(noTriggers, "missing", CanonicalStoryRepeatPolicy.ONCE);
            }
        });
    }

    private static CanonicalGraphResource fullStory() {
        return story(
            "tavern_story",
            Arrays.asList(
                start(),
                node(
                    "offer",
                    "session",
                    ports(
                        flowIn("flow_in"),
                        logicIn("logic_in"),
                        flowOut("accepted", 2),
                        flowOut("rejected", 3),
                        logicOut("accepted_logic", 4)),
                    props("resource_id", "offer_session")),
                node(
                    "accepted_condition",
                    "condition",
                    ports(flowIn("flow_in"), logicIn("logic_in"), flowOut("flow_true", 2), flowOut("flow_false", 3)),
                    empty()),
                node(
                    "slime_task",
                    "task",
                    ports(flowIn("flow_in"), flowOut("complete", 1), logicOut("kill_done", 2)),
                    props("resource_id", "slime_task_resource")),
                node(
                    "reward",
                    "action",
                    ports(flowIn("flow_in"), flowOut("flow_out", 1)),
                    props("action_type", "give_currency", "amount", 10)),
                node("epilogue_transfer", "terminate", ports(flowIn("flow_in")), empty()),
                node("rejected_end", "terminate", ports(flowIn("flow_in")), empty())),
            Arrays.asList(
                flow("start", "trigger_accept", "offer", "flow_in"),
                flow("offer", "accepted", "accepted_condition", "flow_in"),
                flow("offer", "rejected", "rejected_end", "flow_in"),
                logic("offer", "accepted_logic", "accepted_condition", "logic_in"),
                flow("accepted_condition", "flow_true", "slime_task", "flow_in"),
                flow("accepted_condition", "flow_false", "rejected_end", "flow_in"),
                flow("slime_task", "complete", "reward", "flow_in"),
                flow("reward", "flow_out", "epilogue_transfer", "flow_in")));
    }

    private static CanonicalGraphResource flowJudgmentStory() {
        return story(
            "flow_judgment_story",
            Arrays.asList(
                start(),
                node("gate", "action", ports(flowIn("flow_in"), flowOut("flow_out", 1)), empty()),
                node(
                    "judgment",
                    "flow_judgment",
                    ports(flowIn("flow_in"), flowOut("flow_out", 1), logicOut("executed", 2)),
                    empty()),
                node("end", "terminate", ports(flowIn("flow_in")), empty()),
                node(
                    "published",
                    "logic_output",
                    ports(logicIn("logic_in")),
                    props("port_id", "judgment_executed", "display_name", "Judgment Executed"))),
            Arrays.asList(
                flow("start", "trigger_accept", "gate", "flow_in"),
                flow("gate", "flow_out", "judgment", "flow_in"),
                flow("judgment", "flow_out", "end", "flow_in"),
                logic("judgment", "executed", "published", "logic_in")));
    }

    private static CanonicalTaskInstanceSnapshot settledTask(String result, boolean killDone) {
        CanonicalTaskSnapshot runtime = new CanonicalTaskSnapshot(
            "slime_task_resource",
            "task-fingerprint",
            CanonicalTaskStatus.SETTLED,
            Collections.singletonMap("objective", Integer.valueOf(10)),
            Collections.singletonMap("objective", CanonicalTaskObjectiveStatus.COMPLETED),
            Collections.<String, Boolean>emptyMap(),
            bools("kill_done", killDone),
            false,
            result);
        return new CanonicalTaskInstanceSnapshot(
            PLAYER,
            "tavern_story",
            "slime_task",
            "slime_task_resource",
            CanonicalTaskInstanceStatus.SETTLED,
            100L,
            Long.valueOf(200L),
            runtime);
    }

    private static CanonicalGraphNode start() {
        return node("start", "start", ports(flowOut("trigger_accept", 0)), empty());
    }

    private static CanonicalGraphResource story(String id, java.util.List<CanonicalGraphNode> nodes,
        java.util.List<CanonicalGraphConnection> edges) {
        return new CanonicalGraphResource(
            CanonicalGraphResource.CURRENT_SCHEMA_VERSION,
            CanonicalGraphResourceKind.STORY,
            id,
            id,
            new CanonicalGraph(nodes, edges));
    }

    private static CanonicalGraphNode node(String id, String type, java.util.List<CanonicalGraphPort> ports,
        Map<String, JsonElement> properties) {
        return new CanonicalGraphNode(id, type, id, ports, properties);
    }

    private static java.util.List<CanonicalGraphPort> ports(CanonicalGraphPort... ports) {
        return Arrays.asList(ports);
    }

    private static CanonicalGraphPort flowIn(String id) {
        return new CanonicalGraphPort(id, id, CanonicalGraphPortDirection.INPUT, CanonicalGraphInterfaceKind.FLOW, 0);
    }

    private static CanonicalGraphPort logicIn(String id) {
        return new CanonicalGraphPort(id, id, CanonicalGraphPortDirection.INPUT, CanonicalGraphInterfaceKind.LOGIC, 1);
    }

    private static CanonicalGraphPort flowOut(String id, int order) {
        return new CanonicalGraphPort(
            id,
            id,
            CanonicalGraphPortDirection.OUTPUT,
            CanonicalGraphInterfaceKind.FLOW,
            order);
    }

    private static CanonicalGraphPort logicOut(String id) {
        return logicOut(id, 2);
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

    private static Map<String, JsonElement> props(Object... values) {
        LinkedHashMap<String, JsonElement> result = new LinkedHashMap<String, JsonElement>();
        JsonParser parser = new JsonParser();
        for (int i = 0; i < values.length; i += 2) {
            Object value = values[i + 1];
            String json = value instanceof Number ? value.toString() : "\"" + value + "\"";
            result.put((String) values[i], parser.parse(json));
        }
        return result;
    }

    private static Map<String, JsonElement> empty() {
        return Collections.emptyMap();
    }

    private static Map<String, Boolean> bools(String key, boolean value) {
        Map<String, Boolean> result = new HashMap<String, Boolean>();
        result.put(key, Boolean.valueOf(value));
        return result;
    }

    private static void expectFailure(String code, Runnable action) {
        try {
            action.run();
        } catch (CanonicalGraphResourceException expected) {
            check(code.equals(expected.getCode()), "Expected " + code + " but got " + expected.getCode());
            return;
        }
        throw new AssertionError("Expected failure " + code);
    }

    private static void check(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }
}
