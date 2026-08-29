package darkgrey.rpg.session.instance;

import java.util.UUID;

import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.session.runtime.CanonicalSessionRuntime;
import darkgrey.rpg.session.runtime.CanonicalSessionSnapshot;
import darkgrey.rpg.session.runtime.CanonicalSessionStatus;
import darkgrey.rpg.session.runtime.CanonicalSessionStep;

/** One server-neutral Session cursor. Runtime semantics remain in CanonicalSessionRuntime. */
public final class CanonicalSessionInstance {

    private final UUID playerUuid;
    private final String storyId;
    private final String aggregatePlacementId;
    private final long transportId;
    private final CanonicalSessionRuntime runtime;

    CanonicalSessionInstance(UUID playerUuid, String storyId, String aggregatePlacementId, long transportId,
        CanonicalSessionRuntime runtime) {
        if (playerUuid == null || blank(storyId) || blank(aggregatePlacementId) || transportId <= 0 || runtime == null)
            throw new IllegalArgumentException("Invalid Session instance.");
        this.playerUuid = playerUuid;
        this.storyId = storyId;
        this.aggregatePlacementId = aggregatePlacementId;
        this.transportId = transportId;
        this.runtime = runtime;
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
        return runtime.getResource()
            .getId();
    }

    public long getTransportId() {
        return transportId;
    }

    public long getSessionId() {
        return transportId;
    }

    public CanonicalSessionRuntime getRuntime() {
        return runtime;
    }

    public CanonicalSessionRuntime getSessionRuntime() {
        return runtime;
    }

    public CanonicalSessionStatus getStatus() {
        return runtime.getStatus();
    }

    public String getCurrentNodeId() {
        return runtime.getCurrentNodeId();
    }

    public CanonicalSessionStep getCurrentStep() {
        return runtime.getCurrentStep();
    }

    public boolean isActive() {
        return runtime.isActive();
    }

    public boolean isCompleted() {
        return runtime.isCompleted();
    }

    public boolean isFailed() {
        return runtime.isFailed();
    }

    /** Returns a detached identity and complete canonical runtime snapshot. */
    public CanonicalSessionInstanceSnapshot snapshot() {
        CanonicalSessionSnapshot snapshot = runtime.snapshot();
        return new CanonicalSessionInstanceSnapshot(
            playerUuid,
            storyId,
            aggregatePlacementId,
            getSessionResourceId(),
            transportId,
            snapshot);
    }

    public CanonicalSessionInstanceSnapshot createSnapshot() {
        return snapshot();
    }

    static CanonicalSessionInstance restore(CanonicalSessionInstanceSnapshot snapshot,
        CanonicalGraphResource resource) {
        if (snapshot == null || resource == null) throw new IllegalArgumentException("Restore input is required.");
        CanonicalSessionRuntime runtime = CanonicalSessionRuntime.restore(resource, snapshot.getRuntimeSnapshot());
        return new CanonicalSessionInstance(
            snapshot.getPlayerUuid(),
            snapshot.getStoryId(),
            snapshot.getAggregatePlacementId(),
            snapshot.getTransportId(),
            runtime);
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }
}
