package darkgrey.rpg.client.gui;

import java.util.Collections;
import java.util.List;

import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.GuiButton;
import net.minecraft.client.gui.GuiTextField;
import net.minecraft.client.gui.inventory.GuiContainer;
import net.minecraft.entity.player.EntityPlayer;
import net.minecraft.item.ItemStack;

import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.message.nominator.C2SNominatorInventoryBind;
import darkgrey.rpg.nominator.NominatorCatalog;
import darkgrey.rpg.nominator.NominatorStorySearch;
import darkgrey.rpg.nominator.container.ContainerNominatorInventory;

/** Inventory nominator view backed by the server-authoritative target container. */
public final class GuiNominatorInventory extends GuiContainer {

    private NominatorCatalog catalog;
    private long revision;
    private long catalogRevision = -1L;
    private List<NominatorCatalog.PackageChoice> packages = Collections.emptyList();
    private List<NominatorStorySearch.ItemChoice> resources = Collections.emptyList();
    private int packageChoice;
    private int resourceChoice;
    private boolean browsingGroups;
    private String selectedItem;
    private String selectedGroup;
    private String lastQuery = "";
    private GuiTextField searchField;
    private GuiButton bindButton;

    public GuiNominatorInventory() {
        this(emptyCatalog(), -1L, -1);
    }

    public GuiNominatorInventory(NominatorCatalog catalog, long revision, int selectedSlot) {
        this(newContainer(), catalog, revision, -1L);
    }

    public GuiNominatorInventory(ContainerNominatorInventory container) {
        this(container, emptyCatalog(), -1L, -1L);
    }

    public GuiNominatorInventory(ContainerNominatorInventory container, NominatorCatalog catalog, long revision,
        long catalogRevision) {
        super(container);
        this.catalog = catalog == null ? emptyCatalog() : catalog;
        this.revision = revision;
        this.catalogRevision = catalogRevision;
    }

    public void applyServerSnapshot(NominatorCatalog catalog, long revision, long catalogRevision) {
        this.catalog = catalog == null ? emptyCatalog() : catalog;
        this.revision = revision;
        this.catalogRevision = catalogRevision;
        packages = this.catalog.getPackageChoices();
        packageChoice = 0;
        resourceChoice = 0;
        refreshResources();
    }

    private static ContainerNominatorInventory newContainer() {
        EntityPlayer player = Minecraft.getMinecraft().thePlayer;
        return new ContainerNominatorInventory(player);
    }

    private static NominatorCatalog emptyCatalog() {
        return new NominatorCatalog(
            Collections.<NominatorCatalog.Story>emptyList(),
            Collections.<NominatorCatalog.Actor>emptyList(),
            Collections.<NominatorCatalog.Item>emptyList(),
            Collections.<NominatorCatalog.Item>emptyList());
    }

    @Override
    public void initGui() {
        xSize = 360;
        ySize = 228;
        super.initGui();
        buttonList.clear();
        packages = catalog.getPackageChoices();
        packageChoice = 0;
        resourceChoice = 0;

        searchField = new GuiTextField(fontRendererObj, guiLeft + 8, guiTop + 42, 344, 18);
        searchField.setMaxStringLength(128);
        refreshResources();
        buttonList.add(new GuiModernButton(1, guiLeft + 8, guiTop + 20, 110, 18, "上一个包"));
        buttonList.add(new GuiModernButton(2, guiLeft + 124, guiTop + 20, 110, 18, "下一个包"));
        buttonList.add(new GuiModernButton(3, guiLeft + 240, guiTop + 20, 112, 18, "浏览物品组"));
        buttonList.add(new GuiModernButton(4, guiLeft + 8, guiTop + 64, 110, 18, "上一个条目"));
        buttonList.add(new GuiModernButton(5, guiLeft + 124, guiTop + 64, 110, 18, "下一个条目"));
        bindButton = new GuiModernButton(6, guiLeft + 240, guiTop + 64, 112, 18, "绑定目标");
        buttonList.add(bindButton);
        buttonList.add(new GuiModernButton(0, guiLeft + 270, guiTop + 204, 82, 18, "关闭"));
        refreshButtonState();
    }

    @Override
    protected void actionPerformed(GuiButton button) {
        if (button.id == 0) {
            mc.displayGuiScreen(null);
        } else if (button.id == 1 || button.id == 2) {
            cyclePackage(button.id == 1 ? -1 : 1);
        } else if (button.id == 3) {
            browsingGroups = !browsingGroups;
            button.displayString = browsingGroups ? "浏览物品" : "浏览物品组";
            resourceChoice = 0;
            refreshResources();
        } else if (button.id == 4 || button.id == 5) {
            cycleResource(button.id == 4 ? -1 : 1);
        } else if (button.id == 6 && isBindable()) {
            NominatorCatalog.PackageChoice choice = packages.get(packageChoice);
            DialogueNetwork.CHANNEL.sendToServer(
                new C2SNominatorInventoryBind(
                    choice.getPackageId(),
                    selectedItem,
                    selectedGroup,
                    revision,
                    catalogRevision));
            // Main's server handler closes the live container after capture/bind.
        }
    }

    private void cyclePackage(int direction) {
        if (packages.isEmpty()) return;
        packageChoice = (packageChoice + direction + packages.size()) % packages.size();
        resourceChoice = 0;
        refreshResources();
    }

    private void cycleResource(int direction) {
        if (resources.isEmpty()) return;
        resourceChoice = (resourceChoice + direction + resources.size()) % resources.size();
        chooseResource();
        refreshButtonState();
    }

    private void refreshResources() {
        lastQuery = searchField == null ? "" : searchField.getText();
        if (packages.isEmpty()) resources = Collections.emptyList();
        else resources = NominatorStorySearch.items(catalog, packages.get(packageChoice), lastQuery, browsingGroups);
        if (resourceChoice >= resources.size()) resourceChoice = 0;
        chooseResource();
        refreshButtonState();
    }

    private void chooseResource() {
        if (resources.isEmpty()) {
            if (browsingGroups) selectedGroup = null;
            else selectedItem = null;
            return;
        }
        NominatorStorySearch.ItemChoice choice = resources.get(resourceChoice);
        if (browsingGroups) {
            selectedGroup = choice.getId();
            selectedItem = null;
        } else {
            selectedItem = choice.getId();
            selectedGroup = null;
        }
    }

    private boolean isBindable() {
        ItemStack target = ((ContainerNominatorInventory) inventorySlots).getTargetInventory()
            .getStackInSlot(0);
        return target != null && !resources.isEmpty() && (selectedItem != null || selectedGroup != null);
    }

    private void refreshButtonState() {
        if (bindButton != null) bindButton.enabled = isBindable();
        for (GuiButton button : buttonList) if (button.id == 1 || button.id == 2) button.enabled = packages.size() > 1;
        for (GuiButton button : buttonList) if (button.id == 4 || button.id == 5) button.enabled = resources.size() > 1;
    }

    @Override
    public void drawScreen(int mouseX, int mouseY, float partialTicks) {
        if (searchField != null && !lastQuery.equals(searchField.getText())) refreshResources();
        refreshButtonState();
        super.drawScreen(mouseX, mouseY, partialTicks);
        if (searchField != null) searchField.drawTextBox();
    }

    @Override
    protected void drawGuiContainerBackgroundLayer(float partialTicks, int mouseX, int mouseY) {
        drawRect(guiLeft, guiTop, guiLeft + xSize, guiTop + ySize, 0xF02B2F4A);
    }

    @Override
    protected void drawGuiContainerForegroundLayer(int mouseX, int mouseY) {
        drawString(fontRendererObj, "Nominator · 物品指名", 8, 8, 0xFFEEF0FF);
        drawString(fontRendererObj, "搜索 ID、名称或标签", 240, 8, 0xFFB8C0E8);
        drawString(fontRendererObj, packageText(), 8, 86, 0xFFB8C0E8);
        drawString(fontRendererObj, resourceText(), 8, 98, 0xFFEEF0FF);
        drawString(fontRendererObj, "玩家背包", 178, 121, 0xFFB8C0E8);
        drawString(fontRendererObj, "目标槽", 286, 165, 0xFFFFCC88);
        drawString(
            fontRendererObj,
            resources.isEmpty() ? "服务器目录为空或无匹配条目" : "指名修订 " + revision + " · 包目录修订 " + catalogRevision,
            8,
            110,
            0xFFFFCC88);
    }

    private String packageText() {
        if (packages.isEmpty()) return "故事包：无可用包";
        NominatorCatalog.PackageChoice choice = packages.get(packageChoice);
        return fit("故事包：" + choice.getPackageId() + " / " + choice.getDisplayName(), 314);
    }

    private String resourceText() {
        if (resources.isEmpty()) return browsingGroups ? "物品组：无匹配条目" : "物品：无匹配条目";
        NominatorStorySearch.ItemChoice choice = resources.get(resourceChoice);
        return fit((browsingGroups ? "物品组：" : "物品：") + choice.getId() + " / " + choice.getDisplayName(), 314);
    }

    private String fit(String text, int width) {
        return fontRendererObj.trimStringToWidth(text, width);
    }

    @Override
    protected void keyTyped(char typedChar, int keyCode) {
        if (searchField != null && searchField.textboxKeyTyped(typedChar, keyCode)) return;
        super.keyTyped(typedChar, keyCode);
    }

    @Override
    protected void mouseClicked(int mouseX, int mouseY, int button) {
        super.mouseClicked(mouseX, mouseY, button);
        if (searchField != null) searchField.mouseClicked(mouseX, mouseY, button);
    }

    @Override
    public boolean doesGuiPauseGame() {
        return false;
    }
}
