package darkgrey.rpg.media;

import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.security.MessageDigest;
import java.util.Arrays;

import darkgrey.rpg.network.message.canonical.CanonicalMediaChunk;
import darkgrey.rpg.network.message.canonical.CanonicalMediaRequest;
import io.netty.buffer.ByteBuf;
import io.netty.buffer.Unpooled;

public final class MediaTransfer0330Probe {

    private MediaTransfer0330Probe() {}

    public static void main(String[] args) throws Exception {
        for (int count = 1; count <= 33; count++) {
            int limit = CanonicalMediaTextures.decodeLimitForCount(count);
            require((long) limit * limit * count <= 16777216, "aggregate decoded texture budget");
        }
        java.awt.image.BufferedImage fixture = new java.awt.image.BufferedImage(
            128,
            180,
            java.awt.image.BufferedImage.TYPE_INT_RGB);
        java.util.Random random = new java.util.Random(331L);
        for (int y = 0; y < fixture.getHeight(); y++)
            for (int x = 0; x < fixture.getWidth(); x++) fixture.setRGB(x, y, random.nextInt());
        java.io.ByteArrayOutputStream png = new java.io.ByteArrayOutputStream();
        javax.imageio.ImageIO.write(fixture, "png", png);
        byte[] content = png.toByteArray();
        require(content.length > 65536 && content.length <= 98304, "legal three-chunk media fixture");
        StringBuilder hash = new StringBuilder();
        for (byte value : MessageDigest.getInstance("SHA-256")
            .digest(content)) {
            hash.append(Character.forDigit((value & 255) >> 4, 16));
            hash.append(Character.forDigit(value & 15, 16));
        }
        String ref = "media/" + hash + ".png";
        ByteBuf encoded = Unpooled.buffer();
        new CanonicalMediaRequest(9, ref, 32768).toBytes(encoded);
        CanonicalMediaRequest decoded = new CanonicalMediaRequest();
        decoded.fromBytes(encoded);
        require(
            decoded.getOffset() == 32768 && decoded.getMediaRef()
                .equals(ref),
            "request round-trip");
        encoded.release();
        Path root = Paths.get(".tooling/0.3.3.0/p6/cache-probe");
        Files.createDirectories(root);
        VerifiedMediaCache cache = new VerifiedMediaCache(Files.createTempDirectory(root, "cache-"));
        try (VerifiedMediaCache.Download download = cache.begin(9, ref, content.length)) {
            require(!download.accept(chunk(9, ref, content, 32768)), "out-of-order chunk remains pending");
            require(!download.accept(chunk(9, ref, content, 32768)), "identical duplicate is idempotent");
            require(!download.accept(chunk(9, ref, content, 0)), "missing final chunk remains pending");
            require(download.accept(chunk(9, ref, content, 65536)), "complete verified download commits");
        }
        require(cache.available(ref), "committed hash is valid");
        try (VerifiedMediaCache.Download active = cache.begin(100, ref, content.length)) {
            Path abandoned = Files.createTempFile(
                cache.path(ref)
                    .getParent(),
                ".media-",
                ".part");
            cache.recoverInterruptedDownloads();
            require(!Files.exists(abandoned), "startup removes abandoned partial file");
            active.accept(chunk(100, ref, content, 0));
        }
        byte[] ogg = Files.readAllBytes(Paths.get(".tooling/0.3.3.0/p9/payloads/audio.ogg"));
        MediaPayloadValidation.validate("test.ogg", ogg);
        ogg[ogg.length - 1] ^= 1;
        try {
            MediaPayloadValidation.validate("test.ogg", ogg);
            throw new AssertionError("corrupt OGG accepted");
        } catch (IllegalArgumentException expected) {}

        byte[] damaged = content.clone();
        damaged[0] ^= 1;
        try (VerifiedMediaCache.Download download = cache.begin(10, ref, damaged.length)) {
            download.accept(chunk(10, ref, damaged, 0));
            download.accept(chunk(10, ref, damaged, 32768));
            try {
                download.accept(chunk(10, ref, damaged, 65536));
                throw new AssertionError("accepted corrupt download");
            } catch (java.io.IOException expected) {}
        }
        require(cache.available(ref), "failed replacement preserves verified cache");
        try {
            new CanonicalMediaRequest(1, "../../outside.ogg", 0);
            throw new AssertionError("accepted traversal");
        } catch (IllegalArgumentException expected) {}
        try {
            new CanonicalMediaChunk(1, ref, Integer.MAX_VALUE, 0, new byte[1]);
            throw new AssertionError("accepted oversized transfer");
        } catch (IllegalArgumentException expected) {}
        MediaCacheRetention retention = new MediaCacheRetention(cache, 0, 0, 0);
        require(cache.pin(ref), "pin current playback");
        retention.touch(ref, MediaCacheRetention.Kind.VOICE);
        retention.sweep();
        require(cache.available(ref), "active cache survives zero budget sweep");
        cache.unpin(ref);
        retention.sweep();
        require(!cache.available(ref), "unused cache is removed at budget boundary");
        System.out.println("MEDIA_CACHE_ACTIVE_PIN_RETENTION=PASS");
        System.out.println("MEDIA_TRANSFER_CODEC_BOUNDS_OUT_OF_ORDER_DUPLICATE_INTEGRITY=PASS");
    }

    private static CanonicalMediaChunk chunk(long id, String ref, byte[] content, int offset) {
        CanonicalMediaChunk original = new CanonicalMediaChunk(
            id,
            ref,
            content.length,
            offset,
            Arrays.copyOfRange(content, offset, Math.min(content.length, offset + CanonicalMediaChunk.CHUNK_BYTES)));
        ByteBuf bytes = Unpooled.buffer();
        original.toBytes(bytes);
        CanonicalMediaChunk copy = new CanonicalMediaChunk();
        copy.fromBytes(bytes);
        bytes.release();
        return copy;
    }

    private static void require(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }
}
