package darkgrey.rpg.client.gui;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.UUID;

import net.minecraft.client.gui.GuiButton;
import net.minecraft.client.gui.GuiScreen;

import darkgrey.rpg.nominator.NominatorCatalog;

/** Entity nominator: choose an accepted package, then an actor in its closure. */
public final class GuiNominatorEntity extends GuiScreen {

    private final int entityId;
    private final UUID entityUuid;
    private String individual;
    private final List<String> groups = new ArrayList<String>();
    private long revision = -1L;
    private long catalogRevision = -1L;
    private NominatorCatalog catalog;
    private GuiButton bindButton;
    private int panelLeft;
    private int panelTop;
    private int panelWidth;

    public GuiNominatorEntity(int entityId, UUID entityUuid) {
        this.entityId = entityId;
        this.entityUuid = entityUuid;
        this.catalog = emptyCatalog();
    }

    public GuiNominatorEntity(int entityId, UUID entityUuid, String individual, List<String> groups, String story,
        long revision) {
        this(entityId, entityUuid);
        this.individual = individual;
        if (groups != null) this.groups.addAll(groups);
        this.revision = revision;
    }

    public GuiNominatorEntity(int entityId, UUID entityUuid, String displayName, String entityType, String individual,
        List<String> groups, List<String> typeGroups, String story, long revision, long catalogRevision,
        NominatorCatalog catalog) {
        this(entityId, entityUuid, individual, groups, story, revision);
        this.catalogRevision = catalogRevision;
        this.catalog = catalog == null ? emptyCatalog() : catalog;
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
    private String currentGroups = "";
    private int panelHeight;

    public void acceptResult(net.minecraft.nbt.NBTTagCompound data, NominatorCatalog catalog) {
        if (!controls.accept(data)) return;
        this.catalog = catalog;
        individual = data.getString("individual");
        currentGroups = data.getString("groups");
        revision = controls.revision;
        catalogRevision = controls.catalogRevision;
        rebuildBrowser();
    }

    private void rebuildBrowser() {
        NominatorBrowser old = browser;
        browser = new NominatorBrowser(
            fontRendererObj,
            catalog,
            false,
            panelLeft + 8,
            panelTop + 24,
            panelWidth - 16,
            panelHeight - 108);
        browser.restore(old);
    }

    @Override
    public void initGui() {
        buttonList.clear();
        panelWidth = Math.min(540, width - 12);
        panelHeight = Math.min(340, height - 12);
        panelLeft = (width - panelWidth) / 2;
        panelTop = (height - panelHeight) / 2;
        rebuildBrowser();
        buttonList.add(new GuiRpgButton(3, panelLeft + 8, panelTop + panelHeight - 78, 76, 20, "ID释放"));
        bindButton = new GuiRpgButton(1, panelLeft + panelWidth - 90, panelTop + panelHeight - 78, 82, 20, "实体指名");
        buttonList.add(bindButton);
        buttonList.add(new GuiRpgButton(2, panelLeft + 8, panelTop + panelHeight - 26, 76, 20, "实体解绑"));
        buttonList.add(new GuiRpgButton(0, panelLeft + panelWidth - 84, panelTop + panelHeight - 26, 76, 20, "关闭"));
    }

    @Override
    public void updateScreen() {
        super.updateScreen();
        if (!controls.initialized && !controls.pending) send("sync");
    }

    private void send(String op) {
        net.minecraft.nbt.NBTTagCompound q = new net.minecraft.nbt.NBTTagCompound();
        q.setString("op", op);
        q.setInteger("entity", entityId);
        q.setString("entityUuid", entityUuid.toString());
        darkgrey.rpg.client.NominatorGlobalSearch.Row row = browser.selected();
        if (row != null) {
            q.setString("resource", row.id);
            q.setString("type", row.type);
            q.setString("package", row.source.getPackageId());
        }
        controls.begin(q, "release".equals(op) ? "release" : "");
    }

    @Override
    protected void actionPerformed(GuiButton button) {
        if (controls.modal() || controls.pending) return;
        if (button.id == 0) {
            mc.displayGuiScreen(null);
            return;
        }
        if (!controls.initialized) return;
        if (button.id == 2) send("unbind");
        if (browser.selected() != null) {
            if (button.id == 1) send("bind");
            if (button.id == 3) send("release");
        }
    }

    @Override
    public void drawScreen(int mx, int my, float partial) {
        drawDefaultBackground();
        drawRect(panelLeft, panelTop, panelLeft + panelWidth, panelTop + panelHeight, DgrUiPalette.PANEL);
        drawCenteredString(fontRendererObj, "实体指名器", width / 2, panelTop + 8, DgrUiPalette.TEXT);
        browser.draw(mx, my);
        String binding = "当前实体：" + (individual == null ? "" : individual) + " " + currentGroups;
        drawString(
            fontRendererObj,
            fontRendererObj.trimStringToWidth(binding, panelWidth - 16),
            panelLeft + 8,
            panelTop + panelHeight - 51,
            DgrUiPalette.SECONDARY);
        drawString(
            fontRendererObj,
            fontRendererObj.trimStringToWidth(controls.message, panelWidth - 16),
            panelLeft + 8,
            panelTop + panelHeight - 40,
            DgrUiPalette.SECONDARY);
        for (Object obj : buttonList) {
            GuiButton b = (GuiButton) obj;
            b.enabled = !controls.pending && !controls.modal()
                && (b.id == 0 || controls.initialized)
                && (b.id != 1 && b.id != 3 || browser.selected() != null);
        }
        super.drawScreen(mx, my, partial);
        controls.draw(width, height, mx, my);
    }

    @Override
    protected void keyTyped(char c, int key) {
        if (controls.modal()) {
            if (key == 1) controls.cancel();
            return;
        }
        if (controls.pending && key != 1) return;
        if (key == 1 || !browser.search.textboxKeyTyped(c, key)) super.keyTyped(c, key);
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
