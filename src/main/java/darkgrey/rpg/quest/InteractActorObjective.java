package darkgrey.rpg.quest;

public final class InteractActorObjective extends QuestObjective {

    private final String actorId;

    public InteractActorObjective(String id, String description, String actorId, int requiredAmount) {
        super(id, ObjectiveType.INTERACT_ACTOR, description, requiredAmount);
        this.actorId = actorId;
    }

    public String getActorId() {
        return actorId;
    }
}
