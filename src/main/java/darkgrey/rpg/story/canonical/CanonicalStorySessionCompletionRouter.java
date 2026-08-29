package darkgrey.rpg.story.canonical;

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

import darkgrey.rpg.graph.canonical.CanonicalGraph;
import darkgrey.rpg.graph.canonical.CanonicalGraphConnection;
import darkgrey.rpg.graph.canonical.CanonicalGraphInterfaceKind;
import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphPort;
import darkgrey.rpg.graph.canonical.CanonicalGraphPortDirection;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceKind;
import darkgrey.rpg.graph.canonical.CanonicalStoryMembership;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.session.server.CanonicalSessionCompletionResult;

/**
 * Pure Story Flow boundary router for a completed canonical Session.
 *
 * <p>
 * The router only inspects detached canonical data. It never consumes a
 * Session, mutates persistence, or invokes the legacy Story runtime.
 * </p>
 */
public final class CanonicalStorySessionCompletionRouter {

    private final CanonicalGraphResource story;
    private final ProjectSnapshot project;
    private final String expectedStoryId;

    /** Creates a resource-local router; Session resource/membership binding is outside this boundary. */
    public CanonicalStorySessionCompletionRouter(CanonicalGraphResource story) {
        this.story = story;
        this.project = null;
        this.expectedStoryId = null;
    }

    public CanonicalStorySessionCompletionRouter(ProjectSnapshot project, String storyId) {
        if (project == null) throw failure("Project is required.");
        this.story = null;
        this.project = project;
        this.expectedStoryId = storyId;
    }

    public CanonicalStorySessionCompletionRouter(ProjectSnapshot project) {
        if (project == null) throw failure("Project is required.");
        this.story = null;
        this.project = project;
        this.expectedStoryId = null;
    }

    public CanonicalStorySessionCompletionRoute route(CanonicalSessionCompletionResult completion) {
        if (completion == null) throw failure("Completion result is required.");
        CanonicalGraphResource selectedStory = story;
        if (selectedStory == null && project != null) {
            if (expectedStoryId != null && !expectedStoryId.equals(completion.getStoryId()))
                throw failure("Completion Story does not match router context.");
            validateProjectBinding(project, completion);
            selectedStory = project.getCanonicalStory(completion.getStoryId());
        }
        return route(completion, selectedStory);
    }

    public static CanonicalStorySessionCompletionRoute route(CanonicalGraphResource story,
        CanonicalSessionCompletionResult completion) {
        return new CanonicalStorySessionCompletionRouter(story).route(completion);
    }

    private static CanonicalStorySessionCompletionRoute route(CanonicalSessionCompletionResult completion,
        CanonicalGraphResource story) {
        if (completion == null) throw failure("Completion result is required.");
        requireId(completion.getStoryId(), "completion Story ID");
        requireId(completion.getAggregatePlacementId(), "completion placement ID");
        requireId(completion.getSessionResourceId(), "completion Session resource ID");
        requireId(completion.getEndPortId(), "completion End ID");
        if (completion.getTransportId() <= 0) throw failure("Completion transport ID is invalid.");
        if (story == null || story.getResourceKind() != CanonicalGraphResourceKind.STORY
            || !completion.getStoryId()
                .equals(story.getId())
            || story.getGraph() == null) throw failure("Canonical Story resource does not match completion.");

        CanonicalGraph graph = story.getGraph();
        GraphIndex index = validateGraph(graph);
        CanonicalGraphNode placement = findExactPlacement(index.nodes, completion.getAggregatePlacementId());
        if (placement == null || !"session".equals(placement.getType()))
            throw failure("Completion placement is not an exact Session aggregate.");
        String resourceId = requiredString(placement, "resource_id");
        if (!completion.getSessionResourceId()
            .equals(resourceId)) throw failure("Completion Session resource does not match aggregate placement.");

        CanonicalGraphPort selected = uniquePort(placement, completion.getEndPortId());
        if (selected == null || selected.getDirection() != CanonicalGraphPortDirection.OUTPUT
            || selected.getKind() != CanonicalGraphInterfaceKind.FLOW)
            throw failure("Selected End is not a Flow output on the aggregate.");

        CanonicalGraphConnection selectedConnection = null;
        for (CanonicalGraphConnection connection : graph.getConnections()) {
            if (placement.getId()
                .equals(connection.getFromNodeId())
                && completion.getEndPortId()
                    .equals(connection.getFromPortId())
                && connection.getInterfaceKind() == CanonicalGraphInterfaceKind.FLOW) {
                if (selectedConnection != null) throw failure("Selected End Flow output fans out.");
                selectedConnection = connection;
            }
        }
        if (selectedConnection == null) throw failure("Selected End Flow output is unconnected.");
        CanonicalGraphNode target = index.byId.get(selectedConnection.getToNodeId());
        CanonicalGraphPort targetPort = target == null ? null : uniquePort(target, selectedConnection.getToPortId());
        if (target == null || targetPort == null
            || targetPort.getDirection() != CanonicalGraphPortDirection.INPUT
            || targetPort.getKind() != CanonicalGraphInterfaceKind.FLOW)
            throw failure("Selected End target is not an existing Flow input.");
        int incoming = 0;
        for (CanonicalGraphConnection connection : graph.getConnections()) {
            if (target.getId()
                .equals(connection.getToNodeId())
                && targetPort.getId()
                    .equals(connection.getToPortId())
                && connection.getInterfaceKind() == CanonicalGraphInterfaceKind.FLOW) incoming++;
        }
        if (incoming != 1) throw failure("Selected End target Flow input is ambiguous.");

        Map<String, Boolean> completionLogic = completion.getPublicLogicOutputs();
        if (completionLogic == null) throw failure("Completion public Logic map is required.");
        List<CanonicalGraphPort> logicPorts = new ArrayList<CanonicalGraphPort>();
        for (CanonicalGraphPort port : placement.getPorts()) {
            if (port.getDirection() == CanonicalGraphPortDirection.OUTPUT
                && port.getKind() == CanonicalGraphInterfaceKind.LOGIC) logicPorts.add(port);
        }
        Collections.sort(logicPorts, new Comparator<CanonicalGraphPort>() {

            @Override
            public int compare(CanonicalGraphPort left, CanonicalGraphPort right) {
                int order = Integer.compare(left.getOrder(), right.getOrder());
                return order != 0 ? order
                    : left.getId()
                        .compareTo(right.getId());
            }
        });
        LinkedHashMap<String, Boolean> orderedLogic = new LinkedHashMap<String, Boolean>();
        for (CanonicalGraphPort port : logicPorts) {
            if (!completionLogic.containsKey(port.getId()) || completionLogic.get(port.getId()) == null)
                throw failure("Completion public Logic map is missing aggregate output '" + port.getId() + "'.");
            orderedLogic.put(port.getId(), completionLogic.get(port.getId()));
        }
        if (orderedLogic.size() != completionLogic.size() || !orderedLogic.keySet()
            .equals(completionLogic.keySet()))
            throw failure("Completion public Logic map has extra or unknown aggregate outputs.");

        CanonicalStoryFlowTransition transition = new CanonicalStoryFlowTransition(
            completion.getStoryId(),
            completion.getAggregatePlacementId(),
            completion.getEndPortId(),
            selectedConnection.getToNodeId(),
            selectedConnection.getToPortId());
        return new CanonicalStorySessionCompletionRoute(
            transition,
            new CanonicalStoryPublicLogicSnapshot(orderedLogic));
    }

    private static GraphIndex validateGraph(CanonicalGraph graph) {
        if (graph == null || graph.getNodes() == null || graph.getConnections() == null)
            throw failure("Canonical Story graph is malformed.");
        Map<String, CanonicalGraphNode> byId = new HashMap<String, CanonicalGraphNode>();
        Set<String> ids = new HashSet<String>();
        for (CanonicalGraphNode node : graph.getNodes()) {
            if (node == null || blank(node.getId()) || blank(node.getType()) || !ids.add(node.getId()))
                throw failure("Canonical Story graph contains invalid or duplicate node IDs.");
            List<CanonicalGraphPort> ports = node.getPorts();
            if (ports == null) throw failure("Canonical Story graph contains a node without ports.");
            Set<String> portIds = new HashSet<String>();
            for (CanonicalGraphPort port : ports) {
                if (port == null || blank(port.getId())
                    || !portIds.add(port.getId())
                    || port.getDirection() == null
                    || port.getKind() == null)
                    throw failure("Canonical Story graph contains invalid or duplicate port IDs.");
            }
            byId.put(node.getId(), node);
        }
        for (CanonicalGraphConnection connection : graph.getConnections()) {
            if (connection == null || blank(connection.getFromNodeId())
                || blank(connection.getFromPortId())
                || blank(connection.getToNodeId())
                || blank(connection.getToPortId())
                || connection.getInterfaceKind() == null)
                throw failure("Canonical Story graph contains a malformed connection.");
            CanonicalGraphNode from = byId.get(connection.getFromNodeId());
            CanonicalGraphNode to = byId.get(connection.getToNodeId());
            CanonicalGraphPort fromPort = from == null ? null : uniquePort(from, connection.getFromPortId());
            CanonicalGraphPort toPort = to == null ? null : uniquePort(to, connection.getToPortId());
            if (from == null || to == null
                || fromPort == null
                || toPort == null
                || fromPort.getDirection() != CanonicalGraphPortDirection.OUTPUT
                || toPort.getDirection() != CanonicalGraphPortDirection.INPUT
                || fromPort.getKind() != connection.getInterfaceKind()
                || toPort.getKind() != connection.getInterfaceKind())
                throw failure("Canonical Story graph contains an invalid connection.");
        }
        return new GraphIndex(byId, graph.getNodes());
    }

    private static CanonicalGraphNode findExactPlacement(List<CanonicalGraphNode> nodes, String placementId) {
        CanonicalGraphNode found = null;
        for (CanonicalGraphNode node : nodes) {
            if (placementId.equals(node.getId())) {
                if (found != null) throw failure("Aggregate placement is ambiguous.");
                found = node;
            }
        }
        return found;
    }

    private static CanonicalGraphPort uniquePort(CanonicalGraphNode node, String portId) {
        CanonicalGraphPort found = null;
        for (CanonicalGraphPort port : node.getPorts()) if (portId.equals(port.getId())) {
            if (found != null) throw failure("Aggregate port is ambiguous.");
            found = port;
        }
        return found;
    }

    private static String requiredString(CanonicalGraphNode node, String key) {
        JsonElement value;
        try {
            value = node.getProperties()
                .get(key);
        } catch (RuntimeException exception) {
            throw failure("Canonical aggregate properties are malformed.");
        }
        if (value == null || !value.isJsonPrimitive()
            || !value.getAsJsonPrimitive()
                .isString()
            || blank(value.getAsString())) throw failure("Aggregate resource_id is missing or invalid.");
        return value.getAsString();
    }

    private static void validateProjectBinding(ProjectSnapshot project, CanonicalSessionCompletionResult completion) {
        String resourceId = completion.getSessionResourceId();
        CanonicalGraphResource session = project.getCanonicalSessions()
            .get(resourceId);
        if (session == null || session.getResourceKind() != CanonicalGraphResourceKind.SESSION
            || !resourceId.equals(session.getId()))
            throw failure("Canonical Session resource binding is missing or incoherent.");
        CanonicalStoryMembership membership = project.getCanonicalStoryMembership(completion.getStoryId());
        if (membership == null || !completion.getStoryId()
            .equals(membership.getStoryId())) throw failure("Canonical Story membership is missing or incoherent.");
        boolean owned = membership.getOwnedResources()
            .getSessionIds()
            .contains(resourceId);
        boolean referenced = membership.getReferencedResources()
            .getSessionIds()
            .contains(resourceId);
        if (owned == referenced) throw failure("Canonical Session membership is missing or ambiguous.");
    }

    private static void requireId(String value, String name) {
        if (blank(value)) throw failure(name + " is required.");
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }

    private static IllegalStateException failure(String message) {
        return new IllegalStateException(message);
    }

    private static final class GraphIndex {

        private final Map<String, CanonicalGraphNode> byId;
        private final List<CanonicalGraphNode> nodes;

        GraphIndex(Map<String, CanonicalGraphNode> byId, List<CanonicalGraphNode> nodes) {
            this.byId = byId;
            this.nodes = nodes;
        }
    }
}
