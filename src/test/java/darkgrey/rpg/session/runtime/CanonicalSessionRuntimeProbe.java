package darkgrey.rpg.session.runtime;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

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

/** Executable acceptance matrix for the first canonical Session runtime slice. */
public final class CanonicalSessionRuntimeProbe {

    private CanonicalSessionRuntimeProbe() {}

    public static void main(String[] args) {
        verifyLinearStartLineEnd();
        verifyExactStartAndChoiceHistory();
        verifyLogicActivationAndChoiceOutputs();
        verifySnapshotDetachAndRestore();
        verifyFailures();
        System.out.println("CANONICAL_SESSION_RUNTIME_PROBE=PASS");
    }

    private static void verifyLogicActivationAndChoiceOutputs() {
        CanonicalGraphResource condition = session(
            "activation",
            new CanonicalGraph(
                Arrays.asList(
                    node("start", "start", startPorts(), empty()),
                    node(
                        "condition",
                        "condition",
                        ports(in("flow_in"), logicIn("logic_in"), out("flow_true"), out("flow_false")),
                        empty()),
                    node("yes", "end", ports(in("flow_in")), props("port_id", "yes", "display_name", "Yes")),
                    node("no", "end", ports(in("flow_in")), props("port_id", "no", "display_name", "No"))),
                Arrays.asList(
                    edge("start", "flow_out", "condition", "flow_in"),
                    logicEdge("start", "logic_out", "condition", "logic_in"),
                    edge("condition", "flow_true", "yes", "flow_in"),
                    edge("condition", "flow_false", "no", "flow_in"))));
        require(
            "yes".equals(
                CanonicalSessionRuntime.start(condition, true)
                    .getFinalEndPortId()),
            "true activation chose false branch");
        require(
            "no".equals(
                CanonicalSessionRuntime.start(condition, false)
                    .getFinalEndPortId()),
            "false activation chose true branch");

        CanonicalGraphResource choice = session(
            "logic-choice",
            new CanonicalGraph(
                Arrays.asList(
                    node("start", "start", startPorts(), empty()),
                    node(
                        "choice",
                        "choice",
                        ports(
                            in("flow_in"),
                            out("flow_accept"),
                            out("flow_decline"),
                            logicOut("accept"),
                            logicOut("decline")),
                        choiceProps()),
                    node("or", "or", ports(logicIn("left"), logicIn("right"), logicOut("logic_out")), empty()),
                    node("and", "and", ports(logicIn("left"), logicIn("right"), logicOut("logic_out")), empty()),
                    node("not", "not", ports(logicIn("logic_in"), logicOut("logic_out")), empty()),
                    node(
                        "published",
                        "logic_output",
                        ports(logicIn("logic_in")),
                        props("port_id", "picked", "display_name", "Picked")),
                    node(
                        "negated",
                        "logic_output",
                        ports(logicIn("logic_in")),
                        props("port_id", "negated", "display_name", "Negated")),
                    node("end", "end", ports(in("flow_in")), props("port_id", "done", "display_name", "Done"))),
                Arrays.asList(
                    edge("start", "flow_out", "choice", "flow_in"),
                    edge("choice", "flow_accept", "end", "flow_in"),
                    edge("choice", "flow_decline", "end", "flow_in"),
                    logicEdge("choice", "accept", "or", "left"),
                    logicEdge("choice", "decline", "or", "right"),
                    logicEdge("start", "logic_out", "and", "left"),
                    logicEdge("choice", "accept", "and", "right"),
                    logicEdge("and", "logic_out", "not", "logic_in"),
                    logicEdge("or", "logic_out", "published", "logic_in"),
                    logicEdge("not", "logic_out", "negated", "logic_in"))));
        CanonicalSessionRuntime runtime = CanonicalSessionRuntime.start(choice);
        require(
            Boolean.FALSE.equals(
                runtime.getPublicLogicOutputs()
                    .get("picked")),
            "unselected choice leaked true state");
        require(
            Boolean.FALSE.equals(
                runtime.getInternalLogicValues()
                    .get("and.logic_out")),
            "unconnected And input was not false");
        runtime.choose("accept");
        require(
            Boolean.TRUE.equals(
                runtime.getInternalLogicValues()
                    .get("choice.accept")),
            "selected choice state missing");
        require(
            Boolean.FALSE.equals(
                runtime.getInternalLogicValues()
                    .get("choice.decline")),
            "unselected choice state changed");
        require(
            Boolean.TRUE.equals(
                runtime.getPublicLogicOutputs()
                    .get("picked")),
            "public Logic output did not publish Or state");
        require(
            Boolean.TRUE.equals(
                runtime.getPublicLogicOutputs()
                    .get("negated")),
            "Not did not invert And false");
        CanonicalSessionRuntime activated = CanonicalSessionRuntime.start(choice, true);
        activated.choose("accept");
        require(
            Boolean.TRUE.equals(
                activated.getInternalLogicValues()
                    .get("and.logic_out")),
            "And did not combine two true inputs");
        require(
            Boolean.FALSE.equals(
                activated.getPublicLogicOutputs()
                    .get("negated")),
            "Not did not invert And true");
    }

    private static void verifyLinearStartLineEnd() {
        CanonicalGraphResource resource = session(
            node("start", "start", startPorts(), empty()),
            node(
                "line",
                "line",
                ports(in("flow_in"), out("flow_out")),
                props("speaker_actor_id", "actor_guard", "text", "Halt.")),
            node("end", "end", ports(in("flow_in")), props("port_id", "done", "display_name", "Done")),
            edge("start", "flow_out", "line", "flow_in"),
            edge("line", "flow_out", "end", "flow_in"));
        CanonicalSessionRuntime runtime = CanonicalSessionRuntime.start(resource);
        require(
            runtime.getCurrentStep()
                .getKind() == CanonicalSessionStep.Kind.LINE,
            "linear line was not paused");
        require(
            "actor_guard".equals(
                runtime.getCurrentStep()
                    .getSpeakerActorId()),
            "line speaker changed");
        runtime.continueLine();
        require(runtime.getStatus() == CanonicalSessionStatus.COMPLETED, "linear session did not complete");
        require("done".equals(runtime.getFinalEndPortId()), "final end port was not captured");
    }

    private static void verifyExactStartAndChoiceHistory() {
        CanonicalGraphResource resource = session(
            node("start", "start", startPorts(), empty()),
            node(
                "line_a",
                "line",
                ports(in("flow_in"), out("flow_out")),
                props("speaker_actor_id", "a", "text", "First")),
            node(
                "choice",
                "choice",
                ports(in("flow_in"), out("flow_accept"), out("flow_decline"), logicOut("accept"), logicOut("decline")),
                choiceProps()),
            node(
                "line_b",
                "line",
                ports(in("flow_in"), out("flow_out")),
                props("speaker_actor_id", "b", "text", "Accepted")),
            node("end_a", "end", ports(in("flow_in")), props("port_id", "accepted", "display_name", "Accepted")),
            node("end_b", "end", ports(in("flow_in")), props("port_id", "declined", "display_name", "Declined")),
            edge("start", "flow_out", "choice", "flow_in"),
            edge("line_a", "flow_out", "choice", "flow_in"),
            edge("choice", "flow_accept", "line_b", "flow_in"),
            edge("choice", "flow_decline", "end_b", "flow_in"),
            edge("line_b", "flow_out", "end_a", "flow_in"));
        CanonicalSessionRuntime runtime = CanonicalSessionRuntime.start(resource);
        require(
            runtime.getCurrentStep()
                .getKind() == CanonicalSessionStep.Kind.CHOICE,
            "choice was not paused");
        require(
            runtime.getCurrentStep()
                .getOptions()
                .size() == 2,
            "choice options changed");
        runtime.choose("accept");
        require(runtime.getStatus() == CanonicalSessionStatus.ACTIVE, "choice branch should pause at line");
        require(
            runtime.getSelectedOptionIds()
                .equals(Collections.singletonList("accept")),
            "choice history changed");
        runtime.continueLine();
        require(runtime.getStatus() == CanonicalSessionStatus.COMPLETED, "choice branch did not complete");
        require("accepted".equals(runtime.getFinalEndPortId()), "choice end port changed");

        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionRuntime.start(
                    session(
                        node("start", "start", ports(out("flow_out")), empty()),
                        node("end", "end", ports(in("flow_in")), props("port_id", "done", "display_name", "Done")),
                        edge("start", "flow_out", "end", "flow_in")));
            }
        }, "session.start.port.missing");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionRuntime.start(
                    session(
                        node("start", "start", ports(out("flow_out"), out("logic_out")), empty()),
                        node("end", "end", ports(in("flow_in")), props("port_id", "done", "display_name", "Done")),
                        edge("start", "flow_out", "end", "flow_in")));
            }
        }, "session.start.port.kind");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionRuntime.start(
                    session(
                        node("start", "start", ports(out("flow_out"), logicOut("logic_out"), out("extra")), empty()),
                        node("end", "end", ports(in("flow_in")), props("port_id", "done", "display_name", "Done")),
                        edge("start", "flow_out", "end", "flow_in")));
            }
        }, "session.start.port.extra");
    }

    private static void verifySnapshotDetachAndRestore() {
        CanonicalGraphResource resource = session(
            node("start", "start", startPorts(), empty()),
            node(
                "choice",
                "choice",
                ports(in("flow_in"), out("flow_accept"), out("flow_decline"), logicOut("accept"), logicOut("decline")),
                choiceProps()),
            node("end", "end", ports(in("flow_in")), props("port_id", "yes_end", "display_name", "Yes")),
            edge("start", "flow_out", "choice", "flow_in"),
            edge("choice", "flow_accept", "end", "flow_in"),
            edge("choice", "flow_decline", "end", "flow_in"));
        CanonicalSessionRuntime runtime = CanonicalSessionRuntime.start(resource);
        CanonicalSessionSnapshot snapshot = runtime.snapshot();
        require(
            snapshot.getInternalLogicValues()
                .containsKey("start.logic_out"),
            "internal logic map must contain Start output");
        require(
            snapshot.getPublicLogicOutputs()
                .isEmpty(),
            "public logic map must be empty");
        expectUnsupported(new Runnable() {

            @Override
            public void run() {
                snapshot.getSelectedOptionIds()
                    .add("no");
            }
        });
        runtime.choose("accept");
        CanonicalSessionRuntime restored = CanonicalSessionRuntime.restore(resource, snapshot);
        require(restored.getStatus() == CanonicalSessionStatus.ACTIVE, "restored status changed");
        restored.choose("accept");
        require(restored.getStatus() == CanonicalSessionStatus.COMPLETED, "restored session did not complete");
        CanonicalSessionSnapshot chosen = runtime.snapshot();
        List<String> contradictoryHistory = Collections.singletonList("decline");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionRuntime.restore(
                    resource,
                    new CanonicalSessionSnapshot(
                        chosen.getSessionResourceId(),
                        chosen.getCurrentNodeId(),
                        chosen.getStatus(),
                        contradictoryHistory,
                        chosen.getInternalLogicValues(),
                        chosen.getFinalEndPortId(),
                        chosen.getPublicLogicOutputs(),
                        chosen.getActivationLogic(),
                        chosen.getLatestChoiceSelections(),
                        Collections.singletonList("choice")));
            }
        }, "session.snapshot.history");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionRuntime.restore(sessionWithId("different", resource.getGraph()), snapshot);
            }
        }, "session.snapshot.resource_mismatch");

        Map<String, Boolean> unknownInternal = new LinkedHashMap<String, Boolean>(snapshot.getInternalLogicValues());
        unknownInternal.put("ghost.logic_out", Boolean.TRUE);
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionRuntime.restore(
                    resource,
                    new CanonicalSessionSnapshot(
                        snapshot.getSessionResourceId(),
                        snapshot.getCurrentNodeId(),
                        snapshot.getStatus(),
                        snapshot.getSelectedOptionIds(),
                        unknownInternal,
                        snapshot.getFinalEndPortId(),
                        snapshot.getPublicLogicOutputs(),
                        snapshot.getActivationLogic(),
                        snapshot.getLatestChoiceSelections(),
                        snapshot.getSelectedChoiceNodeIds()));
            }
        }, "session.snapshot.logic");

        Map<String, Boolean> stalePublic = new LinkedHashMap<String, Boolean>(snapshot.getPublicLogicOutputs());
        stalePublic.put("ghost", Boolean.TRUE);
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionRuntime.restore(
                    resource,
                    new CanonicalSessionSnapshot(
                        snapshot.getSessionResourceId(),
                        snapshot.getCurrentNodeId(),
                        snapshot.getStatus(),
                        snapshot.getSelectedOptionIds(),
                        snapshot.getInternalLogicValues(),
                        snapshot.getFinalEndPortId(),
                        stalePublic,
                        snapshot.getActivationLogic(),
                        snapshot.getLatestChoiceSelections(),
                        snapshot.getSelectedChoiceNodeIds()));
            }
        }, "session.snapshot.logic");

        Map<String, Boolean> incompleteInternal = new LinkedHashMap<String, Boolean>(snapshot.getInternalLogicValues());
        incompleteInternal.remove("start.logic_out");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionRuntime.restore(
                    resource,
                    new CanonicalSessionSnapshot(
                        snapshot.getSessionResourceId(),
                        snapshot.getCurrentNodeId(),
                        snapshot.getStatus(),
                        snapshot.getSelectedOptionIds(),
                        incompleteInternal,
                        snapshot.getFinalEndPortId(),
                        snapshot.getPublicLogicOutputs(),
                        snapshot.getActivationLogic(),
                        snapshot.getLatestChoiceSelections(),
                        snapshot.getSelectedChoiceNodeIds()));
            }
        }, "session.snapshot.logic");
    }

    private static void verifyFailures() {
        CanonicalGraphResource unknown = session(
            node("start", "start", startPorts(), empty()),
            node(
                "choice",
                "choice",
                ports(in("flow_in"), out("flow_yes"), logicOut("yes")),
                rawProps("prompt", "\"Pick\"", "options", "[{}]")),
            edge("start", "flow_out", "choice", "flow_in"));
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionRuntime.start(unknown);
            }
        }, "session.choice.option.fields");

        CanonicalGraphResource validChoice = session(
            node("start", "start", startPorts(), empty()),
            node(
                "choice",
                "choice",
                ports(in("flow_in"), out("flow_yes"), logicOut("yes")),
                rawProps(
                    "prompt",
                    "\"Pick\"",
                    "options",
                    "[{\"option_id\":\"yes\",\"display_text\":\"Yes\",\"flow_port_id\":\"flow_yes\"}]")),
            node("end", "end", ports(in("flow_in")), props("port_id", "yes", "display_name", "Yes")),
            edge("start", "flow_out", "choice", "flow_in"),
            edge("choice", "flow_yes", "end", "flow_in"));
        final CanonicalSessionRuntime validChoiceRuntime = CanonicalSessionRuntime.start(validChoice);
        expectFailure(new Runnable() {

            @Override
            public void run() {
                validChoiceRuntime.choose("unknown");
            }
        }, "session.choice.unknown");
        require(
            validChoiceRuntime.getStatus() == CanonicalSessionStatus.FAILED,
            "failed choice did not enter FAILED state");

        CanonicalGraphResource duplicateFlowPort = session(
            node("start", "start", startPorts(), empty()),
            node(
                "choice",
                "choice",
                ports(in("flow_in"), out("flow_yes"), logicOut("a"), logicOut("b")),
                rawProps(
                    "prompt",
                    "\"Pick\"",
                    "options",
                    "[{\"option_id\":\"a\",\"display_text\":\"A\",\"flow_port_id\":\"flow_yes\"},"
                        + "{\"option_id\":\"b\",\"display_text\":\"B\",\"flow_port_id\":\"flow_yes\"}]")),
            node("end", "end", ports(in("flow_in")), props("port_id", "done", "display_name", "Done")),
            edge("start", "flow_out", "choice", "flow_in"),
            edge("choice", "flow_yes", "end", "flow_in"));
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionRuntime.start(duplicateFlowPort);
            }
        }, "session.choice.option.duplicate_port");

        CanonicalGraphResource extraChoiceOutput = session(
            node("start", "start", startPorts(), empty()),
            node(
                "choice",
                "choice",
                ports(in("flow_in"), out("flow_yes"), out("unused"), logicOut("yes")),
                rawProps(
                    "prompt",
                    "\"Pick\"",
                    "options",
                    "[{\"option_id\":\"yes\",\"display_text\":\"Yes\",\"flow_port_id\":\"flow_yes\"}]")),
            node("end", "end", ports(in("flow_in")), props("port_id", "done", "display_name", "Done")),
            edge("start", "flow_out", "choice", "flow_in"),
            edge("choice", "flow_yes", "end", "flow_in"),
            edge("choice", "unused", "end", "flow_in"));
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionRuntime.start(extraChoiceOutput);
            }
        }, "session.choice.option.mapping");

        CanonicalGraphResource missingLogicOutput = session(
            node("start", "start", startPorts(), empty()),
            node(
                "choice",
                "choice",
                ports(in("flow_in"), out("flow_yes")),
                rawProps(
                    "prompt",
                    "\"Pick\"",
                    "options",
                    "[{\"option_id\":\"yes\",\"display_text\":\"Yes\",\"flow_port_id\":\"flow_yes\"}]")),
            node("end", "end", ports(in("flow_in")), props("port_id", "done", "display_name", "Done")),
            edge("start", "flow_out", "choice", "flow_in"),
            edge("choice", "flow_yes", "end", "flow_in"));
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionRuntime.start(missingLogicOutput);
            }
        }, "session.choice.option.mapping");

        CanonicalGraphResource extraLogicOutput = session(
            node("start", "start", startPorts(), empty()),
            node(
                "choice",
                "choice",
                ports(in("flow_in"), out("flow_yes"), logicOut("yes"), logicOut("extra")),
                rawProps(
                    "prompt",
                    "\"Pick\"",
                    "options",
                    "[{\"option_id\":\"yes\",\"display_text\":\"Yes\",\"flow_port_id\":\"flow_yes\"}]")),
            node("end", "end", ports(in("flow_in")), props("port_id", "done", "display_name", "Done")),
            edge("start", "flow_out", "choice", "flow_in"),
            edge("choice", "flow_yes", "end", "flow_in"));
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionRuntime.start(extraLogicOutput);
            }
        }, "session.choice.option.mapping");

        CanonicalGraphResource wrongLogicKind = session(
            node("start", "start", startPorts(), empty()),
            node(
                "choice",
                "choice",
                ports(in("flow_in"), out("flow_yes"), out("yes")),
                rawProps(
                    "prompt",
                    "\"Pick\"",
                    "options",
                    "[{\"option_id\":\"yes\",\"display_text\":\"Yes\",\"flow_port_id\":\"flow_yes\"}]")),
            node("end", "end", ports(in("flow_in")), props("port_id", "done", "display_name", "Done")),
            edge("start", "flow_out", "choice", "flow_in"),
            edge("choice", "flow_yes", "end", "flow_in"));
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionRuntime.start(wrongLogicKind);
            }
        }, "session.choice.option.logic.kind");

        CanonicalGraphResource malformedLine = session(
            node("start", "start", startPorts(), empty()),
            node("line", "line", ports(in("flow_in"), out("flow_out")), props("text", "Missing speaker")),
            edge("start", "flow_out", "line", "flow_in"));
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionRuntime.start(malformedLine);
            }
        }, "session.line.property.required");

        CanonicalGraphResource unsupported = session(
            node("start", "start", startPorts(), empty()),
            node("mystery", "mystery", Collections.<CanonicalGraphPort>emptyList(), empty()),
            edge("start", "flow_out", "mystery", "flow_in"));
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionRuntime.start(unsupported);
            }
        }, "session.node.unsupported");

        CanonicalGraphResource cycle = session(
            node("start", "start", startPorts(), empty()),
            node("jump_a", "legacy_jump", ports(in("flow_in"), out("flow_out")), empty()),
            node("jump_b", "legacy_jump", ports(in("flow_in"), out("flow_out")), empty()),
            edge("start", "flow_out", "jump_a", "flow_in"),
            edge("jump_a", "flow_out", "jump_b", "flow_in"),
            edge("jump_b", "flow_out", "jump_a", "flow_in"));
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionRuntime.start(cycle);
            }
        }, "session.runtime.cycle_guard");

        CanonicalGraphResource unconnected = session(node("start", "start", startPorts(), empty()));
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionRuntime.start(unconnected);
            }
        }, "session.start.unconnected");

        CanonicalGraphResource mismatched = session(
            node("start", "start", startPorts(), empty()),
            node(
                "line",
                "line",
                ports(in("flow_in"), out("flow_out")),
                props("speaker_actor_id", "actor", "text", "Text")),
            edge("start", "flow_out", "line", "missing"));
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionRuntime.start(mismatched);
            }
        }, "session.flow.edge.port");

        CanonicalGraphResource ambiguousJump = session(
            node("start", "start", startPorts(), empty()),
            node("jump", "legacy_jump", ports(in("flow_in"), out("one"), out("two")), empty()),
            node("end", "end", ports(in("flow_in")), props("port_id", "done", "display_name", "Done")),
            edge("start", "flow_out", "jump", "flow_in"),
            edge("jump", "one", "end", "flow_in"),
            edge("jump", "two", "end", "flow_in"));
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionRuntime.start(ambiguousJump);
            }
        }, "session.legacy_jump.ambiguous");

        CanonicalGraphResource multipleLogicSources = session(
            "multiple-sources",
            new CanonicalGraph(
                Arrays.asList(
                    node("start", "start", startPorts(), empty()),
                    node(
                        "choice",
                        "choice",
                        ports(in("flow_in"), out("flow_yes"), logicOut("yes")),
                        rawProps(
                            "prompt",
                            "\"Pick\"",
                            "options",
                            "[{\"option_id\":\"yes\",\"display_text\":\"Yes\",\"flow_port_id\":\"flow_yes\"}]")),
                    node("and", "and", ports(logicIn("left"), logicIn("right"), logicOut("logic_out")), empty()),
                    node("end", "end", ports(in("flow_in")), props("port_id", "done", "display_name", "Done"))),
                Arrays.asList(
                    edge("start", "flow_out", "choice", "flow_in"),
                    edge("choice", "flow_yes", "end", "flow_in"),
                    logicEdge("start", "logic_out", "and", "left"),
                    logicEdge("choice", "yes", "and", "left"))));
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionRuntime.start(multipleLogicSources);
            }
        }, "session.logic.input.multiple_sources");

        CanonicalGraphResource logicCycle = session(
            "logic-cycle",
            new CanonicalGraph(
                Arrays.asList(
                    node("start", "start", startPorts(), empty()),
                    node("one", "not", ports(logicIn("logic_in"), logicOut("logic_out")), empty()),
                    node("two", "not", ports(logicIn("logic_in"), logicOut("logic_out")), empty()),
                    node("end", "end", ports(in("flow_in")), props("port_id", "done", "display_name", "Done"))),
                Arrays.asList(
                    edge("start", "flow_out", "end", "flow_in"),
                    logicEdge("one", "logic_out", "two", "logic_in"),
                    logicEdge("two", "logic_out", "one", "logic_in"))));
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionRuntime.start(logicCycle);
            }
        }, "session.logic.cycle");

        CanonicalGraphResource wrongLogicEdge = session(
            "wrong-logic-edge",
            new CanonicalGraph(
                Arrays.asList(
                    node("start", "start", startPorts(), empty()),
                    node("end", "end", ports(in("flow_in")), props("port_id", "done", "display_name", "Done"))),
                Arrays.asList(
                    edge("start", "flow_out", "end", "flow_in"),
                    logicEdge("start", "flow_out", "end", "flow_in"))));
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionRuntime.start(wrongLogicEdge);
            }
        }, "session.logic.edge.kind");

        CanonicalGraphResource wrongLogicDirection = session(
            "wrong-logic-direction",
            new CanonicalGraph(
                Arrays.asList(
                    node("start", "start", startPorts(), empty()),
                    node("not", "not", ports(logicIn("logic_in"), logicOut("logic_out")), empty()),
                    node(
                        "published",
                        "logic_output",
                        ports(logicIn("logic_in")),
                        props("port_id", "value", "display_name", "Value")),
                    node("end", "end", ports(in("flow_in")), props("port_id", "done", "display_name", "Done"))),
                Arrays.asList(
                    edge("start", "flow_out", "end", "flow_in"),
                    logicEdge("not", "logic_in", "published", "logic_in"))));
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionRuntime.start(wrongLogicDirection);
            }
        }, "session.logic.edge.direction");

        CanonicalGraphResource insufficientAndInputs = session(
            "insufficient-and",
            new CanonicalGraph(
                Arrays.asList(
                    node("start", "start", startPorts(), empty()),
                    node("and", "and", ports(logicIn("only"), logicOut("logic_out")), empty()),
                    node("end", "end", ports(in("flow_in")), props("port_id", "done", "display_name", "Done"))),
                Collections.singletonList(edge("start", "flow_out", "end", "flow_in"))));
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionRuntime.start(insufficientAndInputs);
            }
        }, "session.logic.input.cardinality");

        CanonicalGraphResource multipleFlowTargets = session(
            "multiple-flow-targets",
            new CanonicalGraph(
                Arrays.asList(
                    node("start", "start", startPorts(), empty()),
                    node("one", "end", ports(in("flow_in")), props("port_id", "one", "display_name", "One")),
                    node("two", "end", ports(in("flow_in")), props("port_id", "two", "display_name", "Two"))),
                Arrays
                    .asList(edge("start", "flow_out", "one", "flow_in"), edge("start", "flow_out", "two", "flow_in"))));
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionRuntime.start(multipleFlowTargets);
            }
        }, "session.flow.output.multiple_targets");

        CanonicalGraphResource duplicatePublicId = session(
            "duplicate-public-id",
            new CanonicalGraph(
                Arrays.asList(
                    node("start", "start", startPorts(), empty()),
                    node("end", "end", ports(in("flow_in")), props("port_id", "result", "display_name", "Done")),
                    node(
                        "published",
                        "logic_output",
                        ports(logicIn("logic_in")),
                        props("port_id", "result", "display_name", "Known"))),
                Collections.singletonList(edge("start", "flow_out", "end", "flow_in"))));
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionRuntime.start(duplicatePublicId);
            }
        }, "session.public_port.id.duplicate");

        CanonicalGraphResource duplicatePublicName = session(
            "duplicate-public-name",
            new CanonicalGraph(
                Arrays.asList(
                    node("start", "start", startPorts(), empty()),
                    node("end", "end", ports(in("flow_in")), props("port_id", "done", "display_name", "Result")),
                    node(
                        "published",
                        "logic_output",
                        ports(logicIn("logic_in")),
                        props("port_id", "known", "display_name", "Result"))),
                Collections.singletonList(edge("start", "flow_out", "end", "flow_in"))));
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionRuntime.start(duplicatePublicName);
            }
        }, "session.public_port.display_name.duplicate");

        CanonicalGraphResource reservedPublicId = session(
            "reserved-public-id",
            new CanonicalGraph(
                Arrays.asList(
                    node("start", "start", startPorts(), empty()),
                    node("end", "end", ports(in("flow_in")), props("port_id", "logic_in", "display_name", "Done"))),
                Collections.singletonList(edge("start", "flow_out", "end", "flow_in"))));
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionRuntime.start(reservedPublicId);
            }
        }, "session.public_port.id.reserved");
    }

    private static CanonicalGraphResource session(CanonicalGraphNode first, CanonicalGraphNode second,
        CanonicalGraphNode... rest) {
        List<CanonicalGraphNode> nodes = new ArrayList<CanonicalGraphNode>();
        nodes.add(first);
        nodes.add(second);
        nodes.addAll(Arrays.asList(rest));
        return session("session_probe", new CanonicalGraph(nodes, Collections.<CanonicalGraphConnection>emptyList()));
    }

    private static CanonicalGraphResource session(String id, CanonicalGraph graph) {
        return new CanonicalGraphResource(1, CanonicalGraphResourceKind.SESSION, id, "Probe", graph);
    }

    private static CanonicalGraphResource sessionWithId(String id, CanonicalGraph graph) {
        return session(id, graph);
    }

    private static CanonicalGraphResource session(CanonicalGraphNode first, CanonicalGraphNode second,
        CanonicalGraphNode third, CanonicalGraphNode fourth, CanonicalGraphNode fifth, CanonicalGraphNode sixth,
        CanonicalGraphConnection... edges) {
        return session(
            "session_probe",
            new CanonicalGraph(Arrays.asList(first, second, third, fourth, fifth, sixth), Arrays.asList(edges)));
    }

    private static CanonicalGraphResource session(CanonicalGraphNode first, CanonicalGraphNode second,
        CanonicalGraphNode third, CanonicalGraphConnection... edges) {
        return session("session_probe", new CanonicalGraph(Arrays.asList(first, second, third), Arrays.asList(edges)));
    }

    private static CanonicalGraphResource session(CanonicalGraphNode first, CanonicalGraphNode second,
        CanonicalGraphConnection... edges) {
        return session("session_probe", new CanonicalGraph(Arrays.asList(first, second), Arrays.asList(edges)));
    }

    private static CanonicalGraphResource session(CanonicalGraphNode first) {
        return session(
            "session_probe",
            new CanonicalGraph(Collections.singletonList(first), Collections.<CanonicalGraphConnection>emptyList()));
    }

    private static CanonicalGraphNode node(String id, String type, List<CanonicalGraphPort> ports,
        Map<String, JsonElement> properties) {
        return new CanonicalGraphNode(id, type, id, ports, properties);
    }

    private static List<CanonicalGraphPort> ports(CanonicalGraphPort... ports) {
        return Arrays.asList(ports);
    }

    private static List<CanonicalGraphPort> startPorts() {
        return ports(out("flow_out"), logicOut("logic_out"));
    }

    private static CanonicalGraphPort in(String id) {
        return new CanonicalGraphPort(id, id, CanonicalGraphPortDirection.INPUT, CanonicalGraphInterfaceKind.FLOW, 0);
    }

    private static CanonicalGraphPort out(String id) {
        return new CanonicalGraphPort(id, id, CanonicalGraphPortDirection.OUTPUT, CanonicalGraphInterfaceKind.FLOW, 0);
    }

    private static CanonicalGraphPort logicIn(String id) {
        return new CanonicalGraphPort(id, id, CanonicalGraphPortDirection.INPUT, CanonicalGraphInterfaceKind.LOGIC, 0);
    }

    private static CanonicalGraphPort logicOut(String id) {
        return new CanonicalGraphPort(id, id, CanonicalGraphPortDirection.OUTPUT, CanonicalGraphInterfaceKind.LOGIC, 0);
    }

    private static CanonicalGraphConnection edge(String from, String fromPort, String to, String toPort) {
        return new CanonicalGraphConnection(from, fromPort, to, toPort, CanonicalGraphInterfaceKind.FLOW);
    }

    private static CanonicalGraphConnection logicEdge(String from, String fromPort, String to, String toPort) {
        return new CanonicalGraphConnection(from, fromPort, to, toPort, CanonicalGraphInterfaceKind.LOGIC);
    }

    private static Map<String, JsonElement> empty() {
        return Collections.emptyMap();
    }

    private static Map<String, JsonElement> props(String... values) {
        Map<String, JsonElement> result = new LinkedHashMap<String, JsonElement>();
        for (int i = 0; i < values.length; i += 2)
            result.put(values[i], new JsonParser().parse("\"" + values[i + 1] + "\""));
        return result;
    }

    private static Map<String, JsonElement> choiceProps() {
        Map<String, JsonElement> result = new LinkedHashMap<String, JsonElement>();
        result.put("prompt", new JsonParser().parse("\"Pick\""));
        result.put(
            "options",
            new JsonParser().parse(
                "[{\"option_id\":\"accept\",\"display_text\":\"Accept\",\"flow_port_id\":\"flow_accept\"},"
                    + "{\"option_id\":\"decline\",\"display_text\":\"Decline\",\"flow_port_id\":\"flow_decline\"}]"));
        return result;
    }

    private static Map<String, JsonElement> rawProps(String... values) {
        Map<String, JsonElement> result = new LinkedHashMap<String, JsonElement>();
        for (int i = 0; i < values.length; i += 2) result.put(values[i], new JsonParser().parse(values[i + 1]));
        return result;
    }

    private static void expectFailure(Runnable action, String code) {
        try {
            action.run();
            throw new AssertionError("Expected " + code);
        } catch (CanonicalGraphResourceException exception) {
            require(code.equals(exception.getCode()), "Expected " + code + ", got " + exception.getCode());
        }
    }

    private static void expectUnsupported(Runnable action) {
        try {
            action.run();
            throw new AssertionError("Expected immutable collection");
        } catch (UnsupportedOperationException expected) {}
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
