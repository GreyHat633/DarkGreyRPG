package darkgrey.rpg.client.gui;

import java.util.ArrayList;
import java.util.List;

import net.minecraft.client.gui.FontRenderer;
import net.minecraft.client.gui.Gui;

import org.lwjgl.opengl.GL11;

/** Compact outline icons and common text hierarchy for Runtime directory browsers. */
public final class RuntimeDirectoryVisuals extends Gui {

    public static final int ROW_HEIGHT = 22;

    private RuntimeDirectoryVisuals() {}

    public static void heading(FontRenderer font, String text, int x, int y, int width) {
        // Vanilla's duplicated-stroke bold makes small Unicode glyphs uneven; color and spacing carry hierarchy.
        text(font, text, x, y, width, DgrUiPalette.STORY_TEXT);
        drawRect(x, y + 15, x + width, y + 16, DgrUiPalette.BORDER);
    }

    public static void row(FontRenderer font, String name, boolean folder, boolean open, int depth, int x, int y,
        int width, boolean selected, boolean hovered) {
        row(font, name, folder, open, depth, x, y, width, selected, hovered, 0);
    }

    public static void row(FontRenderer font, String name, boolean folder, boolean open, int depth, int x, int y,
        int width, boolean selected, boolean hovered, int reservedWidth) {
        if (selected || hovered)
            drawRect(x, y, x + width, y + ROW_HEIGHT, selected ? DgrUiPalette.SELECTED_FILL : DgrUiPalette.HOVER);
        if (selected) drawRect(x, y, x + 2, y + ROW_HEIGHT, DgrUiPalette.STORY_TEXT);
        int left = x + 4 + depth * 14;
        int textY = textY(font, y);
        if (depth > 0) drawRect(x + 10, y, x + 11, y + ROW_HEIGHT, DgrUiPalette.BORDER);
        if (folder) text(font, open ? "-" : "+", left, textY, 8, DgrUiPalette.SECONDARY);
        int icon = left + 10, color = folder ? DgrUiPalette.STORY_TEXT : DgrUiPalette.SECONDARY;
        if (folder) {
            drawRect(icon, y + 7, icon + 9, y + 8, color);
            drawRect(icon, y + 5, icon + 4, y + 6, color);
            drawRect(icon, y + 5, icon + 1, y + 14, color);
            drawRect(icon + 8, y + 7, icon + 9, y + 14, color);
            drawRect(icon, y + 13, icon + 9, y + 14, color);
            drawRect(icon + 3, y + 5, icon + 4, y + 8, color);
        } else {
            drawRect(icon, y + 6, icon + 7, y + 7, color);
            drawRect(icon, y + 6, icon + 1, y + 15, color);
            drawRect(icon + 6, y + 6, icon + 7, y + 15, color);
            drawRect(icon, y + 14, icon + 7, y + 15, color);
            drawRect(icon + 2, y + 9, icon + 5, y + 10, color);
            drawRect(icon + 2, y + 12, icon + 5, y + 13, color);
        }
        text(font, name, icon + 12, textY, Math.max(0, width - (icon + 12 - x) - 5 - reservedWidth), DgrUiPalette.TEXT);
    }

    public static int textY(FontRenderer font, int rowTop) {
        return rowTop + (ROW_HEIGHT - font.FONT_HEIGHT) / 2;
    }

    public static RuntimeRowText.Metrics metrics(final FontRenderer font) {
        return new RuntimeRowText.Metrics() {

            @Override
            public int width(String text) {
                return font.getStringWidth(text);
            }

            @Override
            public String trim(String text, int width) {
                return font.trimStringToWidth(text, width);
            }
        };
    }

    public static void text(FontRenderer font, String text, int x, int y, int width, int color) {
        DgrUiText.left(font, RuntimeRowText.fit(DgrUiText.label(text), width, metrics(font)), x, y, color);
    }

    public static void tooltip(FontRenderer font, List<String> lines, int mouseX, int mouseY, int screenWidth,
        int screenHeight) {
        List<String> wrapped = new ArrayList<String>();
        int limit = Math.max(64, Math.min(240, screenWidth - 24)), width = 0;
        for (String line : lines) wrapped.addAll(font.listFormattedStringToWidth(line, limit));
        for (String line : wrapped) width = Math.max(width, font.getStringWidth(line));
        int height = wrapped.size() * (font.FONT_HEIGHT + 2);
        int left = Math.max(4, Math.min(mouseX + 12, screenWidth - width - 4));
        int top = Math.max(4, Math.min(mouseY - 12, screenHeight - height - 4));
        GL11.glPushAttrib(GL11.GL_TEXTURE_BIT | GL11.GL_ENABLE_BIT | GL11.GL_COLOR_BUFFER_BIT | GL11.GL_CURRENT_BIT);
        GL11.glPushMatrix();
        try {
            GL11.glDisable(GL11.GL_LIGHTING);
            GL11.glDisable(GL11.GL_DEPTH_TEST);
            GL11.glTranslatef(0, 0, 300);
            drawRect(left - 3, top - 3, left + width + 3, top + height + 3, DgrUiPalette.BORDER);
            drawRect(left - 2, top - 2, left + width + 2, top + height + 2, DgrUiPalette.SUB_PANEL);
            for (int i = 0; i < wrapped.size(); i++)
                DgrUiText.left(font, wrapped.get(i), left, top + i * (font.FONT_HEIGHT + 2), DgrUiPalette.TEXT);
        } finally {
            GL11.glPopMatrix();
            GL11.glPopAttrib();
        }
    }
}
