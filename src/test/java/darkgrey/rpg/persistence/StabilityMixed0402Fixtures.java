package darkgrey.rpg.persistence;

import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

import com.google.gson.JsonElement;
import com.google.gson.JsonPrimitive;

import darkgrey.rpg.graph.canonical.CanonicalGraph;
import darkgrey.rpg.graph.canonical.CanonicalGraphConnection;
import darkgrey.rpg.graph.canonical.CanonicalGraphInterfaceKind;
import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphPort;
import darkgrey.rpg.graph.canonical.CanonicalGraphPortDirection;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceKind;
import darkgrey.rpg.graph.canonical.CanonicalProjectContent;
import darkgrey.rpg.graph.canonical.CanonicalStoryLogicGraph;
import darkgrey.rpg.graph.canonical.CanonicalStoryMembership;
import darkgrey.rpg.graph.canonical.CanonicalStoryMembershipSet;
import darkgrey.rpg.project.ActorDefinition;
import darkgrey.rpg.project.ItemResourceDefinition;
import darkgrey.rpg.project.ProjectDefinition;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.story.canonical.forge.Stability0402Fixtures;
import darkgrey.rpg.task.forge.MixedBusiness0402Probe;

/** Legal current resource/membership closure for the service-level real inventory/host driver. */
public final class StabilityMixed0402Fixtures {

    public static final String STORY = Stability0402Fixtures.SOURCE;
    public static final String ACTOR = STORY + "~actor~clerk";
    public static final String ITEM = STORY + "~item~coin";
    public static final String[] TYPES = { "kill_entity", "collect_item", "submit_item" };

    private StabilityMixed0402Fixtures() {}

    /** Test-only union; authored resource objects and media bytes are never rewritten. */
    public static ProjectSnapshot withAuthored(ProjectSnapshot authored) {
        ProjectSnapshot generated = project();
        List<darkgrey.rpg.graph.canonical.CanonicalStoryLogicConnection> edges = new ArrayList<>(
            authored.getCanonicalStoryLogicConnections());
        edges.addAll(generated.getCanonicalStoryLogicConnections());
        return new ProjectSnapshot(
            authored.getProject(),
            union(authored.getActors(), generated.getActors()),
            union(authored.getItems(), generated.getItems()),
            union(authored.getItemGroups(), generated.getItemGroups()),
            new CanonicalProjectContent(
                union(authored.getCanonicalStories(), generated.getCanonicalStories()),
                union(authored.getCanonicalSessions(), generated.getCanonicalSessions()),
                union(authored.getCanonicalTasks(), generated.getCanonicalTasks()),
                union(authored.getCanonicalStoryMemberships(), generated.getCanonicalStoryMemberships()),
                new CanonicalStoryLogicGraph(edges)));
    }

    private static <T> Map<String, T> union(Map<String, T> first, Map<String, T> second) {
        Map<String, T> result = new LinkedHashMap<>(first);
        for (Map.Entry<String, T> value : second.entrySet()) {
            if (result.put(value.getKey(), value.getValue()) != null)
                throw new IllegalStateException("Test union identity collision");
        }
        return result;
    }

    public static String story(int kind) {
        return kind == 0 ? STORY : Stability0402Fixtures.loadStory(100 + kind);
    }

    public static CanonicalGraphResource task(int kind) {
        CanonicalGraphResource original = MixedBusiness0402Probe.fixtureResource(kind);
        return new CanonicalGraphResource(
            3,
            CanonicalGraphResourceKind.TASK,
            story(kind) + "~task~work",
            "Mixed",
            original.getGraph());
    }

    public static ProjectSnapshot project() {
        ProjectSnapshot base = Stability0402Fixtures.load(1, 20);
        Map<String, CanonicalGraphResource> stories = new LinkedHashMap<>();
        Map<String, CanonicalGraphResource> tasks = new LinkedHashMap<>();
        Map<String, CanonicalStoryMembership> members = new LinkedHashMap<>();
        for (int kind = 0; kind < 3; kind++) {
            CanonicalGraphResource resource = task(kind);
            tasks.put(resource.getId(), resource);
            List<CanonicalGraphNode> nodes = new ArrayList<>();
            List<CanonicalGraphConnection> edges = new ArrayList<>();
            nodes.add(
                base.getCanonicalStory(Stability0402Fixtures.loadStory(0))
                    .getGraph()
                    .getNodes()
                    .get(0));
            Map<String, JsonElement> properties = new LinkedHashMap<>();
            properties.put("resource_id", new JsonPrimitive(resource.getId()));
            List<CanonicalGraphPort> ports = new ArrayList<>();
            ports.add(
                new CanonicalGraphPort(
                    "flow_in",
                    "In",
                    CanonicalGraphPortDirection.INPUT,
                    CanonicalGraphInterfaceKind.FLOW,
                    0));
            if (kind != 1) ports.add(
                new CanonicalGraphPort(
                    "done",
                    "Done",
                    CanonicalGraphPortDirection.OUTPUT,
                    CanonicalGraphInterfaceKind.FLOW,
                    1));
            nodes.add(new CanonicalGraphNode("work", "task", "Mixed task", ports, properties));
            edges.add(
                new CanonicalGraphConnection("start", "entry", "work", "flow_in", CanonicalGraphInterfaceKind.FLOW));
            if (kind != 1) {
                nodes.add(
                    Stability0402Fixtures.flow()
                        .getCanonicalStory(STORY)
                        .getGraph()
                        .getNodes()
                        .get(1));
                edges.add(
                    new CanonicalGraphConnection("work", "done", "end", "flow_in", CanonicalGraphInterfaceKind.FLOW));
            }
            stories.put(
                story(kind),
                new CanonicalGraphResource(
                    3,
                    CanonicalGraphResourceKind.STORY,
                    story(kind),
                    "Mixed",
                    new CanonicalGraph(nodes, edges)));
            members.put(
                story(kind),
                new CanonicalStoryMembership(
                    story(kind),
                    new CanonicalStoryMembershipSet(
                        kind == 0 ? Collections.singletonList(ACTOR) : Collections.emptyList(),
                        kind == 0 ? Collections.singletonList(ITEM) : Collections.emptyList(),
                        Collections.emptyList(),
                        Collections.emptyList(),
                        Collections.singletonList(resource.getId())),
                    new CanonicalStoryMembershipSet(
                        kind == 0 ? Collections.emptyList() : Collections.singletonList(ACTOR),
                        kind == 0 ? Collections.emptyList() : Collections.singletonList(ITEM),
                        Collections.emptyList(),
                        Collections.emptyList(),
                        Collections.emptyList())));
        }
        return new ProjectSnapshot(
            new ProjectDefinition(3, "mixed0402", "Mixed 0402"),
            Collections.singletonMap(
                ACTOR,
                new ActorDefinition(
                    3,
                    ActorDefinition.TYPE_INDIVIDUAL,
                    ACTOR,
                    "Clerk",
                    "",
                    Collections.emptyList(),
                    STORY)),
            Collections.singletonMap(
                ITEM,
                new ItemResourceDefinition(
                    3,
                    ItemResourceDefinition.TYPE_INDIVIDUAL,
                    ITEM,
                    "Coin",
                    Collections.emptyList())),
            Collections.emptyMap(),
            new CanonicalProjectContent(
                stories,
                Collections.emptyMap(),
                tasks,
                members,
                new CanonicalStoryLogicGraph(Collections.emptyList())));
    }
}
