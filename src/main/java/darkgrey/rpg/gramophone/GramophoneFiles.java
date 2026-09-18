package darkgrey.rpg.gramophone;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.LinkOption;
import java.nio.file.Path;
import java.nio.file.attribute.BasicFileAttributes;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.Executors;
import java.util.concurrent.TimeUnit;

/** Only known files are retired; never recursively delete a user-controlled tree. */
public final class GramophoneFiles {

    private static final ConcurrentHashMap<Path, Boolean> RETIRED = new ConcurrentHashMap<Path, Boolean>();
    private static final ConcurrentHashMap<Path, Lease> LEASES = new ConcurrentHashMap<Path, Lease>();
    private static final java.util.concurrent.ConcurrentLinkedQueue<Lease> CLOSING = new java.util.concurrent.ConcurrentLinkedQueue<Lease>();

    private static final class Lease {

        final Path directory;
        java.nio.channels.FileChannel channel;
        java.nio.channels.FileLock lock;
        boolean closed;

        Lease(Path directory) {
            this.directory = directory;
        }
    }

    public static void activate(Path directory) {
        Path normalized = directory.toAbsolutePath()
            .normalize();
        LEASES.putIfAbsent(normalized, new Lease(normalized));
    }

    public static void release(Path directory) {
        if (directory == null) return;
        Lease lease = LEASES.remove(
            directory.toAbsolutePath()
                .normalize());
        if (lease != null) {
            synchronized (lease) {
                lease.closed = true;
            }
            CLOSING.add(lease);
        }
    }

    static {
        Executors.newSingleThreadScheduledExecutor(task -> {
            Thread thread = new Thread(task, "DGR-Gramophone-Cleanup");
            thread.setDaemon(true);
            return thread;
        })
            .scheduleWithFixedDelay(() -> {
                Lease lease;
                while ((lease = CLOSING.poll()) != null) synchronized (lease) {
                    try {
                        if (lease.lock != null) lease.lock.release();
                        if (lease.channel != null) lease.channel.close();
                        cleanContext(lease.directory);
                    } catch (IOException ignored) {}
                }
                for (Path path : RETIRED.keySet()) try {
                    check(path);
                    Files.deleteIfExists(path);
                    RETIRED.remove(path);
                    Path parent = path.getParent();
                    cleanContext(parent);
                } catch (IOException exception) { /* Audio engine may still be closing its stream; retry later. */ }
            }, 2, 2, TimeUnit.SECONDS);
    }

    private GramophoneFiles() {}

    public static void check(Path path) throws IOException {
        Path absolute = path.toAbsolutePath()
            .normalize();
        for (Path cursor = absolute; cursor != null; cursor = cursor.getParent()) {
            if (!Files.exists(cursor, LinkOption.NOFOLLOW_LINKS)) continue;
            BasicFileAttributes attributes = Files
                .readAttributes(cursor, BasicFileAttributes.class, LinkOption.NOFOLLOW_LINKS);
            if (attributes.isSymbolicLink() || attributes.isOther()
                || !cursor.toRealPath()
                    .equals(cursor))
                throw new IOException("留声机管理目录不能经过链接或重解析点");
        }
    }

    public static void directory(Path path) throws IOException {
        if (RETIRED.size() > 512) throw new IOException("留声机临时文件等待释放，请关闭播放器后重试");
        check(path);
        Files.createDirectories(path);
        check(path);
        if (cacheContext(path)) {
            Lease lease = LEASES.get(
                path.toAbsolutePath()
                    .normalize());
            if (lease == null) throw new IOException("媒体上下文已关闭");
            synchronized (lease) {
                if (lease.closed) throw new IOException("媒体上下文已关闭");
                if (lease.channel == null) {
                    Path marker = path.resolve("Owner.lock");
                    check(marker);
                    lease.channel = java.nio.channels.FileChannel
                        .open(marker, java.nio.file.StandardOpenOption.CREATE, java.nio.file.StandardOpenOption.WRITE);
                    lease.lock = lease.channel.tryLock();
                    if (lease.lock == null) {
                        lease.channel.close();
                        lease.channel = null;
                        throw new IOException("媒体上下文由另一个客户端持有");
                    }
                }
            }
        }
    }

    private static boolean cacheContext(Path path) {
        return path.getFileName() != null && path.getFileName()
            .toString()
            .matches("[0-9a-f-]{36}")
            && path.getParent() != null
            && path.getParent()
                .getFileName()
                .toString()
                .equals("Gramophone")
            && path.getParent()
                .getParent() != null
            && path.getParent()
                .getParent()
                .getFileName()
                .toString()
                .equals("Cache");
    }

    private static void cleanContext(Path path) throws IOException {
        if (!cacheContext(path) || LEASES.containsKey(
            path.toAbsolutePath()
                .normalize())
            || !Files.exists(path)) return;
        check(path);
        try (java.nio.file.DirectoryStream<Path> files = Files.newDirectoryStream(path)) {
            for (Path file : files) if (!file.getFileName()
                .toString()
                .equals("Owner.lock")) return;
        }
        Path marker = path.resolve("Owner.lock");
        check(marker);
        Files.deleteIfExists(marker);
        Files.deleteIfExists(path);
    }

    public static void retire(Path path) {
        if (path != null) RETIRED.put(
            path.toAbsolutePath()
                .normalize(),
            Boolean.TRUE);
    }

    public static void reclaimCache(Path root) throws IOException {
        directory(root);
        try (java.nio.file.DirectoryStream<Path> contexts = Files.newDirectoryStream(root)) {
            for (Path context : contexts) {
                if (!context.getFileName()
                    .toString()
                    .matches("[0-9a-f-]{36}")) continue;
                check(context);
                if (!Files.isDirectory(context, LinkOption.NOFOLLOW_LINKS)) continue;
                // Cross-process file locks protect long-running clients, not just recently-created directories.
                if (System.currentTimeMillis() - Files.getLastModifiedTime(context)
                    .toMillis() < 86400000L) continue;
                Path marker = context.resolve("Owner.lock");
                check(marker);
                if (!Files.isRegularFile(marker, LinkOption.NOFOLLOW_LINKS)) continue;
                try (java.nio.channels.FileChannel owner = java.nio.channels.FileChannel
                    .open(marker, java.nio.file.StandardOpenOption.WRITE)) {
                    java.nio.channels.FileLock lock;
                    try {
                        lock = owner.tryLock();
                    } catch (java.nio.channels.OverlappingFileLockException active) {
                        continue;
                    }
                    if (lock == null) continue;
                    try (java.nio.file.DirectoryStream<Path> files = Files.newDirectoryStream(context)) {
                        for (Path file : files) if (file.getFileName()
                            .toString()
                            .matches("(track|draft)-[0-9]+\\.dgrmp3")) retire(file);
                    } finally {
                        lock.release();
                    }
                }
            }
        }
    }
}
