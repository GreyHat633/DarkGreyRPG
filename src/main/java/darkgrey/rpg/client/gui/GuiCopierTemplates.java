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
    private final UtilityWindowGeometry windowGeometry = new UtilityWindowGeometry(260, 160, 420, 340);
    private boolean geometryInitialized;
    private int laidOutOffset = -1;
    private int laidOutDelete = -2;

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
        UtilityWindowChrome.open("copier", windowGeometry, width, height, geometryInitialized);
        geometryInitialized = true;
        layoutControls();
    }

    private void layoutControls() {
        panelLeft = windowGeometry.x;
        panelTop = windowGeometry.y;
        panelWidth = windowGeometry.width;
        panelHeight = windowGeometry.height;
        offset = Math.max(0, Math.min(offset, maxOffset()));
        int rows = Math.min(visibleRows(), Math.max(0, templates.size() - offset));
        int rowButtonWidth = Math.max(80, panelWidth - 88);
        if (buttonList.size() == rows * 2 + 1 && laidOutOffset == offset && laidOutDelete == pendingDelete) {
            for (int row = 0; row < rows; row++) {
                GuiButton select = (GuiButton) buttonList.get(row * 2);
                GuiButton delete = (GuiButton) buttonList.get(row * 2 + 1);
                select.xPosition = panelLeft + 8;
                select.yPosition = listTop() + row * ROW_HEIGHT;
                select.width = rowButtonWidth;
                String prefix = offset + row == selectedIndex ? "✓ " : "  ";
                select.displayString = fontRendererObj.trimStringToWidth(
                    prefix + (offset + row + 1)
                        + ". "
                        + templates.get(offset + row)
                            .getEntityType(),
                    rowButtonWidth - 12);
                delete.xPosition = panelLeft + panelWidth - 72;
                delete.yPosition = select.yPosition;
            }
            GuiButton close = (GuiButton) buttonList.get(rows * 2);
            close.xPosition = panelLeft + panelWidth - 88;
            close.yPosition = panelTop + panelHeight - 28;
            return;
        }
        buttonList.clear();
        laidOutOffset = offset;
        laidOutDelete = pendingDelete;
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
                layoutControls();
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
            panelLeft + panelWidth / 2,
            panelTop + 10,
            DgrUiPalette.TEXT);
        if (pendingDelete >= 0) drawCenteredString(
            fontRendererObj,
            net.minecraft.client.resources.I18n.format("gui.darkgrey_rpg.copier_delete_warning"),
            panelLeft + panelWidth / 2,
            panelTop + 21,
            DgrUiPalette.TEXT);
        if (templates.isEmpty()) drawCenteredString(
            fontRendererObj,
            net.minecraft.client.resources.I18n.format("gui.darkgrey_rpg.copier_empty"),
            panelLeft + panelWidth / 2,
            panelTop + panelHeight / 2,
            0xFFCCCCCC);
        else if (templates.size() > visibleRows()) drawString(
            fontRendererObj,
            net.minecraft.client.resources.I18n.format("gui.darkgrey_rpg.copier_scroll"),
            panelLeft + 8,
            panelTop + panelHeight - 38,
            0xFFBDBDBD);
        UtilityWindowChrome.drawGrip(windowGeometry);
        super.drawScreen(mouseX, mouseY, partialTicks);
    }

    @Override
    public void handleMouseInput() {
        super.handleMouseInput();
        if (windowGeometry.active() || pendingDelete >= 0) return;
        int delta = Mouse.getEventDWheel();
        if (delta == 0 || templates.size() <= visibleRows()) return;
        int mouseX = Mouse.getEventX() * width / mc.displayWidth;
        int mouseY = height - Mouse.getEventY() * height / mc.displayHeight - 1;
        if (mouseX < panelLeft + 4 || mouseX >= panelLeft + panelWidth - 4
            || mouseY < listTop()
            || mouseY >= panelTop + panelHeight - 42) return;
        offset = Math.max(0, Math.min(maxOffset(), offset + (delta < 0 ? 1 : -1)));
        pendingDelete = -1;
        layoutControls();
    }

    @Override
    protected void mouseClicked(int x, int y, int button) {
        if (pendingDelete < 0 && windowGeometry.begin(x, y, button)) return;
        super.mouseClicked(x, y, button);
    }

    @Override
    protected void mouseClickMove(int x, int y, int button, long elapsed) {
        if (windowGeometry.active()) {
            windowGeometry.move(x, y);
            layoutControls();
            return;
        }
        super.mouseClickMove(x, y, button, elapsed);
    }

    @Override
    protected void mouseMovedOrUp(int x, int y, int button) {
        if (button == 0 && windowGeometry.active()) {
            windowGeometry.end();
            UtilityWindowChrome.save("copier", windowGeometry, width, height);
            return;
        }
        super.mouseMovedOrUp(x, y, button);
    }

    @Override
    protected void keyTyped(char character, int key) {
        if (key == 1 && pendingDelete >= 0) {
            pendingDelete = -1;
            layoutControls();
            return;
        }
        if (key == mc.gameSettings.keyBindInventory.getKeyCode()) {
            mc.displayGuiScreen(null);
            return;
        }
        super.keyTyped(character, key);
    }

    @Override
    public void onGuiClosed() {
        windowGeometry.end();
        if (geometryInitialized) UtilityWindowChrome.save("copier", windowGeometry, width, height);
        super.onGuiClosed();
    }

    @Override
    public boolean doesGuiPauseGame() {
        return false;
    }
}
