package darkgrey.rpg.story.canonical;

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
import darkgrey.rpg.project.ProjectDefinition;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.session.server.CanonicalSessionCompletionResult;

/** Focused pure Story Flow Session-completion routing probe. */
public final class CanonicalStorySessionCompletionRouterProbe {

    private static final UUID PLAYER = UUID.fromString("00000000-0000-0000-0000-000000000030");

    public static void main(String[] args) {
        CanonicalGraphResource story = story("Session Aggregate", "End A", "End B", "Logic A", "Logic B");
        LinkedHashMap<String, Boolean> input = new LinkedHashMap<String, Boolean>();
        input.put("logic_b", Boolean.TRUE);
        input.put("logic_a", Boolean.FALSE);
        CanonicalSessionCompletionResult completion = completion("session_1", "end_b", input);
        CanonicalStorySessionCompletionRoute route = new CanonicalStorySessionCompletionRouter(story).route(completion);
        require(
            "target_b".equals(
                route.getNextFlow()
                    .getTargetNodeId()),
            "selected End successor");
        require(
            "flow_in".equals(
                route.getNextFlow()
                    .getTargetPortId()),
            "successor Flow input");
        List<String> order = Arrays.asList("logic_a", "logic_b");
        require(
            order.equals(
                Arrays.asList(
                    route.getPublicLogic()
                        .getValues()
                        .keySet()
                        .toArray(new String[0]))),
            "aggregate Logic port order");
        require(
            Boolean.FALSE.equals(
                route.getPublicLogic()
                    .getValues()
                    .get("logic_a")),
            "logic_a value");
        require(
            Boolean.TRUE.equals(
                route.getPublicLogic()
                    .getValues()
                    .get("logic_b")),
            "logic_b value");
        reject(new Runnable() {

            @Override
            public void run() {
                route.getPublicLogic()
                    .getValues()
                    .put("new", Boolean.TRUE);
            }
        }, "immutable Logic snapshot");

        CanonicalGraphResource renamed = story("Renamed Session", "Renamed A", "Renamed B", "Renamed A", "Renamed B");
        CanonicalStorySessionCompletionRoute renamedRoute = new CanonicalStorySessionCompletionRouter(renamed)
            .route(completion);
        require(
            "target_b".equals(
                renamedRoute.getNextFlow()
                    .getTargetNodeId()),
            "display-name-stable route");

        ProjectSnapshot boundProject = project(story, session("session_1"), membership(false));
        require(
            "target_b".equals(
                new CanonicalStorySessionCompletionRouter(boundProject).route(completion)
                    .getNextFlow()
                    .getTargetNodeId()),
            "ProjectSnapshot binding");
        require(
            "target_b".equals(
                new CanonicalStorySessionCompletionRouter(boundProject, "story_1").route(completion)
                    .getNextFlow()
                    .getTargetNodeId()),
            "ProjectSnapshot Story context binding");
        reject(new Runnable() {

            @Override
            public void run() {
                new CanonicalStorySessionCompletionRouter(project(story, null, membership(false))).route(completion);
            }
        }, "missing Session resource");
        reject(new Runnable() {

            @Override
            public void run() {
                new CanonicalStorySessionCompletionRouter(
                    project(
                        story,
                        new CanonicalGraphResource(1, CanonicalGraphResourceKind.STORY, "session_1", "Drift", null),
                        membership(false))).route(completion);
            }
        }, "Session kind drift");
        reject(new Runnable() {

            @Override
            public void run() {
                new CanonicalStorySessionCompletionRouter(
                    project(
                        story,
                        new CanonicalGraphResource(1, CanonicalGraphResourceKind.SESSION, "other", "Drift", null),
                        membership(false))).route(completion);
            }
        }, "Session ID drift");
        reject(new Runnable() {

            @Override
            public void run() {
                new CanonicalStorySessionCompletionRouter(project(story, session("session_1"), null)).route(completion);
            }
        }, "missing Story membership");
        reject(new Runnable() {

            @Override
            public void run() {
                new CanonicalStorySessionCompletionRouter(project(story, session("session_1"), membership(true)))
                    .route(completion);
            }
        }, "ambiguous Story membership");
        reject(new Runnable() {

            @Override
            public void run() {
                new CanonicalStorySessionCompletionRouter(boundProject, "wrong_story").route(completion);
            }
        }, "Project Story context drift");

        reject(new Runnable() {

            @Override
            public void run() {
                new CanonicalStorySessionCompletionRouter(story).route(completion("drift", "end_b", input));
            }
        }, "resource drift");
        LinkedHashMap<String, Boolean> missing = new LinkedHashMap<String, Boolean>();
        missing.put("logic_a", Boolean.FALSE);
        reject(new Runnable() {

            @Override
            public void run() {
                new CanonicalStorySessionCompletionRouter(story).route(completion("session_1", "end_b", missing));
            }
        }, "missing Logic output");
        LinkedHashMap<String, Boolean> extra = new LinkedHashMap<String, Boolean>(input);
        extra.put("unknown", Boolean.TRUE);
        reject(new Runnable() {

            @Override
            public void run() {
                new CanonicalStorySessionCompletionRouter(story).route(completion("session_1", "end_b", extra));
            }
        }, "extra Logic output");
        reject(new Runnable() {

            @Override
            public void run() {
                new CanonicalStorySessionCompletionRouter(story).route(completion("session_1", "missing", input));
            }
        }, "unconnected End");
        reject(new Runnable() {

            @Override
            public void run() {
                new CanonicalStorySessionCompletionRouter(fanoutStory()).route(completion);
            }
        }, "fanout End");
        System.out.println("CANONICAL_STORY_SESSION_COMPLETION_ROUTER_PROBE=PASS");
    }

    private static CanonicalSessionCompletionResult completion(String resourceId, String endId,
        Map<String, Boolean> logic) {
        return new CanonicalSessionCompletionResult(PLAYER, "story_1", "placement", resourceId, 1L, endId, logic);
    }

    private static CanonicalGraphResource story(String storyName, String endAName, String endBName, String logicAName,
        String logicBName) {
        CanonicalGraphNode placement = node(
            "placement",
            "session",
            storyName,
            ports(
                in("flow_in", "Flow In", CanonicalGraphInterfaceKind.FLOW, 0),
                in("logic_in", "Logic In", CanonicalGraphInterfaceKind.LOGIC, 1),
                out("end_a", endAName, CanonicalGraphInterfaceKind.FLOW, 2),
                out("end_b", endBName, CanonicalGraphInterfaceKind.FLOW, 3),
                out("logic_a", logicAName, CanonicalGraphInterfaceKind.LOGIC, 4),
                out("logic_b", logicBName, CanonicalGraphInterfaceKind.LOGIC, 5)),
            props("resource_id", "session_1"));
        CanonicalGraphNode targetA = node(
            "target_a",
            "action",
            "A",
            ports(in("flow_in", "A", CanonicalGraphInterfaceKind.FLOW, 0)),
            props());
        CanonicalGraphNode targetB = node(
            "target_b",
            "terminate",
            "B",
            ports(in("flow_in", "B", CanonicalGraphInterfaceKind.FLOW, 0)),
            props());
        return resource(
            Arrays.asList(placement, targetA, targetB),
            Arrays.asList(
                edge("placement", "end_a", "target_a", "flow_in", CanonicalGraphInterfaceKind.FLOW),
                edge("placement", "end_b", "target_b", "flow_in", CanonicalGraphInterfaceKind.FLOW)));
    }

    private static CanonicalGraphResource fanoutStory() {
        CanonicalGraphResource base = story("Session", "A", "B", "A", "B");
        List<CanonicalGraphConnection> edges = Arrays.asList(
            edge("placement", "end_a", "target_a", "flow_in", CanonicalGraphInterfaceKind.FLOW),
            edge("placement", "end_b", "target_a", "flow_in", CanonicalGraphInterfaceKind.FLOW),
            edge("placement", "end_b", "target_b", "flow_in", CanonicalGraphInterfaceKind.FLOW));
        return resource(
            base.getGraph()
                .getNodes(),
            edges);
    }

    private static ProjectSnapshot project(CanonicalGraphResource story, CanonicalGraphResource session,
        CanonicalStoryMembership membership) {
        Map<String, CanonicalGraphResource> stories = new LinkedHashMap<String, CanonicalGraphResource>();
        stories.put("story_1", story);
        Map<String, CanonicalGraphResource> sessions = new LinkedHashMap<String, CanonicalGraphResource>();
        if (session != null) sessions.put("session_1", session);
        Map<String, CanonicalStoryMembership> memberships = new LinkedHashMap<String, CanonicalStoryMembership>();
        if (membership != null) memberships.put("story_1", membership);
        return new ProjectSnapshot(
            new ProjectDefinition(1, "probe", "Probe"),
            Collections.emptyMap(),
            Collections.emptyMap(),
            Collections.emptyMap(),
            Collections.emptyMap(),
            new CanonicalProjectContent(stories, sessions, Collections.emptyMap(), memberships));
    }

    private static CanonicalGraphResource session(String id) {
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.SESSION,
            id,
            "Session",
            new CanonicalGraph(
                Collections.<CanonicalGraphNode>emptyList(),
                Collections.<CanonicalGraphConnection>emptyList()));
    }

    private static CanonicalStoryMembership membership(boolean ambiguous) {
        CanonicalStoryMembershipSet owned = new CanonicalStoryMembershipSet(
            Collections.<String>emptyList(),
            Arrays.asList("session_1"),
            Collections.<String>emptyList());
        CanonicalStoryMembershipSet referenced = ambiguous
            ? new CanonicalStoryMembershipSet(
                Collections.<String>emptyList(),
                Arrays.asList("session_1"),
                Collections.<String>emptyList())
            : new CanonicalStoryMembershipSet();
        return new CanonicalStoryMembership("story_1", owned, referenced);
    }

    private static CanonicalGraphResource resource(List<CanonicalGraphNode> nodes,
        List<CanonicalGraphConnection> edges) {
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.STORY,
            "story_1",
            "Story",
            new CanonicalGraph(nodes, edges));
    }

    private static CanonicalGraphNode node(String id, String type, String displayName, List<CanonicalGraphPort> ports,
        Map<String, JsonElement> properties) {
        // Fixture upgrade: public termination metadata belongs in test data, not runtime fallback.
        if ("terminate".equals(type)) {
            properties = new java.util.LinkedHashMap<String, JsonElement>(properties);
            if (!properties.containsKey("port_id")) properties.put("port_id", new com.google.gson.JsonPrimitive(id));
            if (!properties.containsKey("display_name"))
                properties.put("display_name", new com.google.gson.JsonPrimitive(id));
        }
        return new CanonicalGraphNode(id, type, displayName, ports, properties);
    }

    private static CanonicalGraphPort in(String id, String name, CanonicalGraphInterfaceKind kind, int order) {
        return new CanonicalGraphPort(id, name, CanonicalGraphPortDirection.INPUT, kind, order);
    }

    private static CanonicalGraphPort out(String id, String name, CanonicalGraphInterfaceKind kind, int order) {
        return new CanonicalGraphPort(id, name, CanonicalGraphPortDirection.OUTPUT, kind, order);
    }

    private static List<CanonicalGraphPort> ports(CanonicalGraphPort... ports) {
        return Arrays.asList(ports);
    }

    private static CanonicalGraphConnection edge(String fromNode, String fromPort, String toNode, String toPort,
        CanonicalGraphInterfaceKind kind) {
        return new CanonicalGraphConnection(fromNode, fromPort, toNode, toPort, kind);
    }

    private static Map<String, JsonElement> props(String... values) {
        Map<String, JsonElement> result = new LinkedHashMap<String, JsonElement>();
        for (int i = 0; i < values.length; i += 2)
            result.put(values[i], new JsonParser().parse("\"" + values[i + 1] + "\""));
        return result;
    }

    private static Map<String, JsonElement> props() {
        return Collections.emptyMap();
    }

    private static void reject(Runnable action, String label) {
        try {
            action.run();
        } catch (RuntimeException expected) {
            return;
        }
        throw new IllegalStateException("Expected rejection: " + label);
    }

    private static void require(boolean condition, String label) {
        if (!condition) throw new IllegalStateException("Probe failure: " + label);
    }
}
