package darkgrey.rpg.entitytools;

import java.util.HashSet;
import java.util.Set;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;

/** Strict codec for one Storage Box ItemStack payload. */
public final class StorageBoxNbtCodec {

    public static final int SCHEMA_VERSION = 1;
    private static final int INT = 3, BYTE = 1, STRING = 8, COMPOUND = 10;

    private StorageBoxNbtCodec() {}

    public static NBTTagCompound encode(StoragePayload payload) {
        if (payload == null) throw EntityTemplateNbtCodec.malformed("storage payload");
        NBTTagCompound root = new NBTTagCompound();
        root.setInteger("schema_version", SCHEMA_VERSION);
        root.setBoolean("occupied", true);
        NBTTagCompound value = new NBTTagCompound();
        value.setString(
            "mode",
            payload.getMode()
                .name());
        value.setTag("template", EntityTemplateNbtCodec.encodeTemplate(payload.getTemplate()));
        if (payload.getOriginalUuid() != null) value.setString(
            "original_uuid",
            payload.getOriginalUuid()
                .toString());
        if (payload.getReservedNpcId() != null) value.setString("reserved_npc_id", payload.getReservedNpcId());
        value.setBoolean("identity_reserved", payload.isIdentityReserved());
        root.setTag("payload", value);
        return root;
    }

    public static StoragePayload decode(NBTTagCompound root) {
        EntityTemplateNbtCodec.requireKeys(root, set("schema_version", "occupied", "payload"), "storage");
        EntityTemplateNbtCodec.requireType(root, "schema_version", INT);
        EntityTemplateNbtCodec.requireType(root, "occupied", BYTE);
        EntityTemplateNbtCodec.requireType(root, "payload", COMPOUND);
        if (root.getInteger("schema_version") != SCHEMA_VERSION)
            throw EntityTemplateNbtCodec.malformed("unsupported schema_version");
        if (!root.getBoolean("occupied")) throw EntityTemplateNbtCodec.malformed("occupied must be true for a payload");
        NBTTagCompound value = root.getCompoundTag("payload");
        EntityTemplateNbtCodec.requireKeys(
            value,
            set("mode", "template", "identity_reserved"),
            "storage payload",
            "original_uuid",
            "reserved_npc_id");
        EntityTemplateNbtCodec.requireType(value, "mode", STRING);
        EntityTemplateNbtCodec.requireType(value, "template", COMPOUND);
        EntityTemplateNbtCodec.requireType(value, "identity_reserved", BYTE);
        StorageMode mode;
        try {
            mode = StorageMode.valueOf(value.getString("mode"));
        } catch (RuntimeException exception) {
            throw EntityTemplateNbtCodec.malformed("storage mode");
        }
        UUID original = null;
        if (value.hasKey("original_uuid")) {
            EntityTemplateNbtCodec.requireType(value, "original_uuid", STRING);
            try {
                original = UUID.fromString(value.getString("original_uuid"));
            } catch (RuntimeException exception) {
                throw EntityTemplateNbtCodec.malformed("original_uuid");
            }
        }
        String npc = null;
        if (value.hasKey("reserved_npc_id")) {
            EntityTemplateNbtCodec.requireType(value, "reserved_npc_id", STRING);
            npc = value.getString("reserved_npc_id");
        }
        boolean reserved = value.getBoolean("identity_reserved");
        EntityTemplate template = EntityTemplateNbtCodec.decodeTemplate(value.getCompoundTag("template"));
        if (mode == StorageMode.SURVIVAL && original == null)
            throw EntityTemplateNbtCodec.malformed("survival original_uuid");
        if (mode == StorageMode.CREATIVE
            && (original != null || npc != null || reserved || template.getSourceNpcId() != null))
            throw EntityTemplateNbtCodec.malformed("creative identity");
        if (mode == StorageMode.SURVIVAL && reserved != (npc != null))
            throw EntityTemplateNbtCodec.malformed("identity reservation");
        return mode == StorageMode.SURVIVAL ? newPayloadSurvival(template, original, npc)
            : newPayloadCreative(template);
    }

    private static StoragePayload newPayloadSurvival(EntityTemplate template, UUID original, String npc) {
        return StoragePayload.decoded(StorageMode.SURVIVAL, template, original, npc);
    }

    private static StoragePayload newPayloadCreative(EntityTemplate template) {
        return StoragePayload.decoded(StorageMode.CREATIVE, template, null, null);
    }

    private static Set<String> set(String... values) {
        Set<String> result = new HashSet<String>();
        for (String value : values) result.add(value);
        return result;
    }
}
