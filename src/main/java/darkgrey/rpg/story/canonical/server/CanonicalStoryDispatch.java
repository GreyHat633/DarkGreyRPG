package darkgrey.rpg.story.canonical.server;

import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.Map;

import com.google.gson.JsonElement;
import com.google.gson.JsonParser;

import darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceSnapshot;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryStartDisposition;

/** Detached instruction for the Forge coordinator after a Story cursor advances. */
public final class CanonicalStoryDispatch {

    private final CanonicalStoryDispatchKind kind;
    private final CanonicalStoryInstanceSnapshot snapshot;
    private final String placementId;
    private final String resourceId;
    private final boolean activationLogic;
    private final Map<String, JsonElement> actionProperties;
    private final String targetStoryId;
    private final Integer waitDimension;
    private final Double waitX;
    private final Double waitY;
    private final Double waitZ;
    private final Double waitRadius;
    private final CanonicalStoryStartDisposition startDisposition;

    CanonicalStoryDispatch withStartDisposition(CanonicalStoryStartDisposition disposition) {
        return new CanonicalStoryDispatch(
            kind,
            snapshot,
            placementId,
            resourceId,
            activationLogic,
            actionProperties,
            targetStoryId,
            waitDimension,
            waitX,
            waitY,
            waitZ,
            waitRadius,
            disposition);
    }

    public CanonicalStoryStartDisposition getStartDisposition() {
        return startDisposition;
    }

    CanonicalStoryDispatch(CanonicalStoryDispatchKind kind, CanonicalStoryInstanceSnapshot snapshot, String placementId,
        String resourceId, boolean activationLogic, Map<String, JsonElement> actionProperties, String targetStoryId) {
        this(
            kind,
            snapshot,
            placementId,
            resourceId,
            activationLogic,
            actionProperties,
            targetStoryId,
            null,
            null,
            null,
            null,
            null);
    }

    CanonicalStoryDispatch(CanonicalStoryDispatchKind kind, CanonicalStoryInstanceSnapshot snapshot, String placementId,
        String resourceId, boolean activationLogic, Map<String, JsonElement> actionProperties, String targetStoryId,
        Integer waitDimension, Double waitX, Double waitY, Double waitZ, Double waitRadius) {
        this(
            kind,
            snapshot,
            placementId,
            resourceId,
            activationLogic,
            actionProperties,
            targetStoryId,
            waitDimension,
            waitX,
            waitY,
            waitZ,
            waitRadius,
            null);
    }

    private CanonicalStoryDispatch(CanonicalStoryDispatchKind kind, CanonicalStoryInstanceSnapshot snapshot,
        String placementId, String resourceId, boolean activationLogic, Map<String, JsonElement> actionProperties,
        String targetStoryId, Integer waitDimension, Double waitX, Double waitY, Double waitZ, Double waitRadius,
        CanonicalStoryStartDisposition startDisposition) {
        if (kind == null || snapshot == null)
            throw new IllegalArgumentException("Canonical Story dispatch is required.");
        this.kind = kind;
        this.snapshot = snapshot;
        this.placementId = placementId;
        this.resourceId = resourceId;
        this.activationLogic = activationLogic;
        LinkedHashMap<String, JsonElement> detached = new LinkedHashMap<String, JsonElement>();
        if (actionProperties != null)
            for (Map.Entry<String, JsonElement> entry : actionProperties.entrySet()) detached.put(
                entry.getKey(),
                new JsonParser().parse(
                    entry.getValue()
                        .toString()));
        this.actionProperties = Collections.unmodifiableMap(detached);
        this.targetStoryId = targetStoryId;
        this.waitDimension = waitDimension;
        this.waitX = waitX;
        this.waitY = waitY;
        this.waitZ = waitZ;
        this.waitRadius = waitRadius;
        this.startDisposition = startDisposition;
    }

    public CanonicalStoryDispatchKind getKind() {
        return kind;
    }

    public CanonicalStoryInstanceSnapshot getSnapshot() {
        return snapshot;
    }

    public String getPlacementId() {
        return placementId;
    }

    public String getResourceId() {
        return resourceId;
    }

    public boolean getActivationLogic() {
        return activationLogic;
    }

    public Map<String, JsonElement> getActionProperties() {
        return actionProperties;
    }

    public String getTargetStoryId() {
        return targetStoryId;
    }

    public String getActorId() {
        return kind == CanonicalStoryDispatchKind.ACTOR_INTERACT || kind == CanonicalStoryDispatchKind.INTERACT_ACTOR
            ? resourceId
            : null;
    }

    public String getWaitActorId() {
        return getActorId();
    }

    public Integer getDimension() {
        return waitDimension;
    }

    public Integer getRegionDimension() {
        return waitDimension;
    }

    public Double getX() {
        return waitX;
    }

    public Double getRegionX() {
        return waitX;
    }

    public Double getY() {
        return waitY;
    }

    public Double getRegionY() {
        return waitY;
    }

    public Double getZ() {
        return waitZ;
    }

    public Double getRegionZ() {
        return waitZ;
    }

    public Double getRadius() {
        return waitRadius;
    }

    public Double getRegionRadius() {
        return waitRadius;
    }
}
