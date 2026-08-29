package darkgrey.rpg.nominator;

import java.util.ArrayList;
import java.util.Collections;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;
import net.minecraft.server.MinecraftServer;
import net.minecraft.world.WorldSavedData;
import net.minecraft.world.WorldServer;
import net.minecraft.world.storage.MapStorage;

/** Overworld persistence for selection metadata; real NPC identity is separate. */
public final class NominatorSavedData extends WorldSavedData {

    public static final String DATA_NAME = "darkgrey_rpg_nominator";
    private static final int SCHEMA_VERSION = 2;
    private final Map<UUID, NominatorEntityBinding> entities = new LinkedHashMap<UUID, NominatorEntityBinding>();
    private final Map<String, List<String>> typeGroups = new LinkedHashMap<String, List<String>>();
    private long revision;

    public NominatorSavedData() {
        this(DATA_NAME);
    }

    public NominatorSavedData(String name) {
        super(name);
    }

    public static NominatorSavedData get() {
        MinecraftServer server = MinecraftServer.getServer();
        if (server == null) throw new IllegalStateException("Minecraft server is unavailable.");
        WorldServer overworld = server.worldServerForDimension(0);
        if (overworld == null) throw new IllegalStateException("Overworld is unavailable.");
        return get(overworld.mapStorage);
    }

    public static NominatorSavedData get(MapStorage storage) {
        if (storage == null) throw new IllegalArgumentException("MapStorage is required.");
        WorldSavedData loaded = storage.loadData(NominatorSavedData.class, DATA_NAME);
        if (loaded instanceof NominatorSavedData) return (NominatorSavedData) loaded;
        NominatorSavedData created = new NominatorSavedData();
        storage.setData(DATA_NAME, created);
        return created;
    }

    public synchronized void put(NominatorEntityBinding binding) {
        if (binding == null) throw new IllegalArgumentException("Binding is required.");
        entities.put(binding.getEntityUuid(), binding);
        revision++;
        markDirty();
    }

    /** Explicitly removes an entity selection; an absent selection is idempotent. */
    public synchronized boolean remove(UUID uuid) {
        if (uuid == null) throw new IllegalArgumentException("Entity UUID is required.");
        if (entities.remove(uuid) == null) return false;
        revision++;
        markDirty();
        return true;
    }

    public synchronized long getRevision() {
        return revision;
    }

    public synchronized NominatorEntityBinding get(UUID uuid) {
        return entities.get(uuid);
    }

    public synchronized List<NominatorEntityBinding> bindings() {
        return Collections.unmodifiableList(new ArrayList<NominatorEntityBinding>(entities.values()));
    }

    /** Collective groups applied to every safe entity with this exact registry type. */
    public synchronized List<String> getTypeGroups(String entityType) {
        if (entityType == null) return Collections.emptyList();
        List<String> groups = typeGroups.get(entityType.trim());
        return groups == null ? Collections.<String>emptyList()
            : Collections.unmodifiableList(new ArrayList<String>(groups));
    }

    public synchronized boolean addTypeGroup(String entityType, String groupId) {
        String type = requireText(entityType, "Entity type");
        if (!NominatorService.safeType(type)) throw new IllegalArgumentException("Unsafe wrapper entity type.");
        String group = requireText(groupId, "Group ID");
        List<String> values = typeGroups.get(type);
        if (values == null) {
            values = new ArrayList<String>();
            typeGroups.put(type, values);
        }
        if (values.contains(group)) return false;
        values.add(group);
        revision++;
        markDirty();
        return true;
    }

    public synchronized boolean removeTypeGroup(String entityType, String groupId) {
        if (entityType == null || groupId == null) return false;
        List<String> values = typeGroups.get(entityType.trim());
        if (values == null || !values.remove(groupId.trim())) return false;
        if (values.isEmpty()) typeGroups.remove(entityType.trim());
        revision++;
        markDirty();
        return true;
    }

    public synchronized Map<String, List<String>> typeGroups() {
        Map<String, List<String>> result = new LinkedHashMap<String, List<String>>();
        for (Map.Entry<String, List<String>> value : typeGroups.entrySet())
            result.put(value.getKey(), Collections.unmodifiableList(new ArrayList<String>(value.getValue())));
        return Collections.unmodifiableMap(result);
    }

    @Override
    public synchronized void readFromNBT(NBTTagCompound root) {
        if (root == null || !root.hasKey("schema_version", 3)
            || (root.getInteger("schema_version") != 1 && root.getInteger("schema_version") != SCHEMA_VERSION)
            || !root.hasKey("entities", 9)) throw new IllegalArgumentException("Invalid nominator persistence root.");
        HashSet<String> rootKeys = new HashSet<String>(root.func_150296_c());
        HashSet<String> expectedRoot = new HashSet<String>();
        expectedRoot.add("schema_version");
        expectedRoot.add("revision");
        expectedRoot.add("entities");
        if (root.getInteger("schema_version") >= 2) expectedRoot.add("type_groups");
        if (!rootKeys.equals(expectedRoot)) throw new IllegalArgumentException("Invalid nominator persistence root.");
        NBTTagList list = root.getTagList("entities", 10);
        if (list.tagCount() > NominatorCatalog.MAX_ENTRIES)
            throw new IllegalArgumentException("Too many entity bindings.");
        Map<UUID, NominatorEntityBinding> decoded = new LinkedHashMap<UUID, NominatorEntityBinding>();
        for (int i = 0; i < list.tagCount(); i++) {
            NBTTagCompound value = list.getCompoundTagAt(i);
            if (!keys(value, "entity_uuid", "individual_id", "groups", "story_id"))
                throw new IllegalArgumentException("Invalid nominator entity binding.");
            UUID uuid;
            try {
                uuid = UUID.fromString(value.getString("entity_uuid"));
            } catch (IllegalArgumentException exception) {
                throw new IllegalArgumentException("Invalid entity UUID.", exception);
            }
            List<String> groups = new ArrayList<String>();
            NBTTagList groupList = value.getTagList("groups", 8);
            if (groupList.tagCount() > 32) throw new IllegalArgumentException("Too many entity groups.");
            for (int group = 0; group < groupList.tagCount(); group++) groups.add(groupList.getStringTagAt(group));
            String individual = value.getString("individual_id");
            String story = value.getString("story_id");
            decoded.put(uuid, new NominatorEntityBinding(uuid, individual, groups, story));
        }
        entities.clear();
        entities.putAll(decoded);
        typeGroups.clear();
        if (root.getInteger("schema_version") >= 2) {
            if (!root.hasKey("type_groups", 9)) throw new IllegalArgumentException("Invalid nominator type groups.");
            NBTTagList types = root.getTagList("type_groups", 10);
            if (types.tagCount() > NominatorCatalog.MAX_ENTRIES)
                throw new IllegalArgumentException("Too many entity types.");
            for (int i = 0; i < types.tagCount(); i++) {
                NBTTagCompound value = types.getCompoundTagAt(i);
                if (!exactKeys(value, "entity_type", "groups") || !value.hasKey("entity_type", 8)
                    || !value.hasKey("groups", 9)) throw new IllegalArgumentException("Invalid nominator type group.");
                String type = requireText(value.getString("entity_type"), "Entity type");
                if (typeGroups.containsKey(type))
                    throw new IllegalArgumentException("Duplicate nominator entity type.");
                NBTTagList values = value.getTagList("groups", 8);
                List<String> groups = new ArrayList<String>();
                for (int j = 0; j < values.tagCount(); j++) {
                    String group = requireText(values.getStringTagAt(j), "Group ID");
                    if (!groups.contains(group)) groups.add(group);
                }
                if (!groups.isEmpty()) typeGroups.put(type, groups);
            }
        }
        revision = root.hasKey("revision", 4) ? root.getLong("revision") : 0L;
    }

    @Override
    public synchronized void writeToNBT(NBTTagCompound root) {
        if (root == null) throw new IllegalArgumentException("Output NBT is required.");
        for (String key : new HashSet<String>(root.func_150296_c())) root.removeTag(key);
        root.setInteger("schema_version", SCHEMA_VERSION);
        root.setLong("revision", revision);
        NBTTagList list = new NBTTagList();
        for (NominatorEntityBinding binding : entities.values()) {
            NBTTagCompound value = new NBTTagCompound();
            value.setString(
                "entity_uuid",
                binding.getEntityUuid()
                    .toString());
            value.setString("individual_id", binding.getIndividualId() == null ? "" : binding.getIndividualId());
            NBTTagList groups = new NBTTagList();
            for (String group : binding.getGroupIds()) groups.appendTag(new net.minecraft.nbt.NBTTagString(group));
            value.setTag("groups", groups);
            value.setString("story_id", binding.getStoryId() == null ? "" : binding.getStoryId());
            list.appendTag(value);
        }
        root.setTag("entities", list);
        NBTTagList types = new NBTTagList();
        for (Map.Entry<String, List<String>> entry : typeGroups.entrySet()) {
            NBTTagCompound value = new NBTTagCompound();
            value.setString("entity_type", entry.getKey());
            NBTTagList groups = new NBTTagList();
            for (String group : entry.getValue()) groups.appendTag(new net.minecraft.nbt.NBTTagString(group));
            value.setTag("groups", groups);
            types.appendTag(value);
        }
        root.setTag("type_groups", types);
    }

    private static boolean keys(NBTTagCompound value, String... expected) {
        if (value == null) return false;
        HashSet<String> actual = new HashSet<String>(value.func_150296_c());
        HashSet<String> required = new HashSet<String>();
        for (String key : expected) required.add(key);
        return actual.equals(required) && value.hasKey("entity_uuid", 8)
            && value.hasKey("individual_id", 8)
            && value.hasKey("groups", 9)
            && value.hasKey("story_id", 8);
    }

    private static String requireText(String value, String label) {
        if (value == null || value.trim()
            .isEmpty() || value.length() > 256) throw new IllegalArgumentException(label + " is invalid.");
        return value.trim();
    }

    private static boolean exactKeys(NBTTagCompound value, String... expected) {
        if (value == null) return false;
        HashSet<String> actual = new HashSet<String>(value.func_150296_c());
        HashSet<String> required = new HashSet<String>();
        for (String key : expected) required.add(key);
        return actual.equals(required);
    }
}
