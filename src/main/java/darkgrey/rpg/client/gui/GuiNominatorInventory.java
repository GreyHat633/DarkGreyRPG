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
        resourceSearch = new GuiTextField(fontRendererObj, width / 2 - 175, height / 2 - 78, 350, 16);
        resourceSearch.setMaxStringLength(128);
        refreshResources();
        refreshInventorySlots();
        buttonList.add(new GuiModernButton(1, width / 2 - 145, height / 2 - 45, 140, 20, "浏览精确物品 ID"));
        buttonList.add(new GuiModernButton(2, width / 2 + 5, height / 2 - 45, 140, 20, "浏览精确群组"));
        buttonList.add(new GuiModernButton(3, width / 2 - 145, height / 2 - 10, 140, 20, "添加模糊群组"));
        GuiModernButton bind = new GuiModernButton(4, width / 2 + 5, height / 2 - 10, 140, 20, "绑定选择");
        bind.enabled = selectedSlot >= 0;
        buttonList.add(bind);
        buttonList.add(new GuiModernButton(10, width / 2 - 145, height / 2 + 55, 65, 20, "上一格"));
        buttonList.add(new GuiModernButton(11, width / 2 + 80, height / 2 + 55, 65, 20, "下一格"));
        buttonList.add(new GuiModernButton(0, width / 2 - 30, height / 2 + 82, 60, 20, "关闭"));
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
        drawRect(width / 2 - 190, height / 2 - 80, width / 2 + 190, height / 2 + 110, 0xF02B2F4A);
        drawCenteredString(fontRendererObj, "指名器：物品", width / 2, height / 2 - 70, 0xFFEEF0FF);
        resourceSearch.drawTextBox();
        ItemStack selected = selectedSlot >= 0
            && selectedSlot < Minecraft.getMinecraft().thePlayer.inventory.mainInventory.length
                ? Minecraft.getMinecraft().thePlayer.inventory.mainInventory[selectedSlot]
                : null;
        drawString(fontRendererObj, "搜索物品/群组 ID、名称或标签", width / 2 - 175, height / 2 - 60, 0xFFB8C0E8);
        drawString(
            fontRendererObj,
            "选择槽位 " + (selectedSlot < 0 ? "无" : String.valueOf(selectedSlot + 1))
                + ": "
                + (selected == null ? "空" : selected.getDisplayName()),
            width / 2 - 175,
            height / 2 - 40,
            0xFFEEF0FF);
        drawString(
            fontRendererObj,
            "精确物品 ID：" + (itemId == null ? "无" : itemId),
            width / 2 - 175,
            height / 2 - 18,
            0xFFEEF0FF);
        drawString(
            fontRendererObj,
            "精确群组：" + (exactGroup == null ? "无" : exactGroup),
            width / 2 - 175,
            height / 2 + 28,
            0xFFEEF0FF);
        drawString(fontRendererObj, "模糊群组（仅注册名）：" + fuzzyGroups, width / 2 - 175, height / 2 + 48, 0xFFB8C0E8);
        drawString(
            fontRendererObj,
            catalog.getItems()
                .isEmpty()
                && catalog.getItemGroups()
                    .isEmpty() ? "服务器物品目录为空" : "目录来源：服务器快照；绑定冲突会被服务端拒绝。",
            width / 2 - 175,
            height / 2 + 68,
            0xFFFFCC88);
        super.drawScreen(mouseX, mouseY, partialTicks);
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
