package darkgrey.rpg.nominator;

import java.util.ArrayList;
import java.util.Collections;
import java.util.HashSet;
import java.util.List;
import java.util.UUID;
import java.util.function.LongSupplier;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;
import net.minecraft.server.MinecraftServer;
import net.minecraft.world.WorldSavedData;
import net.minecraft.world.WorldServer;
import net.minecraft.world.storage.MapStorage;

import darkgrey.rpg.identity.BindingCapacity;
import darkgrey.rpg.identity.CompactBindingMap;
import darkgrey.rpg.identity.ResourceAddress;
import darkgrey.rpg.identity.ResourceAddressNbt;

/** Overworld persistence for selection metadata; real NPC identity is separate. */
public final class NominatorSavedData extends WorldSavedData {

    public static final String DATA_NAME = "darkgrey_rpg_nominator";
    public static final int SCHEMA_VERSION = 4;
    private CompactBindingMap<UUID, NominatorEntityBinding> entities = new CompactBindingMap<UUID, NominatorEntityBinding>();
    private final BindingCapacity capacity;
    private boolean batching;
    private long revision;
    private boolean awaitingRead;
    private RuntimeException readFailure;

    public NominatorSavedData() {
        this(DATA_NAME, false, System::nanoTime);
    }

    public NominatorSavedData(String name) {
        this(name, true, System::nanoTime);
    }

    NominatorSavedData(LongSupplier clock) {
        this(DATA_NAME, false, clock);
    }

    private NominatorSavedData(String name, boolean awaitingRead, LongSupplier clock) {
        super(name);
        capacity = new BindingCapacity(clock);
        this.awaitingRead = awaitingRead;
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
        if (loaded instanceof NominatorSavedData) {
            NominatorSavedData data = (NominatorSavedData) loaded;
            data.requireUsable();
            NominatorCapacityMaintenance.track(storage, data);
            return data;
        }
        if (loaded != null) throw new NominatorDataUnavailableException(
            DATA_NAME,
            new IllegalStateException("Unexpected SavedData type."));
        NominatorSavedData created = new NominatorSavedData();
        storage.setData(DATA_NAME, created);
        NominatorCapacityMaintenance.track(storage, created);
        return created;
    }

    public synchronized void put(NominatorEntityBinding binding) {
        requireUsable();
        if (binding == null) throw new IllegalArgumentException("Binding is required.");
        NominatorEntityBinding previous = entities.get(binding.getEntityUuid());
        if (previous != null && java.util.Objects.equals(previous.getIndividualId(), binding.getIndividualId())
            && previous.getGroupIds()
                .equals(binding.getGroupIds())
            && java.util.Objects.equals(previous.getStoryId(), binding.getStoryId())) return;
        entities.put(binding.getEntityUuid(), binding);
        revision++;
        markDirty();
        countChanged();
    }

    /** Explicitly removes an entity selection; an absent selection is idempotent. */
    public synchronized boolean remove(UUID uuid) {
        requireUsable();
        if (uuid == null) throw new IllegalArgumentException("Entity UUID is required.");
        if (entities.remove(uuid) == null) return false;
        revision++;
        markDirty();
        countChanged();
        return true;
    }

    public synchronized long getRevision() {
        requireUsable();
        return revision;
    }

    public synchronized NominatorEntityBinding get(UUID uuid) {
        requireUsable();
        return entities.get(uuid);
    }

    public synchronized List<NominatorEntityBinding> bindings() {
        requireUsable();
        return Collections.unmodifiableList(new ArrayList<NominatorEntityBinding>(entities.values()));
    }

    synchronized void beginBatch() {
        requireUsable();
        if (batching) throw new IllegalStateException("Selection batch is already active.");
        batching = true;
    }

    synchronized void endBatch(boolean committed) {
        batching = false;
        if (committed) countChanged();
    }

    private void countChanged() {
        if (!batching) capacity.changed(entities.size(), entities.canCompact());
    }

    public synchronized boolean readableForMaintenance() {
        return !awaitingRead && readFailure == null;
    }

    public synchronized boolean maintainMemory() {
        requireUsable();
        if (batching || !capacity.maintenanceDue(entities.size(), entities.canCompact())) return false;
        CompactBindingMap<UUID, NominatorEntityBinding> replacement = entities.compacted();
        entities = replacement;
        capacity.maintenanceComplete(entities.size());
        return true;
    }

    synchronized long currentCapacity() {
        requireUsable();
        return capacity.capacity();
    }

    /** MapStorage can cache a failed reader; never let that instance become writable empty data. */
    public synchronized void requireUsable() {
        if (awaitingRead || readFailure != null) throw new NominatorDataUnavailableException(mapName, readFailure);
    }

    @Override
    public synchronized void setDirty(boolean dirty) {
        if (dirty) requireUsable();
        super.setDirty(dirty);
    }

    @Override
    public synchronized void readFromNBT(NBTTagCompound root) {
        awaitingRead = true;
        try {
            readCurrentNBT(root);
            readFailure = null;
            awaitingRead = false;
        } catch (RuntimeException failure) {
            readFailure = failure;
            super.setDirty(false);
            throw failure;
        }
    }

    private void readCurrentNBT(NBTTagCompound root) {
        if (root == null || !root.hasKey("schema_version", 3)
            || root.getInteger("schema_version") != SCHEMA_VERSION
            || !root.hasKey("entities", 9)) throw new IllegalArgumentException("Invalid nominator persistence root.");
        ResourceAddressNbt.requireFormat(root);
        HashSet<String> rootKeys = new HashSet<String>(root.func_150296_c());
        HashSet<String> expectedRoot = new HashSet<String>();
        expectedRoot.add("schema_version");
        expectedRoot.add("identity_format");
        expectedRoot.add("revision");
        expectedRoot.add("entities");
        if (!rootKeys.equals(expectedRoot)) throw new IllegalArgumentException("Invalid nominator persistence root.");
        NBTTagList list = ResourceAddressNbt.compounds(root, "entities");
        CompactBindingMap<UUID, NominatorEntityBinding> decoded = new CompactBindingMap<UUID, NominatorEntityBinding>();
        for (int i = 0; i < list.tagCount(); i++) {
            NBTTagCompound value = list.getCompoundTagAt(i);
            if (!(value.hasKey("individual_id") ? keys(value, "entity_uuid", "individual_id", "groups", "story_id")
                : keys(value, "entity_uuid", "groups", "story_id")))
                throw new IllegalArgumentException("Invalid nominator entity binding.");
            UUID uuid;
            try {
                uuid = UUID.fromString(value.getString("entity_uuid"));
            } catch (IllegalArgumentException exception) {
                throw new IllegalArgumentException("Invalid entity UUID.", exception);
            }
            List<String> groups = new ArrayList<String>();
            NBTTagList groupList = ResourceAddressNbt.compounds(value, "groups");
            if (groupList.tagCount() > 32) throw new IllegalArgumentException("Too many entity groups.");
            for (int group = 0; group < groupList.tagCount(); group++)
                groups.add(ResourceAddressNbt.read(groupList.getCompoundTagAt(group), ResourceAddress.Kind.ACTOR));
            String individual = value.hasKey("individual_id")
                ? ResourceAddressNbt.read(value, "individual_id", ResourceAddress.Kind.ACTOR)
                : null;
            String story = value.getString("story_id");
            if (decoded.put(uuid, new NominatorEntityBinding(uuid, individual, groups, story)) != null)
                throw new IllegalArgumentException("Duplicate entity binding");
        }
        if (!root.hasKey("revision", 4) || root.getLong("revision") < 0)
            throw new IllegalArgumentException("Invalid revision");
        entities = decoded;
        revision = root.getLong("revision");
        capacity.loaded(entities.size());
        batching = false;
    }

    @Override
    public synchronized void writeToNBT(NBTTagCompound root) {
        requireUsable();
        if (root == null) throw new IllegalArgumentException("Output NBT is required.");
        for (String key : new HashSet<String>(root.func_150296_c())) root.removeTag(key);
        root.setInteger("schema_version", SCHEMA_VERSION);
        root.setString("identity_format", ResourceAddressNbt.IDENTITY_FORMAT);
        root.setLong("revision", revision);
        NBTTagList list = new NBTTagList();
        for (NominatorEntityBinding binding : entities.values()) {
            NBTTagCompound value = new NBTTagCompound();
            value.setString(
                "entity_uuid",
                binding.getEntityUuid()
                    .toString());
            if (binding.getIndividualId() != null) value.setTag(
                "individual_id",
                ResourceAddressNbt.write(binding.getIndividualId(), ResourceAddress.Kind.ACTOR));
            NBTTagList groups = new NBTTagList();
            for (String group : binding.getGroupIds())
                groups.appendTag(ResourceAddressNbt.write(group, ResourceAddress.Kind.ACTOR));
            value.setTag("groups", groups);
            value.setString("story_id", binding.getStoryId() == null ? "" : binding.getStoryId());
            list.appendTag(value);
        }
        root.setTag("entities", list);
    }

    private static boolean keys(NBTTagCompound value, String... expected) {
        if (value == null) return false;
        HashSet<String> actual = new HashSet<String>(value.func_150296_c());
        HashSet<String> required = new HashSet<String>();
        for (String key : expected) required.add(key);
        return actual.equals(required) && value.hasKey("entity_uuid", 8)
            && (!value.hasKey("individual_id") || value.hasKey("individual_id", 10))
            && value.hasKey("groups", 9)
            && value.hasKey("story_id", 8);
    }

}
