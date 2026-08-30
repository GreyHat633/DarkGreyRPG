package darkgrey.rpg.item.identity;

import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

import net.minecraft.item.ItemStack;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;
import net.minecraft.server.MinecraftServer;
import net.minecraft.world.WorldSavedData;
import net.minecraft.world.WorldServer;
import net.minecraft.world.storage.MapStorage;

/** Overworld-owned persistence for server-established Item ID and Group bindings. */
public final class ItemIdentitySavedData extends WorldSavedData {

    public static final String DATA_NAME = "darkgrey_rpg_item_identities";
    private static final int SCHEMA_VERSION = 1;
    private ItemIdentityRegistry registry = new ItemIdentityRegistry();
    private long revision;

    public ItemIdentitySavedData() {
        this(DATA_NAME);
    }

    public ItemIdentitySavedData(String name) {
        super(name);
    }

    public static ItemIdentitySavedData get() {
        MinecraftServer server = MinecraftServer.getServer();
        if (server == null) throw new IllegalStateException("Minecraft server is unavailable.");
        WorldServer overworld = server.worldServerForDimension(0);
        if (overworld == null) throw new IllegalStateException("Overworld is unavailable.");
        return get(overworld.mapStorage);
    }

    public static ItemIdentitySavedData get(MapStorage storage) {
        if (storage == null) throw new IllegalArgumentException("MapStorage is required.");
        WorldSavedData loaded = storage.loadData(ItemIdentitySavedData.class, DATA_NAME);
        if (loaded instanceof ItemIdentitySavedData) return (ItemIdentitySavedData) loaded;
        ItemIdentitySavedData created = new ItemIdentitySavedData();
        storage.setData(DATA_NAME, created);
        return created;
    }

    public synchronized boolean bindItem(String itemId, ItemStackDefinition definition) {
        boolean changed = registry.bindItem(itemId, definition);
        if (changed) {
            revision++;
            markDirty();
        }
        return changed;
    }

    public synchronized boolean unbindItem(String itemId) {
        boolean changed = registry.unbindItem(itemId);
        if (changed) {
            revision++;
            markDirty();
        }
        return changed;
    }

    public synchronized boolean addGroupMember(String groupId, ItemGroupMember member) {
        boolean changed = registry.addGroupMember(groupId, member);
        if (changed) {
            revision++;
            markDirty();
        }
        return changed;
    }

    public synchronized boolean removeGroupMember(String groupId, ItemGroupMember member) {
        boolean changed = registry.removeGroupMember(groupId, member);
        if (changed) {
            revision++;
            markDirty();
        }
        return changed;
    }

    public synchronized ItemStackDefinition getItem(String itemId) {
        return registry.getItem(itemId);
    }

    public synchronized boolean matchesItem(String itemId, ItemStack stack) {
        return registry.matchesItem(itemId, stack);
    }

    public synchronized boolean matchesGroup(String groupId, ItemStack stack) {
        return registry.matchesGroup(groupId, stack);
    }

    public synchronized List<String> matchingItemIds(ItemStack stack) {
        return registry.matchingItemIds(stack);
    }

    public synchronized List<String> matchingGroupIds(ItemStack stack) {
        return registry.matchingGroupIds(stack);
    }

    public synchronized List<ItemGroupMember> getGroup(String groupId) {
        return registry.getGroup(groupId);
    }

    public synchronized long getRevision() {
        return revision;
    }

    @Override
    public synchronized void readFromNBT(NBTTagCompound root) {
        if (root == null) throw new IllegalArgumentException("Item identity root is required.");
        Set<String> rootKeys = new HashSet<String>(root.func_150296_c());
        if (!rootKeys.equals(set("schema_version", "items", "groups"))
            && !rootKeys.equals(set("schema_version", "revision", "items", "groups")))
            throw new IllegalArgumentException("Item identity root has unknown or missing keys.");
        requireType(root, "schema_version", 3);
        if (root.getInteger("schema_version") != SCHEMA_VERSION)
            throw new IllegalArgumentException("Unsupported Item identity schema_version.");
        NBTTagList items = list(root, "items");
        NBTTagList groups = list(root, "groups");
        List<ItemIdentityRegistry.ItemBinding> itemValues = new ArrayList<ItemIdentityRegistry.ItemBinding>();
        for (int index = 0; index < items.tagCount(); index++) {
            NBTTagCompound value = items.getCompoundTagAt(index);
            requireKeys(value, set("item_id", "definition"), "Item identity binding");
            requireType(value, "item_id", 8);
            requireType(value, "definition", 10);
            itemValues.add(
                new ItemIdentityRegistry.ItemBinding(
                    value.getString("item_id"),
                    decodeDefinition(value.getCompoundTag("definition"))));
        }
        List<ItemIdentityRegistry.GroupBinding> groupValues = new ArrayList<ItemIdentityRegistry.GroupBinding>();
        for (int index = 0; index < groups.tagCount(); index++) {
            NBTTagCompound value = groups.getCompoundTagAt(index);
            requireKeys(value, set("group_id", "match_mode", "definition"), "Item Group binding");
            requireType(value, "group_id", 8);
            requireType(value, "match_mode", 8);
            requireType(value, "definition", 10);
            groupValues.add(
                new ItemIdentityRegistry.GroupBinding(
                    value.getString("group_id"),
                    new ItemGroupMember(
                        ItemMatchMode.fromName(value.getString("match_mode")),
                        decodeDefinition(value.getCompoundTag("definition")))));
        }
        ItemIdentityRegistry candidate = new ItemIdentityRegistry();
        candidate.replaceAll(itemValues, groupValues);
        registry = candidate;
        revision = root.hasKey("revision", 4) ? root.getLong("revision") : 0L;
    }

    @Override
    public synchronized void writeToNBT(NBTTagCompound root) {
        if (root == null) throw new IllegalArgumentException("Output NBT is required.");
        for (String key : new HashSet<String>(root.func_150296_c())) root.removeTag(key);
        root.setInteger("schema_version", SCHEMA_VERSION);
        root.setLong("revision", revision);
        NBTTagList items = new NBTTagList();
        for (ItemIdentityRegistry.ItemBinding binding : registry.itemBindings()) {
            NBTTagCompound value = new NBTTagCompound();
            value.setString("item_id", binding.getItemId());
            value.setTag("definition", encodeDefinition(binding.getDefinition()));
            items.appendTag(value);
        }
        root.setTag("items", items);
        NBTTagList groups = new NBTTagList();
        for (ItemIdentityRegistry.GroupBinding binding : registry.groupBindings()) {
            NBTTagCompound value = new NBTTagCompound();
            value.setString("group_id", binding.getGroupId());
            value.setString(
                "match_mode",
                binding.getMember()
                    .getMatchMode()
                    .getJsonName());
            value.setTag(
                "definition",
                encodeDefinition(
                    binding.getMember()
                        .getDefinition()));
            groups.appendTag(value);
        }
        root.setTag("groups", groups);
    }

    private static NBTTagCompound encodeDefinition(ItemStackDefinition definition) {
        NBTTagCompound value = new NBTTagCompound();
        value.setString("registry_name", definition.getRegistryName());
        value.setInteger("damage", definition.getDamage());
        NBTTagCompound tag = definition.getTag();
        if (tag != null) value.setTag("tag", tag);
        return value;
    }

    private static ItemStackDefinition decodeDefinition(NBTTagCompound value) {
        Set<String> keys = new HashSet<String>(value.func_150296_c());
        Set<String> required = set("registry_name", "damage");
        Set<String> withTag = set("registry_name", "damage", "tag");
        if (!keys.equals(required) && !keys.equals(withTag))
            throw new IllegalArgumentException("Item definition has unknown or missing keys.");
        requireType(value, "registry_name", 8);
        requireType(value, "damage", 3);
        if (value.hasKey("tag")) requireType(value, "tag", 10);
        return new ItemStackDefinition(
            value.getString("registry_name"),
            value.getInteger("damage"),
            value.hasKey("tag") ? value.getCompoundTag("tag") : null);
    }

    private static NBTTagList list(NBTTagCompound value, String key) {
        requireType(value, key, 9);
        NBTTagList result = (NBTTagList) value.getTag(key);
        if (result.tagCount() > 0 && result.func_150303_d() != 10)
            throw new IllegalArgumentException("Item identity " + key + " entries must be compounds.");
        return result;
    }

    private static void requireType(NBTTagCompound value, String key, int type) {
        if (value == null || !value.hasKey(key, type))
            throw new IllegalArgumentException("Item identity " + key + " has wrong type or is missing.");
    }

    private static void requireKeys(NBTTagCompound value, Set<String> expected, String label) {
        if (value == null || !new HashSet<String>(value.func_150296_c()).equals(expected))
            throw new IllegalArgumentException(label + " has unknown or missing keys.");
    }

    private static Set<String> set(String... values) {
        Set<String> result = new HashSet<String>();
        for (String value : values) result.add(value);
        return result;
    }
}
