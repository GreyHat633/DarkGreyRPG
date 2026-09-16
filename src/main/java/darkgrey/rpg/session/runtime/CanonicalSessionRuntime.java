package darkgrey.rpg.session.runtime;

import java.util.ArrayList;
import java.util.Collections;
import java.util.HashMap;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;

import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonPrimitive;

import darkgrey.rpg.graph.canonical.CanonicalGraph;
import darkgrey.rpg.graph.canonical.CanonicalGraphConnection;
import darkgrey.rpg.graph.canonical.CanonicalGraphInterfaceKind;
import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphPort;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceException;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceKind;

/** Deterministic, local execution of one canonical Session resource. */
public final class CanonicalSessionRuntime {

    private static final int MAX_AUTOMATIC_TRANSITIONS = 256;
    private final CanonicalGraphResource resource;
    private final Map<String, CanonicalGraphNode> nodes;
    private final List<String> selectedOptionIds = new ArrayList<String>();
    private final List<String> selectedChoiceNodeIds = new ArrayList<String>();
    /** Latest selection per Choice node; history remains in selectedOptionIds. */
    private final Map<String, String> selectedChoiceOptions = new LinkedHashMap<String, String>();
    private final Map<String, Boolean> internalLogicValues = new LinkedHashMap<String, Boolean>();
    private final Map<String, Boolean> publicLogicOutputs = new LinkedHashMap<String, Boolean>();
    private final Map<String, Boolean> externalLogicInputs = new LinkedHashMap<String, Boolean>();
    private final List<String> executedFlowJudgmentNodeIds = new ArrayList<String>();
    private CanonicalSessionPresentation presentation = CanonicalSessionPresentation.EMPTY;
    private long lineEpoch;
    private int linePageIndex;
    private CanonicalSessionStatus status;
    private String currentNodeId;
    private String finalEndPortId;
    private CanonicalSessionStep currentStep;
    private boolean activationLogic;
    private boolean waitingCondition;
    private Boolean waitingConditionValue;

    private CanonicalSessionRuntime(CanonicalGraphResource resource, boolean initialize, boolean activationLogic) {
        if (resource == null) throw failure("session.resource.required", "Session resource is required.");
        if (resource.getResourceKind() != CanonicalGraphResourceKind.SESSION)
            throw failure("session.resource.kind", "Canonical Session runtime requires a session resource.");
        if (blank(resource.getId())) throw failure("session.resource.id.required", "Session resource ID is required.");
        this.resource = resource;
        this.nodes = indexNodes(resource.getGraph());
        this.status = CanonicalSessionStatus.ACTIVE;
        validateCanonicalSessionShape();
        validateCanonicalEdges();
        if (initialize) initialize(activationLogic);
    }

    public static CanonicalSessionRuntime start(CanonicalGraphResource resource) {
        return start(resource, false);
    }

    public static CanonicalSessionRuntime start(CanonicalGraphResource resource, boolean activationLogic) {
        return new CanonicalSessionRuntime(resource, true, activationLogic);
    }

    public static CanonicalSessionRuntime start(CanonicalGraphResource resource, boolean activationLogic,
        Map<String, Boolean> logicInputs) {
        CanonicalSessionRuntime runtime = new CanonicalSessionRuntime(resource, false, activationLogic);
        runtime.setInitialLogicInputs(logicInputs);
        runtime.initialize(activationLogic);
        return runtime;
    }

    public static CanonicalSessionRuntime begin(CanonicalGraphResource resource) {
        return start(resource);
    }

    public static CanonicalSessionRuntime begin(CanonicalGraphResource resource, boolean activationLogic) {
        return start(resource, activationLogic);
    }

    public static CanonicalSessionRuntime restore(CanonicalGraphResource resource, CanonicalSessionSnapshot snapshot) {
        if (snapshot == null) throw failure("session.snapshot.required", "Session snapshot is required.");
        CanonicalSessionRuntime runtime = new CanonicalSessionRuntime(resource, false, false);
        runtime.restoreSnapshot(snapshot);
        return runtime;
    }

    public CanonicalGraphResource getResource() {
        return resource;
    }

    public String getCurrentNodeId() {
        return currentNodeId;
    }

    public CanonicalSessionStatus getStatus() {
        return status;
    }

    public CanonicalSessionStatus status() {
        return status;
    }

    public boolean isActive() {
        return status == CanonicalSessionStatus.ACTIVE;
    }

    public boolean isCompleted() {
        return status == CanonicalSessionStatus.COMPLETED;
    }

    public boolean isFailed() {
        return status == CanonicalSessionStatus.FAILED;
    }

    public List<String> getSelectedOptionIds() {
        return Collections.unmodifiableList(new ArrayList<String>(selectedOptionIds));
    }

    public String getFinalEndPortId() {
        return finalEndPortId;
    }

    public CanonicalSessionStep getCurrentStep() {
        return currentStep;
    }

    public CanonicalSessionStep currentStep() {
        return currentStep;
    }

    public CanonicalGraphNode getCurrentNode() {
        return currentNodeId == null ? null : nodes.get(currentNodeId);
    }

    public Map<String, Boolean> getInternalLogicValues() {
        return detachedMap(internalLogicValues);
    }

    public Map<String, Boolean> getInternalLogicMap() {
        return getInternalLogicValues();
    }

    public Map<String, Boolean> getPublicLogicOutputs() {
        return detachedMap(publicLogicOutputs);
    }

    public Map<String, Boolean> getPublicLogicOutputMap() {
        return getPublicLogicOutputs();
    }

    public Map<String, Boolean> getLogicValues() {
        return getInternalLogicValues();
    }

    public Map<String, Boolean> getPublicLogic() {
        return getPublicLogicOutputs();
    }

    public Map<String, Boolean> getPublicLogicValues() {
        return getPublicLogicOutputs();
    }

    public Map<String, Boolean> getExternalLogicInputs() {
        return detachedMap(externalLogicInputs);
    }

    public Map<String, Boolean> getLogicInputs() {
        return getExternalLogicInputs();
    }

    public boolean setLogicInput(String portId, boolean value) {
        requireInputPort(portId);
        Boolean previous = externalLogicInputs.put(portId, Boolean.valueOf(value));
        recomputeLogic();
        boolean changed = previous == null ? value : previous.booleanValue() != value;
        if (waitingCondition && changed) {
            CanonicalGraphNode condition = currentNode();
            boolean now = logicInputValue(condition, "logic_in");
            waitingConditionValue = Boolean.valueOf(now);
            String output = now ? "flow_true" : "flow_false";
            if (flowOutgoing(condition, output) == 1) {
                waitingCondition = false;
                waitingConditionValue = null;
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

    public boolean getActivationLogic() {
        return activationLogic;
    }

    public CanonicalSessionStep continueLine() {
        requireActive();
        CanonicalGraphNode node = currentNode();
        if (!"line".equals(node.getType()))
            throw fail("session.line.expected", "Session is not paused at a line node.");
        if (linePageIndex + 1 < linePages(node).size()) {
            linePageIndex++;
            resolveAutomatic();
            return currentStep;
        }
        transitionFrom(node, "flow_out");
        resolveAutomatic();
        return currentStep;
    }

    public CanonicalSessionStep continueCurrentLine() {
        return continueLine();
    }

    public CanonicalSessionStep continueNarration() {
        return continueLine();
    }

    public CanonicalSessionStep choose(String optionId) {
        requireActive();
        CanonicalGraphNode node = currentNode();
        if (!"choice".equals(node.getType()))
            throw fail("session.choice.expected", "Session is not paused at a choice node.");
        CanonicalSessionChoiceOption selected = null;
        for (CanonicalSessionChoiceOption option : parseChoiceOptions(node)) if (option.getOptionId()
            .equals(optionId)) selected = option;
        if (selected == null) throw fail("session.choice.unknown", "Unknown choice option_id '" + optionId + "'.");
        selectedOptionIds.add(selected.getOptionId());
        selectedChoiceNodeIds.add(node.getId());
        selectedChoiceOptions.put(node.getId(), selected.getOptionId());
        recomputeLogic();
        transitionFrom(node, selected.getFlowPortId());
        resolveAutomatic();
        return currentStep;
    }

    public CanonicalSessionStep chooseOption(String optionId) {
        return choose(optionId);
    }

    public CanonicalSessionSnapshot snapshot() {
        return new CanonicalSessionSnapshot(
            resource.getId(),
            currentNodeId,
            status,
            selectedOptionIds,
            internalLogicValues,
            finalEndPortId,
            publicLogicOutputs,
            activationLogic,
            selectedChoiceOptions,
            selectedChoiceNodeIds,
            externalLogicInputs,
            waitingCondition,
            waitingConditionValue,
            executedFlowJudgmentNodeIds,
            presentation,
            lineEpoch,
            linePageIndex);
    }

    public CanonicalSessionSnapshot createSnapshot() {
        return snapshot();
    }

    private void initialize(boolean activation) {
        activationLogic = activation;
        CanonicalGraphNode start = uniqueNode("start");
        if (!hasExactlyOneTarget(start, "flow_out"))
            throw fail("session.start.unconnected", "Start requires one connected Flow output 'flow_out'.");
        recomputeLogic();
        transitionFrom(start, "flow_out");
        resolveAutomatic();
    }

    private void resolveAutomatic() {
        for (int i = 0; i < MAX_AUTOMATIC_TRANSITIONS; i++) {
            CanonicalGraphNode node = currentNode();
            String type = node.getType();
            if ("music".equals(type) || "screen".equals(type)) {
                presentation = presentation.apply(node);
                transitionFrom(node, "flow_out");
                continue;
            }
            if ("line".equals(type)) {
                if (lineEpoch == Long.MAX_VALUE) throw fail("session.line.epoch", "Line epoch exhausted.");
                lineEpoch++;
                currentStep = lineStep(node);
                return;
            }
            if ("choice".equals(type)) {
                currentStep = CanonicalSessionStep
                    .choice(node.getId(), optionalString(node, "prompt"), parseChoiceOptions(node));
                return;
            }
            if ("end".equals(type)) {
                presentation = CanonicalSessionPresentation.EMPTY;
                finalEndPortId = requiredString(node, "port_id", "session.end");
                status = CanonicalSessionStatus.COMPLETED;
                currentStep = CanonicalSessionStep
                    .end(node.getId(), finalEndPortId, requiredString(node, "display_name", "session.end"));
                return;
            }
            if ("legacy_jump".equals(type)) {
                transitionFromSingleFlowOutput(node);
                continue;
            }
            if ("condition".equals(type)) {
                boolean value = logicInputValue(node, "logic_in");
                String output = value ? "flow_true" : "flow_false";
                if (flowOutgoing(node, output) == 0) {
                    waitingCondition = true;
                    waitingConditionValue = Boolean.valueOf(value);
                    currentStep = null;
                    return;
                }
                transitionFrom(node, output);
                continue;
            }
            if ("flow_judgment".equals(type)) {
                if (!executedFlowJudgmentNodeIds.contains(node.getId())) executedFlowJudgmentNodeIds.add(node.getId());
                recomputeLogic();
                transitionFrom(node, "flow_out");
                continue;
            }
            throw fail(
                "session.node.unsupported",
                "Session node type '" + type + "' is unsupported in Flow execution.");
        }
        throw fail("session.runtime.cycle_guard", "Automatic Session transitions exceeded the deterministic guard.");
    }

    private void recomputeLogic() {
        internalLogicValues.clear();
        publicLogicOutputs.clear();
        Map<String, Boolean> visiting = new HashMap<String, Boolean>();
        for (CanonicalGraphNode node : nodes.values()) for (CanonicalGraphPort port : node.getPorts())
            if (port != null && port.isOutput() && port.getKind() == CanonicalGraphInterfaceKind.LOGIC)
                evaluateOutput(node, port.getId(), visiting);
        for (CanonicalGraphNode node : nodes.values()) if ("logic_output".equals(node.getType())) {
            String id = requiredString(node, "port_id", "session.logic_output");
            publicLogicOutputs.put(id, Boolean.valueOf(logicInputValue(node, "logic_in")));
        }
    }

    private boolean evaluateOutput(CanonicalGraphNode node, String portId, Map<String, Boolean> visiting) {
        String key = endpoint(node.getId(), portId);
        Boolean known = internalLogicValues.get(key);
        if (known != null) return known.booleanValue();
        if (Boolean.TRUE.equals(visiting.get(key)))
            throw fail("session.logic.cycle", "Logic graph contains a directed cycle.");
        visiting.put(key, Boolean.TRUE);
        String type = node.getType();
        boolean value;
        if ("start".equals(type)) value = activationLogic;
        else if ("logic_input".equals(type)) {
            Boolean external = externalLogicInputs.get(requiredString(node, "port_id", "session.logic_input"));
            value = external != null && external.booleanValue();
        } else if ("choice".equals(type)) value = portId.equals(selectedChoiceOptions.get(node.getId()));
        else if ("flow_judgment".equals(type))
            value = "executed".equals(portId) && executedFlowJudgmentNodeIds.contains(node.getId());
        else if ("and".equals(type) || "or".equals(type)) {
            boolean all = "and".equals(type);
            value = all;
            int count = 0;
            for (CanonicalGraphPort input : node.getPorts())
                if (input != null && input.isInput() && input.getKind() == CanonicalGraphInterfaceKind.LOGIC) {
                    count++;
                    boolean inputValue = logicInputValue(node, input.getId(), visiting);
                    value = all ? value && inputValue : value || inputValue;
                }
            if (count < 2) throw fail("session.logic.input.cardinality", "And/Or requires at least two Logic inputs.");
        } else if ("not".equals(type)) value = !logicInputValue(node, "logic_in", visiting);
        else throw fail("session.logic.output.invalid", "Node has an invalid Logic output: " + key);
        visiting.remove(key);
        internalLogicValues.put(key, Boolean.valueOf(value));
        return value;
    }

    private boolean logicInputValue(CanonicalGraphNode node, String portId) {
        return logicInputValue(node, portId, new HashMap<String, Boolean>());
    }

    private boolean logicInputValue(CanonicalGraphNode node, String portId, Map<String, Boolean> visiting) {
        CanonicalGraphConnection edge = logicIncoming(node, portId);
        if (edge == null) return false;
        return evaluateOutput(nodes.get(edge.getFromNodeId()), edge.getFromPortId(), visiting);
    }

    private CanonicalGraphConnection logicIncoming(CanonicalGraphNode node, String portId) {
        CanonicalGraphConnection found = null;
        for (CanonicalGraphConnection edge : resource.getGraph()
            .getConnections())
            if (edge != null && node.getId()
                .equals(edge.getToNodeId())
                && portId.equals(edge.getToPortId())
                && edge.getInterfaceKind() == CanonicalGraphInterfaceKind.LOGIC) {
                    if (found != null) throw fail(
                        "session.logic.input.multiple_sources",
                        "Logic input has multiple sources: " + endpoint(node.getId(), portId));
                    found = edge;
                }
        return found;
    }

    private void validateCanonicalSessionShape() {
        uniqueNode("start");
        for (CanonicalGraphNode node : nodes.values()) {
            String type = node.getType();
            if ("start".equals(type)) validateStartPorts(node);
            else if ("music".equals(type) || "screen".equals(type)) {
                validateFixedPorts(
                    node,
                    type,
                    spec("flow_in", true, CanonicalGraphInterfaceKind.FLOW),
                    spec("flow_out", false, CanonicalGraphInterfaceKind.FLOW));
                try {
                    CanonicalSessionPresentation.EMPTY.apply(node);
                } catch (IllegalArgumentException exception) {
                    throw failure("session.presentation.invalid", exception.getMessage());
                }
            } else if ("line".equals(type)) {
                validateFixedPorts(
                    node,
                    "line",
                    spec("flow_in", true, CanonicalGraphInterfaceKind.FLOW),
                    spec("flow_out", false, CanonicalGraphInterfaceKind.FLOW));
                validateLine(node);

            } else if ("end".equals(type)) {
                validateFixedPorts(node, "end", spec("flow_in", true, CanonicalGraphInterfaceKind.FLOW));
                requiredString(node, "port_id", "session.end");
                requiredString(node, "display_name", "session.end");
            } else if ("choice".equals(type)) validateChoicePorts(node);
            else if ("flow_judgment".equals(type)) validateFixedPorts(
                node,
                "flow_judgment",
                spec("flow_in", true, CanonicalGraphInterfaceKind.FLOW),
                spec("flow_out", false, CanonicalGraphInterfaceKind.FLOW),
                spec("executed", false, CanonicalGraphInterfaceKind.LOGIC));
            else if ("condition".equals(type)) validateFixedPorts(
                node,
                "condition",
                spec("flow_in", true, CanonicalGraphInterfaceKind.FLOW),
                spec("logic_in", true, CanonicalGraphInterfaceKind.LOGIC),
                spec("flow_true", false, CanonicalGraphInterfaceKind.FLOW),
                spec("flow_false", false, CanonicalGraphInterfaceKind.FLOW));
            else if ("not".equals(type)) validateFixedPorts(
                node,
                "not",
                spec("logic_in", true, CanonicalGraphInterfaceKind.LOGIC),
                spec("logic_out", false, CanonicalGraphInterfaceKind.LOGIC));
            else if ("logic_output".equals(type)) {
                validateFixedPorts(node, "logic_output", spec("logic_in", true, CanonicalGraphInterfaceKind.LOGIC));
                requiredString(node, "port_id", "session.logic_output");
                requiredString(node, "display_name", "session.logic_output");
            } else if ("logic_input".equals(type)) {
                validateFixedPorts(node, "logic_input", spec("logic_out", false, CanonicalGraphInterfaceKind.LOGIC));
                requiredString(node, "port_id", "session.logic_input");
                requiredString(node, "display_name", "session.logic_input");
            } else if ("and".equals(type) || "or".equals(type)) validateAndOr(node);
            else if ("legacy_jump".equals(type)) validateLegacyJump(node);
            else throw failure("session.node.unsupported", "Session node type '" + type + "' is unsupported.");
        }
        validatePublicBoundaries();
    }

    private void validatePublicBoundaries() {
        Set<String> inputIds = new HashSet<String>();
        Set<String> publicIds = new HashSet<String>();
        Set<String> publicNames = new HashSet<String>();
        for (CanonicalGraphNode node : nodes.values()) {
            String type = node.getType();
            if ("logic_input".equals(type)) {
                String inputId = requiredString(node, "port_id", "session.logic_input");
                if (!inputIds.add(inputId)) throw failure(
                    "session.logic_input.duplicate",
                    "Session Logic input port_id is duplicated: " + inputId);
                continue;
            }
            if (!"end".equals(type) && !"logic_output".equals(type)) continue;
            String prefix = "end".equals(type) ? "session.end" : "session.logic_output";
            String id = requiredString(node, "port_id", prefix);
            String displayName = requiredString(node, "display_name", prefix);
            if ("flow_in".equals(id) || "logic_in".equals(id))
                throw failure("session.public_port.id.reserved", "Session public boundary port_id is reserved: " + id);
            if (!publicIds.add(id)) throw failure(
                "session.public_port.id.duplicate",
                "Session public boundary port_id is duplicated: " + id);
            if (!publicNames.add(displayName)) throw failure(
                "session.public_port.display_name.duplicate",
                "Session public boundary display_name is duplicated: " + displayName);
        }
    }

    private void validateAndOr(CanonicalGraphNode node) {
        Map<String, CanonicalGraphPort> ports = portMap(node, node.getType());
        CanonicalGraphPort output = ports.remove("logic_out");
        if (output == null || !output.isOutput() || output.getKind() != CanonicalGraphInterfaceKind.LOGIC)
            throw failure(
                "session." + node.getType() + ".port",
                "And/Or requires exactly one Logic output 'logic_out'.");
        int inputs = 0;
        for (CanonicalGraphPort port : ports.values()) {
            if (!port.isInput() || port.getKind() != CanonicalGraphInterfaceKind.LOGIC)
                throw failure("session.logic.port", "And/Or ports must be Logic inputs plus logic_out.");
            inputs++;
        }
        if (inputs < 2) throw failure("session.logic.input.cardinality", "And/Or requires at least two Logic inputs.");
    }

    private void validateLegacyJump(CanonicalGraphNode node) {
        portMap(node, "legacy_jump");
    }

    private void validateStartPorts(CanonicalGraphNode node) {
        Map<String, CanonicalGraphPort> ports = portMap(node, "start");
        CanonicalGraphPort flow = ports.remove("flow_out");
        if (flow == null || !flow.isOutput() || flow.getKind() != CanonicalGraphInterfaceKind.FLOW)
            throw failure("session.start.port.missing", "Start requires Flow output 'flow_out'.");
        CanonicalGraphPort legacyLogic = ports.remove("logic_out");
        if (legacyLogic != null
            && (!legacyLogic.isOutput() || legacyLogic.getKind() != CanonicalGraphInterfaceKind.LOGIC))
            throw failure("session.start.port.kind", "Legacy Start output 'logic_out' must be Logic output.");
        if (!ports.isEmpty()) throw failure("session.start.port.extra", "Start has an unexpected port.");
    }

    private void validateChoicePorts(CanonicalGraphNode node) {
        Map<String, CanonicalGraphPort> ports = portMap(node, "choice");
        CanonicalGraphPort input = ports.remove("flow_in");
        if (input == null || !input.isInput() || input.getKind() != CanonicalGraphInterfaceKind.FLOW)
            throw failure("session.choice.port.missing", "Choice requires Flow input 'flow_in'.");
        optionalString(node, "prompt");
        List<CanonicalSessionChoiceOption> options = parseChoiceOptions(node);
        Set<String> flows = new HashSet<String>();
        Set<String> ids = new HashSet<String>();
        for (CanonicalSessionChoiceOption option : options) {
            if (!flows.add(option.getFlowPortId())) throw failure(
                "session.choice.option.duplicate_port",
                "Choice flow_port_id is duplicated: " + option.getFlowPortId());
            if (!ids.add(option.getOptionId())) throw failure(
                "session.choice.option.duplicate",
                "Choice option_id is duplicated: " + option.getOptionId());
            CanonicalGraphPort flow = ports.remove(option.getFlowPortId());
            CanonicalGraphPort logic = ports.remove(option.getOptionId());
            if (flow == null)
                throw failure("session.choice.option.mapping", "Choice Flow outputs must map one-to-one to options.");
            if (!flow.isOutput() || flow.getKind() != CanonicalGraphInterfaceKind.FLOW)
                throw failure("session.choice.option.flow.kind", "Choice option Flow output is invalid.");
            if (logic != null && (!logic.isOutput() || logic.getKind() != CanonicalGraphInterfaceKind.LOGIC))
                throw failure("session.choice.option.logic.kind", "Choice option Logic output is invalid.");
            if (!hasExactlyOneTarget(node, flow.getId())) throw failure(
                "session.choice.option.unconnected",
                "Choice option Flow output is unconnected: " + flow.getId());
            if (option.getFlowPortId()
                .equals(option.getOptionId()))
                throw failure("session.choice.option.mapping", "Choice Flow and Logic IDs must be distinct.");
        }
        if (!ports.isEmpty()) throw failure(
            "session.choice.option.mapping",
            "Choice has an unmapped output port: " + ports.keySet()
                .iterator()
                .next());
    }

    private void validateFixedPorts(CanonicalGraphNode node, String type, PortSpec... expected) {
        Map<String, CanonicalGraphPort> actual = portMap(node, type);
        for (PortSpec spec : expected) {
            CanonicalGraphPort port = actual.remove(spec.id);
            if (port == null)
                throw failure("session." + type + ".port.missing", "Required port is missing: " + spec.id);
            if (port.isInput() != spec.input)
                throw failure("session." + type + ".port.direction", "Port has the wrong direction: " + spec.id);
            if (port.getKind() != spec.kind)
                throw failure("session." + type + ".port.kind", "Port has the wrong interface kind: " + spec.id);
        }
        if (!actual.isEmpty()) throw failure(
            "session." + type + ".port.extra",
            "Unexpected port on node: " + actual.keySet()
                .iterator()
                .next());
    }

    private Map<String, CanonicalGraphPort> portMap(CanonicalGraphNode node, String type) {
        Map<String, CanonicalGraphPort> result = new LinkedHashMap<String, CanonicalGraphPort>();
        for (CanonicalGraphPort port : node.getPorts()) {
            if (port == null || blank(port.getId()))
                throw failure("session." + type + ".port.id.required", "Node port IDs are required.");
            if (result.put(port.getId(), port) != null)
                throw failure("session." + type + ".port.duplicate", "Port ID is duplicated: " + port.getId());
        }
        return result;
    }

    private void validateCanonicalEdges() {
        for (CanonicalGraphConnection edge : resource.getGraph()
            .getConnections()) {
            if (edge == null) throw failure("session.edge.required", "Session graph cannot contain a null edge.");
            CanonicalGraphNode from = nodes.get(edge.getFromNodeId());
            CanonicalGraphNode to = nodes.get(edge.getToNodeId());
            CanonicalGraphPort source = from == null ? null : findPort(from, edge.getFromPortId());
            CanonicalGraphPort target = to == null ? null : findPort(to, edge.getToPortId());
            String prefix = edge.getInterfaceKind() == CanonicalGraphInterfaceKind.LOGIC ? "session.logic.edge"
                : "session.flow.edge";
            if (from == null || to == null) throw failure(prefix + ".node", "Edge references a missing node.");
            if (source == null || target == null) throw failure(prefix + ".port", "Edge references a missing port.");
            if (edge.getInterfaceKind() == null || source.getKind() != edge.getInterfaceKind()
                || target.getKind() != edge.getInterfaceKind())
                throw failure(prefix + ".kind", "Edge kind does not match both endpoints.");
            if (!source.isOutput() || !target.isInput())
                throw failure(prefix + ".direction", "Edge must connect output to input.");
            if (edge.getInterfaceKind() == CanonicalGraphInterfaceKind.FLOW && flowOutgoing(from, source.getId()) > 1)
                throw failure("session.flow.output.multiple_targets", "Flow output has multiple targets.");
        }
        for (CanonicalGraphNode node : nodes.values()) for (CanonicalGraphPort port : node.getPorts())
            if (port != null && port.isInput() && port.getKind() == CanonicalGraphInterfaceKind.LOGIC)
                logicIncoming(node, port.getId());
        validateLogicAcyclic();
    }

    private void validateLogicAcyclic() {
        Map<String, Integer> colors = new HashMap<String, Integer>();
        for (CanonicalGraphNode node : nodes.values()) if (hasLogicOutput(node)) visitLogic(node, colors);
    }

    private void visitLogic(CanonicalGraphNode node, Map<String, Integer> colors) {
        Integer color = colors.get(node.getId());
        if (color != null && color.intValue() == 1)
            throw failure("session.logic.cycle", "Logic graph contains a directed cycle.");
        if (color != null && color.intValue() == 2) return;
        colors.put(node.getId(), Integer.valueOf(1));
        for (CanonicalGraphPort port : node.getPorts())
            if (port != null && port.isInput() && port.getKind() == CanonicalGraphInterfaceKind.LOGIC) {
                CanonicalGraphConnection edge = logicIncoming(node, port.getId());
                if (edge != null) visitLogic(nodes.get(edge.getFromNodeId()), colors);
            }
        colors.put(node.getId(), Integer.valueOf(2));
    }

    private boolean hasLogicOutput(CanonicalGraphNode node) {
        for (CanonicalGraphPort port : node.getPorts())
            if (port != null && port.isOutput() && port.getKind() == CanonicalGraphInterfaceKind.LOGIC) return true;
        return false;
    }

    private int flowOutgoing(CanonicalGraphNode node, String portId) {
        int count = 0;
        for (CanonicalGraphConnection edge : resource.getGraph()
            .getConnections())
            if (edge != null && node.getId()
                .equals(edge.getFromNodeId())
                && portId.equals(edge.getFromPortId())
                && edge.getInterfaceKind() == CanonicalGraphInterfaceKind.FLOW) count++;
        return count;
    }

    private boolean hasExactlyOneTarget(CanonicalGraphNode node, String portId) {
        return flowOutgoing(node, portId) == 1;
    }

    private void transitionFrom(CanonicalGraphNode node, String outputPortId) {
        requireOutputPort(node, outputPortId, CanonicalGraphInterfaceKind.FLOW);
        CanonicalGraphConnection found = null;
        for (CanonicalGraphConnection edge : resource.getGraph()
            .getConnections())
            if (node.getId()
                .equals(edge.getFromNodeId()) && outputPortId.equals(edge.getFromPortId())) {
                    if (found != null) throw fail("session.flow.ambiguous", "Flow output has multiple targets.");
                    found = edge;
                }
        if (found == null) throw fail("session.flow.unconnected", "Flow output '" + outputPortId + "' is unconnected.");
        currentNodeId = found.getToNodeId();
        linePageIndex = 0;
    }

    private void transitionFromSingleFlowOutput(CanonicalGraphNode node) {
        String found = null;
        for (CanonicalGraphPort port : node.getPorts()) if (port != null && port.isOutput()
            && port.getKind() == CanonicalGraphInterfaceKind.FLOW
            && hasExactlyOneTarget(node, port.getId())) {
                if (found != null)
                    throw fail("session.legacy_jump.ambiguous", "Legacy Jump has multiple connected Flow outputs.");
                found = port.getId();
            }
        if (found == null)
            throw fail("session.legacy_jump.unconnected", "Legacy Jump requires a connected Flow output.");
        transitionFrom(node, found);
    }

    private CanonicalGraphPort requireOutputPort(CanonicalGraphNode node, String id, CanonicalGraphInterfaceKind kind) {
        CanonicalGraphPort port = findPort(node, id);
        if (port == null) throw fail("session.flow.port.missing", "Flow output port is missing: " + id);
        if (!port.isOutput()) throw fail("session.flow.port.direction", "Flow source port must be an output: " + id);
        if (port.getKind() != kind)
            throw fail("session.flow.port.kind", "Flow source port has the wrong interface kind: " + id);
        return port;
    }

    private CanonicalGraphNode currentNode() {
        CanonicalGraphNode node = nodes.get(currentNodeId);
        if (node == null) throw fail("session.node.missing", "Current Session node is missing: " + currentNodeId);
        return node;
    }

    private CanonicalGraphNode uniqueNode(String type) {
        CanonicalGraphNode found = null;
        for (CanonicalGraphNode node : nodes.values()) if (type.equals(node.getType())) {
            if (found != null) throw failure("session.node.ambiguous", "Session has multiple '" + type + "' nodes.");
            found = node;
        }
        if (found == null) throw failure("session.node.missing", "Session is missing its '" + type + "' node.");
        return found;
    }

    private void restoreSnapshot(CanonicalSessionSnapshot snapshot) {
        if (!resource.getId()
            .equals(snapshot.getSessionResourceId()))
            throw failure("session.snapshot.resource_mismatch", "Snapshot resource ID does not match.");
        if (snapshot.getStatus() == null || snapshot.getCurrentNodeId() == null
            || !nodes.containsKey(snapshot.getCurrentNodeId()))
            throw failure("session.snapshot.state", "Snapshot cursor and status are inconsistent.");
        for (String id : snapshot.getSelectedOptionIds()) if (blank(id) || !knownOptionId(id))
            throw failure("session.snapshot.history", "Snapshot selected option is unknown: " + id);
        if (snapshot.getSelectedChoiceNodeIds()
            .size()
            != snapshot.getSelectedOptionIds()
                .size())
            throw failure("session.snapshot.history", "Snapshot Choice history is incomplete.");
        Map<String, String> expectedLatest = new LinkedHashMap<String, String>();
        for (int i = 0; i < snapshot.getSelectedOptionIds()
            .size(); i++) {
            String choiceNodeId = snapshot.getSelectedChoiceNodeIds()
                .get(i);
            CanonicalGraphNode choiceNode = nodes.get(choiceNodeId);
            if (choiceNode == null || !"choice".equals(choiceNode.getType()))
                throw failure("session.snapshot.history", "Snapshot Choice node is unknown: " + choiceNodeId);
            String optionId = snapshot.getSelectedOptionIds()
                .get(i);
            boolean belongs = false;
            for (CanonicalSessionChoiceOption option : parseChoiceOptions(choiceNode))
                if (optionId.equals(option.getOptionId())) belongs = true;
            if (!belongs) throw failure(
                "session.snapshot.history",
                "Snapshot option does not belong to its Choice node: " + optionId);
            expectedLatest.put(choiceNodeId, optionId);
        }
        if (!expectedLatest.equals(snapshot.getLatestChoiceSelections()))
            throw failure("session.snapshot.history", "Snapshot latest Choice selections are stale or incomplete.");
        if (snapshot.getStatus() == CanonicalSessionStatus.COMPLETED) {
            CanonicalGraphNode node = nodes.get(snapshot.getCurrentNodeId());
            if (!"end".equals(node.getType()) || blank(snapshot.getFinalEndPortId()))
                throw failure("session.snapshot.state", "Completed snapshot must point at an end.");
        } else if (snapshot.getFinalEndPortId() != null)
            throw failure("session.snapshot.state", "Only completed snapshots may contain a final end port.");
        selectedOptionIds.addAll(snapshot.getSelectedOptionIds());
        selectedChoiceNodeIds.addAll(snapshot.getSelectedChoiceNodeIds());
        selectedChoiceOptions.putAll(snapshot.getLatestChoiceSelections());
        for (String nodeId : snapshot.getExecutedFlowJudgmentNodeIds()) {
            CanonicalGraphNode executed = nodes.get(nodeId);
            if (executed == null || !"flow_judgment".equals(executed.getType()))
                throw failure("session.snapshot.flow_judgment", "Snapshot Flow Judgment node is unknown: " + nodeId);
            if (executedFlowJudgmentNodeIds.contains(nodeId))
                throw failure("session.snapshot.flow_judgment", "Snapshot Flow Judgment node is duplicated: " + nodeId);
            executedFlowJudgmentNodeIds.add(nodeId);
        }
        for (Map.Entry<String, Boolean> entry : snapshot.getExternalLogicInputs()
            .entrySet()) {
            if (blank(entry.getKey()) || entry.getValue() == null)
                throw failure("session.snapshot.logic", "Snapshot contains an invalid external Logic input.");
            requireInputPort(entry.getKey());
        }
        externalLogicInputs.putAll(snapshot.getExternalLogicInputs());
        presentation = snapshot.getPresentation();
        lineEpoch = snapshot.getLineEpoch();
        linePageIndex = snapshot.getLinePageIndex();
        currentNodeId = snapshot.getCurrentNodeId();
        status = snapshot.getStatus();
        finalEndPortId = snapshot.getFinalEndPortId();
        activationLogic = snapshot.getActivationLogic();
        waitingCondition = snapshot.isWaitingCondition();
        waitingConditionValue = snapshot.getWaitingConditionValue();
        if (waitingCondition != (waitingConditionValue != null))
            throw failure("session.snapshot.state", "Condition wait state and value must be supplied together.");
        recomputeLogic();
        if (!internalLogicValues.equals(snapshot.getInternalLogicValues())
            || !publicLogicOutputs.equals(snapshot.getPublicLogicOutputs()))
            throw failure("session.snapshot.logic", "Snapshot Logic maps are stale, unknown, or incomplete.");
        CanonicalGraphNode node = currentNode();
        if (linePageIndex < 0
            || ("line".equals(node.getType()) ? linePageIndex >= linePages(node).size() : linePageIndex != 0))
            throw failure("session.snapshot.page", "Snapshot line page is outside the current node.");
        if (status == CanonicalSessionStatus.ACTIVE && waitingCondition) {
            if (!"condition".equals(node.getType()))
                throw failure("session.snapshot.state", "Condition wait must point at a Condition node.");
            boolean currentValue = logicInputValue(node, "logic_in");
            String output = currentValue ? "flow_true" : "flow_false";
            if (currentValue != waitingConditionValue.booleanValue() || flowOutgoing(node, output) != 0)
                throw failure("session.snapshot.state", "Condition wait state is stale.");
            currentStep = null;
        } else
            if (status == CanonicalSessionStatus.ACTIVE && "line".equals(node.getType())) currentStep = lineStep(node);
            else if (status == CanonicalSessionStatus.ACTIVE && "choice".equals(node.getType()))
                currentStep = CanonicalSessionStep
                    .choice(node.getId(), optionalString(node, "prompt"), parseChoiceOptions(node));
            else if (status == CanonicalSessionStatus.COMPLETED && "end".equals(node.getType()))
                currentStep = CanonicalSessionStep.end(
                    node.getId(),
                    requiredString(node, "port_id", "session.end"),
                    requiredString(node, "display_name", "session.end"));
            else if (status != CanonicalSessionStatus.FAILED)
                throw failure("session.snapshot.state", "Snapshot cursor does not point at a valid paused state.");
    }

    private boolean knownOptionId(String id) {
        for (CanonicalGraphNode node : nodes.values())
            if ("choice".equals(node.getType())) for (CanonicalSessionChoiceOption option : parseChoiceOptions(node))
                if (id.equals(option.getOptionId())) return true;
        return false;
    }

    private void requireActive() {
        if (status != CanonicalSessionStatus.ACTIVE)
            throw failure("session.state.not_active", "Session is not active.");
    }

    private CanonicalSessionStep lineStep(CanonicalGraphNode node) {
        node = linePages(node).get(linePageIndex);
        return CanonicalSessionStep
            .line(
                node.getId(),
                optionalLineString(node, "speaker_actor_id"),
                requiredString(node, "text", "session.line"),
                optionalLineString(node, "portrait_variant"),
                optionalLineString(node, "voice_ref"),
                optionalNumber(node, "voice_volume", 0, 1, 1))
            .withTextSpeed(customTextSpeed(node) ? optionalNumber(node, "text_speed", 0, 120, 30) : -1);
    }

    private void setInitialLogicInputs(Map<String, Boolean> values) {
        if (values == null) throw new IllegalArgumentException("Session Logic inputs are required.");
        for (Map.Entry<String, Boolean> entry : values.entrySet()) {
            String id = entry.getKey();
            if (blank(id)) throw new IllegalArgumentException("Session Logic input port ID is required.");
            if (entry.getValue() == null) throw new IllegalArgumentException("Session Logic input value is required.");
            requireInputPort(id);
            externalLogicInputs.put(id, entry.getValue());
        }
        recomputeLogic();
    }

    private void requireInputPort(String portId) {
        if (blank(portId)) throw new IllegalArgumentException("Session Logic input port ID is required.");
        for (CanonicalGraphNode node : nodes.values()) if ("logic_input".equals(node.getType())
            && portId.equals(requiredString(node, "port_id", "session.logic_input"))) return;
        throw failure("session.logic_input.missing", "Unknown Session Logic input: " + portId);
    }

    private List<CanonicalSessionChoiceOption> parseChoiceOptions(CanonicalGraphNode node) {
        JsonElement value = node.getProperties()
            .get("options");
        if (value == null || !value.isJsonArray())
            throw failure("session.choice.options.type", "Choice property 'options' must be a JSON array.");
        List<CanonicalSessionChoiceOption> result = new ArrayList<CanonicalSessionChoiceOption>();
        Set<String> ids = new HashSet<String>();
        for (JsonElement item : value.getAsJsonArray()) {
            if (item == null || !item.isJsonObject())
                throw failure("session.choice.option.type", "Each choice option must be a JSON object.");
            JsonObject object = item.getAsJsonObject();
            if (object.entrySet()
                .size() != 3 || !object.has("option_id")
                || !object.has("display_text")
                || !object.has("flow_port_id"))
                throw failure(
                    "session.choice.option.fields",
                    "Choice options require exactly option_id, display_text, and flow_port_id.");
            String id = optionString(object, "option_id");
            String text = optionString(object, "display_text");
            String flow = optionString(object, "flow_port_id");
            if (!ids.add(id)) throw failure("session.choice.option.duplicate", "Choice option_id is duplicated: " + id);
            result.add(new CanonicalSessionChoiceOption(id, text, flow));
        }
        if (result.isEmpty())
            throw failure("session.choice.options.empty", "Choice options must contain at least one option.");
        return Collections.unmodifiableList(result);
    }

    private static String requiredString(CanonicalGraphNode node, String name, String prefix) {
        JsonElement value = node.getProperties()
            .get(name);
        if (!(value instanceof JsonPrimitive) || !value.getAsJsonPrimitive()
            .isString() || blank(value.getAsString()))
            throw failure(prefix + ".property.required", "Required non-blank string property is missing: " + name);
        return value.getAsString();
    }

    private static void validateLine(CanonicalGraphNode node) {
        for (CanonicalGraphNode page : linePages(node)) {
            validateLinePage(page);
            requiredString(page, "text", "session.line");
        }
    }

    /** Detached per-page views; legacy single-line nodes remain readable. */
    public static List<CanonicalGraphNode> linePages(CanonicalGraphNode node) {
        Map<String, JsonElement> properties = node.getProperties();
        if (!properties.containsKey("pages")) return Collections.singletonList(node);
        for (String key : properties.keySet()) if (!"pages".equals(key) && !"speaker_actor_id".equals(key)
            && !"text".equals(key)
            && !"portrait_variant".equals(key)
            && !"voice_ref".equals(key)
            && !"voice_volume".equals(key)
            && !"text_speed".equals(key)
            && !"custom_text_speed".equals(key))
            throw failure("session.line.property.unsupported", "Unsupported paged line property: " + key);
        optionalLineString(node, "speaker_actor_id");
        JsonElement pages = properties.get("pages");
        if (pages == null || !pages.isJsonArray()
            || pages.getAsJsonArray()
                .size() == 0)
            throw failure("session.line.pages", "Line pages must be a nonempty array.");
        List<CanonicalGraphNode> result = new ArrayList<CanonicalGraphNode>();
        Set<String> ids = new HashSet<String>();
        for (JsonElement page : pages.getAsJsonArray()) {
            if (!page.isJsonObject()) throw failure("session.line.page", "Line page must be an object.");
            JsonObject object = page.getAsJsonObject();
            JsonElement id = object.get("page_id");
            if (id == null || !id.isJsonPrimitive()
                || !id.getAsJsonPrimitive()
                    .isString()
                || blank(id.getAsString())
                || !ids.add(id.getAsString()))
                throw failure("session.line.page.id", "Line page IDs must be nonblank and unique.");
            Map<String, JsonElement> values = new LinkedHashMap<String, JsonElement>();
            for (Map.Entry<String, JsonElement> entry : object.entrySet()) {
                if ("speaker_actor_id".equals(entry.getKey()))
                    throw failure("session.line.page.speaker", "The speaker belongs to the line node.");
                if (!"page_id".equals(entry.getKey())) values.put(entry.getKey(), entry.getValue());
            }
            if (properties.containsKey("speaker_actor_id"))
                values.put("speaker_actor_id", properties.get("speaker_actor_id"));
            result.add(
                new CanonicalGraphNode(node.getId(), node.getType(), node.getDisplayName(), node.getPorts(), values));
        }
        return Collections.unmodifiableList(result);
    }

    private static void validateLinePage(CanonicalGraphNode node) {
        for (String key : node.getProperties()
            .keySet())
            if (!"speaker_actor_id".equals(key) && !"text".equals(key)
                && !"portrait_variant".equals(key)
                && !"voice_ref".equals(key)
                && !"voice_volume".equals(key)
                && !"text_speed".equals(key)
                && !"custom_text_speed".equals(key))
                throw failure("session.line.property.unsupported", "Unsupported line property: " + key);
        optionalLineString(node, "speaker_actor_id");
        for (String name : new String[] { "portrait_variant", "voice_ref" }) {
            JsonElement value = node.getProperties()
                .get(name);
            String parsed = optionalLineString(node, name);
            if (value != null && !value.isJsonNull() && parsed == null)
                throw failure("session.line.property.blank", "Optional line reference must not be blank: " + name);
            if ("voice_ref".equals(name) && parsed != null
                && !darkgrey.rpg.graph.canonical.CanonicalMediaReference.isAudio(parsed))
                throw failure("session.line.voice.invalid", "Line voice must reference project OGG media.");
        }
        optionalNumber(node, "voice_volume", 0, 1, 1);
        optionalNumber(node, "text_speed", 0, 120, 30);
        customTextSpeed(node);
    }

    private static boolean customTextSpeed(CanonicalGraphNode node) {
        JsonElement flag = node.getProperties()
            .get("custom_text_speed");
        if (flag == null) return false;
        if (!(flag instanceof JsonPrimitive) || !flag.getAsJsonPrimitive()
            .isBoolean()) throw failure("session.line.property.type", "Custom text speed must be boolean");
        return flag.getAsBoolean();
    }

    private static double optionalNumber(CanonicalGraphNode node, String name, double min, double max,
        double fallback) {
        JsonElement value = node.getProperties()
            .get(name);
        if (value == null || value.isJsonNull()) return fallback;
        if (!(value instanceof JsonPrimitive) || !value.getAsJsonPrimitive()
            .isNumber()) throw failure("session.line.property.type", "Line property must be a number: " + name);
        double number = value.getAsDouble();
        if (Double.isNaN(number) || Double.isInfinite(number) || number < min || number > max)
            throw failure("session.line.property.range", "Line property is outside its allowed range: " + name);
        return number;
    }

    public static String optionalLineString(CanonicalGraphNode node, String name) {
        JsonElement value = node.getProperties()
            .get(name);
        if (value == null || value.isJsonNull()) return null;
        if (!(value instanceof JsonPrimitive) || !value.getAsJsonPrimitive()
            .isString()) throw failure("session.line.property.type", "Line property must be a string or null: " + name);
        return blank(value.getAsString()) ? null : value.getAsString();
    }

    private static String optionalString(CanonicalGraphNode node, String name) {
        JsonElement value = node.getProperties()
            .get(name);
        if (value == null) return null;
        if (!(value instanceof JsonPrimitive) || !value.getAsJsonPrimitive()
            .isString()) throw failure("session.choice.prompt.type", "Choice prompt must be a string.");
        return value.getAsString();
    }

    private static String optionString(JsonObject object, String name) {
        JsonElement value = object.get(name);
        if (!(value instanceof JsonPrimitive) || !value.getAsJsonPrimitive()
            .isString() || blank(value.getAsString()))
            throw failure("session.choice.option.value", "Choice option field must be a non-blank string: " + name);
        return value.getAsString();
    }

    private static Map<String, CanonicalGraphNode> indexNodes(CanonicalGraph graph) {
        if (graph == null) throw failure("session.graph.required", "Session graph is required.");
        Map<String, CanonicalGraphNode> result = new LinkedHashMap<String, CanonicalGraphNode>();
        for (CanonicalGraphNode node : graph.getNodes()) {
            if (node == null || blank(node.getId()))
                throw failure("session.node.id.required", "Session graph node IDs are required.");
            if (result.put(node.getId(), node) != null)
                throw failure("session.node.duplicate", "Session graph node ID is duplicated: " + node.getId());
        }
        return result;
    }

    private static CanonicalGraphPort findPort(CanonicalGraphNode node, String id) {
        CanonicalGraphPort result = null;
        for (CanonicalGraphPort port : node.getPorts()) if (port != null && id.equals(port.getId())) {
            if (result != null) return null;
            result = port;
        }
        return result;
    }

    private static String endpoint(String node, String port) {
        return node + "." + port;
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }

    private static Map<String, Boolean> detachedMap(Map<String, Boolean> map) {
        return Collections.unmodifiableMap(new LinkedHashMap<String, Boolean>(map));
    }

    private static PortSpec spec(String id, boolean input, CanonicalGraphInterfaceKind kind) {
        return new PortSpec(id, input, kind);
    }

    private static final class PortSpec {

        final String id;
        final boolean input;
        final CanonicalGraphInterfaceKind kind;

        PortSpec(String id, boolean input, CanonicalGraphInterfaceKind kind) {
            this.id = id;
            this.input = input;
            this.kind = kind;
        }
    }

    private CanonicalGraphResourceException fail(String code, String message) {
        status = CanonicalSessionStatus.FAILED;
        return failure(code, message);
    }

    private static CanonicalGraphResourceException failure(String code, String message) {
        return new CanonicalGraphResourceException(code, message);
    }
}
