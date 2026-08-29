package darkgrey.rpg.client.gui;

import java.util.List;

import net.minecraft.client.gui.GuiButton;
import net.minecraft.client.gui.GuiScreen;
import net.minecraft.item.ItemStack;

import darkgrey.rpg.entitytools.CopierState;
import darkgrey.rpg.entitytools.EntityTemplate;
import darkgrey.rpg.item.ItemCopier;
import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.message.entitytools.C2SCopierTemplateAction;

/** Client presentation for selecting and deleting templates stored in the held copier. */
public final class GuiCopierTemplates extends GuiScreen {

    private static final int ROWS = 6;
    private final List<EntityTemplate> templates;
    private final int selectedIndex;
    private int offset;
    private int pendingDelete = -1;

    public GuiCopierTemplates(ItemStack stack) {
        CopierState state = ItemCopier.loadState(stack);
        templates = state.getTemplates();
        selectedIndex = state.getSelectedIndex();
    }

    @Override
    public void initGui() {
        buttonList.clear();
        int left = width / 2 - 180;
        int top = height / 2 - 105;
        int visible = Math.min(ROWS, templates.size() - offset);
        for (int row = 0; row < visible; row++) {
            int index = offset + row;
            EntityTemplate template = templates.get(index);
            String prefix = index == selectedIndex ? "✓ " : "";
            buttonList.add(
                new GuiModernButton(
                    100 + row,
                    left + 18,
                    top + 34 + row * 25,
                    260,
                    20,
                    prefix + (index + 1) + ". " + template.getEntityType()));
            buttonList.add(
                new GuiModernButton(
                    200 + row,
                    left + 286,
                    top + 34 + row * 25,
                    56,
                    20,
                    pendingDelete == index ? "确认" : "删除"));
        }
        if (offset > 0) buttonList.add(new GuiModernButton(10, left + 18, top + 188, 80, 20, "上一页"));
        if (offset + visible < templates.size())
            buttonList.add(new GuiModernButton(11, left + 108, top + 188, 80, 20, "下一页"));
        buttonList.add(new GuiModernButton(0, left + 262, top + 188, 80, 20, "关闭"));
    }

    @Override
    protected void actionPerformed(GuiButton button) {
        if (button.id == 0) {
            mc.displayGuiScreen(null);
            return;
        }
        if (button.id == 10) {
            offset = Math.max(0, offset - ROWS);
            pendingDelete = -1;
            initGui();
            return;
        }
        if (button.id == 11) {
            offset += ROWS;
            pendingDelete = -1;
            initGui();
            return;
        }
        if (button.id >= 100 && button.id < 100 + ROWS) {
            send(C2SCopierTemplateAction.Operation.SELECT, offset + button.id - 100);
            return;
        }
        if (button.id >= 200 && button.id < 200 + ROWS) {
            int index = offset + button.id - 200;
            if (pendingDelete != index) {
                pendingDelete = index;
                initGui();
            } else send(C2SCopierTemplateAction.Operation.DELETE, index);
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
        int left = width / 2 - 180;
        int top = height / 2 - 105;
        drawRect(left, top, left + 360, top + 218, 0xF02B2F4A);
        drawRect(left, top, left + 360, top + 2, 0xFF7D8CFF);
        drawCenteredString(fontRendererObj, "复制器模板管理", width / 2, top + 12, 0xFFEEF0FF);
        if (templates.isEmpty())
            drawCenteredString(fontRendererObj, "暂无模板。右键生物可保存模板。", width / 2, top + 88, 0xFFB8C0E8);
        else if (pendingDelete >= 0)
            drawCenteredString(fontRendererObj, "再次点击“确认”永久删除该模板。", width / 2, top + 172, 0xFFFFC46B);
        super.drawScreen(mouseX, mouseY, partialTicks);
    }

    @Override
    public boolean doesGuiPauseGame() {
        return false;
    }
}
