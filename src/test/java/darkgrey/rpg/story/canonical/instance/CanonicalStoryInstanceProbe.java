package darkgrey.rpg.story.canonical.instance;

import java.util.Arrays;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;

import com.google.gson.JsonElement;
import com.google.gson.JsonParser;

import darkgrey.rpg.graph.canonical.CanonicalGraph;
import darkgrey.rpg.graph.canonical.CanonicalGraphConnection;
import darkgrey.rpg.graph.canonical.CanonicalGraphInterfaceKind;
import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphPort;
import darkgrey.rpg.graph.canonical.CanonicalGraphPortDirection;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceException;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceKind;
import darkgrey.rpg.session.instance.CanonicalSessionResourceResolver;
import darkgrey.rpg.session.persistence.CanonicalSessionSavedData;
import darkgrey.rpg.session.persistence.CanonicalSessionWorldStateNbtCodec;
import darkgrey.rpg.story.canonical.CanonicalStoryPendingContinuation;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryRepeatPolicy;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryStatus;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryWaitKind;

/** Executable Stage 5 probe for player plus Story identity, repeat policy, and restart persistence. */
public final class CanonicalStoryInstanceProbe {

    private static final UUID PLAYER = UUID.fromString("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");

    private CanonicalStoryInstanceProbe() {}

    public static void main(String[] args) {
        oneInstanceAndRepeatPolicy();
        packageGenerationRetirement();
        flowJudgmentRestartRoundTrip();
        restartRoundTripAndAtomicFailure();
        atomicSessionHandoffCheckpoint();
        strictNbtBoundary();
        System.out.println("CANONICAL_STORY_INSTANCE_IDENTITY=PASS");
        System.out.println("CANONICAL_STORY_INSTANCE_REPEAT_POLICY=PASS");
        System.out.println("CANONICAL_STORY_INSTANCE_NBT_RESTORE=PASS");
        System.out.println("CANONICAL_STORY_FLOW_JUDGMENT_NBT=PASS");
        System.out.println("CANONICAL_STORY_SESSION_CHECKPOINT=PASS");
        System.out.println("CANONICAL_STORY_GENERATION_RETIREMENT=PASS");
    }

    private static void flowJudgmentRestartRoundTrip() {
        final CanonicalGraphResource story = flowJudgmentStory();
        CanonicalStoryInstanceStore source = new CanonicalStoryInstanceStore();
        CanonicalStoryInstance active = source.start(PLAYER, story, "trigger", CanonicalStoryRepeatPolicy.ONCE, 1000L);
        check(
            active.getRuntime()
                .getExecutedFlowJudgmentNodeIds()
                .isEmpty(),
            "Flow Judgment was initially executed");
        NBTTagCompound saved = source.writeToNbt();

        CanonicalStoryInstanceStore restored = new CanonicalStoryInstanceStore();
        restored.readFromNbt(saved, new CanonicalStoryResourceResolver() {

            @Override
            public CanonicalGraphResource resolve(String storyId) {
                return story.getId()
                    .equals(storyId) ? story : null;
            }
        });
        CanonicalStoryInstance value = restored.get(PLAYER, story.getId());
        check(value != null, "Flow Judgment Story instance was not restored");
        value.completeAction("gate", 1100L);
        check(value.getStatus() == CanonicalStoryStatus.TERMINATED, "Flow Judgment did not continue after restart");
        check(
            value.getRuntime()
                .getExecutedFlowJudgmentNodeIds()
                .equals(Collections.singletonList("judgment")),
            "Flow Judgment execution was not retained in the instance");
        check(
            CanonicalStoryInstanceNbtCodec.decode(restored.writeToNbt())
                .get(0)
                .getRuntimeSnapshot()
                .getExecutedFlowJudgmentNodeIds()
                .equals(Collections.singletonList("judgment")),
            "Flow Judgment execution did not survive Story NBT round trip");
    }

    private static void oneInstanceAndRepeatPolicy() {
        CanonicalGraphResource terminal = terminalStory("terminal_story");
        CanonicalStoryInstanceStore onceStore = new CanonicalStoryInstanceStore();
        CanonicalStoryInstance once = onceStore
            .start(PLAYER, terminal, "trigger", CanonicalStoryRepeatPolicy.ONCE, 100L);
        check(once.getStatus() == CanonicalStoryStatus.TERMINATED, "Immediate Story did not terminate");
        CanonicalStoryInstance onceAgain = onceStore
            .start(PLAYER, terminal, "trigger", CanonicalStoryRepeatPolicy.REPEATABLE, 200L);
        check(once == onceAgain, "Completed once Story was restarted");
        check(onceAgain.getActivationTime() == 100L, "Completed once Story activation time changed");
        check(onceStore.size() == 1, "Once Story created a duplicate instance");

        CanonicalStoryInstanceStore repeatStore = new CanonicalStoryInstanceStore();
        CanonicalStoryInstance first = repeatStore
            .start(PLAYER, terminal, "trigger", CanonicalStoryRepeatPolicy.REPEATABLE, 300L);
        CanonicalStoryInstance second = repeatStore
            .start(PLAYER, terminal, "trigger", CanonicalStoryRepeatPolicy.ONCE, 400L);
        check(first != second, "Repeatable terminal Story was not replaced");
        check(second.getActivationTime() == 400L, "Repeatable Story retained the old activation time");
        check(repeatStore.size() == 1, "Repeatable Story created a duplicate identity");

        CanonicalGraphResource waiting = waitingStory("waiting_story");
        CanonicalStoryInstanceStore activeStore = new CanonicalStoryInstanceStore();
        CanonicalStoryInstance active = activeStore
            .start(PLAYER, waiting, "trigger", CanonicalStoryRepeatPolicy.REPEATABLE, 500L);
        CanonicalStoryInstance activeAgain = activeStore
            .start(PLAYER, waiting, "trigger", CanonicalStoryRepeatPolicy.REPEATABLE, 600L);
        check(active == activeAgain, "Active Story re-entry created a second cursor");
        check(
            activeAgain.getRuntime()
                .getWaitKind() == CanonicalStoryWaitKind.SESSION,
            "Active cursor moved on re-entry");
        check(activeStore.activeCount(PLAYER) == 1, "Player has more than one active instance for the Story");
    }

    private static void packageGenerationRetirement() {
        CanonicalGraphResource retired = terminalStory("retired_story");
        CanonicalGraphResource retained = terminalStory("retained_story");
        CanonicalStoryInstanceStore store = new CanonicalStoryInstanceStore();
        store.start(PLAYER, retired, "trigger", CanonicalStoryRepeatPolicy.ONCE, 1L);
        store.start(PLAYER, retained, "trigger", CanonicalStoryRepeatPolicy.ONCE, 2L);
        check(store.size() == 2, "Generation retirement fixture did not persist both terminal Stories");
        check(
            store.discardByStoryIds(Collections.singleton(retired.getId())) == 1,
            "Generation retirement did not remove the selected terminal Story");
        check(store.get(PLAYER, retired.getId()) == null, "Retired terminal Story remained restart-blocking");
        check(store.get(PLAYER, retained.getId()) != null, "Generation retirement touched another Story");
    }

    private static void restartRoundTripAndAtomicFailure() {
        final CanonicalGraphResource waiting = waitingStory("waiting_story");
        CanonicalStoryInstanceStore source = new CanonicalStoryInstanceStore();
        source.start(PLAYER, waiting, "trigger", CanonicalStoryRepeatPolicy.REPEATABLE, 700L);
        NBTTagCompound saved = source.writeToNbt();

        CanonicalStoryInstanceStore restored = new CanonicalStoryInstanceStore();
        restored.readFromNbt(saved, new CanonicalStoryResourceResolver() {

            @Override
            public CanonicalGraphResource resolve(String storyId) {
                return waiting.getId()
                    .equals(storyId) ? waiting : null;
            }
        });
        CanonicalStoryInstance value = restored.get(PLAYER, waiting.getId());
        check(value != null, "Persisted Story was not restored");
        check(value.getActivationTime() == 700L, "Activation time did not survive restart");
        check(
            value.getRuntime()
                .getWaitKind() == CanonicalStoryWaitKind.SESSION,
            "Session wait did not survive restart");

        final CanonicalGraphResource drifted = waitingStoryWithDisplayChange("waiting_story");
        expectGraphFailure("story.restore.fingerprint", new Runnable() {

            @Override
            public void run() {
                restored.readFromNbt(saved, new CanonicalStoryResourceResolver() {

                    @Override
                    public CanonicalGraphResource resolve(String storyId) {
                        return drifted;
                    }
                });
            }
        });
        check(restored.get(PLAYER, waiting.getId()) == value, "Failed restore partially replaced the live store");
    }

    private static void strictNbtBoundary() {
        CanonicalStoryInstanceStore store = new CanonicalStoryInstanceStore();
        store.start(PLAYER, terminalStory("strict_story"), "trigger", CanonicalStoryRepeatPolicy.ONCE, 800L);
        final NBTTagCompound unknown = store.writeToNbt();
        unknown.setString("unexpected", "value");
        expectIllegal(new Runnable() {

            @Override
            public void run() {
                CanonicalStoryInstanceNbtCodec.decode(unknown);
            }
        });

        final CanonicalStoryInstanceSnapshot snapshot = store.snapshots()
            .get(0);
        expectIllegal(new Runnable() {

            @Override
            public void run() {
                CanonicalStoryInstanceNbtCodec.encode(Arrays.asList(snapshot, snapshot));
            }
        });
    }

    private static void atomicSessionHandoffCheckpoint() {
        final CanonicalGraphResource waiting = waitingStory("handoff_story");
        CanonicalStoryInstanceStore stories = new CanonicalStoryInstanceStore();
        stories.start(PLAYER, waiting, "trigger", CanonicalStoryRepeatPolicy.ONCE, 900L);
        CanonicalStoryPendingContinuation continuation = new CanonicalStoryPendingContinuation(
            PLAYER,
            waiting.getId(),
            "session",
            "session_resource",
            12L,
            "done",
            "end",
            "flow_in",
            Collections.<String, Boolean>emptyMap());
        NBTTagCompound checkpoint = CanonicalSessionWorldStateNbtCodec.encode(
            Collections.<darkgrey.rpg.session.instance.CanonicalSessionInstanceSnapshot>emptyList(),
            13L,
            Collections.singletonList(continuation),
            stories.snapshots());

        CanonicalSessionSavedData data = new CanonicalSessionSavedData();
        data.readFromNBT(checkpoint);
        bindWorldState(data, waiting);
        CanonicalStoryInstanceSnapshot advanced = data.resumeStorySession(PLAYER, waiting.getId(), 950L);
        check(advanced.getStatus() == CanonicalStoryStatus.TERMINATED, "Durable Session handoff did not advance Story");
        check(data.getPendingContinuation(PLAYER, waiting.getId()) == null, "Consumed handoff remained pending");

        NBTTagCompound after = new NBTTagCompound();
        data.writeToNBT(after);
        CanonicalSessionSavedData restarted = new CanonicalSessionSavedData();
        restarted.readFromNBT(after);
        bindWorldState(restarted, waiting);
        check(
            restarted.getStorySnapshot(PLAYER, waiting.getId())
                .getStatus() == CanonicalStoryStatus.TERMINATED,
            "Advanced Story checkpoint did not survive restart");
        check(
            restarted.getPendingContinuation(PLAYER, waiting.getId()) == null,
            "Consumed handoff reappeared after restart");
    }

    private static void bindWorldState(CanonicalSessionSavedData data, final CanonicalGraphResource story) {
        data.bind(new CanonicalSessionResourceResolver() {

            @Override
            public CanonicalGraphResource resolve(String sessionResourceId) {
                return null;
            }
        }, new CanonicalStoryResourceResolver() {

            @Override
            public CanonicalGraphResource resolve(String storyId) {
                return story.getId()
                    .equals(storyId) ? story : null;
            }
        });
    }

    private static CanonicalGraphResource terminalStory(String id) {
        return story(
            id,
            "Start",
            Arrays.asList(start(), node("end", "terminate", ports(flowIn("flow_in")), empty())),
            Collections.singletonList(flow("start", "trigger", "end", "flow_in")));
    }

    private static CanonicalGraphResource waitingStory(String id) {
        return story(
            id,
            "Start",
            Arrays.asList(
                start(),
                node(
                    "session",
                    "session",
                    ports(flowIn("flow_in"), logicIn("logic_in"), flowOut("done", 2)),
                    props("resource_id", "session_resource")),
                node("end", "terminate", ports(flowIn("flow_in")), empty())),
            Arrays.asList(flow("start", "trigger", "session", "flow_in"), flow("session", "done", "end", "flow_in")));
    }

    private static CanonicalGraphResource waitingStoryWithDisplayChange(String id) {
        return story(
            id,
            "Changed Start",
            Arrays.asList(
                node("start", "start", "Changed Start", ports(flowOut("trigger", 0)), empty()),
                node(
                    "session",
                    "session",
                    ports(flowIn("flow_in"), logicIn("logic_in"), flowOut("done", 2)),
                    props("resource_id", "session_resource")),
                node("end", "terminate", ports(flowIn("flow_in")), empty())),
            Arrays.asList(flow("start", "trigger", "session", "flow_in"), flow("session", "done", "end", "flow_in")));
    }

    private static CanonicalGraphResource flowJudgmentStory() {
        return story(
            "flow_judgment_story",
            "Start",
            Arrays.asList(
                start(),
                node("gate", "action", ports(flowIn("flow_in"), flowOut("flow_out", 1)), empty()),
                node(
                    "judgment",
                    "flow_judgment",
                    ports(flowIn("flow_in"), flowOut("flow_out", 1), logicOut("executed", 2)),
                    empty()),
                node("end", "terminate", ports(flowIn("flow_in")), empty())),
            Arrays.asList(
                flow("start", "trigger", "gate", "flow_in"),
                flow("gate", "flow_out", "judgment", "flow_in"),
                flow("judgment", "flow_out", "end", "flow_in")));
    }

    private static CanonicalGraphNode start() {
        return node("start", "start", ports(flowOut("trigger", 0)), empty());
    }

    private static CanonicalGraphResource story(String id, String displayName, java.util.List<CanonicalGraphNode> nodes,
        java.util.List<CanonicalGraphConnection> edges) {
        return new CanonicalGraphResource(
            CanonicalGraphResource.CURRENT_SCHEMA_VERSION,
            CanonicalGraphResourceKind.STORY,
            id,
            displayName,
            new CanonicalGraph(nodes, edges));
    }

    private static CanonicalGraphNode node(String id, String type, java.util.List<CanonicalGraphPort> ports,
        Map<String, JsonElement> properties) {
        return node(id, type, id, ports, properties);
    }

    private static CanonicalGraphNode node(String id, String type, String displayName,
        java.util.List<CanonicalGraphPort> ports, Map<String, JsonElement> properties) {
        // Fixture upgrade: public termination metadata belongs in test data, not runtime fallback.
        if ("terminate".equals(type)) {
            properties = new java.util.LinkedHashMap<String, JsonElement>(properties);
            if (!properties.containsKey("port_id")) properties.put("port_id", new com.google.gson.JsonPrimitive(id));
            if (!properties.containsKey("display_name"))
                properties.put("display_name", new com.google.gson.JsonPrimitive(id));
        }
        return new CanonicalGraphNode(id, type, displayName, ports, properties);
    }

    private static java.util.List<CanonicalGraphPort> ports(CanonicalGraphPort... values) {
        return Arrays.asList(values);
    }

    private static CanonicalGraphPort flowIn(String id) {
        return new CanonicalGraphPort(id, id, CanonicalGraphPortDirection.INPUT, CanonicalGraphInterfaceKind.FLOW, 0);
    }

    private static CanonicalGraphPort logicIn(String id) {
        return new CanonicalGraphPort(id, id, CanonicalGraphPortDirection.INPUT, CanonicalGraphInterfaceKind.LOGIC, 1);
    }

    private static CanonicalGraphPort flowOut(String id, int order) {
        return new CanonicalGraphPort(
            id,
            id,
            CanonicalGraphPortDirection.OUTPUT,
            CanonicalGraphInterfaceKind.FLOW,
            order);
    }

    private static CanonicalGraphPort logicOut(String id, int order) {
        return new CanonicalGraphPort(
            id,
            id,
            CanonicalGraphPortDirection.OUTPUT,
            CanonicalGraphInterfaceKind.LOGIC,
            order);
    }

    private static CanonicalGraphConnection flow(String fromNode, String fromPort, String toNode, String toPort) {
        return new CanonicalGraphConnection(fromNode, fromPort, toNode, toPort, CanonicalGraphInterfaceKind.FLOW);
    }

    private static Map<String, JsonElement> props(Object... values) {
        LinkedHashMap<String, JsonElement> result = new LinkedHashMap<String, JsonElement>();
        JsonParser parser = new JsonParser();
        for (int index = 0; index < values.length; index += 2) {
            Object value = values[index + 1];
            String json = value instanceof Number ? value.toString() : "\"" + value + "\"";
            result.put((String) values[index], parser.parse(json));
        }
        return result;
    }

    private static Map<String, JsonElement> empty() {
        return Collections.emptyMap();
    }

    private static void expectGraphFailure(String code, Runnable action) {
        try {
            action.run();
        } catch (CanonicalGraphResourceException expected) {
            check(code.equals(expected.getCode()), "Expected " + code + " but got " + expected.getCode());
            return;
        }
        throw new AssertionError("Expected graph failure " + code);
    }

    private static void expectIllegal(Runnable action) {
        try {
            action.run();
        } catch (IllegalArgumentException expected) {
            return;
        }
        throw new AssertionError("Expected IllegalArgumentException");
    }

    private static void check(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }
}
