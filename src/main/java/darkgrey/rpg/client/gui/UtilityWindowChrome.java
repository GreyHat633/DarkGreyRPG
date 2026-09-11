package darkgrey.rpg.client.gui;

import java.io.File;

import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.Gui;

import darkgrey.rpg.DarkGreyRpg;

/** Connects pure window geometry to local preferences at open/release/close boundaries. */
final class UtilityWindowChrome extends Gui {

    private UtilityWindowChrome() {}

    static void open(String key, UtilityWindowGeometry geometry, int width, int height, boolean initialized) {
        if (initialized) geometry.restore(width, height, geometry.snapshot());
        else settings().load(key, geometry, width, height);
    }

    static void save(String key, UtilityWindowGeometry geometry, int width, int height) {
        try {
            settings().save(key, geometry, width, height);
        } catch (RuntimeException failure) {
            DarkGreyRpg.LOG.warn("Could not save local utility window layout " + key, failure);
        }
    }

    private static UtilityWindowSettings settings() {
        return new UtilityWindowSettings(
            new File(Minecraft.getMinecraft().mcDataDir, "config/darkgrey-rpg-windows.properties"));
    }

    static void drawGrip(UtilityWindowGeometry geometry) {
        for (int sx : new int[] { -1, 1 }) {
            for (int sy : new int[] { -1, 1 }) {
                int cx = sx < 0 ? geometry.x + 3 : geometry.x + geometry.width - 4;
                int cy = sy < 0 ? geometry.y + 3 : geometry.y + geometry.height - 4;
                for (int offset = 0; offset <= 6; offset += 3) {
                    int px = cx - sx * offset, py = cy - sy * offset;
                    drawRect(px, cy, px + 2, cy + 2, DgrUiPalette.SECONDARY);
                    drawRect(cx, py, cx + 2, py + 2, DgrUiPalette.SECONDARY);
                }
            }
        }
    }

    static boolean overButton(java.util.List buttons, int x, int y) {
        for (Object value : buttons) {
            net.minecraft.client.gui.GuiButton button = (net.minecraft.client.gui.GuiButton) value;
            if (button.visible && x >= button.xPosition
                && x < button.xPosition + button.width
                && y >= button.yPosition
                && y < button.yPosition + button.height) return true;
        }
        return false;
    }
}
