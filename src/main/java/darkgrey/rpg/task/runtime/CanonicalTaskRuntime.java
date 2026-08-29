package darkgrey.rpg.task.runtime;

import java.nio.charset.Charset;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.HashMap;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;

import com.google.gson.JsonElement;
import com.google.gson.JsonObject;

import darkgrey.rpg.graph.canonical.CanonicalGraph;
import darkgrey.rpg.graph.canonical.CanonicalGraphConnection;
import darkgrey.rpg.graph.canonical.CanonicalGraphInterfaceKind;
import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphPort;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceException;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceKind;

/** Pure Java 8 execution core for one canonical Task resource. */
public final class CanonicalTaskRuntime {

    private static final String ACTIVATE = "activate";
    private static final String OBJECTIVE = "objective";
    private static final String AND = "and";
    private static final String OR = "or";
    private static final String NOT = "not";
    private static final String LOGIC_OUTPUT = "logic_output";
    private static final String SETTLE = "settle";

    private final CanonicalGraphResource resource;
    private final String resourceFingerprint;
    private final Map<String, CanonicalGraphNode> nodes;
    private final Map<String, CanonicalGraphNode> objectives = new LinkedHashMap<String, CanonicalGraphNode>();
    private final Map<String, CanonicalGraphPort> ports = new HashMap<String, CanonicalGraphPort>();
    private final Map<String, CanonicalGraphConnection> incoming = new HashMap<String, CanonicalGraphConnection>();
    private final List<CanonicalGraphPort> settlementSlots = new ArrayList<CanonicalGraphPort>();
    private final Map<String, Integer> progress = new LinkedHashMap<String, Integer>();
    private final Map<String, CanonicalTaskObjectiveStatus> objectiveStatuses = new LinkedHashMap<String, CanonicalTaskObjectiveStatus>();
    private final Map<String, Boolean> logicValues = new LinkedHashMap<String, Boolean>();
    private final Map<String, Boolean> publicLogicOutputs = new LinkedHashMap<String, Boolean>();
    private boolean activationLogic;
    private CanonicalTaskStatus status;
    private String resultPortId;

    private CanonicalTaskRuntime(CanonicalGraphResource resource, boolean initialize) {
        validateResourceEnvelope(resource);
        this.resource = resource;
        this.nodes = indexAndValidateNodes(resource.getGraph());
        validateEdges(resource.getGraph());
        this.resourceFingerprint = fingerprint(resource);
        if (initialize) initialize();
    }

    public static CanonicalTaskRuntime start(CanonicalGraphResource resource) {
        return new CanonicalTaskRuntime(resource, true);
    }

    public static CanonicalTaskRuntime begin(CanonicalGraphResource resource) {
        return start(resource);
    }

    public static CanonicalTaskRuntime restore(CanonicalGraphResource resource, CanonicalTaskSnapshot snapshot) {
        if (snapshot == null) throw failure("task.snapshot.required", "Task snapshot is required.");
        CanonicalTaskRuntime runtime = new CanonicalTaskRuntime(resource, false);
        runtime.restoreSnapshot(snapshot);
        return runtime;
    }

    public CanonicalGraphResource getResource() {
        return resource;
    }

    public CanonicalTaskStatus getStatus() {
        return status;
    }

    public CanonicalTaskStatus status() {
        return status;
    }

    public boolean isActive() {
        return status == CanonicalTaskStatus.ACTIVE;
    }

    public boolean isSettled() {
        return status == CanonicalTaskStatus.SETTLED;
    }

    public boolean getActivationLogic() {
        return activationLogic;
    }

    public String getResultPortId() {
        return resultPortId;
    }

    public String getSettlementResult() {
        return resultPortId;
    }

    public String getResult() {
        return resultPortId;
    }

    public Map<String, Integer> getProgress() {
        return detached(progress);
    }

    public Map<String, Integer> getObjectiveProgress() {
        return getProgress();
    }

    public Map<String, CanonicalTaskObjectiveStatus> getObjectiveStatuses() {
        return detached(objectiveStatuses);
    }

    public Map<String, CanonicalTaskObjectiveStatus> getObjectiveStatus() {
        return getObjectiveStatuses();
    }

    public Map<String, Boolean> getLogicValues() {
        return detached(logicValues);
    }

    public Map<String, Boolean> getInternalLogicValues() {
        return getLogicValues();
    }

    public Map<String, Boolean> getPublicLogicOutputs() {
        return detached(publicLogicOutputs);
    }

    public Map<String, Boolean> getPublicLogic() {
        return getPublicLogicOutputs();
    }

    /** Applies exactly one targeted event. Returns whether at least one active objective changed. */
    public boolean accept(CanonicalTaskEvent event) {
        if (event == null) throw failure("task.event.required", "Task event is required.");
        if (!isActive()) return false;
        List<String> activeBefore = new ArrayList<String>();
        for (Map.Entry<String, CanonicalTaskObjectiveStatus> entry : objectiveStatuses.entrySet())
            if (entry.getValue() == CanonicalTaskObjectiveStatus.ACTIVE) activeBefore.add(entry.getKey());
        boolean changed = false;
        for (String id : activeBefore) {
            CanonicalGraphNode node = objectives.get(id);
            if (matches(node, event)) {
                int oldValue = progress.get(id)
                    .intValue();
                int required = required(node);
                int remaining = required - oldValue;
                int next = event.getAmount() >= remaining ? required : oldValue + event.getAmount();
                if (next != oldValue) {
                    progress.put(id, Integer.valueOf(next));
                    if (next >= required) objectiveStatuses.put(id, CanonicalTaskObjectiveStatus.COMPLETED);
                    changed = true;
                }
            }
        }
        if (changed) refreshState();
        return changed;
    }

    public boolean handle(CanonicalTaskEvent event) {
        return accept(event);
    }

    public boolean applyEvent(CanonicalTaskEvent event) {
        return accept(event);
    }

    public boolean acceptEvent(CanonicalTaskEvent event) {
        return accept(event);
    }

    public boolean isObjectiveActive(String objectiveId) {
        return objectiveStatuses.get(objectiveId) == CanonicalTaskObjectiveStatus.ACTIVE;
    }

    public CanonicalTaskSnapshot snapshot() {
        return new CanonicalTaskSnapshot(
            resource.getId(),
            resourceFingerprint,
            status,
            progress,
            objectiveStatuses,
            logicValues,
            publicLogicOutputs,
            activationLogic,
            resultPortId);
    }

    public CanonicalTaskSnapshot createSnapshot() {
        return snapshot();
    }

    private void initialize() {
        status = CanonicalTaskStatus.ACTIVE;
        activationLogic = true;
        for (String id : objectives.keySet()) {
            progress.put(id, Integer.valueOf(0));
            objectiveStatuses.put(id, CanonicalTaskObjectiveStatus.INACTIVE);
        }
        refreshState();
    }

    private void refreshState() {
        for (int pass = 0; pass <= nodes.size(); pass++) {
            recomputeLogic();
            boolean changed = false;
            if (isActive()) for (Map.Entry<String, CanonicalGraphNode> entry : objectives.entrySet()) {
                if (objectiveStatuses.get(entry.getKey()) == CanonicalTaskObjectiveStatus.INACTIVE
                    && logicInputValue(entry.getValue(), "logic_enable", new HashMap<String, Boolean>())) {
                    objectiveStatuses.put(entry.getKey(), CanonicalTaskObjectiveStatus.ACTIVE);
                    changed = true;
                }
            }
            if (!changed) break;
        }
        recomputeLogic();
        if (isActive()) settleIfReady();
    }

    private void settleIfReady() {
        for (CanonicalGraphPort slot : settlementSlots) {
            if (logicInputValue(node("settle"), slot.getId(), new HashMap<String, Boolean>())) {
                resultPortId = slot.getId();
                Map<String, Boolean> finalPublic = detached(publicLogicOutputs);
                status = CanonicalTaskStatus.SETTLED;
                activationLogic = false;
                recomputeLogic();
                publicLogicOutputs.clear();
                publicLogicOutputs.putAll(finalPublic);
                return;
            }
        }
    }

    private void recomputeLogic() {
        logicValues.clear();
        publicLogicOutputs.clear();
        Map<String, Boolean> visiting = new HashMap<String, Boolean>();
        for (CanonicalGraphNode node : nodes.values()) for (CanonicalGraphPort port : node.getPorts())
            if (port.isOutput() && port.getKind() == CanonicalGraphInterfaceKind.LOGIC)
                evaluateOutput(node, port.getId(), visiting);
        for (CanonicalGraphNode node : nodes.values()) if (LOGIC_OUTPUT.equals(node.getType())) publicLogicOutputs.put(
            requiredString(node, "port_id", "task.logic_output"),
            Boolean.valueOf(logicInputValue(node, "logic_in", new HashMap<String, Boolean>())));
    }

    private boolean evaluateOutput(CanonicalGraphNode node, String portId, Map<String, Boolean> visiting) {
        String key = endpoint(node.getId(), portId);
        Boolean known = logicValues.get(key);
        if (known != null) return known.booleanValue();
        if (Boolean.TRUE.equals(visiting.get(key))) throw failure("task.logic.cycle", "Logic graph contains a cycle.");
        visiting.put(key, Boolean.TRUE);
        String type = node.getType();
        boolean value;
        if (ACTIVATE.equals(type)) value = activationLogic;
        else if (OBJECTIVE.equals(type))
            value = objectiveStatuses.get(node.getId()) == CanonicalTaskObjectiveStatus.COMPLETED;
        else if (AND.equals(type) || OR.equals(type)) {
            boolean and = AND.equals(type);
            value = and;
            for (CanonicalGraphPort port : node.getPorts())
                if (port.isInput()) value = and ? value && logicInputValue(node, port.getId(), visiting)
                    : value || logicInputValue(node, port.getId(), visiting);
        } else if (NOT.equals(type)) value = !logicInputValue(node, "logic_in", visiting);
        else throw failure("task.logic.output.invalid", "Invalid Logic output " + key + ".");
        visiting.remove(key);
        logicValues.put(key, Boolean.valueOf(value));
        return value;
    }

    private boolean logicInputValue(CanonicalGraphNode node, String portId, Map<String, Boolean> visiting) {
        CanonicalGraphConnection edge = incoming.get(endpoint(node.getId(), portId));
        if (edge == null) return false;
        return evaluateOutput(nodes.get(edge.getFromNodeId()), edge.getFromPortId(), visiting);
    }

    private Map<String, CanonicalGraphNode> indexAndValidateNodes(CanonicalGraph graph) {
        if (graph == null || graph.getNodes() == null || graph.getConnections() == null)
            throw failure("task.graph.required", "Task graph is required.");
        Map<String, CanonicalGraphNode> result = new LinkedHashMap<String, CanonicalGraphNode>();
        int activateCount = 0;
        int settleCount = 0;
        Set<String> publicIds = new HashSet<String>();
        Set<String> publicNames = new HashSet<String>();
        for (CanonicalGraphNode node : graph.getNodes()) {
            if (node == null || blank(node.getId()) || node.getType() == null || node.getPorts() == null)
                throw failure("task.node.invalid", "Task node is incomplete.");
            if (result.put(node.getId(), node) != null)
                throw failure("task.node.id.duplicate", "Duplicate Task node ID.");
            if (ACTIVATE.equals(node.getType())) activateCount++;
            if (SETTLE.equals(node.getType())) settleCount++;
            validateNodeShape(node);
            for (CanonicalGraphPort port : node.getPorts()) {
                if (port == null || blank(port.getId())) throw failure("task.port.invalid", "Task port is incomplete.");
                if (port.getKind() != CanonicalGraphInterfaceKind.LOGIC)
                    throw failure("task.port.kind", "Task resources are Logic-only.");
                if (ports.put(endpoint(node.getId(), port.getId()), port) != null)
                    throw failure("task.port.duplicate", "Duplicate Task port.");
            }
            if (LOGIC_OUTPUT.equals(node.getType())) {
                String publicId = requiredString(node, "port_id", "task.logic_output");
                String publicName = requiredString(node, "display_name", "task.logic_output");
                if ("flow_in".equals(publicId) || "logic_in".equals(publicId))
                    throw failure("task.public_port.id.reserved", "Reserved public port ID.");
                if (!publicIds.add(publicId))
                    throw failure("task.public_port.id.duplicate", "Duplicate public Logic port ID.");
                if (!publicNames.add(publicName))
                    throw failure("task.public_port.display_name.duplicate", "Duplicate public Logic display name.");
            }
        }
        if (activateCount != 1) throw failure("task.node.activate.unique", "Task requires exactly one activate node.");
        if (settleCount != 1) throw failure("task.node.settle.unique", "Task requires exactly one settle node.");
        for (CanonicalGraphPort slot : settlementSlots) {
            if ("flow_in".equals(slot.getId()) || "logic_in".equals(slot.getId()))
                throw failure("task.public_port.id.reserved", "Reserved public port ID.");
            for (CanonicalGraphNode node : result.values()) if (LOGIC_OUTPUT.equals(node.getType()) && slot.getId()
                .equals(requiredString(node, "port_id", "task.logic_output")))
                throw failure("task.public_port.id.duplicate", "Settlement and Logic output IDs must be unique.");
            if (!publicNames.add(slot.getDisplayName()))
                throw failure("task.public_port.display_name.duplicate", "Duplicate public Logic display name.");
        }
        return result;
    }

    private void validateNodeShape(CanonicalGraphNode node) {
        String type = node.getType();
        if (!ACTIVATE.equals(type) && !OBJECTIVE.equals(type)
            && !AND.equals(type)
            && !OR.equals(type)
            && !NOT.equals(type)
            && !LOGIC_OUTPUT.equals(type)
            && !SETTLE.equals(type))
            throw failure("task.node.type.unsupported", "Unsupported Task node type '" + type + "'.");
        if (ACTIVATE.equals(type)) {
            requireProperties(node);
            requirePorts(node, 0, 1, "logic_out");
            requireDirection(node, "logic_out", false);
        } else if (OBJECTIVE.equals(type)) {
            requirePorts(node, 1, 1, "logic_enable", "logic_status");
            requireDirection(node, "logic_enable", true);
            requireDirection(node, "logic_status", false);
            validateObjective(node);
            objectives.put(node.getId(), node);
        } else if (AND.equals(type) || OR.equals(type)) {
            requireProperties(node);
            requirePortsAtLeast(node, 2, 1, "logic_out");
        } else if (NOT.equals(type)) {
            requireProperties(node);
            requirePorts(node, 1, 1, "logic_in", "logic_out");
            requireDirection(node, "logic_in", true);
            requireDirection(node, "logic_out", false);
        } else if (LOGIC_OUTPUT.equals(type)) {
            requireProperties(node, "port_id", "display_name");
            requirePorts(node, 1, 0, "logic_in");
            requireDirection(node, "logic_in", true);
            requiredString(node, "port_id", "task.logic_output");
            requiredString(node, "display_name", "task.logic_output");
        } else if (SETTLE.equals(type)) {
            requireProperties(node);
            if (node.getPorts()
                .isEmpty()) throw failure("task.settle.slots.empty", "Settle requires result slots.");
            for (CanonicalGraphPort port : node.getPorts()) {
                if (!port.isInput()) throw failure("task.settle.port", "Settle slots must be Logic inputs.");
                if (port.getOrder() < 0)
                    throw failure("task.settle.order", "Settlement slot order cannot be negative.");
                for (CanonicalGraphPort previous : settlementSlots) if (previous.getOrder() == port.getOrder())
                    throw failure("task.settle.order", "Settlement slot order must be unique.");
                if (blank(port.getDisplayName()))
                    throw failure("task.settle.display_name", "Settlement display name required.");
                settlementSlots.add(port);
            }
            Collections.sort(settlementSlots, new Comparator<CanonicalGraphPort>() {

                @Override
                public int compare(CanonicalGraphPort left, CanonicalGraphPort right) {
                    return left.getOrder() < right.getOrder() ? -1 : left.getOrder() == right.getOrder() ? 0 : 1;
                }
            });
        }
    }

    private void validateObjective(CanonicalGraphNode node) {
        String type = requiredString(node, "objective_type", "task.objective");
        if (!CanonicalTaskEvent.KILL_ENTITY.equals(type) && !CanonicalTaskEvent.COLLECT_ITEM.equals(type)
            && !CanonicalTaskEvent.INTERACT_ACTOR.equals(type))
            throw failure("task.objective.type", "Unsupported objective type.");
        requiredString(node, "description", "task.objective");
        int required = required(node);
        if (required <= 0) throw failure("task.objective.required", "Objective required must be positive.");
        if (CanonicalTaskEvent.KILL_ENTITY.equals(type)) requiredString(node, "entity", "task.objective");
        if (CanonicalTaskEvent.COLLECT_ITEM.equals(type)) {
            requiredString(node, "item", "task.objective");
            JsonElement metadata = node.getProperties()
                .get("metadata");
            if (metadata == null || !metadata.isJsonObject())
                throw failure("task.objective.metadata", "Collect objective metadata must be an object.");
            for (Map.Entry<String, JsonElement> entry : metadata.getAsJsonObject()
                .entrySet())
                if (!entry.getValue()
                    .isJsonPrimitive()
                    || !entry.getValue()
                        .getAsJsonPrimitive()
                        .isString())
                    throw failure("task.objective.metadata", "Collect objective metadata values must be strings.");
        }
        if (CanonicalTaskEvent.INTERACT_ACTOR.equals(type)) requiredString(node, "actor_id", "task.objective");
        if (CanonicalTaskEvent.KILL_ENTITY.equals(type))
            requireProperties(node, "objective_type", "description", "required", "entity");
        if (CanonicalTaskEvent.COLLECT_ITEM.equals(type))
            requireProperties(node, "objective_type", "description", "required", "item", "metadata");
        if (CanonicalTaskEvent.INTERACT_ACTOR.equals(type))
            requireProperties(node, "objective_type", "description", "required", "actor_id");
    }

    private void requireProperties(CanonicalGraphNode node, String... allowed) {
        Set<String> keys = new HashSet<String>(java.util.Arrays.asList(allowed));
        if (!keys.equals(
            node.getProperties()
                .keySet()))
            throw failure("task.node.properties", "Task node has unknown or missing properties.");
    }

    private void validateEdges(CanonicalGraph graph) {
        Set<String> edges = new HashSet<String>();
        Map<String, List<String>> adjacency = new HashMap<String, List<String>>();
        for (CanonicalGraphConnection edge : graph.getConnections()) {
            if (edge == null || edge.getInterfaceKind() != CanonicalGraphInterfaceKind.LOGIC)
                throw failure("task.edge.kind", "Task edges must be Logic.");
            CanonicalGraphPort source = ports.get(endpoint(edge.getFromNodeId(), edge.getFromPortId()));
            CanonicalGraphPort target = ports.get(endpoint(edge.getToNodeId(), edge.getToPortId()));
            if (source == null || target == null)
                throw failure("task.edge.port", "Task edge references an unknown port.");
            if (!source.isOutput() || !target.isInput())
                throw failure("task.edge.direction", "Task edge direction is invalid.");
            String key = edge.getFromNodeId() + ":"
                + edge.getFromPortId()
                + "->"
                + edge.getToNodeId()
                + ":"
                + edge.getToPortId();
            if (!edges.add(key)) throw failure("task.edge.duplicate", "Duplicate Task edge.");
            String targetKey = endpoint(edge.getToNodeId(), edge.getToPortId());
            if (incoming.put(targetKey, edge) != null)
                throw failure("task.logic.input.multiple_sources", "Logic input has multiple sources.");
            List<String> list = adjacency.get(edge.getFromNodeId());
            if (list == null) {
                list = new ArrayList<String>();
                adjacency.put(edge.getFromNodeId(), list);
            }
            list.add(edge.getToNodeId());
        }
        for (String id : nodes.keySet()) if (hasCycle(id, adjacency, new HashMap<String, Integer>()))
            throw failure("task.logic.cycle", "Logic graph contains a cycle.");
    }

    private boolean hasCycle(String id, Map<String, List<String>> adjacency, Map<String, Integer> states) {
        Integer state = states.get(id);
        if (state != null) return state.intValue() == 1;
        states.put(id, Integer.valueOf(1));
        List<String> targets = adjacency.get(id);
        if (targets != null) for (String target : targets) if (hasCycle(target, adjacency, states)) return true;
        states.put(id, Integer.valueOf(2));
        return false;
    }

    private void restoreSnapshot(CanonicalTaskSnapshot snapshot) {
        if (!resource.getId()
            .equals(snapshot.getResourceId()) || !resourceFingerprint.equals(snapshot.getResourceFingerprint()))
            throw failure("task.snapshot.resource", "Snapshot resource identity or semantics differ.");
        if (snapshot.getStatus() == null || (snapshot.getStatus() != CanonicalTaskStatus.ACTIVE
            && snapshot.getStatus() != CanonicalTaskStatus.SETTLED))
            throw failure("task.snapshot.status", "Unknown snapshot status.");
        if (!snapshot.getProgress()
            .keySet()
            .equals(objectives.keySet())
            || !snapshot.getObjectiveStatuses()
                .keySet()
                .equals(objectives.keySet()))
            throw failure("task.snapshot.objectives", "Snapshot objective set differs.");
        progress.putAll(snapshot.getProgress());
        objectiveStatuses.putAll(snapshot.getObjectiveStatuses());
        for (String id : objectives.keySet()) {
            int value = progress.get(id) == null ? -1
                : progress.get(id)
                    .intValue();
            CanonicalTaskObjectiveStatus objectiveStatus = objectiveStatuses.get(id);
            if (value < 0 || value > required(objectives.get(id)) || objectiveStatus == null)
                throw failure("task.snapshot.progress", "Snapshot objective progress is invalid.");
            if (objectiveStatus == CanonicalTaskObjectiveStatus.ACTIVE && value >= required(objectives.get(id)))
                throw failure("task.snapshot.progress", "Active objective progress must be below required.");
            if (objectiveStatus == CanonicalTaskObjectiveStatus.COMPLETED && value != required(objectives.get(id)))
                throw failure("task.snapshot.progress", "Completed objective progress is contradictory.");
            if (objectiveStatus == CanonicalTaskObjectiveStatus.INACTIVE && value != 0)
                throw failure("task.snapshot.progress", "Inactive objective has progress.");
        }
        status = snapshot.getStatus();
        resultPortId = snapshot.getResultPortId();
        activationLogic = snapshot.getActivationLogic();
        if (status == CanonicalTaskStatus.ACTIVE && (!activationLogic || resultPortId != null))
            throw failure("task.snapshot.state", "Active snapshot state is contradictory.");
        if (status == CanonicalTaskStatus.SETTLED && (activationLogic || !settlementIds().contains(resultPortId)))
            throw failure("task.snapshot.state", "Settled snapshot state is contradictory.");
        if (status == CanonicalTaskStatus.ACTIVE) {
            recomputeLogic();
            if (!logicValues.equals(snapshot.getLogicValues()))
                throw failure("task.snapshot.logic", "Snapshot Logic state differs.");
            if (!publicLogicOutputs.keySet()
                .equals(
                    snapshot.getPublicLogicOutputs()
                        .keySet()))
                throw failure("task.snapshot.public_logic", "Snapshot public Logic ports differ.");
            for (String id : objectives.keySet()) {
                boolean enabled = logicInputValue(objectives.get(id), "logic_enable", new HashMap<String, Boolean>());
                if (objectiveStatuses.get(id) == CanonicalTaskObjectiveStatus.INACTIVE && enabled)
                    throw failure("task.snapshot.active_objective", "Enabled objective is marked inactive.");
            }
            for (CanonicalGraphPort slot : settlementSlots)
                if (logicInputValue(node(SETTLE), slot.getId(), new HashMap<String, Boolean>()))
                    throw failure("task.snapshot.result", "Active snapshot already has a settlement result.");
            if (!publicLogicOutputs.equals(snapshot.getPublicLogicOutputs()))
                throw failure("task.snapshot.public_logic", "Snapshot public Logic differs.");
        } else {
            activationLogic = true;
            recomputeLogic();
            for (String id : objectives.keySet()) if (objectiveStatuses.get(id) == CanonicalTaskObjectiveStatus.INACTIVE
                && logicInputValue(objectives.get(id), "logic_enable", new HashMap<String, Boolean>()))
                throw failure("task.snapshot.active_objective", "Enabled objective is marked inactive.");
            String reconstructedResult = firstTrueSettlement();
            if (!resultPortId.equals(reconstructedResult))
                throw failure("task.snapshot.result", "Settled result is not the first true settlement slot.");
            if (!publicLogicOutputs.equals(snapshot.getPublicLogicOutputs()))
                throw failure("task.snapshot.public_logic", "Retained public Logic differs from settlement snapshot.");
            activationLogic = false;
            recomputeLogic();
            if (!logicValues.equals(snapshot.getLogicValues()))
                throw failure("task.snapshot.logic", "Settled Logic state differs.");
        }
        if (status == CanonicalTaskStatus.SETTLED) {
            publicLogicOutputs.clear();
            publicLogicOutputs.putAll(snapshot.getPublicLogicOutputs());
        }
    }

    private String firstTrueSettlement() {
        for (CanonicalGraphPort slot : settlementSlots)
            if (logicInputValue(node(SETTLE), slot.getId(), new HashMap<String, Boolean>())) return slot.getId();
        return null;
    }

    private Set<String> settlementIds() {
        Set<String> ids = new HashSet<String>();
        for (CanonicalGraphPort slot : settlementSlots) ids.add(slot.getId());
        return ids;
    }

    private boolean matches(CanonicalGraphNode node, CanonicalTaskEvent event) {
        String type = requiredString(node, "objective_type", "task.objective");
        if (!type.equals(event.getType())) return false;
        if (CanonicalTaskEvent.KILL_ENTITY.equals(type)) return value(node, "entity").equals(event.get("entity"));
        if (CanonicalTaskEvent.INTERACT_ACTOR.equals(type))
            return value(node, "actor_id").equals(event.get("actor_id"));
        if (!value(node, "item").equals(event.get("item"))) return false;
        JsonObject metadata = node.getProperties()
            .get("metadata")
            .getAsJsonObject();
        for (Map.Entry<String, JsonElement> entry : metadata.entrySet())
            if (event.get(entry.getKey()) == null || !entry.getValue()
                .getAsString()
                .equals(event.get(entry.getKey()))) return false;
        return true;
    }

    private int required(CanonicalGraphNode node) {
        JsonElement value = node.getProperties()
            .get("required");
        if (value == null || !value.isJsonPrimitive()
            || !value.getAsJsonPrimitive()
                .isNumber())
            throw failure("task.objective.required", "Objective required must be an integer.");
        int result = value.getAsInt();
        if (value.getAsDouble() != result)
            throw failure("task.objective.required", "Objective required must be an integer.");
        return result;
    }

    private static String requiredString(CanonicalGraphNode node, String key, String prefix) {
        JsonElement value = node.getProperties()
            .get(key);
        if (value == null || !value.isJsonPrimitive()
            || !value.getAsJsonPrimitive()
                .isString()
            || blank(value.getAsString()))
            throw failure(prefix + ".property.required", "Property '" + key + "' is required.");
        return value.getAsString();
    }

    private static String value(CanonicalGraphNode node, String key) {
        return requiredString(node, key, "task.objective");
    }

    private void requirePorts(CanonicalGraphNode node, int inputs, int outputs, String... fixedIds) {
        if (node.getPorts()
            .size() != inputs + outputs) throw failure("task.node.ports", "Invalid fixed Task node port count.");
        for (String id : fixedIds)
            if (!portsContain(node, id)) throw failure("task.node.ports", "Missing fixed Task port '" + id + "'.");
        int inputCount = 0;
        for (CanonicalGraphPort port : node.getPorts()) if (port.isInput()) inputCount++;
        if (inputCount != inputs) throw failure("task.node.ports", "Invalid Task node port directions.");
    }

    private void requirePortsAtLeast(CanonicalGraphNode node, int inputs, int outputs, String outputId) {
        if (node.getPorts()
            .size() < inputs + outputs || !portsContain(node, outputId))
            throw failure("task.node.ports", "Invalid combinator ports.");
        int inputCount = 0;
        int outputCount = 0;
        for (CanonicalGraphPort port : node.getPorts()) if (port.isInput()) inputCount++;
        else outputCount++;
        if (inputCount < inputs || outputCount != outputs)
            throw failure("task.node.ports", "Invalid combinator ports.");
        requireDirection(node, outputId, false);
    }

    private boolean portsContain(CanonicalGraphNode node, String id) {
        for (CanonicalGraphPort port : node.getPorts()) if (id.equals(port.getId())) return true;
        return false;
    }

    private void requireDirection(CanonicalGraphNode node, String id, boolean input) {
        for (CanonicalGraphPort port : node.getPorts()) if (id.equals(port.getId())) {
            if (port.isInput() != input) throw failure("task.node.ports", "Fixed Task port has invalid direction.");
            return;
        }
        throw failure("task.node.ports", "Missing fixed Task port '" + id + "'.");
    }

    private CanonicalGraphNode node(String type) {
        for (CanonicalGraphNode node : nodes.values()) if (type.equals(node.getType())) return node;
        throw failure("task.node.required", "Missing Task node '" + type + "'.");
    }

    private static void validateResourceEnvelope(CanonicalGraphResource resource) {
        if (resource == null) throw failure("task.resource.required", "Task resource is required.");
        if (resource.getResourceKind() != CanonicalGraphResourceKind.TASK)
            throw failure("task.resource.kind", "Canonical Task runtime requires a Task resource.");
        if (resource.getSchemaVersion() != CanonicalGraphResource.CURRENT_SCHEMA_VERSION)
            throw failure("task.resource.schema", "Unsupported Task schema version.");
        if (blank(resource.getId())) throw failure("task.resource.id", "Task resource ID is required.");
    }

    private static String endpoint(String nodeId, String portId) {
        return nodeId + ":" + portId;
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }

    private static CanonicalGraphResourceException failure(String code, String message) {
        return new CanonicalGraphResourceException(code, message);
    }

    private static <T> Map<String, T> detached(Map<String, T> source) {
        return Collections.unmodifiableMap(new LinkedHashMap<String, T>(source));
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
        CanonicalGraph graph = resource.getGraph();
        for (CanonicalGraphNode node : graph.getNodes()) {
            text.append("|N:")
                .append(node.getId())
                .append(':')
                .append(node.getType())
                .append(':')
                .append(node.getDisplayName());
            for (CanonicalGraphPort port : node.getPorts()) text.append("|P:")
                .append(port.getId())
                .append(':')
                .append(port.getDirection())
                .append(':')
                .append(port.getKind())
                .append(':')
                .append(port.getOrder())
                .append(':')
                .append(port.getDisplayName());
            for (Map.Entry<String, JsonElement> property : node.getProperties()
                .entrySet())
                text.append("|K:")
                    .append(property.getKey())
                    .append('=')
                    .append(property.getValue());
        }
        for (CanonicalGraphConnection edge : graph.getConnections()) text.append("|E:")
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
            for (byte value : digest) result.append(String.format("%02x", Byte.valueOf(value)));
            return result.toString();
        } catch (NoSuchAlgorithmException exception) {
            throw new IllegalStateException(exception);
        }
    }
}
