package darkgrey.rpg.quest;

public final class CollectItemObjective extends QuestObjective {

    private final String itemId;
    private final int metadata;

    public CollectItemObjective(String id, String description, String itemId, int metadata, int requiredAmount) {
        super(id, ObjectiveType.COLLECT_ITEM, description, requiredAmount);
        this.itemId = itemId;
        this.metadata = metadata;
    }

    public String getItemId() {
        return itemId;
    }

    public int getMetadata() {
        return metadata;
    }
}
