package darkgrey.rpg.task.runtime;

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

/** Focused, dependency-free acceptance probe for the pure Task runtime core. */
public final class CanonicalTaskRuntimeProbe {

    public static void main(String[] args) {
        canonical0310TaskSemantics();
        parallelSequentialAndTypes();
        priorityAndUnconnectedFalse();
        activationSettlementAndPostSettlement();
        overflowAndParallelSameType();
        immutableSnapshotRestore();
        malformedFailsClosed();
        System.out.println("TASK_RUNTIME_PROBE_PASS");
    }

    private static void canonical0310TaskSemantics() {
        CanonicalTaskRuntime runtime = CanonicalTaskRuntime.start(canonical0310Resource());
        require(runtime.isActive(), "Task without activate starts Active");
        require(runtime.isObjectiveActive("default"), "Objective without conditions is active by default");
        require(!runtime.isObjectiveActive("gated"), "False condition gates objective");
        require(runtime.accept(CanonicalTaskEvent.killEntity("slime")), "Default objective accepts event");
        require(
            runtime.getProgress()
                .get("default")
                .intValue() == 1,
            "Default objective progress");
        require(!runtime.accept(CanonicalTaskEvent.killEntity("slime")), "Gated objective pauses counting");
        require(
            runtime.getProgress()
                .get("gated")
                .intValue() == 0,
            "Gated objective remains at zero");
        require(runtime.setLogicInput("night", true), "First condition changes");
        require(!runtime.isObjectiveActive("gated"), "AND gate remains false with one input");
        require(runtime.setLogicInput("armed", true), "Second condition changes");
        require(runtime.isObjectiveActive("gated"), "AND gate activates objective");
        require(runtime.accept(CanonicalTaskEvent.killEntity("slime")), "Gated objective counts when enabled");
        require(
            runtime.getProgress()
                .get("gated")
                .intValue() == 1,
            "Gated objective progress");
        CanonicalTaskSnapshot saved = runtime.snapshot();
        runtime = CanonicalTaskRuntime.restore(canonical0310Resource(), saved);
        require(!runtime.setLogicInput("night", true), "Restored first input survives");
        require(!runtime.setLogicInput("armed", true), "Restored second input survives");
        require(runtime.isObjectiveActive("gated"), "Restored gate remains active");
        require(
            runtime.getProgress()
                .get("gated")
                .intValue() == 1,
            "Restored progress survives");
        require(runtime.setLogicInput("armed", false), "Gate can pause dynamically");
        require(!runtime.isObjectiveActive("gated"), "Dynamic false pauses objective");
        require(!runtime.accept(CanonicalTaskEvent.killEntity("slime")), "Paused objective ignores new event");
        require(
            runtime.getProgress()
                .get("gated")
                .intValue() == 1,
            "Paused objective retains progress");
        require(runtime.setLogicInput("armed", true), "Gate resumes");
        require(runtime.accept(CanonicalTaskEvent.killEntity("slime")), "Resumed objective completes");
        require(runtime.isSettled() && "success".equals(runtime.getResultPortId()), "First settlement slot wins");
        require(
            runtime.getPublicLogicOutputs()
                .get("gated_done")
                .booleanValue(),
            "Logic output is updated");
        require(!runtime.accept(CanonicalTaskEvent.killEntity("slime")), "Settled Task ignores events");
    }

    private static CanonicalGraphResource canonical0310Resource() {
        List<CanonicalGraphNode> nodes = Arrays.asList(
            node(
                "night",
                "logic_input",
                ports(out("logic_out", 0)),
                props("port_id", "night", "display_name", "Night")),
            node(
                "armed",
                "logic_input",
                ports(out("logic_out", 0)),
                props("port_id", "armed", "display_name", "Armed")),
            node("default", "objective", ports(out("logic_status", 0)), objective("kill_entity", "slime", null, 1)),
            node(
                "gated",
                "objective",
                ports(in("night_condition", 0), in("armed_condition", 1), out("logic_status", 2)),
                objective("kill_entity", "slime", null, 2)),
            node(
                "published",
                "logic_output",
                ports(in("logic_in", 0)),
                props("port_id", "gated_done", "display_name", "Gated done")),
            node("settle", "settle", ports(in("success", 0), in("fallback", 1)), empty()));
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.TASK,
            "canonical_0310",
            "Canonical 0.3.1.0",
            new CanonicalGraph(
                nodes,
                Arrays.asList(
                    edge("night", "logic_out", "gated", "night_condition"),
                    edge("armed", "logic_out", "gated", "armed_condition"),
                    edge("gated", "logic_status", "published", "logic_in"),
                    edge("gated", "logic_status", "settle", "success"))));
    }

    private static void parallelSequentialAndTypes() {
        CanonicalTaskRuntime runtime = CanonicalTaskRuntime.start(resource("task_main", false));
        require(runtime.isActive(), "Task must start Active");
        require(
            runtime.getLogicValues()
                .get("activate:logic_out")
                .booleanValue(),
            "activate output must be true");
        require(
            runtime.getObjectiveStatuses()
                .get("kill") == CanonicalTaskObjectiveStatus.ACTIVE,
            "kill must latch");
        require(
            runtime.getObjectiveStatuses()
                .get("collect") == CanonicalTaskObjectiveStatus.ACTIVE,
            "collect must latch in parallel");
        require(runtime.accept(CanonicalTaskEvent.killEntity("slime")), "kill event must update");
        require(
            runtime.getProgress()
                .get("kill")
                .intValue() == 1,
            "kill progress");
        require(
            runtime.getObjectiveStatuses()
                .get("interact") == CanonicalTaskObjectiveStatus.INACTIVE,
            "interact must remain disabled before kill completes");
        require(
            runtime.getProgress()
                .get("interact")
                .intValue() == 0,
            "new objective must not consume event");
        require(
            runtime.accept(CanonicalTaskEvent.collectItem("iron", metadata("grade", "raw"), 2)),
            "collect event must update");
        require(runtime.accept(CanonicalTaskEvent.killEntity("slime")), "second kill must update");
        require(
            runtime.getObjectiveStatuses()
                .get("interact") == CanonicalTaskObjectiveStatus.ACTIVE,
            "interact must enable after kill completes");
        require(
            runtime.getProgress()
                .get("interact")
                .intValue() == 0,
            "new objective must not consume event");
        require(runtime.accept(CanonicalTaskEvent.interactActor("tavern_boss")), "interact event must update");
        require(
            runtime.isSettled() && "success".equals(runtime.getResultPortId()),
            "Task must settle once all objectives complete");
        require(!runtime.getActivationLogic(), "activate output must be false after settlement");
        require(
            runtime.getPublicLogicOutputs()
                .get("all_done")
                .booleanValue(),
            "final public Logic retained");
    }

    private static void priorityAndUnconnectedFalse() {
        CanonicalTaskRuntime runtime = CanonicalTaskRuntime.start(resource("task_priority", true));
        require(
            !runtime.getPublicLogicOutputs()
                .get("never")
                .booleanValue(),
            "unconnected public Logic must be false");
        require(runtime.accept(CanonicalTaskEvent.killEntity("slime")), "priority event must update");
        require(runtime.isSettled() && "first".equals(runtime.getResultPortId()), "first true slot wins");
        Map<String, Integer> before = runtime.getProgress();
        require(!runtime.accept(CanonicalTaskEvent.killEntity("slime")), "settled Task must ignore events");
        require(before.equals(runtime.getProgress()), "settled event must not change progress");
    }

    private static void activationSettlementAndPostSettlement() {
        CanonicalGraphResource resource = new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.TASK,
            "activation_settlement",
            "Probe",
            new CanonicalGraph(
                Arrays.asList(
                    node("activate", "activate", ports(out("logic_out", 0)), empty()),
                    node("settle", "settle", ports(in("done", 0)), empty())),
                Collections.singletonList(edge("activate", "logic_out", "settle", "done"))));
        CanonicalTaskRuntime runtime = CanonicalTaskRuntime.start(resource);
        require(
            runtime.isSettled() && "done".equals(runtime.getResultPortId()),
            "activate-to-settle must initialize deterministically");
        require(!runtime.accept(CanonicalTaskEvent.interactActor("nobody")), "settled event must be ignored");
    }

    private static void overflowAndParallelSameType() {
        CanonicalGraphResource resource = resourceWithKillRequirements("overflow", Integer.MAX_VALUE);
        CanonicalTaskRuntime runtime = CanonicalTaskRuntime.start(resource);
        require(runtime.accept(CanonicalTaskEvent.killEntity("slime")), "overflow first event");
        require(
            runtime.getProgress()
                .get("kill")
                .intValue() == 1,
            "overflow first progress");
        require(runtime.accept(CanonicalTaskEvent.killEntity("slime", Integer.MAX_VALUE)), "overflow event");
        require(
            runtime.getProgress()
                .get("kill")
                .intValue() == Integer.MAX_VALUE,
            "progress must cap without overflow");
        require(
            runtime.getProgress()
                .get("collect")
                .intValue() == 0,
            "unrelated objective must not update");
        CanonicalTaskRuntime parallel = CanonicalTaskRuntime.start(parallelKillResource());
        parallel.accept(CanonicalTaskEvent.killEntity("slime"));
        require(
            parallel.getProgress()
                .get("first_kill")
                .intValue() == 1
                && parallel.getProgress()
                    .get("second_kill")
                    .intValue() == 1,
            "parallel same-type objectives must both update");
    }

    private static CanonicalGraphResource parallelKillResource() {
        Map<String, JsonElement> first = objective("kill_entity", "slime", null, 2);
        Map<String, JsonElement> second = objective("kill_entity", "slime", null, 2);
        List<CanonicalGraphNode> nodes = Arrays.asList(
            node("activate", "activate", ports(out("logic_out", 0)), empty()),
            node("first_kill", "objective", ports(in("logic_enable", 0), out("logic_status", 1)), first),
            node("second_kill", "objective", ports(in("logic_enable", 0), out("logic_status", 1)), second),
            node("settle", "settle", ports(in("done", 0)), empty()));
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.TASK,
            "parallel_kill",
            "Probe",
            new CanonicalGraph(
                nodes,
                Arrays.asList(
                    edge("activate", "logic_out", "first_kill", "logic_enable"),
                    edge("activate", "logic_out", "second_kill", "logic_enable"))));
    }

    private static void immutableSnapshotRestore() {
        CanonicalGraphResource resource = resource("task_snapshot", false);
        CanonicalTaskRuntime runtime = CanonicalTaskRuntime.start(resource);
        runtime.accept(CanonicalTaskEvent.killEntity("slime"));
        CanonicalTaskSnapshot snapshot = runtime.snapshot();
        expectUnsupported(new Runnable() {

            @Override
            public void run() {
                snapshot.getProgress()
                    .put("kill", Integer.valueOf(9));
            }
        });
        CanonicalTaskRuntime restored = CanonicalTaskRuntime.restore(resource, snapshot);
        require(
            restored.getProgress()
                .equals(runtime.getProgress()),
            "restore progress");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalTaskRuntime.restore(resource("drift", false), snapshot);
            }
        }, "task.snapshot.resource");
        Map<String, Integer> badProgress = new LinkedHashMap<String, Integer>(snapshot.getProgress());
        badProgress.put("kill", Integer.valueOf(2));
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalTaskRuntime.restore(
                    resource,
                    new CanonicalTaskSnapshot(
                        resource.getId(),
                        snapshot.getResourceFingerprint(),
                        CanonicalTaskStatus.ACTIVE,
                        badProgress,
                        snapshot.getObjectiveStatuses(),
                        snapshot.getLogicValues(),
                        snapshot.getPublicLogicOutputs(),
                        true,
                        null));
            }
        }, "task.snapshot.progress");
        CanonicalGraphResource settledResource = resource("settled_snapshot", true);
        CanonicalTaskRuntime settledRuntime = CanonicalTaskRuntime.start(settledResource);
        settledRuntime.accept(CanonicalTaskEvent.killEntity("slime"));
        CanonicalTaskSnapshot settled = settledRuntime.snapshot();
        Map<String, Boolean> fakePublic = new LinkedHashMap<String, Boolean>(settled.getPublicLogicOutputs());
        fakePublic.put("never", Boolean.TRUE);
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalTaskRuntime.restore(
                    settledResource,
                    new CanonicalTaskSnapshot(
                        settled.getResourceId(),
                        settled.getResourceFingerprint(),
                        CanonicalTaskStatus.SETTLED,
                        settled.getProgress(),
                        settled.getObjectiveStatuses(),
                        settled.getLogicValues(),
                        fakePublic,
                        false,
                        settled.getResultPortId()));
            }
        }, "task.snapshot.public_logic");
        Map<String, CanonicalTaskObjectiveStatus> fakeStatuses = new LinkedHashMap<String, CanonicalTaskObjectiveStatus>(
            settled.getObjectiveStatuses());
        fakeStatuses.put("interact", CanonicalTaskObjectiveStatus.INACTIVE);
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalTaskRuntime.restore(
                    settledResource,
                    new CanonicalTaskSnapshot(
                        settled.getResourceId(),
                        settled.getResourceFingerprint(),
                        CanonicalTaskStatus.SETTLED,
                        settled.getProgress(),
                        fakeStatuses,
                        settled.getLogicValues(),
                        settled.getPublicLogicOutputs(),
                        false,
                        settled.getResultPortId()));
            }
        }, "task.snapshot.active_objective");
    }

    private static void malformedFailsClosed() {
        CanonicalGraphResource cycle = resource("cycle", false);
        List<CanonicalGraphNode> nodes = Arrays.asList(
            node("activate", "activate", ports(out("logic_out", 0)), empty()),
            node(
                "kill",
                "objective",
                ports(in("logic_enable", 0), out("logic_status", 1)),
                objective("kill_entity", "slime", null, 1)),
            node("settle", "settle", ports(in("result", 0)), empty()));
        List<CanonicalGraphConnection> edges = Arrays.asList(
            edge("activate", "logic_out", "kill", "logic_enable"),
            edge("kill", "logic_status", "settle", "result"),
            edge("kill", "logic_status", "kill", "logic_enable"));
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalTaskRuntime.start(
                    new CanonicalGraphResource(
                        1,
                        CanonicalGraphResourceKind.TASK,
                        cycle.getId(),
                        "Cycle",
                        new CanonicalGraph(nodes, edges)));
            }
        }, "task.logic.input.multiple_sources");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalTaskRuntime.start(resource("bad", false, "unsupported"));
            }
        }, "task.node.type.unsupported");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalTaskRuntime
                    .start(new CanonicalGraphResource(1, CanonicalGraphResourceKind.TASK, "null_graph", "Null", null));
            }
        }, "task.graph.required");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalTaskRuntime.start(resourceWithExtraObjectiveProperty());
            }
        }, "task.node.properties");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalTaskRuntime.start(resourceWithReservedPublicId());
            }
        }, "task.public_port.id.reserved");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalTaskRuntime.start(resourceWithDuplicateLogicId());
            }
        }, "task.public_port.id.duplicate");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalTaskRuntime.start(resourceWithDuplicatePublicDisplay());
            }
        }, "task.public_port.display_name.duplicate");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalTaskRuntime.start(resourceWithCrossDuplicateDisplay());
            }
        }, "task.public_port.display_name.duplicate");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalTaskRuntime.start(resourceWithDuplicateSettleId());
            }
        }, "task.public_port.id.duplicate");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                CanonicalTaskRuntime.start(resourceWithNegativeSettleOrder());
            }
        }, "task.settle.order");
    }

    private static CanonicalGraphResource resourceWithExtraObjectiveProperty() {
        CanonicalGraphResource base = resource("extra", false);
        List<CanonicalGraphNode> nodes = new java.util.ArrayList<CanonicalGraphNode>(
            base.getGraph()
                .getNodes());
        Map<String, JsonElement> properties = new LinkedHashMap<String, JsonElement>(
            nodes.get(1)
                .getProperties());
        properties.put("unexpected", json("nope"));
        nodes.set(1, node("kill", "objective", ports(in("logic_enable", 0), out("logic_status", 1)), properties));
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.TASK,
            "extra",
            "Probe",
            new CanonicalGraph(
                nodes,
                base.getGraph()
                    .getConnections()));
    }

    private static CanonicalGraphResource resource(String id, boolean priority) {
        return resource(id, priority, null);
    }

    private static CanonicalGraphResource resource(String id, boolean priority, String extraType) {
        Map<String, JsonElement> all = objective("kill_entity", "slime", null, priority ? 1 : 2);
        Map<String, JsonElement> collect = objective("collect_item", "iron", metadataJson("grade", "raw"), 2);
        Map<String, JsonElement> interact = objective("interact_actor", "tavern_boss", null, 1);
        List<CanonicalGraphNode> nodes = Arrays.asList(
            node("activate", "activate", ports(out("logic_out", 0)), empty()),
            node("kill", "objective", ports(in("logic_enable", 0), out("logic_status", 1)), all),
            node("collect", "objective", ports(in("logic_enable", 0), out("logic_status", 1)), collect),
            node("interact", "objective", ports(in("logic_enable", 0), out("logic_status", 1)), interact),
            node("all", "and", ports(in("left", 0), in("right", 1), in("third", 2), out("logic_out", 3)), empty()),
            node(
                "published",
                "logic_output",
                ports(in("logic_in", 0)),
                props("port_id", "all_done", "display_name", "All done")),
            node("never", "logic_output", ports(in("logic_in", 0)), props("port_id", "never", "display_name", "Never")),
            node("settle", "settle", ports(in(priority ? "first" : "success", 0), in("fallback", 1)), empty()));
        if (extraType != null) nodes = Arrays.asList(
            nodes.get(0),
            node("bad", extraType, Collections.<CanonicalGraphPort>emptyList(), empty()),
            nodes.get(7));
        List<CanonicalGraphConnection> edges = Arrays.asList(
            edge("activate", "logic_out", "kill", "logic_enable"),
            edge("activate", "logic_out", "collect", "logic_enable"),
            edge("kill", "logic_status", "interact", "logic_enable"),
            edge("kill", "logic_status", "all", "left"),
            edge("collect", "logic_status", "all", "right"),
            edge("interact", "logic_status", "all", "third"),
            edge("all", "logic_out", "published", "logic_in"),
            edge(
                priority ? "kill" : "all",
                priority ? "logic_status" : "logic_out",
                "settle",
                priority ? "first" : "success"));
        if (priority) edges = Arrays.asList(
            edge("activate", "logic_out", "kill", "logic_enable"),
            edge("activate", "logic_out", "collect", "logic_enable"),
            edge("kill", "logic_status", "interact", "logic_enable"),
            edge("kill", "logic_status", "all", "left"),
            edge("collect", "logic_status", "all", "right"),
            edge("interact", "logic_status", "all", "third"),
            edge("all", "logic_out", "published", "logic_in"),
            edge("kill", "logic_status", "settle", "first"));
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.TASK,
            id,
            "Probe",
            new CanonicalGraph(nodes, edges));
    }

    private static CanonicalGraphResource resourceWithReservedPublicId() {
        CanonicalGraphResource base = resource("reserved", false);
        List<CanonicalGraphNode> nodes = new java.util.ArrayList<CanonicalGraphNode>(
            base.getGraph()
                .getNodes());
        nodes.set(
            5,
            node(
                "published",
                "logic_output",
                ports(in("logic_in", 0)),
                props("port_id", "logic_in", "display_name", "Reserved")));
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.TASK,
            "reserved",
            "Probe",
            new CanonicalGraph(
                nodes,
                base.getGraph()
                    .getConnections()));
    }

    private static CanonicalGraphResource resourceWithDuplicateLogicId() {
        CanonicalGraphResource base = resource("duplicate_logic", false);
        List<CanonicalGraphNode> nodes = new java.util.ArrayList<CanonicalGraphNode>(
            base.getGraph()
                .getNodes());
        nodes.set(
            6,
            node(
                "never",
                "logic_output",
                ports(in("logic_in", 0)),
                props("port_id", "all_done", "display_name", "Another")));
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.TASK,
            "duplicate_logic",
            "Probe",
            new CanonicalGraph(
                nodes,
                base.getGraph()
                    .getConnections()));
    }

    private static CanonicalGraphResource resourceWithDuplicatePublicDisplay() {
        CanonicalGraphResource base = resource("duplicate_display", false);
        List<CanonicalGraphNode> nodes = new java.util.ArrayList<CanonicalGraphNode>(
            base.getGraph()
                .getNodes());
        nodes.set(
            6,
            node(
                "never",
                "logic_output",
                ports(in("logic_in", 0)),
                props("port_id", "other", "display_name", "All done")));
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.TASK,
            "duplicate_display",
            "Probe",
            new CanonicalGraph(
                nodes,
                base.getGraph()
                    .getConnections()));
    }

    private static CanonicalGraphResource resourceWithDuplicateSettleId() {
        CanonicalGraphResource base = resource("duplicate_settle", false);
        List<CanonicalGraphNode> nodes = new java.util.ArrayList<CanonicalGraphNode>(
            base.getGraph()
                .getNodes());
        nodes.set(7, node("settle", "settle", ports(in("all_done", 0), in("fallback", 1)), empty()));
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.TASK,
            "duplicate_settle",
            "Probe",
            new CanonicalGraph(
                nodes,
                base.getGraph()
                    .getConnections()));
    }

    private static CanonicalGraphResource resourceWithCrossDuplicateDisplay() {
        CanonicalGraphResource base = resource("duplicate_cross_display", false);
        List<CanonicalGraphNode> nodes = new java.util.ArrayList<CanonicalGraphNode>(
            base.getGraph()
                .getNodes());
        nodes.set(7, node("settle", "settle", ports(in("success", 0), in("fallback", 1)), empty()));
        List<CanonicalGraphPort> slots = Arrays.asList(
            new CanonicalGraphPort(
                "success",
                "All done",
                CanonicalGraphPortDirection.INPUT,
                CanonicalGraphInterfaceKind.LOGIC,
                0),
            new CanonicalGraphPort(
                "fallback",
                "Fallback",
                CanonicalGraphPortDirection.INPUT,
                CanonicalGraphInterfaceKind.LOGIC,
                1));
        nodes.set(7, node("settle", "settle", slots, empty()));
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.TASK,
            "duplicate_cross_display",
            "Probe",
            new CanonicalGraph(
                nodes,
                base.getGraph()
                    .getConnections()));
    }

    private static CanonicalGraphResource resourceWithNegativeSettleOrder() {
        CanonicalGraphResource base = resource("negative_order", false);
        List<CanonicalGraphNode> nodes = new java.util.ArrayList<CanonicalGraphNode>(
            base.getGraph()
                .getNodes());
        nodes.set(7, node("settle", "settle", ports(in("success", -1), in("fallback", 1)), empty()));
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.TASK,
            "negative_order",
            "Probe",
            new CanonicalGraph(
                nodes,
                base.getGraph()
                    .getConnections()));
    }

    private static CanonicalGraphResource resourceWithKillRequirements(String id, int required) {
        CanonicalGraphResource base = resource(id, false);
        List<CanonicalGraphNode> nodes = new java.util.ArrayList<CanonicalGraphNode>(
            base.getGraph()
                .getNodes());
        Map<String, JsonElement> kill = new LinkedHashMap<String, JsonElement>(
            nodes.get(1)
                .getProperties());
        kill.put("required", new JsonParser().parse(Integer.toString(required)));
        nodes.set(1, node("kill", "objective", ports(in("logic_enable", 0), out("logic_status", 1)), kill));
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.TASK,
            id,
            "Probe",
            new CanonicalGraph(
                nodes,
                base.getGraph()
                    .getConnections()));
    }

    private static Map<String, JsonElement> objective(String type, String value, JsonElement metadata, int required) {
        Map<String, JsonElement> props = props("objective_type", type, "description", "Probe objective");
        if (!"interact_actor".equals(type)) props.put("required", new JsonParser().parse(Integer.toString(required)));
        if ("kill_entity".equals(type)) props.put("entity", json(value));
        if ("collect_item".equals(type)) {
            props.put("item", json(value));
            props.put("metadata", metadata == null ? metadataJson("grade", "raw") : metadata);
        }
        if ("interact_actor".equals(type)) props.put("actor_id", json(value));
        return props;
    }

    private static CanonicalGraphNode node(String id, String type, List<CanonicalGraphPort> ports,
        Map<String, JsonElement> properties) {
        return new CanonicalGraphNode(id, type, id, ports, properties);
    }

    private static CanonicalGraphPort in(String id, int order) {
        return port(id, true, order);
    }

    private static CanonicalGraphPort out(String id, int order) {
        return port(id, false, order);
    }

    private static CanonicalGraphPort port(String id, boolean input, int order) {
        return new CanonicalGraphPort(
            id,
            id,
            input ? CanonicalGraphPortDirection.INPUT : CanonicalGraphPortDirection.OUTPUT,
            CanonicalGraphInterfaceKind.LOGIC,
            order);
    }

    private static List<CanonicalGraphPort> ports(CanonicalGraphPort... ports) {
        return Arrays.asList(ports);
    }

    private static CanonicalGraphConnection edge(String from, String fromPort, String to, String toPort) {
        return new CanonicalGraphConnection(from, fromPort, to, toPort, CanonicalGraphInterfaceKind.LOGIC);
    }

    private static Map<String, JsonElement> props(String... values) {
        Map<String, JsonElement> result = new LinkedHashMap<String, JsonElement>();
        for (int i = 0; i < values.length; i += 2) result.put(values[i], json(values[i + 1]));
        return result;
    }

    private static JsonElement json(String value) {
        return new JsonParser().parse("\"" + value + "\"");
    }

    private static JsonElement metadataJson(String key, String value) {
        return new JsonParser().parse("{\"" + key + "\":\"" + value + "\"}");
    }

    private static Map<String, JsonElement> empty() {
        return Collections.emptyMap();
    }

    private static Map<String, String> metadata(String key, String value) {
        Map<String, String> result = new LinkedHashMap<String, String>();
        result.put(key, value);
        return result;
    }

    private static void expectUnsupported(Runnable action) {
        try {
            action.run();
            throw new AssertionError("Expected immutable map");
        } catch (UnsupportedOperationException expected) {}
    }

    private static void expectFailure(Runnable action, String code) {
        try {
            action.run();
            throw new AssertionError("Expected " + code);
        } catch (CanonicalGraphResourceException expected) {
            require(code.equals(expected.getCode()), "Expected " + code + ", got " + expected.getCode());
        }
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
