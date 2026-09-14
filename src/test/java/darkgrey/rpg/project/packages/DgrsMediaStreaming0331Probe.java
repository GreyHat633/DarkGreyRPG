package darkgrey.rpg.project.packages;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.OutputStream;
import java.nio.file.DirectoryStream;
import java.nio.file.Files;
import java.nio.file.Path;
import java.security.MessageDigest;
import java.util.concurrent.CountDownLatch;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.Executors;
import java.util.concurrent.Future;
import java.util.zip.CRC32;
import java.util.zip.ZipEntry;
import java.util.zip.ZipOutputStream;

/** Focused WP-F probe: metadata-only archive, streaming media cache, leases and guards. */
public final class DgrsMediaStreaming0331Probe {

    private DgrsMediaStreaming0331Probe() {}

    public static void main(String[] args) throws Exception {
        Path tempParent = args.length
            == 0 ? new File(System.getProperty("user.dir"), "build/dgrs-media-probes").getAbsoluteFile()
                .toPath()
                : new File(args[0]).getAbsoluteFile()
                    .toPath();
        Files.createDirectories(tempParent);
        Path root = Files.createTempDirectory(tempParent, "dgrs-wpf-");
        try {
            byte[] png = png();
            String ref = "media/" + hex(
                MessageDigest.getInstance("SHA-256")
                    .digest(png))
                + ".png";
            File archive = root.resolve("demo.dgrs")
                .toFile();
            try (OutputStream output = Files.newOutputStream(archive.toPath());
                ZipOutputStream zip = new ZipOutputStream(output)) {
                put(zip, "manifest.json", manifest().getBytes("UTF-8"));
                put(zip, "project.json", "{}".getBytes("UTF-8"));
                put(zip, "stories/story.json", "{}".getBytes("UTF-8"));
                put(zip, ref, png);
            }
            DgrsArchiveReader reader = DgrsArchiveReader.open(archive);
            require(reader.getEntrySize(ref) == png.length, "metadata size missing");
            require(reader.readBytes(ref).length == png.length, "bounded read failed");
            DgrsGenerationStore store = new DgrsGenerationStore(root.resolve(".dgrs-runtime"));
            StoryPackageManifest parsed = StoryPackageManifest
                .read(reader.readBytes("manifest.json"), "probe!/manifest.json");
            String fingerprint = StoryPackageContentFingerprint.compute(parsed, reader);
            DgrsGenerationStore.Generation generation = store.install(archive, fingerprint);
            Path media = store.materialize(ref, reader);
            require(Files.size(media) == png.length, "materialized size mismatch");
            require(java.util.Arrays.equals(png, Files.readAllBytes(media)), "materialized content mismatch");
            ExecutorService workers = Executors.newFixedThreadPool(4);
            try {
                Future<Path>[] futures = new Future[8];
                for (int i = 0; i < futures.length; i++)
                    futures[i] = workers.submit(new java.util.concurrent.Callable<Path>() {

                        @Override
                        public Path call() throws Exception {
                            return store.materialize(ref, reader);
                        }
                    });
                for (Future<Path> future : futures)
                    require(media.equals(future.get()), "concurrent materialization diverged");
            } finally {
                workers.shutdownNow();
            }
            require(generation.retain(), "generation lease not retained");
            final CountDownLatch held = new CountDownLatch(1);
            final CountDownLatch finish = new CountDownLatch(1);
            Thread transfer = new Thread(new Runnable() {

                @Override
                public void run() {
                    try {
                        require(generation.retain(), "transfer lease not retained");
                        held.countDown();
                        finish.await();
                        generation.release();
                    } catch (Exception exception) {
                        throw new RuntimeException(exception);
                    }
                }
            });
            transfer.start();
            held.await();
            generation.retire();
            require(Files.exists(generation.getArchive()), "retired generation removed during transfer");
            finish.countDown();
            transfer.join();
            generation.release();
            require(!Files.exists(generation.getArchive()), "retired generation was not reclaimed");
            Runtime runtime = Runtime.getRuntime();
            Path largeArchive = root.resolve("large.dgrs");
            writeLargeArchive(largeArchive, 8 * 1024 * 1024);
            System.gc();
            long before = runtime.totalMemory() - runtime.freeMemory();
            for (int i = 0; i < 3; i++) DgrsArchiveReader.open(largeArchive.toFile());
            System.gc();
            long after = runtime.totalMemory() - runtime.freeMemory();
            System.out.println("DGRS_MEDIA_STREAMING_HEAP_DELTA_BYTES=" + (after - before));
            System.out.println("DGRS_MEDIA_STREAMING_MEASURED_ARCHIVE_BYTES=8388608");
            System.out.println("DGRS_MEDIA_STREAMING_CONCURRENT_MATERIALIZE=PASS");
            System.out.println("DGRS_MEDIA_STREAMING_RELOAD_TRANSFER_LEASE=PASS");
            System.out.println("DGRS_MEDIA_STREAMING_0331=PASS");
        } finally {
            delete(root);
        }
    }

    private static byte[] png() throws Exception {
        ByteArrayOutputStream output = new ByteArrayOutputStream();
        output.write(new byte[] { (byte) 137, 80, 78, 71, 13, 10, 26, 10 });
        chunk(output, "IHDR", new byte[] { 0, 0, 0, 1, 0, 0, 0, 1, 8, 6, 0, 0, 0 });
        chunk(output, "IDAT", new byte[] { 0, 0, 0, 0 });
        chunk(output, "IEND", new byte[0]);
        return output.toByteArray();
    }

    private static void chunk(ByteArrayOutputStream output, String type, byte[] data) throws Exception {
        writeInt(output, data.length);
        byte[] name = type.getBytes("US-ASCII");
        output.write(name);
        output.write(data);
        CRC32 crc = new CRC32();
        crc.update(name);
        crc.update(data);
        writeInt(output, (int) crc.getValue());
    }

    private static void writeInt(ByteArrayOutputStream output, int value) {
        output.write(value >>> 24);
        output.write(value >>> 16);
        output.write(value >>> 8);
        output.write(value);
    }

    private static void put(ZipOutputStream zip, String name, byte[] data) throws Exception {
        zip.putNextEntry(new ZipEntry(name));
        zip.write(data);
        zip.closeEntry();
    }

    private static void writeLargeArchive(Path path, int bytes) throws Exception {
        try (OutputStream output = Files.newOutputStream(path); ZipOutputStream zip = new ZipOutputStream(output)) {
            put(zip, "manifest.json", "{}".getBytes("UTF-8"));
            put(zip, "project.json", "{}".getBytes("UTF-8"));
            zip.putNextEntry(new ZipEntry("content/large.bin"));
            byte[] buffer = new byte[32768];
            int left = bytes;
            while (left > 0) {
                int count = Math.min(left, buffer.length);
                zip.write(buffer, 0, count);
                left -= count;
            }
            zip.closeEntry();
        }
    }

    private static String manifest() {
        return "{\"format\":\"dgrs\",\"format_version\":1,\"producer\":\"DarkGreyRPGStudio\",\"producer_version\":\"probe\",\"schema_version\":1,\"package_id\":\"demo\",\"package_version\":\"1\",\"story_id\":\"story\",\"story_schema_version\":1,\"required_resources\":{\"story\":\"stories/story.json\",\"actors\":[],\"items\":[],\"item_groups\":[],\"dialogues\":[],\"quests\":[],\"canonical_stories\":[],\"canonical_memberships\":[],\"sessions\":[],\"tasks\":[],\"media\":[]}}";
    }

    private static String hex(byte[] values) {
        StringBuilder text = new StringBuilder();
        for (byte value : values) {
            text.append(String.format("%02x", value & 255));
        }
        return text.toString();
    }

    private static void require(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }

    private static void delete(Path path) throws Exception {
        if (!Files.exists(path)) return;
        if (Files.isDirectory(path)) try (DirectoryStream<Path> entries = Files.newDirectoryStream(path)) {
            for (Path child : entries) delete(child);
        }
        Files.deleteIfExists(path);
    }
}
