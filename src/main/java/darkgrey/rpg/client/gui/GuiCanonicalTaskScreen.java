package darkgrey.rpg.client.gui;

import java.util.ArrayList;
import java.util.List;

import net.minecraft.client.gui.GuiButton;
import net.minecraft.client.gui.GuiScreen;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

import org.lwjgl.input.Mouse;

import darkgrey.rpg.client.CanonicalTaskClientStore;

/**
 * Read-only presentation of the server-produced Task cache.
 *
 * <p>
 * Only explicit submit intents are sent; progress remains server-owned. The cache is consumed
 * when the screen opens and whenever its monotonic revision changes, so an
 * already-open journal reflects a server push on the next frame.
 * </p>
 */
public final class GuiCanonicalTaskScreen extends GuiScreen {

    private static final int ROW_HEIGHT = 24;
    private static final int STACKED_ROW_HEIGHT = 20;

    private NBTTagCompound snapshot = new NBTTagCompound();
    private long snapshotRevision = Long.MIN_VALUE;
    private String selectedTaskId;
    private int taskScroll;
    private int detailScroll;
    private boolean completedView;
    private static Object sessionConnection;
    private static final java.util.Set<String> expandedStories = new java.util.LinkedHashSet<String>();
    private static String rememberedTask;
    private static int rememberedScroll, rememberedDetailScroll;
    private static boolean rememberedCompleted;
    private static String rememberedHistoryContext, rememberedDetailTask;
    private static NBTTagList rememberedHistoryRows;
    private static NBTTagCompound rememberedHistoryPage, rememberedDetailPage, rememberedDetailRecord;
    private static int rememberedHistoryCursor, rememberedDetailCursor;
    private NBTTagList historyRows = new NBTTagList();
    private NBTTagCompound detailRecord;
    private boolean historyLoading = true, detailLoading;
    private String historyContextKey;
    private int detailHeight;
    private boolean historyFailed, detailFailed;
    private int historyCursor;
    private NBTTagCompound historyPage;

    private final List<Integer> historyCursors = new ArrayList<Integer>(), detailCursors = new ArrayList<Integer>();
    private int detailCursor;
    private String detailTaskId;
    private NBTTagCompound detailPage;
    private GuiRpgButton completedButton, trackingButton;
    private String trackingMessage = "";
    private String detailCacheKey;
    private List<DetailBlock> cachedDetails = new ArrayList<DetailBlock>();
    private List<String> itemTooltip;

    private static final class DetailBlock {

        final String text;
        final ItemSlotStrip items;
        String objectiveId;
        int top, bottom;

        DetailBlock(String text) {
            this.text = text;
            this.items = null;
        }

        DetailBlock(ItemSlotStrip items) {
            this(items, null);
        }

        DetailBlock(ItemSlotStrip items, String counter) {
            this.text = counter;
            this.items = items;
        }
    }

    private void detailText(List<DetailBlock> blocks, String text, int width) {
        for (Object line : fontRendererObj.listFormattedStringToWidth(text, width))
            blocks.add(new DetailBlock((String) line));
    }

    private final UtilityWindowGeometry windowGeometry = new UtilityWindowGeometry(280, 180, 620, 300);
    private boolean geometryInitialized;
    private CanonicalTaskLayout currentLayout;

    @Override
    public void initGui() {
        UtilityWindowChrome.open("task", windowGeometry, width, height, geometryInitialized);
        geometryInitialized = true;
        updateWindowGeometry();
        if (sessionConnection != mc.getNetHandler()) {
            sessionConnection = mc.getNetHandler();
            expandedStories.clear();
            rememberedTask = null;
            rememberedScroll = rememberedDetailScroll = 0;
            rememberedCompleted = false;
            rememberedHistoryRows = null;
            rememberedHistoryPage = rememberedDetailPage = rememberedDetailRecord = null;
            rememberedHistoryContext = rememberedDetailTask = null;
        }
        if (snapshotRevision == Long.MIN_VALUE) {
            selectedTaskId = rememberedTask;
            taskScroll = rememberedScroll;
            detailScroll = rememberedDetailScroll;
            completedView = rememberedCompleted;
            String currentHistory = darkgrey.rpg.client.TaskPresentationPages
                .identity(darkgrey.rpg.client.TaskPresentationPages.historyContext());
            if (completedView && currentHistory.equals(rememberedHistoryContext) && rememberedHistoryRows != null) {
                historyContextKey = rememberedHistoryContext;
                historyRows = (NBTTagList) rememberedHistoryRows.copy();
                historyPage = rememberedHistoryPage;
                historyCursor = rememberedHistoryCursor;
                historyLoading = historyPage == null;
                detailTaskId = rememberedDetailTask;
                detailPage = rememberedDetailPage;
                detailRecord = rememberedDetailRecord == null ? null : (NBTTagCompound) rememberedDetailRecord.copy();
                detailCursor = rememberedDetailCursor;
            }
        }
        refreshCache();
        completedButton = new GuiRpgButton(1, 0, 0, 80, 20, "已完成");
        trackingButton = new GuiRpgButton(2, 0, 0, 116, 20, "追踪");
        buttonList.add(completedButton);
        buttonList.add(trackingButton);
        updateFooterButtons();
    }

    private void updateWindowGeometry() {
        currentLayout = new CanonicalTaskLayout(
            windowGeometry.x,
            windowGeometry.y,
            windowGeometry.width,
            windowGeometry.height);
        updateFooterButtons();
    }

    @Override
    public void updateScreen() {
        // Cache reads are local and replace the detached snapshot atomically.
        // There is intentionally no request or timer-driven network polling.
        refreshCache();
    }

    private void refreshCache() {
        if (completedView) {
            NBTTagCompound context = darkgrey.rpg.client.TaskPresentationPages.historyContext();
            String key = darkgrey.rpg.client.TaskPresentationPages.identity(context);
            if (!key.equals(historyContextKey)) {
                historyContextKey = key;
                historyRows = new NBTTagList();
                historyCursor = 0;
                historyPage = null;
                historyLoading = true;
                detailRecord = null;
                detailTaskId = null;
            }
            if (historyLoading) {
                NBTTagCompound page = darkgrey.rpg.client.TaskPresentationPages.page(context, historyCursor);
                if (page != null && page.getBoolean("restart")) {
                    historyContextKey = null;
                    return;
                }
                if (page != null && page.hasKey("error")) {
                    trackingMessage = page.getString("error");
                    historyLoading = false;
                    historyFailed = true;
                } else if (page != null) {
                    for (int i = 0; i < page.getTagList("rows", 10)
                        .tagCount(); i++) {
                        NBTTagCompound row = page.getTagList("rows", 10)
                            .getCompoundTagAt(i);
                        if (findTask(historyRows, taskId(row)) == null) historyRows.appendTag(row.copy());
                    }
                    historyPage = page;
                    historyLoading = false;
                    detailCacheKey = null;
                }
            }
        }
        long revision = CanonicalTaskClientStore.getRevision();
        if (revision == snapshotRevision) return;
        NBTTagCompound next = CanonicalTaskClientStore.getSnapshot();
        snapshot = next == null ? new NBTTagCompound() : next;
        snapshotRevision = revision;
        NBTTagList tasks = tasks();
        if (selectedTaskId != null && findTask(tasks, selectedTaskId) != null) return;
        if (completedView && historyLoading) return;
        selectedTaskId = null;
        taskScroll = 0;
        detailScroll = 0;
    }

    private CanonicalTaskLayout layout() {
        return currentLayout;
    }

    private NBTTagList tasks() {
        return completedView ? historyRows : snapshot.getTagList("tasks", 10);
    }

    private static NBTTagCompound findTask(NBTTagList tasks, String id) {
        if (id == null) return null;
        for (int index = 0; index < tasks.tagCount(); index++) {
            NBTTagCompound task = tasks.getCompoundTagAt(index);
            if (id.equals(taskId(task))) return task;
        }
        return null;
    }

    private static String taskId(NBTTagCompound task) {
        String id = task.getString("id");
        return id.length() == 0 ? task.getString("title") : id;
    }

    @Override
    public void handleMouseInput() {
        int popupX = Mouse.getEventX() * width / mc.displayWidth;
        int popupY = height - Mouse.getEventY() * height / mc.displayHeight - 1;

        super.handleMouseInput();
        if (windowGeometry.active()) return;
        int wheel = Mouse.getEventDWheel();
        if (wheel == 0) return;
        CanonicalTaskLayout layout = layout();
        int mouseX = Mouse.getEventX() * width / mc.displayWidth;
        int mouseY = height - Mouse.getEventY() * height / mc.displayHeight - 1;
        if (ItemCandidatePopover.wheel(mouseX, mouseY, wheel)) return;
        int amount = wheel < 0 ? 2 : -2;
        if (layout.containsList(mouseX, mouseY)) {
            taskScroll = Math.max(0, taskScroll + amount);
            int visible = Math.max(1, (layout.listBottom - layout.listTop) / (listRowHeight(layout)));
            if (amount > 0 && completedView
                && taskScroll + visible >= TaskStoryRows.flatten(tasks(), expandedStories)
                    .size() - 2
                && historyPage != null
                && historyPage.getInteger("next") < historyPage.getInteger("total")) {
                historyCursor = historyPage.getInteger("next");
                historyLoading = true;
            }
        } else if (layout.containsDetail(mouseX, mouseY)) {
            detailScroll = Math.max(0, detailScroll + amount * 12);
            if (amount > 0 && completedView
                && detailScroll + layout.detailBottom - layout.detailTop >= detailHeight - 24
                && detailPage != null
                && detailPage.getInteger("next") < detailPage.getInteger("total")) {
                detailCursor = detailPage.getInteger("next");
                detailLoading = true;
            }
        }
    }

    @Override
    protected void mouseClicked(int mouseX, int mouseY, int button) {

        if (ItemCandidatePopover.click(mouseX, mouseY, button)) return;
        if (windowGeometry.begin(mouseX, mouseY, button)) return;
        super.mouseClicked(mouseX, mouseY, button);
        if (button != 0) return;
        if ((historyFailed || detailFailed) && mouseY >= layout().panelBottom - 24
            && mouseX >= layout().panelLeft + 94
            && mouseX < layout().panelRight - 120) {
            NBTTagCompound context = darkgrey.rpg.client.TaskPresentationPages.historyContext();
            if (detailFailed) {
                context.setInteger("operation", 3);
                context.setString("history", detailTaskId);
                darkgrey.rpg.client.TaskPresentationPages.retry(context, detailCursor);
                detailLoading = true;
            } else {
                darkgrey.rpg.client.TaskPresentationPages.retry(context, historyCursor);
                historyLoading = true;
            }
            historyFailed = detailFailed = false;
            trackingMessage = "";
            return;
        }
        if (layout().containsDetail(mouseX, mouseY)) {
            for (DetailBlock block : cachedDetails)
                if (block.objectiveId != null && mouseY >= block.top && mouseY < block.bottom) {
                    NBTTagCompound selected = findTask(tasks(), selectedTaskId);
                    if (selected != null) darkgrey.rpg.client.TaskTrackerClient.focus(selected, block.objectiveId);
                    return;
                }
            for (DetailBlock block : cachedDetails) if (block.items != null && block.items.moreAt(mouseX, mouseY)) {
                mc.displayGuiScreen(new GuiItemCandidates(this, block.items.source()));
                return;
            }
        }
        // Item submission is completed only by the server's real entity
        // interaction event. The journal has no remote-submit hit target.
        CanonicalTaskLayout layout = layout();
        if (!layout.containsList(mouseX, mouseY)) return;
        int rowHeight = listRowHeight(layout);
        int row = (mouseY - layout.listTop) / rowHeight;
        if (row >= Math.max(1, (layout.listBottom - layout.listTop) / rowHeight)) return;
        int index = taskScroll + row;
        List<NBTTagCompound> rows = TaskStoryRows.flatten(tasks(), expandedStories);
        if (index < 0 || index >= rows.size()) return;
        NBTTagCompound rowData = rows.get(index);
        if (rowData.getBoolean("story_header")) {
            String story = TaskStoryRows.key(rowData);
            if (!expandedStories.add(story)) {
                expandedStories.remove(story);
                NBTTagCompound selected = findTask(tasks(), selectedTaskId);
                if (selected != null && TaskStoryRows.key(selected)
                    .equals(story)) {
                    selectedTaskId = null;
                    detailCacheKey = null;
                    cachedDetails.clear();
                    detailScroll = detailHeight = 0;
                    ItemCandidatePopover.close();
                    detailRecord = null;
                    detailTaskId = null;
                }
            }
            return;
        }
        String id = taskId(rowData);
        if (!id.equals(selectedTaskId)) detailScroll = 0;
        if (!id.equals(selectedTaskId)) ItemCandidatePopover.close();
        selectedTaskId = id;
    }

    @Override
    public void drawScreen(int mouseX, int mouseY, float partialTicks) {

        refreshCache();
        CanonicalTaskLayout layout = layout();
        drawDefaultBackground();
        drawPanel(layout);
        darkgrey.rpg.client.gui.DgrUiText.centered(
            fontRendererObj,
            "任务",
            (layout.panelLeft + layout.panelRight) / 2,
            layout.panelTop + 9,
            DgrUiPalette.SELECTED_BORDER);
        drawList(layout, mouseX, mouseY);
        ItemCandidatePopover.begin();
        drawDetails(layout, mouseX, mouseY);
        updateFooterButtons();
        if (!trackingMessage.isEmpty())
            fontRendererObj.drawString(trackingMessage, layout.panelLeft + 10, layout.panelTop - 12, DgrUiPalette.TEXT);
        UtilityWindowChrome.drawGrip(windowGeometry);
        super.drawScreen(mouseX, mouseY, partialTicks);
        List<String> candidateTooltip = ItemCandidatePopover.draw(width, height, mouseX, mouseY);
        if (candidateTooltip != null) itemTooltip = candidateTooltip;
        if (itemTooltip != null) drawHoveringText(itemTooltip, mouseX, mouseY, fontRendererObj);
        if (historyFailed || detailFailed) fontRendererObj
            .drawString("加载失败，点击重试", layout.panelLeft + 96, layout.panelBottom - 18, DgrUiPalette.SECONDARY);
        else if (historyLoading && completedView || detailLoading && selectedTaskId != null)
            fontRendererObj.drawString("正在加载…", layout.panelLeft + 96, layout.panelBottom - 18, DgrUiPalette.SECONDARY);

    }

    private void updateFooterButtons() {
        if (completedButton == null || currentLayout == null) return;
        CanonicalTaskLayout footer = layout();
        completedButton.xPosition = footer.panelLeft + 8;
        completedButton.yPosition = footer.panelBottom - 24;
        completedButton.displayString = completedView ? "进行中" : "已完成";
        completedButton.selected = completedView;
        trackingButton.xPosition = footer.panelRight - trackingButton.width - 12;
        trackingButton.yPosition = completedButton.yPosition;
        trackingButton.visible = !completedView;
        NBTTagCompound selected = findTask(tasks(), selectedTaskId);
        boolean tracked = selected != null && darkgrey.rpg.client.TaskTrackerClient.selected()
            .contains(darkgrey.rpg.client.TaskTrackerClient.identity(selected));
        trackingButton.enabled = selected != null;
        trackingButton.selected = tracked;
        trackingButton.displayString = (tracked ? "取消追踪" : "追踪") + "  "
            + darkgrey.rpg.client.TaskTrackerClient.selected()
                .size()
            + " / 3";
    }

    @Override
    protected void actionPerformed(GuiButton button) {
        if (!button.enabled) return;
        if (button.id == 1) {
            completedView = !completedView;
            historyFailed = detailFailed = false;
            detailLoading = false;
            taskScroll = detailScroll = 0;
            ItemCandidatePopover.close();
            historyCursor = 0;
            historyRows = new NBTTagList();
            historyContextKey = null;
            historyLoading = true;
            historyCursors.clear();
            historyPage = null;
            detailCacheKey = null;
            selectedTaskId = null;
            trackingMessage = "";
            snapshotRevision = Long.MIN_VALUE;
            refreshCache();
        } else if (button.id == 2 && !completedView) {
            NBTTagCompound selected = findTask(tasks(), selectedTaskId);
            if (selected != null)
                trackingMessage = darkgrey.rpg.client.TaskTrackerClient.toggle(selected) ? "" : "最多追踪3个任务，请先取消一个";
        }
        updateFooterButtons();
    }

    private void drawPanel(CanonicalTaskLayout layout) {
        drawRect(layout.panelLeft, layout.panelTop, layout.panelRight, layout.panelBottom, DgrUiPalette.WINDOW_PANEL);
        drawRect(
            layout.panelLeft,
            layout.panelTop,
            layout.panelRight,
            layout.panelTop + 1,
            DgrUiPalette.SELECTED_BORDER);
        drawRect(layout.panelLeft, layout.panelBottom - 1, layout.panelRight, layout.panelBottom, DgrUiPalette.BORDER);
        drawRect(layout.panelLeft, layout.panelTop, layout.panelLeft + 1, layout.panelBottom, DgrUiPalette.BORDER);
        drawRect(layout.panelRight - 1, layout.panelTop, layout.panelRight, layout.panelBottom, DgrUiPalette.BORDER);
        drawRect(layout.listLeft, layout.listTop - 4, layout.listRight, layout.listBottom, DgrUiPalette.WINDOW_CONTENT);
        drawRect(
            layout.detailLeft,
            layout.detailTop - 4,
            layout.detailRight,
            layout.detailBottom,
            DgrUiPalette.WINDOW_CONTENT);
        if (!layout.stacked) drawRect(
            layout.detailLeft - 5,
            layout.detailTop - 4,
            layout.detailLeft - 4,
            layout.detailBottom,
            DgrUiPalette.BORDER);
    }

    private int listRowHeight(CanonicalTaskLayout layout) {
        return Math.max(
            layout.stacked ? STACKED_ROW_HEIGHT : ROW_HEIGHT,
            (int) Math.ceil(fontRendererObj.FONT_HEIGHT * darkgrey.rpg.client.session.PlayerUiPreferences.textScale())
                + 10);
    }

    private void drawList(CanonicalTaskLayout layout, int mouseX, int mouseY) {
        List<NBTTagCompound> tasks = TaskStoryRows.flatten(tasks(), expandedStories);
        int rowHeight = listRowHeight(layout);
        int visible = Math.max(1, (layout.listBottom - layout.listTop) / rowHeight);
        taskScroll = Math.min(taskScroll, Math.max(0, tasks.size() - visible));
        if (tasks.size() == 0) {
            fontRendererObj.drawString(
                completedView ? "暂无已完成任务" : "暂无进行中的任务",
                layout.listLeft + 8,
                layout.listTop + 20,
                DgrUiPalette.SECONDARY);
            return;
        }
        for (int row = 0; row < visible && row + taskScroll < tasks.size(); row++) {
            int index = row + taskScroll;
            NBTTagCompound task = tasks.get(index);
            int top = layout.listTop + row * rowHeight;
            String id = taskId(task);
            boolean selected = id.equals(selectedTaskId);
            boolean hovered = layout.containsList(mouseX, mouseY) && mouseY >= top && mouseY < top + rowHeight;
            if (selected)
                drawRect(layout.listLeft + 4, top, layout.listRight - 4, top + rowHeight - 2, DgrUiPalette.HOVER);
            else if (hovered)
                drawRect(layout.listLeft + 4, top, layout.listRight - 4, top + rowHeight - 2, DgrUiPalette.HOVER);
            boolean story = task.getBoolean("story_header");
            if (story && !hovered)
                drawRect(layout.listLeft + 4, top, layout.listRight - 4, top + rowHeight - 2, DgrUiPalette.SUB_PANEL);
            double textScale = darkgrey.rpg.client.session.PlayerUiPreferences.textScale();
            String title = task.getString("title");
            if (title.length() == 0) title = "未命名任务";
            int titleX = layout.listLeft + (story ? 22 : 28);
            title = fontRendererObj
                .trimStringToWidth(title, Math.max(1, (int) ((layout.listRight - titleX - 8) / textScale)));
            int textY = top + (rowHeight - (int) Math.ceil(fontRendererObj.FONT_HEIGHT * textScale)) / 2;
            if (story) fontRendererObj.drawString(
                expandedStories.contains(TaskStoryRows.key(task)) ? "▾" : "▸",
                layout.listLeft + 8,
                top + (rowHeight - fontRendererObj.FONT_HEIGHT) / 2,
                DgrUiPalette.TEXT);
            else if (selected) drawRect(
                layout.listLeft + 14,
                top + 4,
                layout.listLeft + 16,
                top + rowHeight - 6,
                DgrUiPalette.SELECTED_BORDER);
            CanonicalDialogueRenderer.drawText(
                fontRendererObj,
                title,
                titleX,
                textY,
                textScale,
                story ? DgrUiPalette.STORY_TEXT : DgrUiPalette.TEXT);
        }
    }

    private void drawDetails(CanonicalTaskLayout layout, int mouseX, int mouseY) {
        itemTooltip = null;
        NBTTagCompound task = findTask(tasks(), selectedTaskId);
        if (task == null) {
            cachedDetails.clear();
            detailCacheKey = null;
            detailHeight = 0;
            detailScroll = 0;
            fontRendererObj
                .drawString("选择一个任务查看详情", layout.detailLeft + 8, layout.detailTop + 8, DgrUiPalette.SECONDARY);
            return;
        }
        if (completedView) {
            if (!taskId(task).equals(detailTaskId)) {
                detailTaskId = taskId(task);
                detailRecord = null;
                detailLoading = true;
                detailFailed = false;
                detailCursor = 0;
                detailCursors.clear();
                detailPage = null;
                detailCacheKey = null;
            }
            NBTTagCompound context = darkgrey.rpg.client.TaskPresentationPages.historyContext();
            context.setInteger("operation", 3);
            context.setString("history", detailTaskId);
            if (detailRecord == null || detailLoading) {
                NBTTagCompound page = darkgrey.rpg.client.TaskPresentationPages.page(context, detailCursor);
                if (page != null && page.getBoolean("restart")) {
                    historyContextKey = null;
                    return;
                }
                if (page != null && page.hasKey("error")) {
                    trackingMessage = page.getString("error");
                    detailLoading = false;
                    detailFailed = true;
                } else if (page != null && page.hasKey("record", 10)) {
                    NBTTagCompound record = page.getCompoundTag("record");
                    if (detailCursor == 0 || detailRecord == null) detailRecord = (NBTTagCompound) record.copy();
                    else {
                        String field = "current_definition".equals(record.getString("content_source"))
                            ? "reference_objectives"
                            : "objectives";
                        NBTTagList items = detailRecord.getTagList(field, 10), next = record.getTagList(field, 10);
                        for (int i = 0; i < next.tagCount(); i++) items.appendTag(
                            next.getCompoundTagAt(i)
                                .copy());
                        detailRecord.setTag(field, items);
                    }
                    detailPage = page;
                    detailLoading = false;
                    detailCacheKey = null;
                }
            }
            if (detailRecord == null) {
                fontRendererObj
                    .drawString("正在读取目标…", layout.detailLeft + 8, layout.detailTop + 8, DgrUiPalette.SECONDARY);
                return;
            }
            task = detailRecord;
        }
        int contentWidth = Math.max(20, layout.detailRight - layout.detailLeft - 16);
        String cacheKey = snapshotRevision + ":"
            + selectedTaskId
            + ":"
            + completedView
            + ":"
            + contentWidth
            + ":"
            + darkgrey.rpg.client.ClientResourceRevision.current()
            + ":"
            + fontRendererObj.getUnicodeFlag()
            + ":"
            + CanonicalTaskClientStore.delayedReward(task.getString("tracking_id"));
        if (!cacheKey.equals(detailCacheKey)) {
            List<DetailBlock> blocks = new ArrayList<DetailBlock>();

            if (!task.getString("description")
                .isEmpty()) {
                detailText(blocks, "", contentWidth);
                detailText(blocks, task.getString("description"), contentWidth);
                detailText(blocks, "", contentWidth);
            }
            if (CanonicalTaskClientStore.delayedReward(task.getString("tracking_id")))
                detailText(blocks, "奖励尚未发放完成，服务器将继续重试。", contentWidth);
            if (completedView && task.hasKey("completion_count")) {
                if ("current_definition".equals(task.getString("content_source"))) {
                    detailText(blocks, "完成次数：" + task.getLong("completion_count"), contentWidth);
                    detailText(blocks, "", contentWidth);
                    // 1.7.10's Unicode renderer sign-extends the full-width opening bracket's glyph byte.
                    // ASCII brackets keep the source label visible without changing the global font renderer.
                    detailText(blocks, "§l任务内容§r(当前故事包)：", contentWidth);
                    detailText(blocks, "以下为当前任务要求，仅供回顾参考。", contentWidth);
                    if (!task.getString("reference_description")
                        .isEmpty()) detailText(blocks, task.getString("reference_description"), contentWidth);
                    NBTTagList references = task.getTagList("reference_objectives", 10);
                    for (int i = 0; i < references.tagCount(); i++) {
                        NBTTagCompound goal = references.getCompoundTagAt(i);
                        for (String line : TaskObjectiveText.referenceLines(goal))
                            detailText(blocks, line, contentWidth);
                        if (goal.hasKey("item_preview", 10)) {
                            NBTTagCompound preview = goal.getCompoundTag("item_preview");
                            if (preview.getBoolean("group"))
                                detailText(blocks, "以下任意物品，合计需要 " + goal.getInteger("required") + " 个", contentWidth);
                            blocks.add(
                                new DetailBlock(
                                    new ItemSlotStrip(preview, true),
                                    "需要 " + goal.getInteger("required") + " 个"));
                        }
                        NBTTagList rewards = goal.getTagList("reward_preview", 10);
                        if (rewards.tagCount() > 0) {
                            detailText(blocks, "§l奖励参考：", contentWidth);
                            blocks.add(new DetailBlock(ItemSlotStrip.rewards(rewards)));
                        }
                        detailText(blocks, "", contentWidth);
                    }
                } else if ("missing".equals(task.getString("content_source"))) {
                    detailText(blocks, "完成次数：" + task.getLong("completion_count"), contentWidth);
                    detailText(blocks, "关联任务资源已缺失，现有记录保留。", contentWidth);
                } else
                    for (String line : TaskObjectiveText.completedLines(task)) detailText(blocks, line, contentWidth);
            } else {
                detailText(
                    blocks,
                    "§l" + (completedView ? ("ERROR".equals(task.getString("status")) ? "任务失败：" : "已完成目标：") : "当前目标："),
                    contentWidth);
                NBTTagList objectives = task.getTagList("objectives", 10);
                if (objectives.tagCount() == 0)
                    detailText(blocks, completedView ? "此记录仅保留已结算摘要" : "暂无进行中的目标", contentWidth);
                for (int index = 0; index < objectives.tagCount(); index++) {
                    NBTTagCompound objective = objectives.getCompoundTagAt(index);
                    int headingStart = blocks.size();
                    detailText(blocks, "● " + objective.getString("text"), contentWidth);
                    for (int heading = headingStart; heading < blocks.size(); heading++)
                        blocks.get(heading).objectiveId = objective.getString("id");
                    String type = objective.getString("type");
                    if ("collect_item".equals(type) || "submit_item".equals(type)) {
                        NBTTagCompound preview = objective.getCompoundTag("item_preview");
                        if (preview.getBoolean("group"))
                            detailText(blocks, "以下任意物品，合计需要 " + objective.getInteger("required") + " 个", contentWidth);
                        if (!preview.hasKey("items")) {
                            NBTTagList missing = new NBTTagList();
                            NBTTagCompound row = new NBTTagCompound();
                            row.setString("error", "此记录没有物品预览，请更新配套服务器");
                            missing.appendTag(row);
                            preview.setTag("items", missing);
                        }
                        blocks.add(
                            new DetailBlock(
                                new ItemSlotStrip(preview, true),
                                "持有 "
                                    + objective.getInteger(objective.hasKey("held_count", 3) ? "held_count" : "current")
                                    + " / 需要 "
                                    + objective.getInteger("required")));
                    } else if ("kill_entity".equals(type)) detailText(
                        blocks,
                        "进度 " + objective.getInteger("current") + " / " + objective.getInteger("required"),
                        contentWidth);
                    NBTTagList rewards = objective.getTagList("reward_preview", 10);
                    if (rewards.tagCount() > 0 && !completedView) {
                        detailText(blocks, "§l奖励：", contentWidth);
                        blocks.add(new DetailBlock(ItemSlotStrip.rewards(rewards)));
                    }
                    if (objective.getBoolean("submit") && !completedView)
                        detailText(blocks, "请与 " + objective.getString("submit_actor") + " 交互提交物品", contentWidth);
                    if (objective.hasKey("x", 3)) detailText(
                        blocks,
                        "坐标：" + objective
                            .getInteger("x") + " / " + objective.getInteger("y") + " / " + objective.getInteger("z"),
                        contentWidth);
                    detailText(blocks, "", contentWidth);
                }
            }
            detailCacheKey = cacheKey;
            cachedDetails = blocks;
        }
        int total = 0, lineHeight = fontRendererObj.FONT_HEIGHT + 2;
        for (DetailBlock block : cachedDetails) total += blockHeight(block, contentWidth);
        detailHeight = total;
        detailScroll = Math.min(detailScroll, Math.max(0, total - (layout.detailBottom - layout.detailTop)));
        int y = layout.detailTop - detailScroll;
        for (DetailBlock block : cachedDetails) {
            block.top = Math.max(y, layout.detailTop);
            block.bottom = Math.min(y + blockHeight(block, contentWidth), layout.detailBottom);
            if (block.objectiveId != null
                && block.objectiveId.equals(darkgrey.rpg.client.TaskTrackerClient.focused(task)))
                drawRect(
                    layout.detailLeft + 4,
                    block.top,
                    layout.detailRight - 4,
                    Math.max(block.top, block.bottom),
                    DgrUiPalette.HOVER);
            if (block.items != null) {
                int slotsWidth = slotsWidth(block, contentWidth);
                block.items
                    .draw(layout.detailLeft + 8, y, slotsWidth, layout.detailTop, layout.detailBottom, mouseX, mouseY);
                if (block.items.tooltip != null) itemTooltip = block.items.tooltip;
                if (block.text != null) {
                    int textY = slotsWidth < contentWidth ? y + 6 : y + block.items.height(slotsWidth);
                    int textX = slotsWidth < contentWidth ? layout.detailLeft + 8 + slotsWidth + 8
                        : layout.detailLeft + 8;
                    for (Object line : fontRendererObj.listFormattedStringToWidth(
                        block.text,
                        slotsWidth < contentWidth ? contentWidth - slotsWidth - 8 : contentWidth)) {
                        if (textY >= layout.detailTop && textY + fontRendererObj.FONT_HEIGHT <= layout.detailBottom)
                            fontRendererObj.drawString((String) line, textX, textY, DgrUiPalette.SECONDARY);
                        textY += lineHeight;
                    }
                }
                y += blockHeight(block, contentWidth);
            } else {
                if (y >= layout.detailTop && y + fontRendererObj.FONT_HEIGHT <= layout.detailBottom)
                    fontRendererObj.drawString(
                        block.text,
                        layout.detailLeft + 8,
                        y,
                        block.text.startsWith("§l") ? DgrUiPalette.SELECTED_BORDER : DgrUiPalette.TEXT);
                y += lineHeight;
            }
        }
    }

    private int slotsWidth(DetailBlock block, int width) {
        if (block.text == null) return width;
        int reserved = fontRendererObj.getStringWidth(block.text) + 8;
        return reserved <= width - 24 ? Math.min(block.items.preferredWidth(), width - reserved) : width;
    }

    private int blockHeight(DetailBlock block, int width) {
        int line = fontRendererObj.FONT_HEIGHT + 2;
        if (block.items == null) return line;
        int slots = slotsWidth(block, width), result = block.items.height(slots);
        if (block.text != null && slots == width)
            result += fontRendererObj.listFormattedStringToWidth(block.text, width)
                .size() * line;
        return result;
    }

    private static String value(NBTTagCompound tag, String key, String fallback) {
        String value = tag.getString(key);
        return value.length() == 0 ? fallback : value;
    }

    @Override
    protected void keyTyped(char character, int key) {
        if (key == 1 && ItemCandidatePopover.escape()) return;
        if (key != 0 && (key == mc.gameSettings.keyBindInventory.getKeyCode()
            || key == darkgrey.rpg.client.ClientQuestKeyHandler.journalKeyCode())) {
            mc.displayGuiScreen(null);
            return;
        }
        super.keyTyped(character, key);
    }

    @Override
    protected void mouseClickMove(int x, int y, int button, long elapsed) {
        if (windowGeometry.active()) {
            windowGeometry.move(x, y);
            updateWindowGeometry();
            return;
        }
        super.mouseClickMove(x, y, button, elapsed);
    }

    @Override
    protected void mouseMovedOrUp(int x, int y, int button) {
        if (button == 0 && windowGeometry.active()) {
            windowGeometry.end();
            UtilityWindowChrome.save("task", windowGeometry, width, height);
            return;
        }
        super.mouseMovedOrUp(x, y, button);
    }

    @Override
    public void onGuiClosed() {
        ItemCandidatePopover.close();
        rememberedTask = selectedTaskId;
        rememberedScroll = taskScroll;
        rememberedDetailScroll = detailScroll;
        rememberedCompleted = completedView;
        rememberedHistoryContext = historyContextKey;
        rememberedHistoryRows = (NBTTagList) historyRows.copy();
        rememberedHistoryPage = historyPage;
        rememberedHistoryCursor = historyCursor;
        rememberedDetailTask = detailTaskId;
        rememberedDetailPage = detailPage;
        rememberedDetailRecord = detailRecord;
        rememberedDetailCursor = detailCursor;
        windowGeometry.end();
        if (geometryInitialized) UtilityWindowChrome.save("task", windowGeometry, width, height);
        super.onGuiClosed();
    }

    @Override
    public boolean doesGuiPauseGame() {
        return false;
    }
}
