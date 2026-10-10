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
            if (factor < 1 || factor > 4) throw new IllegalArgumentException("GUI scale 1..4 only");
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
        String fixtureTitle = config.has("title") ? config.get("title")
            .getAsString() : "走到终点领取 7 XP";
        String fixtureObjective = config.has("objective") ? config.get("objective")
            .getAsString() : "到达 X=110，保持任务活动供库存验收";
        NBTTagList tasks = new NBTTagList(), notifications = new NBTTagList();
        for (int i = 0; i < count; i++) {
            NBTTagCompound task = new NBTTagCompound();
            string(task, "id", "hud-font-fixture-" + i);
            string(task, "tracking_id", "hud-font-fixture-" + i);
            string(task, "story", "ST-TEST-HUD-FONT");
            string(task, "story_title", "HUD 字体对照");
            string(task, "title", fixtureTitle);
            NBTTagList objectives = new NBTTagList();
            for (int j = 0; j < 4; j++) {
                NBTTagCompound row = new NBTTagCompound();
                string(row, "text", fixtureObjective);
                integer(row, "current", 0);
                integer(row, "required", 1);
                append(objectives, row);
                NBTTagCompound notice = new NBTTagCompound();
                string(notice, "event", "hud-font-" + i + "-" + j + "-" + System.nanoTime());
                string(notice, "task", "hud-font-fixture-" + i + ":0");
                string(notice, "kind", j == 0 ? "received" : "objective_active");
                string(notice, "objective", "objective-" + j);
                string(notice, "title", fixtureTitle);
                string(notice, "text", fixtureObjective);
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
        if (config.has("screen")) openScreen(
            (net.minecraft.client.Minecraft) mc,
            config.get("screen")
                .getAsString());
        expires = System.nanoTime() + 15000000000L;
        Files.write(
            new File(root, "hud-font-result.json").toPath(),
            ("{\"status\":\"VISUAL_FIXTURE_RENDERING\",\"server_mutated\":false,\"cards\":" + count
                + ",\"scale\":"
                + PlayerUiPreferences.textScale()
                + layoutMetrics(mc)
                + "}").getBytes(StandardCharsets.UTF_8));
    }

    private String layoutMetrics(Object mc) throws Exception {
        net.minecraft.client.Minecraft client = (net.minecraft.client.Minecraft) mc;
        net.minecraft.client.gui.ScaledResolution viewport = new net.minecraft.client.gui.ScaledResolution(
            client,
            (Integer) Stability0402Reflect.read(client, "displayWidth", "field_71443_c"),
            (Integer) Stability0402Reflect.read(client, "displayHeight", "field_71440_d"));
        darkgrey.rpg.client.gui.GuiWrappedChoiceButton choice = new darkgrey.rpg.client.gui.GuiWrappedChoiceButton(
            987,
            0,
            0,
            160,
            160,
            "较长的中文选项用于比较用户选择的字号和原有按钮高度，继续阅读完整说明，不增加新的按钮尺寸。",
            (net.minecraft.client.gui.FontRenderer) font);
        java.lang.reflect.Field choiceScale = choice.getClass()
            .getDeclaredField("scale");
        java.lang.reflect.Field choiceLines = choice.getClass()
            .getDeclaredField("lines");
        choiceScale.setAccessible(true);
        choiceLines.setAccessible(true);
        String geometry = "";
        Object currentScreen = Stability0402Reflect.read(client, "currentScreen", "field_71462_r");
        if (currentScreen != null) {
            geometry = ",\"screen_type\":\"" + currentScreen.getClass()
                .getSimpleName() + "\"";
            for (String name : new String[] { "geometry", "windowGeometry" }) {
                try {
                    java.lang.reflect.Field f = currentScreen.getClass()
                        .getDeclaredField(name);
                    f.setAccessible(true);
                    Object box = f.get(currentScreen);
                    for (String key : new String[] { "x", "y", "width", "height" }) {
                        geometry += ",\"window_" + key
                            + "\":"
                            + box.getClass()
                                .getField(key)
                                .getInt(box);
                    }
                    break;
                } catch (NoSuchFieldException ignored) {}
            }
        }
        return geometry + ",\"display_width\":"
            + Stability0402Reflect.read(client, "displayWidth", "field_71443_c")
            + ",\"display_height\":"
            + Stability0402Reflect.read(client, "displayHeight", "field_71440_d")
            + ",\"gui_factor\":"
            + Stability0402Reflect.call(viewport, "getScaleFactor", "func_78325_e")
            + ",\"scaled_width\":"
            + Stability0402Reflect.call(viewport, "getScaledWidth", "func_78326_a")
            + ",\"scaled_height\":"
            + Stability0402Reflect.call(viewport, "getScaledHeight", "func_78328_b")
            + ",\"choice_width\":"
            + Stability0402Reflect.read(choice, "width", "field_146120_f")
            + ",\"choice_height\":"
            + Stability0402Reflect.read(choice, "height", "field_146121_g")
            + ",\"choice_scale\":"
            + choiceScale.getDouble(choice)
            + ",\"choice_lines\":"
            + ((java.util.List<?>) choiceLines.get(choice)).size();
    }

    private void openScreen(net.minecraft.client.Minecraft client, String screen) throws Exception {
        if (screen.equals("hud")) display(client, null);
        else if (screen.equals("coverage")) {
            display(client, new net.minecraft.client.gui.GuiScreen() {

                @Override
                public void drawScreen(int x, int y, float partialTicks) {
                    try {
                        int screenWidth = (Integer) Stability0402Reflect.read(this, "width", "field_146294_l");
                        int screenHeight = (Integer) Stability0402Reflect.read(this, "height", "field_146295_m");
                        Stability0402Reflect
                            .method(
                                net.minecraft.client.gui.Gui.class,
                                "drawRect",
                                "func_73734_a",
                                int.class,
                                int.class,
                                int.class,
                                int.class,
                                int.class)
                            .invoke(null, 0, 0, screenWidth, screenHeight, 0xFFFFFFFF);
                        double[] scales = { 0.629, 0.74, 0.925, 1, 1.25, 1.5, 0.2125, 0.05 };
                        for (int row = 0; row < scales.length; row++) {
                            darkgrey.rpg.client.gui.DialogueFontDrawing.draw(
                                (net.minecraft.client.gui.FontRenderer) font,
                                "丨丨丨丨丨丨丨丨",
                                40,
                                12 + row * 30,
                                scales[row],
                                0xFF000000);
                            darkgrey.rpg.client.gui.DialogueFontDrawing.draw(
                                (net.minecraft.client.gui.FontRenderer) font,
                                "任务接受 新目标:与测试员再次对话,开启故事包B",
                                110,
                                12 + row * 30,
                                scales[row],
                                0xFF000000);
                        }
                        darkgrey.rpg.client.gui.DialogueFontDrawing.draw(
                            (net.minecraft.client.gui.FontRenderer) font,
                            "§c颜色§r §n下划线§r §m删除线§r",
                            40,
                            200,
                            1.25,
                            0xFF000000);
                        darkgrey.rpg.client.gui.DialogueFontDrawing
                            .draw((net.minecraft.client.gui.FontRenderer) font, "透明度", 250, 200, 1.25, 0x80000000);
                        darkgrey.rpg.client.gui.DialogueFontDrawing.draw(
                            (net.minecraft.client.gui.FontRenderer) font,
                            "§c红色 ABC§r",
                            110,
                            234,
                            0.74,
                            0xFF000000);
                    } catch (Exception error) {
                        throw new IllegalStateException(error);
                    }
                }

                public void func_73863_a(int x, int y, float partialTicks) {
                    drawScreen(x, y, partialTicks);
                }
            });
        } else if (screen.equals("choice")) {
            display(client, new net.minecraft.client.gui.GuiScreen() {

                private darkgrey.rpg.client.gui.GuiWrappedChoiceButton choice;

                @Override
                public void initGui() {
                    try {
                        int screenWidth = (Integer) Stability0402Reflect.read(this, "width", "field_146294_l");
                        choice = new darkgrey.rpg.client.gui.GuiWrappedChoiceButton(
                            987,
                            (screenWidth - 160) / 2,
                            35,
                            160,
                            160,
                            "较长的中文选项用于比较用户选择的字号和原有按钮高度，继续阅读完整说明，不增加新的按钮尺寸。",
                            (net.minecraft.client.gui.FontRenderer) font);
                    } catch (Exception error) {
                        throw new IllegalStateException(error);
                    }
                }

                // The separate driver is not reobfuscated; formal Forge invokes these SRG names.
                public void func_73866_w_() {
                    initGui();
                }

                @Override
                public void drawScreen(int x, int y, float partialTicks) {
                    try {
                        Stability0402Reflect.call(this, "drawDefaultBackground", "func_146276_q_");
                        Stability0402Reflect
                            .method(
                                choice.getClass(),
                                "drawButton",
                                "func_146112_a",
                                net.minecraft.client.Minecraft.class,
                                int.class,
                                int.class)
                            .invoke(choice, client, x, y);
                        int screenWidth = (Integer) Stability0402Reflect.read(this, "width", "field_146294_l");
                        int screenHeight = (Integer) Stability0402Reflect.read(this, "height", "field_146295_m");
                        darkgrey.rpg.client.gui.DgrUiText.tooltip(
                            (net.minecraft.client.gui.FontRenderer) font,
                            java.util.Arrays.asList("提示标题", "第二行提示内容", "第三行提示内容"),
                            screenWidth / 2,
                            screenHeight - 40,
                            screenWidth,
                            screenHeight);
                    } catch (Exception error) {
                        throw new IllegalStateException(error);
                    }
                }

                public void func_73863_a(int x, int y, float partialTicks) {
                    drawScreen(x, y, partialTicks);
                }
            });
        } else if (screen.equals("GuiStoryPackageManager")) {
            display(client, new darkgrey.rpg.client.gui.GuiStoryPackageManager(new NBTTagCompound()));
        } else if (java.util.Arrays.asList("GuiDialogueSettings", "GuiCanonicalTaskScreen", "GuiPlayerStateInspection")
            .contains(screen))
            display(
                client,
                (net.minecraft.client.gui.GuiScreen) Class.forName("darkgrey.rpg.client.gui." + screen)
                    .getDeclaredConstructor()
                    .newInstance());
        else throw new IllegalArgumentException("Unsupported isolated UI audit screen");
    }

    private void display(net.minecraft.client.Minecraft client, net.minecraft.client.gui.GuiScreen screen)
        throws Exception {
        Stability0402Reflect
            .method(client.getClass(), "displayGuiScreen", "func_147108_a", net.minecraft.client.gui.GuiScreen.class)
            .invoke(client, screen);
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
