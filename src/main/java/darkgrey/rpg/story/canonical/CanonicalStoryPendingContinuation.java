package darkgrey.rpg.story.canonical;

import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.UUID;

import darkgrey.rpg.session.server.CanonicalSessionCompletionResult;

/**
 * Immutable persisted cursor at the boundary between a completed Session and
 * its caller Story. It deliberately contains no executable Story state.
 */
public final class CanonicalStoryPendingContinuation {

    private final UUID playerUuid;
    private final String storyId;
    private final String aggregatePlacementId;
    private final String sessionResourceId;
    private final long transportId;
    private final String selectedEndPortId;
    private final String targetNodeId;
    private final String targetPortId;
    private final Map<String, Boolean> publicLogic;

    public CanonicalStoryPendingContinuation(UUID playerUuid, String storyId, String aggregatePlacementId,
        String sessionResourceId, long transportId, String selectedEndPortId, String targetNodeId, String targetPortId,
        Map<String, Boolean> publicLogic) {
        if (playerUuid == null || blank(storyId)
            || blank(aggregatePlacementId)
            || blank(sessionResourceId)
            || transportId <= 0
            || blank(selectedEndPortId)
            || blank(targetNodeId)
            || blank(targetPortId)
            || publicLogic == null) throw new IllegalArgumentException("Invalid pending Story continuation.");
        this.playerUuid = playerUuid;
        this.storyId = storyId;
        this.aggregatePlacementId = aggregatePlacementId;
        this.sessionResourceId = sessionResourceId;
        this.transportId = transportId;
        this.selectedEndPortId = selectedEndPortId;
        this.targetNodeId = targetNodeId;
        this.targetPortId = targetPortId;
        LinkedHashMap<String, Boolean> detached = new LinkedHashMap<String, Boolean>();
        for (Map.Entry<String, Boolean> entry : publicLogic.entrySet()) {
            if (blank(entry.getKey()) || entry.getValue() == null)
                throw new IllegalArgumentException("Invalid pending Story Logic output.");
            if (detached.put(entry.getKey(), entry.getValue()) != null)
                throw new IllegalArgumentException("Duplicate pending Story Logic output.");
        }
        this.publicLogic = Collections.unmodifiableMap(detached);
    }

    public UUID getPlayerUuid() {
        return playerUuid;
    }

    public UUID getPlayerId() {
        return playerUuid;
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

    public String getSessionResourceId() {
        return sessionResourceId;
    }

    public long getTransportId() {
        return transportId;
    }

    public long getSessionId() {
        return transportId;
    }

    public String getSelectedEndPortId() {
        return selectedEndPortId;
    }

    public String getEndPortId() {
        return selectedEndPortId;
    }

    public String getFinalEndPortId() {
        return selectedEndPortId;
    }

    public String getTargetNodeId() {
        return targetNodeId;
    }

    public String getNextNodeId() {
        return targetNodeId;
    }

    public String getTargetPortId() {
        return targetPortId;
    }

    public String getNextPortId() {
        return targetPortId;
    }

    public Map<String, Boolean> getPublicLogic() {
        return publicLogic;
    }

    public Map<String, Boolean> getPublicLogicOutputs() {
        return publicLogic;
    }

    public Map<String, Boolean> getPublicLogicOutputMap() {
        return publicLogic;
    }

    public Map<String, Boolean> getLogic() {
        return publicLogic;
    }

    public static CanonicalStoryPendingContinuation from(CanonicalSessionCompletionResult completion,
        CanonicalStorySessionCompletionRoute route) {
        if (completion == null || route == null)
            throw new IllegalArgumentException("Continuation inputs are required.");
        CanonicalStoryFlowTransition transition = route.getNextFlow();
        if (transition == null) throw new IllegalArgumentException("Continuation transition is required.");
        return new CanonicalStoryPendingContinuation(
            completion.getPlayerUuid(),
            completion.getStoryId(),
            completion.getAggregatePlacementId(),
            completion.getSessionResourceId(),
            completion.getTransportId(),
            transition.getEndPortId(),
            transition.getTargetNodeId(),
            transition.getTargetPortId(),
            route.getPublicLogic()
                .getValues());
    }

    public boolean sameValue(CanonicalStoryPendingContinuation other) {
        return equals(other);
    }

    @Override
    public boolean equals(Object value) {
        if (this == value) return true;
        if (!(value instanceof CanonicalStoryPendingContinuation)) return false;
        CanonicalStoryPendingContinuation other = (CanonicalStoryPendingContinuation) value;
        return transportId == other.transportId && playerUuid.equals(other.playerUuid)
            && storyId.equals(other.storyId)
            && aggregatePlacementId.equals(other.aggregatePlacementId)
            && sessionResourceId.equals(other.sessionResourceId)
            && selectedEndPortId.equals(other.selectedEndPortId)
            && targetNodeId.equals(other.targetNodeId)
            && targetPortId.equals(other.targetPortId)
            && publicLogic.equals(other.publicLogic)
            && new ArrayList<String>(publicLogic.keySet()).equals(new ArrayList<String>(other.publicLogic.keySet()));
    }

    @Override
    public int hashCode() {
        int result = playerUuid.hashCode();
        result = 31 * result + storyId.hashCode();
        result = 31 * result + aggregatePlacementId.hashCode();
        result = 31 * result + sessionResourceId.hashCode();
        result = 31 * result + (int) (transportId ^ (transportId >>> 32));
        result = 31 * result + selectedEndPortId.hashCode();
        result = 31 * result + targetNodeId.hashCode();
        result = 31 * result + targetPortId.hashCode();
        return 31 * result + publicLogic.hashCode();
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }
}
