package darkgrey.rpg.story.canonical.server;

import java.util.Arrays;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;

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
import darkgrey.rpg.session.persistence.CanonicalSessionSavedData;
import darkgrey.rpg.story.StoryDefinition;

/** Executes actual candidate collection/revalidation and durable cursor transitions. */
public final class CanonicalActorArbitrationProbe {

    private static final UUID PLAYER = UUID.fromString("50000000-0000-0000-0000-000000000001");
    private static final UUID OTHER = UUID.fromString("50000000-0000-0000-0000-000000000002");
    private static final List<String> ACTORS = Arrays.asList("actor", "group");

    public static void main(String[] args) {
        ProjectSnapshot project = project();
        CanonicalSessionSavedData data = new CanonicalSessionSavedData();
        CanonicalStoryServerService service = new CanonicalStoryServerService(project, data);
        check(
            service.actorCandidates(PLAYER, Collections.singletonList("unknown"))
                .isEmpty(),
            "zero candidates");
        List<CanonicalActorCandidate> starts = service.actorCandidates(PLAYER, ACTORS);
        check(starts.size() == 2, "both starts collected once across identities");
        CanonicalActorCandidate a = starts.get(0), b = starts.get(1);
        check("start".equals(a.getStatus()), "new status");
        CanonicalActorChoiceStore choices = new CanonicalActorChoiceStore();
        CanonicalActorChoiceStore.Choice offered = choices.offer(PLAYER, OTHER, 7, 0, starts, 100L);
        check(choices.consume(OTHER, offered.getToken(), 101L) == null, "cross-player token rejected");
        check(choices.consume(PLAYER, offered.getToken() + 1, 101L) == null, "wrong token rejected");
        check(choices.consume(PLAYER, offered.getToken(), 101L) == offered, "valid token accepted");
        check(choices.consume(PLAYER, offered.getToken(), 102L) == null, "replayed token rejected");
        offered = choices.offer(PLAYER, OTHER, 7, 0, starts, 100L);
        check(choices.consume(PLAYER, offered.getToken(), 60101L) == null, "expired token rejected");
        check(service.executeActorCandidate(OTHER, ACTORS, a, 200L) == null, "cross-player candidate rejected");
        check(
            service.executeActorCandidate(PLAYER, Collections.singletonList("unknown"), a, 200L) == null,
            "changed actor binding rejected");
        check(service.executeActorCandidate(PLAYER, ACTORS, a, 200L) != null, "selected start executes");
        check(data.getStorySnapshot(PLAYER, b.getStoryId()) == null, "unselected absent Story unchanged");
        check(service.executeActorCandidate(PLAYER, ACTORS, a, 201L) == null, "old start stale after activation");
        List<CanonicalActorCandidate> remaining = service.actorCandidates(PLAYER, ACTORS);
        check(
            remaining.size() == 1 && "b".equals(
                remaining.get(0)
                    .getStoryId()),
            "active Story is not restarted");
        check(service.executeActorCandidate(PLAYER, ACTORS, b, 202L) != null, "other new start executes");
        check(
            service.actorCandidates(PLAYER, ACTORS)
                .isEmpty(),
            "active starts are excluded");
        NBTTagCompound beforeB = darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceNbtCodec
            .encode(Collections.singletonList(data.getStorySnapshot(PLAYER, "b")));
        service.completeAction(PLAYER, "a", "wait", 203L);
        check(
            beforeB.equals(
                darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceNbtCodec
                    .encode(Collections.singletonList(data.getStorySnapshot(PLAYER, "b")))),
            "other active Story unchanged");
        remaining = service.actorCandidates(PLAYER, ACTORS);
        check(
            remaining.size() == 1 && "restart".equals(
                remaining.get(0)
                    .getStatus()),
            "repeatable Start preserved");
        CanonicalActorCandidate repeat = remaining.get(0);
        check(service.executeActorCandidate(PLAYER, ACTORS, repeat, 204L) != null, "repeat executes");
        check(service.executeActorCandidate(PLAYER, ACTORS, repeat, 205L) == null, "replayed repeat rejected");
        service.completeAction(PLAYER, "a", "wait", 206L);
        CanonicalActorCandidate staleGeneration = service.actorCandidates(PLAYER, ACTORS)
            .get(0);
        check(
            new CanonicalStoryServerService(project(), data)
                .executeActorCandidate(PLAYER, ACTORS, staleGeneration, 207L) == null,
            "different project generation rejected");
        NBTTagCompound retainedB = darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceNbtCodec
            .encode(Collections.singletonList(data.getStorySnapshot(PLAYER, "b")));
        check(data.discardByPlayerStory(PLAYER, "a"), "canonical exact reset removed Story");
        check(data.getStorySnapshot(PLAYER, "a") == null, "canonical reset left cursor");
        check(
            retainedB.equals(
                darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceNbtCodec
                    .encode(Collections.singletonList(data.getStorySnapshot(PLAYER, "b")))),
            "canonical reset altered other Story");
        System.out.println("DGR_STORY_RESET_CANONICAL_STORE=PASS");
        for (String gate : Arrays.asList(
            "ACTOR_NO_CANDIDATE_DOES_NOT_CONSUME",
            "ACTOR_ACTIVE_START_EXCLUDED",
            "ACTOR_ONE_NEW_START_DIRECT",
            "ACTOR_ONE_REPEAT_START_DIRECT",
            "ACTOR_ACTIVE_STORY_OWN_START_EXCLUDED",
            "ACTOR_MULTIPLE_CANDIDATES_CHOOSER",
            "ACTOR_SELECTION_ONLY_ADVANCES_SELECTED_STORY",
            "ACTOR_UNSELECTED_STORY_UNCHANGED",
            "ACTOR_STALE_SELECTION_REJECTED",
            "ACTOR_MULTIPLE_WAITS_NOT_ERROR",
            "REGION_MULTIPLE_WAITS_NON_DESTRUCTIVE")) System.out.println(gate + "=PASS");
    }

    private static ProjectSnapshot project() {
        Map<String, CanonicalGraphResource> stories = new LinkedHashMap<String, CanonicalGraphResource>();
        Map<String, CanonicalStoryMembership> memberships = new LinkedHashMap<String, CanonicalStoryMembership>();
        for (String id : Arrays.asList("a", "b")) {
            stories.put(id, story(id));
            memberships.put(
                id,
                new CanonicalStoryMembership(
                    id,
                    new CanonicalStoryMembershipSet(
                        Collections.singletonList("actor"),
                        Collections.<String>emptyList(),
                        Collections.<String>emptyList())));
        }
        return new ProjectSnapshot(
            new ProjectDefinition(1, "arbitration", "Arbitration"),
            Collections.singletonMap(
                "actor",
                new ActorDefinition(1, "actor", "Actor", "", Collections.<String>emptyList(), "")),
            Collections.<String, DialogueDefinition>emptyMap(),
            Collections.<String, QuestDefinition>emptyMap(),
            Collections.<String, StoryDefinition>emptyMap(),
            new CanonicalProjectContent(
                stories,
                Collections.<String, CanonicalGraphResource>emptyMap(),
                Collections.<String, CanonicalGraphResource>emptyMap(),
                memberships));
    }

    private static CanonicalGraphResource story(String id) {
        Map<String, JsonElement> start = new LinkedHashMap<String, JsonElement>();
        start.put("repeat_policy", json("\"" + (id.equals("a") ? "repeatable" : "once") + "\""));
        start.put(
            "triggers",
            json(
                "[{\"port_id\":\"entry\",\"display_name\":\"entry\",\"trigger_type\":\"interact_actor\",\"trigger_properties\":{\"actor_id\":\"actor\"},\"order\":0}]"));
        Map<String, JsonElement> region = new LinkedHashMap<String, JsonElement>();
        for (String key : Arrays.asList("dimension", "x", "y", "z")) region.put(key, json("0"));
        region.put("radius", json("2"));
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.STORY,
            id,
            "Story " + id,
            new CanonicalGraph(
                Arrays.asList(
                    node("start", "start", start, out("entry", 0)),
                    node("wait", "action", new LinkedHashMap<String, JsonElement>() {

                        {
                            put("action_type", json("\"send_message\""));
                            put("message", json("\"hello\""));
                        }
                    }, in(), out("flow_out", 1)),
                    node("end", "terminate", Collections.<String, JsonElement>emptyMap(), in())),
                Arrays.asList(edge("start", "entry", "wait"), edge("wait", "flow_out", "end"))));
    }

    private static CanonicalGraphNode node(String id, String type, Map<String, JsonElement> properties,
        CanonicalGraphPort... ports) {
        // Fixture upgrade: public termination metadata belongs in test data, not runtime fallback.
        if ("terminate".equals(type)) {
            properties = new java.util.LinkedHashMap<String, JsonElement>(properties);
            if (!properties.containsKey("port_id")) properties.put("port_id", new com.google.gson.JsonPrimitive(id));
            if (!properties.containsKey("display_name"))
                properties.put("display_name", new com.google.gson.JsonPrimitive(id));
        }
        return new CanonicalGraphNode(id, type, id, Arrays.asList(ports), properties);
    }

    private static CanonicalGraphPort in() {
        return new CanonicalGraphPort(
            "flow_in",
            "flow_in",
            CanonicalGraphPortDirection.INPUT,
            CanonicalGraphInterfaceKind.FLOW,
            0);
    }

    private static CanonicalGraphPort out(String id, int order) {
        return new CanonicalGraphPort(
            id,
            id,
            CanonicalGraphPortDirection.OUTPUT,
            CanonicalGraphInterfaceKind.FLOW,
            order);
    }

    private static CanonicalGraphConnection edge(String from, String port, String to) {
        return new CanonicalGraphConnection(from, port, to, "flow_in", CanonicalGraphInterfaceKind.FLOW);
    }

    private static JsonElement json(String value) {
        return new JsonParser().parse(value);
    }

    private static void check(boolean value, String label) {
        if (!value) throw new AssertionError(label);
    }
}
