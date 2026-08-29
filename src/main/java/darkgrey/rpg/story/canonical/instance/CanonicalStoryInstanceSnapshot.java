package darkgrey.rpg.story.canonical.instance;

import java.util.UUID;

import darkgrey.rpg.story.canonical.runtime.CanonicalStorySnapshot;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryStatus;

/** Detached player identity, timestamps, and complete canonical Story cursor state. */
public final class CanonicalStoryInstanceSnapshot {

    private final UUID playerUuid;
    private final String storyId;
    private final long activationTime;
    private final Long terminalTime;
    private final CanonicalStorySnapshot runtimeSnapshot;

    public CanonicalStoryInstanceSnapshot(UUID playerUuid, String storyId, long activationTime, Long terminalTime,
        CanonicalStorySnapshot runtimeSnapshot) {
        if (playerUuid == null || blank(storyId)
            || activationTime <= 0
            || runtimeSnapshot == null
            || !storyId.equals(runtimeSnapshot.getResourceId()))
            throw new IllegalArgumentException("Invalid canonical Story instance snapshot.");
        boolean terminal = runtimeSnapshot.getStatus() != CanonicalStoryStatus.ACTIVE;
        if (terminal != (terminalTime != null))
            throw new IllegalArgumentException("Canonical Story terminal time contradicts runtime status.");
        if (terminalTime != null && (terminalTime.longValue() <= 0 || terminalTime.longValue() < activationTime))
            throw new IllegalArgumentException("Invalid canonical Story terminal time.");
        this.playerUuid = playerUuid;
        this.storyId = storyId;
        this.activationTime = activationTime;
        this.terminalTime = terminalTime;
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

    public String getStoryInstanceId() {
        return storyId;
    }

    public long getActivationTime() {
        return activationTime;
    }

    public Long getTerminalTime() {
        return terminalTime;
    }

    public Long getCompletionTime() {
        return terminalTime;
    }

    public CanonicalStorySnapshot getRuntimeSnapshot() {
        return runtimeSnapshot;
    }

    public CanonicalStorySnapshot getSnapshot() {
        return runtimeSnapshot;
    }

    public CanonicalStoryStatus getStatus() {
        return runtimeSnapshot.getStatus();
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }
}
