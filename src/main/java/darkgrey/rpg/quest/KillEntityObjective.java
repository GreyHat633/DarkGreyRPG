package darkgrey.rpg.quest;

public final class KillEntityObjective extends QuestObjective {

    private final String entityId;

    public KillEntityObjective(String id, String description, String entityId, int requiredAmount) {
        super(id, ObjectiveType.KILL_ENTITY, description, requiredAmount);
        this.entityId = entityId;
    }

    public String getEntityId() {
        return entityId;
    }
}
