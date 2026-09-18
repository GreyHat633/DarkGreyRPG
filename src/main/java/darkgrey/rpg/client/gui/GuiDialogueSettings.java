package darkgrey.rpg.client.gui;

import net.minecraft.client.gui.GuiScreen;

import org.lwjgl.input.Keyboard;

import darkgrey.rpg.client.ClientQuestKeyHandler;
import darkgrey.rpg.client.session.DialoguePreferences;
import darkgrey.rpg.client.session.PlayerUiPreferences;

/** Local, non-pausing presentation preferences, using the shared utility-window chrome. */
public final class GuiDialogueSettings extends GuiScreen {

    private final UtilityWindowGeometry geometry = new UtilityWindowGeometry(260, 220, 380, 280);
    private boolean initialized;
    private boolean adjusting;
    private int tab;
    private int activeSlider;
    private long previewStarted = System.nanoTime();
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
        drawCenteredString(fontRendererObj, "设置", x + w / 2, y + 10, DgrUiPalette.SELECTED_BORDER);
        String[] tabs = { "外观", "阅读", "声音" };
        for (int i = 0; i < 3; i++) {
            int tx = x + 16 + i * 72;
            drawRect(tx, y + 30, tx + 66, y + 49, tab == i ? DgrUiPalette.HOVER : DgrUiPalette.SUB_PANEL);
            fontRendererObj.drawString(tabs[i], tx + 10, y + 35, DgrUiPalette.TEXT);
        }
        if (tab == 0) {
            String[] themes = { "灰黑", "浅白", "蔚蓝" };
            fontRendererObj.drawString(
                "主题：" + themes[PlayerUiPreferences.theme()
                    .ordinal()] + "  ›",
                x + 20,
                y + 64,
                DgrUiPalette.TEXT);
            drawSlider(1, "对话框背景", PlayerUiPreferences.opacity());
            fontRendererObj.drawString(
                "文字大小：" + (int) (PlayerUiPreferences.textScale() * 100) + "%  ›",
                x + 20,
                y + 134,
                DgrUiPalette.TEXT);
        } else if (tab == 2) {
            drawSlider(0, "语音/音效", PlayerUiPreferences.voiceVolume());
            drawSlider(1, "会话音乐", PlayerUiPreferences.musicVolume());
            drawSlider(2, "环境留声机", PlayerUiPreferences.gramophoneVolume());
        } else {
            String label = DialoguePreferences.speed() == 0 ? "立即显示" : (int) DialoguePreferences.speed() + " 字/秒";
            fontRendererObj
                .drawString(label, x + w - 20 - fontRendererObj.getStringWidth(label), y + 64, DgrUiPalette.TEXT);
            drawSlider(0, "台词显示速度", DialoguePreferences.speed() / 120);
            fontRendererObj.drawString("0：立即显示    120：最快", x + 20, y + 100, DgrUiPalette.SECONDARY);
        }
        if (tab != 2) drawPreview();
        drawRect(x + 16, y + h - 28, x + 116, y + h - 8, DgrUiPalette.SUB_PANEL);
        fontRendererObj.drawString("恢复默认", x + 26, y + h - 22, DgrUiPalette.TEXT);
        UtilityWindowChrome.drawGrip(geometry);
        super.drawScreen(mouseX, mouseY, partialTicks);
    }

    private void adjust(int x) {
        double value = Math.max(0, Math.min(1, (x - geometry.x - 20.0) / (geometry.width - 40)));
        if (tab == 0) PlayerUiPreferences.setOpacity(value);
        else if (tab == 1) DialoguePreferences.setSpeed(Math.round(value * 120));
        else if (activeSlider == 0) PlayerUiPreferences.setVoiceVolume(value);
        else if (activeSlider == 1) PlayerUiPreferences.setMusicVolume(value);
        else PlayerUiPreferences.setGramophoneVolume(value);
        if (tab == 1) previewStarted = System.nanoTime();
    }

    private void drawPreview() {
        int x = geometry.x + 16, top = geometry.y + (tab == 0 ? 158 : 124);
        int bottom = geometry.y + geometry.height - 34;
        if (bottom - top < 22) return;
        drawRect(x, top, geometry.x + geometry.width - 16, bottom, DgrUiPalette.dialoguePanel());
        double scale = PlayerUiPreferences.textScale();
        double speed = DialoguePreferences.speed();
        double elapsed = (System.nanoTime() - previewStarted) / 1000000000.0;
        if (speed > 0 && elapsed > PREVIEW.length() / speed + 2) {
            previewStarted = System.nanoTime();
            elapsed = 0;
        }
        String shown = speed == 0 ? PREVIEW : PREVIEW.substring(0, Math.min(PREVIEW.length(), (int) (elapsed * speed)));
        int lineHeight = (int) Math.ceil(fontRendererObj.FONT_HEIGHT * scale) + 2;
        int yy = top + 6;
        for (String line : (java.util.List<String>) fontRendererObj
            .listFormattedStringToWidth(shown, Math.max(1, (int) ((geometry.width - 48) / scale)))) {
            if (yy + lineHeight > bottom) break;
            CanonicalDialogueRenderer.drawText(fontRendererObj, line, x + 8, yy, scale, DgrUiPalette.TEXT);
            yy += lineHeight;
        }
    }

    private void drawSlider(int index, String label, double value) {
        int x = geometry.x + 20, y = geometry.y + 64 + index * 36;
        fontRendererObj
            .drawString(label + (tab == 1 ? "" : "  " + Math.round(value * 100) + "%"), x, y, DgrUiPalette.TEXT);
        int end = geometry.x + geometry.width - 20;
        int knob = x + (int) Math.round((end - x) * value);
        drawRect(x, y + 18, end, y + 20, DgrUiPalette.BORDER);
        drawRect(knob - 3, y + 14, knob + 4, y + 24, DgrUiPalette.SELECTED_BORDER);
    }

    @Override
    protected void mouseClicked(int x, int y, int button) {
        if (button >= 0 && button - 100 == ClientQuestKeyHandler.settingsKeyCode()) {
            mc.displayGuiScreen(null);
            return;
        }
        if (geometry.begin(x, y, button)) return;
        if (button == 0 && x >= geometry.x + 16 && x < geometry.x + geometry.width - 16) {
            int localY = y - geometry.y;
            if (localY >= 30 && localY < 49 && x < geometry.x + 232) {
                tab = Math.min(2, (x - geometry.x - 16) / 72);
                return;
            }
            if (localY >= geometry.height - 28 && localY < geometry.height - 8 && x < geometry.x + 116) {
                PlayerUiPreferences.reset();
                DgrUiPalette.apply();
                save();
                return;
            }
            if (tab == 0 && localY >= 58 && localY < 80) {
                PlayerUiPreferences.setTheme(
                    PlayerUiPreferences.Theme.values()[(PlayerUiPreferences.theme()
                        .ordinal() + 1) % 3]);
                DgrUiPalette.apply();
                save();
                return;
            }
            if (tab == 0 && localY >= 128 && localY < 150) {
                PlayerUiPreferences.setTextScale(
                    PlayerUiPreferences.textScale() == 1 ? 1.25 : PlayerUiPreferences.textScale() == 1.25 ? 1.5 : 1);
                save();
                return;
            }
            int slider = Math.min(2, Math.max(0, (localY - 64) / 36));
            int sliderY = 64 + slider * 36;
            if ((tab == 0 && localY >= 114 && localY < 126)
                || (tab == 2 && localY >= sliderY + 14 && localY < sliderY + 26)) {
                activeSlider = slider;
                adjusting = true;
                adjust(x);
                return;
            }
        }
        if (tab == 1 && button == 0
            && x >= geometry.x + 16
            && x <= geometry.x + geometry.width - 16
            && y >= geometry.y + 78
            && y <= geometry.y + 90) {
            adjusting = true;
            adjust(x);
            return;
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
        geometry.end();
        if (initialized) save();
        super.onGuiClosed();
    }

    @Override
    protected void keyTyped(char character, int key) {
        if (key != Keyboard.KEY_NONE && key == ClientQuestKeyHandler.settingsKeyCode()) mc.displayGuiScreen(null);
        else super.keyTyped(character, key);
    }

    @Override
    public boolean doesGuiPauseGame() {
        return false;
    }
}
