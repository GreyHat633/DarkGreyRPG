package darkgrey.rpg.session.instance;

import java.util.UUID;

import darkgrey.rpg.session.runtime.CanonicalSessionSnapshot;

/** Detached identity plus canonical runtime state for one Session cursor. */
public final class CanonicalSessionInstanceSnapshot {

    private final UUID playerUuid;
    private final String storyId;
    private final String aggregatePlacementId;
    private final String sessionResourceId;
    private final long transportId;
    private final CanonicalSessionSnapshot runtimeSnapshot;

    public CanonicalSessionInstanceSnapshot(String playerUuid, String storyId, String aggregatePlacementId,
        String sessionResourceId, long transportId, CanonicalSessionSnapshot runtimeSnapshot) {
        this(parseUuid(playerUuid), storyId, aggregatePlacementId, sessionResourceId, transportId, runtimeSnapshot);
    }

    public CanonicalSessionInstanceSnapshot(UUID playerUuid, String storyId, String aggregatePlacementId,
        String sessionResourceId, long transportId, CanonicalSessionSnapshot runtimeSnapshot) {
        if (playerUuid == null || blank(storyId)
            || blank(aggregatePlacementId)
            || blank(sessionResourceId)
            || transportId <= 0
            || runtimeSnapshot == null) throw new IllegalArgumentException("Invalid Session instance snapshot.");
        if (!sessionResourceId.equals(runtimeSnapshot.getSessionResourceId()))
            throw new IllegalArgumentException("Instance and runtime resource IDs do not match.");
        this.playerUuid = playerUuid;
        this.storyId = storyId;
        this.aggregatePlacementId = aggregatePlacementId;
        this.sessionResourceId = sessionResourceId;
        this.transportId = transportId;
        this.runtimeSnapshot = runtimeSnapshot;
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

    public CanonicalSessionSnapshot getRuntimeSnapshot() {
        return runtimeSnapshot;
    }

    public CanonicalSessionSnapshot getSessionSnapshot() {
        return runtimeSnapshot;
    }

    public CanonicalSessionSnapshot getSnapshot() {
        return runtimeSnapshot;
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }

    private static UUID parseUuid(String value) {
        if (value == null || value.length() != 36) throw new IllegalArgumentException("Invalid player UUID.");
        try {
            UUID uuid = UUID.fromString(value);
            if (!uuid.toString()
                .equals(value.toLowerCase(java.util.Locale.ROOT)))
                throw new IllegalArgumentException("Invalid player UUID.");
            return uuid;
        } catch (RuntimeException exception) {
            throw new IllegalArgumentException("Invalid player UUID.");
        }
    }
}
