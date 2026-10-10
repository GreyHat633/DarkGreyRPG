package darkgrey.rpg.story.canonical.forge;

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
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceKind;
import darkgrey.rpg.graph.canonical.CanonicalProjectContent;
import darkgrey.rpg.graph.canonical.CanonicalStoryLogicConnection;
import darkgrey.rpg.graph.canonical.CanonicalStoryLogicGraph;
import darkgrey.rpg.graph.canonical.CanonicalStoryMembership;
import darkgrey.rpg.graph.canonical.CanonicalStoryMembershipSet;
import darkgrey.rpg.project.ActorDefinition;
import darkgrey.rpg.project.ProjectDefinition;
import darkgrey.rpg.project.ProjectSnapshot;

public final class Stability0402Fixtures {

    public static final String SOURCE = "ST-AAAA-BBBB-CCCC-DDDD";
    public static final String TARGET = "ST-2222-3333-4444-5555";

    private Stability0402Fixtures() {}

    public static String loadStory(int index) {
        char[] digits = new char[16];
        long value = index + 402000L;
        for (int i = 15; i >= 0; i--) {
            digits[i] = darkgrey.rpg.identity.StoryUid.ALPHABET.charAt((int) (value % 32));
            value /= 32;
        }
        String hex = new String(digits);
        return "ST-" + hex
            .substring(0, 4) + "-" + hex.substring(4, 8) + "-" + hex.substring(8, 12) + "-" + hex.substring(12);
    }

    /** Each placement exists in an owned current Story; objectives remain ACTIVE with zero settlement. */
    public static ProjectSnapshot load(int storyCount, int graphNodes) {
        ProjectSnapshot flow = flow();
        Map<String, CanonicalGraphResource> stories = new LinkedHashMap<>(flow.getCanonicalStories());
        Map<String, CanonicalGraphResource> tasks = new LinkedHashMap<>();
        Map<String, ActorDefinition> actors = new LinkedHashMap<>();
        List<String> actorIds = new ArrayList<>();
        for (int n = 0; n < 10; n++) {
            String actor = SOURCE + "~actor~load" + n;
            actorIds.add(actor);
            actors.put(
                actor,
                new ActorDefinition(
                    3,
                    ActorDefinition.TYPE_INDIVIDUAL,
                    actor,
                    "Load " + n,
                    "",
                    Collections.emptyList(),
                    SOURCE));
        }
        Map<String, CanonicalStoryMembership> members = new LinkedHashMap<>(flow.getCanonicalStoryMemberships());
        members.put(
            SOURCE,
            new CanonicalStoryMembership(
                SOURCE,
                new CanonicalStoryMembershipSet(actorIds, Collections.emptyList(), Collections.emptyList())));
        for (int i = 0; i < storyCount; i++) {
            String id = loadStory(i), task = id + "~task~work";
            CanonicalGraphResource terminal = terminal(id, "entry", "enter_story");
            CanonicalGraphNode start = terminal.getGraph()
                .getNodes()
                .get(0);
            Map<String, JsonElement> properties = new LinkedHashMap<>();
            properties.put("resource_id", new JsonParser().parse("\"" + task + "\""));
            CanonicalGraphNode placement = new CanonicalGraphNode(
                "work",
                "task",
                "Work",
                Collections.singletonList(
                    new CanonicalGraphPort(
                        "flow_in",
                        "In",
                        CanonicalGraphPortDirection.INPUT,
                        CanonicalGraphInterfaceKind.FLOW,
                        0)),
                properties);
            stories.put(
                id,
                new CanonicalGraphResource(
                    3,
                    CanonicalGraphResourceKind.STORY,
                    id,
                    "Load " + i,
                    new CanonicalGraph(
                        Arrays.asList(start, placement),
                        Collections.singletonList(
                            new CanonicalGraphConnection(
                                "start",
                                "entry",
                                "work",
                                "flow_in",
                                CanonicalGraphInterfaceKind.FLOW)))));
            List<CanonicalGraphNode> objectives = new ArrayList<>();
            for (int n = 0; n < graphNodes; n++) {
                Map<String, JsonElement> fields = new LinkedHashMap<>();
                fields.put("objective_type", new JsonParser().parse("\"kill_entity\""));
                fields.put("entity", new JsonParser().parse("\"" + SOURCE + "~actor~load" + (n % 10) + "\""));
                fields.put("description", new JsonParser().parse("\"Load objective\""));
                fields.put("required", new JsonParser().parse("2000000"));
                objectives.add(
                    new CanonicalGraphNode(
                        "objective" + n,
                        "objective",
                        "Objective " + n,
                        Collections.singletonList(
                            new CanonicalGraphPort(
                                "logic_status",
                                "Status",
                                CanonicalGraphPortDirection.OUTPUT,
                                CanonicalGraphInterfaceKind.LOGIC,
                                0)),
                        fields));
            }
            tasks.put(
                task,
                new CanonicalGraphResource(
                    3,
                    CanonicalGraphResourceKind.TASK,
                    task,
                    "Work " + i,
                    new CanonicalGraph(objectives, Collections.emptyList())));
            members.put(
                id,
                new CanonicalStoryMembership(
                    id,
                    new CanonicalStoryMembershipSet(
                        Collections.emptyList(),
                        Collections.emptyList(),
                        Collections.singletonList(task)),
                    new CanonicalStoryMembershipSet(actorIds, Collections.emptyList(), Collections.emptyList())));
        }
        return new ProjectSnapshot(
            new ProjectDefinition(3, "stability0402", "Load 0402"),
            actors,
            Collections.emptyMap(),
            Collections.emptyMap(),
            new CanonicalProjectContent(
                stories,
                Collections.emptyMap(),
                tasks,
                members,
                flow.getCanonicalContent()
                    .getStoryLogicGraph()));
    }

    public static ProjectSnapshot flow() {
        Map<String, CanonicalGraphResource> stories = new LinkedHashMap<>();
        stories.put(SOURCE, terminal(SOURCE, "out", "enter_story"));
        stories.put(TARGET, terminal(TARGET, "in", "flow_driven"));
        Map<String, CanonicalStoryMembership> members = new LinkedHashMap<>();
        for (String id : stories.keySet()) members.put(
            id,
            new CanonicalStoryMembership(
                id,
                new CanonicalStoryMembershipSet(
                    Collections.emptyList(),
                    Collections.emptyList(),
                    Collections.emptyList())));
        return new ProjectSnapshot(
            new ProjectDefinition(3, "stability0402", "Stability 0402"),
            Collections.emptyMap(),
            Collections.emptyMap(),
            Collections.emptyMap(),
            new CanonicalProjectContent(
                stories,
                Collections.emptyMap(),
                Collections.emptyMap(),
                members,
                new CanonicalStoryLogicGraph(
                    Collections.singletonList(
                        new CanonicalStoryLogicConnection(
                            SOURCE,
                            "out",
                            TARGET,
                            "in",
                            CanonicalGraphInterfaceKind.FLOW)))));
    }

    public static ProjectSnapshot flowChain(int count, boolean loop) {
        Map<String, CanonicalGraphResource> stories = new LinkedHashMap<>();
        Map<String, CanonicalStoryMembership> members = new LinkedHashMap<>();
        java.util.List<CanonicalStoryLogicConnection> edges = new java.util.ArrayList<>();
        for (int i = 0; i < count; i++) {
            String id = loadStory(i);
            stories.put(id, terminal(id, "out", i == 0 ? "enter_story" : "flow_driven"));
            members.put(
                id,
                new CanonicalStoryMembership(
                    id,
                    new CanonicalStoryMembershipSet(
                        Collections.emptyList(),
                        Collections.emptyList(),
                        Collections.emptyList())));
            if (i > 0) edges.add(
                new CanonicalStoryLogicConnection(
                    loadStory(i - 1),
                    "out",
                    id,
                    "out",
                    CanonicalGraphInterfaceKind.FLOW));
        }
        if (loop) edges.add(
            new CanonicalStoryLogicConnection(
                loadStory(count - 1),
                "out",
                loadStory(0),
                "out",
                CanonicalGraphInterfaceKind.FLOW));
        return new ProjectSnapshot(
            new ProjectDefinition(3, "stability0402", "Flow chain"),
            Collections.emptyMap(),
            Collections.emptyMap(),
            Collections.emptyMap(),
            new CanonicalProjectContent(
                stories,
                Collections.emptyMap(),
                Collections.emptyMap(),
                members,
                new CanonicalStoryLogicGraph(edges)));
    }

    static CanonicalGraphResource terminal(String id, String trigger, String type) {
        Map<String, JsonElement> startProperties = new LinkedHashMap<>();
        startProperties.put("repeat_policy", new JsonParser().parse("\"once\""));
        startProperties.put(
            "triggers",
            new JsonParser().parse(
                "[{\"port_id\":\"" + trigger
                    + "\",\"display_name\":\"Start\",\"trigger_type\":\""
                    + type
                    + "\",\"trigger_properties\":{},\"order\":0}]"));
        CanonicalGraphNode start = new CanonicalGraphNode(
            "start",
            "start",
            "Start",
            Collections.singletonList(
                new CanonicalGraphPort(
                    trigger,
                    "Start",
                    CanonicalGraphPortDirection.OUTPUT,
                    CanonicalGraphInterfaceKind.FLOW,
                    0)),
            startProperties);
        Map<String, JsonElement> endProperties = new LinkedHashMap<>();
        endProperties.put("port_id", new JsonParser().parse("\"" + trigger + "\""));
        endProperties.put("display_name", new JsonParser().parse("\"End\""));
        endProperties.put("display_order", new JsonParser().parse("0"));
        CanonicalGraphNode end = new CanonicalGraphNode(
            "end",
            "terminate",
            "End",
            Collections.singletonList(
                new CanonicalGraphPort(
                    "flow_in",
                    "In",
                    CanonicalGraphPortDirection.INPUT,
                    CanonicalGraphInterfaceKind.FLOW,
                    0)),
            endProperties);
        return new CanonicalGraphResource(
            3,
            CanonicalGraphResourceKind.STORY,
            id,
            "Flow",
            new CanonicalGraph(
                Arrays.asList(start, end),
                Collections.singletonList(
                    new CanonicalGraphConnection(
                        "start",
                        trigger,
                        "end",
                        "flow_in",
                        CanonicalGraphInterfaceKind.FLOW))));
    }
}
