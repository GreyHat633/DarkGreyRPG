package darkgrey.rpg.diagnostics;

import java.io.File;
import java.io.FileInputStream;
import java.util.HashMap;
import java.util.Map;
import java.util.WeakHashMap;

import net.minecraft.nbt.CompressedStreamTools;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.world.WorldSavedData;
import net.minecraft.world.WorldServer;
import net.minecraft.world.storage.MapStorage;

/** Lookup references to the actual owner; diagnostic reads never load/bind/create canonical stores. */
public final class ReadOnlyStateSource {

    private static final Map<MapStorage, Map<String, WorldSavedData>> LOADED = new WeakHashMap<MapStorage, Map<String, WorldSavedData>>();

    private ReadOnlyStateSource() {}

    public static synchronized void observe(MapStorage storage, String name, WorldSavedData data) {
        Map<String, WorldSavedData> entries = LOADED.get(storage);
        if (entries == null) {
            entries = new HashMap<String, WorldSavedData>();
            LOADED.put(storage, entries);
        }
        entries.put(name, data);
    }

    public static synchronized NBTTagCompound read(WorldServer world, String name) throws java.io.IOException {
        Map<String, WorldSavedData> entries = LOADED.get(world.mapStorage);
        WorldSavedData data = entries == null ? null : entries.get(name);
        NBTTagCompound result = new NBTTagCompound();
        if (data != null) {
            data.writeToNBT(result);
            result.setString("diagnostic_source", "服务器当前状态");
            return result;
        }
        File file = world.getSaveHandler()
            .getMapFileFromName(name);
        NBTTagCompound resultFromDisk = readFile(file);
        if (resultFromDisk != null
            && darkgrey.rpg.session.persistence.CanonicalSessionSavedData.DATA_NAME.equals(name)) {
            NBTTagCompound payload = (NBTTagCompound) resultFromDisk.copy();
            payload.removeTag("diagnostic_source");
            darkgrey.rpg.session.persistence.CanonicalSessionWorldStateNbtCodec.decode(payload);
        }
        return resultFromDisk;
    }

    static NBTTagCompound readFile(File file) throws java.io.IOException {
        if (file == null || !file.isFile()) return null;
        if (file.length() > 16777216) throw new java.io.IOException("Saved data exceeds diagnostic read limit.");
        try (FileInputStream stream = new FileInputStream(file)) {
            java.io.ByteArrayOutputStream bytes = new java.io.ByteArrayOutputStream();
            byte[] buffer = new byte[8192];
            int count;
            while ((count = stream.read(buffer)) != -1) {
                if (bytes.size() + count > 16777216)
                    throw new java.io.IOException("Saved data exceeds diagnostic read limit.");
                bytes.write(buffer, 0, count);
            }
            NBTTagCompound root = CompressedStreamTools
                .func_152457_a(bytes.toByteArray(), new net.minecraft.nbt.NBTSizeTracker(67108864));
            if (!root.hasKey("data", 10)) throw new java.io.IOException("Saved data has no compound payload.");
            NBTTagCompound result = root.getCompoundTag("data");
            result.setString("diagnostic_source", "从最近可读存档加载");
            return result;
        }
    }
}
