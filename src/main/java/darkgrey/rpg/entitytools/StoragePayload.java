package darkgrey.rpg.entitytools;

import java.util.UUID;

/** Explicit contents of a Storage Box; identity is never hidden in entity NBT. */
public final class StoragePayload {

    private final StorageMode mode;
    private final EntityTemplate template;
    private final UUID originalUuid;
    private final String reservedNpcId;

    StoragePayload(StorageMode mode, EntityTemplate template, UUID originalUuid, String reservedNpcId) {
        if (mode == null) throw new IllegalArgumentException("Storage mode is required.");
        if (template == null) throw new IllegalArgumentException("Storage template is required.");
        if (mode == StorageMode.SURVIVAL && originalUuid == null)
            throw new IllegalArgumentException("Survival UUID is required.");
        if (mode == StorageMode.CREATIVE && reservedNpcId != null)
            throw new IllegalArgumentException("Creative storage cannot reserve NPC identity.");
        this.mode = mode;
        this.template = template;
        this.originalUuid = originalUuid;
        this.reservedNpcId = reservedNpcId == null || reservedNpcId.trim()
            .isEmpty() ? null : reservedNpcId.trim();
    }

    public static StoragePayload survival(EntityCapture capture, String reservedNpcId) {
        EntityCapture.requireLivingMob(capture);
        return new StoragePayload(
            StorageMode.SURVIVAL,
            EntityTemplate.fromCapture(capture),
            capture.getEntityUuid(),
            reservedNpcId);
    }

    public static StoragePayload creative(EntityCapture capture) {
        EntityCapture.requireLivingMob(capture);
        // Deliberately omit sourceNpcId: creative output has no effective or retained unique identity.
        EntityTemplate template = new EntityTemplate(
            capture.getEntityType(),
            capture.getConfiguration(),
            capture.getAppearance(),
            capture.getAttributes(),
            capture.getEquipment(),
            capture.getAi(),
            capture.getExtra(),
            null,
            capture.getGroups());
        return new StoragePayload(StorageMode.CREATIVE, template, null, null);
    }

    static StoragePayload decoded(StorageMode mode, EntityTemplate template, UUID originalUuid, String reservedNpcId) {
        return new StoragePayload(mode, template, originalUuid, reservedNpcId);
    }

    public StorageMode getMode() {
        return mode;
    }

    public EntityTemplate getTemplate() {
        return template;
    }

    public UUID getOriginalUuid() {
        return originalUuid;
    }

    public String getReservedNpcId() {
        return reservedNpcId;
    }

    public boolean isIdentityReserved() {
        return mode == StorageMode.SURVIVAL && reservedNpcId != null;
    }

    public EntitySpawnSpec release() {
        return mode == StorageMode.SURVIVAL ? template.spawn(originalUuid, reservedNpcId) : template.spawnFresh();
    }
}
