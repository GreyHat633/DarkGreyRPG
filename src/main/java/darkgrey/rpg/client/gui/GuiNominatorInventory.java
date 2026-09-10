package darkgrey.rpg.client.gui;

import java.util.Collections;

import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.GuiButton;
import net.minecraft.client.gui.inventory.GuiContainer;
import net.minecraft.entity.player.EntityPlayer;

import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.message.nominator.C2SNominatorInventoryBind;
import darkgrey.rpg.nominator.NominatorCatalog;
import darkgrey.rpg.nominator.container.ContainerNominatorInventory;

/** Inventory nominator view backed by the server-authoritative target container. */
public final class GuiNominatorInventory extends GuiContainer {

    private NominatorCatalog catalog;
    private long revision;
    private long catalogRevision = -1L;
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
        if (mc != null) initGui();
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

    private NominatorBrowser browser;

    @Override
    public void initGui() {
        xSize = Math.min(360, width - 12);
        ySize = Math.min(330, height - 12);
        super.initGui();
        buttonList.clear();
        for (int i = 0; i < inventorySlots.inventorySlots.size(); i++) {
            net.minecraft.inventory.Slot slot = (net.minecraft.inventory.Slot) inventorySlots.inventorySlots.get(i);
            slot.yDisplayPosition = (i == 0 || i >= 28 ? 177 : 119 + ((i - 1) / 9) * 18) + ySize - 228;
        }
        browser = new NominatorBrowser(
            fontRendererObj,
            catalog,
            true,
            guiLeft + 8,
            guiTop + 24,
            xSize - 16,
            ySize - 144);
        bindButton = new GuiButton(
            6,
            guiLeft + xSize - 88,
            guiTop + ySize - 26,
            80,
            20,
            net.minecraft.client.resources.I18n.format("gui.darkgrey_rpg.bind"));
        buttonList.add(bindButton);
        buttonList.add(
            new GuiButton(
                0,
                guiLeft + 8,
                guiTop + ySize - 26,
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
        darkgrey.rpg.client.NominatorGlobalSearch.Row row = browser.selected();
        if (button.id == 6 && row != null && hasTarget()) {
            DialogueNetwork.CHANNEL.sendToServer(
                new C2SNominatorInventoryBind(
                    row.source.getPackageId(),
                    "Item".equals(row.type) ? row.id : null,
                    "Item Group".equals(row.type) ? row.id : null,
                    revision,
                    catalogRevision));
            bindButton.enabled = false;
        }
    }

    private boolean hasTarget() {
        return ((ContainerNominatorInventory) inventorySlots).getTargetInventory()
            .getStackInSlot(0) != null;
    }

    @Override
    public void drawScreen(int mx, int my, float partial) {
        bindButton.enabled = browser.selected() != null && hasTarget();
        super.drawScreen(mx, my, partial);
    }

    @Override
    protected void drawGuiContainerBackgroundLayer(float partial, int mx, int my) {
        drawRect(guiLeft, guiTop, guiLeft + xSize, guiTop + ySize, 0xFF383838);
        browser.draw(mx, my);
        for (Object obj : inventorySlots.inventorySlots) {
            net.minecraft.inventory.Slot slot = (net.minecraft.inventory.Slot) obj;
            int x = guiLeft + slot.xDisplayPosition, y = guiTop + slot.yDisplayPosition;
            drawRect(x - 1, y - 1, x + 17, y + 17, 0xFFCCCCCC);
            drawRect(x - 1, y - 1, x + 16, y + 16, 0xFF171717);
            drawRect(x, y, x + 16, y + 16, 0xFF777777);
        }
    }

    @Override
    protected void drawGuiContainerForegroundLayer(int mx, int my) {
        fontRendererObj
            .drawString(net.minecraft.client.resources.I18n.format("gui.darkgrey_rpg.item_nominator"), 8, 8, 0xFFFFFF);
        fontRendererObj.drawString(
            net.minecraft.client.resources.I18n.format("gui.darkgrey_rpg.inventory"),
            8,
            ySize - 120,
            0xCCCCCC);
        fontRendererObj.drawString(
            net.minecraft.client.resources.I18n.format("gui.darkgrey_rpg.nominator_slot"),
            xSize - 98,
            ySize - 65,
            0xDDCCAA);
    }

    @Override
    protected void keyTyped(char c, int key) {
        if (!browser.search.textboxKeyTyped(c, key)) super.keyTyped(c, key);
    }

    @Override
    protected void mouseClicked(int x, int y, int b) {
        browser.click(x, y, b);
        super.mouseClicked(x, y, b);
    }

    @Override
    public void handleMouseInput() {
        super.handleMouseInput();
        browser.scroll(
            org.lwjgl.input.Mouse.getEventX() * width / mc.displayWidth,
            height - org.lwjgl.input.Mouse.getEventY() * height / mc.displayHeight - 1,
            org.lwjgl.input.Mouse.getEventDWheel());
    }

    @Override
    public boolean doesGuiPauseGame() {
        return false;
    }
}
