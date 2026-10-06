package darkgrey.rpg.client.gui;

import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.GuiButton;

/** Bounded vanilla button interaction with the Dialogue panel palette. */
public final class GuiRpgButton extends GuiButton {

    public boolean selected;

    public enum Icon {
        NONE,
        ENABLE,
        DISABLE,
        REFRESH
    }

    private final Icon icon;

    public GuiRpgButton(int id, int x, int y, int width, int height, String text) {
        this(id, x, y, width, height, text, Icon.NONE);
    }

    public GuiRpgButton(int id, int x, int y, int width, int height, String text, Icon icon) {
        super(id, x, y, width, height, text);
        this.icon = icon;
    }

    @Override
    public void drawButton(Minecraft mc, int mouseX, int mouseY) {
        if (!visible) return;
        boolean hover = enabled && mouseX >= xPosition
            && mouseX < xPosition + width
            && mouseY >= yPosition
            && mouseY < yPosition + height;
        int border = !enabled ? DgrUiPalette.DISABLED
            : hover || selected ? DgrUiPalette.SELECTED_BORDER : DgrUiPalette.BORDER;
        drawRect(xPosition, yPosition, xPosition + width, yPosition + height, border);
        drawRect(
            xPosition + 1,
            yPosition + 1,
            xPosition + width - 1,
            yPosition + height - 1,
            selected ? DgrUiPalette.SELECTED_FILL : hover ? DgrUiPalette.HOVER : DgrUiPalette.SUB_PANEL);
        int labelWidth = mc.fontRenderer.getStringWidth(displayString), gap = icon == Icon.NONE ? 0 : 10;
        int left = xPosition + (width - labelWidth - gap) / 2;
        if (icon != Icon.NONE) {
            int color = !enabled ? DgrUiPalette.DISABLED
                : icon == Icon.ENABLE ? 0xff49a85d : icon == Icon.DISABLE ? 0xffdc4949 : DgrUiPalette.STORY_TEXT;
            int cy = yPosition + (height - 7) / 2;
            if (icon == Icon.ENABLE) {
                for (int i = 0; i < 7; i++) drawRect(left + i / 2, cy + i, left + 4, cy + i + 1, color);
            } else if (icon == Icon.DISABLE) {
                drawRect(left, cy, left + 6, cy + 6, color);
                drawRect(left + 1, cy + 2, left + 5, cy + 4, DgrUiPalette.SUB_PANEL);
            } else {
                drawRect(left, cy + 1, left + 6, cy + 2, color);
                drawRect(left, cy + 1, left + 1, cy + 6, color);
                drawRect(left, cy + 5, left + 6, cy + 6, color);
                drawRect(left + 5, cy + 3, left + 6, cy + 6, color);
                drawRect(left + 4, cy, left + 6, cy + 3, color);
            }
        }
        mc.fontRenderer.drawString(
            displayString,
            left + gap,
            yPosition + (height - 8) / 2,
            !enabled ? DgrUiPalette.DISABLED : DgrUiPalette.TEXT);
    }
}
