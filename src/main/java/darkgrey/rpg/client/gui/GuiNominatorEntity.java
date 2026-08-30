package darkgrey.rpg.client.gui;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.List;
import java.util.UUID;

import net.minecraft.client.gui.GuiButton;
import net.minecraft.client.gui.GuiScreen;
import net.minecraft.client.gui.GuiTextField;

import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.message.nominator.C2SNominatorEntityBind;
import darkgrey.rpg.nominator.NominatorCatalog;
import darkgrey.rpg.nominator.NominatorStorySearch;

/** Compact entity nominator: enter one exact Studio Actor ID and bind it. */
public final class GuiNominatorEntity extends GuiScreen {

    private static final int PANEL_HEIGHT = 184;

    private final int entityId;
    private final UUID entityUuid;
    private String individual;
    private final List<String> groups = new ArrayList<String>();
    private long revision = -1L;
    private NominatorCatalog catalog;
    private String displayName;
    private GuiTextField actorIdField;
    private GuiButton bindButton;
    private String lastQuery = "";
    private NominatorStorySearch.ActorChoice resolvedActor;
    private String resolutionMessage = "请输入完整 Actor ID。";
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
        List<String> groups, List<String> typeGroups, String story, long revision, NominatorCatalog catalog) {
        this(entityId, entityUuid, individual, groups, story, revision);
        this.displayName = displayName;
        this.catalog = catalog == null ? emptyCatalog() : catalog;
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
        buttonList.clear();
        panelWidth = Math.min(520, width - 12);
        panelLeft = (width - panelWidth) / 2;
        panelTop = Math.max(2, (height - PANEL_HEIGHT) / 2);
        int contentLeft = panelLeft + 10;
        int contentWidth = panelWidth - 20;
        int actionGap = 6;
        int actionWidth = (contentWidth - actionGap * 2) / 3;

        actorIdField = new GuiTextField(fontRendererObj, contentLeft, panelTop + 43, contentWidth, 17);
        actorIdField.setMaxStringLength(128);
        refreshResolution();
        bindButton = new GuiModernButton(1, contentLeft, panelTop + 86, actionWidth, 17, "指名");
        bindButton.enabled = isBindableActor();
        buttonList.add(bindButton);
        buttonList
            .add(new GuiModernButton(2, contentLeft + actionWidth + actionGap, panelTop + 86, actionWidth, 17, "解除指名"));
        buttonList.add(
            new GuiModernButton(0, contentLeft + (actionWidth + actionGap) * 2, panelTop + 86, actionWidth, 17, "关闭"));
    }

    private void refreshResolution() {
        String query = actorIdField == null ? "" : actorIdField.getText();
        resolvedActor = NominatorStorySearch.exactActor(catalog, query);
        lastQuery = query;
        if (catalog.getActors()
            .isEmpty()) resolutionMessage = "服务器角色目录为空，无法绑定。";
        else if (query == null || query.trim()
            .isEmpty()) resolutionMessage = "请输入完整 Actor ID；不支持部分或名称匹配。";
        else if (resolvedActor == null) resolutionMessage = "未找到精确 Actor ID；部分或名称匹配不会绑定。";
        else if (!isBindableActor()) resolutionMessage = "该 ID 无法指名。";
        else resolutionMessage = "已解析：" + resolvedActor.getId() + " · " + resolvedActor.getDisplayName();
        if (bindButton != null) bindButton.enabled = isBindableActor();
    }

    private boolean isBindableActor() {
        return resolvedActor != null
            && ("individual".equals(resolvedActor.getType()) || "collective".equals(resolvedActor.getType()));
    }

    @Override
    protected void actionPerformed(GuiButton button) {
        if (button.id == 0) {
            mc.displayGuiScreen(null);
        } else if (button.id == 1 && isBindableActor()) {
            if ("individual".equals(resolvedActor.getType()))
                sendSelection(resolvedActor.getId(), Collections.<String>emptyList(), resolvedActor.getStoryId());
            else sendSelection(null, Arrays.asList(resolvedActor.getId()), resolvedActor.getStoryId());
            mc.displayGuiScreen(null);
        } else if (button.id == 2) {
            sendSelection(null, Collections.<String>emptyList(), null);
            mc.displayGuiScreen(null);
        }
    }

    private void sendSelection(String individualId, List<String> groupIds, String storyId) {
        DialogueNetwork.CHANNEL.sendToServer(
            new C2SNominatorEntityBind(entityId, entityUuid, individualId, groupIds, storyId, revision, false));
    }

    @Override
    public void drawScreen(int mouseX, int mouseY, float partialTicks) {
        if (actorIdField != null && !lastQuery.equals(actorIdField.getText())) refreshResolution();
        drawDefaultBackground();
        drawRect(panelLeft, panelTop, panelLeft + panelWidth, panelTop + PANEL_HEIGHT, 0xF02B2F4A);
        drawCenteredString(fontRendererObj, "Nominator · 实体指名", width / 2, panelTop + 6, 0xFFEEF0FF);
        drawString(
            fontRendererObj,
            fit("目标：" + (displayName == null ? "未知" : displayName), panelWidth - 20),
            panelLeft + 10,
            panelTop + 18,
            0xFFEEF0FF);
        drawString(fontRendererObj, "输入服务器目录中的完整 Actor ID", panelLeft + 10, panelTop + 31, 0xFFB8C0E8);
        actorIdField.drawTextBox();
        drawString(fontRendererObj, fit(resolutionMessage, panelWidth - 20), panelLeft + 10, panelTop + 66, 0xFFFFCC88);

        String current = currentBinding();
        drawString(
            fontRendererObj,
            fit("当前指名：" + current, panelWidth - 20),
            panelLeft + 10,
            panelTop + 113,
            0xFFEEF0FF);
        drawString(
            fontRendererObj,
            fit(
                catalog.getActors()
                    .isEmpty() ? "服务器角色目录为空。" : "目录修订 " + revision + " · ID 定义来自 Studio。",
                panelWidth - 20),
            panelLeft + 10,
            panelTop + 132,
            0xFFB8C0E8);
        super.drawScreen(mouseX, mouseY, partialTicks);
    }

    private String currentBinding() {
        if (individual != null && !individual.trim()
            .isEmpty()) return individual;
        if (!groups.isEmpty()) return join(groups);
        return "无";
    }

    private String join(List<String> values) {
        StringBuilder result = new StringBuilder();
        for (String value : values) {
            if (result.length() > 0) result.append(", ");
            result.append(value);
        }
        return result.toString();
    }

    private String fit(String value, int maxWidth) {
        return fontRendererObj.trimStringToWidth(value, maxWidth);
    }

    @Override
    protected void keyTyped(char typedChar, int keyCode) {
        if (actorIdField.textboxKeyTyped(typedChar, keyCode)) return;
        super.keyTyped(typedChar, keyCode);
    }

    @Override
    protected void mouseClicked(int mouseX, int mouseY, int button) {
        super.mouseClicked(mouseX, mouseY, button);
        actorIdField.mouseClicked(mouseX, mouseY, button);
    }

    @Override
    public boolean doesGuiPauseGame() {
        return false;
    }
}
