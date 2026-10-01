package darkgrey.rpg.gramophone;

import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.Collections;

import io.netty.buffer.ByteBuf;
import io.netty.buffer.Unpooled;

/** Exercises real file commits, recovery, reference protection, decoder, and hostile packet lengths. */
public final class GramophoneStorageProbe {

    private static void sharedPreviewLease(GramophoneMediaInfo info) throws Exception {
        Class<?> type = Class.forName("darkgrey.rpg.gramophone.GramophoneClient$Media");
        java.lang.reflect.Constructor<?> mediaConstructor = type.getDeclaredConstructor();
        mediaConstructor.setAccessible(true);
        Object media = mediaConstructor.newInstance();
        java.lang.reflect.Field pins = type.getDeclaredField("pins");
        pins.setAccessible(true);
        pins.setInt(media, 2);
        java.lang.reflect.Field futureField = type.getDeclaredField("future");
        futureField.setAccessible(true);
        java.util.concurrent.CompletableFuture<Path> transfer = new java.util.concurrent.CompletableFuture<Path>();
        futureField.set(media, transfer);
        java.lang.reflect.Constructor<?> constructor = GramophoneClient.PreviewLease.class
            .getDeclaredConstructor(String.class, type);
        constructor.setAccessible(true);
        GramophoneClient.PreviewLease first = (GramophoneClient.PreviewLease) constructor.newInstance("probe", media);
        GramophoneClient.PreviewLease second = (GramophoneClient.PreviewLease) constructor.newInstance("probe", media);
        require(first.ready() == null, "inflight media stays asynchronous");
        first.close();
        first.close();
        require(pins.getInt(media) == 1 && !transfer.isCancelled(), "closing preview preserves another consumer");
        java.lang.reflect.Field metadata = type.getDeclaredField("preview");
        metadata.setAccessible(true);
        metadata.set(media, info);
        transfer.complete(info.path);
        require(second.ready() == info && second.ready() == info, "shared metadata reused without reinspection");
        second.close();
        require(pins.getInt(media) == 0 && Files.isRegularFile(info.path), "idle completed media survives close");
        Object pending = mediaConstructor.newInstance();
        pins.setInt(pending, 1);
        java.util.concurrent.CompletableFuture<Path> abandoned = new java.util.concurrent.CompletableFuture<Path>();
        futureField.set(pending, abandoned);
        GramophoneClient.PreviewLease last = (GramophoneClient.PreviewLease) constructor
            .newInstance("pending", pending);
        last.close();
        require(abandoned.isCancelled(), "last consumer cancels abandoned transfer");
        Object failed = mediaConstructor.newInstance();
        pins.setInt(failed, 1);
        java.util.concurrent.CompletableFuture<Path> unavailable = new java.util.concurrent.CompletableFuture<Path>();
        unavailable.completeExceptionally(new java.io.IOException("temporary source failure"));
        futureField.set(failed, unavailable);
        GramophoneClient.PreviewLease failure = (GramophoneClient.PreviewLease) constructor
            .newInstance("failed", failed);
        try {
            failure.ready();
            throw new AssertionError("Expected source failure");
        } catch (java.io.IOException expected) {
            require(
                expected.getMessage()
                    .contains("temporary source failure"),
                "preserve cause");
        }
        failure.close();
        java.lang.reflect.Field discarded = type.getDeclaredField("discarded");
        discarded.setAccessible(true);
        require(discarded.getBoolean(failed), "failed idle cache is discarded so reopening can retry");
        System.out.println("GRAMOPHONE_SHARED_PREVIEW_LEASE=PASS");
    }

    private static void require(boolean value, String label) {
        if (!value) throw new AssertionError(label);
    }

    static void run() throws Exception {
        Path base = Paths.get(".tooling/0332-gramophone/storage-probe")
            .toAbsolutePath();
        Files.createDirectories(base);
        Path run = Files.createTempDirectory(base, "run-");
        Path tone = run.resolve("tone.dgrmp3");
        try (java.io.InputStream input = GramophoneStorageProbe.class.getResourceAsStream("/gramophone/tone.mp3")) {
            require(input != null, "generated tone fixture present");
            Files.copy(input, tone);
        }
        GramophoneMediaInfo info = GramophoneMediaInfo.inspect(tone);
        sharedPreviewLease(info);
        GramophoneCachePolicyProbe.run(info);
        require(
            info.seconds > 1.9 && info.seconds < 2.3 && info.bytes > 0 && info.envelope.length == 256,
            "streaming media inspection");
        GramophoneAudio mono = new GramophoneAudio();
        mono.file(tone);
        mono.preview(0);
        GramophoneAudio.Cursor resampler = new GramophoneAudio.Cursor(mono, 0);
        int frames = 0, crossings = 0;
        float peak = 0;
        boolean negative = false;
        try {
            while (resampler.next()) {
                frames++;
                peak = Math.max(peak, Math.abs(resampler.left));
                // Ignore MP3 encoder padding/ringing near silence when measuring pitch.
                if (resampler.left < -.02f) negative = true;
                if (negative && resampler.left > .02f) {
                    crossings++;
                    negative = false;
                }
                require(resampler.left == resampler.right, "mono resamples to equal stereo channels");
            }
        } finally {
            resampler.close();
        }
        System.out.println("RESAMPLER frames=" + frames + " crossings=" + crossings + " peak=" + peak);
        require(
            frames > 96000 && frames < 110400 && crossings > 870 && crossings < 900 && peak > .08f && peak < .14f,
            "48k resampling preserves duration, 440Hz pitch, and PCM amplitude");
        System.out.println("GRAMOPHONE_SOFTWARE_RESAMPLER_PCM_PITCH=PASS");
        CodecGramophoneMp3 codec = new CodecGramophoneMp3();
        try {
            require(
                codec.initialize(
                    new java.net.URL(
                        tone.toUri()
                            .toURL() + "#1")),
                "preview seek initializes");
            long pcm = 0;
            paulscode.sound.SoundBuffer frame;
            javax.sound.sampled.AudioFormat format = codec.getAudioFormat();
            while ((frame = codec.read()) != null) pcm += frame.audioData.length;
            double duration = pcm / (format.getFrameSize() * (double) format.getFrameRate());
            require(duration > .8 && duration < 1.3 && !codec.failed(), "preview seek consumes only remainder");
        } finally {
            codec.cleanup();
        }
        GramophoneBlobStore store = new GramophoneBlobStore(run.resolve("Store"));
        Path temporary = store.temporary();
        Files.copy(tone, temporary, java.nio.file.StandardCopyOption.REPLACE_EXISTING);
        store.install(temporary, info.hash);
        GramophonePacket first = device(1, info.hash), second = device(2, info.hash);
        store.commit(first);
        store.commit(second);
        store = new GramophoneBlobStore(run.resolve("Store"));
        require(store.saved(first.key()).source.equals(first.source), "durable configuration recovery");
        store.remove(first.key());
        long now = System.currentTimeMillis();
        store.collect(Collections.<String>emptySet(), now + 172800000L);
        require(Files.exists(store.blob(info.hash)), "second persistent owner protects shared blob");
        store.remove(second.key());
        store.collect(Collections.<String>emptySet(), now);
        require(Files.exists(store.blob(info.hash)), "last unreference keeps grace period");
        store.collect(Collections.singleton(info.hash), now + 172800000L);
        require(Files.exists(store.blob(info.hash)), "temporary transfer pin protects blob");
        store.collect(Collections.<String>emptySet(), now + 172800000L);
        store.collect(Collections.<String>emptySet(), now + 345600001L);
        require(!Files.exists(store.blob(info.hash)), "unreferenced unpinned blob retired after grace");
        GramophoneMediaPacket packet = new GramophoneMediaPacket();
        packet.operation = GramophoneMediaPacket.CHUNK;
        packet.token = java.util.UUID.randomUUID()
            .toString();
        packet.hash = info.hash;
        packet.total = 20000;
        packet.data = new byte[16384];
        packet.device = first;
        ByteBuf buffer = Unpooled.buffer();
        try {
            packet.toBytes(buffer);
            GramophoneMediaPacket decoded = new GramophoneMediaPacket();
            ByteBuf copy = buffer.copy();
            try {
                decoded.fromBytes(copy);
            } finally {
                copy.release();
            }
            require(
                decoded.data.length == 16384 && decoded.device.key()
                    .equals(first.key()),
                "bounded packet round trip");
            buffer.writeByte(1);
            boolean rejected = false;
            try {
                new GramophoneMediaPacket().fromBytes(buffer);
            } catch (IllegalArgumentException expected) {
                rejected = true;
            }
            require(rejected, "trailing transfer bytes rejected");
        } finally {
            buffer.release();
        }
        Path corrupt = run.resolve("corrupt.dgrmp3");
        Files.write(corrupt, new byte[4096]);
        boolean rejected = false;
        try {
            GramophoneMediaInfo.inspect(corrupt);
        } catch (java.io.IOException expected) {
            rejected = true;
        }
        require(rejected, "corrupt local media rejected before commit");
        Path cache = run.resolve("Cache/Gramophone");
        Path context = cache.resolve(
            java.util.UUID.randomUUID()
                .toString());
        GramophoneFiles.activate(context);
        GramophoneFiles.directory(context);
        Path active = context.resolve("track-123.dgrmp3");
        Files.copy(tone, active);
        java.nio.file.attribute.FileTime old = java.nio.file.attribute.FileTime
            .fromMillis(System.currentTimeMillis() - 172800000L);
        Files.setLastModifiedTime(context, old);
        GramophoneFiles.reclaimCache(cache);
        Thread.sleep(2200);
        require(Files.exists(active), "live client lease protects old cache directory");
        GramophoneFiles.release(context);
        Thread.sleep(2200);
        Files.setLastModifiedTime(context, old);
        GramophoneFiles.reclaimCache(cache);
        Thread.sleep(2200);
        require(!Files.exists(active), "expired unowned crash file reclaimed");
        System.out.println("GRAMOPHONE_CACHE_LIVE_LEASE_CRASH_RECOVERY=PASS");
        System.out.println("GRAMOPHONE_LOCAL_CODEC_SEEK_STORAGE_REFERENCES_PACKET=PASS");
    }

    private static GramophonePacket device(int x, String hash) {
        GramophonePacket packet = new GramophonePacket();
        packet.x = x;
        packet.instance = java.util.UUID.randomUUID()
            .toString();
        packet.source = "local:" + hash;
        packet.revision = 1;
        return packet;
    }
}
