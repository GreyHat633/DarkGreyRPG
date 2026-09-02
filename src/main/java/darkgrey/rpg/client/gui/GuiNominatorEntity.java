package darkgrey.rpg.client.gui;

import java.util.ArrayList;
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

/** Entity nominator: choose an accepted package, then an actor in its closure. */
public final class GuiNominatorEntity extends GuiScreen {

    private static final int PANEL_HEIGHT = 228;

    private final int entityId;
    private final UUID entityUuid;
    private String individual;
    private final List<String> groups = new ArrayList<String>();
    private long revision = -1L;
    private long catalogRevision = -1L;
    private NominatorCatalog catalog;
    private String displayName;
    private GuiTextField searchField;
    private GuiButton bindButton;
    private List<NominatorCatalog.PackageChoice> packages = Collections.emptyList();
    private List<NominatorStorySearch.ActorChoice> candidates = Collections.emptyList();
    private int packageChoice;
    private int candidateChoice;
    private String lastQuery = "";
    private String selectedStory;
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
        this.selectedStory = story;
        this.revision = revision;
    }

    public GuiNominatorEntity(int entityId, UUID entityUuid, String displayName, String entityType, String individual,
        List<String> groups, List<String> typeGroups, String story, long revision, long catalogRevision,
        NominatorCatalog catalog) {
        this(entityId, entityUuid, individual, groups, story, revision);
        this.displayName = displayName;
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

    @Override
    public void initGui() {
        buttonList.clear();
        packages = catalog.getPackageChoices();
        packageChoice = packageForStory(selectedStory);
        candidateChoice = 0;
        panelWidth = Math.min(520, width - 12);
        panelLeft = (width - panelWidth) / 2;
        panelTop = Math.max(2, (height - PANEL_HEIGHT) / 2);
        int contentLeft = panelLeft + 10;
        int contentWidth = panelWidth - 20;
        int gap = 6;
        int halfWidth = (contentWidth - gap) / 2;

        searchField = new GuiTextField(fontRendererObj, contentLeft, panelTop + 43, contentWidth, 17);
        searchField.setMaxStringLength(128);
        refreshCandidates();

        buttonList.add(new GuiModernButton(3, contentLeft, panelTop + 67, halfWidth, 17, "上一个包"));
        buttonList.add(new GuiModernButton(4, contentLeft + halfWidth + gap, panelTop + 67, halfWidth, 17, "下一个包"));
        buttonList.add(new GuiModernButton(5, contentLeft, panelTop + 109, halfWidth, 17, "上一个角色"));
        buttonList.add(new GuiModernButton(6, contentLeft + halfWidth + gap, panelTop + 109, halfWidth, 17, "下一个角色"));
        bindButton = new GuiModernButton(1, contentLeft, panelTop + 153, halfWidth, 18, "指名");
        bindButton.enabled = isBindable();
        buttonList.add(bindButton);
        buttonList.add(new GuiModernButton(2, contentLeft + halfWidth + gap, panelTop + 153, halfWidth, 18, "解除指名"));
        buttonList.add(new GuiModernButton(0, width / 2 - 35, panelTop + 204, 70, 18, "关闭"));
        setNavigationEnabled();
    }

    private int packageForStory(String storyId) {
        if (storyId != null) for (int index = 0; index < packages.size(); index++) if (storyId.equals(
            packages.get(index)
                .getStoryId()))
            return index;
        return 0;
    }

    private void refreshCandidates() {
        lastQuery = searchField == null ? "" : searchField.getText();
        if (packages.isEmpty()) candidates = Collections.emptyList();
        else candidates = NominatorStorySearch.actors(catalog, packages.get(packageChoice), lastQuery);
        if (candidateChoice >= candidates.size()) candidateChoice = 0;
        if (bindButton != null) bindButton.enabled = isBindable();
        setNavigationEnabled();
    }

    private void setNavigationEnabled() {
        for (GuiButton button : buttonList) {
            if (button.id == 3 || button.id == 4) button.enabled = packages.size() > 1;
            else if (button.id == 5 || button.id == 6) button.enabled = candidates.size() > 1;
        }
    }

    private boolean isBindable() {
        if (packages.isEmpty() || candidates.isEmpty()) return false;
        String type = candidates.get(candidateChoice)
            .getType();
        return "individual".equals(type) || "collective".equals(type);
    }

    @Override
    protected void actionPerformed(GuiButton button) {
        if (button.id == 0) {
            mc.displayGuiScreen(null);
        } else if (button.id == 3) {
            cyclePackage(-1);
        } else if (button.id == 4) {
            cyclePackage(1);
        } else if (button.id == 5) {
            cycleCandidate(-1);
        } else if (button.id == 6) {
            cycleCandidate(1);
        } else if (button.id == 1 && isBindable()) {
            NominatorStorySearch.ActorChoice actor = candidates.get(candidateChoice);
            if ("individual".equals(actor.getType())) sendSelection(
                actor.getId(),
                Collections.<String>emptyList(),
                packages.get(packageChoice)
                    .getStoryId());
            else sendSelection(
                null,
                Collections.singletonList(actor.getId()),
                packages.get(packageChoice)
                    .getStoryId());
            mc.displayGuiScreen(null);
        } else if (button.id == 2) {
            sendSelection(null, Collections.<String>emptyList(), null);
            mc.displayGuiScreen(null);
        }
    }

    private void cyclePackage(int direction) {
        if (packages.isEmpty()) return;
        packageChoice = (packageChoice + direction + packages.size()) % packages.size();
        candidateChoice = 0;
        refreshCandidates();
    }

    private void cycleCandidate(int direction) {
        if (candidates.isEmpty()) return;
        candidateChoice = (candidateChoice + direction + candidates.size()) % candidates.size();
        if (bindButton != null) bindButton.enabled = isBindable();
    }

    private void sendSelection(String individualId, List<String> groupIds, String storyId) {
        String packageId = storyId == null || packages.isEmpty() ? null
            : packages.get(packageChoice)
                .getPackageId();
        DialogueNetwork.CHANNEL.sendToServer(
            new C2SNominatorEntityBind(
                entityId,
                entityUuid,
                individualId,
                groupIds,
                storyId,
                revision,
                false,
                packageId,
                catalogRevision));
    }

    @Override
    public void drawScreen(int mouseX, int mouseY, float partialTicks) {
        if (searchField != null && !lastQuery.equals(searchField.getText())) refreshCandidates();
        drawDefaultBackground();
        drawRect(panelLeft, panelTop, panelLeft + panelWidth, panelTop + PANEL_HEIGHT, 0xF02B2F4A);
        drawCenteredString(fontRendererObj, "Nominator · 实体指名", width / 2, panelTop + 6, 0xFFEEF0FF);
        drawString(
            fontRendererObj,
            fit("目标：" + value(displayName), panelWidth - 20),
            panelLeft + 10,
            panelTop + 18,
            0xFFEEF0FF);
        drawString(fontRendererObj, "选择故事包后搜索角色 ID 或显示名", panelLeft + 10, panelTop + 31, 0xFFB8C0E8);
        searchField.drawTextBox();

        String packageText = packages.isEmpty() ? "无可用故事包" : packageLabel(packages.get(packageChoice));
        drawString(
            fontRendererObj,
            fit("故事包：" + packageText, panelWidth - 20),
            panelLeft + 10,
            panelTop + 91,
            0xFFEEF0FF);
        String actorText = candidates.isEmpty() ? "无匹配角色" : actorLabel(candidates.get(candidateChoice));
        drawString(
            fontRendererObj,
            fit("角色/群组：" + actorText, panelWidth - 20),
            panelLeft + 10,
            panelTop + 133,
            0xFFEEF0FF);
        drawString(
            fontRendererObj,
            fit("当前指名：" + currentBinding(), panelWidth - 20),
            panelLeft + 10,
            panelTop + 176,
            0xFFEEF0FF);
        drawString(fontRendererObj, fit(statusText(), panelWidth - 20), panelLeft + 10, panelTop + 190, 0xFFFFCC88);
        super.drawScreen(mouseX, mouseY, partialTicks);
    }

    private String packageLabel(NominatorCatalog.PackageChoice choice) {
        return choice.getPackageId() + " / " + choice.getDisplayName() + " [故事 " + choice.getStoryId() + "]";
    }

    private String actorLabel(NominatorStorySearch.ActorChoice actor) {
        return actor.getId() + " / " + value(actor.getDisplayName()) + " (" + typeLabel(actor.getType()) + ")";
    }

    private String typeLabel(String type) {
        return "collective".equals(type) ? "群组" : "individual".equals(type) ? "个体" : value(type);
    }

    private String statusText() {
        if (packages.isEmpty()) return "服务器未提供可浏览的故事包。";
        if (candidates.isEmpty()) return lastQuery.trim()
            .isEmpty() ? "所选故事包没有可绑定角色。" : "未找到匹配的角色 ID 或显示名。";
        if (!isBindable()) return "所选资源类型不支持实体指名。";
        return "已选择角色；包目录修订 " + catalogRevision + "，仅提交所选故事包闭包。";
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

    private String value(String value) {
        return value == null || value.trim()
            .isEmpty() ? "未知" : value;
    }

    private String fit(String value, int maxWidth) {
        return fontRendererObj.trimStringToWidth(value, maxWidth);
    }

    @Override
    protected void keyTyped(char typedChar, int keyCode) {
        if (searchField.textboxKeyTyped(typedChar, keyCode)) return;
        super.keyTyped(typedChar, keyCode);
    }

    @Override
    protected void mouseClicked(int mouseX, int mouseY, int button) {
        super.mouseClicked(mouseX, mouseY, button);
        searchField.mouseClicked(mouseX, mouseY, button);
    }

    @Override
    public boolean doesGuiPauseGame() {
        return false;
    }
}
