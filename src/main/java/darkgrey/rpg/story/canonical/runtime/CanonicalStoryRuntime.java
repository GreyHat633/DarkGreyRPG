package darkgrey.rpg.story.canonical.runtime;

import java.nio.charset.Charset;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;

import com.google.gson.JsonElement;

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

/** Pure, server-neutral, single-cursor executor for one canonical Story resource. */
public final class CanonicalStoryRuntime {

    private static final int MAX_AUTOMATIC_TRANSITIONS = 512;
    private static final Set<String> FLOW_TYPES = set(
        "start",
        "terminate",
        "session",
        "task",
        "condition",
        "flow_judgment",
        "action",
        "title");
    private static final Set<String> LOGIC_TYPES = set("and", "or", "not", "logic_input", "logic_output");

    private final CanonicalGraphResource resource;
    private final String resourceFingerprint;
    private final Map<String, CanonicalGraphNode> nodes;
    private final Map<String, Boolean> aggregateLogic = new LinkedHashMap<String, Boolean>();
    private final Map<String, Boolean> externalLogicInputs = new LinkedHashMap<String, Boolean>();
    private final Map<String, Boolean> publicLogicOutputs = new LinkedHashMap<String, Boolean>();
    private final List<String> executedFlowJudgmentNodeIds = new ArrayList<String>();
    private CanonicalStoryStatus status;
    private CanonicalStoryRepeatPolicy repeatPolicy;
    private String triggerPortId;
    private String currentNodeId;
    private String currentInputPortId;
    private CanonicalStoryWaitKind waitKind;
    private String waitResourceId;
    private Integer waitDimension;
    private Double waitX;
    private Double waitY;
    private Double waitZ;
    private Double waitRadius;
    private String targetStoryId;
    private Boolean waitingConditionValue;

    private CanonicalStoryRuntime(CanonicalGraphResource resource) {
        validateEnvelope(resource);
        this.resource = resource;
        this.nodes = indexNodes(resource.getGraph());
        validateGraph();
        this.resourceFingerprint = fingerprint(resource);
    }

    public static CanonicalStoryRuntime start(CanonicalGraphResource resource, String triggerPortId,
        CanonicalStoryRepeatPolicy repeatPolicy) {
        return start(resource, triggerPortId, repeatPolicy, Collections.<String, Boolean>emptyMap());
    }

    public static CanonicalStoryRuntime start(CanonicalGraphResource resource, String triggerPortId,
        CanonicalStoryRepeatPolicy repeatPolicy, Map<String, Boolean> logicInputs) {
        CanonicalStoryRuntime runtime = new CanonicalStoryRuntime(resource);
        runtime.status = CanonicalStoryStatus.ACTIVE;
        runtime.repeatPolicy = requirePolicy(repeatPolicy);
        runtime.triggerPortId = requireId(triggerPortId, "Story trigger port ID");
        runtime.waitKind = CanonicalStoryWaitKind.NONE;
        runtime.setInitialLogicInputs(logicInputs);
        CanonicalGraphNode start = runtime.uniqueNode("start");
        runtime.requirePort(
            start,
            runtime.triggerPortId,
            CanonicalGraphPortDirection.OUTPUT,
            CanonicalGraphInterfaceKind.FLOW);
        CanonicalStoryStartConfiguration.Trigger trigger = runtime.startTriggerIfConfigured(runtime.triggerPortId);
        if (trigger != null && trigger.getLogicPortId() != null
            && !runtime.logicInputValue(start, trigger.getLogicPortId(), new HashMap<String, Boolean>()))
            throw failure("story.start.trigger.condition", "Story Start trigger condition is false.");
        runtime.transitionFrom(start, runtime.triggerPortId);
        runtime.resolveAutomatic();
        return runtime;
    }

    public static CanonicalStoryRuntime restore(CanonicalGraphResource resource, CanonicalStorySnapshot snapshot) {
        if (snapshot == null) throw new IllegalArgumentException("Canonical Story snapshot is required.");
        CanonicalStoryRuntime runtime = new CanonicalStoryRuntime(resource);
        if (!resource.getId()
            .equals(snapshot.getResourceId())) throw failure("story.restore.resource", "Story resource ID changed.");
        if (!runtime.resourceFingerprint.equals(snapshot.getResourceFingerprint()))
            throw failure("story.restore.fingerprint", "Story resource changed after the cursor was saved.");
        runtime.status = snapshot.getStatus();
        runtime.repeatPolicy = snapshot.getRepeatPolicy();
        runtime.triggerPortId = snapshot.getTriggerPortId();
        runtime.currentNodeId = snapshot.getCurrentNodeId();
        runtime.currentInputPortId = snapshot.getCurrentInputPortId();
        runtime.waitKind = snapshot.getWaitKind();
        runtime.waitResourceId = snapshot.getWaitResourceId();
        runtime.waitDimension = snapshot.getWaitDimension();
        runtime.waitX = snapshot.getWaitX();
        runtime.waitY = snapshot.getWaitY();
        runtime.waitZ = snapshot.getWaitZ();
        runtime.waitRadius = snapshot.getWaitRadius();
        runtime.aggregateLogic.putAll(snapshot.getLogicValues());
        runtime.targetStoryId = snapshot.getTargetStoryId();
        for (Map.Entry<String, Boolean> entry : snapshot.getExternalLogicInputs()
            .entrySet()) {
            String id = requireId(entry.getKey(), "Story Logic input port ID");
            if (entry.getValue() == null) throw failure("story.restore.logic", "Restored Story Logic input is null.");
            runtime.requireExternalInput(id);
        }
        runtime.externalLogicInputs.putAll(snapshot.getExternalLogicInputs());
        runtime.waitingConditionValue = snapshot.getWaitingConditionValue();
        for (String nodeId : snapshot.getExecutedFlowJudgmentNodeIds()) {
            CanonicalGraphNode executed = runtime.nodes.get(nodeId);
            if (executed == null || !"flow_judgment".equals(executed.getType()))
                throw failure("story.snapshot.flow_judgment", "Snapshot Flow Judgment node is unknown: " + nodeId);
            if (!runtime.executedFlowJudgmentNodeIds.add(nodeId))
                throw failure("story.snapshot.flow_judgment", "Snapshot Flow Judgment node is duplicated: " + nodeId);
        }
        runtime.recomputePublicLogic();
        runtime.validateRestoredCursor();
        return runtime;
    }

    public CanonicalStorySnapshot snapshot() {
        ensureInitialized();
        return new CanonicalStorySnapshot(
            resource.getId(),
            resourceFingerprint,
            status,
            repeatPolicy,
            triggerPortId,
            currentNodeId,
            currentInputPortId,
            waitKind,
            waitResourceId,
            waitDimension,
            waitX,
            waitY,
            waitZ,
            waitRadius,
            aggregateLogic,
            targetStoryId,
            externalLogicInputs,
            waitingConditionValue,
            executedFlowJudgmentNodeIds);
    }

    public CanonicalGraphResource getResource() {
        return resource;
    }

    public CanonicalStoryStatus getStatus() {
        ensureInitialized();
        return status;
    }

    public CanonicalStoryWaitKind getWaitKind() {
        ensureInitialized();
        return waitKind;
    }

    public String getCurrentNodeId() {
        ensureInitialized();
        return currentNodeId;
    }

    public String getWaitResourceId() {
        ensureInitialized();
        return waitResourceId;
    }

    public Integer getWaitDimension() {
        ensureInitialized();
        return waitDimension;
    }

    public Double getWaitX() {
        ensureInitialized();
        return waitX;
    }

    public Double getWaitY() {
        ensureInitialized();
        return waitY;
    }

    public Double getWaitZ() {
        ensureInitialized();
        return waitZ;
    }

    public Double getWaitRadius() {
        ensureInitialized();
        return waitRadius;
    }

    public String getTargetStoryId() {
        ensureInitialized();
        return targetStoryId;
    }

    public Map<String, Boolean> getLogicInputs() {
        return detached(externalLogicInputs);
    }

    public Map<String, Boolean> getExternalLogicInputs() {
        return detached(externalLogicInputs);
    }

    public Map<String, Boolean> getPublicLogicOutputs() {
        return detached(publicLogicOutputs);
    }

    public Map<String, Boolean> getPublicLogic() {
        return getPublicLogicOutputs();
    }

    public List<String> getExecutedFlowJudgmentNodeIds() {
        ensureInitialized();
        return Collections.unmodifiableList(new ArrayList<String>(executedFlowJudgmentNodeIds));
    }

    /** Sets one externally-owned named Logic input and resumes a waiting Condition once. */
    public boolean setLogicInput(String portId, boolean value) {
        return setLogicInputs(Collections.singletonMap(portId, Boolean.valueOf(value)));
    }

    /** Applies one coherent public Logic input snapshot before evaluating a waiting Condition. */
    public boolean setLogicInputs(Map<String, Boolean> values) {
        ensureInitialized();
        if (values == null) throw new IllegalArgumentException("Story Logic input values are required.");
        LinkedHashMap<String, Boolean> validated = new LinkedHashMap<String, Boolean>();
        for (Map.Entry<String, Boolean> entry : values.entrySet()) {
            String id = requireId(entry.getKey(), "Story Logic input port ID");
            if (entry.getValue() == null) throw new IllegalArgumentException("Story Logic input value is required.");
            requireExternalInput(id);
            validated.put(id, entry.getValue());
        }
        boolean changed = false;
        for (Map.Entry<String, Boolean> entry : validated.entrySet()) {
            Boolean previous = externalLogicInputs.put(entry.getKey(), entry.getValue());
            if (previous == null ? entry.getValue()
                .booleanValue()
                : previous.booleanValue() != entry.getValue()
                    .booleanValue())
                changed = true;
        }
        recomputePublicLogic();
        if (waitKind == CanonicalStoryWaitKind.CONDITION && changed) {
            CanonicalGraphNode condition = currentNode();
            boolean now = logicInputValue(condition, "logic_in", new HashMap<String, Boolean>());
            waitingConditionValue = Boolean.valueOf(now);
            String output = now ? "flow_true" : "flow_false";
            if (uniqueOutgoing(condition, output, CanonicalGraphInterfaceKind.FLOW) != null) {
                clearWait();
                transitionFrom(condition, output);
                resolveAutomatic();
                return true;
            }
        }
        return false;
    }

    public boolean setExternalLogicInput(String portId, boolean value) {
        return setLogicInput(portId, value);
    }

    /** Logic input supplied to the Session aggregate currently blocking the cursor. */
    public boolean getSessionActivationLogic() {
        requireWait(CanonicalStoryWaitKind.SESSION);
        return logicInputValue(currentNode(), "logic_in", new HashMap<String, Boolean>());
    }

    public Map<String, JsonElement> getPendingActionProperties() {
        requireWait(CanonicalStoryWaitKind.ACTION);
        return currentNode().getProperties();
    }

    /** Applies the already-routed, persisted completion and advances the same cursor exactly once. */
    public boolean resumeSession(CanonicalStoryPendingContinuation continuation) {
        requireWait(CanonicalStoryWaitKind.SESSION);
        if (continuation == null) throw new IllegalArgumentException("Pending Session continuation is required.");
        if (!resource.getId()
            .equals(continuation.getStoryId()) || !currentNodeId.equals(continuation.getAggregatePlacementId())
            || !waitResourceId.equals(continuation.getSessionResourceId()))
            throw failure("story.session.identity", "Session continuation does not match the waiting Story cursor.");
        requirePort(
            currentNode(),
            continuation.getSelectedEndPortId(),
            CanonicalGraphPortDirection.OUTPUT,
            CanonicalGraphInterfaceKind.FLOW);
        CanonicalGraphConnection transition = uniqueOutgoing(
            currentNode(),
            continuation.getSelectedEndPortId(),
            CanonicalGraphInterfaceKind.FLOW);
        if (transition == null || !transition.getToNodeId()
            .equals(continuation.getTargetNodeId())
            || !transition.getToPortId()
                .equals(continuation.getTargetPortId()))
            throw failure("story.session.route", "Session continuation does not match the current Story edge.");
        publishAggregateLogic(currentNode(), continuation.getPublicLogic());
        moveTo(continuation.getTargetNodeId(), continuation.getTargetPortId());
        clearWait();
        resolveAutomatic();
        return true;
    }

    /** Applies one settled Task snapshot; settled Task state remains queryable and is not consumed here. */
    public boolean resumeTask(CanonicalTaskInstanceSnapshot task) {
        requireWait(CanonicalStoryWaitKind.TASK);
        if (task == null || task.getStatus() != CanonicalTaskInstanceStatus.SETTLED)
            throw failure("story.task.unsettled", "Story can resume only from one settled Task.");
        if (!resource.getId()
            .equals(task.getStoryInstanceId()) || !currentNodeId.equals(task.getTaskNodePlacementId())
            || !waitResourceId.equals(task.getTaskResourceId()))
            throw failure("story.task.identity", "Task result does not match the waiting Story cursor.");
        publishAggregateLogic(
            currentNode(),
            task.getRuntimeSnapshot()
                .getPublicLogicOutputs());
        CanonicalGraphNode placement = currentNode();
        String resultPortId = requireId(task.getResultPortId(), "Task result port ID");
        requirePort(placement, resultPortId, CanonicalGraphPortDirection.OUTPUT, CanonicalGraphInterfaceKind.FLOW);
        clearWait();
        transitionFrom(placement, resultPortId);
        resolveAutomatic();
        return true;
    }

    /** A server action owner calls this only after the current action was durably applied or idempotently replayed. */
    public boolean completeTitle(String nodeId) {
        requireWait(CanonicalStoryWaitKind.TITLE);
        if (!currentNodeId.equals(requireId(nodeId, "Title node ID")))
            throw failure("story.title.identity", "Title acknowledgement is stale.");
        CanonicalGraphNode title = currentNode();
        clearWait();
        transitionFrom(title, "flow_out");
        resolveAutomatic();
        return true;
    }

    public boolean completeAction(String actionNodeId) {
        requireWait(CanonicalStoryWaitKind.ACTION);
        if (!currentNodeId.equals(requireId(actionNodeId, "Action node ID")))
            throw failure("story.action.identity", "Action acknowledgement does not match the waiting Story cursor.");
        CanonicalGraphNode action = currentNode();
        clearWait();
        transitionFrom(action, "flow_out");
        resolveAutomatic();
        return true;
    }

    public boolean markError() {
        ensureInitialized();
        if (status != CanonicalStoryStatus.ACTIVE) return false;
        status = CanonicalStoryStatus.ERROR;
        clearWait();
        targetStoryId = null;
        return true;
    }

    private void resolveAutomatic() {
        for (int count = 0; count < MAX_AUTOMATIC_TRANSITIONS; count++) {
            if (status != CanonicalStoryStatus.ACTIVE || waitKind != CanonicalStoryWaitKind.NONE) return;
            CanonicalGraphNode node = currentNode();
            String type = node.getType();
            if ("terminate".equals(type)) {
                status = CanonicalStoryStatus.TERMINATED;
                return;
            }
            if ("condition".equals(type)) {
                boolean value = logicInputValue(node, "logic_in", new HashMap<String, Boolean>());
                String output = value ? "flow_true" : "flow_false";
                if (uniqueOutgoing(node, output, CanonicalGraphInterfaceKind.FLOW) == null) {
                    waitKind = CanonicalStoryWaitKind.CONDITION;
                    waitingConditionValue = Boolean.valueOf(value);
                    return;
                }
                transitionFrom(node, output);
                continue;
            }
            if ("flow_judgment".equals(type)) {
                if (!executedFlowJudgmentNodeIds.contains(node.getId())) executedFlowJudgmentNodeIds.add(node.getId());
                recomputePublicLogic();
                transitionFrom(node, "flow_out");
                continue;
            }
            if ("session".equals(type)) {
                waitKind = CanonicalStoryWaitKind.SESSION;
                waitResourceId = requiredString(node, "resource_id", "story.session.resource");
                return;
            }
            if ("task".equals(type)) {
                waitKind = CanonicalStoryWaitKind.TASK;
                waitResourceId = requiredString(node, "resource_id", "story.task.resource");
                return;
            }
            if ("title".equals(type)) {
                waitKind = CanonicalStoryWaitKind.TITLE;
                return;
            }
            if ("action".equals(type)) {
                waitKind = CanonicalStoryWaitKind.ACTION;
                return;
            }
            throw failure("story.flow.node", "Node type '" + type + "' cannot own the Story Flow cursor.");
        }
        throw failure("story.runtime.cycle_guard", "Automatic Story transitions exceeded the deterministic guard.");
    }

    private boolean logicInputValue(CanonicalGraphNode node, String portId, Map<String, Boolean> visiting) {
        CanonicalGraphConnection incoming = uniqueIncoming(node, portId, CanonicalGraphInterfaceKind.LOGIC);
        if (incoming == null) return false;
        return evaluateOutput(nodes.get(incoming.getFromNodeId()), incoming.getFromPortId(), visiting);
    }

    private boolean evaluateOutput(CanonicalGraphNode node, String portId, Map<String, Boolean> visiting) {
        String endpoint = endpoint(node.getId(), portId);
        Boolean published = aggregateLogic.get(endpoint);
        if (published != null) return published.booleanValue();
        if (Boolean.TRUE.equals(visiting.get(endpoint)))
            throw failure("story.logic.cycle", "Canonical Story Logic graph contains a cycle.");
        visiting.put(endpoint, Boolean.TRUE);
        String type = node.getType();
        boolean value;
        if ("logic_input".equals(type)) value = externalLogicInputsValue(node);
        else if ("logic_output".equals(type)) value = logicInputValue(node, "logic_in", visiting);
        else if ("flow_judgment".equals(type))
            value = "executed".equals(portId) && executedFlowJudgmentNodeIds.contains(node.getId());
        else if ("and".equals(type) || "or".equals(type)) {
            boolean all = "and".equals(type);
            value = all;
            int inputs = 0;
            for (CanonicalGraphPort port : node.getPorts())
                if (port != null && port.isInput() && port.getKind() == CanonicalGraphInterfaceKind.LOGIC) {
                    inputs++;
                    boolean input = logicInputValue(node, port.getId(), visiting);
                    value = all ? value && input : value || input;
                }
            if (inputs < 2) throw failure("story.logic.input.cardinality", "And/Or requires at least two inputs.");
        } else if ("not".equals(type)) value = !logicInputValue(node, "logic_in", visiting);
        else if ("session".equals(type) || "task".equals(type)) value = false;
        else throw failure("story.logic.output", "Unsupported Story Logic source: " + endpoint);
        visiting.remove(endpoint);
        return value;
    }

    private void publishAggregateLogic(CanonicalGraphNode placement, Map<String, Boolean> values) {
        if (values == null) throw new IllegalArgumentException("Aggregate Logic values are required.");
        Set<String> expected = new HashSet<String>();
        for (CanonicalGraphPort port : placement.getPorts())
            if (port != null && port.isOutput() && port.getKind() == CanonicalGraphInterfaceKind.LOGIC)
                expected.add(port.getId());
        if (!expected.equals(values.keySet()))
            throw failure("story.aggregate.logic", "Aggregate public Logic values do not match the Story placement.");
        for (Map.Entry<String, Boolean> entry : values.entrySet()) {
            if (entry.getValue() == null) throw failure("story.aggregate.logic", "Aggregate Logic value is null.");
            aggregateLogic.put(endpoint(placement.getId(), entry.getKey()), entry.getValue());
        }
        recomputePublicLogic();
    }

    private void setInitialLogicInputs(Map<String, Boolean> values) {
        if (values == null) throw new IllegalArgumentException("Story Logic inputs are required.");
        for (Map.Entry<String, Boolean> entry : values.entrySet()) {
            String id = requireId(entry.getKey(), "Story Logic input port ID");
            if (entry.getValue() == null) throw new IllegalArgumentException("Story Logic input value is required.");
            requireExternalInput(id);
            externalLogicInputs.put(id, entry.getValue());
        }
        recomputePublicLogic();
    }

    private void requireExternalInput(String id) {
        boolean found = false;
        for (CanonicalGraphNode node : nodes.values())
            if ("logic_input".equals(node.getType()) && id.equals(requiredString(node, "port_id", "story.logic_input")))
                found = true;
        if (!found) throw failure("story.logic_input.missing", "Unknown Story Logic input: " + id);
    }

    private boolean externalLogicInputsValue(CanonicalGraphNode node) {
        String id = requiredString(node, "port_id", "story.logic_input");
        Boolean value = externalLogicInputs.get(id);
        return value != null && value.booleanValue();
    }

    private void recomputePublicLogic() {
        publicLogicOutputs.clear();
        for (CanonicalGraphNode node : nodes.values()) if ("logic_output".equals(node.getType())) {
            String id = requiredString(node, "port_id", "story.logic_output");
            publicLogicOutputs
                .put(id, Boolean.valueOf(logicInputValue(node, "logic_in", new HashMap<String, Boolean>())));
        }
    }

    private CanonicalStoryStartConfiguration.Trigger startTrigger(String portId) {
        for (CanonicalStoryStartConfiguration.Trigger trigger : CanonicalStoryStartConfiguration.parse(resource)
            .getTriggers()) if (portId.equals(trigger.getPortId())) return trigger;
        throw failure("story.start.trigger.port", "Unknown Story Start trigger: " + portId);
    }

    /** Legacy resources had no Start metadata; retain their explicit trigger-port compatibility path. */
    private CanonicalStoryStartConfiguration.Trigger startTriggerIfConfigured(String portId) {
        CanonicalGraphNode start = uniqueNode("start");
        if (start.getProperties()
            .isEmpty()) return null;
        return startTrigger(portId);
    }

    private Map<String, Boolean> detached(Map<String, Boolean> source) {
        return Collections.unmodifiableMap(new LinkedHashMap<String, Boolean>(source));
    }

    private void transitionFrom(CanonicalGraphNode node, String portId) {
        requirePort(node, portId, CanonicalGraphPortDirection.OUTPUT, CanonicalGraphInterfaceKind.FLOW);
        CanonicalGraphConnection transition = uniqueOutgoing(node, portId, CanonicalGraphInterfaceKind.FLOW);
        if (transition == null)
            throw failure("story.flow.unconnected", "Flow output is not connected: " + endpoint(node.getId(), portId));
        moveTo(transition.getToNodeId(), transition.getToPortId());
    }

    private void moveTo(String nodeId, String portId) {
        CanonicalGraphNode target = nodes.get(requireId(nodeId, "Story target node ID"));
        if (target == null) throw failure("story.flow.target", "Story target node does not exist: " + nodeId);
        requirePort(target, portId, CanonicalGraphPortDirection.INPUT, CanonicalGraphInterfaceKind.FLOW);
        currentNodeId = nodeId;
        currentInputPortId = portId;
    }

    private void validateGraph() {
        CanonicalGraph graph = resource.getGraph();
        if (graph == null) throw failure("story.graph.missing", "Canonical Story graph is required.");
        int starts = 0;
        for (CanonicalGraphNode node : graph.getNodes()) {
            if ("start".equals(node.getType())) starts++;
            if (!FLOW_TYPES.contains(node.getType()) && !LOGIC_TYPES.contains(node.getType()))
                throw failure("story.node.unsupported", "Unsupported canonical Story node type: " + node.getType());
            validatePorts(node);
            validateNodeShape(node);
        }
        if (starts != 1) throw failure("story.start.cardinality", "Canonical Story requires exactly one Start node.");
        Map<String, Integer> flowOutgoing = new HashMap<String, Integer>();
        Map<String, Integer> logicIncoming = new HashMap<String, Integer>();
        for (CanonicalGraphConnection edge : graph.getConnections()) {
            if (edge == null) throw failure("story.edge.null", "Canonical Story contains a null connection.");
            CanonicalGraphNode from = nodes.get(edge.getFromNodeId());
            CanonicalGraphNode to = nodes.get(edge.getToNodeId());
            if (from == null || to == null) throw failure("story.edge.node", "Connection endpoint node is missing.");
            requirePort(from, edge.getFromPortId(), CanonicalGraphPortDirection.OUTPUT, edge.getInterfaceKind());
            requirePort(to, edge.getToPortId(), CanonicalGraphPortDirection.INPUT, edge.getInterfaceKind());
            if (edge.getInterfaceKind() == CanonicalGraphInterfaceKind.FLOW) {
                String key = endpoint(edge.getFromNodeId(), edge.getFromPortId());
                int count = integer(flowOutgoing.get(key)) + 1;
                flowOutgoing.put(key, Integer.valueOf(count));
                if (count > 1) throw failure("story.flow.output.multiple_targets", "Flow output has multiple targets.");
            } else {
                String key = endpoint(edge.getToNodeId(), edge.getToPortId());
                int count = integer(logicIncoming.get(key)) + 1;
                logicIncoming.put(key, Integer.valueOf(count));
                if (count > 1) throw failure("story.logic.input.multiple_sources", "Logic input has multiple sources.");
            }
        }
        Set<String> inputIds = new HashSet<String>();
        Set<String> outputIds = new HashSet<String>();
        for (CanonicalGraphNode node : nodes.values()) {
            if ("logic_input".equals(node.getType()))
                inputIds.add(requiredString(node, "port_id", "story.logic_input"));
            if ("logic_output".equals(node.getType()))
                outputIds.add(requiredString(node, "port_id", "story.logic_output"));
        }
        if (inputIds.size() != countType("logic_input"))
            throw failure("story.logic_input.duplicate", "Story Logic input port_id is duplicated.");
        if (outputIds.size() != countType("logic_output"))
            throw failure("story.logic_output.duplicate", "Story Logic output port_id is duplicated.");
        validateLogicAcyclic();
    }

    private int countType(String type) {
        int count = 0;
        for (CanonicalGraphNode node : nodes.values()) if (type.equals(node.getType())) count++;
        return count;
    }

    private void validateNodeShape(CanonicalGraphNode node) {
        String type = node.getType();
        if ("start".equals(type)) {
            int outputs = 0;
            for (CanonicalGraphPort port : node.getPorts()) {
                if (port.isOutput() && port.getKind() == CanonicalGraphInterfaceKind.FLOW) outputs++;
                else if (!port.isInput() || port.getKind() != CanonicalGraphInterfaceKind.LOGIC)
                    throw failure("story.start.port", "Start may contain Flow outputs and Logic inputs.");
            }
            if (outputs < 1)
                throw failure("story.start.trigger.required", "Start requires at least one trigger output.");
        } else if ("terminate".equals(type)) {
            requirePort(node, "flow_in", CanonicalGraphPortDirection.INPUT, CanonicalGraphInterfaceKind.FLOW);
        } else if ("condition".equals(type)) {
            requirePort(node, "flow_in", CanonicalGraphPortDirection.INPUT, CanonicalGraphInterfaceKind.FLOW);
            requirePort(node, "logic_in", CanonicalGraphPortDirection.INPUT, CanonicalGraphInterfaceKind.LOGIC);
            requirePort(node, "flow_true", CanonicalGraphPortDirection.OUTPUT, CanonicalGraphInterfaceKind.FLOW);
            requirePort(node, "flow_false", CanonicalGraphPortDirection.OUTPUT, CanonicalGraphInterfaceKind.FLOW);
        } else if ("flow_judgment".equals(type)) {
            requirePort(node, "flow_in", CanonicalGraphPortDirection.INPUT, CanonicalGraphInterfaceKind.FLOW);
            requirePort(node, "flow_out", CanonicalGraphPortDirection.OUTPUT, CanonicalGraphInterfaceKind.FLOW);
            requirePort(node, "executed", CanonicalGraphPortDirection.OUTPUT, CanonicalGraphInterfaceKind.LOGIC);
        } else if ("session".equals(type)) {
            requirePort(node, "flow_in", CanonicalGraphPortDirection.INPUT, CanonicalGraphInterfaceKind.FLOW);
            requiredString(node, "resource_id", "story.session.resource");
            validateAggregatePorts(node);
        } else if ("task".equals(type)) {
            requirePort(node, "flow_in", CanonicalGraphPortDirection.INPUT, CanonicalGraphInterfaceKind.FLOW);
            requiredString(node, "resource_id", "story.task.resource");
            validateAggregatePorts(node);
        } else if ("title".equals(type)) {
            requirePort(node, "flow_in", CanonicalGraphPortDirection.INPUT, CanonicalGraphInterfaceKind.FLOW);
            requirePort(node, "flow_out", CanonicalGraphPortDirection.OUTPUT, CanonicalGraphInterfaceKind.FLOW);
            if (node.getPorts()
                .size() != 2) throw failure("story.title.ports", "Title requires one Flow input and output.");
            try {
                CanonicalTitleConfiguration.parse(node.getProperties());
            } catch (IllegalArgumentException exception) {
                throw failure("story.title.invalid", exception.getMessage());
            }
        } else if ("action".equals(type)) {
            requirePort(node, "flow_in", CanonicalGraphPortDirection.INPUT, CanonicalGraphInterfaceKind.FLOW);
            requirePort(node, "flow_out", CanonicalGraphPortDirection.OUTPUT, CanonicalGraphInterfaceKind.FLOW);
        } else if ("not".equals(type)) {
            requirePort(node, "logic_in", CanonicalGraphPortDirection.INPUT, CanonicalGraphInterfaceKind.LOGIC);
            requirePort(node, "logic_out", CanonicalGraphPortDirection.OUTPUT, CanonicalGraphInterfaceKind.LOGIC);
        } else if ("and".equals(type) || "or".equals(type)) {
            requirePort(node, "logic_out", CanonicalGraphPortDirection.OUTPUT, CanonicalGraphInterfaceKind.LOGIC);
            int inputs = 0;
            for (CanonicalGraphPort port : node.getPorts())
                if (port.isInput() && port.getKind() == CanonicalGraphInterfaceKind.LOGIC) inputs++;
            if (inputs < 2) throw failure("story.logic.input.cardinality", "And/Or requires at least two inputs.");
        } else if ("logic_input".equals(type)) {
            requirePort(node, "logic_out", CanonicalGraphPortDirection.OUTPUT, CanonicalGraphInterfaceKind.LOGIC);
            requiredString(node, "port_id", "story.logic_input");
            requiredString(node, "display_name", "story.logic_input");
        } else if ("logic_output".equals(type)) {
            requirePort(node, "logic_in", CanonicalGraphPortDirection.INPUT, CanonicalGraphInterfaceKind.LOGIC);
            requiredString(node, "port_id", "story.logic_output");
            requiredString(node, "display_name", "story.logic_output");
        }
    }

    private void validateAggregatePorts(CanonicalGraphNode node) {
        for (CanonicalGraphPort port : node.getPorts())
            if (port != null && port.getId() != null && !"flow_in".equals(port.getId())) {
                if (port.getKind() == CanonicalGraphInterfaceKind.FLOW && !port.isOutput())
                    throw failure("story.aggregate.port", "Aggregate Flow ports besides flow_in must be outputs.");
                if (port.getKind() != CanonicalGraphInterfaceKind.FLOW
                    && port.getKind() != CanonicalGraphInterfaceKind.LOGIC)
                    throw failure("story.aggregate.port", "Aggregate ports must be Flow or Logic.");
            }
    }

    private void validatePorts(CanonicalGraphNode node) {
        Set<String> ids = new HashSet<String>();
        Set<String> orders = new HashSet<String>();
        for (CanonicalGraphPort port : node.getPorts()) {
            if (port == null || blank(port.getId())
                || blank(port.getDisplayName())
                || port.getDirection() == null
                || port.getKind() == null
                || port.getOrder() < 0) throw failure("story.port.invalid", "Invalid canonical Story port.");
            if (!ids.add(port.getId()))
                throw failure("story.port.duplicate", "Duplicate port ID on node " + node.getId());
            String orderKey = port.getDirection()
                .name() + ':'
                + port.getOrder();
            if (!orders.add(orderKey)) throw failure(
                "story.port.order",
                "Duplicate port order for " + port.getDirection()
                    .name()
                    .toLowerCase() + " ports on node " + node.getId());
        }
    }

    private void validateLogicAcyclic() {
        Map<String, Integer> state = new HashMap<String, Integer>();
        for (CanonicalGraphConnection edge : resource.getGraph()
            .getConnections())
            if (edge.getInterfaceKind() == CanonicalGraphInterfaceKind.LOGIC) visitLogic(edge.getFromNodeId(), state);
    }

    private void visitLogic(String nodeId, Map<String, Integer> state) {
        Integer known = state.get(nodeId);
        if (known != null && known.intValue() == 1)
            throw failure("story.logic.cycle", "Canonical Story Logic graph contains a cycle.");
        if (known != null && known.intValue() == 2) return;
        state.put(nodeId, Integer.valueOf(1));
        for (CanonicalGraphConnection edge : resource.getGraph()
            .getConnections())
            if (edge.getInterfaceKind() == CanonicalGraphInterfaceKind.LOGIC && nodeId.equals(edge.getFromNodeId()))
                visitLogic(edge.getToNodeId(), state);
        state.put(nodeId, Integer.valueOf(2));
    }

    private Map<String, CanonicalGraphNode> indexNodes(CanonicalGraph graph) {
        if (graph == null || graph.getNodes() == null || graph.getConnections() == null)
            throw failure("story.graph.missing", "Canonical Story graph is required.");
        LinkedHashMap<String, CanonicalGraphNode> result = new LinkedHashMap<String, CanonicalGraphNode>();
        for (CanonicalGraphNode node : graph.getNodes()) {
            if (node == null || blank(node.getId()) || blank(node.getType()) || blank(node.getDisplayName()))
                throw failure("story.node.invalid", "Invalid canonical Story node.");
            if (result.put(node.getId(), node) != null)
                throw failure("story.node.duplicate", "Duplicate canonical Story node ID: " + node.getId());
        }
        return result;
    }

    private void validateRestoredCursor() {
        ensureInitialized();
        uniqueNode("start");
        if (status == CanonicalStoryStatus.ACTIVE) {
            CanonicalGraphNode current = currentNode();
            requirePort(
                current,
                currentInputPortId,
                CanonicalGraphPortDirection.INPUT,
                CanonicalGraphInterfaceKind.FLOW);
            if (waitKind == CanonicalStoryWaitKind.SESSION && !"session".equals(current.getType()))
                throw failure("story.restore.wait", "Restored Session wait is not on a Session placement.");
            if (waitKind == CanonicalStoryWaitKind.TASK && !"task".equals(current.getType()))
                throw failure("story.restore.wait", "Restored Task wait is not on a Task placement.");
            if (waitKind == CanonicalStoryWaitKind.TITLE && !"title".equals(current.getType()))
                throw failure("story.restore.wait", "Restored Title wait is not on a Title node.");
            if (waitKind == CanonicalStoryWaitKind.ACTION && !"action".equals(current.getType()))
                throw failure("story.restore.wait", "Restored Action wait is not on an Action node.");
            if (waitKind == CanonicalStoryWaitKind.CONDITION) {
                if (!"condition".equals(current.getType()) || waitingConditionValue == null)
                    throw failure("story.restore.wait", "Restored Condition wait is invalid.");
                boolean currentValue = logicInputValue(current, "logic_in", new HashMap<String, Boolean>());
                String output = currentValue ? "flow_true" : "flow_false";
                if (currentValue != waitingConditionValue.booleanValue()
                    || uniqueOutgoing(current, output, CanonicalGraphInterfaceKind.FLOW) != null)
                    throw failure("story.restore.wait", "Restored Condition wait is stale.");
            } else if (waitingConditionValue != null) {
                throw failure("story.restore.wait", "Only a Condition may retain a wait value.");
            }
        }
        for (String endpoint : aggregateLogic.keySet()) {
            int separator = endpoint.indexOf('\u0000');
            if (separator <= 0 || separator == endpoint.length() - 1)
                throw failure("story.restore.logic", "Malformed restored aggregate Logic endpoint.");
            CanonicalGraphNode node = nodes.get(endpoint.substring(0, separator));
            if (node == null) throw failure("story.restore.logic", "Restored aggregate Logic node is missing.");
            requirePort(
                node,
                endpoint.substring(separator + 1),
                CanonicalGraphPortDirection.OUTPUT,
                CanonicalGraphInterfaceKind.LOGIC);
        }
    }

    private CanonicalGraphConnection uniqueOutgoing(CanonicalGraphNode node, String portId,
        CanonicalGraphInterfaceKind kind) {
        CanonicalGraphConnection found = null;
        for (CanonicalGraphConnection edge : resource.getGraph()
            .getConnections())
            if (node.getId()
                .equals(edge.getFromNodeId()) && portId.equals(edge.getFromPortId())
                && kind == edge.getInterfaceKind()) {
                    if (found != null)
                        throw failure("story.flow.output.multiple_targets", "Flow output has multiple targets.");
                    found = edge;
                }
        return found;
    }

    private CanonicalGraphConnection uniqueIncoming(CanonicalGraphNode node, String portId,
        CanonicalGraphInterfaceKind kind) {
        CanonicalGraphConnection found = null;
        for (CanonicalGraphConnection edge : resource.getGraph()
            .getConnections())
            if (node.getId()
                .equals(edge.getToNodeId()) && portId.equals(edge.getToPortId())
                && kind == edge.getInterfaceKind()) {
                    if (found != null)
                        throw failure("story.logic.input.multiple_sources", "Logic input has multiple sources.");
                    found = edge;
                }
        return found;
    }

    private CanonicalGraphNode uniqueNode(String type) {
        CanonicalGraphNode found = null;
        for (CanonicalGraphNode node : nodes.values()) if (type.equals(node.getType())) {
            if (found != null)
                throw failure("story." + type + ".cardinality", "Canonical Story has multiple " + type + " nodes.");
            found = node;
        }
        if (found == null) throw failure("story." + type + ".missing", "Canonical Story is missing " + type + ".");
        return found;
    }

    private CanonicalGraphNode currentNode() {
        CanonicalGraphNode node = nodes.get(currentNodeId);
        if (node == null) throw failure("story.cursor.node", "Story cursor node no longer exists.");
        return node;
    }

    private CanonicalGraphPort requirePort(CanonicalGraphNode node, String portId,
        CanonicalGraphPortDirection direction, CanonicalGraphInterfaceKind kind) {
        CanonicalGraphPort found = null;
        for (CanonicalGraphPort port : node.getPorts()) if (portId.equals(port.getId())) {
            if (found != null) throw failure("story.port.duplicate", "Duplicate port ID on node " + node.getId());
            found = port;
        }
        if (found == null || found.getDirection() != direction || found.getKind() != kind)
            throw failure("story.port.shape", "Missing or incompatible port: " + endpoint(node.getId(), portId));
        return found;
    }

    private String requiredString(CanonicalGraphNode node, String key, String code) {
        JsonElement value = node.getProperties()
            .get(key);
        if (value == null || !value.isJsonPrimitive()
            || !value.getAsJsonPrimitive()
                .isString())
            throw failure(code, "Required Story property is missing: " + key);
        return requireId(value.getAsString(), "Story property " + key);
    }

    private int requiredInteger(CanonicalGraphNode node, String key, String code) {
        JsonElement value = node.getProperties()
            .get(key);
        if (value == null || !value.isJsonPrimitive()
            || !value.getAsJsonPrimitive()
                .isNumber())
            throw failure(code, "Required integer Story property is missing: " + key);
        double number = value.getAsDouble();
        int result = value.getAsInt();
        if (Double.isNaN(number) || Double.isInfinite(number) || number != result)
            throw failure(code, "Required integer Story property is invalid: " + key);
        return result;
    }

    private double requiredFinite(CanonicalGraphNode node, String key, String code) {
        JsonElement value = node.getProperties()
            .get(key);
        if (value == null || !value.isJsonPrimitive()
            || !value.getAsJsonPrimitive()
                .isNumber())
            throw failure(code, "Required numeric Story property is missing: " + key);
        double result = value.getAsDouble();
        if (!finite(result)) throw failure(code, "Required finite Story property is invalid: " + key);
        return result;
    }

    private double requiredPositiveFinite(CanonicalGraphNode node, String key, String code) {
        double result = requiredFinite(node, key, code);
        if (result <= 0D) throw failure(code, "Required positive Story property is invalid: " + key);
        return result;
    }

    private void requireWait(CanonicalStoryWaitKind expected) {
        ensureInitialized();
        if (status != CanonicalStoryStatus.ACTIVE || waitKind != expected)
            throw failure("story.wait.state", "Story cursor is not waiting for " + expected + ".");
    }

    private void clearWait() {
        waitKind = CanonicalStoryWaitKind.NONE;
        waitResourceId = null;
        waitDimension = null;
        waitX = null;
        waitY = null;
        waitZ = null;
        waitRadius = null;
        waitingConditionValue = null;
    }

    private void ensureInitialized() {
        if (status == null || repeatPolicy == null || triggerPortId == null || waitKind == null)
            throw new IllegalStateException("Canonical Story runtime is not initialized.");
    }

    private static void validateEnvelope(CanonicalGraphResource resource) {
        if (resource == null || resource.getSchemaVersion() != CanonicalGraphResource.CURRENT_SCHEMA_VERSION
            || resource.getResourceKind() != CanonicalGraphResourceKind.STORY
            || blank(resource.getId())
            || blank(resource.getDisplayName())
            || resource.getGraph() == null)
            throw failure("story.resource.invalid", "Invalid canonical Story resource.");
    }

    private static CanonicalStoryRepeatPolicy requirePolicy(CanonicalStoryRepeatPolicy value) {
        if (value == null) throw new IllegalArgumentException("Story repeat policy is required.");
        return value;
    }

    private static String requireId(String value, String label) {
        if (blank(value)) throw new IllegalArgumentException(label + " is required.");
        return value;
    }

    private static String endpoint(String nodeId, String portId) {
        return nodeId + "\u0000" + portId;
    }

    private static int integer(Integer value) {
        return value == null ? 0 : value.intValue();
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }

    private static boolean finite(double value) {
        return !Double.isNaN(value) && !Double.isInfinite(value);
    }

    private static Set<String> set(String... values) {
        Set<String> result = new HashSet<String>();
        for (String value : values) result.add(value);
        return result;
    }

    private static CanonicalGraphResourceException failure(String code, String message) {
        return new CanonicalGraphResourceException(code, message);
    }

    private static String fingerprint(CanonicalGraphResource resource) {
        StringBuilder text = new StringBuilder();
        text.append(resource.getSchemaVersion())
            .append('|')
            .append(resource.getResourceKind())
            .append('|')
            .append(resource.getId())
            .append('|')
            .append(resource.getDisplayName());
        for (CanonicalGraphNode node : resource.getGraph()
            .getNodes()) {
            text.append("|n:")
                .append(node.getId())
                .append(':')
                .append(node.getType())
                .append(':')
                .append(node.getDisplayName());
            for (CanonicalGraphPort port : node.getPorts()) text.append("|p:")
                .append(port.getId())
                .append(':')
                .append(port.getDisplayName())
                .append(':')
                .append(port.getDirection())
                .append(':')
                .append(port.getKind())
                .append(':')
                .append(port.getOrder());
            for (Map.Entry<String, JsonElement> property : node.getProperties()
                .entrySet())
                text.append("|v:")
                    .append(property.getKey())
                    .append(':')
                    .append(property.getValue());
        }
        for (CanonicalGraphConnection edge : resource.getGraph()
            .getConnections())
            text.append("|e:")
                .append(edge.getFromNodeId())
                .append(':')
                .append(edge.getFromPortId())
                .append("->")
                .append(edge.getToNodeId())
                .append(':')
                .append(edge.getToPortId())
                .append(':')
                .append(edge.getInterfaceKind());
        try {
            byte[] digest = MessageDigest.getInstance("SHA-256")
                .digest(
                    text.toString()
                        .getBytes(Charset.forName("UTF-8")));
            StringBuilder result = new StringBuilder();
            for (byte value : digest) result.append(String.format("%02x", Integer.valueOf(value & 0xff)));
            return result.toString();
        } catch (NoSuchAlgorithmException impossible) {
            throw new IllegalStateException("SHA-256 is unavailable.", impossible);
        }
    }
}
