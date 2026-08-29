package darkgrey.rpg.entitytools;

import java.util.Locale;

import net.minecraft.nbt.NBTBase;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

/** Central defensive copy/filter for ordinary entity data. */
final class EntityTemplateSanitizer {

    private EntityTemplateSanitizer() {}

    static NBTTagCompound sanitize(NBTTagCompound input) {
        NBTTagCompound result = new NBTTagCompound();
        if (input == null) return result;
        for (String key : input.func_150296_c()) {
            if (forbidden(key)) continue;
            NBTBase value = input.getTag(key);
            result.setTag(key, sanitizeTag(value));
        }
        return result;
    }

    static void requireSanitized(NBTTagCompound input) {
        if (input == null) throw new IllegalArgumentException("Template compound is required.");
        for (String key : input.func_150296_c()) {
            if (forbidden(key))
                throw new IllegalArgumentException("Template contains transient identity key '" + key + "'.");
            requireSanitizedTag(input.getTag(key));
        }
    }

    private static void requireSanitizedTag(NBTBase value) {
        if (value instanceof NBTTagCompound) {
            requireSanitized((NBTTagCompound) value);
        } else if (value instanceof NBTTagList) {
            NBTTagList remaining = (NBTTagList) value.copy();
            while (remaining.tagCount() > 0) requireSanitizedTag(remaining.removeTag(0));
        }
    }

    private static NBTBase sanitizeTag(NBTBase value) {
        if (value instanceof NBTTagCompound) return sanitize((NBTTagCompound) value);
        if (value instanceof NBTTagList) {
            NBTTagList source = (NBTTagList) value;
            NBTTagList result = new NBTTagList();
            NBTTagList remaining = (NBTTagList) source.copy();
            while (remaining.tagCount() > 0) result.appendTag(sanitizeTag(remaining.removeTag(0)));
            return result;
        }
        return value.copy();
    }

    static boolean forbidden(String key) {
        if (key == null) return true;
        String normalized = key.toLowerCase(Locale.ROOT)
            .replace("_", "")
            .replace("-", "");
        return normalized.equals("uuid") || normalized.equals("uuidmost")
            || normalized.equals("uuidleast")
            || normalized.equals("uniqueuuid")
            || normalized.equals("persistentuuid")
            || normalized.equals("pos")
            || normalized.equals("position")
            || normalized.equals("motion")
            || normalized.equals("rotation")
            || normalized.equals("dimension")
            || normalized.equals("world")
            || normalized.equals("worlduuid")
            || dgrIdentityKey(normalized.replace(".", ""));
    }

    private static boolean dgrIdentityKey(String normalized) {
        if (normalized.startsWith("dgr")) return identitySuffix(normalized.substring(3));
        if (normalized.startsWith("darkgreyrpg")) return identitySuffix(normalized.substring(11));
        return false;
    }

    private static boolean identitySuffix(String suffix) {
        return suffix.equals("npcid") || suffix.equals("actorid")
            || suffix.equals("entityid")
            || suffix.equals("uniqueid")
            || suffix.equals("uuid")
            || suffix.equals("identity")
            || suffix.equals("identityid");
    }
}
