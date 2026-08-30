package darkgrey.rpg.client.gui;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.GuiButton;
import net.minecraft.client.gui.GuiScreen;
import net.minecraft.client.gui.GuiTextField;
import net.minecraft.item.ItemStack;

import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.message.nominator.C2SNominatorInventoryBind;
import darkgrey.rpg.nominator.NominatorCatalog;
import darkgrey.rpg.nominator.NominatorStorySearch;

/** Inventory nominator view; the server captures the held stack and explains matching semantics. */
public final class GuiNominatorInventory extends GuiScreen {

    private String itemId;
    private String exactGroup;
    private final List<String> fuzzyGroups = new ArrayList<String>();
    private int selectedSlot;
    private List<NominatorStorySearch.ItemChoice> items = Collections.emptyList();
    private List<NominatorStorySearch.ItemChoice> itemGroups = Collections.emptyList();
    private int itemChoice;
    private int exactGroupChoice;
    private int fuzzyGroupChoice;
    private final List<Integer> availableSlots = new ArrayList<Integer>();
    private GuiTextField resourceSearch;
    private String lastResourceQuery = "";
    private NominatorCatalog catalog;
    private long revision = -1L;
    private int panelLeft;
    private int panelTop;
    private int panelWidth;

    public GuiNominatorInventory() {
        this(
            new NominatorCatalog(
                Collections.<NominatorCatalog.Story>emptyList(),
                Collections.<NominatorCatalog.Actor>emptyList(),
                Collections.<NominatorCatalog.Item>emptyList(),
                Collections.<NominatorCatalog.Item>emptyList()),
            -1L,
            -1);
    }

    public GuiNominatorInventory(NominatorCatalog catalog, long revision, int selectedSlot) {
        this.catalog = catalog;
        this.revision = revision;
        this.selectedSlot = selectedSlot;
    }

    @Override
    public void initGui() {
        buttonList.clear();
        panelWidth = Math.min(520, width - 12);
        panelLeft = (width - panelWidth) / 2;
        panelTop = Math.max(4, (height - 228) / 2);
        int contentLeft = panelLeft + 10;
        int contentWidth = panelWidth - 20;
        int columnGap = 10;
        int columnWidth = (contentWidth - columnGap) / 2;
        int rightColumn = contentLeft + columnWidth + columnGap;
        resourceSearch = new GuiTextField(fontRendererObj, contentLeft, panelTop + 32, contentWidth, 18);
        resourceSearch.setMaxStringLength(128);
        refreshResources();
        refreshInventorySlots();
        buttonList.add(new GuiModernButton(1, contentLeft, panelTop + 76, columnWidth, 18, "浏览物品"));
        buttonList.add(new GuiModernButton(2, rightColumn, panelTop + 76, columnWidth, 18, "浏览精确群组"));
        buttonList.add(new GuiModernButton(3, contentLeft, panelTop + 112, columnWidth, 18, "添加模糊群组"));
        GuiModernButton bind = new GuiModernButton(4, rightColumn, panelTop + 112, columnWidth, 18, "绑定到当前槽位");
        bind.enabled = selectedSlot >= 0;
        buttonList.add(bind);
        buttonList.add(new GuiModernButton(10, contentLeft, panelTop + 160, columnWidth, 18, "上一个槽位"));
        buttonList.add(new GuiModernButton(11, rightColumn, panelTop + 160, columnWidth, 18, "下一个槽位"));
        buttonList.add(new GuiModernButton(0, width / 2 - 35, panelTop + 202, 70, 18, "关闭"));
    }

    @Override
    protected void actionPerformed(GuiButton button) {
        if (button.id == 0) mc.displayGuiScreen(null);
        else if (button.id == 10) cycleSlot(-1);
        else if (button.id == 11) cycleSlot(1);
        else if (button.id == 1 && !items.isEmpty()) {
            itemId = items.get(itemChoice++ % items.size())
                .getId();
        } else if (button.id == 2 && !itemGroups.isEmpty()) {
            exactGroup = itemGroups.get(exactGroupChoice++ % itemGroups.size())
                .getId();
        } else if (button.id == 3 && !itemGroups.isEmpty()) {
            String group = itemGroups.get(fuzzyGroupChoice++ % itemGroups.size())
                .getId();
            if (!fuzzyGroups.contains(group)) fuzzyGroups.add(group);
        } else if (button.id == 4 && selectedSlot >= 0) {
            DialogueNetwork.CHANNEL
                .sendToServer(new C2SNominatorInventoryBind(selectedSlot, itemId, exactGroup, fuzzyGroups, revision));
            mc.displayGuiScreen(null);
        }
    }

    @Override
    public void drawScreen(int mouseX, int mouseY, float partialTicks) {
        if (resourceSearch != null && !lastResourceQuery.equals(resourceSearch.getText())) refreshResources();
        drawDefaultBackground();
        drawRect(panelLeft, panelTop, panelLeft + panelWidth, panelTop + 228, 0xF02B2F4A);
        drawCenteredString(fontRendererObj, "Nominator · 物品指名", width / 2, panelTop + 8, 0xFFEEF0FF);
        drawString(fontRendererObj, "搜索物品或群组的 ID、名称与标签", panelLeft + 10, panelTop + 21, 0xFFB8C0E8);
        resourceSearch.drawTextBox();
        ItemStack selected = selectedSlot >= 0
            && selectedSlot < Minecraft.getMinecraft().thePlayer.inventory.mainInventory.length
                ? Minecraft.getMinecraft().thePlayer.inventory.mainInventory[selectedSlot]
                : null;
        int contentLeft = panelLeft + 10;
        int contentWidth = panelWidth - 20;
        int columnWidth = (contentWidth - 10) / 2;
        int rightColumn = contentLeft + columnWidth + 10;
        drawString(fontRendererObj, fit("精确物品：" + value(itemId), columnWidth), contentLeft, panelTop + 64, 0xFFEEF0FF);
        drawString(
            fontRendererObj,
            fit("精确群组：" + value(exactGroup), columnWidth),
            rightColumn,
            panelTop + 64,
            0xFFEEF0FF);
        drawString(fontRendererObj, fit("模糊群组：" + fuzzyGroups, contentWidth), contentLeft, panelTop + 100, 0xFFB8C0E8);
        drawString(
            fontRendererObj,
            fit(
                "当前槽位 " + (selectedSlot < 0 ? "无" : String.valueOf(selectedSlot + 1))
                    + " · "
                    + (selected == null ? "空" : selected.getDisplayName()),
                contentWidth),
            contentLeft,
            panelTop + 148,
            0xFFEEF0FF);
        drawString(
            fontRendererObj,
            fit(
                catalog.getItems()
                    .isEmpty()
                    && catalog.getItemGroups()
                        .isEmpty() ? "服务器物品目录为空" : "服务器目录修订 " + revision + " · 冲突时会拒绝绑定",
                contentWidth),
            contentLeft,
            panelTop + 184,
            0xFFFFCC88);
        super.drawScreen(mouseX, mouseY, partialTicks);
    }

    private String value(String value) {
        return value == null || value.isEmpty() ? "无" : value;
    }

    private String fit(String value, int maxWidth) {
        return fontRendererObj.trimStringToWidth(value, maxWidth);
    }

    private void refreshResources() {
        lastResourceQuery = resourceSearch == null ? "" : resourceSearch.getText();
        items = NominatorStorySearch.items(catalog, lastResourceQuery, false);
        itemGroups = NominatorStorySearch.items(catalog, lastResourceQuery, true);
    }

    private void refreshInventorySlots() {
        availableSlots.clear();
        ItemStack[] inventory = Minecraft.getMinecraft().thePlayer.inventory.mainInventory;
        for (int slot = 0; slot < inventory.length; slot++)
            if (inventory[slot] != null && inventory[slot].getItem() != darkgrey.rpg.content.ModItems.nominator)
                availableSlots.add(slot);
        selectedSlot = availableSlots.isEmpty() ? -1 : availableSlots.get(0);
    }

    private void cycleSlot(int direction) {
        if (availableSlots.isEmpty()) return;
        int current = availableSlots.indexOf(selectedSlot);
        if (current < 0) current = 0;
        current = (current + direction + availableSlots.size()) % availableSlots.size();
        selectedSlot = availableSlots.get(current);
    }

    @Override
    protected void keyTyped(char typedChar, int keyCode) {
        if (resourceSearch.textboxKeyTyped(typedChar, keyCode)) return;
        super.keyTyped(typedChar, keyCode);
    }

    @Override
    protected void mouseClicked(int mouseX, int mouseY, int button) {
        super.mouseClicked(mouseX, mouseY, button);
        resourceSearch.mouseClicked(mouseX, mouseY, button);
    }

    @Override
    public boolean doesGuiPauseGame() {
        return false;
    }
}
