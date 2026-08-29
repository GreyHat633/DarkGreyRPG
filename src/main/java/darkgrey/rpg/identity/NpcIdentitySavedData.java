package darkgrey.rpg.identity;

import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Set;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;
import net.minecraft.server.MinecraftServer;
import net.minecraft.world.WorldSavedData;
import net.minecraft.world.WorldServer;
import net.minecraft.world.storage.MapStorage;

/** Overworld-owned persistence boundary for unique NPC identities. */
public final class NpcIdentitySavedData extends WorldSavedData {

    public static final String DATA_NAME = "darkgrey_rpg_npc_identities";
    private static final int SCHEMA_VERSION = 1;
    private NpcIdentityRegistry registry = new NpcIdentityRegistry();

    public NpcIdentitySavedData() {
        this(DATA_NAME);
    }

    public NpcIdentitySavedData(String name) {
        super(name);
    }

    public static NpcIdentitySavedData get() {
        MinecraftServer server = MinecraftServer.getServer();
        if (server == null) throw new IllegalStateException("Minecraft server is unavailable.");
        WorldServer overworld = server.worldServerForDimension(0);
        if (overworld == null) throw new IllegalStateException("Overworld is unavailable.");
        return get(overworld.mapStorage);
    }

    public static NpcIdentitySavedData get(MapStorage storage) {
        if (storage == null) throw new IllegalArgumentException("MapStorage is required.");
        WorldSavedData loaded = storage.loadData(NpcIdentitySavedData.class, DATA_NAME);
        if (loaded instanceof NpcIdentitySavedData) return (NpcIdentitySavedData) loaded;
        NpcIdentitySavedData created = new NpcIdentitySavedData();
        storage.setData(DATA_NAME, created);
        return created;
    }

    public synchronized boolean bind(String npcId, NpcHostIdentity host) {
        boolean changed = registry.bind(npcId, host);
        if (changed) markDirty();
        return changed;
    }

    public synchronized boolean transfer(String npcId, NpcHostIdentity replacement) {
        boolean changed = registry.transfer(npcId, replacement);
        if (changed) markDirty();
        return changed;
    }

    public synchronized boolean observe(NpcHostIdentity observed) {
        boolean changed = registry.observe(observed);
        if (changed) markDirty();
        return changed;
    }

    public synchronized boolean unbindNpcId(String npcId) {
        boolean changed = registry.unbindNpcId(npcId);
        if (changed) markDirty();
        return changed;
    }

    public synchronized boolean unbindHost(UUID hostUuid) {
        boolean changed = registry.unbindHost(hostUuid);
        if (changed) markDirty();
        return changed;
    }

    public synchronized NpcHostIdentity getHost(String npcId) {
        return registry.getHost(npcId);
    }

    public synchronized String getNpcId(UUID hostUuid) {
        return registry.getNpcId(hostUuid);
    }

    public synchronized List<NpcIdentityRegistry.Binding> bindings() {
        return registry.bindings();
    }

    @Override
    public synchronized void readFromNBT(NBTTagCompound root) {
        if (root == null) throw new IllegalArgumentException("NPC identity NBT is required.");
        requireKeys(root, set("schema_version", "bindings"), "NPC identity root");
        if (!root.hasKey("schema_version", 3) || root.getInteger("schema_version") != SCHEMA_VERSION)
            throw new IllegalArgumentException("Unsupported NPC identity schema_version.");
        if (!root.hasKey("bindings", 9)) throw new IllegalArgumentException("NPC identity bindings must be a list.");
        NBTTagList values = root.getTagList("bindings", 10);
        List<NpcIdentityRegistry.Binding> decoded = new ArrayList<NpcIdentityRegistry.Binding>();
        for (int index = 0; index < values.tagCount(); index++) {
            NBTTagCompound value = values.getCompoundTagAt(index);
            requireKeys(
                value,
                set("npc_id", "entity_uuid", "entity_type", "last_dimension", "compatibility_key"),
                "NPC identity binding");
            requireType(value, "npc_id", 8);
            requireType(value, "entity_uuid", 8);
            requireType(value, "entity_type", 8);
            requireType(value, "last_dimension", 3);
            if (value.hasKey("compatibility_key") && !value.hasKey("compatibility_key", 8))
                throw new IllegalArgumentException("NPC compatibility_key must be a string.");
            UUID uuid;
            try {
                uuid = UUID.fromString(value.getString("entity_uuid"));
            } catch (IllegalArgumentException exception) {
                throw new IllegalArgumentException("NPC entity_uuid is invalid.", exception);
            }
            String compatibility = value.hasKey("compatibility_key") ? value.getString("compatibility_key") : null;
            decoded.add(
                new NpcIdentityRegistry.Binding(
                    value.getString("npc_id"),
                    new NpcHostIdentity(
                        uuid,
                        value.getString("entity_type"),
                        value.getInteger("last_dimension"),
                        compatibility)));
        }
        NpcIdentityRegistry candidate = new NpcIdentityRegistry();
        candidate.replaceAll(decoded);
        registry = candidate;
    }

    @Override
    public synchronized void writeToNBT(NBTTagCompound root) {
        if (root == null) throw new IllegalArgumentException("Output NBT is required.");
        for (String key : new HashSet<String>(root.func_150296_c())) root.removeTag(key);
        root.setInteger("schema_version", SCHEMA_VERSION);
        NBTTagList values = new NBTTagList();
        for (NpcIdentityRegistry.Binding binding : registry.bindings()) {
            NpcHostIdentity host = binding.getHost();
            NBTTagCompound value = new NBTTagCompound();
            value.setString("npc_id", binding.getNpcId());
            value.setString(
                "entity_uuid",
                host.getEntityUuid()
                    .toString());
            value.setString("entity_type", host.getEntityType());
            value.setInteger("last_dimension", host.getLastKnownDimension());
            if (host.getCompatibilityKey() != null) value.setString("compatibility_key", host.getCompatibilityKey());
            values.appendTag(value);
        }
        root.setTag("bindings", values);
    }

    private static void requireType(NBTTagCompound value, String key, int type) {
        if (!value.hasKey(key, type)) throw new IllegalArgumentException("NPC identity " + key + " has wrong type.");
    }

    private static void requireKeys(NBTTagCompound value, Set<String> expected, String label) {
        for (String key : value.func_150296_c()) if (!expected.contains(key))
            throw new IllegalArgumentException(label + " contains unsupported key '" + key + "'.");
        for (String key : expected) if (!key.equals("compatibility_key") && !value.hasKey(key))
            throw new IllegalArgumentException(label + " is missing '" + key + "'.");
    }

    private static Set<String> set(String... values) {
        Set<String> result = new HashSet<String>();
        for (String value : values) result.add(value);
        return result;
    }
}
