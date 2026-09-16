package darkgrey.rpg.client.gui;

import net.minecraft.client.gui.GuiScreen;

import org.lwjgl.input.Keyboard;

import darkgrey.rpg.client.session.DialoguePreferences;

/** Local, non-pausing presentation preferences, using the shared utility-window chrome. */
public final class GuiDialogueSettings extends GuiScreen {

    private final UtilityWindowGeometry geometry = new UtilityWindowGeometry(240, 130, 360, 180);
    private boolean initialized;
    private boolean adjusting;

    public static void loadPreferences() {
        DialoguePreferences.setSpeed(
            UtilityWindowChrome.settings()
                .dialogueSpeed());
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
        fontRendererObj.drawString("台词显示速度", x + 20, y + 43, DgrUiPalette.TEXT);
        String label = DialoguePreferences.speed() == 0 ? "立即显示" : (int) DialoguePreferences.speed() + " 字/秒";
        fontRendererObj
            .drawString(label, x + w - 20 - fontRendererObj.getStringWidth(label), y + 43, DgrUiPalette.TEXT);
        drawRect(x + 20, y + 72, x + w - 20, y + 75, DgrUiPalette.SECONDARY);
        int knob = x + 20 + (int) ((w - 40) * DialoguePreferences.speed() / 120);
        drawRect(x + 20, y + 72, knob, y + 75, DgrUiPalette.SELECTED_BORDER);
        drawRect(knob - 3, y + 68, knob + 4, y + 79, DgrUiPalette.SELECTED_BORDER);
        fontRendererObj.drawString("0：立即显示    120：最快", x + 20, y + 92, DgrUiPalette.SECONDARY);
        UtilityWindowChrome.drawGrip(geometry);
        super.drawScreen(mouseX, mouseY, partialTicks);
    }

    private void adjust(int x) {
        DialoguePreferences
            .setSpeed(Math.round(Math.max(0, Math.min(1, (x - geometry.x - 20.0) / (geometry.width - 40))) * 120));
    }

    @Override
    protected void mouseClicked(int x, int y, int button) {
        if (geometry.begin(x, y, button)) return;
        if (button == 0 && x >= geometry.x + 16
            && x <= geometry.x + geometry.width - 16
            && y >= geometry.y + 62
            && y <= geometry.y + 84) {
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
                .saveDialogueSpeed(DialoguePreferences.speed());
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
        if (key == Keyboard.KEY_P) mc.displayGuiScreen(null);
        else super.keyTyped(character, key);
    }

    @Override
    public boolean doesGuiPauseGame() {
        return false;
    }
}
