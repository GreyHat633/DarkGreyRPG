package darkgrey.rpg.media;

import java.io.IOException;
import java.io.InputStream;
import java.io.RandomAccessFile;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardCopyOption;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.BitSet;

import darkgrey.rpg.graph.canonical.CanonicalMediaReference;
import darkgrey.rpg.network.message.canonical.CanonicalMediaChunk;

/** Disk work is performed only by the media worker, never by the render thread. */
public final class VerifiedMediaCache {

    private final Path root;
    private boolean recovered;
    private final java.util.concurrent.ConcurrentHashMap<String, java.util.concurrent.atomic.AtomicInteger> pins = new java.util.concurrent.ConcurrentHashMap<String, java.util.concurrent.atomic.AtomicInteger>();

    public boolean pin(String ref) {
        path(ref);
        java.util.concurrent.atomic.AtomicInteger count = pins
            .computeIfAbsent(ref, key -> new java.util.concurrent.atomic.AtomicInteger());
        for (;;) {
            int value = count.get();
            if (value < 0) return false;
            if (count.compareAndSet(value, value + 1)) return true;
        }
    }

    public void unpin(String ref) {
        java.util.concurrent.atomic.AtomicInteger count = pins.get(ref);
        if (count == null || count.decrementAndGet() < 0) throw new IllegalStateException("Unbalanced media cache pin");
    }

    Path root() {
        return root;
    }

    boolean deleteIfUnpinned(String ref) throws IOException {
        java.util.concurrent.atomic.AtomicInteger count = pins
            .computeIfAbsent(ref, key -> new java.util.concurrent.atomic.AtomicInteger());
        if (!count.compareAndSet(0, -1)) return false;
        try {
            return Files.deleteIfExists(path(ref));
        } finally {
            count.set(0);
        }
    }

    public VerifiedMediaCache(Path root) {
        this.root = root.toAbsolutePath()
            .normalize();
    }

    public Path path(String ref) {
        if (!CanonicalMediaReference.isValid(ref)) throw new IllegalArgumentException("Invalid media cache reference");
        return root.resolve(ref);
    }

    public boolean available(String ref) throws IOException {
        if (!recovered) {
            recoverInterruptedDownloads();
            recovered = true;
        }
        Path file = path(ref);
        return Files.isRegularFile(file) && Files.size(file) <= CanonicalMediaChunk.MAX_MEDIA_BYTES
            && ref.substring(6, 70)
                .equals(hash(file));
    }

    public Download begin(long requestId, String ref, int total) throws IOException {
        return new Download(requestId, ref, total);
    }

    /** Worker-only recovery; live downloads hold a file lock and are never collected. */
    public void recoverInterruptedDownloads() throws IOException {
        Path media = root.resolve("media");
        if (!Files.isDirectory(media) || Files.isSymbolicLink(media) || Files.isSymbolicLink(root)) return;
        try (java.nio.file.DirectoryStream<Path> files = Files.newDirectoryStream(media, ".media-*.part")) {
            for (Path part : files) {
                if (!Files.isRegularFile(part, java.nio.file.LinkOption.NOFOLLOW_LINKS) || Files.isSymbolicLink(part))
                    continue;
                boolean abandoned = false;
                try (java.nio.channels.FileChannel channel = java.nio.channels.FileChannel
                    .open(part, java.nio.file.StandardOpenOption.WRITE)) {
                    try (java.nio.channels.FileLock lock = channel.tryLock()) {
                        abandoned = lock != null;
                    }
                } catch (java.nio.channels.OverlappingFileLockException | java.nio.file.AccessDeniedException busy) {
                    continue;
                }
                if (abandoned) Files.deleteIfExists(part);
            }
        }
    }

    private static String hash(Path file) throws IOException {
        try {
            MessageDigest digest = MessageDigest.getInstance("SHA-256");
            try (InputStream input = Files.newInputStream(file)) {
                byte[] buffer = new byte[8192];
                int count;
                while ((count = input.read(buffer)) != -1) digest.update(buffer, 0, count);
            }
            StringBuilder text = new StringBuilder();
            for (byte value : digest.digest()) {
                text.append(Character.forDigit((value & 255) >> 4, 16));
                text.append(Character.forDigit(value & 15, 16));
            }
            return text.toString();
        } catch (NoSuchAlgorithmException exception) {
            throw new IllegalStateException(exception);
        }
    }

    public final class Download implements AutoCloseable {

        private final long id;
        private final String ref;
        private final int total;
        private final Path temporary;
        private final RandomAccessFile file;
        private final BitSet received = new BitSet();
        private boolean closed;

        private Download(long id, String ref, int total) throws IOException {
            if (id <= 0 || total <= 0 || total > CanonicalMediaChunk.MAX_MEDIA_BYTES)
                throw new IllegalArgumentException("Invalid download bounds");
            this.id = id;
            this.ref = ref;
            this.total = total;
            Files.createDirectories(path(ref).getParent());
            temporary = Files.createTempFile(path(ref).getParent(), ".media-", ".part");
            file = new RandomAccessFile(temporary.toFile(), "rw");
            try {
                file.getChannel()
                    .lock(); // Released when the file closes, including exceptional cleanup.
                file.setLength(total);
            } catch (IOException | RuntimeException exception) {
                file.close();
                Files.deleteIfExists(temporary);
                throw exception;
            }
        }

        public boolean accept(CanonicalMediaChunk chunk) throws IOException {
            if (closed || chunk.getRequestId() != id || !ref.equals(chunk.getMediaRef()) || chunk.getTotal() != total)
                throw new IOException("Stale or inconsistent media transfer");
            int index = chunk.getOffset() / CanonicalMediaChunk.CHUNK_BYTES;
            byte[] bytes = chunk.getData();
            if (received.get(index)) {
                byte[] existing = new byte[bytes.length];
                file.seek(chunk.getOffset());
                file.readFully(existing);
                if (!java.util.Arrays.equals(existing, bytes))
                    throw new IOException("Conflicting duplicate media chunk");
            } else {
                file.seek(chunk.getOffset());
                file.write(bytes);
                received.set(index);
            }
            if (received.cardinality()
                != (total + CanonicalMediaChunk.CHUNK_BYTES - 1) / CanonicalMediaChunk.CHUNK_BYTES) return false;
            file.getFD()
                .sync();
            file.close();
            closed = true;
            if (!ref.substring(6, 70)
                .equals(hash(temporary))) {
                Files.deleteIfExists(temporary);
                throw new IOException("Media fingerprint mismatch");
            }
            Files.move(temporary, path(ref), StandardCopyOption.ATOMIC_MOVE, StandardCopyOption.REPLACE_EXISTING);
            return true;
        }

        @Override
        public void close() throws IOException {
            if (!closed) {
                file.close();
                closed = true;
            }
            Files.deleteIfExists(temporary);
        }
    }
}
