package darkgrey.rpg.quest;

public abstract class QuestObjective {

    private final String id;
    private final ObjectiveType type;
    private final String description;
    private final int requiredAmount;

    protected QuestObjective(String id, ObjectiveType type, String description, int requiredAmount) {
        this.id = id;
        this.type = type;
        this.description = description;
        this.requiredAmount = requiredAmount;
    }

    public String getId() {
        return id;
    }

    public ObjectiveType getType() {
        return type;
    }

    public String getDescription() {
        return description;
    }

    public int getRequiredAmount() {
        return requiredAmount;
    }
}
