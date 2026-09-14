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
    public static final String GIVE_HEALTH = "give_health";
    public static final String TELEPORT = "teleport_player";
    public static final String GIVE_BUFF = "give_buff";
    public static final String EXECUTE_COMMAND = "execute_command";

    private final String type;
    private final String itemId;
    private final int metadata;
    private final int amount;
    private final String message;
    private final boolean legacyRegistryItem;
    private Map<String, JsonElement> extended = Collections.emptyMap();

    private CanonicalStoryActionConfiguration(String type, String itemId, int metadata, int amount, String message,
        boolean legacyRegistryItem) {
        this.type = type;
        this.itemId = itemId;
        this.metadata = metadata;
        this.amount = amount;
        this.message = message;
        this.legacyRegistryItem = legacyRegistryItem;
    }

    public static CanonicalStoryActionConfiguration parse(Map<String, JsonElement> properties) {
        if (properties == null) throw failure("story.action.properties", "Action properties are required.");
        String type = string(properties, "action_type");
        if (GIVE_ITEM.equals(type)) {
            int amount = integer(properties, "amount");
            if (properties.keySet()
                .equals(set("action_type", "item_id", "amount")))
                return new CanonicalStoryActionConfiguration(
                    type,
                    string(properties, "item_id"),
                    0,
                    amount,
                    null,
                    false);
            // Compatibility-only 0.3.0.0 payload. New Studio authoring never emits this shape.
            requireExactKeys(properties, set("action_type", "item", "metadata", "amount"));
            int metadata = integer(properties, "metadata");
            if (metadata < 0) throw failure("story.action.metadata", "Action metadata must be nonnegative.");
            return new CanonicalStoryActionConfiguration(
                type,
                string(properties, "item"),
                metadata,
                amount,
                null,
                true);
        }
        if (GIVE_XP.equals(type)) {
            requireExactKeys(properties, set("action_type", "amount"));
            int amount = integer(properties, "amount");
            return new CanonicalStoryActionConfiguration(type, null, 0, amount, null, false);
        }
        if (SEND_MESSAGE.equals(type)) {
            requireExactKeys(properties, set("action_type", "message"));
            return new CanonicalStoryActionConfiguration(type, null, 0, 0, string(properties, "message"), false);
        }
        if (GIVE_HEALTH.equals(type)) {
            requireExactKeys(properties, set("action_type", "amount"));
            finite(properties, "amount");
        } else if (TELEPORT.equals(type)) {
            requireExactKeys(properties, set("action_type", "dimension_id", "x", "y", "z"));
            integer(properties, "dimension_id");
            finite(properties, "x");
            finite(properties, "y");
            finite(properties, "z");
            if (Math.abs(finite(properties, "x")) > 29999984 || Math.abs(finite(properties, "z")) > 29999984
                || Math.abs(finite(properties, "y")) > 30000000)
                throw failure("story.action.teleport.bounds", "Teleport coordinates exceed Minecraft world bounds.");
        } else if (GIVE_BUFF.equals(type)) {
            JsonElement extension = properties.get("mod_extension");
            if (extension == null || !extension.isJsonPrimitive()
                || !extension.getAsJsonPrimitive()
                    .isBoolean())
                throw failure("story.action.buff", "mod_extension must be boolean.");
            if (extension.getAsBoolean()) {
                requireExactKeys(
                    properties,
                    set("action_type", "mod_extension", "mod_id", "buff_name", "duration_delta", "level_delta"));
                string(properties, "mod_id");
                string(properties, "buff_name");
            } else {
                requireExactKeys(
                    properties,
                    set("action_type", "mod_extension", "buff", "duration_delta", "level_delta"));
                String buff = string(properties, "buff");
                if (!VANILLA_BUFFS.contains(buff)) throw failure("story.action.buff", "Unknown Vanilla buff: " + buff);
            }
            integer(properties, "duration_delta");
            integer(properties, "level_delta");
        } else if (EXECUTE_COMMAND.equals(type)) {
            requireExactKeys(properties, set("action_type", "command"));
            String command = string(properties, "command");
            if (command.length() > 2048 || command.indexOf('\n') >= 0
                || command.indexOf('\r') >= 0
                || command.indexOf('\0') >= 0)
                throw failure("story.action.command", "Command must be one line of at most 2048 characters.");
        } else throw failure("story.action.type", "Unsupported canonical Story action type: " + type);
        CanonicalStoryActionConfiguration result = new CanonicalStoryActionConfiguration(type, null, 0, 0, null, false);
        result.extended = Collections.unmodifiableMap(new java.util.LinkedHashMap<String, JsonElement>(properties));
        return result;
    }

    public static final Set<String> VANILLA_BUFFS = Collections.unmodifiableSet(
        set(
            "speed",
            "slowness",
            "haste",
            "mining_fatigue",
            "strength",
            "instant_health",
            "instant_damage",
            "jump_boost",
            "nausea",
            "regeneration",
            "resistance",
            "fire_resistance",
            "water_breathing",
            "invisibility",
            "blindness",
            "night_vision",
            "hunger",
            "weakness",
            "poison",
            "wither",
            "health_boost",
            "absorption",
            "saturation"));

    public String getText(String key) {
        return string(extended, key);
    }

    public int getInteger(String key) {
        return integer(extended, key);
    }

    public double getNumber(String key) {
        return finite(extended, key);
    }

    public boolean isModBuff() {
        return extended.containsKey("mod_extension") && extended.get("mod_extension")
            .getAsBoolean();
    }

    private static double finite(Map<String, JsonElement> properties, String key) {
        JsonElement value = properties.get(key);
        if (value == null || !value.isJsonPrimitive()
            || !value.getAsJsonPrimitive()
                .isNumber()
            || Double.isNaN(value.getAsDouble())
            || Double.isInfinite(value.getAsDouble()))
            throw failure("story.action.property", "Finite numeric action property required: " + key);
        return value.getAsDouble();
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

    public boolean isLegacyRegistryItem() {
        return legacyRegistryItem;
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
