package darkgrey.rpg.client.gui;

import java.util.ArrayList;
import java.util.List;

import net.minecraft.client.gui.GuiButton;
import net.minecraft.client.gui.GuiScreen;

import org.lwjgl.input.Mouse;

import darkgrey.rpg.quest.runtime.QuestJournalEntry;
import darkgrey.rpg.quest.runtime.QuestStatus;

public final class GuiQuestJournal extends GuiScreen {

    private static final int PANEL_WIDTH = 420;
    private static final int PANEL_HEIGHT = 300;
    private static final QuestStatus[] VISIBLE_TABS = { QuestStatus.ACTIVE, QuestStatus.COMPLETED, QuestStatus.FAILED };
    private final List<QuestJournalEntry> entries;
    private QuestStatus tab = QuestStatus.ACTIVE;
    private int scrollOffset;

    public GuiQuestJournal(List<QuestJournalEntry> entries) {
        this.entries = entries;
    }

    /** Package-private structural seam for the Stage 4 UI probe. */
    static QuestStatus[] visibleTabs() {
        return VISIBLE_TABS.clone();
    }

    @Override
    @SuppressWarnings("unchecked")
    public void initGui() {
        buttonList.clear();
        int panelHeight = panelHeight();
        int left = (width - PANEL_WIDTH) / 2;
        int top = (height - panelHeight) / 2;
        buttonList.add(new GuiModernButton(1, left + 16, top + 30, 110, 20, "进行中"));
        buttonList.add(new GuiModernButton(2, left + 132, top + 30, 110, 20, "已完成"));
        buttonList.add(new GuiModernButton(3, left + 248, top + 30, 110, 20, "失败"));
        buttonList.add(new GuiModernButton(0, left + PANEL_WIDTH - 76, top + panelHeight - 30, 60, 20, "关闭"));
    }

    @Override
    protected void actionPerformed(GuiButton button) {
        if (button.id == 0) {
            mc.displayGuiScreen(null);
        } else if (button.id == 1) {
            tab = QuestStatus.ACTIVE;
            scrollOffset = 0;
        } else if (button.id == 2) {
            tab = QuestStatus.COMPLETED;
            scrollOffset = 0;
        } else if (button.id == 3) {
            tab = QuestStatus.FAILED;
            scrollOffset = 0;
        }
    }

    @Override
    public void handleMouseInput() {
        super.handleMouseInput();
        int wheel = Mouse.getEventDWheel();
        if (wheel != 0) {
            scrollOffset = Math.max(0, scrollOffset + (wheel < 0 ? 22 : -22));
        }
    }

    @Override
    public void drawScreen(int mouseX, int mouseY, float partialTicks) {
        drawDefaultBackground();
        int panelHeight = panelHeight();
        int left = (width - PANEL_WIDTH) / 2;
        int top = (height - panelHeight) / 2;
        drawRect(left, top, left + PANEL_WIDTH, top + panelHeight, DgrUiPalette.PANEL); // Deep Space
        drawRect(left, top, left + PANEL_WIDTH, top + 2, DgrUiPalette.BORDER); // Deep Space Accent
        drawRect(left + 8, top + 58, left + PANEL_WIDTH - 8, top + panelHeight - 38, DgrUiPalette.SUB_PANEL); // Inner
                                                                                                              // dark
                                                                                                              // panel
        drawCenteredString(fontRendererObj, "任务追踪", width / 2, top + 10, DgrUiPalette.TEXT); // Light title

        List<String> lines = buildLines();
        int y = top + 66 - scrollOffset;
        int clipTop = top + 62;
        int clipBottom = top + panelHeight - 42;
        for (String line : lines) {
            if (y >= clipTop && y <= clipBottom) {
                fontRendererObj
                    .drawString(line, left + 18, y, line.startsWith("  ") ? DgrUiPalette.SECONDARY : DgrUiPalette.TEXT); // Secondary
                // /
                // Primary
                // text
            }
            y += 12;
        }
        super.drawScreen(mouseX, mouseY, partialTicks);
    }

    /** Keeps the fixed design height on large screens while fitting the 854x480 scale-2 client. */
    private int panelHeight() {
        return Math.max(1, Math.min(PANEL_HEIGHT, height - 20));
    }

    private List<String> buildLines() {
        List<String> lines = new ArrayList<String>();
        for (QuestJournalEntry entry : entries) {
            if (entry.getStatus() != tab) {
                continue;
            }
            lines.add(entry.getTitle());
            appendWrapped(lines, "  " + entry.getDescription(), PANEL_WIDTH - 54);
            for (String objective : entry.getObjectiveLines()) {
                appendWrapped(lines, "  - " + objective, PANEL_WIDTH - 54);
            }
            lines.add("");
        }
        if (lines.isEmpty()) {
            if (tab == QuestStatus.ACTIVE) lines.add("没有进行中的任务。");
            else if (tab == QuestStatus.COMPLETED) lines.add("没有已完成的任务。");
            else lines.add("没有失败的任务。");
        }
        return lines;
    }

    private void appendWrapped(List<String> lines, String value, int widthLimit) {
        @SuppressWarnings("unchecked")
        List<String> wrapped = fontRendererObj.listFormattedStringToWidth(value, widthLimit);
        lines.addAll(wrapped);
    }

    @Override
    public boolean doesGuiPauseGame() {
        return false;
    }
}
