package darkgrey.rpg.project.packages;

import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.TreeMap;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;
import net.minecraft.world.WorldSavedData;
import net.minecraft.world.storage.MapStorage;

/** Minimal world-level registry of the last accepted Story Package generations. */
public final class StoryPackageGenerationSavedData extends WorldSavedData {

    public static final String DATA_NAME = "darkgrey_rpg_story_package_generations";
    private static final int SCHEMA_VERSION = 1;
    private static final int COMPOUND = 10;
    private Map<String, PackageGenerationKey> generations = Collections.emptyMap();
    private boolean initialized;

    public StoryPackageGenerationSavedData(String name) {
        super(name);
    }

    public StoryPackageGenerationSavedData() {
        this(DATA_NAME);
    }

    public static StoryPackageGenerationSavedData get(MapStorage storage) {
        if (storage == null) throw new IllegalArgumentException("MapStorage is required.");
        WorldSavedData loaded = storage.loadData(StoryPackageGenerationSavedData.class, DATA_NAME);
        if (loaded instanceof StoryPackageGenerationSavedData) return (StoryPackageGenerationSavedData) loaded;
        StoryPackageGenerationSavedData created = new StoryPackageGenerationSavedData();
        storage.setData(DATA_NAME, created);
        return created;
    }

    public synchronized Map<String, PackageGenerationKey> snapshot() {
        return generations;
    }

    public synchronized boolean isInitialized() {
        return initialized;
    }

    public synchronized void replace(Map<String, PackageGenerationKey> values) {
        if (values == null) throw new IllegalArgumentException("Generation registry values are required.");
        Map<String, PackageGenerationKey> replacement = freeze(values);
        if (!initialized || !generations.equals(replacement)) {
            generations = replacement;
            initialized = true;
            markDirty();
        }
    }

    @Override
    public synchronized void readFromNBT(NBTTagCompound root) {
        if (root == null || !root.hasKey("schema_version", 3)
            || root.getInteger("schema_version") != SCHEMA_VERSION
            || !root.hasKey("generations", 9))
            throw new IllegalArgumentException("Malformed Story Package generation registry.");
        NBTTagList list = root.getTagList("generations", COMPOUND);
        if (list.tagCount() > 0 && list.func_150303_d() != COMPOUND)
            throw new IllegalArgumentException("Malformed Story Package generation entries.");
        Map<String, PackageGenerationKey> replacement = new LinkedHashMap<String, PackageGenerationKey>();
        for (int i = 0; i < list.tagCount(); i++) {
            NBTTagCompound entry = list.getCompoundTagAt(i);
            if (!entry.hasKey("package_id", 8) || !entry.hasKey("story_id", 8)
                || !entry.hasKey("content_fingerprint", 8))
                throw new IllegalArgumentException("Malformed Story Package generation entry.");
            PackageGenerationKey value = new PackageGenerationKey(
                entry.getString("package_id"),
                entry.getString("story_id"),
                entry.getString("content_fingerprint"));
            if (replacement.put(value.getPackageId(), value) != null)
                throw new IllegalArgumentException("Duplicate Story Package generation package_id.");
        }
        generations = freeze(replacement);
        initialized = true;
    }

    @Override
    public synchronized void writeToNBT(NBTTagCompound root) {
        if (root == null) throw new IllegalArgumentException("Output NBT is required.");
        root.setInteger("schema_version", SCHEMA_VERSION);
        NBTTagList list = new NBTTagList();
        for (PackageGenerationKey value : new TreeMap<String, PackageGenerationKey>(generations).values()) {
            NBTTagCompound entry = new NBTTagCompound();
            entry.setString("package_id", value.getPackageId());
            entry.setString("story_id", value.getStoryId());
            entry.setString("content_fingerprint", value.getContentFingerprint());
            list.appendTag(entry);
        }
        root.setTag("generations", list);
    }

    private static Map<String, PackageGenerationKey> freeze(Map<String, PackageGenerationKey> values) {
        Map<String, PackageGenerationKey> ordered = new TreeMap<String, PackageGenerationKey>();
        for (Map.Entry<String, PackageGenerationKey> entry : values.entrySet()) {
            PackageGenerationKey value = entry.getValue();
            if (value == null || !entry.getKey()
                .equals(value.getPackageId()))
                throw new IllegalArgumentException("Generation registry key does not match package_id.");
            ordered.put(entry.getKey(), value);
        }
        return Collections.unmodifiableMap(new LinkedHashMap<String, PackageGenerationKey>(ordered));
    }
}
