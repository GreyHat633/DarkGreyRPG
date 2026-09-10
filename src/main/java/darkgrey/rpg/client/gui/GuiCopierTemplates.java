package darkgrey.rpg.client.gui;

import java.util.List;

import net.minecraft.client.gui.GuiButton;
import net.minecraft.client.gui.GuiScreen;
import net.minecraft.item.ItemStack;

import org.lwjgl.input.Mouse;

import darkgrey.rpg.entitytools.CopierState;
import darkgrey.rpg.entitytools.EntityTemplate;
import darkgrey.rpg.item.ItemCopier;
import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.message.entitytools.C2SCopierTemplateAction;

/** Client presentation for selecting and deleting templates stored in the held copier. */
public final class GuiCopierTemplates extends GuiScreen {

    private static final int ROW_HEIGHT = 24;
    private final List<EntityTemplate> templates;
    private final int selectedIndex;
    private int offset;
    private int pendingDelete = -1;
    private int panelLeft, panelTop, panelWidth, panelHeight;

    public GuiCopierTemplates(ItemStack stack) {
        CopierState state = ItemCopier.loadState(stack);
        templates = state.getTemplates();
        selectedIndex = state.getSelectedIndex();
    }

    private int visibleRows() {
        return Math.max(1, (panelHeight - 72) / ROW_HEIGHT);
    }

    private int listTop() {
        return panelTop + 32;
    }

    private int maxOffset() {
        return Math.max(0, templates.size() - visibleRows());
    }

    @Override
    public void initGui() {
        panelWidth = Math.min(420, width - 12);
        panelHeight = Math.min(340, height - 12);
        panelLeft = (width - panelWidth) / 2;
        panelTop = (height - panelHeight) / 2;
        offset = Math.max(0, Math.min(offset, maxOffset()));
        buttonList.clear();

        int rows = Math.min(visibleRows(), Math.max(0, templates.size() - offset));
        int rowButtonWidth = Math.max(80, panelWidth - 88);
        for (int row = 0; row < rows; row++) {
            int index = offset + row;
            EntityTemplate template = templates.get(index);
            String prefix = index == selectedIndex ? "✓ " : "  ";
            buttonList.add(
                new GuiRpgButton(
                    100 + row,
                    panelLeft + 8,
                    listTop() + row * ROW_HEIGHT,
                    rowButtonWidth,
                    20,
                    fontRendererObj.trimStringToWidth(
                        prefix + (index + 1) + ". " + template.getEntityType(),
                        rowButtonWidth - 12)));
            buttonList.add(
                new GuiRpgButton(
                    200 + row,
                    panelLeft + panelWidth - 72,
                    listTop() + row * ROW_HEIGHT,
                    64,
                    20,
                    pendingDelete == index
                        ? net.minecraft.client.resources.I18n.format("gui.darkgrey_rpg.copier_delete_confirm")
                        : net.minecraft.client.resources.I18n.format("gui.darkgrey_rpg.delete")));
        }
        buttonList.add(
            new GuiRpgButton(
                0,
                panelLeft + panelWidth - 88,
                panelTop + panelHeight - 28,
                80,
                20,
                net.minecraft.client.resources.I18n.format("gui.darkgrey_rpg.close")));
    }

    @Override
    protected void actionPerformed(GuiButton button) {
        if (button.id == 0) {
            mc.displayGuiScreen(null);
            return;
        }
        if (button.id >= 100 && button.id < 100 + visibleRows()) {
            send(C2SCopierTemplateAction.Operation.SELECT, offset + button.id - 100);
            return;
        }
        if (button.id >= 200 && button.id < 200 + visibleRows()) {
            int index = offset + button.id - 200;
            if (pendingDelete != index) {
                pendingDelete = index;
                initGui();
            } else {
                send(C2SCopierTemplateAction.Operation.DELETE, index);
            }
        }
    }

    private void send(C2SCopierTemplateAction.Operation operation, int index) {
        if (index < 0 || index >= templates.size()) return;
        DialogueNetwork.CHANNEL.sendToServer(
            new C2SCopierTemplateAction(
                mc.thePlayer.inventory.currentItem,
                operation,
                index,
                templates.size(),
                templates.get(index)
                    .getEntityType()));
        mc.displayGuiScreen(null);
    }

    @Override
    public void drawScreen(int mouseX, int mouseY, float partialTicks) {
        drawDefaultBackground();
        drawRect(panelLeft, panelTop, panelLeft + panelWidth, panelTop + panelHeight, 0xEE303030);
        drawRect(panelLeft, panelTop, panelLeft + panelWidth, panelTop + 2, 0xFF8C8C8C);
        drawCenteredString(
            fontRendererObj,
            net.minecraft.client.resources.I18n.format("gui.darkgrey_rpg.copier"),
            width / 2,
            panelTop + 10,
            0xFFF0E6D2);
        if (pendingDelete >= 0) drawCenteredString(
            fontRendererObj,
            net.minecraft.client.resources.I18n.format("gui.darkgrey_rpg.copier_delete_warning"),
            width / 2,
            panelTop + 21,
            0xFFFFC46B);
        if (templates.isEmpty()) drawCenteredString(
            fontRendererObj,
            net.minecraft.client.resources.I18n.format("gui.darkgrey_rpg.copier_empty"),
            width / 2,
            panelTop + panelHeight / 2,
            0xFFCCCCCC);
        else if (templates.size() > visibleRows()) drawString(
            fontRendererObj,
            net.minecraft.client.resources.I18n.format("gui.darkgrey_rpg.copier_scroll"),
            panelLeft + 8,
            panelTop + panelHeight - 38,
            0xFFBDBDBD);
        super.drawScreen(mouseX, mouseY, partialTicks);
    }

    @Override
    public void handleMouseInput() {
        super.handleMouseInput();
        int delta = Mouse.getEventDWheel();
        if (delta == 0 || templates.size() <= visibleRows()) return;
        int mouseX = Mouse.getEventX() * width / mc.displayWidth;
        int mouseY = height - Mouse.getEventY() * height / mc.displayHeight - 1;
        if (mouseX < panelLeft + 4 || mouseX >= panelLeft + panelWidth - 4
            || mouseY < listTop()
            || mouseY >= panelTop + panelHeight - 42) return;
        offset = Math.max(0, Math.min(maxOffset(), offset + (delta < 0 ? 1 : -1)));
        pendingDelete = -1;
        initGui();
    }

    @Override
    public boolean doesGuiPauseGame() {
        return false;
    }
}
