package darkgrey.rpg.client.gui;

import java.util.List;

import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.FontRenderer;
import net.minecraft.client.gui.GuiButton;

/** One measured rectangle for rendering and hit testing; oversized text remains scrollable. */
public final class GuiWrappedChoiceButton extends GuiButton {

    private final List<String> lines;
    private final double scale;
    private final int lineHeight;
    private final int visibleLines;
    private final SmoothScroll scroll = new SmoothScroll();
    private boolean keyboardFocused;
    private boolean presentationOnly;

    public void presentationOnly() {
        presentationOnly = true;
        enabled = false;
    }

    public void setKeyboardFocused(boolean focused) {
        keyboardFocused = focused;
    }

    public GuiWrappedChoiceButton(int id, int x, int y, int width, int maximumHeight, String text, FontRenderer font) {
        super(id, x, y, width, 20, text);
        scale = DialogueFontDrawing.scale();
        lineHeight = (int) Math.ceil(font.FONT_HEIGHT * scale);
        lines = font.listFormattedStringToWidth(text, Math.max(1, (int) ((width - 24) / scale)));
        int naturalHeight = Math.max(20, lines.size() * lineHeight + 12);
        height = Math.min(Math.max(lineHeight + 24, maximumHeight), naturalHeight);
        visibleLines = Math.max(1, (height - (naturalHeight > height ? 24 : 12)) / lineHeight);
        scroll.bounds((lines.size() - visibleLines) * lineHeight);
    }

    public boolean contains(int x, int y) {
        return visible && x >= xPosition && x < xPosition + width && y >= yPosition && y < yPosition + height;
    }

    public void scroll(int amount) {
        if (Math.abs((long) amount) > 100000) scroll.jump(amount < 0 ? 0 : Integer.MAX_VALUE);
        else scroll.wheel(amount < 0 ? 1 : -1, Math.abs(amount) * lineHeight);
    }

    @Override
    public void drawButton(Minecraft mc, int mouseX, int mouseY) {
        if (!visible) return;
        boolean hover = enabled && (contains(mouseX, mouseY) || keyboardFocused);
        drawRect(
            xPosition,
            yPosition,
            xPosition + width,
            yPosition + height,
            hover ? DgrUiPalette.SELECTED_BORDER : DgrUiPalette.BORDER);
        drawRect(
            xPosition + 1,
            yPosition + 1,
            xPosition + width - 1,
            yPosition + height - 1,
            hover ? DgrUiPalette.HOVER : DgrUiPalette.SUB_PANEL);
        scroll.tick();
        int first = scroll.pixelOffset() / lineHeight;
        try (GuiScrollClip clip = new GuiScrollClip(
            xPosition + 4,
            yPosition + 6,
            xPosition + width - 6,
            yPosition + 6 + visibleLines * lineHeight)) {
            for (int i = first; i < lines.size() && i <= first + visibleLines; i++) CanonicalDialogueRenderer.drawText(
                mc.fontRenderer,
                lines.get(i),
                xPosition + 8,
                yPosition + 6 + i * lineHeight - scroll.pixelOffset(),
                scale,
                enabled || presentationOnly ? DgrUiPalette.TEXT : DgrUiPalette.DISABLED);
        }
        if (lines.size() > visibleLines) {
            DgrUiText.left(
                mc.fontRenderer,
                "滚轮阅读  " + (first + 1) + "–" + Math.min(lines.size(), first + visibleLines) + " / " + lines.size(),
                xPosition + 8,
                yPosition + height - 11,
                DgrUiPalette.SECONDARY);
            int rail = height - 8;
            int thumb = Math.max(4, rail * visibleLines / lines.size());
            int top = yPosition + 4
                + (int) ((rail - thumb) * scroll.position() / Math.max(1, (lines.size() - visibleLines) * lineHeight));
            drawRect(xPosition + width - 5, top, xPosition + width - 3, top + thumb, DgrUiPalette.SELECTED_BORDER);
        }
    }
}
