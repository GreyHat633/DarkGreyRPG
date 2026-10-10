package darkgrey.rpg.client.gui;

import net.minecraft.client.gui.FontRenderer;

/** Common single-pass, pixel-aligned drawing for DGR interface labels. */
public final class DgrUiText {

    private DgrUiText() {}

    public static void centered(FontRenderer font, String text, int x, int y, int color) {
        String label = label(text);
        left(font, label, x - font.getStringWidth(label) / 2, y, color);
    }

    public static void left(FontRenderer font, String text, int x, int y, int color) {
        DialogueFontDrawing.draw(font, label(text), x, y, 1, color);
    }

    /** Unicode synthetic bold duplicates the bitmap stroke; interface hierarchy uses color and spacing. */
    public static String label(String text) {
        return text == null ? ""
            : text.replace("\u00a7l", "")
                .replace("\u00a7L", "");
    }

    public static void wrapped(FontRenderer font, String text, int x, int y, int width, int color) {
        int row = 0;
        for (Object line : font.listFormattedStringToWidth(label(text), width))
            left(font, (String) line, x, y + row++ * font.FONT_HEIGHT, color);
    }

    public static void tooltip(FontRenderer font, java.util.List<String> lines, int x, int y, int width, int height) {
        RuntimeDirectoryVisuals.tooltip(font, lines, x, y, width, height);
    }
}
