package darkgrey.rpg.client.gui;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.UUID;

import net.minecraft.client.gui.GuiButton;
import net.minecraft.client.gui.GuiScreen;

import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.message.nominator.C2SNominatorEntityBind;
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

    @Override
    public void initGui() {
        buttonList.clear();
        panelWidth = Math.min(540, width - 12);
        int panelHeight = Math.min(340, height - 12);
        panelLeft = (width - panelWidth) / 2;
        panelTop = (height - panelHeight) / 2;
        browser = new NominatorBrowser(
            fontRendererObj,
            catalog,
            false,
            panelLeft + 8,
            panelTop + 24,
            panelWidth - 16,
            panelHeight - 80);
        bindButton = new GuiButton(
            1,
            panelLeft + panelWidth - 168,
            panelTop + panelHeight - 26,
            76,
            20,
            net.minecraft.client.resources.I18n.format("gui.darkgrey_rpg.bind"));
        buttonList.add(bindButton);
        buttonList.add(
            new GuiButton(
                2,
                panelLeft + 8,
                panelTop + panelHeight - 26,
                76,
                20,
                net.minecraft.client.resources.I18n.format("gui.darkgrey_rpg.unbind")));
        buttonList.add(
            new GuiButton(
                0,
                panelLeft + panelWidth - 84,
                panelTop + panelHeight - 26,
                76,
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
        if (button.id == 1 && row == null) return;
        DialogueNetwork.CHANNEL.sendToServer(
            new C2SNominatorEntityBind(
                entityId,
                entityUuid,
                button.id == 1 && "NPC".equals(row.type) ? row.id : null,
                button.id == 1 && "Group".equals(row.type) ? Collections.singletonList(row.id)
                    : Collections.<String>emptyList(),
                button.id == 1 ? row.source.getStoryId() : null,
                revision,
                false,
                button.id == 1 ? row.source.getPackageId() : null,
                catalogRevision));
        mc.displayGuiScreen(null);
    }

    @Override
    public void drawScreen(int mx, int my, float partial) {
        drawDefaultBackground();
        int h = Math.min(340, height - 12);
        drawRect(panelLeft, panelTop, panelLeft + panelWidth, panelTop + h, 0xEE303030);
        drawCenteredString(
            fontRendererObj,
            net.minecraft.client.resources.I18n.format("gui.darkgrey_rpg.entity_nominator"),
            width / 2,
            panelTop + 8,
            0xFFFFFF);
        browser.draw(mx, my);
        bindButton.enabled = browser.selected() != null;
        String binding = net.minecraft.client.resources.I18n.format("gui.darkgrey_rpg.binding")
            + (individual == null ? "" : individual)
            + " "
            + groups;
        drawString(
            fontRendererObj,
            fontRendererObj.trimStringToWidth(binding, panelWidth - 16),
            panelLeft + 8,
            panelTop + h - 42,
            0xCCCCCC);
        super.drawScreen(mx, my, partial);
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
