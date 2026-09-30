package darkgrey.rpg.gramophone;

import java.lang.reflect.Field;
import java.util.Map;
import java.util.concurrent.TimeUnit;

import net.minecraft.client.Minecraft;
import net.minecraft.client.entity.EntityClientPlayerMP;

import cpw.mods.fml.common.gameevent.TickEvent;

/** Controlled cache metadata against the real tick eviction path, not a real download/TTL soak. */
public final class GramophoneCachePolicyProbe {

    private static Field field(Class<?> type, String name) throws Exception {
        Field value = type.getDeclaredField(name);
        value.setAccessible(true);
        return value;
    }

    private static void require(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }

    private static Object media(GramophoneMediaInfo info, long bytes, long idle, int pins) throws Exception {
        Class<?> type = Class.forName("darkgrey.rpg.gramophone.GramophoneClient$Media");
        java.lang.reflect.Constructor<?> constructor = type.getDeclaredConstructor();
        constructor.setAccessible(true);
        Object result = constructor.newInstance();
        field(type, "preview").set(result, info);
        field(type, "bytes").setLong(result, bytes);
        field(type, "idle").setLong(result, idle);
        field(type, "pins").setInt(result, pins);
        return result;
    }

    private static boolean discarded(Object media) throws Exception {
        return field(media.getClass(), "discarded").getBoolean(media);
    }

    private static void fileRetirement(GramophoneClient client, TickEvent.ClientTickEvent tick,
        Map<String, Object> cache, GramophoneMediaInfo info) throws Exception {
        java.nio.file.Path directory = java.nio.file.Files.createTempDirectory(info.path.getParent(), "eviction-");
        java.util.List<java.nio.file.Path> paths = new java.util.ArrayList<java.nio.file.Path>();
        byte[] block = new byte[1024 * 1024];
        long now = System.nanoTime();
        cache.clear();
        try {
            for (int i = 0; i < 5; i++) {
                java.nio.file.Path path = directory.resolve("track-" + i + ".dgrmp3");
                paths.add(path);
                // Real disk bytes for eviction only: these blobs are not claimed to be decoded audio.
                try (java.io.OutputStream output = java.nio.file.Files.newOutputStream(path)) {
                    for (int part = 0; part < (i == 4 ? 64 : 32); part++) output.write(block);
                }
                Object entry = media(
                    info,
                    java.nio.file.Files.size(path),
                    now - TimeUnit.MINUTES.toNanos(5 - i),
                    i == 4 ? 1 : 0);
                field(entry.getClass(), "path").set(entry, path);
                cache.put("file" + i, entry);
            }
            client.tick(tick);
            require(cache.size() == 5, "real 128 MiB idle files plus 64 MiB pinned file admitted");
            cache.put("overflow", media(info, 1, now, 0));
            client.tick(tick);
            require(!cache.containsKey("file0"), "real oldest file is retired when budget is exceeded");
            long deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(10);
            while (java.nio.file.Files.exists(paths.get(0)) && System.nanoTime() < deadline) Thread.sleep(50);
            require(!java.nio.file.Files.exists(paths.get(0)), "background cleanup deletes evicted real file");
            for (int i = 1; i < paths.size(); i++)
                require(java.nio.file.Files.exists(paths.get(i)), "unretired and pinned files remain");
            Object expired = cache.get("file1");
            field(expired.getClass(), "idle").setLong(expired, now - TimeUnit.MINUTES.toNanos(31));
            Object pinned = cache.get("file4");
            field(pinned.getClass(), "idle").setLong(pinned, now - TimeUnit.MINUTES.toNanos(60));
            client.tick(tick);
            deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(10);
            while (java.nio.file.Files.exists(paths.get(1)) && System.nanoTime() < deadline) Thread.sleep(50);
            require(!java.nio.file.Files.exists(paths.get(1)), "expired idle real file is removed");
            require(java.nio.file.Files.exists(paths.get(4)) && !discarded(pinned), "old pinned real file remains");
            System.out.println(
                "GRAMOPHONE_REAL_FILE_BUDGET_AND_RETIREMENT=PASS (192 MiB disk blobs; controlled idle clock; no download/decoder claim)");
        } finally {
            cache.clear();
            for (java.nio.file.Path path : paths) java.nio.file.Files.deleteIfExists(path);
            java.nio.file.Files.deleteIfExists(directory);
        }
    }

    @SuppressWarnings("unchecked")
    static void run(GramophoneMediaInfo info) throws Exception {
        // Isolated CLI only: no window/world, constructor side effects, or production singleton mutation.
        Field singleton = field(Minecraft.class, "theMinecraft");
        require(singleton.get(null) == null, "cache policy probe requires an unstarted client JVM");
        Field unsafeField = field(sun.misc.Unsafe.class, "theUnsafe");
        sun.misc.Unsafe unsafe = (sun.misc.Unsafe) unsafeField.get(null);
        Minecraft mc = (Minecraft) unsafe.allocateInstance(Minecraft.class);
        mc.thePlayer = (EntityClientPlayerMP) unsafe.allocateInstance(EntityClientPlayerMP.class);
        Map<String, Object> cache = (Map<String, Object>) field(GramophoneClient.class, "MEDIA").get(null);
        require(cache.isEmpty(), "probe must not replace pre-existing cache owners");
        singleton.set(null, mc);
        GramophoneClient client = new GramophoneClient();
        TickEvent.ClientTickEvent tick = new TickEvent.ClientTickEvent(TickEvent.Phase.END);
        long now = System.nanoTime();
        long mib = 1024L * 1024;
        try {
            Object oldest = media(info, 32 * mib, now - TimeUnit.MINUTES.toNanos(4), 0);
            cache.put("oldest", oldest);
            for (int i = 1; i <= 3; i++)
                cache.put("idle" + i, media(info, 32 * mib, now - TimeUnit.MINUTES.toNanos(4 - i), 0));
            Object pinned = media(info, 64 * mib, now - TimeUnit.MINUTES.toNanos(60), 1);
            cache.put("pinned", pinned);
            client.tick(tick);
            require(cache.size() == 5 && !discarded(oldest), "exactly 128 MiB idle is admitted; pins excluded");
            cache.put("extra", media(info, 1, now, 0));
            client.tick(tick);
            require(!cache.containsKey("oldest") && discarded(oldest), "one byte over budget evicts oldest idle");
            require(
                cache.size() == 5 && cache.get("pinned") == pinned && !discarded(pinned),
                "budget eviction preserves pinned data");
            cache.clear();

            Object expired = media(info, 1, now - TimeUnit.MINUTES.toNanos(31), 0);
            Object recent = media(info, 1, now - TimeUnit.MINUTES.toNanos(29), 0);
            cache.put("expired", expired);
            cache.put("recent", recent);
            cache.put("pinned", pinned);
            client.tick(tick);
            require(!cache.containsKey("expired") && discarded(expired), "idle metadata beyond 30 minutes expires");
            require(cache.get("recent") == recent && !discarded(recent), "29-minute idle metadata remains");
            require(cache.get("pinned") == pinned && !discarded(pinned), "TTL preserves a live consumer");
            cache.clear();

            cache.put("pinned", pinned);
            Object oldestEntry = media(info, 0, now - 1000, 0);
            cache.put("oldest", oldestEntry);
            for (int i = 0; i < 127; i++) cache.put("entry" + i, media(info, 0, now + i, 0));
            client.tick(tick);
            require(
                cache.size() == 128 && !cache.containsKey("oldest") && discarded(oldestEntry),
                "129 entries evict oldest idle even with zero byte metadata");
            require(cache.get("pinned") == pinned && !discarded(pinned), "entry limit retains pinned resource");
            System.out
                .println("GRAMOPHONE_CACHE_POLICY_128MIB_30MIN_128ENTRIES=PASS (controlled metadata; not native soak)");
            fileRetirement(client, tick, cache, info);
            GramophoneDownloadCompletionProbe.run(info);
        } finally {
            cache.clear();
            singleton.set(null, null);
        }
    }
}
