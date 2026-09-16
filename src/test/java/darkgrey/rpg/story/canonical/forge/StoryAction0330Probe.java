package darkgrey.rpg.story.canonical.forge;

import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.Map;

import net.minecraft.potion.Potion;

import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

import cpw.mods.fml.common.ModContainer;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryActionConfiguration;

public final class StoryAction0330Probe {

    public static void main(String[] args) {
        for (String json : new String[] { "{\"action_type\":\"give_item\",\"item_id\":\"apple\",\"amount\":-2}",
            "{\"action_type\":\"give_xp\",\"amount\":0}", "{\"action_type\":\"give_health\",\"amount\":-2.5}",
            "{\"action_type\":\"teleport_player\",\"dimension_id\":-7,\"x\":1.25,\"y\":64,\"z\":-4.25}",
            "{\"action_type\":\"give_buff\",\"mod_extension\":false,\"buff\":\"speed\",\"duration_delta\":-5,\"level_delta\":0}",
            "{\"action_type\":\"give_buff\",\"mod_extension\":true,\"mod_id\":\"test\",\"buff_name\":\"potion.test\",\"duration_delta\":30,\"level_delta\":1}",
            "{\"action_type\":\"send_message\",\"message\":\"Hello\"}",
            "{\"action_type\":\"execute_command\",\"command\":\"/say Hello\"}" })
            CanonicalStoryActionConfiguration.parse(properties(json));
        for (String json : new String[] { "{\"action_type\":\"give_xp\",\"amount\":1.5}",
            "{\"action_type\":\"give_buff\",\"mod_extension\":false,\"buff\":\"unknown\",\"duration_delta\":1,\"level_delta\":1}",
            "{\"action_type\":\"give_buff\",\"mod_extension\":true,\"buff\":\"speed\",\"duration_delta\":1,\"level_delta\":1}",
            "{\"action_type\":\"execute_command\",\"command\":\"/say one\\nsay two\"}",
            "{\"action_type\":\"execute_command\",\"command\":\"say Hello\"}",
            "{\"action_type\":\"teleport_player\",\"dimension_id\":1.5,\"x\":0,\"y\":0,\"z\":0}",
            "{\"action_type\":\"teleport_player\",\"dimension_id\":0,\"x\":1e100,\"y\":0,\"z\":0}" }) {
            try {
                CanonicalStoryActionConfiguration.parse(properties(json));
                throw new AssertionError("Invalid action accepted: " + json);
            } catch (darkgrey.rpg.graph.canonical.CanonicalGraphResourceException expected) {}
        }
        require(CanonicalStoryInstantActions.healthAfter(10, -99, 20) == 0, "Health lower clamp");
        require(CanonicalStoryInstantActions.healthAfter(10, 99, 20) == 20, "Health upper clamp");
        require(CanonicalStoryInstantActions.healthAfter(10, 0, 20) == 10, "Health zero");
        int[] delta = CanonicalBuffCatalog.delta(400, 2, -5, -1);
        require(delta[0] == 300 && delta[1] == 1, "BUFF seconds and 1-based delta");
        require(CanonicalBuffCatalog.delta(400, 2, -20, 0)[0] == 0, "Duration removal");
        require(CanonicalBuffCatalog.delta(400, 2, 0, -2)[0] == 0, "Level removal");
        require(CanonicalBuffCatalog.delta(0, 0, 30, 1)[0] == 600, "Absent buff addition");
        try {
            CanonicalBuffCatalog.delta(20, 128, 0, 1);
            throw new AssertionError("Unpersistable buff accepted");
        } catch (IllegalArgumentException expected) {}
        require(
            CanonicalStoryInstantActions.commandContext(null)
                .canCommandSenderUseCommand(2, "give"),
            "Command permission 2 denied");
        require(
            !CanonicalStoryInstantActions.commandContext(null)
                .canCommandSenderUseCommand(3, "op"),
            "Command privilege escalation");
        catalog();
        System.out.println("STORY_ACTION_0330_SCHEMA_SIGNED_HEALTH_BUFF_COMMAND=PASS");
    }

    private static void catalog() {
        Potion old24 = Potion.potionTypes[24], old25 = Potion.potionTypes[25];
        try {
            Potion first = new TestPotion(24).setPotionName("potion.duplicate");
            Potion second = new TestPotion(25).setPotionName("potion.duplicate");
            ModContainer owner = (ModContainer) java.lang.reflect.Proxy.newProxyInstance(
                ModContainer.class.getClassLoader(),
                new Class<?>[] { ModContainer.class },
                (proxy, method, values) -> "getModId".equals(method.getName()) ? "testmod"
                    : "getOwnedPackages".equals(method.getName())
                        ? Collections.singletonList("darkgrey.rpg.story.canonical.forge")
                        : null);
            CanonicalBuffCatalog catalog = CanonicalBuffCatalog
                .build(new Potion[] { Potion.moveSpeed, first, second }, Collections.singletonList(owner));
            require(catalog.vanilla("speed") == Potion.moveSpeed, "Vanilla lookup");
            require(
                catalog.entries()
                    .size() == 3,
                "Export inventory");
            try {
                catalog.modded("testmod", "potion.duplicate");
                throw new AssertionError("Duplicate chose first");
            } catch (IllegalArgumentException expected) {}
            CanonicalBuffCatalog unique = CanonicalBuffCatalog
                .build(new Potion[] { first }, Collections.singletonList(owner));
            require(unique.modded("testmod", "potion.duplicate") == first, "Mod ownership lookup");
            try {
                unique.modded("wrong", "potion.duplicate");
                throw new AssertionError("Cross-mod match");
            } catch (IllegalArgumentException expected) {}
            System.out.println("STORY_ACTION_0330_BUFF_OWNER_DUPLICATE_AND_LOOKUP=PASS");
        } finally {
            Potion.potionTypes[24] = old24;
            Potion.potionTypes[25] = old25;
        }
    }

    private static final class TestPotion extends Potion {

        TestPotion(int id) {
            super(id, false, 0);
        }
    }

    private static Map<String, JsonElement> properties(String json) {
        JsonObject object = new JsonParser().parse(json)
            .getAsJsonObject();
        Map<String, JsonElement> result = new LinkedHashMap<String, JsonElement>();
        for (Map.Entry<String, JsonElement> field : object.entrySet()) result.put(field.getKey(), field.getValue());
        return result;
    }

    private static void require(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }
}
