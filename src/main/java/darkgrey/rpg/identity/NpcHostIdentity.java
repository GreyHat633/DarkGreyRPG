package darkgrey.rpg.identity;

import java.util.UUID;

/**
 * Stable description of the real entity that hosts one unique DGR NPC ID.
 *
 * <p>
 * The NPC ID is deliberately not part of this value and is never written to
 * the entity. The UUID is the registry key; the remaining fields are retained
 * only for diagnostics and compatibility-assisted transfers.
 * </p>
 */
public final class NpcHostIdentity {

    private final UUID entityUuid;
    private final String entityType;
    private final int lastKnownDimension;
    private final String compatibilityKey;

    public NpcHostIdentity(UUID entityUuid, String entityType, int lastKnownDimension) {
        this(entityUuid, entityType, lastKnownDimension, null);
    }

    public NpcHostIdentity(UUID entityUuid, String entityType, int lastKnownDimension, String compatibilityKey) {
        if (entityUuid == null) throw new IllegalArgumentException("Entity UUID is required.");
        if (blank(entityType)) throw new IllegalArgumentException("Entity type is required.");
        if (compatibilityKey != null && compatibilityKey.trim()
            .isEmpty()) throw new IllegalArgumentException("Compatibility key cannot be blank.");
        this.entityUuid = entityUuid;
        this.entityType = entityType.trim();
        this.lastKnownDimension = lastKnownDimension;
        this.compatibilityKey = compatibilityKey == null ? null : compatibilityKey.trim();
    }

    public UUID getEntityUuid() {
        return entityUuid;
    }

    public String getEntityType() {
        return entityType;
    }

    public int getLastKnownDimension() {
        return lastKnownDimension;
    }

    public String getCompatibilityKey() {
        return compatibilityKey;
    }

    @Override
    public boolean equals(Object value) {
        if (this == value) return true;
        if (!(value instanceof NpcHostIdentity)) return false;
        NpcHostIdentity other = (NpcHostIdentity) value;
        return entityUuid.equals(other.entityUuid) && entityType.equals(other.entityType)
            && lastKnownDimension == other.lastKnownDimension
            && equal(compatibilityKey, other.compatibilityKey);
    }

    @Override
    public int hashCode() {
        int result = entityUuid.hashCode();
        result = 31 * result + entityType.hashCode();
        result = 31 * result + lastKnownDimension;
        return 31 * result + (compatibilityKey == null ? 0 : compatibilityKey.hashCode());
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }

    private static boolean equal(Object left, Object right) {
        return left == null ? right == null : left.equals(right);
    }
}
