package darkgrey.rpg.story.canonical.runtime;

import java.util.LinkedHashMap;
import java.util.Map;

import com.google.gson.JsonElement;
import com.google.gson.JsonParser;

import darkgrey.rpg.graph.canonical.CanonicalGraphResourceException;

/** Pure proof for the exact Stage 5 Story action payload contract shared with Studio. */
public final class CanonicalStoryActionConfigurationProbe {

    private CanonicalStoryActionConfigurationProbe() {}

    public static void main(String[] args) {
        parsesEnabledActions();
        rejectsSchemaDriftAndInvalidValues();
        System.out.println("CANONICAL_STORY_ACTION_CONFIGURATION=PASS");
        System.out.println("CANONICAL_STORY_ACTION_STRICT_REJECTION=PASS");
    }

    private static void parsesEnabledActions() {
        CanonicalStoryActionConfiguration item = CanonicalStoryActionConfiguration
            .parse(properties("action_type", "\"give_item\"", "item_id", "\"reward_coin\"", "amount", "10"));
        check(CanonicalStoryActionConfiguration.GIVE_ITEM.equals(item.getType()), "give_item type changed");
        check("reward_coin".equals(item.getItemId()), "give_item DGR Item ID changed");
        check(!item.isLegacyRegistryItem() && item.getAmount() == 10, "give_item DGR payload changed");

        CanonicalStoryActionConfiguration legacyItem = CanonicalStoryActionConfiguration.parse(
            properties(
                "action_type",
                "\"give_item\"",
                "item",
                "\"minecraft:gold_nugget\"",
                "metadata",
                "0",
                "amount",
                "1"));
        check(legacyItem.isLegacyRegistryItem(), "0.3.0 item compatibility was not retained");

        CanonicalStoryActionConfiguration xp = CanonicalStoryActionConfiguration
            .parse(properties("action_type", "\"give_xp\"", "amount", "25"));
        check(
            CanonicalStoryActionConfiguration.GIVE_XP.equals(xp.getType()) && xp.getAmount() == 25,
            "give_xp payload changed");

        CanonicalStoryActionConfiguration message = CanonicalStoryActionConfiguration
            .parse(properties("action_type", "\"send_message\"", "message", "\"任务完成\""));
        check(
            CanonicalStoryActionConfiguration.SEND_MESSAGE.equals(message.getType())
                && "任务完成".equals(message.getMessage()),
            "send_message payload changed");
    }

    private static void rejectsSchemaDriftAndInvalidValues() {
        expect("story.action.properties", null);
        expect("story.action.type", properties("action_type", "\"teleport\""));
        expect("story.action.schema", properties("action_type", "\"give_xp\"", "amount", "10", "unknown", "true"));
        expect(
            "story.action.schema",
            properties("action_type", "\"give_item\"", "item_id", "\"stone\"", "amount", "1", "unknown", "true"));
        expect("story.action.property", properties("action_type", "\"give_item\"", "item_id", "\" \"", "amount", "1"));
        expect("story.action.property", properties("action_type", "\"give_xp\"", "amount", "1.5"));
        expect(
            "story.action.metadata",
            properties("action_type", "\"give_item\"", "item", "\"stone\"", "metadata", "-1", "amount", "1"));
        check(
            CanonicalStoryActionConfiguration.parse(properties("action_type", "\"give_xp\"", "amount", "0"))
                .getAmount() == 0,
            "Zero XP must be a no-op");
        expect("story.action.property", properties("action_type", "\"send_message\"", "message", "\"\t\""));
    }

    private static void expect(String code, Map<String, JsonElement> properties) {
        try {
            CanonicalStoryActionConfiguration.parse(properties);
            throw new AssertionError("Expected action failure " + code);
        } catch (CanonicalGraphResourceException expected) {
            check(code.equals(expected.getCode()), "Expected " + code + " but got " + expected.getCode());
        }
    }

    private static Map<String, JsonElement> properties(String... values) {
        Map<String, JsonElement> result = new LinkedHashMap<String, JsonElement>();
        for (int index = 0; index < values.length; index += 2)
            result.put(values[index], new JsonParser().parse(values[index + 1]));
        return result;
    }

    private static void check(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
