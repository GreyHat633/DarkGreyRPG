package darkgrey.rpg.client.gui;

import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.FontRenderer;
import net.minecraft.client.gui.GuiButton;

import org.lwjgl.opengl.GL11;

public class GuiModernButton extends GuiButton {

    public GuiModernButton(int id, int x, int y, int width, int height, String text) {
        super(id, x, y, width, height, text);
    }

    @Override
    public void drawButton(Minecraft mc, int mouseX, int mouseY) {
        if (this.visible) {
            FontRenderer fontrenderer = mc.fontRenderer;
            this.field_146123_n = mouseX >= this.xPosition && mouseY >= this.yPosition
                && mouseX < this.xPosition + this.width
                && mouseY < this.yPosition + this.height;
            int hoverState = this.getHoverState(this.field_146123_n);

            // Draw clean background without default texture
            GL11.glEnable(GL11.GL_BLEND);
            GL11.glBlendFunc(GL11.GL_SRC_ALPHA, GL11.GL_ONE_MINUS_SRC_ALPHA);

            int bgColor = 0xCC35395A; // Deep Space Panel
            int borderColor = 0xFF5B5FA6; // Deep Space Border
            int textColor = 0xFFEEF0FF; // Light text

            if (!this.enabled) {
                bgColor = 0x882B2F4A;
                borderColor = 0x885B5FA6;
                textColor = 0x88EEF0FF;
            } else if (this.field_146123_n) {
                bgColor = 0xFF7D8CFF; // Accent Purple Blue
                borderColor = 0xFF9DACFF;
                textColor = 0xFFFFFFFF; // White text on hover
            }

            // Draw border
            drawRect(
                this.xPosition,
                this.yPosition,
                this.xPosition + this.width,
                this.yPosition + this.height,
                borderColor);
            // Draw background
            drawRect(
                this.xPosition + 1,
                this.yPosition + 1,
                this.xPosition + this.width - 1,
                this.yPosition + this.height - 1,
                bgColor);

            this.mouseDragged(mc, mouseX, mouseY);

            // Draw centered string manually without shadow for modern flat look
            int stringWidth = fontrenderer.getStringWidth(this.displayString);
            int textX = this.xPosition + (this.width / 2) - (stringWidth / 2);
            int textY = this.yPosition + (this.height - 8) / 2;

            fontrenderer.drawString(this.displayString, textX, textY, textColor);
        }
    }
}
