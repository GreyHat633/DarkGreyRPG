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
    private final List<QuestJournalEntry> entries;
    private QuestStatus tab = QuestStatus.ACTIVE;
    private int scrollOffset;

    public GuiQuestJournal(List<QuestJournalEntry> entries) {
        this.entries = entries;
    }

    @Override
    @SuppressWarnings("unchecked")
    public void initGui() {
        buttonList.clear();
        int left = (width - PANEL_WIDTH) / 2;
        int top = (height - PANEL_HEIGHT) / 2;
        buttonList.add(new GuiModernButton(1, left + 16, top + 30, 110, 20, "Active"));
        buttonList.add(new GuiModernButton(2, left + 132, top + 30, 110, 20, "Completed"));
        buttonList.add(new GuiModernButton(0, left + PANEL_WIDTH - 76, top + PANEL_HEIGHT - 30, 60, 20, "Close"));
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
        int left = (width - PANEL_WIDTH) / 2;
        int top = (height - PANEL_HEIGHT) / 2;
        drawRect(left, top, left + PANEL_WIDTH, top + PANEL_HEIGHT, 0xF02B2F4A); // Deep Space
        drawRect(left, top, left + PANEL_WIDTH, top + 2, 0xFF7D8CFF); // Deep Space Accent
        drawRect(left + 8, top + 58, left + PANEL_WIDTH - 8, top + PANEL_HEIGHT - 38, 0xCC1E213A); // Inner dark panel
        drawCenteredString(fontRendererObj, "Quest Journal", width / 2, top + 10, 0xFFEEF0FF); // Light title

        List<String> lines = buildLines();
        int y = top + 66 - scrollOffset;
        int clipTop = top + 62;
        int clipBottom = top + PANEL_HEIGHT - 42;
        for (String line : lines) {
            if (y >= clipTop && y <= clipBottom) {
                fontRendererObj.drawString(line, left + 18, y, line.startsWith("  ") ? 0xAAEEF0FF : 0xFFEEF0FF); // Secondary
                                                                                                                 // /
                                                                                                                 // Primary
                                                                                                                 // text
            }
            y += 12;
        }
        super.drawScreen(mouseX, mouseY, partialTicks);
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
            lines.add(tab == QuestStatus.ACTIVE ? "No active quests." : "No completed quests.");
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
