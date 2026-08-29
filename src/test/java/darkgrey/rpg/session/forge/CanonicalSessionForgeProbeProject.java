package darkgrey.rpg.session.forge;

import java.util.Arrays;
import java.util.Collections;
import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

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
import darkgrey.rpg.graph.canonical.CanonicalStoryMembership;
import darkgrey.rpg.graph.canonical.CanonicalStoryMembershipSet;
import darkgrey.rpg.project.ActorDefinition;
import darkgrey.rpg.project.ProjectDefinition;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.quest.QuestDefinition;
import darkgrey.rpg.story.StoryDefinition;

/** Deterministic one-Line canonical project used by the Forge manager seam probe. */
public final class CanonicalSessionForgeProbeProject {

    private CanonicalSessionForgeProbeProject() {}

    public static ProjectSnapshot create() {
        return create(true);
    }

    public static ProjectSnapshot create(boolean connectedAggregateEnd) {
        Map<String, ActorDefinition> actors = new LinkedHashMap<String, ActorDefinition>();
        actors.put("actor_a", new ActorDefinition(1, "actor_a", "Actor A", "", Collections.<String>emptyList(), ""));

        Map<String, CanonicalGraphResource> sessions = new LinkedHashMap<String, CanonicalGraphResource>();
        sessions.put("session_a", session());
        Map<String, CanonicalGraphResource> stories = new LinkedHashMap<String, CanonicalGraphResource>();
        stories.put("story_a", story(connectedAggregateEnd));
        Map<String, CanonicalStoryMembership> memberships = new LinkedHashMap<String, CanonicalStoryMembership>();
        memberships.put(
            "story_a",
            new CanonicalStoryMembership(
                "story_a",
                new CanonicalStoryMembershipSet(
                    Arrays.asList("actor_a"),
                    Arrays.asList("session_a"),
                    Collections.<String>emptyList())));
        return new ProjectSnapshot(
            new ProjectDefinition(1, "probe", "Probe"),
            actors,
            Collections.<String, DialogueDefinition>emptyMap(),
            Collections.<String, QuestDefinition>emptyMap(),
            Collections.<String, StoryDefinition>emptyMap(),
            new CanonicalProjectContent(
                stories,
                sessions,
                Collections.<String, CanonicalGraphResource>emptyMap(),
                memberships));
    }

    private static CanonicalGraphResource story(boolean connectedAggregateEnd) {
        Map<String, JsonElement> properties = new HashMap<String, JsonElement>();
        properties.put("resource_id", json("session_a"));
        CanonicalGraphNode placement = node("place_a", "session", ports(in("flow_in"), out("end_a")), properties);
        CanonicalGraphNode target = node("next_a", "terminate", ports(in("flow_in")), props());
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.STORY,
            "story_a",
            "Story A",
            new CanonicalGraph(
                connectedAggregateEnd ? Arrays.asList(placement, target) : Arrays.asList(placement),
                connectedAggregateEnd ? Arrays.asList(edge("place_a", "end_a", "next_a", "flow_in"))
                    : Collections.<CanonicalGraphConnection>emptyList()));
    }

    private static CanonicalGraphResource session() {
        CanonicalGraphNode start = node("start", "start", ports(out("flow_out"), outLogic("logic_out")), props());
        CanonicalGraphNode line = node(
            "line",
            "line",
            ports(in("flow_in"), out("flow_out")),
            props("speaker_actor_id", "actor_a", "text", "Hello"));
        CanonicalGraphNode end = node(
            "end_a",
            "end",
            ports(in("flow_in")),
            props("port_id", "end_a", "display_name", "Done"));
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.SESSION,
            "session_a",
            "Session A",
            new CanonicalGraph(
                Arrays.asList(start, line, end),
                Arrays.asList(
                    edge("start", "flow_out", "line", "flow_in"),
                    edge("line", "flow_out", "end_a", "flow_in"))));
    }

    private static Map<String, JsonElement> props(String... values) {
        Map<String, JsonElement> result = new HashMap<String, JsonElement>();
        for (int index = 0; index < values.length; index += 2) result.put(values[index], json(values[index + 1]));
        return result;
    }

    private static Map<String, JsonElement> props() {
        return new HashMap<String, JsonElement>();
    }

    private static JsonElement json(String value) {
        return new JsonParser().parse("\"" + value + "\"");
    }

    private static CanonicalGraphNode node(String id, String type, List<CanonicalGraphPort> ports,
        Map<String, JsonElement> properties) {
        return new CanonicalGraphNode(id, type, id, ports, properties);
    }

    private static CanonicalGraphPort in(String id) {
        return port(id, CanonicalGraphPortDirection.INPUT);
    }

    private static CanonicalGraphPort out(String id) {
        return port(id, CanonicalGraphPortDirection.OUTPUT);
    }

    private static CanonicalGraphPort outLogic(String id) {
        return new CanonicalGraphPort(id, id, CanonicalGraphPortDirection.OUTPUT, CanonicalGraphInterfaceKind.LOGIC, 0);
    }

    private static CanonicalGraphPort port(String id, CanonicalGraphPortDirection direction) {
        return new CanonicalGraphPort(id, id, direction, CanonicalGraphInterfaceKind.FLOW, 0);
    }

    private static List<CanonicalGraphPort> ports(CanonicalGraphPort... values) {
        return Arrays.asList(values);
    }

    private static CanonicalGraphConnection edge(String fromNode, String fromPort, String toNode, String toPort) {
        return new CanonicalGraphConnection(fromNode, fromPort, toNode, toPort, CanonicalGraphInterfaceKind.FLOW);
    }
}
