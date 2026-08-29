package darkgrey.rpg.session.server;

import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.UUID;

/** Detached server-only completion result for one canonical Session invocation. */
public final class CanonicalSessionCompletionResult {

    private final UUID playerUuid;
    private final String storyId;
    private final String aggregatePlacementId;
    private final String sessionResourceId;
    private final long transportId;
    private final String endPortId;
    private final Map<String, Boolean> publicLogicOutputs;

    public CanonicalSessionCompletionResult(UUID playerUuid, String storyId, String aggregatePlacementId,
        String sessionResourceId, long transportId, String endPortId, Map<String, Boolean> publicLogicOutputs) {
        if (playerUuid == null || blank(storyId)
            || blank(aggregatePlacementId)
            || blank(sessionResourceId)
            || transportId <= 0
            || blank(endPortId)
            || publicLogicOutputs == null)
            throw new IllegalArgumentException("Invalid canonical Session completion result.");
        this.playerUuid = playerUuid;
        this.storyId = storyId;
        this.aggregatePlacementId = aggregatePlacementId;
        this.sessionResourceId = sessionResourceId;
        this.transportId = transportId;
        this.endPortId = endPortId;
        LinkedHashMap<String, Boolean> detached = new LinkedHashMap<String, Boolean>();
        for (Map.Entry<String, Boolean> entry : publicLogicOutputs.entrySet()) {
            if (blank(entry.getKey()) || entry.getValue() == null)
                throw new IllegalArgumentException("Invalid canonical Session Logic output.");
            detached.put(entry.getKey(), entry.getValue());
        }
        this.publicLogicOutputs = Collections.unmodifiableMap(detached);
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

    public String getEndPortId() {
        return endPortId;
    }

    public String getFinalEndPortId() {
        return endPortId;
    }

    public Map<String, Boolean> getPublicLogicOutputs() {
        return publicLogicOutputs;
    }

    public Map<String, Boolean> getPublicLogicOutputMap() {
        return publicLogicOutputs;
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }
}
