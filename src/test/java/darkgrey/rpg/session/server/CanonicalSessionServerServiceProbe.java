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
        dynamicPresentationSurvivesRestart();
        choiceContextSurvivesRestart();
        multiPageActions();
        ProjectSnapshot project = project(true, true, "ST-2345-6789-ABCD-EFGH~session~session_a");
        CanonicalSessionSavedData data = new CanonicalSessionSavedData();
        CanonicalSessionServerService service = new CanonicalSessionServerService(project, data);
        CanonicalSessionDispatch line = service.start(PLAYER, "ST-2345-6789-ABCD-EFGH", "place_a", true);
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
                "ST-2345-6789-ABCD-EFGH",
                line.getFrame()
                    .getCurrentNodeId(),
                CanonicalSessionAction.Kind.CONTINUE,
                null));
        require(
            choice.getFrame()
                .getKind() == CanonicalSessionFrame.Kind.CHOICE,
            "choice frame");
        require(
            "".equals(
                choice.getFrame()
                    .getText()),
            "retired choice prompt is not projected");
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
                "ST-2345-6789-ABCD-EFGH",
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
                    .get("activation_gate")),
            "public logic");
        require(data.getSnapshot(PLAYER, "ST-2345-6789-ABCD-EFGH") != null, "completion remains persisted");
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
            restarted.resume(PLAYER, "ST-2345-6789-ABCD-EFGH")
                .isCompleted(),
            "completed restart resume");

        CanonicalSessionSavedData activeData = new CanonicalSessionSavedData();
        CanonicalSessionServerService active = new CanonicalSessionServerService(project, activeData);
        CanonicalSessionDispatch activeLine = active.start(PLAYER, "ST-2345-6789-ABCD-EFGH", "place_a");
        NBTTagCompound activeNbt = new NBTTagCompound();
        activeData.writeToNBT(activeNbt);
        CanonicalSessionSavedData activeRestartData = new CanonicalSessionSavedData();
        activeRestartData.readFromNBT(activeNbt);
        CanonicalSessionServerService activeRestart = new CanonicalSessionServerService(project, activeRestartData);
        require(
            activeRestart.resume(PLAYER, "ST-2345-6789-ABCD-EFGH")
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
                        "ST-2345-6789-ABCD-EFGH",
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
                        "ST-JKLM-NPQR-STUV-WXYZ",
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
                        "ST-2345-6789-ABCD-EFGH",
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
                        "ST-2345-6789-ABCD-EFGH",
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
                "ST-2345-6789-ABCD-EFGH",
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
                        "ST-2345-6789-ABCD-EFGH",
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
                    new CanonicalSessionAction(
                        1L,
                        "ST-2345-6789-ABCD-EFGH",
                        "end_b",
                        CanonicalSessionAction.Kind.CONTINUE,
                        null));
            }
        }, "completed action rejected");
        require(data.getSnapshot(PLAYER, "ST-2345-6789-ABCD-EFGH") != null, "rejected action preserves completion");

        rejectStart(project(false, true, "ST-2345-6789-ABCD-EFGH~session~session_a"), "missing actor");
        rejectStart(
            project(
                true,
                true,
                "ST-2345-6789-ABCD-EFGH~session~session_a",
                "ST-2345-6789-ABCD-EFGH~session~session_a",
                "ST-2345-6789-ABCD-EFGH~actor~actor_b"),
            "non-member actor");
        rejectStart(project(true, false, "ST-2345-6789-ABCD-EFGH~session~session_a"), "missing Session membership");
        rejectStart(
            project(
                true,
                true,
                "ST-2345-6789-ABCD-EFGH~session~session_a",
                "ST-2345-6789-ABCD-EFGH~session~missing_session"),
            "wrong resource binding");
        rejectStart(
            project(
                true,
                true,
                "ST-2345-6789-ABCD-EFGH~session~session_a",
                "ST-2345-6789-ABCD-EFGH~session~session_a",
                "ST-2345-6789-ABCD-EFGH~actor~actor_a",
                "end"),
            "non-Session aggregate");
        CanonicalSessionSavedData failedData = new CanonicalSessionSavedData();
        CanonicalSessionServerService failedService = new CanonicalSessionServerService(cycleProject(), failedData);
        CanonicalSessionDispatch failedLine = failedService.start(PLAYER, "ST-2345-6789-ABCD-EFGH", "place_a");
        reject(new Runnable() {

            @Override
            public void run() {
                failedService.continueLine(
                    PLAYER,
                    "ST-2345-6789-ABCD-EFGH",
                    failedLine.getFrame()
                        .getTransportId(),
                    failedLine.getFrame()
                        .getCurrentNodeId());
            }
        }, "automatic Flow cycle failure");
        require(
            failedData.getSnapshot(PLAYER, "ST-2345-6789-ABCD-EFGH")
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
                    .start(PLAYER, "ST-2345-6789-ABCD-EFGH", "place_a");
            }
        }, label);
    }

    private static ProjectSnapshot project(boolean actor, boolean member, String resourceId) {
        return project(actor, member, resourceId, resourceId, "ST-2345-6789-ABCD-EFGH~actor~actor_a");
    }

    private static void dynamicPresentationSurvivesRestart() {
        ProjectSnapshot project = project(true, true, "ST-2345-6789-ABCD-EFGH~session~dynamic_session");
        CanonicalSessionSavedData data = new CanonicalSessionSavedData();
        final int[] value = { 12 };
        final int[] calls = { 0 };
        CanonicalSessionServerService.TextResolver resolver = new CanonicalSessionServerService.TextResolver() {

            public String resolve(UUID player, String template) {
                calls[0]++;
                return player.equals(PLAYER) ? Integer.toString(value[0]) : "99";
            }
        };
        CanonicalSessionServerService service = new CanonicalSessionServerService(project, data, resolver);
        require(
            "12".equals(
                service.start(PLAYER, "ST-2345-6789-ABCD-EFGH", "place_a")
                    .getFrame()
                    .getText()),
            "initial dynamic value");
        value[0] = 45;
        require(
            "12".equals(
                service.resume(PLAYER, "ST-2345-6789-ABCD-EFGH")
                    .getFrame()
                    .getText())
                && calls[0] == 1,
            "resend keeps author-page snapshot");
        UUID other = UUID.fromString("00000000-0000-0000-0000-000000000002");
        require(
            "99".equals(
                service.start(other, "ST-2345-6789-ABCD-EFGH", "place_a")
                    .getFrame()
                    .getText()),
            "players have separate snapshots");
        NBTTagCompound stored = new NBTTagCompound();
        data.writeToNBT(stored);
        CanonicalSessionSavedData restored = new CanonicalSessionSavedData();
        restored.readFromNBT(stored);
        CanonicalSessionServerService restarted = new CanonicalSessionServerService(project, restored, resolver);
        require(
            "12".equals(
                restarted.resume(PLAYER, "ST-2345-6789-ABCD-EFGH")
                    .getFrame()
                    .getText())
                && calls[0] == 2,
            "restart keeps resolved presentation");
        System.out.println("DYNAMIC_AUTHOR_PAGE_RESEND_RESTART_PLAYER_ISOLATION=PASS");
    }

    private static void multiPageActions() {
        CanonicalSessionSavedData data = new CanonicalSessionSavedData();
        final CanonicalSessionServerService service = new CanonicalSessionServerService(
            project(true, true, "ST-2345-6789-ABCD-EFGH~session~pages_session"),
            data);
        CanonicalSessionFrame first = service.start(PLAYER, "ST-2345-6789-ABCD-EFGH", "place_a")
            .getFrame();
        final CanonicalSessionAction legacy = new CanonicalSessionAction(
            first.getTransportId(),
            "ST-2345-6789-ABCD-EFGH",
            first.getCurrentNodeId(),
            CanonicalSessionAction.Kind.CONTINUE,
            null);
        reject(new Runnable() {

            public void run() {
                service.dispatch(PLAYER, legacy);
            }
        }, "unversioned page action");
        final CanonicalSessionAction advance = new CanonicalSessionAction(
            first.getTransportId(),
            "ST-2345-6789-ABCD-EFGH",
            first.getCurrentNodeId(),
            CanonicalSessionAction.Kind.CONTINUE,
            null,
            first.getLineEpoch());
        CanonicalSessionFrame second = service.dispatch(PLAYER, advance)
            .getFrame();
        require(
            "Second".equals(second.getText()) && second.getCurrentNodeId()
                .equals(first.getCurrentNodeId()),
            "second page remains on same node");
        require(second.getLineEpoch() == first.getLineEpoch() + 1L, "page playback epoch advances");
        reject(new Runnable() {

            public void run() {
                service.dispatch(PLAYER, advance);
            }
        }, "duplicate page action");
        require(
            "Second".equals(
                service.resume(PLAYER, "ST-2345-6789-ABCD-EFGH")
                    .getFrame()
                    .getText()),
            "rejected duplicate does not skip page");
        CanonicalSessionFrame choice = service
            .dispatch(
                PLAYER,
                new CanonicalSessionAction(
                    second.getTransportId(),
                    "ST-2345-6789-ABCD-EFGH",
                    second.getCurrentNodeId(),
                    CanonicalSessionAction.Kind.CONTINUE,
                    null,
                    second.getLineEpoch()))
            .getFrame();
        require(choice.getKind() == CanonicalSessionFrame.Kind.CHOICE, "last page follows flow output");
        CanonicalSessionDispatch resumed = service.resume(PLAYER, "ST-2345-6789-ABCD-EFGH");
        require(
            "Second".equals(
                resumed.getLineContext()
                    .getText()),
            "Choice retains last author-page context");
    }

    private static void choiceContextSurvivesRestart() {
        ProjectSnapshot project = project(true, true, "ST-2345-6789-ABCD-EFGH~session~dynamic_session");
        CanonicalSessionSavedData data = new CanonicalSessionSavedData();
        final int[] calls = { 0 };
        CanonicalSessionServerService.TextResolver resolver = new CanonicalSessionServerService.TextResolver() {

            public String resolve(UUID player, String template) {
                return "已显示的值 " + ++calls[0];
            }
        };
        CanonicalSessionServerService service = new CanonicalSessionServerService(project, data, resolver);
        CanonicalSessionFrame line = service.start(PLAYER, "ST-2345-6789-ABCD-EFGH", "place_a")
            .getFrame();
        CanonicalSessionFrame choice = service
            .continueLine(PLAYER, line.getStoryId(), line.getTransportId(), line.getCurrentNodeId())
            .getFrame();
        NBTTagCompound nbt = new NBTTagCompound();
        data.writeToNBT(nbt);
        CanonicalSessionSavedData loaded = new CanonicalSessionSavedData();
        loaded.readFromNBT(nbt);
        CanonicalSessionDispatch resumed = new CanonicalSessionServerService(project, loaded, resolver)
            .resume(PLAYER, line.getStoryId());
        CanonicalSessionFrame context = resumed.getLineContext();
        require(
            context != null && context.getText()
                .equals(line.getText())
                && context.getSpeaker()
                    .equals(line.getSpeaker())
                && java.util.Objects.equals(context.getPortraitRef(), line.getPortraitRef())
                && calls[0] == 1,
            "resolved Choice context survives restart without resolving again");
        require(
            !context.shouldPlayVoice() && !context.shouldPlayScreen()
                && context.getLineEpoch() == line.getLineEpoch()
                && resumed.getFrame()
                    .getText()
                    .isEmpty(),
            "restoration is silent and preserves line history identity and empty Choice text");
        darkgrey.rpg.client.session.CanonicalSessionClientModel client = new darkgrey.rpg.client.session.CanonicalSessionClientModel();
        require(
            client.acceptFrame(context) && client.acceptFrame(resumed.getFrame())
                && client.getVisibleText()
                    .equals(line.getText())
                && client.getVisibleSpeaker()
                    .equals(line.getSpeaker())
                && client.choiceAction(
                    choice.getChoices()
                        .get(0)
                        .getOptionId())
                    .getCurrentNodeId()
                    .equals(choice.getCurrentNodeId()),
            "fresh client receives context and retains authoritative Choice cursor");
        NBTTagCompound malformed = (NBTTagCompound) nbt.copy();
        malformed.getCompoundTag("line_contexts")
            .setByteArray(Long.toString(line.getTransportId()), new byte[] { 1 });
        reject(new Runnable() {

            public void run() {
                loaded.readFromNBT(malformed);
            }
        }, "truncated Line context");
        require(
            loaded.lineContext(loaded.getSnapshot(PLAYER, line.getStoryId()))
                .getText()
                .equals(line.getText()),
            "malformed context cannot replace live saved state");
        System.out.println("CHOICE_CONTEXT_SAVE_RECONNECT_SILENT_RESOLVED_TEXT=PASS");
    }

    private static ProjectSnapshot project(boolean actor, boolean member, String resourceId, String storyResourceId) {
        return project(actor, member, resourceId, storyResourceId, "ST-2345-6789-ABCD-EFGH~actor~actor_a");
    }

    private static ProjectSnapshot project(boolean actor, boolean member, String resourceId, String storyResourceId,
        String lineActorId) {
        return project(actor, member, resourceId, storyResourceId, lineActorId, "session");
    }

    private static ProjectSnapshot project(boolean actor, boolean member, String resourceId, String storyResourceId,
        String lineActorId, String placementType) {
        Map<String, ActorDefinition> actors = new LinkedHashMap<String, ActorDefinition>();
        if (actor) {
            actors.put(
                "ST-2345-6789-ABCD-EFGH~actor~actor_a",
                new ActorDefinition(
                    5,
                    ActorDefinition.TYPE_INDIVIDUAL,
                    "ST-2345-6789-ABCD-EFGH~actor~actor_a",
                    "Actor A",
                    "",
                    Collections.<String>emptyList(),
                    "ST-2345-6789-ABCD-EFGH"));
            actors.put(
                "ST-2345-6789-ABCD-EFGH~actor~actor_b",
                new ActorDefinition(
                    5,
                    ActorDefinition.TYPE_INDIVIDUAL,
                    "ST-2345-6789-ABCD-EFGH~actor~actor_b",
                    "Actor B",
                    "",
                    Collections.<String>emptyList(),
                    "ST-2345-6789-ABCD-EFGH"));
        }
        CanonicalGraphResource session = session(resourceId, lineActorId);
        Map<String, CanonicalGraphResource> sessions = new LinkedHashMap<String, CanonicalGraphResource>();
        sessions.put(resourceId, session);
        Map<String, CanonicalGraphResource> stories = new LinkedHashMap<String, CanonicalGraphResource>();
        stories.put("ST-2345-6789-ABCD-EFGH", story(storyResourceId, placementType));
        List<String> members = member ? Arrays.asList(resourceId) : Collections.<String>emptyList();
        List<String> actorMembers = member && actor ? Arrays.asList("ST-2345-6789-ABCD-EFGH~actor~actor_a")
            : Collections.<String>emptyList();
        Map<String, CanonicalStoryMembership> memberships = new LinkedHashMap<String, CanonicalStoryMembership>();
        memberships.put(
            "ST-2345-6789-ABCD-EFGH",
            new CanonicalStoryMembership(
                "ST-2345-6789-ABCD-EFGH",
                new CanonicalStoryMembershipSet(actorMembers, members, Collections.<String>emptyList())));
        CanonicalProjectContent content = new CanonicalProjectContent(
            stories,
            sessions,
            Collections.<String, CanonicalGraphResource>emptyMap(),
            memberships);
        return new ProjectSnapshot(
            new ProjectDefinition(1, "probe", "Probe"),
            actors,
            Collections.emptyMap(),
            Collections.emptyMap(),
            content);
    }

    private static ProjectSnapshot cycleProject() {
        Map<String, ActorDefinition> actors = new LinkedHashMap<String, ActorDefinition>();
        actors.put(
            "ST-2345-6789-ABCD-EFGH~actor~actor_a",
            new ActorDefinition(
                5,
                ActorDefinition.TYPE_INDIVIDUAL,
                "ST-2345-6789-ABCD-EFGH~actor~actor_a",
                "Actor A",
                "",
                Collections.<String>emptyList(),
                "ST-2345-6789-ABCD-EFGH"));
        Map<String, CanonicalGraphResource> sessions = new LinkedHashMap<String, CanonicalGraphResource>();
        sessions.put("ST-2345-6789-ABCD-EFGH~session~cycle_session", cycleSession());
        Map<String, CanonicalGraphResource> stories = new LinkedHashMap<String, CanonicalGraphResource>();
        stories.put("ST-2345-6789-ABCD-EFGH", story("ST-2345-6789-ABCD-EFGH~session~cycle_session"));
        Map<String, CanonicalStoryMembership> memberships = new LinkedHashMap<String, CanonicalStoryMembership>();
        memberships.put(
            "ST-2345-6789-ABCD-EFGH",
            new CanonicalStoryMembership(
                "ST-2345-6789-ABCD-EFGH",
                new CanonicalStoryMembershipSet(
                    Arrays.asList("ST-2345-6789-ABCD-EFGH~actor~actor_a"),
                    Arrays.asList("ST-2345-6789-ABCD-EFGH~session~cycle_session"),
                    Collections.<String>emptyList())));
        return new ProjectSnapshot(
            new ProjectDefinition(1, "probe", "Probe"),
            actors,
            Collections.emptyMap(),
            Collections.emptyMap(),
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
            3,
            CanonicalGraphResourceKind.STORY,
            "ST-2345-6789-ABCD-EFGH",
            "Story A",
            new CanonicalGraph(Arrays.asList(node), Collections.<CanonicalGraphConnection>emptyList()));
    }

    private static CanonicalGraphResource session(String id) {
        return session(id, "ST-2345-6789-ABCD-EFGH~actor~actor_a");
    }

    private static CanonicalGraphResource session(String id, String actorId) {
        CanonicalGraphNode start = node(
            "start",
            "start",
            ports(out("flow_out", false), out("logic_out", true)),
            props());
        Map<String, JsonElement> lineProps = props("speaker_actor_id", actorId, "text", "Hello");
        if ("ST-2345-6789-ABCD-EFGH~session~dynamic_session".equals(id)) lineProps.put(
            "text",
            new com.google.gson.JsonPrimitive(
                darkgrey.rpg.session.runtime.DynamicContentText.PREFIX + "[{\"type\":\"player_level\"}]"));
        if ("ST-2345-6789-ABCD-EFGH~session~pages_session".equals(id)) {
            lineProps.remove("text");
            lineProps.put(
                "pages",
                new JsonParser().parse(
                    "[{\"page_id\":\"first\",\"text\":\"First\"},{\"page_id\":\"second\",\"text\":\"Second\"}]"));
        }
        CanonicalGraphNode line = node("line", "line", ports(in("flow_in", false), out("flow_out", false)), lineProps);
        Map<String, JsonElement> choiceProps = props("prompt", "Pick one");
        JsonArray options = new JsonArray();
        options.add(option("option_a", "A", "flow_a"));
        options.add(option("option_b", "B", "flow_b"));
        choiceProps.put("options", options);
        CanonicalGraphNode choice = node(
            "choice",
            "choice",
            ports(in("flow_in", false), out("flow_a", false), out("flow_b", false)),
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
            props("port_id", "activation_gate", "display_name", "Activation"));
        List<CanonicalGraphConnection> edges = Arrays.asList(
            edge("start", "flow_out", "line", "flow_in", false),
            edge("line", "flow_out", "choice", "flow_in", false),
            edge("choice", "flow_a", "end_a", "flow_in", false),
            edge("choice", "flow_b", "end_b", "flow_in", false),
            edge("start", "logic_out", "output", "logic_in", true));
        return new CanonicalGraphResource(
            3,
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
            props("speaker_actor_id", "ST-2345-6789-ABCD-EFGH~actor~actor_a", "text", "Cycle"));
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
            3,
            CanonicalGraphResourceKind.SESSION,
            "ST-2345-6789-ABCD-EFGH~session~cycle_session",
            "ST-2345-6789-ABCD-EFGH~session~cycle_session",
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
