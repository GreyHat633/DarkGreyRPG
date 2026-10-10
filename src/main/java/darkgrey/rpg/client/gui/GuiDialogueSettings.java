package darkgrey.rpg.client.gui;

import net.minecraft.client.gui.GuiScreen;

import org.lwjgl.input.Keyboard;

import darkgrey.rpg.client.ClientQuestKeyHandler;
import darkgrey.rpg.client.session.DialoguePreferences;
import darkgrey.rpg.client.session.PlayerUiPreferences;

/** Local, non-pausing presentation preferences, using the shared utility-window chrome. */
public final class GuiDialogueSettings extends GuiScreen {

    private final UtilityWindowGeometry geometry = new UtilityWindowGeometry(260, 260, 380, 340);
    private boolean initialized;
    private boolean adjusting;
    private int tab;
    private final SmoothScroll scroll = new SmoothScroll();
    private long scrollUpdated = System.nanoTime();
    private static final String[] TABS = { "外观", "文本", "声音", "任务" };
    private int activeSlider;
    private long previewUpdated = System.nanoTime();
    private double previewCharacters;
    private double previewHold;
    private static final String PREVIEW = "这是一段阅读预览，调整设置不会推进剧情。";

    public static void loadPreferences() {
        UtilityWindowChrome.settings()
            .loadPlayerPreferences();
        DgrUiPalette.apply();
    }

    @Override
    public void initGui() {
        UtilityWindowChrome.open("settings", geometry, width, height, initialized);
        initialized = true;
    }

    @Override
    public void drawScreen(int mouseX, int mouseY, float partialTicks) {
        drawDefaultBackground();
        int x = geometry.x, y = geometry.y, w = geometry.width, h = geometry.height;
        drawRect(x, y, x + w, y + h, DgrUiPalette.WINDOW_PANEL);
        drawRect(x + 8, y + 32, x + w - 8, y + h - 14, DgrUiPalette.WINDOW_CONTENT);
        drawRect(x, y, x + w, y + 1, DgrUiPalette.SECONDARY);
        drawRect(x, y + h - 1, x + w, y + h, DgrUiPalette.SECONDARY);
        drawRect(x, y, x + 1, y + h, DgrUiPalette.SECONDARY);
        drawRect(x + w - 1, y, x + w, y + h, DgrUiPalette.SECONDARY);
        darkgrey.rpg.client.gui.DgrUiText
            .centered(fontRendererObj, "设置", x + w / 2, y + 10, DgrUiPalette.SELECTED_BORDER);
        for (int i = 0; i < TABS.length; i++) {
            int tabWidth = (w - 32) / TABS.length;
            int tx = x + 16 + i * tabWidth;
            drawRect(tx, y + 30, tx + tabWidth, y + 51, tab == i ? DgrUiPalette.HOVER : DgrUiPalette.SUB_PANEL);
            DgrUiText.left(
                fontRendererObj,
                TABS[i],
                tx + (tabWidth - fontRendererObj.getStringWidth(TABS[i])) / 2,
                y + 36,
                DgrUiPalette.TEXT);
        }
        scroll.bounds(maxScroll());
        long scrollNow = System.nanoTime();
        scroll.advance((scrollNow - scrollUpdated) / 1000000000.0);
        scrollUpdated = scrollNow;
        net.minecraft.client.gui.ScaledResolution resolution = new net.minecraft.client.gui.ScaledResolution(
            mc,
            mc.displayWidth,
            mc.displayHeight);
        int factor = resolution.getScaleFactor();
        org.lwjgl.opengl.GL11.glPushAttrib(org.lwjgl.opengl.GL11.GL_SCISSOR_BIT);
        org.lwjgl.opengl.GL11.glEnable(org.lwjgl.opengl.GL11.GL_SCISSOR_TEST);
        org.lwjgl.opengl.GL11.glScissor(
            (x + 8) * factor,
            mc.displayHeight - (y + h - 34) * factor,
            (w - 16) * factor,
            Math.max(0, h - 88) * factor);
        org.lwjgl.opengl.GL11.glPushMatrix();
        org.lwjgl.opengl.GL11.glTranslatef(0, -scroll.pixelOffset(), 0);
        if (tab == 0) {
            DgrUiText.left(fontRendererObj, "主题", x + 20, y + 54, DgrUiPalette.SECONDARY);
            String[] themes = { "灰黑", "浅白", "蔚蓝" };
            drawChoices(
                themes,
                PlayerUiPreferences.theme()
                    .ordinal(),
                y + 64);
        } else if (tab == 2) {
            drawSlider(0, "语音", PlayerUiPreferences.voiceVolume());
            drawSlider(1, "BGM", PlayerUiPreferences.musicVolume());
        } else if (tab == 3) {
            DgrUiText.left(fontRendererObj, "任务追踪", x + 20, y + 60, DgrUiPalette.SECONDARY);
            drawRect(x + 20, y + 80, x + 29, y + 89, DgrUiPalette.BORDER);
            drawRect(x + 21, y + 81, x + 28, y + 88, DgrUiPalette.WINDOW_CONTENT);
            if (PlayerUiPreferences.trackNewTasks()) {
                drawRect(x + 22, y + 84, x + 23, y + 85, DgrUiPalette.TEXT);
                drawRect(x + 23, y + 85, x + 24, y + 86, DgrUiPalette.TEXT);
                drawRect(x + 24, y + 84, x + 25, y + 85, DgrUiPalette.TEXT);
                drawRect(x + 25, y + 83, x + 26, y + 84, DgrUiPalette.TEXT);
                drawRect(x + 26, y + 82, x + 27, y + 83, DgrUiPalette.TEXT);
            }
            DgrUiText.left(fontRendererObj, "任务追踪默认开启", x + 35, y + 80, DgrUiPalette.TEXT);
            DgrUiText.left(fontRendererObj, "任务追踪显示位置", x + 20, y + 103, DgrUiPalette.SECONDARY);
            drawChoices(
                new String[] { "左侧", "右侧" },
                PlayerUiPreferences.trackerSide()
                    .ordinal(),
                y + 116);
            DgrUiText.left(fontRendererObj, "任务弹窗显示位置", x + 20, y + 158, DgrUiPalette.SECONDARY);
            drawChoices(
                new String[] { "左侧", "右侧" },
                PlayerUiPreferences.notificationSide()
                    .ordinal(),
                y + 171);
        } else {
            drawSlider(0, "对话框背景不透明度", PlayerUiPreferences.opacity());
            DgrUiText.left(fontRendererObj, "文字大小", x + 20, y + 91, DgrUiPalette.SECONDARY);
            drawChoices(
                new String[] { "100%", "125%", "150%" },
                PlayerUiPreferences.textScale() == 1 ? 0 : PlayerUiPreferences.textScale() == 1.25 ? 1 : 2,
                y + textScaleChoicesTop());
            String actualScale = DialogueFontDrawing.scaleLabel();
            if (!actualScale.isEmpty()) DgrUiText
                .left(fontRendererObj, actualScale, x + 20, y + textScaleChoicesTop() + 30, DgrUiPalette.SECONDARY);
            String label = DialoguePreferences.speed() == 0 ? "立即显示" : (int) DialoguePreferences.speed() + " 字/秒";
            DgrUiText.left(
                fontRendererObj,
                label,
                x + w - 20 - fontRendererObj.getStringWidth(label),
                y + 150,
                DgrUiPalette.TEXT);
            drawSlider(2, "台词显示速度", DialoguePreferences.sliderPosition() / 120);
            drawSlider(
                3,
                "自动播放等待  " + PlayerUiPreferences.autoWaitSeconds() + " 秒",
                (PlayerUiPreferences.autoWaitSeconds() - 1) / 9);
        }
        if (tab < 2) drawPreview();
        org.lwjgl.opengl.GL11.glPopMatrix();
        org.lwjgl.opengl.GL11.glPopAttrib();
        if (maxScroll() > 0) {
            int track = Math.max(1, h - 94);
            int knob = y + 55 + (int) ((track - 16) * scroll.position() / maxScroll());
            drawRect(x + w - 12, knob, x + w - 10, knob + 16, DgrUiPalette.SECONDARY);
        }
        drawRect(x + 16, y + h - 32, x + 116, y + h - 12, DgrUiPalette.SUB_PANEL);
        DgrUiText.left(fontRendererObj, "恢复默认", x + 26, y + h - 26, DgrUiPalette.TEXT);
        UtilityWindowChrome.drawGrip(geometry);
        super.drawScreen(mouseX, mouseY, partialTicks);
    }

    private int maxScroll() {
        int bottom = tab == 1 ? 296 : tab == 0 ? 152 : tab == 3 ? 205 : 126;
        return Math.max(0, bottom - (geometry.height - 34));
    }

    @Override
    public void handleMouseInput() {
        super.handleMouseInput();
        int wheel = org.lwjgl.input.Mouse.getEventDWheel();
        int x = org.lwjgl.input.Mouse.getEventX() * width / mc.displayWidth;
        int y = height - org.lwjgl.input.Mouse.getEventY() * height / mc.displayHeight - 1;
        if (wheel != 0 && x >= geometry.x + 8
            && x < geometry.x + geometry.width - 8
            && y >= geometry.y + 54
            && y < geometry.y + geometry.height - 34) scroll.wheel(wheel, 24);
    }

    private void adjust(int x) {
        double value = Math.max(0, Math.min(1, (x - geometry.x - 20.0) / (geometry.width - 40)));
        if (tab == 1 && activeSlider == 0) PlayerUiPreferences.setOpacity(value);
        else if (tab == 1 && activeSlider == 2) DialoguePreferences.setSliderPosition(value * 120);
        else if (tab == 1) PlayerUiPreferences.setAutoWaitSeconds(1 + value * 9);
        else if (activeSlider == 0) PlayerUiPreferences.setVoiceVolume(value);
        else if (activeSlider == 1) PlayerUiPreferences.setMusicVolume(value);
        // A continuous drag changes the preview rate, not its start time.
    }

    private void drawPreview() {
        int x = geometry.x + 16, top = geometry.y + (tab == 0 ? 100 : 222);
        int bottom = geometry.y + Math.max(geometry.height - 34, tab == 1 ? 296 : 152);
        if (bottom - top < 22) return;
        drawRect(x, top, geometry.x + geometry.width - 16, bottom, DgrUiPalette.dialoguePanel());
        double scale = DialogueFontDrawing.scale();
        double speed = DialoguePreferences.speed();
        long now = System.nanoTime();
        double elapsed = Math.min(0.25, (now - previewUpdated) / 1000000000.0);
        previewUpdated = now;
        if (speed == 0) previewCharacters = PREVIEW.length();
        else if (previewCharacters < PREVIEW.length())
            previewCharacters = Math.min(PREVIEW.length(), previewCharacters + elapsed * speed);
        else {
            previewHold += elapsed;
            if (!adjusting && previewHold >= 2) {
                previewCharacters = 0;
                previewHold = 0;
            }
        }
        String shown = PREVIEW.substring(0, Math.min(PREVIEW.length(), (int) previewCharacters));
        int lineHeight = (int) Math.ceil(fontRendererObj.FONT_HEIGHT * scale) + 2;
        int yy = top + 6;
        for (String line : (java.util.List<String>) fontRendererObj
            .listFormattedStringToWidth(shown, Math.max(1, (int) ((geometry.width - 48) / scale)))) {
            if (yy + lineHeight > bottom) break;
            CanonicalDialogueRenderer.drawText(fontRendererObj, line, x + 8, yy, scale, DgrUiPalette.TEXT);
            yy += lineHeight;
        }
    }

    private int sliderTop(int index) {
        return 64 + index * 36 + (tab == 1 && index >= 2 ? 14 : 0);
    }

    private int textScaleChoicesTop() {
        return 91 + fontRendererObj.FONT_HEIGHT + 4;
    }

    private void drawSlider(int index, String label, double value) {
        int x = geometry.x + 20, y = geometry.y + sliderTop(index);
        DgrUiText.left(
            fontRendererObj,
            label + (tab == 1 && index != 0 ? "" : "  " + Math.round(value * 100) + "%"),
            x,
            y,
            DgrUiPalette.TEXT);
        int end = geometry.x + geometry.width - 20;
        int knob = x + (int) Math.round((end - x) * value);
        drawRect(x, y + 18, end, y + 20, DgrUiPalette.BORDER);
        drawRect(knob - 3, y + 14, knob + 4, y + 24, DgrUiPalette.SELECTED_BORDER);
    }

    private void drawChoices(String[] labels, int selected, int y) {
        int cell = (geometry.width - 40) / labels.length;
        for (int i = 0; i < labels.length; i++) {
            int x = geometry.x + 20 + i * cell;
            drawRect(x, y, x + cell - 4, y + 24, i == selected ? DgrUiPalette.SELECTED_BORDER : DgrUiPalette.BORDER);
            drawRect(x + 1, y + 1, x + cell - 5, y + 23, i == selected ? DgrUiPalette.HOVER : DgrUiPalette.SUB_PANEL);
            DgrUiText.left(
                fontRendererObj,
                labels[i],
                x + (cell - 4 - fontRendererObj.getStringWidth(labels[i])) / 2,
                y + 8,
                DgrUiPalette.TEXT);
        }
    }

    @Override
    protected void mouseClicked(int x, int y, int button) {
        if (button >= 0 && (button - 100 == ClientQuestKeyHandler.settingsKeyCode()
            || button - 100 == mc.gameSettings.keyBindInventory.getKeyCode())) {
            close(button - 100);
            return;
        }
        if (geometry.begin(x, y, button)) return;
        if (button == 0 && x >= geometry.x + 16 && x < geometry.x + geometry.width - 16) {
            int localY = y - geometry.y;
            if (localY >= 30 && localY < 51) {
                tab = Math.min(TABS.length - 1, (x - geometry.x - 16) / ((geometry.width - 32) / TABS.length));
                scroll.jump(0);
                adjusting = false;
                return;
            }
            if (localY >= geometry.height - 32 && localY < geometry.height - 12 && x < geometry.x + 116) {
                PlayerUiPreferences.reset();
                DgrUiPalette.apply();
                save();
                return;
            }
            if (localY < 54 || localY >= geometry.height - 34) return;
            localY += scroll.pixelOffset();
            if (tab == 3) {
                if (localY >= 76 && localY < 96)
                    PlayerUiPreferences.setTrackNewTasks(!PlayerUiPreferences.trackNewTasks());
                else if (localY >= 116 && localY < 140) PlayerUiPreferences.setTrackerSide(sideAt(x));
                else if (localY >= 171 && localY < 195) PlayerUiPreferences.setNotificationSide(sideAt(x));
                save();
                return;
            }
            if (tab == 0 && localY >= 64 && localY < 88) {
                PlayerUiPreferences.setTheme(
                    PlayerUiPreferences.Theme.values()[Math
                        .min(2, Math.max(0, (x - geometry.x - 20) / ((geometry.width - 40) / 3)))]);
                DgrUiPalette.apply();
                save();
                return;
            }
            if (tab == 1 && localY >= textScaleChoicesTop() && localY < textScaleChoicesTop() + 24) {
                PlayerUiPreferences.setTextScale(
                    1 + 0.25 * Math.min(2, Math.max(0, (x - geometry.x - 20) / ((geometry.width - 40) / 3))));
                save();
                return;
            }
            int slider = Math.min(3, Math.max(0, (localY - 64 - (tab == 1 && localY >= 136 ? 14 : 0)) / 36));
            int sliderY = sliderTop(slider);
            if (((tab == 1 && slider != 1) || (tab == 2 && slider < 2)) && localY >= sliderY + 14
                && localY < sliderY + 26) {
                activeSlider = slider;
                adjusting = true;
                adjust(x);
                return;
            }
        }
        super.mouseClicked(x, y, button);
    }

    @Override
    protected void mouseClickMove(int x, int y, int button, long elapsed) {
        if (geometry.active()) geometry.move(x, y);
        else if (adjusting) adjust(x);
        else super.mouseClickMove(x, y, button, elapsed);
    }

    @Override
    protected void mouseMovedOrUp(int x, int y, int button) {
        if (button == 0) {
            adjusting = false;
            geometry.end();
            save();
        }
        super.mouseMovedOrUp(x, y, button);
    }

    private void save() {
        UtilityWindowChrome.save("settings", geometry, width, height);
        try {
            UtilityWindowChrome.settings()
                .savePlayerPreferences();
        } catch (RuntimeException error) {
            darkgrey.rpg.DarkGreyRpg.LOG.warn("Could not save dialogue preferences", error);
        }
    }

    @Override
    public void onGuiClosed() {
        adjusting = false;
        geometry.end();
        if (initialized) save();
        super.onGuiClosed();
    }

    @Override
    protected void keyTyped(char character, int key) {
        if (key != Keyboard.KEY_NONE && (key == Keyboard.KEY_ESCAPE || key == ClientQuestKeyHandler.settingsKeyCode()
            || key == mc.gameSettings.keyBindInventory.getKeyCode())) close(key);
        else super.keyTyped(character, key);
    }

    private PlayerUiPreferences.Side sideAt(int x) {
        return x < geometry.x + geometry.width / 2 ? PlayerUiPreferences.Side.LEFT : PlayerUiPreferences.Side.RIGHT;
    }

    private void close(int key) {
        net.minecraft.client.settings.KeyBinding.setKeyBindState(key, false);
        mc.displayGuiScreen(null);
    }

    @Override
    public boolean doesGuiPauseGame() {
        return false;
    }
}
