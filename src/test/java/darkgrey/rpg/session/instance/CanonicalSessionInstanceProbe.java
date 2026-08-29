package darkgrey.rpg.session.instance;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.HashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

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
import darkgrey.rpg.session.runtime.CanonicalSessionStep;

/** Bounded offline matrix for Session identity, fencing, persistence, and atomic restore. */
public final class CanonicalSessionInstanceProbe {

    private static final UUID PLAYER_ONE = UUID.fromString("00000000-0000-0000-0000-000000000001");
    private static final UUID PLAYER_TWO = UUID.fromString("00000000-0000-0000-0000-000000000002");

    private CanonicalSessionInstanceProbe() {}

    public static void main(String[] args) {
        CanonicalGraphResource choice = choiceResource("choice-session");
        CanonicalGraphResource linear = linearResource("linear-session");
        CanonicalSessionInstanceStore store = new CanonicalSessionInstanceStore();
        CanonicalSessionInstance active = store.start(PLAYER_ONE, "choice-story", "choice-placement", choice);
        require(
            active.getCurrentStep()
                .getKind() == CanonicalSessionStep.Kind.CHOICE,
            "Choice start");
        require(active.getTransportId() == 1L, "first transport ID");
        String activeBefore = store.writeToNbt()
            .toString();
        reject(new Runnable() {

            @Override
            public void run() {
                store.selectChoice(PLAYER_TWO, "choice-story", 1L, "choice", "left");
            }
        }, "forged player");
        reject(new Runnable() {

            @Override
            public void run() {
                store.selectChoice(PLAYER_ONE, "wrong-story", 1L, "choice", "left");
            }
        }, "forged story");
        reject(new Runnable() {

            @Override
            public void run() {
                store.selectChoice(PLAYER_ONE, "choice-story", 99L, "choice", "left");
            }
        }, "stale transport");
        reject(new Runnable() {

            @Override
            public void run() {
                store.selectChoice(PLAYER_ONE, "choice-story", 1L, "wrong-node", "left");
            }
        }, "stale node");
        reject(new Runnable() {

            @Override
            public void run() {
                store.selectChoice(PLAYER_ONE, "choice-story", 1L, "choice", "missing");
            }
        }, "unknown option");
        require(
            activeBefore.equals(
                store.writeToNbt()
                    .toString()),
            "forged action mutated state");
        reject(new Runnable() {

            @Override
            public void run() {
                store.selectChoice(null, "choice-story", 1L, "choice", "left");
            }
        }, "null player");
        reject(new Runnable() {

            @Override
            public void run() {
                store.continueLine(PLAYER_ONE, "choice-story", 1L, null);
            }
        }, "null node");
        require(
            activeBefore.equals(
                store.writeToNbt()
                    .toString()),
            "null action mutated state");

        store.selectChoice(PLAYER_ONE, "choice-story", 1L, "choice", "right");
        require(active.isCompleted(), "Choice completion");
        require(
            active.getRuntime()
                .getSelectedOptionIds()
                .size() == 1,
            "Choice history");
        require(
            "right".equals(
                active.getRuntime()
                    .snapshot()
                    .getLatestChoiceSelections()
                    .get("choice")),
            "latest Choice map");
        require(
            Boolean.TRUE.equals(
                active.getRuntime()
                    .getPublicLogicOutputs()
                    .get("picked_right")),
            "public Logic output");

        CanonicalSessionInstance otherStory = store.start(PLAYER_ONE, "other-story", "other-placement", linear);
        CanonicalSessionInstance otherPlayer = store.start(PLAYER_TWO, "choice-story", "player-placement", choice);
        require(
            otherStory.getTransportId() > active.getTransportId()
                && otherPlayer.getTransportId() > otherStory.getTransportId(),
            "monotonic IDs");
        String duplicateBefore = store.writeToNbt()
            .toString();
        reject(new Runnable() {

            @Override
            public void run() {
                store.start(PLAYER_ONE, "choice-story", "replacement", choice);
            }
        }, "duplicate same Story");
        require(
            duplicateBefore.equals(
                store.writeToNbt()
                    .toString()),
            "duplicate replaced state");
        NBTTagCompound encoded = store.writeToNbt();
        List<CanonicalSessionInstanceSnapshot> detached = CanonicalSessionInstanceNbtCodec.decode(encoded);
        NBTTagCompound deterministic = CanonicalSessionInstanceNbtCodec
            .encode(detached, encoded.getLong("next_transport_id"));
        require(encoded.equals(deterministic), "deterministic NBT round-trip");
        List<CanonicalSessionInstanceSnapshot> duplicateEncode = new ArrayList<CanonicalSessionInstanceSnapshot>(
            detached);
        duplicateEncode.add(detached.get(0));
        reject(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionInstanceNbtCodec.encode(duplicateEncode, encoded.getLong("next_transport_id"));
            }
        }, "duplicate encode input");
        reject(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionInstanceNbtCodec.encode(detached, 1L);
            }
        }, "direct codec transport ordering");

        CanonicalSessionInstanceStore restored = new CanonicalSessionInstanceStore();
        restored.readFromNbt(encoded, new Resolver(choice, linear));
        require(
            restored.size() == 3 && restored.get(PLAYER_ONE, "choice-story")
                .isCompleted(),
            "active/completed restart");
        require(
            restored.get(PLAYER_TWO, "choice-story")
                .getCurrentStep()
                .getKind() == CanonicalSessionStep.Kind.CHOICE,
            "active restart cursor");
        require(
            restored.start(PLAYER_TWO, "new-story", "new-placement", linear)
                .getTransportId() == 4L,
            "restart monotonicity");
        require(restored.consume(PLAYER_ONE, "choice-story", 1L), "explicit consume");
        require(!restored.consume(PLAYER_ONE, "choice-story", 1L), "consume idempotence");
        require(!restored.consume(PLAYER_ONE, "other-story", 2L), "active consume fails closed");
        CanonicalSessionInstanceStore consumedStore = new CanonicalSessionInstanceStore();
        CanonicalSessionInstance consumed = consumedStore.start(PLAYER_ONE, "consume", "consume-placement", linear);
        consumedStore.continueLine(PLAYER_ONE, "consume", consumed.getTransportId(), "line");
        require(consumedStore.consume(PLAYER_ONE, "consume", consumed.getTransportId()), "consume completed");
        CanonicalSessionInstanceStore consumedRestart = new CanonicalSessionInstanceStore();
        consumedRestart.readFromNbt(consumedStore.writeToNbt(), new Resolver(choice, linear));
        require(
            consumedRestart.start(PLAYER_ONE, "fresh", "fresh-placement", linear)
                .getTransportId() == 2L,
            "empty-after-consume counter");

        strictTamperMatrix(encoded);
        atomicRestoreMatrix(restored, encoded, choice, linear);
        System.out.println("CANONICAL_SESSION_INSTANCE_PROBE=PASS");
    }

    private static void strictTamperMatrix(NBTTagCompound encoded) {
        rejectDecode(copyWithRoot(encoded, "schema_version", 99), "unsupported schema");
        NBTTagCompound unknown = copy(encoded);
        unknown.setString("unknown", "x");
        rejectDecode(unknown, "unknown root key");
        NBTTagCompound missing = copy(encoded);
        missing.removeTag("instances");
        rejectDecode(missing, "missing root key");
        NBTTagCompound wrongType = copy(encoded);
        wrongType.setString("next_transport_id", "wrong");
        rejectDecode(wrongType, "wrong root type");
        NBTTagCompound wrongElementType = copy(encoded);
        NBTTagList wrongInstances = new NBTTagList();
        wrongInstances.appendTag(new net.minecraft.nbt.NBTTagString("not-a-compound"));
        wrongElementType.setTag("instances", wrongInstances);
        rejectDecode(wrongElementType, "wrong instance element type");
        NBTTagCompound invalidNext = copy(encoded);
        invalidNext.setLong("next_transport_id", 0L);
        rejectDecode(invalidNext, "invalid next counter");
        NBTTagCompound invalidUuid = copy(encoded);
        instanceAt(invalidUuid).setString("player_uuid", "invalid");
        rejectDecode(invalidUuid, "invalid UUID");
        NBTTagCompound invalidStatus = copy(encoded);
        instanceAt(invalidStatus).setString("status", "NOPE");
        rejectDecode(invalidStatus, "invalid status");
        NBTTagCompound invalidBoolean = copy(encoded);
        instanceAt(invalidBoolean).setByte("activation_logic", (byte) 2);
        rejectDecode(invalidBoolean, "invalid boolean");
        NBTTagCompound duplicateMap = copy(encoded);
        NBTTagList map = instanceAt(duplicateMap).getTagList("public_logic_outputs", 10);
        if (map.tagCount() > 0) map.appendTag(
            map.getCompoundTagAt(0)
                .copy());
        rejectDecode(duplicateMap, "duplicate map key");
        NBTTagCompound duplicateInstance = copy(encoded);
        NBTTagList instances = duplicateInstance.getTagList("instances", 10);
        instances.appendTag(
            instances.getCompoundTagAt(0)
                .copy());
        rejectDecode(duplicateInstance, "duplicate player/story");
    }

    private static void atomicRestoreMatrix(CanonicalSessionInstanceStore existing, NBTTagCompound encoded,
        CanonicalGraphResource choice, CanonicalGraphResource linear) {
        String before = existing.writeToNbt()
            .toString();
        long nextBefore = existing.writeToNbt()
            .getLong("next_transport_id");
        reject(new Runnable() {

            @Override
            public void run() {
                existing.readFromNbt(encoded, new CanonicalSessionResourceResolver() {

                    @Override
                    public CanonicalGraphResource resolve(String id) {
                        return null;
                    }
                });
            }
        }, "missing resource");
        require(
            before.equals(
                existing.writeToNbt()
                    .toString())
                && nextBefore == existing.writeToNbt()
                    .getLong("next_transport_id"),
            "missing resource atomicity");
        NBTTagCompound invalidCounter = copy(encoded);
        invalidCounter.setLong("next_transport_id", 1L);
        reject(new Runnable() {

            @Override
            public void run() {
                existing.readFromNbt(invalidCounter, new Resolver(choice, linear));
            }
        }, "transport counter ordering");
        require(
            before.equals(
                existing.writeToNbt()
                    .toString()),
            "counter failure atomicity");
        NBTTagCompound lateFailure = copy(encoded);
        NBTTagList tags = lateFailure.getTagList("instances", 10);
        tags.getCompoundTagAt(tags.tagCount() - 1)
            .setString("session_resource_id", "missing");
        reject(new Runnable() {

            @Override
            public void run() {
                existing.readFromNbt(lateFailure, new Resolver(choice, linear));
            }
        }, "late resource failure");
        require(
            before.equals(
                existing.writeToNbt()
                    .toString()),
            "late restore atomicity");
        require(choice != null && linear != null, "atomic setup");
    }

    private static NBTTagCompound copy(NBTTagCompound source) {
        return (NBTTagCompound) source.copy();
    }

    private static NBTTagCompound instanceAt(NBTTagCompound root) {
        return root.getTagList("instances", 10)
            .getCompoundTagAt(0);
    }

    private static NBTTagCompound copyWithRoot(NBTTagCompound source, String key, int value) {
        NBTTagCompound result = copy(source);
        result.setInteger(key, value);
        return result;
    }

    private static void rejectDecode(NBTTagCompound tag, String label) {
        reject(new Runnable() {

            @Override
            public void run() {
                CanonicalSessionInstanceNbtCodec.decode(tag);
            }
        }, label);
    }

    private static void reject(Runnable action, String label) {
        try {
            action.run();
        } catch (RuntimeException expected) {
            return;
        }
        throw new AssertionError("Expected rejection: " + label);
    }

    private static final class Resolver implements CanonicalSessionResourceResolver {

        private final CanonicalGraphResource choice;
        private final CanonicalGraphResource linear;

        Resolver(CanonicalGraphResource choice, CanonicalGraphResource linear) {
            this.choice = choice;
            this.linear = linear;
        }

        @Override
        public CanonicalGraphResource resolve(String id) {
            return choice.getId()
                .equals(id) ? choice
                    : linear.getId()
                        .equals(id) ? linear : null;
        }
    }

    private static CanonicalGraphResource linearResource(String id) {
        CanonicalGraphNode start = node(
            "start",
            "start",
            ports(port("flow_out", false, false), port("logic_out", false, true)),
            new HashMap<String, JsonElement>());
        Map<String, JsonElement> lineProperties = new HashMap<String, JsonElement>();
        lineProperties.put("speaker_actor_id", json("actor"));
        lineProperties.put("text", json("Hello"));
        CanonicalGraphNode line = node(
            "line",
            "line",
            ports(port("flow_in", true, false), port("flow_out", false, false)),
            lineProperties);
        Map<String, JsonElement> endProperties = new HashMap<String, JsonElement>();
        endProperties.put("port_id", json("success"));
        endProperties.put("display_name", json("Success"));
        CanonicalGraphNode end = node("end", "end", ports(port("flow_in", true, false)), endProperties);
        return resource(
            id,
            Arrays.asList(start, line, end),
            Arrays.asList(edge("start", "flow_out", "line", "flow_in"), edge("line", "flow_out", "end", "flow_in")));
    }

    private static CanonicalGraphResource choiceResource(String id) {
        CanonicalGraphNode start = node(
            "start",
            "start",
            ports(port("flow_out", false, false), port("logic_out", false, true)),
            new HashMap<String, JsonElement>());
        Map<String, JsonElement> properties = new HashMap<String, JsonElement>();
        properties.put("prompt", json("Choose"));
        properties.put(
            "options",
            new JsonParser().parse(
                "[{\"option_id\":\"left\",\"display_text\":\"Left\",\"flow_port_id\":\"flow_left\"},{\"option_id\":\"right\",\"display_text\":\"Right\",\"flow_port_id\":\"flow_right\"}]"));
        CanonicalGraphNode choice = node(
            "choice",
            "choice",
            ports(
                port("flow_in", true, false),
                port("flow_left", false, false),
                port("left", false, true),
                port("flow_right", false, false),
                port("right", false, true)),
            properties);
        Map<String, JsonElement> endProperties = new HashMap<String, JsonElement>();
        endProperties.put("port_id", json("success"));
        endProperties.put("display_name", json("Success"));
        CanonicalGraphNode end = node("end", "end", ports(port("flow_in", true, false)), endProperties);
        Map<String, JsonElement> logicProperties = new HashMap<String, JsonElement>();
        logicProperties.put("port_id", json("picked_right"));
        logicProperties.put("display_name", json("Picked right"));
        CanonicalGraphNode logic = node("logic", "logic_output", ports(port("logic_in", true, true)), logicProperties);
        return resource(
            id,
            Arrays.asList(start, choice, end, logic),
            Arrays.asList(
                edge("start", "flow_out", "choice", "flow_in"),
                edge("choice", "flow_left", "end", "flow_in"),
                edge("choice", "flow_right", "end", "flow_in"),
                new CanonicalGraphConnection(
                    "choice",
                    "right",
                    "logic",
                    "logic_in",
                    CanonicalGraphInterfaceKind.LOGIC)));
    }

    private static CanonicalGraphResource resource(String id, java.util.List<CanonicalGraphNode> nodes,
        java.util.List<CanonicalGraphConnection> edges) {
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.SESSION,
            id,
            id,
            new CanonicalGraph(nodes, edges));
    }

    private static CanonicalGraphNode node(String id, String type, java.util.List<CanonicalGraphPort> ports,
        Map<String, JsonElement> properties) {
        return new CanonicalGraphNode(id, type, id, ports, properties);
    }

    private static java.util.List<CanonicalGraphPort> ports(CanonicalGraphPort... ports) {
        return Arrays.asList(ports);
    }

    private static CanonicalGraphPort port(String id, boolean input, boolean logic) {
        return new CanonicalGraphPort(
            id,
            id,
            input ? CanonicalGraphPortDirection.INPUT : CanonicalGraphPortDirection.OUTPUT,
            logic ? CanonicalGraphInterfaceKind.LOGIC : CanonicalGraphInterfaceKind.FLOW,
            0);
    }

    private static CanonicalGraphConnection edge(String from, String fromPort, String to, String toPort) {
        return new CanonicalGraphConnection(from, fromPort, to, toPort, CanonicalGraphInterfaceKind.FLOW);
    }

    private static JsonElement json(String value) {
        return new JsonParser().parse('"' + value + '"');
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
