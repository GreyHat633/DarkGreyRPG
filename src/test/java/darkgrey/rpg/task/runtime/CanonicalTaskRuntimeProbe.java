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
        parallelTreasuresKeepIndependentSuccessors();
        System.out.println("BEHAVIOR parallelTreasuresKeepIndependentSuccessors=PASS");
        canonical0310TaskSemantics();
        System.out.println("BEHAVIOR canonical0310TaskSemantics=PASS");
        parallelSequentialAndTypes();
        System.out.println("BEHAVIOR parallelSequentialAndTypes=PASS");
        priorityAndUnconnectedFalse();
        System.out.println("BEHAVIOR priorityAndUnconnectedFalse=PASS");
        activationSettlementAndPostSettlement();
        System.out.println("BEHAVIOR activationSettlementAndPostSettlement=PASS");
        overflowAndParallelSameType();
        System.out.println("BEHAVIOR overflowAndParallelSameType=PASS");
        prerequisiteActivationSemantics();
        System.out.println("BEHAVIOR prerequisiteActivationSemantics=PASS");
        dormantUnselectedObjectiveSemantics();
        System.out.println("BEHAVIOR dormantUnselectedObjectiveSemantics=PASS");
        immutableSnapshotRestore();
        System.out.println("BEHAVIOR immutableSnapshotRestore=PASS");
        malformedFailsClosed();
        System.out.println("BEHAVIOR malformedFailsClosed=PASS");
        System.out.println("TASK_RUNTIME_PROBE_PASS");
    }

    private static void parallelTreasuresKeepIndependentSuccessors() {
        List<CanonicalGraphNode> nodes = new java.util.ArrayList<CanonicalGraphNode>();
        List<CanonicalGraphConnection> edges = new java.util.ArrayList<CanonicalGraphConnection>();
        nodes.add(node("activate", "activate", ports(out("logic_out", 0)), empty()));
        nodes.add(node("settle", "settle", ports(in("done", 0)), empty()));
        String[] items = { "emerald", "diamond", "lapis", "gold" };
        for (String item : items) {
            nodes.add(node(item, "objective", ports(out("logic_status", 0)), objective("collect_item", item, null, 1)));
            Map<String, JsonElement> next = objective("interact_actor", item + "_actor", null, 1);
            next.put("prerequisite_enabled", bool(true));
            nodes.add(node(item + "_next", "objective", ports(in("prerequisite", 0), out("logic_status", 1)), next));
            edges.add(edge(item, "logic_status", item + "_next", "prerequisite"));
        }
        CanonicalGraphResource resource = currentResource(
            CanonicalGraphResourceKind.TASK,
            "treasures",
            "Treasures",
            new CanonicalGraph(nodes, edges));
        for (String winner : items) {
            CanonicalTaskRuntime runtime = start(resource);
            require(
                runtime.accept(CanonicalTaskEvent.collectItem(itemId(winner), metadata("grade", "raw"), 1)),
                "Treasure completes");
            for (String item : items) {
                require(
                    runtime.isObjectiveActive(item) == !item.equals(winner),
                    "Other treasure objectives remain active");
                require(
                    runtime.isObjectiveActive(item + "_next") == item.equals(winner),
                    "Only corresponding successor activates");
            }
            require(runtime.isActive(), "Completing one branch does not settle task");
        }
    }

    private static void prerequisiteActivationSemantics() {
        CanonicalTaskRuntime legacy = start(legacyObjectiveResource("legacy_default", null));
        require(legacy.isObjectiveActive("objective"), "Absent prerequisite property keeps legacy objective active");
        CanonicalTaskRuntime explicitOff = start(legacyObjectiveResource("legacy_false", Boolean.FALSE));
        require(
            explicitOff.isObjectiveActive("objective"),
            "False prerequisite property keeps legacy objective active");

        CanonicalTaskRuntime runtime = start(prerequisiteResource("prerequisite", true, "prerequisite"));
        require(
            runtime.getObjectiveStatuses()
                .get("objective") == CanonicalTaskObjectiveStatus.INACTIVE,
            "Enabled prerequisite starts inactive");
        require(
            !runtime.accept(CanonicalTaskEvent.killEntity("ST-2345-6789-ABCD-EFGH~actor~slime")),
            "False prerequisite does not activate");
        require(runtime.setLogicInput("prerequisite_source", true), "True prerequisite changes input");
        require(runtime.isObjectiveActive("objective"), "True prerequisite activates objective");
        require(runtime.setLogicInput("prerequisite_source", false), "False transition changes input");
        require(runtime.isObjectiveActive("objective"), "Activated prerequisite objective is sticky");

        CanonicalTaskSnapshot activeSnapshot = runtime.snapshot();
        runtime = CanonicalTaskRuntime
            .restore(prerequisiteResource("prerequisite", true, "prerequisite"), activeSnapshot);
        require(runtime.isObjectiveActive("objective"), "Active prerequisite state survives snapshot restore");
        require(
            runtime.accept(CanonicalTaskEvent.killEntity("ST-2345-6789-ABCD-EFGH~actor~slime")),
            "Activated objective completes");
        require(
            runtime.getObjectiveStatuses()
                .get("objective") == CanonicalTaskObjectiveStatus.COMPLETED,
            "Completed objective status is retained");
        require(!runtime.setLogicInput("prerequisite_source", true), "Settled input remains frozen");
        require(
            runtime.getObjectiveStatuses()
                .get("objective") == CanonicalTaskObjectiveStatus.COMPLETED,
            "Completed objective remains completed after input change");
        CanonicalGraphResource noSettle = withoutSettlements(
            prerequisiteResource("active_latch", true, "prerequisite"));
        CanonicalTaskRuntime active = start(noSettle);
        active.setLogicInput("prerequisite_source", true);
        active.accept(CanonicalTaskEvent.killEntity("ST-2345-6789-ABCD-EFGH~actor~slime"));
        require(active.isActive(), "Zero settlement does not finish the run");
        require(active.setLogicInput("prerequisite_source", false), "Active completed objective input still changes");
        require(
            active.getObjectiveStatuses()
                .get("objective") == CanonicalTaskObjectiveStatus.COMPLETED,
            "Active completion remains latched after gate turns false");
        require(
            CanonicalTaskRuntime.restore(noSettle, active.snapshot())
                .getObjectiveStatuses()
                .get("objective") == CanonicalTaskObjectiveStatus.COMPLETED,
            "Active completion latch restores");
    }

    private static void dormantUnselectedObjectiveSemantics() {
        CanonicalTaskRuntime runtime = start(dormantUnselectedResource(false));
        require(runtime.isObjectiveActive("configured"), "Configured objective remains active");
        require(
            runtime.getObjectiveStatuses()
                .get("dormant") == CanonicalTaskObjectiveStatus.INACTIVE,
            "Detached unselected objective remains dormant");
        require(
            runtime.accept(CanonicalTaskEvent.killEntity("ST-2345-6789-ABCD-EFGH~actor~slimes", 3)),
            "Configured objective accepts events");
        require(runtime.isSettled(), "Dormant authoring objective does not block configured Task settlement");

        expectFailure(new Runnable() {

            @Override
            public void run() {
                start(dormantUnselectedResource(true));
            }
        }, "task.objective.target.unselected.connected");
    }

    private static CanonicalGraphResource dormantUnselectedResource(boolean connectDormant) {
        List<CanonicalGraphNode> nodes = Arrays.asList(
            node(
                "configured",
                "objective",
                ports(out("logic_status", 0)),
                objective("kill_entity", "ST-2345-6789-ABCD-EFGH~actor~slimes", null, 3)),
            node("dormant", "objective", ports(out("logic_status", 0)), objective("kill_entity", "", null, 10)),
            node(
                "settle",
                "settle",
                connectDormant ? ports(in("complete", 0), in("invalid", 1)) : ports(in("complete", 0)),
                empty()));
        List<CanonicalGraphConnection> edges = new java.util.ArrayList<CanonicalGraphConnection>();
        edges.add(edge("configured", "logic_status", "settle", "complete"));
        if (connectDormant) edges.add(edge("dormant", "logic_status", "settle", "invalid"));
        return currentResource(
            CanonicalGraphResourceKind.TASK,
            connectDormant ? "dormant_connected" : "dormant",
            "Dormant Objective",
            new CanonicalGraph(nodes, edges));
    }

    private static void canonical0310TaskSemantics() {
        CanonicalTaskRuntime runtime = start(canonical0310Resource());
        require(runtime.isActive(), "Task without activate starts Active");
        require(runtime.isObjectiveActive("default"), "Objective without conditions is active by default");
        require(!runtime.isObjectiveActive("gated"), "False prerequisite gates objective");
        require(
            runtime.accept(CanonicalTaskEvent.killEntity("ST-2345-6789-ABCD-EFGH~actor~slime")),
            "Default objective accepts event");
        require(
            runtime.getProgress()
                .get("default")
                .intValue() == 1,
            "Default objective progress");
        require(
            !runtime.accept(CanonicalTaskEvent.killEntity("ST-2345-6789-ABCD-EFGH~actor~slime")),
            "Gated objective pauses counting");
        require(runtime.setLogicInput("night", true), "Prerequisite changes");
        require(runtime.isObjectiveActive("gated"), "Prerequisite activates objective");
        require(
            runtime.accept(CanonicalTaskEvent.killEntity("ST-2345-6789-ABCD-EFGH~actor~slime")),
            "Gated objective counts when enabled");
        require(
            runtime.getProgress()
                .get("gated")
                .intValue() == 1,
            "Gated objective progress");
        CanonicalTaskSnapshot saved = runtime.snapshot();
        runtime = CanonicalTaskRuntime.restore(canonical0310Resource(), saved);
        require(!runtime.setLogicInput("night", true), "Restored input survives");
        require(runtime.isObjectiveActive("gated"), "Restored objective remains active");
        require(runtime.setLogicInput("night", false), "False transition changes input");
        require(runtime.isObjectiveActive("gated"), "Activated objective remains active after false");
        require(
            runtime.accept(CanonicalTaskEvent.killEntity("ST-2345-6789-ABCD-EFGH~actor~slime")),
            "Activated objective completes");
        require(runtime.isSettled() && "success".equals(runtime.getResultPortId()), "First settlement slot wins");
        require(
            runtime.getPublicLogicOutputs()
                .get("gated_done")
                .booleanValue(),
            "Logic output is updated");
        require(
            !runtime.accept(CanonicalTaskEvent.killEntity("ST-2345-6789-ABCD-EFGH~actor~slime")),
            "Settled Task ignores events");
    }

    private static CanonicalGraphResource canonical0310Resource() {
        Map<String, JsonElement> gated = objective("kill_entity", "ST-2345-6789-ABCD-EFGH~actor~slime", null, 2);
        gated.put("prerequisite_enabled", bool(true));
        List<CanonicalGraphNode> nodes = Arrays.asList(
            node(
                "night",
                "logic_input",
                ports(out("logic_out", 0)),
                props("port_id", "night", "display_name", "Night")),
            node(
                "default",
                "objective",
                ports(out("logic_status", 0)),
                objective("kill_entity", "ST-2345-6789-ABCD-EFGH~actor~slime", null, 1)),
            node("gated", "objective", ports(in("prerequisite", 0), out("logic_status", 1)), gated),
            node(
                "published",
                "logic_output",
                ports(in("logic_in", 0)),
                props("port_id", "gated_done", "display_name", "Gated done")),
            node("settle", "settle", ports(in("success", 0), in("fallback", 1)), empty()));
        return currentResource(
            CanonicalGraphResourceKind.TASK,
            "canonical_0310",
            "Canonical 0.3.1.0",
            new CanonicalGraph(
                nodes,
                Arrays.asList(
                    edge("night", "logic_out", "gated", "prerequisite"),
                    edge("gated", "logic_status", "published", "logic_in"),
                    edge("gated", "logic_status", "settle", "success"))));
    }

    private static void parallelSequentialAndTypes() {
        CanonicalTaskRuntime runtime = start(resource("task_main", false));
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
        require(
            runtime.accept(CanonicalTaskEvent.killEntity("ST-2345-6789-ABCD-EFGH~actor~slime")),
            "kill event must update");
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
            runtime.accept(
                CanonicalTaskEvent.collectItem("ST-2345-6789-ABCD-EFGH~item~iron", metadata("grade", "raw"), 2)),
            "collect event must update");
        require(
            runtime.accept(CanonicalTaskEvent.killEntity("ST-2345-6789-ABCD-EFGH~actor~slime")),
            "second kill must update");
        require(
            runtime.getObjectiveStatuses()
                .get("interact") == CanonicalTaskObjectiveStatus.ACTIVE,
            "interact must enable after kill completes");
        require(
            runtime.getProgress()
                .get("interact")
                .intValue() == 0,
            "new objective must not consume event");
        require(
            runtime.accept(CanonicalTaskEvent.interactActor("ST-2345-6789-ABCD-EFGH~actor~tavern_boss")),
            "interact event must update");
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
        CanonicalTaskRuntime runtime = start(resource("task_priority", true));
        require(
            !runtime.getPublicLogicOutputs()
                .get("never")
                .booleanValue(),
            "unconnected public Logic must be false");
        require(
            runtime.accept(CanonicalTaskEvent.killEntity("ST-2345-6789-ABCD-EFGH~actor~slime")),
            "priority event must update");
        require(runtime.isSettled() && "first".equals(runtime.getResultPortId()), "first true slot wins");
        Map<String, Integer> before = runtime.getProgress();
        require(
            !runtime.accept(CanonicalTaskEvent.killEntity("ST-2345-6789-ABCD-EFGH~actor~slime")),
            "settled Task must ignore events");
        require(before.equals(runtime.getProgress()), "settled event must not change progress");
    }

    private static void activationSettlementAndPostSettlement() {
        CanonicalGraphResource resource = currentResource(
            CanonicalGraphResourceKind.TASK,
            "activation_settlement",
            "Probe",
            new CanonicalGraph(
                Arrays.asList(
                    node("activate", "activate", ports(out("logic_out", 0)), empty()),
                    node("settle", "settle", ports(in("done", 0)), empty())),
                Collections.singletonList(edge("activate", "logic_out", "settle", "done"))));
        CanonicalTaskRuntime runtime = start(resource);
        require(
            runtime.isSettled() && "done".equals(runtime.getResultPortId()),
            "activate-to-settle must initialize deterministically");
        require(
            !runtime.accept(CanonicalTaskEvent.interactActor("ST-2345-6789-ABCD-EFGH~actor~nobody")),
            "settled event must be ignored");
    }

    private static void overflowAndParallelSameType() {
        CanonicalGraphResource resource = resourceWithKillRequirements("overflow", Integer.MAX_VALUE);
        CanonicalTaskRuntime runtime = start(resource);
        require(
            runtime.accept(CanonicalTaskEvent.killEntity("ST-2345-6789-ABCD-EFGH~actor~slime")),
            "overflow first event");
        require(
            runtime.getProgress()
                .get("kill")
                .intValue() == 1,
            "overflow first progress");
        require(
            runtime.accept(CanonicalTaskEvent.killEntity("ST-2345-6789-ABCD-EFGH~actor~slime", Integer.MAX_VALUE)),
            "overflow event");
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
        CanonicalTaskRuntime parallel = start(parallelKillResource());
        parallel.accept(CanonicalTaskEvent.killEntity("ST-2345-6789-ABCD-EFGH~actor~slime"));
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
        Map<String, JsonElement> first = objective("kill_entity", "ST-2345-6789-ABCD-EFGH~actor~slime", null, 2);
        Map<String, JsonElement> second = objective("kill_entity", "ST-2345-6789-ABCD-EFGH~actor~slime", null, 2);
        first.put("prerequisite_enabled", bool(true));
        second.put("prerequisite_enabled", bool(true));
        List<CanonicalGraphNode> nodes = Arrays.asList(
            node("activate", "activate", ports(out("logic_out", 0)), empty()),
            node("first_kill", "objective", ports(in("prerequisite", 0), out("logic_status", 1)), first),
            node("second_kill", "objective", ports(in("prerequisite", 0), out("logic_status", 1)), second),
            node("settle", "settle", ports(in("done", 0)), empty()));
        return currentResource(
            CanonicalGraphResourceKind.TASK,
            "parallel_kill",
            "Probe",
            new CanonicalGraph(
                nodes,
                Arrays.asList(
                    edge("activate", "logic_out", "first_kill", "prerequisite"),
                    edge("activate", "logic_out", "second_kill", "prerequisite"))));
    }

    private static void immutableSnapshotRestore() {
        CanonicalGraphResource resource = resource("task_snapshot", false);
        CanonicalTaskRuntime runtime = start(resource);
        runtime.accept(CanonicalTaskEvent.killEntity("ST-2345-6789-ABCD-EFGH~actor~slime"));
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
        CanonicalTaskRuntime settledRuntime = start(settledResource);
        settledRuntime.accept(CanonicalTaskEvent.killEntity("ST-2345-6789-ABCD-EFGH~actor~slime"));
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
        Map<String, JsonElement> cycleObjective = objective(
            "kill_entity",
            "ST-2345-6789-ABCD-EFGH~actor~slime",
            null,
            1);
        cycleObjective.put("prerequisite_enabled", bool(true));
        List<CanonicalGraphNode> nodes = Arrays.asList(
            node("activate", "activate", ports(out("logic_out", 0)), empty()),
            node("kill", "objective", ports(in("prerequisite", 0), out("logic_status", 1)), cycleObjective),
            node("settle", "settle", ports(in("result", 0)), empty()));
        List<CanonicalGraphConnection> edges = Arrays.asList(
            edge("activate", "logic_out", "kill", "prerequisite"),
            edge("kill", "logic_status", "settle", "result"),
            edge("kill", "logic_status", "kill", "prerequisite"));
        expectFailure(new Runnable() {

            @Override
            public void run() {
                start(
                    currentResource(
                        CanonicalGraphResourceKind.TASK,
                        cycle.getId(),
                        "Cycle",
                        new CanonicalGraph(nodes, edges)));
            }
        }, "task.logic.input.multiple_sources");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                start(resource("bad", false, "unsupported"));
            }
        }, "task.node.type.unsupported");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                start(currentResource(CanonicalGraphResourceKind.TASK, "null_graph", "Null", null));
            }
        }, "task.graph.required");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                start(resourceWithExtraObjectiveProperty());
            }
        }, "task.node.properties");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                start(resourceWithReservedPublicId());
            }
        }, "task.public_port.id.reserved");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                start(resourceWithDuplicateLogicId());
            }
        }, "task.public_port.id.duplicate");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                start(resourceWithDuplicatePublicDisplay());
            }
        }, "task.public_port.display_name.duplicate");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                start(resourceWithCrossDuplicateDisplay());
            }
        }, "task.public_port.display_name.duplicate");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                start(resourceWithDuplicateSettleId());
            }
        }, "task.public_port.id.duplicate");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                start(resourceWithNegativeSettleOrder());
            }
        }, "task.settle.order");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                start(prerequisiteResource("missing_prerequisite", true, null));
            }
        }, "task.objective.prerequisite.ports");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                start(legacyObjectiveWithInput("absent_other_input", null, "other"));
            }
        }, "task.objective.prerequisite.ports");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                start(legacyObjectiveWithInput("false_other_input", Boolean.FALSE, "other"));
            }
        }, "task.objective.prerequisite.ports");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                start(prerequisiteResource("wrong_prerequisite", true, "other"));
            }
        }, "task.objective.prerequisite.ports");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                start(prerequisiteResource("disabled_prerequisite", false, "prerequisite"));
            }
        }, "task.objective.prerequisite.ports");
        expectFailure(new Runnable() {

            @Override
            public void run() {
                start(prerequisiteResource("invalid_prerequisite_property", "not_boolean", "prerequisite"));
            }
        }, "task.objective.prerequisite_enabled");
    }

    private static CanonicalGraphResource legacyObjectiveResource(String id, Boolean enabled) {
        Map<String, JsonElement> properties = objective("kill_entity", "ST-2345-6789-ABCD-EFGH~actor~slime", null, 1);
        if (enabled != null) properties.put("prerequisite_enabled", bool(enabled.booleanValue()));
        return currentResource(
            CanonicalGraphResourceKind.TASK,
            id,
            "Legacy",
            new CanonicalGraph(
                Arrays.asList(
                    node("objective", "objective", ports(out("logic_status", 0)), properties),
                    node("settle", "settle", ports(in("done", 0)), empty())),
                Collections.singletonList(edge("objective", "logic_status", "settle", "done"))));
    }

    private static CanonicalGraphResource legacyObjectiveWithInput(String id, Boolean enabled, String inputId) {
        Map<String, JsonElement> properties = objective("kill_entity", "ST-2345-6789-ABCD-EFGH~actor~slime", null, 1);
        if (enabled != null) properties.put("prerequisite_enabled", bool(enabled.booleanValue()));
        return prerequisiteGraphResource(id, properties, inputId);
    }

    private static CanonicalGraphResource prerequisiteResource(String id, boolean enabled, String inputId) {
        return prerequisiteResource(id, enabled ? Boolean.TRUE : Boolean.FALSE, inputId);
    }

    private static CanonicalGraphResource prerequisiteResource(String id, String enabled, String inputId) {
        Map<String, JsonElement> properties = objective("kill_entity", "ST-2345-6789-ABCD-EFGH~actor~slime", null, 1);
        properties.put("prerequisite_enabled", json(enabled));
        return prerequisiteGraphResource(id, properties, inputId);
    }

    private static CanonicalGraphResource prerequisiteResource(String id, Boolean enabled, String inputId) {
        Map<String, JsonElement> properties = objective("kill_entity", "ST-2345-6789-ABCD-EFGH~actor~slime", null, 1);
        properties.put("prerequisite_enabled", bool(enabled.booleanValue()));
        return prerequisiteGraphResource(id, properties, inputId);
    }

    private static CanonicalGraphResource prerequisiteGraphResource(String id, Map<String, JsonElement> properties,
        String inputId) {
        List<CanonicalGraphNode> nodes = new java.util.ArrayList<CanonicalGraphNode>();
        List<CanonicalGraphConnection> edges = new java.util.ArrayList<CanonicalGraphConnection>();
        if (inputId != null) {
            nodes.add(
                node(
                    "prerequisite_source",
                    "logic_input",
                    ports(out("logic_out", 0)),
                    props("port_id", "prerequisite_source", "display_name", "Prerequisite")));
            edges.add(edge("prerequisite_source", "logic_out", "objective", inputId));
        }
        List<CanonicalGraphPort> objectivePorts = new java.util.ArrayList<CanonicalGraphPort>();
        if (inputId != null) objectivePorts.add(in(inputId, 0));
        objectivePorts.add(out("logic_status", inputId == null ? 0 : 1));
        nodes.add(node("objective", "objective", objectivePorts, properties));
        nodes.add(node("settle", "settle", ports(in("done", 0)), empty()));
        edges.add(edge("objective", "logic_status", "settle", "done"));
        return currentResource(CanonicalGraphResourceKind.TASK, id, "Prerequisite", new CanonicalGraph(nodes, edges));
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
        nodes.set(1, node("kill", "objective", ports(in("prerequisite", 0), out("logic_status", 1)), properties));
        return currentResource(
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
        Map<String, JsonElement> all = objective(
            "kill_entity",
            "ST-2345-6789-ABCD-EFGH~actor~slime",
            null,
            priority ? 1 : 2);
        Map<String, JsonElement> collect = objective(
            "collect_item",
            "ST-2345-6789-ABCD-EFGH~item~iron",
            metadataJson("grade", "raw"),
            2);
        Map<String, JsonElement> interact = objective(
            "interact_actor",
            "ST-2345-6789-ABCD-EFGH~actor~tavern_boss",
            null,
            1);
        all.put("prerequisite_enabled", bool(true));
        collect.put("prerequisite_enabled", bool(true));
        interact.put("prerequisite_enabled", bool(true));
        List<CanonicalGraphNode> nodes = Arrays.asList(
            node("activate", "activate", ports(out("logic_out", 0)), empty()),
            node("kill", "objective", ports(in("prerequisite", 0), out("logic_status", 1)), all),
            node("collect", "objective", ports(in("prerequisite", 0), out("logic_status", 1)), collect),
            node("interact", "objective", ports(in("prerequisite", 0), out("logic_status", 1)), interact),
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
            edge("activate", "logic_out", "kill", "prerequisite"),
            edge("activate", "logic_out", "collect", "prerequisite"),
            edge("kill", "logic_status", "interact", "prerequisite"),
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
            edge("activate", "logic_out", "kill", "prerequisite"),
            edge("activate", "logic_out", "collect", "prerequisite"),
            edge("kill", "logic_status", "interact", "prerequisite"),
            edge("kill", "logic_status", "all", "left"),
            edge("collect", "logic_status", "all", "right"),
            edge("interact", "logic_status", "all", "third"),
            edge("all", "logic_out", "published", "logic_in"),
            edge("kill", "logic_status", "settle", "first"));
        return currentResource(CanonicalGraphResourceKind.TASK, id, "Probe", new CanonicalGraph(nodes, edges));
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
        return currentResource(
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
        return currentResource(
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
        return currentResource(
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
        nodes.set(7, settlement("settle", "all_done", "Success", 0));
        return currentResource(
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
        nodes.set(7, settlement("settle", "success", "All done", 0));
        return currentResource(
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
        nodes.set(7, settlement("settle", "success", "Success", -1));
        return currentResource(
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
        nodes.set(1, node("kill", "objective", ports(in("prerequisite", 0), out("logic_status", 1)), kill));
        return currentResource(
            CanonicalGraphResourceKind.TASK,
            id,
            "Probe",
            new CanonicalGraph(
                nodes,
                base.getGraph()
                    .getConnections()));
    }

    private static Map<String, JsonElement> objective(String type, String value, JsonElement metadata, int required) {
        if (value != null && !value.isEmpty() && !value.contains("~"))
            value = "collect_item".equals(type) ? itemId(value) : "ST-2345-6789-ABCD-EFGH~actor~" + value;
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
        if ("activate".equals(type)) {
            type = "logic_input";
            properties = props("port_id", "activate", "display_name", "Enable objectives");
        }
        if ("logic_output".equals(type)) {
            properties = new LinkedHashMap<String, JsonElement>(properties);
            properties.put("display_order", new com.google.gson.JsonPrimitive("never".equals(id) ? 1 : 0));
        }
        return new CanonicalGraphNode(id, type, id, ports, properties);
    }

    private static String itemId(String value) {
        return "ST-2345-6789-ABCD-EFGH~item~" + value;
    }

    private static CanonicalGraphNode settlement(String nodeId, String publicId, String name, int order) {
        Map<String, JsonElement> values = props("port_id", publicId, "display_name", name);
        values.put("display_order", new com.google.gson.JsonPrimitive(order));
        return new CanonicalGraphNode(nodeId, "settle", nodeId, ports(in("logic_in", 0)), values);
    }

    /** Current fixtures use public Logic input and separate, explicitly ordered settlement nodes. */
    private static CanonicalGraphResource currentResource(CanonicalGraphResourceKind kind, String id, String name,
        CanonicalGraph graph) {
        if (graph == null) return new CanonicalGraphResource(3, kind, "ST-2345-6789-ABCD-EFGH~task~" + id, name, null);
        List<CanonicalGraphNode> current = new java.util.ArrayList<CanonicalGraphNode>();
        Map<String, String> endpoints = new LinkedHashMap<String, String>();
        for (CanonicalGraphNode node : graph.getNodes()) {
            if (!"settle".equals(node.getType())) {
                current.add(node);
                continue;
            }
            if (node.getProperties()
                .containsKey("port_id")) {
                current.add(node);
                continue;
            }
            for (CanonicalGraphPort port : node.getPorts()) {
                String nodeId = port == node.getPorts()
                    .get(0) ? node.getId() : node.getId() + "__" + port.getId();
                Map<String, JsonElement> props = props("port_id", port.getId(), "display_name", port.getDisplayName());
                props.put("display_order", new com.google.gson.JsonPrimitive(port.getOrder()));
                current.add(new CanonicalGraphNode(nodeId, "settle", nodeId, ports(in("logic_in", 0)), props));
                endpoints.put(node.getId() + ":" + port.getId(), nodeId);
            }
        }
        List<CanonicalGraphConnection> edges = new java.util.ArrayList<CanonicalGraphConnection>();
        for (CanonicalGraphConnection edge : graph.getConnections()) {
            String target = endpoints.get(edge.getToNodeId() + ":" + edge.getToPortId());
            edges.add(target == null ? edge : edge(edge.getFromNodeId(), edge.getFromPortId(), target, "logic_in"));
        }
        return new CanonicalGraphResource(
            3,
            kind,
            "ST-2345-6789-ABCD-EFGH~task~" + id,
            name,
            new CanonicalGraph(current, edges));
    }

    private static CanonicalTaskRuntime start(CanonicalGraphResource resource) {
        CanonicalTaskRuntime runtime = CanonicalTaskRuntime.start(resource);
        for (CanonicalGraphNode node : resource.getGraph()
            .getNodes())
            if ("activate".equals(node.getId()) && "logic_input".equals(node.getType()))
                runtime.setLogicInput("activate", true);
        return runtime;
    }

    private static CanonicalGraphResource withoutSettlements(CanonicalGraphResource resource) {
        List<CanonicalGraphNode> nodes = new java.util.ArrayList<CanonicalGraphNode>();
        List<CanonicalGraphConnection> edges = new java.util.ArrayList<CanonicalGraphConnection>();
        for (CanonicalGraphNode node : resource.getGraph()
            .getNodes()) if (!"settle".equals(node.getType())) nodes.add(node);
        for (CanonicalGraphConnection edge : resource.getGraph()
            .getConnections())
            if (!edge.getToNodeId()
                .startsWith("settle")) edges.add(edge);
        return new CanonicalGraphResource(
            3,
            CanonicalGraphResourceKind.TASK,
            resource.getId(),
            resource.getDisplayName(),
            new CanonicalGraph(nodes, edges));
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

    private static JsonElement bool(boolean value) {
        return new JsonParser().parse(Boolean.toString(value));
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
