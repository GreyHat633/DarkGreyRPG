package darkgrey.rpg.project.packages;

import java.io.ByteArrayOutputStream;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.Arrays;
import java.util.Collections;
import java.util.Random;
import java.util.zip.ZipEntry;
import java.util.zip.ZipOutputStream;

import darkgrey.rpg.media.MediaTransferReaders;
import darkgrey.rpg.network.message.canonical.CanonicalMediaChunk;
import darkgrey.rpg.network.message.canonical.CanonicalMediaRequest;

/** Compares the previous production chunk path against the new leased reader path. */
public final class MediaLatency0334Probe {

    public static void main(String[] args) throws Exception {
        Path root = Paths.get(args[0]);
        Files.createDirectories(root);
        java.awt.image.BufferedImage image = new java.awt.image.BufferedImage(
            1024,
            1024,
            java.awt.image.BufferedImage.TYPE_INT_RGB);
        Random random = new Random(334);
        for (int y = 0; y < 1024; y++) for (int x = 0; x < 1024; x++) image.setRGB(x, y, random.nextInt());
        ByteArrayOutputStream encoded = new ByteArrayOutputStream();
        javax.imageio.ImageIO.write(image, "png", encoded);
        image.flush();
        byte[] bytes = encoded.toByteArray();
        StringBuilder hash = new StringBuilder();
        for (byte b : java.security.MessageDigest.getInstance("SHA-256")
            .digest(bytes)) hash.append(String.format("%02x", b & 255));
        String ref = "media/" + hash + ".png";
        Path archive = root.resolve("latency.dgrs");
        byte[] metadata = manifest(ref).getBytes("UTF-8");
        try (ZipOutputStream zip = new ZipOutputStream(Files.newOutputStream(archive))) {
            put(zip, "manifest.json", metadata);
            put(zip, "project.json", "{}".getBytes("UTF-8"));
            put(zip, "stories/story.json", "{}".getBytes("UTF-8"));
            put(zip, ref, bytes);
        }
        StoryPackageManifest manifest = StoryPackageManifest.read(metadata, "latency");
        DgrsGenerationStore store = new DgrsGenerationStore(root.resolve("Runtime"));
        String fingerprint = StoryPackageContentFingerprint.compute(manifest, DgrsArchiveReader.open(archive.toFile()));
        DgrsGenerationStore.Generation generation = store.install(archive.toFile(), fingerprint);
        LoadedStoryPackage source = new LoadedStoryPackage(
            manifest,
            archive.toFile(),
            store,
            generation,
            null,
            null,
            Collections.<String, byte[]>emptyMap(),
            fingerprint);
        source.readMediaChunk(1, ref, 0); // Materialize once; both measured paths use warm disk.
        long start = System.nanoTime();
        int chunks = 0;
        for (int offset = 0; offset < bytes.length; offset += 32768) {
            check(source.readMediaChunk(2, ref, offset) != null, "baseline chunk");
            chunks++;
        }
        long oldNanos = System.nanoTime() - start;
        MediaTransferReaders readers = new MediaTransferReaders();
        start = System.nanoTime();
        ByteArrayOutputStream received = new ByteArrayOutputStream();
        for (int offset = 0; offset < bytes.length; offset += 32768) {
            CanonicalMediaChunk chunk = readers.read("probe", source, new CanonicalMediaRequest(3, ref, offset));
            check(chunk != null, "leased chunk");
            received.write(chunk.getData());
        }
        long newNanos = System.nanoTime() - start;
        check(Arrays.equals(bytes, received.toByteArray()), "payload identity");
        check(readers.activeCount() == 0, "completed reader leaked");
        readers.read("reorder", source, new CanonicalMediaRequest(8, ref, (chunks - 1) * 32768));
        check(readers.activeCount() == 1, "last chunk prematurely closed reordered reader");
        for (int offset = 0; offset < (chunks - 1) * 32768; offset += 32768)
            readers.read("reorder", source, new CanonicalMediaRequest(8, ref, offset));
        check(readers.activeCount() == 0, "reordered transfer leaked");
        readers.read("cancel", source, new CanonicalMediaRequest(4, ref, 0));
        check(readers.activeCount() == 1, "reader missing");
        readers.releaseOwner("cancel");
        check(readers.activeCount() == 0, "cancel reader leaked");
        java.util.concurrent.atomic.AtomicLong clock = new java.util.concurrent.atomic.AtomicLong();
        MediaTransferReaders timed = new MediaTransferReaders(clock::get);
        timed.read("timeout", source, new CanonicalMediaRequest(6, ref, 0));
        clock.set(16_000_000_000L);
        timed.expire();
        check(timed.activeCount() == 0, "idle timeout leaked");
        for (int i = 0; i < 64; i++) check(
            timed.read("bounded", source, new CanonicalMediaRequest(100 + i, ref, 0)) != null,
            "bounded admission");
        check(timed.read("overflow", source, new CanonicalMediaRequest(200, ref, 0)) == null, "reader bound exceeded");
        timed.releaseOwner("bounded");
        check(timed.activeCount() == 0, "bounded cleanup leaked");
        readers.read("old", source, new CanonicalMediaRequest(5, ref, 0));
        source.close();
        check(Files.exists(generation.getArchive()), "generation deleted while reader leased");
        readers.releaseOwner("old");
        check(!Files.exists(generation.getArchive()), "retired generation leaked");
        System.out.println(
            "BYTES=" + bytes.length
                + " CHUNKS="
                + chunks
                + " BASELINE_HASH_BYTES="
                + ((long) bytes.length * chunks)
                + " FIXED_HASH_BYTES="
                + bytes.length);
        System.out.println("BASELINE_MS=" + oldNanos / 1e6 + " FIXED_MS=" + newNanos / 1e6);
        System.out.println("MEDIA_LATENCY_TRANSFER_PAYLOAD_LEASE_CANCEL=PASS");
    }

    private static void check(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }

    private static void put(ZipOutputStream zip, String name, byte[] data) throws Exception {
        zip.putNextEntry(new ZipEntry(name));
        zip.write(data);
        zip.closeEntry();
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

}
