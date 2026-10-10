package darkgrey.rpg.client.gui;

import net.minecraft.client.gui.FontRenderer;

/** Common single-pass, pixel-aligned drawing for DGR interface labels. */
public final class DgrUiText extends net.minecraft.client.gui.Gui {

    private static final DgrUiText TOOLTIP = new DgrUiText();

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
        if (lines.isEmpty()) return;
        int textWidth = 0;
        for (String line : lines) textWidth = Math.max(textWidth, font.getStringWidth(label(line)));
        int[] bounds = tooltipBounds(x, y, width, height, textWidth, lines.size());
        int left = bounds[0], top = bounds[1], boxHeight = bounds[2];
        org.lwjgl.opengl.GL11.glPushAttrib(org.lwjgl.opengl.GL11.GL_ALL_ATTRIB_BITS);
        org.lwjgl.opengl.GL11.glPushMatrix();
        try {
            org.lwjgl.opengl.GL11.glDisable(org.lwjgl.opengl.GL12.GL_RESCALE_NORMAL);
            org.lwjgl.opengl.GL11.glDisable(org.lwjgl.opengl.GL11.GL_LIGHTING);
            org.lwjgl.opengl.GL11.glDisable(org.lwjgl.opengl.GL11.GL_DEPTH_TEST);
            org.lwjgl.opengl.GL11.glTranslatef(0, 0, 300);
            int background = -267386864, border = 1347420415;
            int bottom = (border & 16711422) >> 1 | border & -16777216;
            TOOLTIP.drawGradientRect(left - 3, top - 4, left + textWidth + 3, top - 3, background, background);
            TOOLTIP.drawGradientRect(
                left - 3,
                top + boxHeight + 3,
                left + textWidth + 3,
                top + boxHeight + 4,
                background,
                background);
            TOOLTIP
                .drawGradientRect(left - 3, top - 3, left + textWidth + 3, top + boxHeight + 3, background, background);
            TOOLTIP.drawGradientRect(left - 4, top - 3, left - 3, top + boxHeight + 3, background, background);
            TOOLTIP.drawGradientRect(
                left + textWidth + 3,
                top - 3,
                left + textWidth + 4,
                top + boxHeight + 3,
                background,
                background);
            TOOLTIP.drawGradientRect(left - 3, top - 2, left - 2, top + boxHeight + 2, border, bottom);
            TOOLTIP.drawGradientRect(
                left + textWidth + 2,
                top - 2,
                left + textWidth + 3,
                top + boxHeight + 2,
                border,
                bottom);
            TOOLTIP.drawGradientRect(left - 3, top - 3, left + textWidth + 3, top - 2, border, border);
            TOOLTIP.drawGradientRect(
                left - 3,
                top + boxHeight + 2,
                left + textWidth + 3,
                top + boxHeight + 3,
                bottom,
                bottom);
            for (int i = 0; i < lines.size(); i++) {
                left(font, lines.get(i), left, top, -1);
                top += i == 0 ? 12 : 10;
            }
        } finally {
            org.lwjgl.opengl.GL11.glPopMatrix();
            org.lwjgl.opengl.GL11.glPopAttrib();
        }
    }

    /** The existing GuiScreen hover placement and row spacing, without a second wrapping pass. */
    private static int[] tooltipBounds(int x, int y, int width, int height, int textWidth, int rows) {
        int left = x + 12, top = y - 12, boxHeight = rows > 1 ? 10 + (rows - 1) * 10 : 8;
        if (left + textWidth > width) left -= 28 + textWidth;
        if (top + boxHeight + 6 > height) top = height - boxHeight - 6;
        return new int[] { left, top, boxHeight };
    }
}
