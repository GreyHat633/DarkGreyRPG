package darkgrey.rpg.graph.canonical;

/** Immutable connection between two public Story logic boundary ports. */
public final class CanonicalStoryLogicConnection {

    private final String sourceStoryId;
    private final String sourcePortId;
    private final String targetStoryId;
    private final String targetPortId;

    public CanonicalStoryLogicConnection(String sourceStoryId, String sourcePortId, String targetStoryId,
        String targetPortId) {
        this.sourceStoryId = require(sourceStoryId, "sourceStoryId");
        this.sourcePortId = require(sourcePortId, "sourcePortId");
        this.targetStoryId = require(targetStoryId, "targetStoryId");
        this.targetPortId = require(targetPortId, "targetPortId");
    }

    public String getSourceStoryId() {
        return sourceStoryId;
    }

    public String getSourcePortId() {
        return sourcePortId;
    }

    public String getTargetStoryId() {
        return targetStoryId;
    }

    public String getTargetPortId() {
        return targetPortId;
    }

    public String getFromStoryId() {
        return sourceStoryId;
    }

    public String getFromPortId() {
        return sourcePortId;
    }

    public String getToStoryId() {
        return targetStoryId;
    }

    public String getToPortId() {
        return targetPortId;
    }

    @Override
    public boolean equals(Object other) {
        if (this == other) return true;
        if (!(other instanceof CanonicalStoryLogicConnection)) return false;
        CanonicalStoryLogicConnection that = (CanonicalStoryLogicConnection) other;
        return sourceStoryId.equals(that.sourceStoryId) && sourcePortId.equals(that.sourcePortId)
            && targetStoryId.equals(that.targetStoryId)
            && targetPortId.equals(that.targetPortId);
    }

    @Override
    public int hashCode() {
        int result = sourceStoryId.hashCode();
        result = 31 * result + sourcePortId.hashCode();
        result = 31 * result + targetStoryId.hashCode();
        result = 31 * result + targetPortId.hashCode();
        return result;
    }

    @Override
    public String toString() {
        return sourceStoryId + ":" + sourcePortId + " -> " + targetStoryId + ":" + targetPortId;
    }

    private static String require(String value, String name) {
        if (value == null || value.trim()
            .isEmpty()) throw new IllegalArgumentException(name + " cannot be blank.");
        return value;
    }
}
