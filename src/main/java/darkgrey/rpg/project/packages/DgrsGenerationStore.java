package darkgrey.rpg.project.packages;

import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.nio.file.AtomicMoveNotSupportedException;
import java.nio.file.DirectoryStream;
import java.nio.file.Files;
import java.nio.file.LinkOption;
import java.nio.file.NoSuchFileException;
import java.nio.file.Path;
import java.nio.file.StandardCopyOption;
import java.nio.file.StandardOpenOption;
import java.nio.file.attribute.BasicFileAttributes;
import java.security.MessageDigest;
import java.security.NoSuchAlgorithmException;
import java.util.Set;
import java.util.concurrent.ConcurrentHashMap;
import java.util.concurrent.atomic.AtomicInteger;

import darkgrey.rpg.graph.canonical.CanonicalMediaReference;

/** Controlled, immutable archive generations and fingerprint-keyed media files. */
public final class DgrsGenerationStore {

    public static final int COPY_BUFFER_BYTES = 32768;
    private final Path root;
    private final Path generations;
    private final Path media;
    private final ConcurrentHashMap<String, Object> locks = new ConcurrentHashMap<String, Object>();
    private final ConcurrentHashMap<Path, AtomicInteger> generationOwners = new ConcurrentHashMap<Path, AtomicInteger>();

    public DgrsGenerationStore(Path root) {
        if (root == null) throw new IllegalArgumentException("Generation root is required");
        this.root = root.toAbsolutePath()
            .normalize();
        this.generations = this.root.resolve("generations");
        this.media = this.root.resolve("media");
    }

    public Path getRoot() {
        return root;
    }

    /** Copies a validated source archive once, then gives the package one owner lease. */
    public Generation install(java.io.File source, String fingerprint) throws IOException {
        if (source == null || fingerprint == null || !fingerprint.matches("[0-9a-fA-F]{64}"))
            throw new IllegalArgumentException("Invalid generation source or fingerprint");
        ensureDirectory(root);
        ensureDirectory(generations);
        Path target = generations.resolve(fingerprint.toLowerCase(java.util.Locale.ROOT) + ".dgrs");
        Object lock = lockFor("generation:" + target.getFileName());
        synchronized (lock) {
            if (!safeRegularFile(target)) {
                Path part = generations.resolve(
                    target.getFileName()
                        .toString() + ".part");
                Files.deleteIfExists(part);
                copyBounded(source.toPath(), part);
                moveAtomic(part, target);
            }
            try {
                DgrsArchiveReader copied = DgrsArchiveReader.open(target.toFile());
                StoryPackageManifest manifest = StoryPackageManifest
                    .read(copied.readBytes("manifest.json"), target.toString() + "!/manifest.json");
                if (!fingerprint.equalsIgnoreCase(StoryPackageContentFingerprint.compute(manifest, copied)))
                    throw new IOException("Generation fingerprint changed during copy");
            } catch (darkgrey.rpg.project.ProjectLoadException exception) {
                throw new IOException("Immutable generation verification failed", exception);
            }
        }
        AtomicInteger owners = generationOwners.get(target);
        if (owners == null) {
            AtomicInteger created = new AtomicInteger();
            owners = generationOwners.putIfAbsent(target, created);
            if (owners == null) owners = created;
        }
        owners.incrementAndGet();
        return new Generation(target, owners);
    }

    /** Materialises one media entry into a fingerprint-keyed immutable file. */
    public Path materialize(String ref, DgrsArchiveReader source) throws IOException {
        if (!CanonicalMediaReference.isValid(ref) || source == null || !source.contains(ref))
            throw new IOException("Unauthorized or missing DGRS media reference");
        ensureDirectory(root);
        ensureDirectory(media);
        Path target = media.resolve(ref.substring(6));
        if (!within(media, target)) throw new IOException("Media cache path escaped its root");
        Object lock = lockFor("media:" + target.getFileName());
        synchronized (lock) {
            long expected;
            try {
                expected = source.getEntrySize(ref);
            } catch (darkgrey.rpg.project.ProjectLoadException exception) {
                throw new IOException("Missing DGRS media", exception);
            }
            if (safeRegularFile(target) && Files.size(target) == expected
                && ref.substring(6, 70)
                    .equals(hashFile(target)))
                return target;
            Path part = media.resolve(
                "." + target.getFileName()
                    .toString() + ".part");
            Files.deleteIfExists(part);
            long copied = 0L;
            MessageDigest digest = sha256();
            InputStream opened;
            try {
                opened = source.openStream(ref);
            } catch (darkgrey.rpg.project.ProjectLoadException exception) {
                throw new IOException("Missing DGRS media", exception);
            }
            try (InputStream input = opened;
                OutputStream output = Files
                    .newOutputStream(part, StandardOpenOption.CREATE_NEW, StandardOpenOption.WRITE)) {
                byte[] buffer = new byte[COPY_BUFFER_BYTES];
                int count;
                while ((count = input.read(buffer)) != -1) {
                    copied += count;
                    if (copied > DgrsArchiveReader.MAX_ENTRY_BYTES) throw new IOException("Media exceeds size bound");
                    digest.update(buffer, 0, count);
                    output.write(buffer, 0, count);
                }
            }
            if (copied != expected || !ref.substring(6, 70)
                .equals(hex(digest.digest()))) {
                Files.deleteIfExists(part);
                throw new IOException("Media fingerprint mismatch");
            }
            try (InputStream check = Files.newInputStream(part)) {
                darkgrey.rpg.media.MediaPayloadValidation.validate(ref, check, copied);
            } catch (IllegalArgumentException exception) {
                Files.deleteIfExists(part);
                throw new IOException("Corrupt media payload", exception);
            }
            moveAtomic(part, target);
            return target;
        }
    }

    public void recoverParts() throws IOException {
        recoverDirectory(generations);
        recoverDirectory(media);
    }

    /** Removes only unleased generations outside the current accepted set. */
    public void sweepGenerations(Set<String> keepFingerprints) throws IOException {
        if (keepFingerprints == null || !safeDirectory(generations)) return;
        java.util.HashSet<String> keep = new java.util.HashSet<String>();
        for (String value : keepFingerprints) if (value != null) keep.add(value.toLowerCase(java.util.Locale.ROOT));
        try (DirectoryStream<Path> entries = Files.newDirectoryStream(generations, "*.dgrs")) {
            for (Path entry : entries) {
                if (!safeRegularFile(entry)) continue;
                String name = entry.getFileName()
                    .toString();
                String fingerprint = name.substring(0, name.length() - 5)
                    .toLowerCase(java.util.Locale.ROOT);
                AtomicInteger owners = generationOwners.get(
                    entry.toAbsolutePath()
                        .normalize());
                if (!keep.contains(fingerprint) && (owners == null || owners.get() <= 0)) Files.deleteIfExists(entry);
            }
        }
    }

    private void recoverDirectory(Path directory) throws IOException {
        if (!safeDirectory(directory)) return;
        try (DirectoryStream<Path> entries = Files.newDirectoryStream(directory, "*.part")) {
            for (Path entry : entries) if (safeRegularFile(entry)) Files.deleteIfExists(entry);
        }
    }

    private void copyBounded(Path source, Path target) throws IOException {
        if (!safeRegularFile(source)) throw new IOException("Generation source is not a regular file");
        long copied = 0L;
        try (InputStream input = Files.newInputStream(source);
            OutputStream output = Files
                .newOutputStream(target, StandardOpenOption.CREATE_NEW, StandardOpenOption.WRITE)) {
            byte[] buffer = new byte[COPY_BUFFER_BYTES];
            int count;
            while ((count = input.read(buffer)) != -1) {
                copied += count;
                output.write(buffer, 0, count);
            }
        }
        if (copied != Files.size(source)) {
            Files.deleteIfExists(target);
            throw new IOException("Generation source changed during copy");
        }
    }

    private static void moveAtomic(Path source, Path target) throws IOException {
        try {
            Files.move(source, target, StandardCopyOption.ATOMIC_MOVE, StandardCopyOption.REPLACE_EXISTING);
        } catch (AtomicMoveNotSupportedException exception) {
            Files.move(source, target, StandardCopyOption.REPLACE_EXISTING);
        }
    }

    private Object lockFor(String key) {
        Object lock = new Object();
        Object prior = locks.putIfAbsent(key, lock);
        return prior == null ? lock : prior;
    }

    private static boolean within(Path parent, Path child) {
        return child.toAbsolutePath()
            .normalize()
            .startsWith(
                parent.toAbsolutePath()
                    .normalize());
    }

    private static boolean safeDirectory(Path path) throws IOException {
        try {
            BasicFileAttributes attributes = Files
                .readAttributes(path, BasicFileAttributes.class, LinkOption.NOFOLLOW_LINKS);
            return attributes.isDirectory() && !attributes.isSymbolicLink()
                && !attributes.isOther()
                && !isReparse(path);
        } catch (NoSuchFileException missing) {
            return false;
        }
    }

    private static boolean safeRegularFile(Path path) throws IOException {
        try {
            BasicFileAttributes attributes = Files
                .readAttributes(path, BasicFileAttributes.class, LinkOption.NOFOLLOW_LINKS);
            return attributes.isRegularFile() && !attributes.isSymbolicLink()
                && !attributes.isOther()
                && !isReparse(path);
        } catch (NoSuchFileException missing) {
            return false;
        }
    }

    private static boolean isReparse(Path path) throws IOException {
        if (Files.isSymbolicLink(path)) return true;
        Path lexical = path.toAbsolutePath()
            .normalize();
        Path followed = path.toRealPath();
        return !lexical.equals(followed);
    }

    private static void ensureDirectory(Path path) throws IOException {
        if (!Files.exists(path, LinkOption.NOFOLLOW_LINKS)) Files.createDirectories(path);
        if (!safeDirectory(path)) throw new IOException("Cache path is not a safe directory: " + path);
    }

    private static MessageDigest sha256() {
        try {
            return MessageDigest.getInstance("SHA-256");
        } catch (NoSuchAlgorithmException exception) {
            throw new IllegalStateException(exception);
        }
    }

    private static String hex(byte[] values) {
        StringBuilder result = new StringBuilder(values.length * 2);
        for (byte value : values) {
            result.append(Character.forDigit((value & 255) >>> 4, 16));
            result.append(Character.forDigit(value & 15, 16));
        }
        return result.toString();
    }

    private static String hashFile(Path path) throws IOException {
        MessageDigest digest = sha256();
        try (InputStream input = Files.newInputStream(path)) {
            byte[] buffer = new byte[COPY_BUFFER_BYTES];
            int count;
            while ((count = input.read(buffer)) != -1) digest.update(buffer, 0, count);
        }
        return hex(digest.digest());
    }

    public final class Generation {

        private final Path archive;
        private final AtomicInteger owners;
        private final AtomicInteger leases = new AtomicInteger(1);
        private volatile boolean retired;

        private Generation(Path archive, AtomicInteger owners) {
            this.archive = archive;
            this.owners = owners;
        }

        public Path getArchive() {
            return archive;
        }

        public synchronized boolean retain() {
            if (retired) return false;
            leases.incrementAndGet();
            return true;
        }

        public synchronized void retire() throws IOException {
            if (retired) return;
            retired = true;
            release();
        }

        public synchronized void release() throws IOException {
            int remaining = leases.decrementAndGet();
            if (remaining < 0) throw new IllegalStateException("Unbalanced generation lease");
            if (remaining == 0 && retired) {
                if (owners.decrementAndGet() <= 0) {
                    generationOwners.remove(archive, owners);
                    Files.deleteIfExists(archive);
                }
            }
        }

        public boolean isRetired() {
            return retired;
        }

        public int getLeaseCount() {
            return leases.get();
        }
    }
}
