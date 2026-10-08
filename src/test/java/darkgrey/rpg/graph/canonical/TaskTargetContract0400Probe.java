package darkgrey.rpg.graph.canonical;

import java.nio.charset.StandardCharsets;
import java.util.Arrays;

import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import com.google.gson.JsonPrimitive;

import darkgrey.rpg.identity.ResourceAddress;
import darkgrey.rpg.identity.ResourceAddressJson;
import darkgrey.rpg.task.runtime.CanonicalTaskRuntime;

/** Strict file/archive graph decoder, including direct current-schema retirement vectors. */
public final class TaskTargetContract0400Probe {

    private static final String OWNER = "ST-2345-6789-ABCD-EFGH";

    private TaskTargetContract0400Probe() {}

    public static void run() {
        for (String type : Arrays.asList("kill_entity", "collect_item", "submit_item", "interact_actor")) {
            String field = "kill_entity".equals(type) ? "entity" : "interact_actor".equals(type) ? "actor_id" : "item";
            String kind = "item".equals(field) ? "item" : "actor";
            String key = OWNER + "~" + kind + "~target";
            CanonicalGraphResource resource = load(type, field, address(key), 3);
            require(
                key.equals(
                    resource.getGraph()
                        .getNodes()
                        .get(0)
                        .getProperties()
                        .get(field)
                        .getAsString()),
                "Typed decode");
            CanonicalTaskRuntime.start(resource);
            load(type, field, new JsonPrimitive(""), 3);
            for (JsonElement bad : Arrays.asList(
                new JsonPrimitive("minecraft:zombie"),
                new JsonParser().parse("{\"registry_name\":\"minecraft:zombie\"}"),
                address(OWNER + "~session~wrong"),
                new JsonPrimitive(" "))) reject(() -> load(type, field, bad, 3), "graph.reference.identity.invalid");
            reject(() -> load(type, field, address(key), 2), "graph.resource.schema_version.unsupported");
        }
        load("collect_item", "item", address(OWNER + "~item_group~group"), 3);
        reject(
            () -> load("kill_entity", "entity", address(OWNER + "~item~wrong"), 3),
            "graph.reference.identity.invalid");
        reject(
            () -> load("collect_item", "item", address(OWNER + "~actor~wrong"), 3),
            "graph.reference.identity.invalid");
        JsonObject legacy = new JsonParser()
            .parse("{\"action_type\":\"give_item\",\"item\":\"minecraft:apple\",\"metadata\":0,\"amount\":1}")
            .getAsJsonObject();
        reject(
            () -> GraphResourceAddressCodec.decode(legacy, "action", CanonicalGraphResourceKind.STORY),
            "graph.reference.identity.invalid");
        System.out.println("TASK_0400_TYPED_GRAPH_DECODER_NATIVE_KIND_SCHEMA_REJECTION=PASS");
    }

    private static CanonicalGraphResource load(String type, String field, JsonElement target, int schema) {
        JsonObject root = new JsonParser().parse(
            "{\"schema_version\":3,\"identity_format\":\"story-uid-v1\",\"resource_kind\":\"task\","
                + "\"id\":{\"story_uid\":\""
                + OWNER
                + "\",\"kind\":\"task\",\"local_id\":\"decoder\"},\"display_name\":\"Task\","
                + "\"graph\":{\"nodes\":[{\"id\":\"objective\",\"type\":\"objective\",\"display_name\":\"Objective\","
                + "\"ports\":[{\"port_id\":\"logic_status\",\"display_name\":\"Done\",\"direction\":\"output\",\"kind\":\"logic\",\"order\":0}],"
                + "\"properties\":{}}],\"connections\":[]}}")
            .getAsJsonObject();
        root.addProperty("schema_version", schema);
        JsonObject properties = root.getAsJsonObject("graph")
            .getAsJsonArray("nodes")
            .get(0)
            .getAsJsonObject()
            .getAsJsonObject("properties");
        properties.addProperty("objective_type", type);
        properties.addProperty("description", "minecraft:zombie");
        if (!"interact_actor".equals(type)) properties.addProperty("required", 1);
        if ("item".equals(field)) {
            properties.add("metadata", new JsonObject());
            if ("submit_item".equals(type)) properties.add("actor_id", address(OWNER + "~actor~receiver"));
        }
        properties.add(field, target);
        return new CanonicalGraphResourceLoader().load(
            root.toString()
                .getBytes(StandardCharsets.UTF_8),
            "task.json",
            CanonicalGraphResourceKind.TASK);
    }

    private static JsonElement address(String key) {
        try {
            return new JsonParser().parse(ResourceAddressJson.serialize(ResourceAddress.fromKey(key)));
        } catch (java.io.IOException failure) {
            throw new AssertionError(failure);
        }
    }

    private static void reject(Runnable action, String code) {
        try {
            action.run();
        } catch (CanonicalGraphResourceException expected) {
            require(code.equals(expected.getCode()), "Unexpected diagnostic: " + expected.getCode());
            return;
        }
        throw new AssertionError("Retired graph input accepted");
    }

    private static void require(boolean valid, String message) {
        if (!valid) throw new AssertionError(message);
    }
}
