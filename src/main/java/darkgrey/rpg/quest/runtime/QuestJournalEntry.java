package darkgrey.rpg.quest.runtime;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

public final class QuestJournalEntry {

    private final String questId;
    private final String title;
    private final String description;
    private final QuestStatus status;
    private final List<String> objectiveLines;

    public QuestJournalEntry(String questId, String title, String description, QuestStatus status,
        List<String> objectiveLines) {
        this.questId = questId;
        this.title = title;
        this.description = description;
        this.status = status;
        this.objectiveLines = Collections.unmodifiableList(new ArrayList<String>(objectiveLines));
    }

    public String getQuestId() {
        return questId;
    }

    public String getTitle() {
        return title;
    }

    public String getDescription() {
        return description;
    }

    public QuestStatus getStatus() {
        return status;
    }

    public List<String> getObjectiveLines() {
        return objectiveLines;
    }
}
