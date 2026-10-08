package darkgrey.rpg.graph.canonical;

import java.io.IOException;

import com.google.gson.JsonElement;
import com.google.gson.JsonObject;

import darkgrey.rpg.identity.ResourceAddress;
import darkgrey.rpg.identity.ResourceAddressJson;
import darkgrey.rpg.identity.StoryUid;

/** Decodes only declared references into lossless runtime index keys. */
final class GraphResourceAddressCodec {

    private GraphResourceAddressCodec() {}

    static void decode(JsonObject fields, String type, CanonicalGraphResourceKind scope)
        throws CanonicalGraphResourceException {
        try {
            if (scope == CanonicalGraphResourceKind.STORY) {
                if ("session".equals(type)) address(fields, "resource_id", ResourceAddress.Kind.SESSION);
                if ("task".equals(type)) address(fields, "resource_id", ResourceAddress.Kind.TASK);
                if ("interact_actor".equals(type)) address(fields, "actor_id", ResourceAddress.Kind.ACTOR);
                if ("action".equals(type) && "give_item".equals(text(fields, "action_type"))) {
                    if (fields.has("item") || fields.has("metadata"))
                        throw new IOException("give_item requires a DGR Item address in item_id");
                    address(fields, "item_id", ResourceAddress.Kind.ITEM);
                }
                if ("enter_story".equals(type)) {
                    String uid = text(fields, "target_story_id");
                    if (uid != null && !uid.isEmpty() && !StoryUid.isValid(uid))
                        throw new IOException("Current target Story UID required");
                }
                if ("start".equals(type) && fields.has("triggers")
                    && fields.get("triggers")
                        .isJsonArray())
                    for (JsonElement element : fields.getAsJsonArray("triggers")) {
                        JsonObject trigger = element.getAsJsonObject();
                        if ("interact_actor".equals(text(trigger, "trigger_type"))) address(
                            trigger.getAsJsonObject("trigger_properties"),
                            "actor_id",
                            ResourceAddress.Kind.ACTOR);
                    }
            }
            if (scope == CanonicalGraphResourceKind.SESSION && "line".equals(type))
                address(fields, "speaker_actor_id", ResourceAddress.Kind.ACTOR);
            if (scope == CanonicalGraphResourceKind.TASK && "objective".equals(type)) {
                String objective = text(fields, "objective_type");
                if ("kill_entity".equals(objective)) address(fields, "entity", ResourceAddress.Kind.ACTOR);
                if ("interact_actor".equals(objective) || "submit_item".equals(objective))
                    address(fields, "actor_id", ResourceAddress.Kind.ACTOR);
                if ("collect_item".equals(objective) || "submit_item".equals(objective))
                    address(fields, "item", ResourceAddress.Kind.ITEM, ResourceAddress.Kind.ITEM_GROUP);
            }
            if (scope == CanonicalGraphResourceKind.TASK && "reward".equals(type)
                && fields.has("entries")
                && fields.get("entries")
                    .isJsonArray())
                for (JsonElement element : fields.getAsJsonArray("entries")) {
                    JsonObject entry = element.getAsJsonObject();
                    if ("item".equals(text(entry, "type"))) address(entry, "item", ResourceAddress.Kind.ITEM);
                }
        } catch (IOException | IllegalArgumentException | IllegalStateException exception) {
            throw new CanonicalGraphResourceException(
                "graph.reference.identity.invalid",
                "Invalid current resource reference",
                exception);
        }
    }

    private static String text(JsonObject fields, String name) {
        return !fields.has(name) || fields.get(name)
            .isJsonNull() ? null
                : fields.get(name)
                    .getAsString();
    }

    private static void address(JsonObject fields, String name, ResourceAddress.Kind... kinds) throws IOException {
        if (fields == null) throw new IOException("Missing reference fields");
        JsonElement value = fields.get(name);
        if (value == null || value.isJsonNull()
            || value.isJsonPrimitive() && value.getAsJsonPrimitive()
                .isString()
                && value.getAsString()
                    .isEmpty())
            return;
        ResourceAddress address = ResourceAddressJson.parse(value.toString());
        boolean matches = false;
        for (ResourceAddress.Kind kind : kinds) matches |= address.getKind() == kind;
        if (!matches) throw new IOException("Resource kind mismatch at " + name);
        fields.addProperty(name, address.toKey());
    }
}
