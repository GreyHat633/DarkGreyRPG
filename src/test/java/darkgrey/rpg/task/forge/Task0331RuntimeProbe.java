package darkgrey.rpg.task.forge;

import java.util.Arrays;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;

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
import darkgrey.rpg.task.runtime.CanonicalTaskEvent;
import darkgrey.rpg.task.runtime.CanonicalTaskRuntime;

/** Focused 0.3.3.1 WP-C probe for actor-bound submit and integer regions. */
public final class Task0331RuntimeProbe {

    private Task0331RuntimeProbe() {}

    public static void main(String[] args) {
        actorBoundSubmit();
        integerRegionAndNegativeFloor();
        submitChoiceCapability();
        submitChoiceCodec();
        System.out.println("TASK_0331_ACTOR_BOUND_SUBMIT=PASS");
        System.out.println("TASK_0331_INTEGER_REGION_NEGATIVE_COORDS=PASS");
        System.out.println("TASK_0331_SUBMIT_CHOICE_STALE_AND_SINGLE_USE=PASS");
        System.out.println("TASK_0331_SUBMIT_CHOICE_CODEC=PASS");
    }

    private static void actorBoundSubmit() {
        CanonicalTaskRuntime runtime = CanonicalTaskRuntime.start(
            resource(
                "submit_item",
                ",\"required\":1,\"item\":\"minecraft:apple\",\"actor_id\":\"probe_actor\",\"metadata\":{}"));
        require(!runtime.accept(submit("wrong_actor")), "Wrong actor must not submit");
        require(!runtime.accept(itemOnly()), "Submit event without actor must not submit");
        require(runtime.accept(submit("probe_actor")), "Matching actor must submit");
        require(!runtime.accept(submit("probe_actor")), "Completed submit must latch");
    }

    private static void integerRegionAndNegativeFloor() {
        require(net.minecraft.util.MathHelper.floor_double(-0.1D) == -1, "Negative block floor");
        CanonicalTaskRuntime runtime = CanonicalTaskRuntime.start(
            resource(
                "reach_region",
                ",\"dimension_id\":7,\"center_x\":-1,\"center_y\":-2,\"center_z\":-3,\"radius\":0"));
        require(runtime.accept(position(7, -1, -2, -3)), "Negative center block");
        runtime = CanonicalTaskRuntime.start(
            resource(
                "reach_region",
                ",\"dimension_id\":7,\"center_x\":-1,\"center_y\":-2,\"center_z\":-3,\"radius\":0"));
        require(!runtime.accept(position(7, 0, -2, -3)), "Adjacent block outside zero radius");
        try {
            CanonicalTaskRuntime.start(
                resource(
                    "reach_region",
                    ",\"dimension_id\":7,\"center_x\":-1.5,\"center_y\":-2,\"center_z\":-3,\"radius\":0"));
            throw new AssertionError("Fractional region coordinate accepted");
        } catch (RuntimeException expected) {}
    }

    private static void submitChoiceCapability() {
        UUID player = UUID.fromString("11111111-1111-1111-1111-111111111111");
        UUID entity = UUID.fromString("22222222-2222-2222-2222-222222222222");
        CanonicalTaskSubmitChoiceStore store = new CanonicalTaskSubmitChoiceStore();
        List<CanonicalTaskSubmitChoiceStore.Candidate> candidates = Arrays.asList(
            new CanonicalTaskSubmitChoiceStore.Candidate("story_a", "placement_a", "objective_a", "actor", 1L, "A"),
            new CanonicalTaskSubmitChoiceStore.Candidate("story_b", "placement_b", "objective_b", "actor", 2L, "B"));
        CanonicalTaskSubmitChoiceStore.Choice offered = store.offer(player, entity, 7, 0, candidates, 100L);
        require(store.consume(player, offered.getToken() + 1, 101L) == null, "Wrong token rejected");
        CanonicalTaskSubmitChoiceStore.Choice selected = store.consume(player, offered.getToken(), 101L);
        require(
            selected != null && selected.getCandidates()
                .size() == 2,
            "One candidate set selected");
        require(store.consume(player, offered.getToken(), 102L) == null, "Choice is single use");
        CanonicalTaskSubmitChoiceStore.Choice expired = store.offer(player, entity, 7, 0, candidates, 100L);
        require(store.consume(player, expired.getToken(), 60101L) == null, "Expired response rejected");
    }

    private static void submitChoiceCodec() {
        java.util.List<darkgrey.rpg.network.message.canonical.CanonicalTaskSubmitChoiceFrame.Option> options = Arrays
            .asList(
                new darkgrey.rpg.network.message.canonical.CanonicalTaskSubmitChoiceFrame.Option("a", "任务 A"),
                new darkgrey.rpg.network.message.canonical.CanonicalTaskSubmitChoiceFrame.Option("b", "任务 B"));
        darkgrey.rpg.network.message.canonical.CanonicalTaskSubmitChoiceFrame source = new darkgrey.rpg.network.message.canonical.CanonicalTaskSubmitChoiceFrame(
            9L,
            options);
        io.netty.buffer.ByteBuf encoded = io.netty.buffer.Unpooled.buffer();
        source.toBytes(encoded);
        darkgrey.rpg.network.message.canonical.CanonicalTaskSubmitChoiceFrame decoded = new darkgrey.rpg.network.message.canonical.CanonicalTaskSubmitChoiceFrame();
        decoded.fromBytes(encoded.copy());
        require(
            decoded.getToken() == 9L && decoded.getOptions()
                .size() == 2,
            "Choice frame roundtrip");
        darkgrey.rpg.network.message.canonical.CanonicalTaskSubmitChoiceSelection selection = new darkgrey.rpg.network.message.canonical.CanonicalTaskSubmitChoiceSelection(
            9L,
            1);
        encoded.clear();
        selection.toBytes(encoded);
        darkgrey.rpg.network.message.canonical.CanonicalTaskSubmitChoiceSelection decodedSelection = new darkgrey.rpg.network.message.canonical.CanonicalTaskSubmitChoiceSelection();
        decodedSelection.fromBytes(encoded.copy());
        require(
            decodedSelection.getToken() == 9L && decodedSelection.getOptionIndex() == 1,
            "Choice selection roundtrip");
    }

    private static CanonicalTaskEvent submit(String actor) {
        Map<String, String> values = new LinkedHashMap<String, String>();
        values.put("item", "minecraft:apple");
        values.put("actor_id", actor);
        return new CanonicalTaskEvent("submit_item", values, 1);
    }

    private static CanonicalTaskEvent itemOnly() {
        return new CanonicalTaskEvent("submit_item", Collections.singletonMap("item", "minecraft:apple"), 1);
    }

    private static CanonicalTaskEvent position(int dimension, int x, int y, int z) {
        Map<String, String> values = new LinkedHashMap<String, String>();
        values.put("dimension_id", String.valueOf(dimension));
        values.put("x", String.valueOf(x));
        values.put("y", String.valueOf(y));
        values.put("z", String.valueOf(z));
        return new CanonicalTaskEvent("reach_region", values, 1);
    }

    private static CanonicalGraphResource resource(String type, String extra) {
        JsonObject properties = new JsonParser()
            .parse("{\"objective_type\":\"" + type + "\",\"description\":\"目标\"" + extra + "}")
            .getAsJsonObject();
        Map<String, JsonElement> objectiveProperties = new LinkedHashMap<String, JsonElement>();
        for (Map.Entry<String, JsonElement> entry : properties.entrySet())
            objectiveProperties.put(entry.getKey(), entry.getValue());
        CanonicalGraphNode objective = new CanonicalGraphNode(
            "objective",
            "objective",
            "目标",
            Collections.singletonList(
                new CanonicalGraphPort(
                    "logic_status",
                    "完成",
                    CanonicalGraphPortDirection.OUTPUT,
                    CanonicalGraphInterfaceKind.LOGIC,
                    0)),
            objectiveProperties);
        CanonicalGraphNode settle = new CanonicalGraphNode(
            "settle",
            "settle",
            "结算",
            Collections.singletonList(
                new CanonicalGraphPort(
                    "done",
                    "完成",
                    CanonicalGraphPortDirection.INPUT,
                    CanonicalGraphInterfaceKind.LOGIC,
                    0)),
            Collections.<String, JsonElement>emptyMap());
        return new CanonicalGraphResource(
            1,
            CanonicalGraphResourceKind.TASK,
            "task",
            "任务",
            new CanonicalGraph(
                Arrays.asList(objective, settle),
                Collections.singletonList(
                    new CanonicalGraphConnection(
                        "objective",
                        "logic_status",
                        "settle",
                        "done",
                        CanonicalGraphInterfaceKind.LOGIC))));
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
