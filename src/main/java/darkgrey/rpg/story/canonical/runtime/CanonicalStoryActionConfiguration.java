package darkgrey.rpg.story.canonical.runtime;

import java.util.Collections;
import java.util.HashSet;
import java.util.Map;
import java.util.Set;

import com.google.gson.JsonElement;

import darkgrey.rpg.graph.canonical.CanonicalGraphResourceException;

/** Strict persisted contract for the enabled Stage 5 Story action subset. */
public final class CanonicalStoryActionConfiguration {

    public static final String GIVE_ITEM = "give_item";
    public static final String GIVE_XP = "give_xp";
    public static final String SEND_MESSAGE = "send_message";

    private final String type;
    private final String itemId;
    private final int metadata;
    private final int amount;
    private final String message;

    private CanonicalStoryActionConfiguration(String type, String itemId, int metadata, int amount, String message) {
        this.type = type;
        this.itemId = itemId;
        this.metadata = metadata;
        this.amount = amount;
        this.message = message;
    }

    public static CanonicalStoryActionConfiguration parse(Map<String, JsonElement> properties) {
        if (properties == null) throw failure("story.action.properties", "Action properties are required.");
        String type = string(properties, "action_type");
        if (GIVE_ITEM.equals(type)) {
            requireExactKeys(properties, set("action_type", "item", "metadata", "amount"));
            int metadata = integer(properties, "metadata");
            int amount = integer(properties, "amount");
            if (metadata < 0) throw failure("story.action.metadata", "Action metadata must be nonnegative.");
            if (amount <= 0) throw failure("story.action.amount", "Action amount must be positive.");
            return new CanonicalStoryActionConfiguration(type, string(properties, "item"), metadata, amount, null);
        }
        if (GIVE_XP.equals(type)) {
            requireExactKeys(properties, set("action_type", "amount"));
            int amount = integer(properties, "amount");
            if (amount <= 0) throw failure("story.action.amount", "Action amount must be positive.");
            return new CanonicalStoryActionConfiguration(type, null, 0, amount, null);
        }
        if (SEND_MESSAGE.equals(type)) {
            requireExactKeys(properties, set("action_type", "message"));
            return new CanonicalStoryActionConfiguration(type, null, 0, 0, string(properties, "message"));
        }
        throw failure("story.action.type", "Unsupported canonical Story action type: " + type);
    }

    public String getType() {
        return type;
    }

    public String getItemId() {
        return itemId;
    }

    public int getMetadata() {
        return metadata;
    }

    public int getAmount() {
        return amount;
    }

    public String getMessage() {
        return message;
    }

    private static String string(Map<String, JsonElement> properties, String key) {
        JsonElement value = properties.get(key);
        if (value == null || !value.isJsonPrimitive()
            || !value.getAsJsonPrimitive()
                .isString()
            || value.getAsString()
                .trim()
                .isEmpty())
            throw failure("story.action.property", "Nonblank string action property is required: " + key);
        return value.getAsString();
    }

    private static int integer(Map<String, JsonElement> properties, String key) {
        JsonElement value = properties.get(key);
        if (value == null || !value.isJsonPrimitive()
            || !value.getAsJsonPrimitive()
                .isNumber())
            throw failure("story.action.property", "Integer action property is required: " + key);
        double number = value.getAsDouble();
        int integer = value.getAsInt();
        if (Double.isNaN(number) || Double.isInfinite(number) || number != integer)
            throw failure("story.action.property", "Integer action property is required: " + key);
        return integer;
    }

    private static void requireExactKeys(Map<String, JsonElement> properties, Set<String> expected) {
        if (!properties.keySet()
            .equals(expected))
            throw failure("story.action.schema", "Action properties contain unknown or missing fields.");
    }

    private static Set<String> set(String... values) {
        Set<String> result = new HashSet<String>();
        Collections.addAll(result, values);
        return result;
    }

    private static CanonicalGraphResourceException failure(String code, String message) {
        return new CanonicalGraphResourceException(code, message);
    }
}
