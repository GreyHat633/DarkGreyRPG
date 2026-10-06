package darkgrey.rpg.identity;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

/** Current structured resource address contract for world-owned bindings. */
public final class ResourceAddressNbt {

    public static final String IDENTITY_FORMAT = "story-uid-v1";

    private ResourceAddressNbt() {}

    public static void requireFormat(NBTTagCompound root) {
        if (!root.hasKey("identity_format", 8) || !IDENTITY_FORMAT.equals(root.getString("identity_format")))
            throw new IllegalArgumentException("Unsupported saved identity format");
    }

    public static NBTTagList compounds(NBTTagCompound root, String field) {
        if (!root.hasKey(field, 9)) throw new IllegalArgumentException("Compound list required: " + field);
        NBTTagList list = (NBTTagList) root.getTag(field);
        if (list.tagCount() > 0 && list.func_150303_d() != 10)
            throw new IllegalArgumentException("Compound entries required: " + field);
        return list;
    }

    public static NBTTagCompound write(String key, ResourceAddress.Kind expected) {
        ResourceAddress address = ResourceAddress.fromKey(key);
        if (address.getKind() != expected) throw new IllegalArgumentException("Resource kind mismatch");
        NBTTagCompound value = new NBTTagCompound();
        value.setString(
            "story_uid",
            address.getStoryUid()
                .getValue());
        value.setString(
            "kind",
            address.getKind()
                .getToken());
        value.setString("local_id", address.getLocalId());
        return value;
    }

    public static String read(NBTTagCompound parent, String field, ResourceAddress.Kind expected) {
        if (!parent.hasKey(field, 10))
            throw new IllegalArgumentException("Structured resource address required: " + field);
        NBTTagCompound value = parent.getCompoundTag(field);
        return read(value, expected);
    }

    public static String read(NBTTagCompound value, ResourceAddress.Kind expected) {
        if (value.func_150296_c()
            .size() != 3 || !value.hasKey("story_uid", 8)
            || !value.hasKey("kind", 8)
            || !value.hasKey("local_id", 8)) throw new IllegalArgumentException("Invalid resource address fields");
        ResourceAddress address = new ResourceAddress(
            StoryUid.parse(value.getString("story_uid")),
            ResourceAddress.Kind.parse(value.getString("kind")),
            value.getString("local_id"));
        if (address.getKind() != expected) throw new IllegalArgumentException("Resource kind mismatch");
        return address.toKey();
    }
}
