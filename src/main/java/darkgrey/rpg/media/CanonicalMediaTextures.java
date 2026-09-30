package darkgrey.rpg.media;

import java.awt.image.BufferedImage;
import java.io.IOException;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.HashMap;
import java.util.HashSet;
import java.util.Iterator;
import java.util.Map;
import java.util.Set;
import java.util.concurrent.ArrayBlockingQueue;
import java.util.concurrent.ExecutorService;
import java.util.concurrent.RejectedExecutionException;
import java.util.concurrent.ThreadFactory;
import java.util.concurrent.ThreadPoolExecutor;
import java.util.concurrent.TimeUnit;

import javax.imageio.ImageIO;
import javax.imageio.ImageReader;
import javax.imageio.stream.ImageInputStream;

import net.minecraft.client.Minecraft;
import net.minecraft.client.renderer.texture.DynamicTexture;
import net.minecraft.util.ResourceLocation;

/** Decodes off-thread; creates/releases GPU textures only on the client thread. */
public final class CanonicalMediaTextures {

    private static final Map<String, ResourceLocation> READY = new HashMap<String, ResourceLocation>();
    private static final Map<String, Double> ASPECTS = new HashMap<String, Double>();
    private static final Map<String, Long> LAST_USED = new HashMap<String, Long>();
    private static final Map<String, Long> PIXELS = new HashMap<String, Long>();
    private static final Set<String> PENDING = new HashSet<String>();
    private static final Set<String> FAILED = new HashSet<String>();
    private static final ExecutorService DECODER = new ThreadPoolExecutor(
        1,
        1,
        0L,
        TimeUnit.MILLISECONDS,
        new ArrayBlockingQueue<Runnable>(16),
        new ThreadFactory() {

            @Override
            public Thread newThread(Runnable task) {
                Thread thread = new Thread(task, "DGR-image-decode");
                thread.setDaemon(true);
                return thread;
            }
        });
    private static volatile long generation;
    private static long uploadTick;
    private static volatile Set<String> demand = Collections.emptySet();
    private static final MediaUploadBudget BUDGET = new MediaUploadBudget();
    private static final java.util.concurrent.ConcurrentLinkedQueue<Decoded> UPLOADS = new java.util.concurrent.ConcurrentLinkedQueue<Decoded>();

    public static void updateDemand(Set<String> refs) {
        demand = Collections.unmodifiableSet(new HashSet<String>(refs));
    }

    private static int decodeLimit = 2048;
    private static final int MAX_WARM_TEXTURES = 24;
    private static final long MAX_WARM_PIXELS = 16777216L;
    private static final long WARM_TEXTURE_NANOS = 30L * 1000000000L;

    static int decodeLimitForCount(int count) {
        return Math.min(2048, (int) Math.sqrt(16777216.0 / Math.max(1, count)));
    }

    public static void configureImageCount(int count) {
        int next = decodeLimitForCount(count);
        // Existing textures are content-hash keyed and remain valid when the active
        // frame's aggregate decode limit changes. New decodes use the new limit.
        decodeLimit = next;
    }

    private CanonicalMediaTextures() {}

    public static ResourceLocation get(final String ref) {
        if (ref == null) return null;
        ResourceLocation existing = READY.get(ref);
        if (existing != null) {
            LAST_USED.put(ref, System.nanoTime());
            return existing;
        }
        final Path path = CanonicalMediaClient.ready(ref);
        if (path == null || PENDING.contains(ref) || FAILED.contains(ref)) return null;
        final int limit = decodeLimit;
        return enqueueDecode(ref, path, limit);
    }

    private static ResourceLocation enqueueDecode(final String ref, final Path path, final int limit) {
        final long epoch = generation;
        final long reserved = (long) limit * limit;
        if (!demand.contains(ref) || !BUDGET.reserve(reserved)) return null;
        final AutoCloseable lease = CanonicalMediaClient.readLease(ref);
        if (lease == null) {
            BUDGET.release(reserved);
            return null;
        }
        final long queued = System.nanoTime();
        try {
            PENDING.add(ref);
            DECODER.execute(() -> {
                BufferedImage image = null;
                boolean failed = false;
                try {
                    if (epoch == generation && demand.contains(ref)) {
                        MediaLatencyTrace.event("decode_queue", ref, queued, "begin");
                        long start = System.nanoTime();
                        image = decode(path, limit);
                        MediaLatencyTrace.event("decode", ref, start, "limit=" + limit);
                    }
                } catch (Exception failure) {
                    failed = true;
                    MediaLatencyTrace.event(
                        "decode_failed",
                        ref,
                        queued,
                        failure.getClass()
                            .getSimpleName());
                } finally {
                    try {
                        lease.close();
                    } catch (Exception ignored) {}
                }
                UPLOADS.add(new Decoded(ref, epoch, reserved, image, failed, queued));
            });
        } catch (RejectedExecutionException exception) {
            PENDING.remove(ref);
            BUDGET.release(reserved);
            try {
                lease.close();
            } catch (Exception ignored) {}
        }
        return null;
    }

    /** Called exactly once from ClientTick END, never from a frame/renderer lookup. */
    public static void tickUploads() {
        uploadTick++;
        Decoded value;
        while ((value = UPLOADS.poll()) != null) {
            boolean current = value.epoch == generation;
            if (current) PENDING.remove(value.ref);
            boolean upload = current && demand.contains(value.ref)
                && CanonicalMediaClient.ready(value.ref) != null
                && value.image != null;
            try {
                if (!upload) {
                    if (current && value.failed) FAILED.add(value.ref);
                    MediaLatencyTrace.event("decode_discard", value.ref, value.queued, "stale_or_failed");
                    continue;
                }
                long start = System.nanoTime();
                ResourceLocation texture = Minecraft.getMinecraft()
                    .getTextureManager()
                    .getDynamicTextureLocation(
                        "dgr_media_" + value.ref.substring(6, 70),
                        new DynamicTexture(value.image));
                READY.put(value.ref, texture);
                ASPECTS.put(value.ref, (double) value.image.getWidth() / value.image.getHeight());
                LAST_USED.put(value.ref, System.nanoTime());
                PIXELS.put(value.ref, (long) value.image.getWidth() * value.image.getHeight());
                MediaLatencyTrace
                    .event("upload", value.ref, start, "tick=" + uploadTick + " pending=" + BUDGET.count());
                MediaLatencyTrace.event("image_ready", value.ref, value.queued, "uploaded");
            } finally {
                if (value.image != null) value.image.flush();
                BUDGET.release(value.reserved);
            }
            if (upload) break;
        }
    }

    private static final class Decoded {

        final String ref;
        final long epoch, reserved, queued;
        final BufferedImage image;
        final boolean failed;

        Decoded(String ref, long epoch, long reserved, BufferedImage image, boolean failed, long queued) {
            this.ref = ref;
            this.epoch = epoch;
            this.reserved = reserved;
            this.image = image;
            this.failed = failed;
            this.queued = queued;
        }
    }

    public static double aspect(String ref) {
        Double value = ASPECTS.get(ref);
        return value == null ? 1.0 : value.doubleValue();
    }

    private static BufferedImage decode(Path path, int limit) throws IOException {
        try (ImageInputStream input = ImageIO.createImageInputStream(path.toFile())) {
            Iterator<ImageReader> readers = ImageIO.getImageReaders(input);
            if (!readers.hasNext()) throw new IOException("Unsupported image");
            ImageReader reader = readers.next();
            try {
                reader.setInput(input, true, true);
                int width = reader.getWidth(0);
                int height = reader.getHeight(0);
                if (width <= 0 || height <= 0 || (long) width * height > 33554432L)
                    throw new IOException("Image exceeds decode bounds");
                // Decoder subsampling bounds allocation before the GPU upload.
                javax.imageio.ImageReadParam param = reader.getDefaultReadParam();
                int sample = Math.max(1, (Math.max(width, height) + limit - 1) / limit);
                param.setSourceSubsampling(sample, sample, 0, 0);
                BufferedImage image = reader.read(0, param);
                return image;
            } finally {
                reader.dispose();
            }
        }
    }

    public static void releaseInactive() {
        long now = System.nanoTime();
        Iterator<Map.Entry<String, ResourceLocation>> iterator = READY.entrySet()
            .iterator();
        while (iterator.hasNext()) {
            Map.Entry<String, ResourceLocation> entry = iterator.next();
            Long used = LAST_USED.get(entry.getKey());
            if (CanonicalMediaClient.ready(entry.getKey()) == null
                && !CanonicalMediaClient.isWarmPortrait(entry.getKey())
                && used != null
                && now - used.longValue() >= WARM_TEXTURE_NANOS) remove(iterator, entry);
        }
        long pixels = 0L;
        for (String ref : READY.keySet()) {
            Long value = PIXELS.get(ref);
            if (value != null) pixels += value.longValue();
        }
        if (READY.size() <= MAX_WARM_TEXTURES && pixels <= MAX_WARM_PIXELS) return;
        ArrayList<String> inactive = new ArrayList<String>();
        for (String ref : READY.keySet()) if (CanonicalMediaClient.ready(ref) == null) inactive.add(ref);
        Collections.sort(inactive, new Comparator<String>() {

            @Override
            public int compare(String left, String right) {
                return Long.compare(LAST_USED.get(left), LAST_USED.get(right));
            }
        });
        for (String ref : inactive) {
            if (READY.size() <= MAX_WARM_TEXTURES && pixels <= MAX_WARM_PIXELS) break;
            ResourceLocation texture = READY.remove(ref);
            if (texture != null) {
                MediaLatencyTrace.event("texture_release", ref, 0, "capacity");
                Minecraft.getMinecraft()
                    .getTextureManager()
                    .deleteTexture(texture);
                ASPECTS.remove(ref);
                LAST_USED.remove(ref);
                Long value = PIXELS.remove(ref);
                if (value != null) pixels -= value.longValue();
            }
        }
    }

    private static void remove(Iterator<Map.Entry<String, ResourceLocation>> iterator,
        Map.Entry<String, ResourceLocation> entry) {
        MediaLatencyTrace.event("texture_release", entry.getKey(), 0, "idle_30s");
        Minecraft.getMinecraft()
            .getTextureManager()
            .deleteTexture(entry.getValue());
        ASPECTS.remove(entry.getKey());
        LAST_USED.remove(entry.getKey());
        PIXELS.remove(entry.getKey());
        iterator.remove();
    }

    public static void clear() {
        generation++;
        demand = Collections.emptySet();
        Decoded discarded;
        while ((discarded = UPLOADS.poll()) != null) {
            if (discarded.image != null) discarded.image.flush();
            BUDGET.release(discarded.reserved);
        }
        for (ResourceLocation texture : READY.values()) Minecraft.getMinecraft()
            .getTextureManager()
            .deleteTexture(texture);
        READY.clear();
        ASPECTS.clear();
        LAST_USED.clear();
        PIXELS.clear();
        PENDING.clear();
        FAILED.clear();
    }
}
