package darkgrey.rpg.task.event;

import java.util.UUID;

/** Detached, deterministic identity of one persisted Task instance. */
public final class CanonicalTaskInstanceIdentity implements Comparable<CanonicalTaskInstanceIdentity> {

    private final UUID playerUuid;
    private final String storyInstanceId;
    private final String taskNodePlacementId;

    public CanonicalTaskInstanceIdentity(UUID playerUuid, String storyInstanceId, String taskNodePlacementId) {
        if (playerUuid == null || blank(storyInstanceId) || blank(taskNodePlacementId))
            throw new IllegalArgumentException("Task instance identity is required.");
        this.playerUuid = playerUuid;
        this.storyInstanceId = storyInstanceId;
        this.taskNodePlacementId = taskNodePlacementId;
    }

    public UUID getPlayerUuid() {
        return playerUuid;
    }

    public UUID getPlayerId() {
        return playerUuid;
    }

    public String getStoryInstanceId() {
        return storyInstanceId;
    }

    public String getStoryId() {
        return storyInstanceId;
    }

    public String getTaskNodePlacementId() {
        return taskNodePlacementId;
    }

    public String getTaskNodePlacement() {
        return taskNodePlacementId;
    }

    @Override
    public int compareTo(CanonicalTaskInstanceIdentity other) {
        int result = playerUuid.toString()
            .compareTo(other.playerUuid.toString());
        if (result == 0) result = storyInstanceId.compareTo(other.storyInstanceId);
        return result == 0 ? taskNodePlacementId.compareTo(other.taskNodePlacementId) : result;
    }

    @Override
    public int hashCode() {
        return ((31 * playerUuid.hashCode() + storyInstanceId.hashCode()) * 31) + taskNodePlacementId.hashCode();
    }

    @Override
    public boolean equals(Object value) {
        return value instanceof CanonicalTaskInstanceIdentity
            && playerUuid.equals(((CanonicalTaskInstanceIdentity) value).playerUuid)
            && storyInstanceId.equals(((CanonicalTaskInstanceIdentity) value).storyInstanceId)
            && taskNodePlacementId.equals(((CanonicalTaskInstanceIdentity) value).taskNodePlacementId);
    }

    @Override
    public String toString() {
        return playerUuid.toString() + "\u0000" + storyInstanceId + "\u0000" + taskNodePlacementId;
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }
}
