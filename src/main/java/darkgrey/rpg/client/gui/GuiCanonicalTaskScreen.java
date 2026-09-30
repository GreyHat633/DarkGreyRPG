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
    private GuiRpgButton completedButton, trackingButton;
    private String trackingMessage = "";
    private String detailCacheKey;
    private List<DetailBlock> cachedDetails = new ArrayList<DetailBlock>();
    private List<String> itemTooltip;

    private static final class DetailBlock {

        final String text;
        final ItemSlotStrip items;

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
        long revision = CanonicalTaskClientStore.getRevision();
        if (revision == snapshotRevision) return;
        NBTTagCompound next = CanonicalTaskClientStore.getSnapshot();
        snapshot = next == null ? new NBTTagCompound() : next;
        snapshotRevision = revision;
        NBTTagList tasks = tasks();
        if (selectedTaskId != null && findTask(tasks, selectedTaskId) != null) return;
        selectedTaskId = tasks.tagCount() == 0 ? null : taskId(tasks.getCompoundTagAt(0));
        taskScroll = 0;
        detailScroll = 0;
    }

    private CanonicalTaskLayout layout() {
        return currentLayout;
    }

    private NBTTagList tasks() {
        return snapshot.getTagList(completedView ? "completed_tasks" : "tasks", 10);
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
        super.handleMouseInput();
        if (windowGeometry.active()) return;
        int wheel = Mouse.getEventDWheel();
        if (wheel == 0) return;
        CanonicalTaskLayout layout = layout();
        int mouseX = Mouse.getEventX() * width / mc.displayWidth;
        int mouseY = height - Mouse.getEventY() * height / mc.displayHeight - 1;
        int amount = wheel < 0 ? 2 : -2;
        if (layout.containsList(mouseX, mouseY)) taskScroll = Math.max(0, taskScroll + amount);
        else if (layout.containsDetail(mouseX, mouseY)) detailScroll = Math.max(0, detailScroll + amount * 12);
    }

    @Override
    protected void mouseClicked(int mouseX, int mouseY, int button) {
        if (windowGeometry.begin(mouseX, mouseY, button)) return;
        super.mouseClicked(mouseX, mouseY, button);
        if (button != 0) return;
        if (layout().containsDetail(mouseX, mouseY)) {
            for (DetailBlock block : cachedDetails) if (block.items != null && block.items.moreAt(mouseX, mouseY)) {
                mc.displayGuiScreen(new GuiItemCandidates(this, block.items.source()));
                return;
            }
        }
        // Item submission is completed only by the server's real entity
        // interaction event. The journal has no remote-submit hit target.
        CanonicalTaskLayout layout = layout();
        if (!layout.containsList(mouseX, mouseY)) return;
        int rowHeight = layout.stacked ? STACKED_ROW_HEIGHT : ROW_HEIGHT;
        int row = (mouseY - layout.listTop) / rowHeight;
        if (row >= Math.max(1, (layout.listBottom - layout.listTop) / rowHeight)) return;
        int index = taskScroll + row;
        NBTTagList tasks = tasks();
        if (index < 0 || index >= tasks.tagCount()) return;
        String id = taskId(tasks.getCompoundTagAt(index));
        if (!id.equals(selectedTaskId)) detailScroll = 0;
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
        drawDetails(layout, mouseX, mouseY);
        updateFooterButtons();
        if (!trackingMessage.isEmpty())
            fontRendererObj.drawString(trackingMessage, layout.panelLeft + 10, layout.panelTop - 12, DgrUiPalette.TEXT);
        UtilityWindowChrome.drawGrip(windowGeometry);
        super.drawScreen(mouseX, mouseY, partialTicks);
        if (itemTooltip != null) drawHoveringText(itemTooltip, mouseX, mouseY, fontRendererObj);
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
        if (button.id == 1) {
            completedView = !completedView;
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

    private void drawList(CanonicalTaskLayout layout, int mouseX, int mouseY) {
        NBTTagList tasks = tasks();
        int rowHeight = layout.stacked ? STACKED_ROW_HEIGHT : ROW_HEIGHT;
        int visible = Math.max(1, (layout.listBottom - layout.listTop) / rowHeight);
        taskScroll = Math.min(taskScroll, Math.max(0, tasks.tagCount() - visible));
        if (tasks.tagCount() == 0) {
            fontRendererObj.drawString(
                completedView ? "暂无已完成任务" : "暂无进行中的任务",
                layout.listLeft + 8,
                layout.listTop + 20,
                DgrUiPalette.SECONDARY);
            return;
        }
        for (int row = 0; row < visible && row + taskScroll < tasks.tagCount(); row++) {
            int index = row + taskScroll;
            NBTTagCompound task = tasks.getCompoundTagAt(index);
            int top = layout.listTop + row * rowHeight;
            String id = taskId(task);
            boolean selected = id.equals(selectedTaskId);
            boolean hovered = layout.containsList(mouseX, mouseY) && mouseY >= top && mouseY < top + rowHeight;
            if (selected)
                drawRect(layout.listLeft + 4, top, layout.listRight - 4, top + rowHeight - 2, DgrUiPalette.HOVER);
            else if (hovered)
                drawRect(layout.listLeft + 4, top, layout.listRight - 4, top + rowHeight - 2, DgrUiPalette.HOVER);
            int color = selected ? DgrUiPalette.TEXT : DgrUiPalette.TEXT;
            String title = task.getString("title");
            if (title.length() == 0) title = "未命名任务";
            title = fontRendererObj.trimStringToWidth(title, layout.listRight - layout.listLeft - 22);
            fontRendererObj.drawString((selected ? "▶ " : "  ") + title, layout.listLeft + 8, top + 5, color);
        }
    }

    private void drawDetails(CanonicalTaskLayout layout, int mouseX, int mouseY) {
        itemTooltip = null;
        NBTTagCompound task = findTask(tasks(), selectedTaskId);
        if (task == null) {
            cachedDetails.clear();
            fontRendererObj
                .drawString("选择一个任务查看详情", layout.detailLeft + 8, layout.detailTop + 8, DgrUiPalette.SECONDARY);
            return;
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
                    detailText(blocks, "● " + objective.getString("text"), contentWidth);
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
        detailScroll = Math.min(detailScroll, Math.max(0, total - (layout.detailBottom - layout.detailTop)));
        int y = layout.detailTop - detailScroll;
        for (DetailBlock block : cachedDetails) {
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
        windowGeometry.end();
        if (geometryInitialized) UtilityWindowChrome.save("task", windowGeometry, width, height);
        super.onGuiClosed();
    }

    @Override
    public boolean doesGuiPauseGame() {
        return false;
    }
}
