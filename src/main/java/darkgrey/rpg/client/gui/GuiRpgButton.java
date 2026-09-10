package darkgrey.rpg.client.gui;

import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.GuiButton;

/** Bounded vanilla button interaction with the Dialogue panel palette. */
public final class GuiRpgButton extends GuiButton {

    public GuiRpgButton(int id, int x, int y, int width, int height, String text) {
        super(id, x, y, width, height, text);
    }

    @Override
    public void drawButton(Minecraft mc, int mouseX, int mouseY) {
        if (!visible) return;
        boolean hover = enabled && mouseX >= xPosition
            && mouseX < xPosition + width
            && mouseY >= yPosition
            && mouseY < yPosition + height;
        int border = !enabled ? DgrUiPalette.DISABLED : hover ? DgrUiPalette.SELECTED_BORDER : DgrUiPalette.BORDER;
        drawRect(xPosition, yPosition, xPosition + width, yPosition + height, border);
        drawRect(
            xPosition + 1,
            yPosition + 1,
            xPosition + width - 1,
            yPosition + height - 1,
            hover ? DgrUiPalette.HOVER : DgrUiPalette.SUB_PANEL);
        drawCenteredString(
            mc.fontRenderer,
            displayString,
            xPosition + width / 2,
            yPosition + (height - 8) / 2,
            !enabled ? DgrUiPalette.DISABLED : hover ? DgrUiPalette.TEXT : DgrUiPalette.TEXT);
    }
}
