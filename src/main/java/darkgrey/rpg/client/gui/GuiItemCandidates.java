package darkgrey.rpg.client.gui;

import net.minecraft.client.gui.GuiScreen;
import net.minecraft.nbt.NBTTagCompound;

import org.lwjgl.input.Mouse;

/** Read-only expansion; returning never submits a choice. */
public final class GuiItemCandidates extends GuiScreen {

    private final GuiScreen parent;
    private final ItemSlotStrip strip;
    private final SmoothScroll scroll = new SmoothScroll();
    private int clipLeft, clipRight, clipTop, clipBottom;

    public GuiItemCandidates(GuiScreen parent, NBTTagCompound source) {
        this.parent = parent;
        strip = new ItemSlotStrip(source, false);
    }

    public void drawScreen(int x, int y, float partial) {
        drawDefaultBackground();
        int w = Math.max(80, Math.min(340, width - 24)), left = (width - w) / 2;
        String title = "可用物品 · 任意成员合计满足数量";
        int header = fontRendererObj.listFormattedStringToWidth(title, w - 20)
            .size() * (fontRendererObj.FONT_HEIGHT + 2);
        int panelHeight = Math.min(height - 24, strip.height(w - 20) + header + 44);
        int top = (height - panelHeight) / 2, bottom = top + panelHeight;
        drawRect(left, top, left + w, bottom, DgrUiPalette.BORDER);
        drawRect(left + 1, top + 1, left + w - 1, bottom - 1, DgrUiPalette.WINDOW_PANEL);
        fontRendererObj.drawSplitString(title, left + 10, top + 10, w - 20, DgrUiPalette.TEXT);
        clipTop = top + header + 20;
        clipBottom = bottom - 24;
        clipLeft = left;
        clipRight = left + w;
        scroll.bounds(strip.height(w - 20) - (clipBottom - clipTop));
        scroll.tick();
        strip.draw(left + 10, clipTop - scroll.pixelOffset(), w - 20, clipTop, clipBottom, x, y);
        fontRendererObj.drawString(
            strip.height(w - 20) > clipBottom - clipTop ? "Esc 返回 · 滚轮查看更多" : "Esc 返回",
            left + 10,
            bottom - 16,
            DgrUiPalette.SECONDARY);
        if (strip.tooltip != null) drawHoveringText(strip.tooltip, x, y, fontRendererObj);
    }

    public void handleMouseInput() {
        super.handleMouseInput();
        int delta = Mouse.getEventDWheel();
        int x = Mouse.getEventX() * width / mc.displayWidth;
        int y = height - Mouse.getEventY() * height / mc.displayHeight - 1;
        if (x >= clipLeft && x < clipRight && y >= clipTop && y < clipBottom) scroll.wheel(delta, 24);
    }

    protected void keyTyped(char c, int key) {
        if (key == 1 || key == mc.gameSettings.keyBindInventory.getKeyCode()) mc.displayGuiScreen(parent);
    }

    public boolean doesGuiPauseGame() {
        return false;
    }
}
