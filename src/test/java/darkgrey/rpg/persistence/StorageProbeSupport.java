package darkgrey.rpg.persistence;

import java.io.OutputStream;
import java.lang.reflect.Field;
import java.lang.reflect.Proxy;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.security.MessageDigest;

import net.minecraft.nbt.CompressedStreamTools;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.world.WorldSavedData;
import net.minecraft.world.storage.ISaveHandler;
import net.minecraft.world.storage.MapStorage;

/** Real Minecraft storage; only the filesystem save-handler adapter is supplied by the probe. */
public final class StorageProbeSupport {

    private StorageProbeSupport() {}

    public static Path root(String prefix) throws Exception {
        Path parent = Paths.get(".tooling", "0401")
            .toAbsolutePath()
            .normalize();
        Files.createDirectories(parent);
        return Files.createTempDirectory(parent, prefix);
    }

    public static MapStorage storage(Path directory) throws Exception {
        Path parent = Paths.get(".tooling", "0401")
            .toAbsolutePath()
            .normalize();
        Path resolved = directory.toAbsolutePath()
            .normalize();
        if (!resolved.startsWith(parent)) throw new IllegalArgumentException("Probe directory outside test root.");
        Files.createDirectories(resolved);
        ISaveHandler handler = (ISaveHandler) Proxy.newProxyInstance(
            StorageProbeSupport.class.getClassLoader(),
            new Class<?>[] { ISaveHandler.class },
            (proxy, method, args) -> {
                if ("getMapFileFromName".equals(method.getName())) return resolved.resolve(args[0] + ".dat")
                    .toFile();
                if ("getWorldDirectory".equals(method.getName())) return resolved.toFile();
                return null;
            });
        return new MapStorage(handler);
    }

    public static void write(Path file, NBTTagCompound payload) throws Exception {
        NBTTagCompound wrapper = new NBTTagCompound();
        wrapper.setTag("data", payload);
        try (OutputStream stream = Files.newOutputStream(file)) {
            CompressedStreamTools.writeCompressed(wrapper, stream);
        }
    }

    public static void forceDirty(WorldSavedData data) throws Exception {
        Field field = WorldSavedData.class.getDeclaredField("dirty");
        field.setAccessible(true);
        field.setBoolean(data, true);
    }

    public static String hash(Path file) throws Exception {
        byte[] bytes = MessageDigest.getInstance("SHA-256")
            .digest(Files.readAllBytes(file));
        StringBuilder result = new StringBuilder();
        for (byte value : bytes) result.append(String.format("%02x", value & 255));
        return result.toString();
    }

    public static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
