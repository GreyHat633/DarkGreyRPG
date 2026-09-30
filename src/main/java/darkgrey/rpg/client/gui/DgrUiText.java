package darkgrey.rpg.client.gui;

import net.minecraft.client.gui.FontRenderer;

/** DGR-only text drawing; vanilla GUI helpers add a shadow unconditionally. */
public final class DgrUiText {

    private DgrUiText() {}

    public static void centered(FontRenderer font, String text, int x, int y, int color) {
        font.drawString(text, x - font.getStringWidth(text) / 2, y, color);
    }

    public static void left(FontRenderer font, String text, int x, int y, int color) {
        font.drawString(text, x, y, color);
    }
}
