package darkgrey.rpg.task.event;

import java.util.UUID;

/** Exact event subscription key: player, objective event type, and primary target. */
public final class CanonicalTaskSubscriptionKey implements Comparable<CanonicalTaskSubscriptionKey> {

    private final UUID playerUuid;
    private final String eventType;
    private final String primaryTarget;

    public CanonicalTaskSubscriptionKey(UUID playerUuid, String eventType, String primaryTarget) {
        if (playerUuid == null || blank(eventType) || blank(primaryTarget))
            throw new IllegalArgumentException("Task subscription key is required.");
        this.playerUuid = playerUuid;
        this.eventType = eventType;
        this.primaryTarget = primaryTarget;
    }

    public UUID getPlayerUuid() {
        return playerUuid;
    }

    public UUID getPlayerId() {
        return playerUuid;
    }

    public String getEventType() {
        return eventType;
    }

    public String getType() {
        return eventType;
    }

    public String getPrimaryTarget() {
        return primaryTarget;
    }

    public String getTarget() {
        return primaryTarget;
    }

    @Override
    public int compareTo(CanonicalTaskSubscriptionKey other) {
        int result = playerUuid.toString()
            .compareTo(other.playerUuid.toString());
        if (result == 0) result = eventType.compareTo(other.eventType);
        return result == 0 ? primaryTarget.compareTo(other.primaryTarget) : result;
    }

    @Override
    public int hashCode() {
        return ((31 * playerUuid.hashCode() + eventType.hashCode()) * 31) + primaryTarget.hashCode();
    }

    @Override
    public boolean equals(Object value) {
        return value instanceof CanonicalTaskSubscriptionKey
            && playerUuid.equals(((CanonicalTaskSubscriptionKey) value).playerUuid)
            && eventType.equals(((CanonicalTaskSubscriptionKey) value).eventType)
            && primaryTarget.equals(((CanonicalTaskSubscriptionKey) value).primaryTarget);
    }

    @Override
    public String toString() {
        return playerUuid.toString() + ":" + eventType + ":" + primaryTarget;
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }
}
