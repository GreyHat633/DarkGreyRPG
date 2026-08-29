package darkgrey.rpg.entitytools;

import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

/** Strict schema codec for sanitized templates stored in ItemStack NBT. */
public final class EntityTemplateNbtCodec {

    public static final int SCHEMA_VERSION = 1;
    private static final int INT = 3, STRING = 8, LIST = 9, COMPOUND = 10;

    private EntityTemplateNbtCodec() {}

    public static NBTTagCompound encode(EntityTemplate template) {
        if (template == null) throw malformed("template");
        NBTTagCompound root = new NBTTagCompound();
        root.setInteger("schema_version", SCHEMA_VERSION);
        root.setTag("template", encodeTemplate(template));
        return root;
    }

    public static EntityTemplate decode(NBTTagCompound root) {
        requireKeys(root, set("schema_version", "template"), "template root");
        requireType(root, "schema_version", INT);
        requireType(root, "template", COMPOUND);
        if (root.getInteger("schema_version") != SCHEMA_VERSION) throw malformed("unsupported schema_version");
        return decodeTemplate(root.getCompoundTag("template"));
    }

    static NBTTagCompound encodeTemplate(EntityTemplate value) {
        NBTTagCompound result = new NBTTagCompound();
        result.setString("entity_type", value.getEntityType());
        result.setTag("configuration", value.getConfiguration());
        result.setTag("appearance", value.getAppearance());
        result.setTag("attributes", value.getAttributes());
        result.setTag("equipment", value.getEquipment());
        result.setTag("ai", value.getAi());
        result.setTag("extra", value.getExtra());
        result.setTag("groups", strings(value.getGroups()));
        if (value.getSourceNpcId() != null) result.setString("source_npc_id", value.getSourceNpcId());
        return result;
    }

    static EntityTemplate decodeTemplate(NBTTagCompound value) {
        requireKeys(
            value,
            set("entity_type", "configuration", "appearance", "attributes", "equipment", "ai", "extra", "groups"),
            "template",
            "source_npc_id");
        requireType(value, "entity_type", STRING);
        requireType(value, "configuration", COMPOUND);
        requireType(value, "appearance", COMPOUND);
        requireType(value, "attributes", COMPOUND);
        requireType(value, "equipment", COMPOUND);
        requireType(value, "ai", COMPOUND);
        requireType(value, "extra", COMPOUND);
        requireType(value, "groups", LIST);
        if (value.hasKey("source_npc_id")) requireType(value, "source_npc_id", STRING);
        EntityTemplateSanitizer.requireSanitized(value.getCompoundTag("configuration"));
        EntityTemplateSanitizer.requireSanitized(value.getCompoundTag("appearance"));
        EntityTemplateSanitizer.requireSanitized(value.getCompoundTag("attributes"));
        EntityTemplateSanitizer.requireSanitized(value.getCompoundTag("equipment"));
        EntityTemplateSanitizer.requireSanitized(value.getCompoundTag("ai"));
        EntityTemplateSanitizer.requireSanitized(value.getCompoundTag("extra"));
        List<String> groups = decodeStrings(value.getTagList("groups", STRING));
        return new EntityTemplate(
            value.getString("entity_type"),
            value.getCompoundTag("configuration"),
            value.getCompoundTag("appearance"),
            value.getCompoundTag("attributes"),
            value.getCompoundTag("equipment"),
            value.getCompoundTag("ai"),
            value.getCompoundTag("extra"),
            value.hasKey("source_npc_id") ? value.getString("source_npc_id") : null,
            groups);
    }

    private static NBTTagList strings(List<String> values) {
        NBTTagList result = new NBTTagList();
        for (String value : values) result.appendTag(new net.minecraft.nbt.NBTTagString(value));
        return result;
    }

    private static List<String> decodeStrings(NBTTagList values) {
        if (values == null) throw malformed("groups");
        List<String> result = new ArrayList<String>();
        Set<String> seen = new HashSet<String>();
        for (int i = 0; i < values.tagCount(); i++) {
            NBTTagList remaining = (NBTTagList) values.copy();
            for (int j = 0; j < i; j++) remaining.removeTag(0);
            net.minecraft.nbt.NBTBase element = remaining.removeTag(0);
            if (!(element instanceof net.minecraft.nbt.NBTTagString)) throw malformed("groups element type");
            String value = values.getStringTagAt(i);
            if (!seen.add(value)) throw malformed("duplicate group");
            result.add(value);
        }
        return result;
    }

    static void requireKeys(NBTTagCompound value, Set<String> expected, String label, String... optional) {
        if (value == null) throw malformed(label + " is required");
        Set<String> allowed = new HashSet<String>(expected);
        for (String key : optional) allowed.add(key);
        for (String key : value.func_150296_c())
            if (!allowed.contains(key)) throw malformed(label + " contains unsupported key '" + key + "'");
        for (String key : expected) if (!value.hasKey(key)) throw malformed(label + " is missing '" + key + "'");
    }

    static void requireType(NBTTagCompound value, String key, int type) {
        if (!value.hasKey(key, type)) throw malformed(key + " has wrong type");
    }

    static IllegalArgumentException malformed(String message) {
        return new IllegalArgumentException("Malformed entity tool payload: " + message);
    }

    private static Set<String> set(String... values) {
        Set<String> result = new HashSet<String>();
        for (String value : values) result.add(value);
        return result;
    }
}
