package darkgrey.rpg.story.canonical.instance;

import java.util.Map;
import java.util.UUID;

import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.story.canonical.CanonicalStoryPendingContinuation;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryRepeatPolicy;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryRuntime;
import darkgrey.rpg.story.canonical.runtime.CanonicalStorySnapshot;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryStatus;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot;

/** Mutable server-neutral owner of one player plus Story canonical cursor. */
public final class CanonicalStoryInstance {

    private final UUID playerUuid;
    private final String storyId;
    private final long activationTime;
    private final CanonicalStoryRuntime runtime;
    private Long terminalTime;

    private CanonicalStoryInstance(UUID playerUuid, String storyId, long activationTime, Long terminalTime,
        CanonicalStoryRuntime runtime) {
        if (playerUuid == null || blank(storyId) || activationTime <= 0 || runtime == null)
            throw new IllegalArgumentException("Invalid canonical Story instance.");
        this.playerUuid = playerUuid;
        this.storyId = storyId;
        this.activationTime = activationTime;
        this.terminalTime = terminalTime;
        this.runtime = runtime;
        validateTerminalTime();
    }

    public static CanonicalStoryInstance start(UUID playerUuid, CanonicalGraphResource resource, String triggerPortId,
        CanonicalStoryRepeatPolicy repeatPolicy, long activationTime) {
        return start(
            playerUuid,
            resource,
            triggerPortId,
            repeatPolicy,
            java.util.Collections.<String, Boolean>emptyMap(),
            activationTime);
    }

    public static CanonicalStoryInstance start(UUID playerUuid, CanonicalGraphResource resource, String triggerPortId,
        CanonicalStoryRepeatPolicy repeatPolicy, Map<String, Boolean> logicInputs, long activationTime) {
        if (resource == null) throw new IllegalArgumentException("Canonical Story resource is required.");
        CanonicalStoryRuntime runtime = CanonicalStoryRuntime.start(resource, triggerPortId, repeatPolicy, logicInputs);
        return new CanonicalStoryInstance(
            playerUuid,
            resource.getId(),
            activationTime,
            runtime.getStatus() == CanonicalStoryStatus.ACTIVE ? null : Long.valueOf(activationTime),
            runtime);
    }

    public static CanonicalStoryInstance restore(CanonicalStoryInstanceSnapshot snapshot,
        CanonicalGraphResource resource) {
        if (snapshot == null) throw new IllegalArgumentException("Canonical Story instance snapshot is required.");
        return new CanonicalStoryInstance(
            snapshot.getPlayerUuid(),
            snapshot.getStoryId(),
            snapshot.getActivationTime(),
            snapshot.getTerminalTime(),
            CanonicalStoryRuntime.restore(resource, snapshot.getRuntimeSnapshot()));
    }

    public boolean resumeSession(CanonicalStoryPendingContinuation continuation, long eventTime) {
        runtime.resumeSession(continuation);
        captureTerminalTime(eventTime);
        return true;
    }

    public boolean resumeTask(CanonicalTaskInstanceSnapshot task, long eventTime) {
        runtime.resumeTask(task);
        captureTerminalTime(eventTime);
        return true;
    }

    public boolean completeAction(String actionNodeId, long eventTime) {
        runtime.completeAction(actionNodeId);
        captureTerminalTime(eventTime);
        return true;
    }

    public boolean resumeActor(String actorId, long eventTime) {
        runtime.resumeActor(actorId);
        captureTerminalTime(eventTime);
        return true;
    }

    public boolean resumeRegion(int dimension, double x, double y, double z, long eventTime) {
        runtime.resumeRegion(dimension, x, y, z);
        captureTerminalTime(eventTime);
        return true;
    }

    /** Applies one durable public Logic input and advances a waiting Condition when its selected outlet changes. */
    public boolean setLogicInput(String portId, boolean value, long eventTime) {
        boolean resumed = runtime.setLogicInput(portId, value);
        captureTerminalTime(eventTime);
        return resumed;
    }

    /** Applies one durable, coherent public Logic input snapshot. */
    public boolean setLogicInputs(Map<String, Boolean> values, long eventTime) {
        boolean resumed = runtime.setLogicInputs(values);
        captureTerminalTime(eventTime);
        return resumed;
    }

    public boolean markError(long eventTime) {
        boolean changed = runtime.markError();
        if (changed) captureTerminalTime(eventTime);
        return changed;
    }

    public CanonicalStoryInstanceSnapshot snapshot() {
        return new CanonicalStoryInstanceSnapshot(
            playerUuid,
            storyId,
            activationTime,
            terminalTime,
            runtime.snapshot());
    }

    public UUID getPlayerUuid() {
        return playerUuid;
    }

    public String getStoryId() {
        return storyId;
    }

    public CanonicalStoryRuntime getRuntime() {
        return runtime;
    }

    public CanonicalStoryStatus getStatus() {
        return runtime.getStatus();
    }

    public boolean isActive() {
        return getStatus() == CanonicalStoryStatus.ACTIVE;
    }

    public long getActivationTime() {
        return activationTime;
    }

    public Long getTerminalTime() {
        return terminalTime;
    }

    private void captureTerminalTime(long eventTime) {
        if (runtime.getStatus() == CanonicalStoryStatus.ACTIVE) return;
        if (terminalTime == null) {
            if (eventTime <= 0 || eventTime < activationTime)
                throw new IllegalArgumentException("Invalid canonical Story terminal event time.");
            terminalTime = Long.valueOf(eventTime);
        }
    }

    private void validateTerminalTime() {
        CanonicalStorySnapshot snapshot = runtime.snapshot();
        if ((snapshot.getStatus() != CanonicalStoryStatus.ACTIVE) != (terminalTime != null))
            throw new IllegalArgumentException("Canonical Story terminal time contradicts runtime state.");
        if (terminalTime != null && (terminalTime.longValue() <= 0 || terminalTime.longValue() < activationTime))
            throw new IllegalArgumentException("Invalid canonical Story terminal time.");
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }
}
