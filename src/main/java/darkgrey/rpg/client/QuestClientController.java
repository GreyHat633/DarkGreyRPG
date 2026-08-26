package darkgrey.rpg.client;

import java.util.ArrayList;
import java.util.List;

import net.minecraft.client.Minecraft;

import darkgrey.rpg.client.gui.GuiQuestJournal;
import darkgrey.rpg.quest.runtime.QuestJournalEntry;

public final class QuestClientController {

    private QuestClientController() {}

    public static void openJournal(List<QuestJournalEntry> entries) {
        Minecraft.getMinecraft()
            .displayGuiScreen(new GuiQuestJournal(new ArrayList<QuestJournalEntry>(entries)));
    }
}
