package darkgrey.rpg.task.instance;

import java.util.UUID;

import darkgrey.rpg.task.runtime.CanonicalTaskSnapshot;

/** Detached identity, lifecycle, timestamps, and complete pure runtime state. */
public final class CanonicalTaskInstanceSnapshot {

    private final UUID playerUuid;
    private final String storyInstanceId;
    private final String taskNodePlacementId;
    private final String taskResourceId;
    private final CanonicalTaskInstanceStatus status;
    private final long activationTime;
    private final Long settlementTime;
    private final CanonicalTaskSnapshot runtimeSnapshot;

    public CanonicalTaskInstanceSnapshot(String playerUuid, String storyInstanceId, String taskNodePlacementId,
        String taskResourceId, CanonicalTaskInstanceStatus status, long activationTime, Long settlementTime,
        CanonicalTaskSnapshot runtimeSnapshot) {
        this(
            parseUuid(playerUuid),
            storyInstanceId,
            taskNodePlacementId,
            taskResourceId,
            status,
            activationTime,
            settlementTime,
            runtimeSnapshot);
    }

    public CanonicalTaskInstanceSnapshot(UUID playerUuid, String storyInstanceId, String taskNodePlacementId,
        String taskResourceId, CanonicalTaskInstanceStatus status, long activationTime, Long settlementTime,
        CanonicalTaskSnapshot runtimeSnapshot) {
        if (playerUuid == null || blank(storyInstanceId)
            || blank(taskNodePlacementId)
            || blank(taskResourceId)
            || status == null
            || activationTime <= 0
            || runtimeSnapshot == null) throw new IllegalArgumentException("Invalid Task instance snapshot.");
        if (!taskResourceId.equals(runtimeSnapshot.getResourceId()))
            throw new IllegalArgumentException("Instance and runtime resource IDs do not match.");
        if (settlementTime != null && (settlementTime.longValue() <= 0 || settlementTime.longValue() < activationTime))
            throw new IllegalArgumentException("Invalid Task settlement time.");
        if ((status == CanonicalTaskInstanceStatus.SETTLED) != (settlementTime != null))
            throw new IllegalArgumentException("Only settled Task instances have settlement time.");
        if (status == CanonicalTaskInstanceStatus.NOT_STARTED && (settlementTime != null
            || runtimeSnapshot.getStatus() != darkgrey.rpg.task.runtime.CanonicalTaskStatus.ACTIVE))
            throw new IllegalArgumentException("Not-started Task instance state is contradictory.");
        if (status == CanonicalTaskInstanceStatus.SETTLED
            && runtimeSnapshot.getStatus() != darkgrey.rpg.task.runtime.CanonicalTaskStatus.SETTLED)
            throw new IllegalArgumentException("Settled Task instance requires settled runtime.");
        if (status == CanonicalTaskInstanceStatus.ACTIVE
            && runtimeSnapshot.getStatus() != darkgrey.rpg.task.runtime.CanonicalTaskStatus.ACTIVE)
            throw new IllegalArgumentException("Active Task instance requires active runtime.");
        if (status == CanonicalTaskInstanceStatus.CANCELLED_BY_STORY_TERMINATION
            && runtimeSnapshot.getStatus() != darkgrey.rpg.task.runtime.CanonicalTaskStatus.ACTIVE)
            throw new IllegalArgumentException("Cancelled Task instance requires retained active runtime.");
        if (status == CanonicalTaskInstanceStatus.ERROR
            && runtimeSnapshot.getStatus() != darkgrey.rpg.task.runtime.CanonicalTaskStatus.ACTIVE)
            throw new IllegalArgumentException("Error Task instance requires retained active runtime.");
        this.playerUuid = playerUuid;
        this.storyInstanceId = storyInstanceId;
        this.taskNodePlacementId = taskNodePlacementId;
        this.taskResourceId = taskResourceId;
        this.status = status;
        this.activationTime = activationTime;
        this.settlementTime = settlementTime;
        this.runtimeSnapshot = runtimeSnapshot;
    }

    public CanonicalTaskInstanceSnapshot(UUID playerUuid, String storyInstanceId, String taskNodePlacementId,
        String taskResourceId, long activationTime, Long settlementTime, CanonicalTaskInstanceStatus status,
        CanonicalTaskSnapshot runtimeSnapshot) {
        this(
            playerUuid,
            storyInstanceId,
            taskNodePlacementId,
            taskResourceId,
            status,
            activationTime,
            settlementTime,
            runtimeSnapshot);
    }

    public CanonicalTaskInstanceSnapshot(String playerUuid, String storyInstanceId, String taskNodePlacementId,
        String taskResourceId, long activationTime, Long settlementTime, CanonicalTaskInstanceStatus status,
        CanonicalTaskSnapshot runtimeSnapshot) {
        this(
            parseUuid(playerUuid),
            storyInstanceId,
            taskNodePlacementId,
            taskResourceId,
            status,
            activationTime,
            settlementTime,
            runtimeSnapshot);
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

    public String getTaskResourceId() {
        return taskResourceId;
    }

    public CanonicalTaskInstanceStatus getStatus() {
        return status;
    }

    public long getActivationTime() {
        return activationTime;
    }

    public long getActivationTimestamp() {
        return activationTime;
    }

    public Long getSettlementTime() {
        return settlementTime;
    }

    public Long getSettlementTimestamp() {
        return settlementTime;
    }

    public CanonicalTaskSnapshot getRuntimeSnapshot() {
        return runtimeSnapshot;
    }

    public CanonicalTaskSnapshot getTaskSnapshot() {
        return runtimeSnapshot;
    }

    public CanonicalTaskSnapshot getSnapshot() {
        return runtimeSnapshot;
    }

    public String getResultPortId() {
        return runtimeSnapshot.getResultPortId();
    }

    public String getResult() {
        return runtimeSnapshot.getResultPortId();
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
                .equals(value)) throw new IllegalArgumentException("Invalid player UUID.");
            return uuid;
        } catch (RuntimeException exception) {
            throw new IllegalArgumentException("Invalid player UUID.");
        }
    }
}
