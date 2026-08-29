package darkgrey.rpg.session.server;

import java.util.Arrays;
import java.util.Collections;
import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;

import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
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
import darkgrey.rpg.network.message.canonical.CanonicalSessionAction;
import darkgrey.rpg.network.message.canonical.CanonicalSessionFrame;
import darkgrey.rpg.project.ActorDefinition;
import darkgrey.rpg.project.ProjectDefinition;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.session.persistence.CanonicalSessionSavedData;
import io.netty.buffer.Unpooled;

/** Focused server-neutral orchestration, projection, and restart probe. */
public final class CanonicalSessionServerServiceProbe {

    private static final UUID PLAYER = UUID.fromString("00000000-0000-0000-0000-000000000001");

    public static void main(String[] args) {
        ProjectSnapshot project = project(true, true, "session_a");
        CanonicalSessionSavedData data = new CanonicalSessionSavedData();
        CanonicalSessionServerService service = new CanonicalSessionServerService(project, data);
        CanonicalSessionDispatch line = service.start(PLAYER, "story_a", "place_a");
        require(
            line.isFrame() && line.getFrame()
                .getKind() == CanonicalSessionFrame.Kind.LINE,
            "start line frame");
        require(
            "Actor A".equals(
                line.getFrame()
                    .getSpeaker()),
            "actor display name projection");
        CanonicalSessionDispatch choice = service.dispatch(
            PLAYER,
            new CanonicalSessionAction(
                line.getFrame()
                    .getTransportId(),
                "story_a",
                line.getFrame()
                    .getCurrentNodeId(),
                CanonicalSessionAction.Kind.CONTINUE,
                null));
        require(
            choice.getFrame()
                .getKind() == CanonicalSessionFrame.Kind.CHOICE,
            "choice frame");
        require(
            "Pick one".equals(
                choice.getFrame()
                    .getText()),
            "choice prompt projection");
        require(
            "option_b".equals(
                choice.getFrame()
                    .getChoices()
                    .get(1)
                    .getOptionId()),
            "stable option ID");
        CanonicalSessionDispatch close = service.dispatch(
            PLAYER,
            new CanonicalSessionAction(
                choice.getFrame()
                    .getTransportId(),
                "story_a",
                choice.getFrame()
                    .getCurrentNodeId(),
                CanonicalSessionAction.Kind.CHOICE,
                "option_b"));
        require(close.isCompleted() && close.getClose() != null, "completion close");
        require(
            "end_b".equals(
                close.getCompletionResult()
                    .getEndPortId()),
            "selected End port");
        require(
            Boolean.TRUE.equals(
                close.getCompletionResult()
                    .getPublicLogicOutputs()
                    .get("picked_b")),
            "public logic");
        require(data.getSnapshot(PLAYER, "story_a") != null, "completion remains persisted");
        CanonicalSessionFrame detachedFrame = line.getFrame();
        CanonicalSessionFrame replacementFrame = new CanonicalSessionFrame(
            detachedFrame.getTransportId(),
            detachedFrame.getStoryId(),
            detachedFrame.getSessionResourceId(),
            detachedFrame.getCurrentNodeId(),
            CanonicalSessionFrame.Kind.LINE,
            "Changed",
            "Changed",
            Collections.<darkgrey.rpg.network.message.canonical.CanonicalSessionChoiceOption>emptyList());
        io.netty.buffer.ByteBuf replacementBytes = Unpooled.buffer();
        replacementFrame.toBytes(replacementBytes);
        detachedFrame.fromBytes(replacementBytes);
        replacementBytes.release();
        require(
            "Hello".equals(
                line.getFrame()
                    .getText()),
            "dispatch frame is detached");
        reject(new Runnable() {

            @Override
            public void run() {
                close.getCompletionResult()
                    .getPublicLogicOutputs()
                    .put("x", Boolean.TRUE);
            }
        }, "immutable completion result");

        NBTTagCompound persisted = new NBTTagCompound();
        data.writeToNBT(persisted);
        CanonicalSessionSavedData restartedData = new CanonicalSessionSavedData();
        restartedData.readFromNBT(persisted);
        CanonicalSessionServerService restarted = new CanonicalSessionServerService(project, restartedData);
        require(
            restarted.resume(PLAYER, "story_a")
                .isCompleted(),
            "completed restart resume");

        CanonicalSessionSavedData activeData = new CanonicalSessionSavedData();
        CanonicalSessionServerService active = new CanonicalSessionServerService(project, activeData);
        CanonicalSessionDispatch activeLine = active.start(PLAYER, "story_a", "place_a");
        NBTTagCompound activeNbt = new NBTTagCompound();
        activeData.writeToNBT(activeNbt);
        CanonicalSessionSavedData activeRestartData = new CanonicalSessionSavedData();
        activeRestartData.readFromNBT(activeNbt);
        CanonicalSessionServerService activeRestart = new CanonicalSessionServerService(project, activeRestartData);
        require(
            activeRestart.resume(PLAYER, "story_a")
                .getFrame()
                .getKind() == CanonicalSessionFrame.Kind.LINE,
            "active restart resume");

        reject(new Runnable() {

            @Override
            public void run() {
                active.dispatch(
                    PLAYER,
                    new CanonicalSessionAction(
                        activeLine.getFrame()
                            .getTransportId() + 1L,
                        "story_a",
                        activeLine.getFrame()
                            .getCurrentNodeId(),
                        CanonicalSessionAction.Kind.CONTINUE,
                        null));
            }
        }, "stale transport");
        reject(new Runnable() {

            @Override
            public void run() {
                active.dispatch(
                    PLAYER,
                    new CanonicalSessionAction(
                        activeLine.getFrame()
                            .getTransportId(),
                        "wrong_story",
                        activeLine.getFrame()
                            .getCurrentNodeId(),
                        CanonicalSessionAction.Kind.CONTINUE,
                        null));
            }
        }, "wrong Story");
        reject(new Runnable() {

            @Override
            public void run() {
                active.dispatch(
                    PLAYER,
                    new CanonicalSessionAction(
                        activeLine.getFrame()
                            .getTransportId(),
                        "story_a",
                        "wrong_node",
                        CanonicalSessionAction.Kind.CONTINUE,
                        null));
            }
        }, "wrong current node");
        reject(new Runnable() {

            @Override
            public void run() {
                active.dispatch(
                    PLAYER,
                    new CanonicalSessionAction(
                        activeLine.getFrame()
                            .getTransportId(),
                        "story_a",
                        activeLine.getFrame()
                            .getCurrentNodeId(),
                        CanonicalSessionAction.Kind.CHOICE,
                        "option_a"));
            }
        }, "wrong action kind");

        CanonicalSessionDispatch activeChoice = active.dispatch(
            PLAYER,
            new CanonicalSessionAction(
                activeLine.getFrame()
                    .getTransportId(),
                "story_a",
                activeLine.getFrame()
                    .getCurrentNodeId(),
                CanonicalSessionAction.Kind.CONTINUE,
                null));
        reject(new Runnable() {

            @Override
            public void run() {
                active.dispatch(
                    PLAYER,
                    new CanonicalSessionAction(
                        activeChoice.getFrame()
                            .getTransportId(),
                        "story_a",
                        activeChoice.getFrame()
                            .getCurrentNodeId(),
                        CanonicalSessionAction.Kind.CHOICE,
                        "missing"));
            }
        }, "invalid option ID");
        reject(new Runnable() {

            @Override
            public void run() {
                service.dispatch(
                    PLAYER,
                    new CanonicalSessionAction(1L, "story_a", "end_b", CanonicalSessionAction.Kind.CONTINUE, null));
            }
        }, "completed action rejected");
        require(data.getSnapshot(PLAYER, "story_a") != null, "rejected action preserves completion");

        rejectStart(project(false, true, "session_a"), "missing actor");
        rejectStart(project(true, true, "session_a", "session_a", "actor_b"), "non-member actor");
        rejectStart(project(true, false, "session_a"), "missing Session membership");
        rejectStart(project(true, true, "session_a", "missing_session"), "wrong resource binding");
        rejectStart(project(true, true, "session_a", "session_a", "actor_a", "end"), "non-Session aggregate");
        CanonicalSessionSavedData failedData = new CanonicalSessionSavedData();
        CanonicalSessionServerService failedService = new CanonicalSessionServerService(cycleProject(), failedData);
        CanonicalSessionDispatch failedLine = failedService.start(PLAYER, "story_a", "place_a");
        reject(new Runnable() {

            @Override
            public void run() {
                failedService.continueLine(
                    PLAYER,
                    "story_a",
                    failedLine.getFrame()
                        .getTransportId(),
                    failedLine.getFrame()
                        .getCurrentNodeId());
            }
        }, "automatic Flow cycle failure");
        require(
            failedData.getSnapshot(PLAYER, "story_a")
                .getRuntimeSnapshot()
                .getStatus()
                .name()
                .equals("FAILED"),
            "FAILED state retained after cycle");
        System.out.println("CANONICAL_SESSION_SERVER_SERVICE_PROBE=PASS");
    }

    private static void rejectStart(ProjectSnapshot project, String label) {
        reject(new Runnable() {

            @Override
            public void run() {
                new CanonicalSessionServerService(project, new CanonicalSessionSavedData())
                    .start(PLAYER, "story_a", "place_a");
            }
        }, label);
    }

    private static ProjectSnapshot project(boolean actor, boolean member, String resourceId) {
        return project(actor, member, resourceId, resourceId, "actor_a");
    }

    private static ProjectSnapshot project(boolean actor, boolean member, String resourceId, String storyResourceId) {
        return project(actor, member, resourceId, storyResourceId, "actor_a");
    }

    private static ProjectSnapshot project(boolean actor, boolean member, String resourceId, String storyResourceId,
        String lineActorId) {
        return project(actor, member, resourceId, storyResourceId, lineActorId, "session");
    }

    private static ProjectSnapshot project(boolean actor, boolean member, String resourceId, String storyResourceId,
        String lineActorId, String placementType) {
        Map<String, ActorDefinition> actors = new LinkedHashMap<String, ActorDefinition>();
        if (actor) {
            actors
                .put("actor_a", new ActorDefinition(1, "actor_a", "Actor A", "", Collections.<String>emptyList(), ""));
            actors
                .put("actor_b", new ActorDefinition(1, "actor_b", "Actor B", "", Collections.<String>emptyList(), ""));
        }
        CanonicalGraphResource session = session(resourceId, lineActorId);
        Map<String, CanonicalGraphResource> sessions = new LinkedHashMap<String, CanonicalGraphResource>();
        sessions.put(resourceId, session);
        Map<String, CanonicalGraphResource> stories = new LinkedHashMap<String, CanonicalGraphResource>();
        stories.put("story_a", story(storyResourceId, placementType));
        List<String> members = member ? Arrays.asList(resourceId) : Collections.<String>emptyList();
        List<String> actorMembers = member && actor ? Arrays.asList("actor_a") : Collections.<String>emptyList();
        Map<String, CanonicalStoryMembership> memberships = new LinkedHashMap<String, CanonicalStoryMembership>();
        memberships.put(
            "story_a",
            new CanonicalStoryMembership(
                "story_a",
                new CanonicalStoryMembershipSet(actorMembers, members, Collections.<String>emptyList())));
        CanonicalProjectContent content = new CanonicalProjectContent(
            stories,
            sessions,
            Collections.<String, CanonicalGraphResource>emptyMap(),
            memberships);
        return new ProjectSnapshot(
            new ProjectDefinition(1, "probe", "Probe"),
            actors,
            Collections.<String, darkgrey.rpg.dialogue.DialogueDefinition>emptyMap(),
            Collections.<String, darkgrey.rpg.quest.QuestDefinition>emptyMap(),
            Collections.<String, darkgrey.rpg.story.StoryDefinition>emptyMap(),
            content);
    }

    private static ProjectSnapshot cycleProject() {
        Map<String, ActorDefinition> actors = new LinkedHashMap<String, ActorDefinition>();
        actors.put("actor_a", new ActorDefinition(1, "actor_a", "Actor A", "", Collections.<String>emptyList(), ""));
        Map<String, CanonicalGraphResource> sessions = new LinkedHashMap<String, CanonicalGraphResource>();
        sessions.put("cycle_session", cycleSession());
        Map<String, CanonicalGraphResource> stories = new LinkedHashMap<String, CanonicalGraphResource>();
        stories.put("story_a", story("cycle_session"));
        Map<String, CanonicalStoryMembership> memberships = new LinkedHashMap<String, CanonicalStoryMembership>();
        memberships.put(
            "story_a",
            new CanonicalStoryMembership(
                "story_a",
                new CanonicalStoryMembershipSet(
                    Arrays.asList("actor_a"),
                    Arrays.asList("cycle_session"),
                    Collections.<String>emptyList())));
        return new ProjectSnapshot(
            new ProjectDefinition(1, "probe", "Probe"),
            actors,
            Collections.<String, darkgrey.rpg.dialogue.DialogueDefinition>emptyMap(),
            Collections.<String, darkgrey.rpg.quest.QuestDefinition>emptyMap(),
            Collections.<String, darkgrey.rpg.story.StoryDefinition>emptyMap(),
            new CanonicalProjectContent(
                stories,
                sessions,
                Collections.<String, CanonicalGraphResource>emptyMap(),
                memberships));
    }

    private static CanonicalGraphResource story(String sessionId) {
        return story(sessionId, "session");
    }

    private static CanonicalGraphResource story(String sessionId, String placementType) {
        Map<String, JsonElement> props = new HashMap<String, JsonElement>();
        props.put("resource_id", json(sessionId));
        CanonicalGraphNode node = node(
            "place_a",
            placementType,
            ports(in("flow_in", false), out("flow_out", false)),
            props);
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.STORY,
            "story_a",
            "Story A",
            new CanonicalGraph(Arrays.asList(node), Collections.<CanonicalGraphConnection>emptyList()));
    }

    private static CanonicalGraphResource session(String id) {
        return session(id, "actor_a");
    }

    private static CanonicalGraphResource session(String id, String actorId) {
        CanonicalGraphNode start = node(
            "start",
            "start",
            ports(out("flow_out", false), out("logic_out", true)),
            props());
        CanonicalGraphNode line = node(
            "line",
            "line",
            ports(in("flow_in", false), out("flow_out", false)),
            props("speaker_actor_id", actorId, "text", "Hello"));
        Map<String, JsonElement> choiceProps = props("prompt", "Pick one");
        JsonArray options = new JsonArray();
        options.add(option("option_a", "A", "flow_a"));
        options.add(option("option_b", "B", "flow_b"));
        choiceProps.put("options", options);
        CanonicalGraphNode choice = node(
            "choice",
            "choice",
            ports(
                in("flow_in", false),
                out("flow_a", false),
                out("option_a", true),
                out("flow_b", false),
                out("option_b", true)),
            choiceProps);
        CanonicalGraphNode endA = node(
            "end_a",
            "end",
            ports(in("flow_in", false)),
            props("port_id", "end_a", "display_name", "A"));
        CanonicalGraphNode endB = node(
            "end_b",
            "end",
            ports(in("flow_in", false)),
            props("port_id", "end_b", "display_name", "B"));
        CanonicalGraphNode output = node(
            "output",
            "logic_output",
            ports(in("logic_in", true)),
            props("port_id", "picked_b", "display_name", "Picked B"));
        List<CanonicalGraphConnection> edges = Arrays.asList(
            edge("start", "flow_out", "line", "flow_in", false),
            edge("line", "flow_out", "choice", "flow_in", false),
            edge("choice", "flow_a", "end_a", "flow_in", false),
            edge("choice", "flow_b", "end_b", "flow_in", false),
            edge("choice", "option_b", "output", "logic_in", true));
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.SESSION,
            id,
            id,
            new CanonicalGraph(Arrays.asList(start, line, choice, endA, endB, output), edges));
    }

    private static CanonicalGraphResource cycleSession() {
        CanonicalGraphNode start = node(
            "start",
            "start",
            ports(out("flow_out", false), out("logic_out", true)),
            props());
        CanonicalGraphNode line = node(
            "line",
            "line",
            ports(in("flow_in", false), out("flow_out", false)),
            props("speaker_actor_id", "actor_a", "text", "Cycle"));
        CanonicalGraphNode jump = node(
            "jump",
            "legacy_jump",
            ports(in("flow_in", false), out("flow_out", false)),
            props());
        List<CanonicalGraphConnection> edges = Arrays.asList(
            edge("start", "flow_out", "line", "flow_in", false),
            edge("line", "flow_out", "jump", "flow_in", false),
            edge("jump", "flow_out", "jump", "flow_in", false));
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.SESSION,
            "cycle_session",
            "cycle_session",
            new CanonicalGraph(Arrays.asList(start, line, jump), edges));
    }

    private static JsonObject option(String id, String text, String flow) {
        JsonObject object = new JsonObject();
        object.addProperty("option_id", id);
        object.addProperty("display_text", text);
        object.addProperty("flow_port_id", flow);
        return object;
    }

    private static Map<String, JsonElement> props(String... values) {
        Map<String, JsonElement> result = new HashMap<String, JsonElement>();
        for (int i = 0; i < values.length; i += 2) result.put(values[i], json(values[i + 1]));
        return result;
    }

    private static Map<String, JsonElement> props() {
        return new HashMap<String, JsonElement>();
    }

    private static JsonElement json(String value) {
        return new JsonParser().parse("\"" + value + "\"");
    }

    private static CanonicalGraphNode node(String id, String type, List<CanonicalGraphPort> ports,
        Map<String, JsonElement> props) {
        return new CanonicalGraphNode(id, type, id, ports, props);
    }

    private static CanonicalGraphPort in(String id, boolean logic) {
        return new CanonicalGraphPort(
            id,
            id,
            CanonicalGraphPortDirection.INPUT,
            logic ? CanonicalGraphInterfaceKind.LOGIC : CanonicalGraphInterfaceKind.FLOW,
            0);
    }

    private static CanonicalGraphPort out(String id, boolean logic) {
        return new CanonicalGraphPort(
            id,
            id,
            CanonicalGraphPortDirection.OUTPUT,
            logic ? CanonicalGraphInterfaceKind.LOGIC : CanonicalGraphInterfaceKind.FLOW,
            0);
    }

    private static List<CanonicalGraphPort> ports(CanonicalGraphPort... values) {
        return Arrays.asList(values);
    }

    private static CanonicalGraphConnection edge(String fromNode, String fromPort, String toNode, String toPort,
        boolean logic) {
        return new CanonicalGraphConnection(
            fromNode,
            fromPort,
            toNode,
            toPort,
            logic ? CanonicalGraphInterfaceKind.LOGIC : CanonicalGraphInterfaceKind.FLOW);
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
