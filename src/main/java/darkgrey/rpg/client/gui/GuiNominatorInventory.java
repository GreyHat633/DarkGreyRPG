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
    private final UtilityWindowGeometry windowGeometry = new UtilityWindowGeometry(308, 240, 420, 350);
    private boolean geometryInitialized;

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
    private int operationLeft;
    private int armorLeft;

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
            guiTop + 28,
            xSize - 16,
            ySize - 164);
        browser.restore(old);
    }

    @Override
    public void initGui() {
        UtilityWindowChrome.open("item", windowGeometry, width, height, geometryInitialized);
        geometryInitialized = true;
        xSize = windowGeometry.width;
        ySize = windowGeometry.height;
        super.initGui();
        buttonList.clear();
        layoutControls();
        rebuildBrowser();
        buttonList.add(new GuiRpgButton(7, guiLeft + xSize - 98, guiTop + 3, 82, 20, "ID释放"));
        bindButton = new GuiRpgButton(6, guiLeft + operationLeft, guiTop + ySize - 94, 82, 18, "物品指名");
        buttonList.add(bindButton);
        buttonList.add(new GuiRpgButton(8, guiLeft + operationLeft, guiTop + ySize - 34, 82, 18, "物品解绑"));
    }

    private void layoutControls() {
        guiLeft = windowGeometry.x;
        guiTop = windowGeometry.y;
        xSize = windowGeometry.width;
        ySize = windowGeometry.height;
        operationLeft = 8;
        // Keep the 3x9 inventory and hotbar on one centered 162px block.
        inventoryLeft = Math.max(operationLeft + 82 + 18, (xSize - 162) / 2);
        armorLeft = (inventoryLeft + 162 + xSize - 16) / 2 - 8;
        for (int i = 0; i < inventorySlots.inventorySlots.size(); i++) {
            net.minecraft.inventory.Slot slot = (net.minecraft.inventory.Slot) inventorySlots.inventorySlots.get(i);
            if (i == ContainerNominatorInventory.NOMINATE_SLOT) {
                slot.xDisplayPosition = operationLeft + 33;
                slot.yDisplayPosition = ySize - 115;
            } else if (i == ContainerNominatorInventory.UNBIND_SLOT) {
                slot.xDisplayPosition = operationLeft + 33;
                slot.yDisplayPosition = ySize - 55;
            } else if (i >= ContainerNominatorInventory.PLAYER_MAIN_SLOT_START
                && i < ContainerNominatorInventory.PLAYER_MAIN_SLOT_END) {
                    int j = i - ContainerNominatorInventory.PLAYER_MAIN_SLOT_START;
                    slot.xDisplayPosition = inventoryLeft + (j % 9) * 18;
                    slot.yDisplayPosition = ySize - 109 + (j / 9) * 18;
                } else if (i >= ContainerNominatorInventory.HOTBAR_SLOT_START
                    && i < ContainerNominatorInventory.HOTBAR_SLOT_END) {
                        int j = i - ContainerNominatorInventory.HOTBAR_SLOT_START;
                        slot.xDisplayPosition = inventoryLeft + j * 18;
                        slot.yDisplayPosition = ySize - 51;
                    } else if (i >= ContainerNominatorInventory.ARMOR_SLOT_START
                        && i < ContainerNominatorInventory.ARMOR_SLOT_END) {
                            slot.xDisplayPosition = armorLeft;
                            slot.yDisplayPosition = ySize - 109
                                + (i - ContainerNominatorInventory.ARMOR_SLOT_START) * 18;
                        }
        }
        if (browser != null) browser.layout(guiLeft + 8, guiTop + 28, xSize - 16, ySize - 164);
        for (Object value : buttonList) {
            GuiButton button = (GuiButton) value;
            button.xPosition = guiLeft + (button.id == 7 ? xSize - 98 : operationLeft);
            button.yPosition = button.id == 7 ? guiTop + 3 : guiTop + ySize - (button.id == 6 ? 94 : 34);
        }
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
        UtilityWindowChrome.drawGrip(windowGeometry);
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
        drawOperationPanel(ySize - 132, ySize - 75);
        drawOperationPanel(ySize - 71, ySize - 14);
        for (Object obj : inventorySlots.inventorySlots) {
            net.minecraft.inventory.Slot slot = (net.minecraft.inventory.Slot) obj;
            int x = guiLeft + slot.xDisplayPosition, y = guiTop + slot.yDisplayPosition;
            drawRect(x - 1, y - 1, x + 17, y + 17, DgrUiPalette.SLOT_BORDER);
            drawRect(x, y, x + 16, y + 16, 0xFF666666);
        }
    }

    private void drawOperationPanel(int top, int bottom) {
        drawRect(
            guiLeft + operationLeft - 2,
            guiTop + top,
            guiLeft + operationLeft + 84,
            guiTop + bottom,
            DgrUiPalette.BORDER);
        drawRect(
            guiLeft + operationLeft - 1,
            guiTop + top + 1,
            guiLeft + operationLeft + 83,
            guiTop + bottom - 1,
            0xFF161616);
    }

    @Override
    protected void drawGuiContainerForegroundLayer(int mx, int my) {
        fontRendererObj
            .drawString("物品指名器", (xSize - fontRendererObj.getStringWidth("物品指名器")) / 2, 8, DgrUiPalette.TEXT);
        String inventoryTitle = "玩家背包";
        fontRendererObj.drawString(
            inventoryTitle,
            inventoryLeft + (162 - fontRendererObj.getStringWidth(inventoryTitle)) / 2,
            ySize - 123,
            DgrUiPalette.SECONDARY);
        fontRendererObj.drawString(
            "物品指名",
            operationLeft + (82 - fontRendererObj.getStringWidth("物品指名")) / 2,
            ySize - 128,
            DgrUiPalette.TEXT);
        fontRendererObj.drawString(
            "物品解绑",
            operationLeft + (82 - fontRendererObj.getStringWidth("物品解绑")) / 2,
            ySize - 67,
            DgrUiPalette.TEXT);
        fontRendererObj.drawString(
            "装备栏",
            armorLeft + 8 - fontRendererObj.getStringWidth("装备栏") / 2,
            ySize - 123,
            DgrUiPalette.SECONDARY);
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
        if (key == 1) {
            super.keyTyped(c, key);
            return;
        }
        if (key == mc.gameSettings.keyBindInventory.getKeyCode() && !browser.search.isFocused()) {
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
        if (slotAt(x, y)) {
            browser.search.setFocused(false);
            super.mouseClicked(x, y, b);
            return;
        }
        if (mc.thePlayer.inventory.getItemStack() == null && !UtilityWindowChrome.overButton(buttonList, x, y)
            && windowGeometry.begin(x, y, b)) return;
        browser.click(x, y, b);
        super.mouseClicked(x, y, b);
    }

    @Override
    protected void mouseClickMove(int x, int y, int b, long elapsed) {
        if (windowGeometry.active()) {
            if (controls.modal() || controls.pending) {
                windowGeometry.end();
                return;
            }
            windowGeometry.move(x, y);
            layoutControls();
            return;
        }
        if (!controls.modal() && !controls.pending) super.mouseClickMove(x, y, b, elapsed);
    }

    @Override
    protected void mouseMovedOrUp(int x, int y, int b) {
        if (b == 0 && windowGeometry.active()) {
            windowGeometry.end();
            UtilityWindowChrome.save("item", windowGeometry, width, height);
            return;
        }
        if (!controls.modal() && !controls.pending) super.mouseMovedOrUp(x, y, b);
    }

    @Override
    public void handleMouseInput() {
        super.handleMouseInput();
        if (!controls.modal() && !controls.pending && !windowGeometry.active()) browser.scroll(
            org.lwjgl.input.Mouse.getEventX() * width / mc.displayWidth,
            height - org.lwjgl.input.Mouse.getEventY() * height / mc.displayHeight - 1,
            org.lwjgl.input.Mouse.getEventDWheel());
    }

    private boolean slotAt(int x, int y) {
        for (Object value : inventorySlots.inventorySlots) {
            net.minecraft.inventory.Slot slot = (net.minecraft.inventory.Slot) value;
            int left = guiLeft + slot.xDisplayPosition, top = guiTop + slot.yDisplayPosition;
            if (x >= left - 1 && x < left + 17 && y >= top - 1 && y < top + 17) return true;
        }
        return false;
    }

    @Override
    public void onGuiClosed() {
        windowGeometry.end();
        if (geometryInitialized) UtilityWindowChrome.save("item", windowGeometry, width, height);
        super.onGuiClosed();
    }

    @Override
    public boolean doesGuiPauseGame() {
        return false;
    }
}
