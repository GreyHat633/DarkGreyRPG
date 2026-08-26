package darkgrey.rpg.quest;

import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

public final class QuestDefinition {

    private final int schemaVersion;
    private final String id;
    private final String title;
    private final String description;
    private final List<QuestObjective> objectives;
    private final Map<String, QuestObjective> objectivesById;
    private final List<ObjectiveGroup> objectiveGroups;
    private final String notes;
    private final List<String> tags;

    public QuestDefinition(int schemaVersion, String id, String title, String description,
        List<QuestObjective> objectives, List<ObjectiveGroup> objectiveGroups, String notes, List<String> tags) {
        this.schemaVersion = schemaVersion;
        this.id = id;
        this.title = title;
        this.description = description;
        this.objectives = Collections.unmodifiableList(new ArrayList<QuestObjective>(objectives));
        Map<String, QuestObjective> index = new LinkedHashMap<String, QuestObjective>();
        for (QuestObjective objective : objectives) {
            index.put(objective.getId(), objective);
        }
        this.objectivesById = Collections.unmodifiableMap(index);
        this.objectiveGroups = Collections.unmodifiableList(new ArrayList<ObjectiveGroup>(objectiveGroups));
        this.notes = notes;
        this.tags = Collections.unmodifiableList(new ArrayList<String>(tags));
    }

    public int getSchemaVersion() {
        return schemaVersion;
    }

    public String getId() {
        return id;
    }

    public String getTitle() {
        return title;
    }

    public String getDescription() {
        return description;
    }

    public List<QuestObjective> getObjectives() {
        return objectives;
    }

    public QuestObjective getObjective(String objectiveId) {
        return objectivesById.get(objectiveId);
    }

    public List<ObjectiveGroup> getObjectiveGroups() {
        return objectiveGroups;
    }

    public String getNotes() {
        return notes;
    }

    public List<String> getTags() {
        return tags;
    }
}
