package darkgrey.rpg.client.gui;

import java.util.Collections;

import net.minecraft.client.Minecraft;
import net.minecraft.client.gui.GuiButton;
import net.minecraft.client.gui.inventory.GuiContainer;
import net.minecraft.entity.player.EntityPlayer;

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
        if (mc != null) rebuildBrowser();
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

    private final NominatorControls controls = new NominatorControls();
    private int inventoryLeft;

    public void acceptResult(net.minecraft.nbt.NBTTagCompound data, NominatorCatalog catalog) {
        if (!controls.accept(data)) return;
        this.catalog = catalog;
        revision = controls.revision;
        catalogRevision = controls.catalogRevision;
        rebuildBrowser();
    }

    private void rebuildBrowser() {
        NominatorBrowser old = browser;
        browser = new NominatorBrowser(
            fontRendererObj,
            catalog,
            true,
            guiLeft + 8,
            guiTop + 24,
            xSize - 16,
            ySize - 160);
        browser.restore(old);
    }

    @Override
    public void initGui() {
        xSize = Math.min(420, width - 12);
        ySize = Math.min(350, height - 12);
        super.initGui();
        buttonList.clear();
        inventoryLeft = Math.max(8, (xSize - 104 - 162) / 2);
        for (int i = 0; i < inventorySlots.inventorySlots.size(); i++) {
            net.minecraft.inventory.Slot slot = (net.minecraft.inventory.Slot) inventorySlots.inventorySlots.get(i);
            if (i < ContainerNominatorInventory.PLAYER_SLOT_START) {
                slot.xDisplayPosition = xSize - 54;
                slot.yDisplayPosition = ySize - 114 + i * 58;
            } else {
                int j = i - ContainerNominatorInventory.PLAYER_SLOT_START;
                slot.xDisplayPosition = inventoryLeft + (j % 9) * 18;
                slot.yDisplayPosition = ySize - 109 + (j < 27 ? (j / 9) * 18 : 58);
            }
        }
        rebuildBrowser();
        buttonList.add(new GuiRpgButton(7, guiLeft + 8, guiTop + ySize - 132, 76, 20, "ID释放"));
        bindButton = new GuiRpgButton(6, guiLeft + xSize - 90, guiTop + ySize - 94, 82, 18, "指名");
        buttonList.add(bindButton);
        buttonList.add(new GuiRpgButton(8, guiLeft + xSize - 90, guiTop + ySize - 36, 82, 18, "物品解绑"));
    }

    @Override
    public void updateScreen() {
        super.updateScreen();
        if (!controls.initialized && !controls.pending) send("sync");
    }

    private void send(String op) {
        net.minecraft.nbt.NBTTagCompound q = new net.minecraft.nbt.NBTTagCompound();
        q.setBoolean("items", true);
        q.setInteger("window", inventorySlots.windowId);
        q.setString("op", op);
        darkgrey.rpg.client.NominatorGlobalSearch.Row row = browser.selected();
        if (row != null) {
            q.setString("resource", row.id);
            q.setString("type", row.type);
            q.setString("package", row.source.getPackageId());
        }
        controls.begin(
            q,
            "release".equals(op) ? "release"
                : "bind".equals(op) && row != null && "Item Group".equals(row.type) ? "group" : "");
    }

    @Override
    protected void actionPerformed(GuiButton button) {
        if (controls.pending || controls.modal() || !controls.initialized) return;
        if (button.id == 6 && browser.selected() != null && hasTarget(ContainerNominatorInventory.NOMINATE_SLOT))
            send("bind");
        if (button.id == 7 && browser.selected() != null) send("release");
        if (button.id == 8 && hasTarget(ContainerNominatorInventory.UNBIND_SLOT)) send("unbind");
    }

    private boolean hasTarget(int slot) {
        return ((ContainerNominatorInventory) inventorySlots).getTargetInventory()
            .getStackInSlot(slot) != null;
    }

    @Override
    public void drawScreen(int mx, int my, float partial) {
        for (Object obj : buttonList) {
            GuiButton button = (GuiButton) obj;
            button.enabled = controls.initialized && !controls.pending
                && !controls.modal()
                && (button.id == 8 ? hasTarget(ContainerNominatorInventory.UNBIND_SLOT) : browser.selected() != null);
            if (button.id == 6) button.enabled &= hasTarget(ContainerNominatorInventory.NOMINATE_SLOT);
        }
        super.drawScreen(mx, my, partial);
        // GuiContainer leaves item lighting enabled; modal chrome uses the same unlit palette as entity modals.
        net.minecraft.client.renderer.RenderHelper.disableStandardItemLighting();
        org.lwjgl.opengl.GL11.glDisable(org.lwjgl.opengl.GL11.GL_LIGHTING);
        org.lwjgl.opengl.GL11.glColor4f(1F, 1F, 1F, 1F);
        org.lwjgl.opengl.GL11.glDisable(org.lwjgl.opengl.GL11.GL_DEPTH_TEST);
        controls.draw(width, height, mx, my);
        org.lwjgl.opengl.GL11.glEnable(org.lwjgl.opengl.GL11.GL_DEPTH_TEST);
    }

    @Override
    protected void drawGuiContainerBackgroundLayer(float partial, int mx, int my) {
        drawRect(guiLeft, guiTop, guiLeft + xSize, guiTop + ySize, DgrUiPalette.PANEL);
        browser.draw(mx, my);
        drawRect(
            guiLeft + inventoryLeft - 3,
            guiTop + ySize - 112,
            guiLeft + inventoryLeft + 165,
            guiTop + ySize - 31,
            DgrUiPalette.SUB_PANEL);
        drawRect(
            guiLeft + xSize - 96,
            guiTop + ySize - 130,
            guiLeft + xSize - 5,
            guiTop + ySize - 15,
            DgrUiPalette.BORDER);
        drawRect(
            guiLeft + xSize - 95,
            guiTop + ySize - 129,
            guiLeft + xSize - 6,
            guiTop + ySize - 16,
            DgrUiPalette.SUB_PANEL);
        for (Object obj : inventorySlots.inventorySlots) {
            net.minecraft.inventory.Slot slot = (net.minecraft.inventory.Slot) obj;
            int x = guiLeft + slot.xDisplayPosition, y = guiTop + slot.yDisplayPosition;
            drawRect(x - 1, y - 1, x + 17, y + 17, DgrUiPalette.SLOT_BORDER);
            drawRect(x, y, x + 16, y + 16, 0xFF666666);
        }
    }

    @Override
    protected void drawGuiContainerForegroundLayer(int mx, int my) {
        fontRendererObj.drawString("物品指名器", 8, 8, DgrUiPalette.TEXT);
        fontRendererObj.drawString("玩家背包", Math.max(inventoryLeft, 96), ySize - 123, DgrUiPalette.SECONDARY);
        fontRendererObj.drawString("物品指名", xSize - 86, ySize - 126, DgrUiPalette.TEXT);
        fontRendererObj.drawString("物品解绑", xSize - 86, ySize - 68, DgrUiPalette.TEXT);
        fontRendererObj.drawString(
            fontRendererObj.trimStringToWidth(controls.message, xSize - 16),
            8,
            ySize - 12,
            DgrUiPalette.SECONDARY);
    }

    @Override
    protected void keyTyped(char c, int key) {
        if (controls.modal()) {
            if (key == 1) controls.cancel();
            return;
        }
        if (controls.pending && key != 1) return;
        if (key == 1 || key == mc.gameSettings.keyBindInventory.getKeyCode() && !browser.search.isFocused()) {
            super.keyTyped(c, key);
            return;
        }
        if (!browser.search.textboxKeyTyped(c, key)) super.keyTyped(c, key);
    }

    @Override
    protected void mouseClicked(int x, int y, int b) {
        if (controls.modal()) {
            controls.click(x, y, b);
            return;
        }
        if (controls.pending) return;
        browser.click(x, y, b);
        super.mouseClicked(x, y, b);
    }

    @Override
    protected void mouseClickMove(int x, int y, int b, long elapsed) {
        if (!controls.modal() && !controls.pending) super.mouseClickMove(x, y, b, elapsed);
    }

    @Override
    protected void mouseMovedOrUp(int x, int y, int b) {
        if (!controls.modal() && !controls.pending) super.mouseMovedOrUp(x, y, b);
    }

    @Override
    public void handleMouseInput() {
        super.handleMouseInput();
        if (!controls.modal() && !controls.pending) browser.scroll(
            org.lwjgl.input.Mouse.getEventX() * width / mc.displayWidth,
            height - org.lwjgl.input.Mouse.getEventY() * height / mc.displayHeight - 1,
            org.lwjgl.input.Mouse.getEventDWheel());
    }

    @Override
    public boolean doesGuiPauseGame() {
        return false;
    }
}
