package darkgrey.rpg.story.canonical;

/** Detached immutable Flow transition selected by a completed Session. */
public final class CanonicalStoryFlowTransition {

    private final String storyId;
    private final String aggregatePlacementId;
    private final String endPortId;
    private final String targetNodeId;
    private final String targetPortId;

    public CanonicalStoryFlowTransition(String storyId, String aggregatePlacementId, String endPortId,
        String targetNodeId, String targetPortId) {
        require(storyId, "storyId");
        require(aggregatePlacementId, "aggregatePlacementId");
        require(endPortId, "endPortId");
        require(targetNodeId, "targetNodeId");
        require(targetPortId, "targetPortId");
        this.storyId = storyId;
        this.aggregatePlacementId = aggregatePlacementId;
        this.endPortId = endPortId;
        this.targetNodeId = targetNodeId;
        this.targetPortId = targetPortId;
    }

    public String getStoryId() {
        return storyId;
    }

    public String getAggregatePlacementId() {
        return aggregatePlacementId;
    }

    public String getAggregateNodePlacementId() {
        return aggregatePlacementId;
    }

    public String getEndPortId() {
        return endPortId;
    }

    public String getSelectedEndPortId() {
        return endPortId;
    }

    public String getTargetNodeId() {
        return targetNodeId;
    }

    public String getNextNodeId() {
        return targetNodeId;
    }

    public String getToNodeId() {
        return targetNodeId;
    }

    public String getTargetPortId() {
        return targetPortId;
    }

    public String getNextPortId() {
        return targetPortId;
    }

    public String getToPortId() {
        return targetPortId;
    }

    public String getFromNodeId() {
        return aggregatePlacementId;
    }

    public String getFromPortId() {
        return endPortId;
    }

    private static void require(String value, String name) {
        if (value == null || value.trim()
            .isEmpty()) throw new IllegalArgumentException(name + " is required.");
    }
}
