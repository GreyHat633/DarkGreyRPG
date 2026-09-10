package darkgrey.rpg.client.gui;

import java.util.ArrayList;
import java.util.List;

import net.minecraft.client.gui.GuiScreen;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

import org.lwjgl.input.Mouse;

import darkgrey.rpg.client.CanonicalTaskClientStore;

/**
 * Read-only presentation of the server-produced Task cache.
 *
 * <p>
 * The screen deliberately has no network lifecycle. The cache is consumed
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

    @Override
    public void initGui() {
        refreshCache();
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
        NBTTagCompound selected = findTask(tasks(), selectedTaskId);
        int objectives = selected == null ? 0
            : selected.getTagList("objectives", 10)
                .tagCount();
        int preferred = Math.min(300, Math.max(180, 80 + Math.max(tasks().tagCount() * 24, objectives * 32)));
        return new CanonicalTaskLayout(width, height, preferred);
    }

    private NBTTagList tasks() {
        return snapshot.getTagList("tasks", 10);
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
        int wheel = Mouse.getEventDWheel();
        if (wheel == 0) return;
        CanonicalTaskLayout layout = layout();
        int mouseX = Mouse.getEventX() * width / mc.displayWidth;
        int mouseY = height - Mouse.getEventY() * height / mc.displayHeight - 1;
        int amount = wheel < 0 ? 2 : -2;
        if (layout.containsList(mouseX, mouseY)) taskScroll = Math.max(0, taskScroll + amount);
        else if (layout.containsDetail(mouseX, mouseY)) detailScroll = Math.max(0, detailScroll + amount);
    }

    @Override
    protected void mouseClicked(int mouseX, int mouseY, int button) {
        super.mouseClicked(mouseX, mouseY, button);
        if (button != 0) return;
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
        drawCenteredString(fontRendererObj, "任务", width / 2, layout.panelTop + 9, DgrUiPalette.SELECTED_BORDER);
        drawList(layout, mouseX, mouseY);
        drawDetails(layout);
        drawCenteredString(fontRendererObj, "Esc 返回", width / 2, layout.panelBottom - 14, 0xFFAAAAAA);
        super.drawScreen(mouseX, mouseY, partialTicks);
    }

    private void drawPanel(CanonicalTaskLayout layout) {
        drawRect(layout.panelLeft, layout.panelTop, layout.panelRight, layout.panelBottom, 0xEE161616);
        drawRect(
            layout.panelLeft,
            layout.panelTop,
            layout.panelRight,
            layout.panelTop + 1,
            DgrUiPalette.SELECTED_BORDER);
        drawRect(layout.panelLeft, layout.panelBottom - 1, layout.panelRight, layout.panelBottom, DgrUiPalette.BORDER);
        drawRect(layout.panelLeft, layout.panelTop, layout.panelLeft + 1, layout.panelBottom, DgrUiPalette.BORDER);
        drawRect(layout.panelRight - 1, layout.panelTop, layout.panelRight, layout.panelBottom, DgrUiPalette.BORDER);
        drawRect(layout.listLeft, layout.listTop - 4, layout.listRight, layout.listBottom, 0xCC202020);
        drawRect(layout.detailLeft, layout.detailTop - 4, layout.detailRight, layout.detailBottom, 0xCC202020);
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
            fontRendererObj.drawString("暂无进行中的任务", layout.listLeft + 8, layout.listTop + 20, 0xFFAAAAAA);
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

    private void drawDetails(CanonicalTaskLayout layout) {
        NBTTagCompound task = findTask(tasks(), selectedTaskId);
        if (task == null) {
            fontRendererObj.drawString("选择一个任务查看详情", layout.detailLeft + 8, layout.detailTop + 8, 0xFFAAAAAA);
            return;
        }
        int contentWidth = Math.max(20, layout.detailRight - layout.detailLeft - 16);
        List<String> lines = new ArrayList<String>();
        String title = task.getString("title");
        if (title.length() == 0) title = "未命名任务";
        lines.addAll(fontRendererObj.listFormattedStringToWidth(title, contentWidth));
        lines.add("当前目标");
        NBTTagList objectives = task.getTagList("objectives", 10);
        if (objectives.tagCount() == 0) {
            lines.add("暂无进行中的目标");
        } else {
            for (int index = 0; index < objectives.tagCount(); index++) {
                NBTTagCompound objective = objectives.getCompoundTagAt(index);
                List<String> wrapped = fontRendererObj
                    .listFormattedStringToWidth("● " + value(objective, "text", "未命名目标"), contentWidth);
                lines.addAll(wrapped);
                int required = objective.getInteger("required");
                int current = objective.getInteger("current");
                if (required > 1 || current > 0) lines.add("  进度 " + current + " / " + required);
            }
        }
        int lineHeight = fontRendererObj.FONT_HEIGHT + 2;
        int visible = Math.max(1, (layout.detailBottom - layout.detailTop) / lineHeight);
        detailScroll = Math.min(detailScroll, Math.max(0, lines.size() - visible));
        for (int index = 0; index < visible && index + detailScroll < lines.size(); index++) {
            int lineIndex = index + detailScroll;
            int color = lineIndex == 0 ? DgrUiPalette.TEXT
                : lineIndex == 1 ? DgrUiPalette.SECONDARY : DgrUiPalette.TEXT;
            fontRendererObj
                .drawString(lines.get(lineIndex), layout.detailLeft + 8, layout.detailTop + index * lineHeight, color);
        }
    }

    private static String value(NBTTagCompound tag, String key, String fallback) {
        String value = tag.getString(key);
        return value.length() == 0 ? fallback : value;
    }

    @Override
    public boolean doesGuiPauseGame() {
        return false;
    }
}
