package darkgrey.rpg.persistence;

import java.io.File;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;

import net.minecraft.nbt.NBTBase;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

import com.google.gson.JsonObject;
import com.google.gson.JsonParser;

import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import cpw.mods.fml.common.gameevent.TickEvent;
import darkgrey.rpg.client.CanonicalTaskClientStore;
import darkgrey.rpg.client.TaskNotificationCards;
import darkgrey.rpg.client.TaskTrackerClient;
import darkgrey.rpg.client.session.PlayerUiPreferences;

/** Opt-in, local presentation fixture. No server commands, requests or business mutations. */
public final class StabilityClient0402HudFixture {

    private long expires;
    private double previousScale;
    private Object settings, font;
    private int previousGuiScale;
    private boolean previousUnicode;

    @SubscribeEvent
    public void tick(TickEvent.ClientTickEvent event) throws Exception {
        if (event.phase != TickEvent.Phase.END || !Boolean.getBoolean("dgr0402.hudFixture")) return;
        File root = new File(System.getProperty("dgr0402.clientRoot")).getCanonicalFile();
        if (!root.equals(new File(".").getCanonicalFile()) || !root.getPath()
            .contains("\\.tooling\\0402\\Worlds\\")) throw new IllegalStateException("Owned isolated client only");
        Object mc = Stability0402Reflect
            .method(Class.forName("net.minecraft.client.Minecraft"), "getMinecraft", "func_71410_x")
            .invoke(null);
        Object world = Stability0402Reflect.read(mc, "theWorld", "field_71441_e");
        if (expires != 0 && System.nanoTime() >= expires) {
            clear();
        }
        File request = new File(root, "hud-font-request.json");
        if (!request.isFile() || world == null) return;
        JsonObject config = new JsonParser()
            .parse(new String(Files.readAllBytes(request.toPath()), StandardCharsets.UTF_8))
            .getAsJsonObject();
        Files.delete(request.toPath());
        if (expires != 0) clear();
        previousScale = PlayerUiPreferences.textScale();
        settings = Stability0402Reflect.read(mc, "gameSettings", "field_71474_y");
        font = Stability0402Reflect.read(mc, "fontRenderer", "field_71466_p");
        previousGuiScale = (Integer) Stability0402Reflect.read(settings, "guiScale", "field_74335_Z");
        previousUnicode = (Boolean) Stability0402Reflect.call(font, "getUnicodeFlag", "func_78260_a");
        if (config.has("gui_scale")) {
            int factor = config.get("gui_scale")
                .getAsInt();
            if (factor < 1 || factor > 3) throw new IllegalArgumentException("GUI scale 1..3 only");
            guiScaleField().setInt(settings, factor);
        }
        if (config.has("unicode"))
            Stability0402Reflect.method(font.getClass(), "setUnicodeFlag", "func_78264_a", boolean.class)
                .invoke(
                    font,
                    config.get("unicode")
                        .getAsBoolean());
        PlayerUiPreferences.setTextScale(
            config.has("scale") ? config.get("scale")
                .getAsDouble() : 1);
        int count = config.has("cards") ? config.get("cards")
            .getAsInt() : 1;
        if (count < 1 || count > 4) throw new IllegalArgumentException("One to four visual cards");
        NBTTagList tasks = new NBTTagList(), notifications = new NBTTagList();
        for (int i = 0; i < count; i++) {
            NBTTagCompound task = new NBTTagCompound();
            string(task, "id", "hud-font-fixture-" + i);
            string(task, "tracking_id", "hud-font-fixture-" + i);
            string(task, "story", "ST-TEST-HUD-FONT");
            string(task, "story_title", "HUD 字体对照");
            string(task, "title", "走到终点领取 7 XP");
            NBTTagList objectives = new NBTTagList();
            for (int j = 0; j < 4; j++) {
                NBTTagCompound row = new NBTTagCompound();
                string(row, "text", "到达 X=110，保持任务活动供库存验收");
                integer(row, "current", 0);
                integer(row, "required", 1);
                append(objectives, row);
                NBTTagCompound notice = new NBTTagCompound();
                string(notice, "event", "hud-font-" + i + "-" + j + "-" + System.nanoTime());
                string(notice, "task", "hud-font-fixture-" + i + ":0");
                string(notice, "kind", j == 0 ? "received" : "objective_active");
                string(notice, "objective", "objective-" + j);
                string(notice, "title", "走到终点领取 7 XP");
                string(notice, "text", "到达 X=110，保持任务活动供库存验收");
                append(notifications, notice);
            }
            tag(task, "objectives", objectives);
            append(tasks, task);
        }
        NBTTagCompound snapshot = new NBTTagCompound();
        tag(snapshot, "tasks", tasks);
        tag(snapshot, "notifications", notifications);
        Stability0402Reflect.method(NBTTagCompound.class, "setLong", "func_74772_a", String.class, long.class)
            .invoke(snapshot, "revision", Long.MAX_VALUE - 1);
        CanonicalTaskClientStore.synchronizeWorld(null);
        CanonicalTaskClientStore.synchronizeWorld(world);
        if (!CanonicalTaskClientStore.replace(snapshot)) throw new AssertionError("Valid detached HUD projection");
        TaskTrackerClient.accept(snapshot, false);
        TaskNotificationCards.clear();
        TaskNotificationCards.accept(notifications, System.nanoTime());
        expires = System.nanoTime() + 15000000000L;
        Files.write(
            new File(root, "hud-font-result.json").toPath(),
            ("{\"status\":\"VISUAL_FIXTURE_RENDERING\",\"server_mutated\":false,\"cards\":" + count
                + ",\"scale\":"
                + PlayerUiPreferences.textScale()
                + "}").getBytes(StandardCharsets.UTF_8));
    }

    private void clear() throws Exception {
        TaskNotificationCards.clear();
        CanonicalTaskClientStore.synchronizeWorld(null);
        PlayerUiPreferences.setTextScale(previousScale);
        guiScaleField().setInt(settings, previousGuiScale);
        Stability0402Reflect.method(font.getClass(), "setUnicodeFlag", "func_78264_a", boolean.class)
            .invoke(font, previousUnicode);
        expires = 0;
    }

    private java.lang.reflect.Field guiScaleField() throws Exception {
        try {
            return settings.getClass()
                .getField("guiScale");
        } catch (NoSuchFieldException formal) {
            return settings.getClass()
                .getField("field_74335_Z");
        }
    }

    private static void string(NBTTagCompound data, String key, String value) throws Exception {
        Stability0402Reflect.method(NBTTagCompound.class, "setString", "func_74778_a", String.class, String.class)
            .invoke(data, key, value);
    }

    private static void integer(NBTTagCompound data, String key, int value) throws Exception {
        Stability0402Reflect.method(NBTTagCompound.class, "setInteger", "func_74768_a", String.class, int.class)
            .invoke(data, key, value);
    }

    private static void tag(NBTTagCompound data, String key, NBTBase value) throws Exception {
        Stability0402Reflect.method(NBTTagCompound.class, "setTag", "func_74782_a", String.class, NBTBase.class)
            .invoke(data, key, value);
    }

    private static void append(NBTTagList data, NBTBase value) throws Exception {
        Stability0402Reflect.method(NBTTagList.class, "appendTag", "func_74742_a", NBTBase.class)
            .invoke(data, value);
    }
}
