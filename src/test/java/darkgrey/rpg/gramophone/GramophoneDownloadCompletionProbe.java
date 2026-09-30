package darkgrey.rpg.gramophone;

import java.lang.reflect.Field;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeUnit;

import cpw.mods.fml.common.gameevent.TickEvent;
import darkgrey.rpg.network.MainThreadScheduler;

/** Final-chunk integration against the real decoder, hash check, main-thread completion and idle timeout. */
final class GramophoneDownloadCompletionProbe {

    private static Field field(Class<?> type, String name) throws Exception {
        Field result = type.getDeclaredField(name);
        result.setAccessible(true);
        return result;
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }

    @SuppressWarnings("unchecked")
    static void run(GramophoneMediaInfo source) throws Exception {
        Class<?> type = Class.forName("darkgrey.rpg.gramophone.GramophoneLocalClient$Download");
        java.lang.reflect.Constructor<?> constructor = type.getDeclaredConstructor(GramophoneMediaPacket.class);
        constructor.setAccessible(true);
        Map<String, Object> downloads = (Map<String, Object>) field(GramophoneLocalClient.class, "DOWNLOADS").get(null);
        require(downloads.isEmpty(), "isolated download registry required");
        byte[] bytes = Files.readAllBytes(source.path);
        MainThreadScheduler scheduler = new MainThreadScheduler();
        TickEvent.ClientTickEvent tick = new TickEvent.ClientTickEvent(TickEvent.Phase.START);
        for (boolean corruptHash : new boolean[] { false, true }) {
            Path path = Files.createTempFile(source.path.getParent(), "final-chunk-", ".dgrmp3");
            GramophoneMediaPacket request = new GramophoneMediaPacket();
            request.token = "completion-" + corruptHash;
            request.hash = corruptHash ? new String(new char[64]).replace('\0', '0') : source.hash;
            Object download = constructor.newInstance(request);
            int prefix = Math.max(0, bytes.length - GramophoneMediaPacket.CHUNK_BYTES);
            Files.write(path, java.util.Arrays.copyOf(bytes, prefix));
            field(type, "path").set(download, path);
            field(type, "offset").setInt(download, prefix);
            downloads.put(request.token, download);
            CompletableFuture<GramophoneMediaInfo> future = (CompletableFuture<GramophoneMediaInfo>) field(
                type,
                "future").get(download);
            GramophoneMediaPacket last = new GramophoneMediaPacket();
            last.operation = GramophoneMediaPacket.DATA;
            last.token = request.token;
            last.hash = request.hash;
            last.offset = prefix;
            last.total = bytes.length;
            last.data = java.util.Arrays.copyOfRange(bytes, prefix, bytes.length);
            try {
                GramophoneLocalClient.accept(last);
                long deadline = System.nanoTime() + TimeUnit.SECONDS.toNanos(10);
                while (!future.isDone() && System.nanoTime() < deadline) {
                    scheduler.onClientTick(tick);
                    Thread.sleep(10);
                }
                require(future.isDone() && downloads.isEmpty(), "final chunk settles and releases registry entry");
                if (corruptHash)
                    require(future.isCompletedExceptionally(), "invalid hash must never publish ready metadata");
                else {
                    GramophoneMediaInfo result = future.get();
                    require(
                        result.path.equals(path) && result.hash.equals(source.hash),
                        "validated metadata reaches consumer");
                    require(
                        result.bytes == bytes.length && result.seconds == source.seconds,
                        "duration and byte count preserved");
                    require(result.envelope.length == source.envelope.length, "analyzed envelope delivered with file");
                }
            } finally {
                downloads.remove(request.token);
                Files.deleteIfExists(path);
            }
        }
        GramophoneMediaPacket request = new GramophoneMediaPacket();
        request.token = "idle-timeout";
        Object download = constructor.newInstance(request);
        downloads.put(request.token, download);
        CompletableFuture<?> future = (CompletableFuture<?>) field(type, "future").get(download);
        GramophoneLocalClient.tick();
        require(!future.isDone(), "progressing download remains pending");
        field(type, "touched").setLong(download, System.nanoTime() - TimeUnit.SECONDS.toNanos(61));
        GramophoneLocalClient.tick();
        require(future.isCompletedExceptionally() && downloads.isEmpty(), "idle download still times out");
        System.out.println("GRAMOPHONE_DOWNLOAD_METADATA_HASH_IDLE_TIMEOUT=PASS");
    }
}
