package darkgrey.rpg.media;

import java.awt.image.BufferedImage;
import java.io.IOException;
import java.nio.file.Path;
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

import darkgrey.rpg.network.MainThreadScheduler;

/** Decodes off-thread; creates/releases GPU textures only on the client thread. */
public final class CanonicalMediaTextures {

    private static final Map<String, ResourceLocation> READY = new HashMap<String, ResourceLocation>();
    private static final Map<String, Double> ASPECTS = new HashMap<String, Double>();
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
    private static long generation;
    private static int decodeLimit = 2048;

    static int decodeLimitForCount(int count) {
        return Math.min(2048, (int) Math.sqrt(16777216.0 / Math.max(1, count)));
    }

    public static void configureImageCount(int count) {
        int next = decodeLimitForCount(count);
        if (next != decodeLimit) {
            clear();
            decodeLimit = next;
        }
    }

    private CanonicalMediaTextures() {}

    public static ResourceLocation get(final String ref) {
        if (ref == null) return null;
        ResourceLocation existing = READY.get(ref);
        if (existing != null) return existing;
        final Path path = CanonicalMediaClient.ready(ref);
        if (path == null || PENDING.contains(ref) || FAILED.contains(ref)) return null;
        final long epoch = generation;
        final int limit = decodeLimit;
        PENDING.add(ref);
        try {
            DECODER.execute(new Runnable() {

                @Override
                public void run() {
                    BufferedImage decoded = null;
                    try {
                        decoded = decode(path, limit);
                    } catch (Exception ignored) {}
                    final BufferedImage image = decoded;
                    MainThreadScheduler.scheduleClient(new Runnable() {

                        @Override
                        public void run() {
                            if (epoch != generation) {
                                if (image != null) image.flush();
                                return;
                            }
                            PENDING.remove(ref);
                            if (image == null) {
                                FAILED.add(ref);
                                return;
                            }
                            if (CanonicalMediaClient.ready(ref) == null) {
                                image.flush();
                                return;
                            }
                            ResourceLocation texture = Minecraft.getMinecraft()
                                .getTextureManager()
                                .getDynamicTextureLocation(
                                    "dgr_media_" + ref.substring(6, 70),
                                    new DynamicTexture(image));
                            READY.put(ref, texture);
                            ASPECTS.put(ref, (double) image.getWidth() / image.getHeight());
                            image.flush();
                        }
                    });
                }
            });
        } catch (RejectedExecutionException exception) {
            PENDING.remove(ref);
        }
        return null;
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
        Iterator<Map.Entry<String, ResourceLocation>> iterator = READY.entrySet()
            .iterator();
        while (iterator.hasNext()) {
            Map.Entry<String, ResourceLocation> entry = iterator.next();
            if (CanonicalMediaClient.ready(entry.getKey()) == null) {
                Minecraft.getMinecraft()
                    .getTextureManager()
                    .deleteTexture(entry.getValue());
                ASPECTS.remove(entry.getKey());
                iterator.remove();
            }
        }
    }

    public static void clear() {
        generation++;
        for (ResourceLocation texture : READY.values()) Minecraft.getMinecraft()
            .getTextureManager()
            .deleteTexture(texture);
        READY.clear();
        ASPECTS.clear();
        PENDING.clear();
        FAILED.clear();
    }
}
