package darkgrey.rpg.task.instance;

import java.util.UUID;

import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.task.runtime.CanonicalTaskEvent;
import darkgrey.rpg.task.runtime.CanonicalTaskRuntime;

/** Server-neutral owner of one canonical Task runtime. */
public final class CanonicalTaskInstance {

    private final UUID playerUuid;
    private final String storyInstanceId;
    private final String taskNodePlacementId;
    private final String taskResourceId;
    private CanonicalTaskInstanceStatus status;
    private long activationTime;
    private Long settlementTime;
    private final CanonicalTaskRuntime runtime;

    private CanonicalTaskInstance(UUID playerUuid, String storyInstanceId, String taskNodePlacementId,
        String taskResourceId, CanonicalTaskInstanceStatus status, long activationTime, Long settlementTime,
        CanonicalTaskRuntime runtime) {
        if (playerUuid == null || blank(storyInstanceId)
            || blank(taskNodePlacementId)
            || blank(taskResourceId)
            || status == null
            || activationTime <= 0
            || runtime == null
            || !taskResourceId.equals(
                runtime.getResource()
                    .getId()))
            throw new IllegalArgumentException("Invalid Task instance.");
        this.playerUuid = playerUuid;
        this.storyInstanceId = storyInstanceId;
        this.taskNodePlacementId = taskNodePlacementId;
        this.taskResourceId = taskResourceId;
        this.status = status;
        this.activationTime = activationTime;
        this.settlementTime = settlementTime;
        this.runtime = runtime;
    }

    public static CanonicalTaskInstance start(UUID playerUuid, String storyInstanceId, String taskNodePlacementId,
        CanonicalGraphResource resource, long activationTime) {
        validateTime(activationTime);
        if (resource == null) throw new IllegalArgumentException("Task resource is required.");
        CanonicalTaskRuntime runtime = CanonicalTaskRuntime.start(resource);
        CanonicalTaskInstanceStatus status = runtime.isSettled() ? CanonicalTaskInstanceStatus.SETTLED
            : CanonicalTaskInstanceStatus.ACTIVE;
        Long settlement = runtime.isSettled() ? Long.valueOf(activationTime) : null;
        return new CanonicalTaskInstance(
            playerUuid,
            storyInstanceId,
            taskNodePlacementId,
            resource.getId(),
            status,
            activationTime,
            settlement,
            runtime);
    }

    public static CanonicalTaskInstance restore(CanonicalTaskInstanceSnapshot snapshot,
        CanonicalGraphResource resource) {
        if (snapshot == null || resource == null) throw new IllegalArgumentException("Restore input is required.");
        CanonicalTaskRuntime runtime = CanonicalTaskRuntime.restore(resource, snapshot.getRuntimeSnapshot());
        return new CanonicalTaskInstance(
            snapshot.getPlayerUuid(),
            snapshot.getStoryInstanceId(),
            snapshot.getTaskNodePlacementId(),
            snapshot.getTaskResourceId(),
            snapshot.getStatus(),
            snapshot.getActivationTime(),
            snapshot.getSettlementTime(),
            runtime);
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

    public boolean isActive() {
        return status == CanonicalTaskInstanceStatus.ACTIVE;
    }

    public boolean isSettled() {
        return status == CanonicalTaskInstanceStatus.SETTLED;
    }

    public boolean isCancelled() {
        return status == CanonicalTaskInstanceStatus.CANCELLED_BY_STORY_TERMINATION;
    }

    public boolean isError() {
        return status == CanonicalTaskInstanceStatus.ERROR;
    }

    public boolean isTerminal() {
        return status == CanonicalTaskInstanceStatus.SETTLED
            || status == CanonicalTaskInstanceStatus.CANCELLED_BY_STORY_TERMINATION
            || status == CanonicalTaskInstanceStatus.ERROR;
    }

    /** Marks an active instance as errored while retaining its complete runtime snapshot. */
    public boolean markError() {
        if (!isActive()) return false;
        status = CanonicalTaskInstanceStatus.ERROR;
        return true;
    }

    public boolean error() {
        return markError();
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

    public CanonicalTaskRuntime getRuntime() {
        return runtime;
    }

    public CanonicalTaskRuntime getTaskRuntime() {
        return runtime;
    }

    public String getResultPortId() {
        return runtime.getResultPortId();
    }

    public String getResult() {
        return runtime.getResultPortId();
    }

    /** Events are ignored for every terminal/non-active instance. */
    public boolean accept(CanonicalTaskEvent event, long eventTime) {
        if (!isActive()) return false;
        validateTime(eventTime);
        if (eventTime < activationTime) throw new IllegalArgumentException("Event timestamp precedes activation.");
        boolean changed = runtime.accept(event);
        if (runtime.isSettled()) {
            status = CanonicalTaskInstanceStatus.SETTLED;
            settlementTime = Long.valueOf(eventTime);
        }
        return changed;
    }

    /** Updates one formal Task Logic Input while preserving every objective's accumulated progress. */
    public boolean setLogicInput(String portId, boolean value, long eventTime) {
        if (!isActive()) return false;
        validateTime(eventTime);
        if (eventTime < activationTime) throw new IllegalArgumentException("Event timestamp precedes activation.");
        boolean changed = runtime.setLogicInput(portId, value);
        if (runtime.isSettled()) {
            status = CanonicalTaskInstanceStatus.SETTLED;
            settlementTime = Long.valueOf(eventTime);
        }
        return changed;
    }

    public boolean grantReward(String nodeId, long eventTime) {
        if (!isActive()) return false;
        validateTime(eventTime);
        if (eventTime < activationTime) throw new IllegalArgumentException("Event timestamp precedes activation.");
        boolean changed = runtime.grantReward(nodeId);
        if (runtime.isSettled()) {
            status = CanonicalTaskInstanceStatus.SETTLED;
            settlementTime = Long.valueOf(eventTime);
        }
        return changed;
    }

    public boolean accept(CanonicalTaskEvent event) {
        return accept(event, activationTime);
    }

    public boolean handle(CanonicalTaskEvent event, long eventTime) {
        return accept(event, eventTime);
    }

    public boolean applyEvent(CanonicalTaskEvent event, long eventTime) {
        return accept(event, eventTime);
    }

    public boolean handle(CanonicalTaskEvent event) {
        return accept(event, activationTime);
    }

    public boolean applyEvent(CanonicalTaskEvent event) {
        return accept(event, activationTime);
    }

    public boolean acceptEvent(CanonicalTaskEvent event, long eventTime) {
        return accept(event, eventTime);
    }

    public boolean acceptEvent(CanonicalTaskEvent event) {
        return accept(event, activationTime);
    }

    void cancel() {
        if (isActive()) status = CanonicalTaskInstanceStatus.CANCELLED_BY_STORY_TERMINATION;
    }

    public CanonicalTaskInstanceSnapshot snapshot() {
        return new CanonicalTaskInstanceSnapshot(
            playerUuid,
            storyInstanceId,
            taskNodePlacementId,
            taskResourceId,
            status,
            activationTime,
            settlementTime,
            runtime.snapshot());
    }

    public CanonicalTaskInstanceSnapshot createSnapshot() {
        return snapshot();
    }

    private static void validateTime(long time) {
        if (time <= 0) throw new IllegalArgumentException("Timestamp must be positive.");
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }
}
