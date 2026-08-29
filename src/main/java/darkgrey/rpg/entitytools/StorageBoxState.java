package darkgrey.rpg.entitytools;

import java.util.HashSet;
import java.util.Set;

import net.minecraft.nbt.NBTTagCompound;

/** One Storage Box's server-authoritative contents and mode-independent state. */
public final class StorageBoxState {

    public static final int SCHEMA_VERSION = 1;
    private StoragePayload payload;

    public synchronized boolean isOccupied() {
        return payload != null;
    }

    public synchronized StoragePayload getPayload() {
        return payload;
    }

    /**
     * Validates and encodes before assigning contents. The adapter may remove the source entity
     * only after this method returns; no world operation is hidden here.
     */
    public synchronized StoragePayload capture(EntityCapture capture, StorageMode mode, String externallyBoundNpcId) {
        if (payload != null) throw new IllegalStateException("Storage Box is already occupied.");
        if (mode == null) throw new IllegalArgumentException("Storage mode is required.");
        StoragePayload candidate = mode == StorageMode.SURVIVAL ? StoragePayload.survival(capture, externallyBoundNpcId)
            : StoragePayload.creative(capture);
        StorageBoxNbtCodec.decode(StorageBoxNbtCodec.encode(candidate));
        payload = candidate;
        return candidate;
    }

    public synchronized StoragePayload captureSurvival(EntityCapture capture, String externallyBoundNpcId) {
        return capture(capture, StorageMode.SURVIVAL, externallyBoundNpcId);
    }

    public synchronized StoragePayload captureCreative(EntityCapture capture) {
        return capture(capture, StorageMode.CREATIVE, null);
    }

    /** Returns a validated spawn request without changing the box. */
    public synchronized EntitySpawnSpec previewRelease() {
        if (payload == null) throw new IllegalStateException("Storage Box is empty.");
        return payload.release();
    }

    /** Survival restore consumes the box only after producing a valid spawn request. */
    public synchronized EntitySpawnSpec release() {
        EntitySpawnSpec result = previewRelease();
        consumeSuccessfulRelease();
        return result;
    }

    /** Called by an adapter after its actual world spawn succeeds. */
    public synchronized void consumeSuccessfulRelease() {
        if (payload == null) throw new IllegalStateException("Storage Box is empty.");
        if (payload.getMode() == StorageMode.SURVIVAL) payload = null;
    }

    public synchronized NBTTagCompound writeToNBT() {
        if (payload == null) {
            NBTTagCompound root = new NBTTagCompound();
            root.setInteger("schema_version", SCHEMA_VERSION);
            root.setBoolean("occupied", false);
            return root;
        }
        return StorageBoxNbtCodec.encode(payload);
    }

    public synchronized void readFromNBT(NBTTagCompound root) {
        EntityTemplateNbtCodec.requireKeys(root, set("schema_version", "occupied"), "storage state", "payload");
        EntityTemplateNbtCodec.requireType(root, "schema_version", 3);
        EntityTemplateNbtCodec.requireType(root, "occupied", 1);
        if (root.getInteger("schema_version") != SCHEMA_VERSION)
            throw EntityTemplateNbtCodec.malformed("unsupported schema_version");
        StoragePayload candidate = null;
        if (root.getBoolean("occupied")) candidate = StorageBoxNbtCodec.decode(root);
        else {
            if (root.hasKey("payload")) throw EntityTemplateNbtCodec.malformed("empty storage has payload");
        }
        payload = candidate;
    }

    private static Set<String> set(String... values) {
        Set<String> result = new HashSet<String>();
        for (String value : values) result.add(value);
        return result;
    }
}
