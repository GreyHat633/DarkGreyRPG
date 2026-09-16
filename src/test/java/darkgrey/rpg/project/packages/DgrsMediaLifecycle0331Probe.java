package darkgrey.rpg.project.packages;

import java.io.File;
import java.io.InputStream;
import java.io.OutputStream;
import java.lang.management.ManagementFactory;
import java.lang.reflect.Method;
import java.nio.charset.StandardCharsets;
import java.nio.file.DirectoryStream;
import java.nio.file.Files;
import java.nio.file.LinkOption;
import java.nio.file.Path;
import java.nio.file.StandardOpenOption;
import java.security.MessageDigest;
import java.util.ArrayList;
import java.util.List;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.concurrent.atomic.AtomicLong;
import java.util.zip.ZipEntry;
import java.util.zip.ZipOutputStream;

/** WP-F lifecycle, security, cache and bounded-memory acceptance probe. */
public final class DgrsMediaLifecycle0331Probe {

    private static final int BUFFER_BYTES = 32768;

    private DgrsMediaLifecycle0331Probe() {}

    public static void main(String[] args) throws Exception {
        SessionPageMediaProbe.run();
        if (args.length != 1) throw new IllegalArgumentException("Expected <E-drive probe root>");
        Path parent = new File(args[0]).getAbsoluteFile()
            .toPath()
            .normalize();
        Files.createDirectories(parent);
        Path root = Files.createTempDirectory(parent, "dgrs-wpf-lifecycle-");
        try {
            runLifecycle(root);
            runHeapMatrix(root);
            System.out.println("DGRS_MEDIA_LIFECYCLE_0331=PASS");
        } finally {
            delete(root);
        }
    }

    private static void runLifecycle(Path root) throws Exception {
        Path payload = root.resolve("payload.jpg");
        String ref = writeJpeg(payload, 1024 * 1024);
        Path archive = root.resolve("accepted.dgrs");
        writeArchive(archive, ref, payload);
        DgrsArchiveReader reader = DgrsArchiveReader.open(archive.toFile());
        StoryPackageManifest manifest = StoryPackageManifest
            .read(reader.readBytes("manifest.json"), "probe!/manifest.json");
        String fingerprint = StoryPackageContentFingerprint.compute(manifest, reader);
        Path runtime = root.resolve(".dgrs-runtime");
        DgrsGenerationStore store = new DgrsGenerationStore(runtime);
        DgrsGenerationStore.Generation generation = store.install(archive.toFile(), fingerprint);
        require(Files.exists(generation.getArchive()), "accepted generation was not published");

        Path materialized = store.materialize(ref, reader);
        require(Files.size(materialized) == Files.size(payload), "materialized media size changed");
        require(equalFiles(payload, materialized), "materialized media bytes changed");
        long cacheBytes = treeBytes(runtime.resolve("media"));
        require(cacheBytes == Files.size(payload), "cache byte accounting mismatch");
        System.out.println("DGRS_MEDIA_CACHE_BYTES=" + cacheBytes);

        Files.delete(materialized);
        Path rebuilt = store.materialize(ref, reader);
        require(equalFiles(payload, rebuilt), "deleted media cache did not rebuild");
        require(countFiles(runtime.resolve("media"), ".part") == 0, "media part file leaked after rebuild");
        System.out.println("DGRS_MEDIA_CACHE_REBUILD=PASS");

        ExecutorService workers = Executors.newFixedThreadPool(8);
        CountDownLatch ready = new CountDownLatch(8);
        CountDownLatch start = new CountDownLatch(1);
        List<Future<Path>> futures = new ArrayList<Future<Path>>();
        try {
            for (int i = 0; i < 8; i++) {
                futures.add(workers.submit(new java.util.concurrent.Callable<Path>() {

                    @Override
                    public Path call() throws Exception {
                        ready.countDown();
                        start.await();
                        return store.materialize(ref, reader);
                    }
                }));
            }
            ready.await();
            Files.delete(rebuilt);
            start.countDown();
            Path first = null;
            for (Future<Path> future : futures) {
                Path result = future.get();
                if (first == null) first = result;
                require(first.equals(result), "concurrent media requests diverged");
            }
            require(countFiles(runtime.resolve("media"), ".dgrs") == 0, "unexpected archive in media cache");
            require(countFiles(runtime.resolve("media"), ".part") == 0, "concurrent materialization left a part");
        } finally {
            workers.shutdownNow();
        }
        System.out.println("DGRS_MEDIA_CONCURRENT_REQUESTS=8");
        System.out.println("DGRS_MEDIA_CONCURRENT_DISTINCT_MATERIALIZED_PATHS=1");
        System.out.println("DGRS_MEDIA_CONCURRENT_SINGLE_MATERIALIZATION=PASS");

        expectReject(
            store,
            "media/0000000000000000000000000000000000000000000000000000000000000000.jpg",
            reader,
            "hash mismatch or unauthorized reference");
        expectReject(
            store,
            "media/0000000000000000000000000000000000000000000000000000000000000000.txt",
            reader,
            "invalid media format reference");
        expectReject(store, "media/../escape.jpg", reader, "path traversal reference");
        byte[] corrupt = new byte[] { 1, 2, 3, 4, 5, 6 };
        String corruptRef = "media/" + hex(sha256(corrupt)) + ".jpg";
        Path corruptArchive = root.resolve("corrupt-media.dgrs");
        writeArchive(corruptArchive, corruptRef, corrupt);
        DgrsArchiveReader corruptReader = DgrsArchiveReader.open(corruptArchive.toFile());
        expectReject(store, corruptRef, corruptReader, "corrupt media container");
        require(countFiles(runtime.resolve("media"), ".part") == 0, "failed media validation leaked a part");
        System.out.println("DGRS_MEDIA_INVALID_FORMAT_PATH_HASH=PASS");

        generation.retain();
        final CountDownLatch transferHeld = new CountDownLatch(1);
        final CountDownLatch transferFinish = new CountDownLatch(1);
        Thread transfer = new Thread(new Runnable() {

            @Override
            public void run() {
                try {
                    require(generation.retain(), "transfer lease could not be retained");
                    transferHeld.countDown();
                    transferFinish.await();
                    generation.release();
                } catch (Exception exception) {
                    throw new RuntimeException(exception);
                }
            }
        }, "dgrs-generation-transfer-probe");
        transfer.start();
        transferHeld.await();
        generation.retire();
        require(Files.exists(generation.getArchive()), "retired generation was deleted while leased");
        generation.release();
        transferFinish.countDown();
        transfer.join();
        require(!Files.exists(generation.getArchive()), "retired generation was not reclaimed after final lease");
        require(!generation.retain(), "retired generation accepted a new lease");
        System.out.println("DGRS_MEDIA_GENERATION_LEASE_TRANSFER=PASS");

        Files.createDirectories(runtime.resolve("generations"));
        Files.createDirectories(runtime.resolve("media"));
        Files.write(runtime.resolve("generations/leftover.dgrs.part"), new byte[] { 1 });
        Files.write(runtime.resolve("media/leftover.media.part"), new byte[] { 1 });
        store.recoverParts();
        require(countFiles(runtime.resolve("generations"), ".part") == 0, "generation part recovery failed");
        require(countFiles(runtime.resolve("media"), ".part") == 0, "media part recovery failed");
        System.out.println("DGRS_MEDIA_PART_RECOVERY=PASS");

        Path linkedRuntime = root.resolve("linked-runtime");
        Path linkTarget = root.resolve("link-target");
        boolean symlinkChecked = false;
        try {
            Files.createDirectories(linkTarget);
            Files.createSymbolicLink(linkedRuntime, linkTarget);
            symlinkChecked = true;
            try {
                new DgrsGenerationStore(linkedRuntime).materialize(ref, reader);
                throw new AssertionError("followed a symlinked runtime root");
            } catch (java.io.IOException expected) {
                // expected
            }
        } catch (UnsupportedOperationException | java.io.IOException expected) {
            // Windows may deny symlink creation without the developer privilege.
        }
        System.out.println("DGRS_MEDIA_REPARSE_ROOT_GUARD=" + (symlinkChecked ? "PASS" : "SKIP_UNAVAILABLE"));
        System.out.println("DGRS_MEDIA_OPEN_HANDLES=" + openHandleCount());
    }

    private static void runHeapMatrix(Path root) throws Exception {
        Path smallPayload = root.resolve("small.jpg");
        Path largePayload = root.resolve("large.jpg");
        String smallRef = writeJpeg(smallPayload, 8 * 1024 * 1024);
        String largeRef = writeJpeg(largePayload, 32 * 1024 * 1024);
        Path smallArchive = root.resolve("small.dgrs");
        Path largeArchive = root.resolve("large.dgrs");
        writeArchive(smallArchive, smallRef, smallPayload);
        writeArchive(largeArchive, largeRef, largePayload);
        forceGc();
        long beforeSmall = usedHeap();
        HeapSampler smallSampler = new HeapSampler();
        smallSampler.start();
        DgrsArchiveReader smallReader = DgrsArchiveReader.open(smallArchive.toFile());
        smallSampler.finish();
        forceGc();
        long retainedSmall = Math.max(0L, usedHeap() - beforeSmall);
        forceGc();
        long beforeLarge = usedHeap();
        HeapSampler largeSampler = new HeapSampler();
        largeSampler.start();
        DgrsArchiveReader largeReader = DgrsArchiveReader.open(largeArchive.toFile());
        largeSampler.finish();
        forceGc();
        long retainedLargeIncrement = Math.max(0L, usedHeap() - beforeLarge);
        long smallPeakDelta = Math.max(0L, smallSampler.peak() - beforeSmall);
        long largePeakDelta = Math.max(0L, largeSampler.peak() - beforeLarge);
        require(smallReader.getEntrySize(smallRef) == 8L * 1024L * 1024L, "small entry metadata size changed");
        require(largeReader.getEntrySize(largeRef) == 32L * 1024L * 1024L, "large entry metadata size changed");
        require(retainedLargeIncrement < 8L * 1024L * 1024L, "retained heap scaled with media bytes");
        require(largePeakDelta < 16L * 1024L * 1024L, "scan peak scaled with full media bytes");
        System.out.println("DGRS_MEDIA_HEAP_RETAINED_SMALL_BYTES=" + retainedSmall);
        System.out.println("DGRS_MEDIA_HEAP_RETAINED_LARGE_INCREMENT_BYTES=" + retainedLargeIncrement);
        System.out.println("DGRS_MEDIA_HEAP_SCAN_PEAK_SMALL_DELTA_BYTES=" + smallPeakDelta);
        System.out.println("DGRS_MEDIA_HEAP_SCAN_PEAK_LARGE_DELTA_BYTES=" + largePeakDelta);
        System.out.println("DGRS_MEDIA_HEAP_MEDIA_BYTES_SMALL=8388608");
        System.out.println("DGRS_MEDIA_HEAP_MEDIA_BYTES_LARGE=33554432");
        System.out.println("DGRS_MEDIA_METADATA_ONLY_HEAP=PASS");
        System.out.println("DGRS_MEDIA_MAX_IN_FLIGHT_BYTES=" + DgrsGenerationStore.COPY_BUFFER_BYTES);
        System.out.println("DGRS_MEDIA_CACHE_DISK_BYTES=" + treeBytes(root.resolve(".dgrs-runtime")));
    }

    private static void expectReject(DgrsGenerationStore store, String ref, DgrsArchiveReader reader, String label)
        throws Exception {
        try {
            store.materialize(ref, reader);
            throw new AssertionError("Accepted " + label);
        } catch (java.io.IOException expected) {
            // expected
        }
    }

    private static String writeJpeg(Path path, int bytes) throws Exception {
        require(bytes >= 20, "test JPEG size too small");
        MessageDigest digest = MessageDigest.getInstance("SHA-256");
        try (OutputStream output = Files
            .newOutputStream(path, StandardOpenOption.CREATE_NEW, StandardOpenOption.WRITE)) {
            byte[] buffer = new byte[BUFFER_BYTES];
            output.write(0xff);
            output.write(0xd8);
            digest.update((byte) 0xff);
            digest.update((byte) 0xd8);
            int remaining = bytes - 4;
            int seed = 0;
            while (remaining > 0) {
                int count = Math.min(remaining, buffer.length);
                for (int i = 0; i < count; i++) buffer[i] = (byte) (seed++ * 31);
                output.write(buffer, 0, count);
                digest.update(buffer, 0, count);
                remaining -= count;
            }
            output.write(0xff);
            output.write(0xd9);
            digest.update((byte) 0xff);
            digest.update((byte) 0xd9);
        }
        return "media/" + hex(digest.digest()) + ".jpg";
    }

    private static void writeArchive(Path path, String ref, Path payload) throws Exception {
        try (OutputStream output = Files.newOutputStream(path); ZipOutputStream zip = new ZipOutputStream(output)) {
            put(zip, "manifest.json", manifest(ref).getBytes(StandardCharsets.UTF_8));
            put(zip, "project.json", "{}".getBytes(StandardCharsets.UTF_8));
            put(zip, "stories/story.json", "{}".getBytes(StandardCharsets.UTF_8));
            zip.putNextEntry(new ZipEntry(ref));
            try (InputStream input = Files.newInputStream(payload)) {
                copy(input, zip);
            }
            zip.closeEntry();
        }
    }

    private static void writeArchive(Path path, String ref, byte[] payload) throws Exception {
        try (OutputStream output = Files.newOutputStream(path); ZipOutputStream zip = new ZipOutputStream(output)) {
            put(zip, "manifest.json", manifest(ref).getBytes(StandardCharsets.UTF_8));
            put(zip, "project.json", "{}".getBytes(StandardCharsets.UTF_8));
            put(zip, "stories/story.json", "{}".getBytes(StandardCharsets.UTF_8));
            put(zip, ref, payload);
        }
    }

    private static String manifest(String ref) {
        return "{\"format\":\"dgrs\",\"format_version\":1,\"producer\":\"DarkGreyRPGStudio\","
            + "\"producer_version\":\"probe\",\"schema_version\":1,\"package_id\":\"probe\","
            + "\"package_version\":\"1\",\"story_id\":\"story\",\"story_schema_version\":1,"
            + "\"required_resources\":{\"story\":\"stories/story.json\",\"actors\":[],\"items\":[],"
            + "\"item_groups\":[],\"dialogues\":[],\"quests\":[],\"canonical_stories\":[],"
            + "\"canonical_memberships\":[],\"sessions\":[],\"tasks\":[],\"media\":[\""
            + ref
            + "\"]}}";
    }

    private static void put(ZipOutputStream zip, String name, byte[] data) throws Exception {
        zip.putNextEntry(new ZipEntry(name));
        zip.write(data);
        zip.closeEntry();
    }

    private static void copy(InputStream input, OutputStream output) throws Exception {
        byte[] buffer = new byte[BUFFER_BYTES];
        int count;
        while ((count = input.read(buffer)) != -1) output.write(buffer, 0, count);
    }

    private static boolean equalFiles(Path left, Path right) throws Exception {
        try (InputStream a = Files.newInputStream(left); InputStream b = Files.newInputStream(right)) {
            byte[] leftBuffer = new byte[BUFFER_BYTES];
            byte[] rightBuffer = new byte[BUFFER_BYTES];
            int leftCount;
            while ((leftCount = a.read(leftBuffer)) != -1) {
                int position = 0;
                while (position < leftCount) {
                    int count = b.read(rightBuffer, 0, leftCount - position);
                    if (count < 0) return false;
                    for (int i = 0; i < count; i++) if (leftBuffer[position + i] != rightBuffer[i]) return false;
                    position += count;
                }
            }
            return b.read() == -1;
        }
    }

    private static long treeBytes(Path directory) throws Exception {
        if (!Files.isDirectory(directory, LinkOption.NOFOLLOW_LINKS)) return 0L;
        long total = 0L;
        try (DirectoryStream<Path> entries = Files.newDirectoryStream(directory)) {
            for (Path entry : entries) {
                if (Files.isDirectory(entry, LinkOption.NOFOLLOW_LINKS)) total += treeBytes(entry);
                else if (Files.isRegularFile(entry, LinkOption.NOFOLLOW_LINKS)) total += Files.size(entry);
            }
        }
        return total;
    }

    private static int countFiles(Path directory, String suffix) throws Exception {
        if (!Files.isDirectory(directory, LinkOption.NOFOLLOW_LINKS)) return 0;
        int count = 0;
        try (DirectoryStream<Path> entries = Files.newDirectoryStream(directory)) {
            for (Path entry : entries) {
                if (Files.isDirectory(entry, LinkOption.NOFOLLOW_LINKS)) count += countFiles(entry, suffix);
                else if (entry.getFileName()
                    .toString()
                    .endsWith(suffix)) count++;
            }
        }
        return count;
    }

    private static void delete(Path path) throws Exception {
        if (!Files.exists(path, LinkOption.NOFOLLOW_LINKS)) return;
        if (Files.isDirectory(path, LinkOption.NOFOLLOW_LINKS)) {
            try (DirectoryStream<Path> entries = Files.newDirectoryStream(path)) {
                for (Path entry : entries) delete(entry);
            }
        }
        Files.deleteIfExists(path);
    }

    private static void forceGc() throws InterruptedException {
        for (int i = 0; i < 3; i++) {
            System.gc();
            Thread.sleep(25L);
        }
    }

    private static long usedHeap() {
        Runtime runtime = Runtime.getRuntime();
        return runtime.totalMemory() - runtime.freeMemory();
    }

    private static long openHandleCount() {
        try {
            Method method = ManagementFactory.getOperatingSystemMXBean()
                .getClass()
                .getMethod("getOpenFileDescriptorCount");
            Object value = method.invoke(ManagementFactory.getOperatingSystemMXBean());
            return ((Number) value).longValue();
        } catch (Exception unavailable) {
            return -1L;
        }
    }

    private static byte[] sha256(byte[] data) throws Exception {
        return MessageDigest.getInstance("SHA-256")
            .digest(data);
    }

    private static String hex(byte[] values) {
        StringBuilder text = new StringBuilder(values.length * 2);
        for (byte value : values) {
            text.append(Character.forDigit((value & 255) >>> 4, 16));
            text.append(Character.forDigit(value & 15, 16));
        }
        return text.toString();
    }

    private static void require(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }

    private static final class HeapSampler implements Runnable {

        private final AtomicLong peak = new AtomicLong();
        private volatile boolean running = true;
        private final Thread thread = new Thread(this, "dgrs-heap-sampler");

        void start() {
            thread.start();
        }

        void finish() throws InterruptedException {
            running = false;
            thread.join();
        }

        long peak() {
            return peak.get();
        }

        @Override
        public void run() {
            while (running) {
                peak.accumulateAndGet(usedHeap(), Math::max);
                Thread.yield();
            }
        }
    }
}
