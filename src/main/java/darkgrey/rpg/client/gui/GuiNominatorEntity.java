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

/** Compact entity nominator: story browser/search and individual/group selection. */
public final class GuiNominatorEntity extends GuiScreen {

    private final int entityId;
    private final UUID entityUuid;
    private String story;
    private String individual;
    private final List<String> groups = new ArrayList<String>();
    private List<NominatorStorySearch.ActorChoice> choices = Collections.emptyList();
    private List<NominatorStorySearch.ActorChoice> individuals = Collections.emptyList();
    private List<NominatorStorySearch.ActorChoice> collectiveGroups = Collections.emptyList();
    private List<NominatorStorySearch.StoryChoice> stories = Collections.emptyList();
    private int storyChoice;
    private int individualChoice;
    private int groupChoice;
    private GuiTextField searchField;
    private String lastQuery = "";
    private long revision = -1L;
    private NominatorCatalog catalog;
    private String displayName;
    private String entityType;
    private final List<String> typeGroups = new ArrayList<String>();

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
        this.story = story;
        this.revision = revision;
    }

    public GuiNominatorEntity(int entityId, UUID entityUuid, String displayName, String entityType, String individual,
        List<String> groups, List<String> typeGroups, String story, long revision, NominatorCatalog catalog) {
        this(entityId, entityUuid, individual, groups, story, revision);
        this.displayName = displayName;
        this.entityType = entityType;
        if (typeGroups != null) this.typeGroups.addAll(typeGroups);
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
        searchField = new GuiTextField(fontRendererObj, width / 2 - 165, height / 2 - 78, 330, 16);
        searchField.setMaxStringLength(128);
        refreshChoices();
        buttonList.add(new GuiModernButton(1, width / 2 - 145, height / 2 - 45, 130, 20, "浏览故事"));
        buttonList.add(new GuiModernButton(2, width / 2 + 15, height / 2 - 45, 130, 20, "选择个体"));
        buttonList.add(new GuiModernButton(3, width / 2 - 145, height / 2 - 10, 130, 20, "添加群组"));
        buttonList.add(new GuiModernButton(4, width / 2 + 15, height / 2 - 10, 130, 20, "绑定选择"));
        buttonList.add(new GuiModernButton(5, width / 2 - 145, height / 2 + 25, 130, 20, "清除个体"));
        buttonList.add(new GuiModernButton(6, width / 2 + 15, height / 2 + 25, 130, 20, "移除群组"));
        buttonList.add(new GuiModernButton(7, width / 2 - 145, height / 2 + 50, 130, 20, "解除全部"));
        buttonList.add(new GuiModernButton(8, width / 2 + 15, height / 2 + 50, 130, 20, "转移选择"));
        buttonList.add(new GuiModernButton(9, width / 2 - 145, height / 2 + 75, 130, 20, "添加类型群组"));
        buttonList.add(new GuiModernButton(10, width / 2 + 15, height / 2 + 75, 130, 20, "移除类型群组"));
        buttonList.add(new GuiModernButton(0, width / 2 - 30, height / 2 + 102, 60, 20, "关闭"));
    }

    private void refreshChoices() {
        String query = searchField == null ? "" : searchField.getText();
        choices = NominatorStorySearch.actors(catalog, story, query);
        individuals = new ArrayList<NominatorStorySearch.ActorChoice>();
        collectiveGroups = new ArrayList<NominatorStorySearch.ActorChoice>();
        for (NominatorStorySearch.ActorChoice choice : choices) {
            if ("individual".equals(choice.getType())) individuals.add(choice);
            if ("collective".equals(choice.getType())) collectiveGroups.add(choice);
        }
        stories = NominatorStorySearch.stories(catalog, searchField == null ? "" : searchField.getText());
        lastQuery = query;
    }

    @Override
    protected void actionPerformed(GuiButton button) {
        if (button.id == 0) mc.displayGuiScreen(null);
        else {
            refreshChoices();
            if (button.id == 1 && !stories.isEmpty()) {
                story = stories.get(storyChoice++ % stories.size())
                    .getId();
                refreshChoices();
            } else if (button.id == 2 && !individuals.isEmpty())
                individual = individuals.get(individualChoice++ % individuals.size())
                    .getId();
            else if (button.id == 3 && !collectiveGroups.isEmpty()) {
                String value = collectiveGroups.get(groupChoice++ % collectiveGroups.size())
                    .getId();
                if (!groups.contains(value)) groups.add(value);
            } else if (button.id == 4) {
                sendSelection(false);
                mc.displayGuiScreen(null);
            } else if (button.id == 5) {
                individual = null;
            } else if (button.id == 6 && !groups.isEmpty()) {
                groups.remove(groups.size() - 1);
            } else if (button.id == 7) {
                individual = null;
                groups.clear();
                sendSelection(false);
                mc.displayGuiScreen(null);
            } else if (button.id == 8) {
                sendSelection(true);
                mc.displayGuiScreen(null);
            } else if ((button.id == 9 || button.id == 10) && !collectiveGroups.isEmpty()) {
                String group = collectiveGroups.get(groupChoice++ % collectiveGroups.size())
                    .getId();
                DialogueNetwork.CHANNEL.sendToServer(
                    new C2SNominatorEntityBind(
                        entityId,
                        entityUuid,
                        individual,
                        groups,
                        story,
                        revision,
                        false,
                        true,
                        group,
                        button.id == 9));
                if (button.id == 9 && !typeGroups.contains(group)) typeGroups.add(group);
                if (button.id == 10) typeGroups.remove(group);
            }
        }
    }

    private void sendSelection(boolean transfer) {
        DialogueNetwork.CHANNEL.sendToServer(
            new C2SNominatorEntityBind(entityId, entityUuid, individual, groups, story, revision, transfer));
    }

    @Override
    public void drawScreen(int mouseX, int mouseY, float partialTicks) {
        if (searchField != null && !lastQuery.equals(searchField.getText())) refreshChoices();
        drawDefaultBackground();
        drawRect(width / 2 - 180, height / 2 - 95, width / 2 + 180, height / 2 + 130, 0xF02B2F4A);
        drawCenteredString(fontRendererObj, "Nominator: entity", width / 2, height / 2 - 70, 0xFFEEF0FF);
        drawString(
            fontRendererObj,
            "Target: " + (displayName == null ? "unknown" : displayName)
                + " / "
                + (entityType == null ? "unknown" : entityType),
            width / 2 - 165,
            height / 2 - 88,
            0xFFEEF0FF);
        searchField.drawTextBox();
        drawString(fontRendererObj, "搜索故事/角色 ID、名称、备注或标签", width / 2 - 165, height / 2 - 60, 0xFFB8C0E8);
        drawString(
            fontRendererObj,
            "故事筛选：" + (story == null ? "全部" : story),
            width / 2 - 165,
            height / 2 - 58,
            0xFFEEF0FF);
        drawString(
            fontRendererObj,
            "个体：" + (individual == null ? "无" : individual),
            width / 2 - 165,
            height / 2 - 30,
            0xFFEEF0FF);
        drawString(fontRendererObj, "群组：" + groups.toString(), width / 2 - 165, height / 2 + 10, 0xFFEEF0FF);
        drawString(fontRendererObj, "精确类型群组：" + typeGroups, width / 2 - 165, height / 2 + 28, 0xFFB8C0E8);
        drawString(
            fontRendererObj,
            catalog.getActors()
                .isEmpty() ? "服务器角色目录为空" : "角色目录：服务器快照",
            width / 2 - 165,
            height / 2 + 42,
            0xFFB8C0E8);
        drawString(fontRendererObj, "绑定冲突或目录过期时，服务端会拒绝请求；请重新打开。", width / 2 - 165, height / 2 + 56, 0xFFFFCC88);
        super.drawScreen(mouseX, mouseY, partialTicks);
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
