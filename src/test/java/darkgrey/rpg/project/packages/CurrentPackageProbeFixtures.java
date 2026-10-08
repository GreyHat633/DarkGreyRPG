package darkgrey.rpg.project.packages;

import java.util.Arrays;

import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

/** Current wire-format fixtures for isolated loader mutations and diagnostics. */
final class CurrentPackageProbeFixtures {

    static final String A = "ST-2345-6789-ABCD-EFGH";
    static final String B = "ST-JKLM-NPQR-STUV-WXYZ";
    static final String C = "ST-AAAA-BBBB-CCCC-DDDD";
    static final String SOURCE = "ST-2222-3333-4444-5555";
    static final String TARGET = "ST-3456-789A-BCDE-FGHJ";

    private CurrentPackageProbeFixtures() {}

    static JsonElement address(String key) {
        try {
            return new JsonParser().parse(
                darkgrey.rpg.identity.ResourceAddressJson
                    .serialize(darkgrey.rpg.identity.ResourceAddress.fromKey(key)));
        } catch (java.io.IOException exception) {
            throw new AssertionError(exception);
        }
    }

    static JsonArray list(String... values) {
        JsonArray array = new JsonArray();
        for (String value : values) array.add(new com.google.gson.JsonPrimitive(value));
        return array;
    }

    static JsonObject project() {
        return new JsonParser().parse(
            "{\"schema_version\":3,\"identity_format\":\"story-uid-v1\",\"id\":\"probe\",\"display_name\":\"Probe\"}")
            .getAsJsonObject();
    }

    static JsonObject manifest(String uid) {
        JsonObject root = new JsonObject();
        root.addProperty("schema_version", 3);
        root.addProperty("format", "dgrs");
        root.addProperty("format_version", 3);
        root.addProperty("identity_format", "story-uid-v1");
        root.addProperty("producer", "DarkGreyRPGStudio");
        root.addProperty("producer_version", "0.4.0.0");
        root.addProperty("package_id", uid);
        root.addProperty("package_version", "1.0.0");
        root.addProperty("story_id", uid);
        root.addProperty("story_schema_version", 3);
        JsonObject resources = new JsonObject();
        resources.addProperty("story", storyPath(uid));
        resources.add("canonical_stories", list(storyPath(uid)));
        resources.add("canonical_memberships", list(membershipPath(uid)));
        for (String field : Arrays.asList("actors", "items", "item_groups", "sessions", "tasks", "media"))
            resources.add(field, list());
        root.add("required_resources", resources);
        return root;
    }

    static String storyPath(String uid) {
        return "resources/canonical/stories/" + uid + ".json";
    }

    static String membershipPath(String uid) {
        return "resources/canonical/memberships/" + uid + ".json";
    }

    static JsonObject membership(String uid) {
        JsonObject root = new JsonObject();
        root.addProperty("schema_version", 4);
        root.addProperty("identity_format", "story-uid-v1");
        root.addProperty("story_id", uid);
        for (String scope : Arrays.asList("owned_resources", "referenced_resources")) {
            JsonObject resources = new JsonObject();
            for (String kind : Arrays.asList("actors", "items", "item_groups", "sessions", "tasks"))
                resources.add(kind, list());
            root.add(scope, resources);
        }
        JsonObject order = new JsonObject();
        for (String kind : Arrays.asList("actors", "items", "sessions", "tasks")) order.add(kind, list());
        root.add("display_order", order);
        return root;
    }

    static JsonObject story(String uid, String name) {
        JsonObject root = new JsonObject();
        root.addProperty("schema_version", 3);
        root.addProperty("identity_format", "story-uid-v1");
        root.addProperty("resource_kind", "story");
        root.addProperty("id", uid);
        root.addProperty("display_name", name);
        JsonObject graph = new JsonObject();
        JsonArray nodes = new JsonArray();
        nodes.add(
            new JsonParser().parse(
                "{\"id\":\"start\",\"type\":\"start\",\"display_name\":\"Start\",\"ports\":[{\"port_id\":\"entry\",\"display_name\":\"Entry\",\"kind\":\"flow\",\"direction\":\"output\",\"order\":0}],\"properties\":{\"repeat_policy\":\"once\",\"triggers\":[{\"port_id\":\"entry\",\"display_name\":\"Entry\",\"trigger_type\":\"enter_region\",\"trigger_properties\":{\"dimension\":0,\"x\":0,\"y\":0,\"z\":0,\"radius\":3},\"order\":0}]}}"));
        graph.add("nodes", nodes);
        graph.add("connections", new JsonArray());
        root.add("graph", graph);
        return root;
    }

    static JsonObject actor(String key, String name, String notes) {
        JsonObject root = new JsonObject();
        root.addProperty("schema_version", 5);
        root.addProperty("identity_format", "story-uid-v1");
        root.addProperty("type", "individual");
        root.add("npc_id", address(key));
        root.addProperty("display_name", notes.isEmpty() ? name : name + " · " + notes);
        root.add("tags", list());
        root.addProperty("home_story_id", A);
        return root;
    }
}
