package darkgrey.rpg.gramophone;

import java.nio.file.Files;
import java.nio.file.Path;
import java.util.HashMap;
import java.util.HashSet;
import java.util.Iterator;
import java.util.Map;
import java.util.Set;
import java.util.concurrent.ArrayBlockingQueue;
import java.util.concurrent.Future;
import java.util.concurrent.ThreadPoolExecutor;
import java.util.concurrent.TimeUnit;

import net.minecraft.client.Minecraft;
import net.minecraftforge.client.event.sound.SoundLoadEvent;

import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import cpw.mods.fml.common.gameevent.TickEvent;

/** Independent context cache, bounded IO queue, and one personal player per device. */
public final class GramophoneClient {

    private static final Map<String, Device> DEVICES = new HashMap<String, Device>();
    private static final Map<String, Media> MEDIA = new HashMap<String, Media>();
    private static final Set<String> SEEN = new HashSet<String>();
    private static final java.util.List<Device> RETIRING = new java.util.ArrayList<Device>();
    private static final ThreadPoolExecutor IO = new ThreadPoolExecutor(
        2,
        2,
        30,
        TimeUnit.SECONDS,
        new ArrayBlockingQueue<Runnable>(32),
        task -> {
            Thread thread = new Thread(task, "DGR-Gramophone-IO");
            thread.setDaemon(true);
            return thread;
        });
    private static Object context;
    private static Path directory;
    private static int dimension;
    private static boolean snapshot;
    private static boolean cacheChecked;
    private static final long IDLE_BUDGET = 128L * 1024 * 1024;
    private static final Map<String, Boolean> RANGES = new HashMap<String, Boolean>();

    private static String rangePreference(GramophonePacket config) {
        String scope = darkgrey.rpg.client.session.PlayerReadingContext.current();
        if (scope == null) return null;
        try {
            byte[] digest = java.security.MessageDigest.getInstance("SHA-256")
                .digest(scope.getBytes(java.nio.charset.StandardCharsets.UTF_8));
            StringBuilder key = new StringBuilder("gramophone.range.");
            for (byte value : digest) key.append(String.format(java.util.Locale.ROOT, "%02x", value & 255));
            return key + "." + config.key();
        } catch (java.security.NoSuchAlgorithmException impossible) {
            throw new IllegalStateException(impossible);
        }
    }

    public static boolean rangeShown(GramophonePacket config) {
        Boolean cached = RANGES.get(config.key());
        if (cached != null) return cached;
        String key = rangePreference(config);
        boolean shown = key != null && "true".equals(
            darkgrey.rpg.client.gui.UtilityWindowChrome.settings()
                .preference(key));
        RANGES.put(config.key(), shown);
        return shown;
    }

    public static void showRange(GramophonePacket config, boolean shown) {
        RANGES.put(config.key(), shown);
        String key = rangePreference(config);
        if (key != null) darkgrey.rpg.client.gui.UtilityWindowChrome.settings()
            .savePreference(key, Boolean.toString(shown));
    }

    private static final class Media {

        Future<Path> future;
        Future<?> transfer;
        Path path;
        volatile boolean discarded;
        String error;
        long idle;
        int pins;
        long bytes;
        volatile GramophoneMediaInfo preview;
    }

    private static final class Device {

        GramophonePacket config;
        final GramophoneAudio audio = new GramophoneAudio();
        final GramophonePlayback playback = new GramophonePlayback(audio);
        Media media;

        Device(GramophonePacket config) {
            this.config = config;
        }

        void clear() {
            playback.clear();
            audio.resetFailure();
            if (media != null) {
                media.pins--;
                media.idle = System.nanoTime();
                if (media.pins == 0 && media.future != null && !media.future.isDone()) {
                    if (MEDIA.get(config.source) == media) MEDIA.remove(config.source);
                    discard(media);
                }
                media = null;
            }
        }
    }

    public static void accept(GramophonePacket packet) {
        Minecraft mc = Minecraft.getMinecraft();
        if (mc.theWorld == null || mc.thePlayer == null || packet.dimension != mc.thePlayer.dimension) return;
        ensureContext();
        if (packet.operation == GramophonePacket.REMOVED) {
            removeDevice(packet.key());
            if (mc.theWorld.blockExists(packet.x, packet.y, packet.z)) {
                net.minecraft.tileentity.TileEntity tile = mc.theWorld.getTileEntity(packet.x, packet.y, packet.z);
                if (tile instanceof TileGramophone && ((TileGramophone) tile).instance.equals(packet.instance))
                    ((TileGramophone) tile).rangeMetadataReady = false;
            }
            return;
        }
        if (packet.operation == GramophonePacket.OPEN) {
            mc.displayGuiScreen(new GuiGramophone(packet));
            return;
        }
        if (packet.operation == GramophonePacket.RESULT) {
            if (mc.currentScreen instanceof GuiGramophone) ((GuiGramophone) mc.currentScreen).result(packet);
            return;
        }
        if (packet.operation == GramophonePacket.BEGIN) {
            SEEN.clear();
            snapshot = true;
            return;
        }
        if (packet.operation == GramophonePacket.STATE) {
            if (snapshot) SEEN.add(packet.key());
            Device old = DEVICES.get(packet.key());
            if (old == null || !old.config.source.equals(packet.source)) {
                if (old != null) RETIRING.add(old);
                DEVICES.put(packet.key(), new Device(packet));
            } else old.config = packet;
        }
        if (packet.operation == GramophonePacket.END && snapshot) {
            snapshot = false;
            Iterator<Map.Entry<String, Device>> iterator = DEVICES.entrySet()
                .iterator();
            while (iterator.hasNext()) {
                Map.Entry<String, Device> entry = iterator.next();
                if (!SEEN.contains(entry.getKey())) {
                    RETIRING.add(entry.getValue());
                    iterator.remove();
                }
            }
        }
    }

    private static void removeDevice(String key) {
        Device device = DEVICES.remove(key);
        if (device != null) device.clear();
        Iterator<Device> retiring = RETIRING.iterator();
        while (retiring.hasNext()) {
            device = retiring.next();
            if (device.config.key()
                .equals(key)) {
                device.clear();
                retiring.remove();
            }
        }
        SEEN.remove(key);
        RANGES.remove(key);
    }

    private static Media request(GramophonePacket config) {
        String source = config.source;
        Media cached = MEDIA.get(source);
        if (cached != null && cached.pins == 0 && cached.error != null) {
            MEDIA.remove(source);
            discard(cached);
            cached = null;
        }
        if (cached != null) {
            cached.pins++;
            return cached;
        }
        final Media media = new Media();
        media.pins = 1;
        MEDIA.put(source, media);
        final Path target = directory;
        try {
            final Future<GramophoneMediaInfo> local = source.startsWith("local:")
                ? GramophoneLocalClient.download(config, target)
                : null;
            media.transfer = local;
            final OnlineMusicSource online = local == null ? OnlineMusicSource.parse(source) : null;
            media.future = IO.submit(() -> {
                Path path = null;
                try {
                    GramophoneMediaInfo info;
                    if (local == null) {
                        path = OnlineMusicResolver.download(online, target);
                        info = GramophoneMediaInfo.inspect(path);
                    } else {
                        // Transfer owns idle timeout/cancellation and has already validated/analyzed the file.
                        // A progressing large download must not fail at an unrelated total-duration cutoff.
                        info = local.get();
                        path = info.path;
                    }
                    synchronized (media) {
                        if (media.discarded) {
                            GramophoneFiles.retire(path);
                            throw new java.io.IOException("媒体上下文已关闭");
                        }
                        media.bytes = Files.size(path);
                        media.path = path;
                        media.preview = info;
                    }
                    return path;
                } catch (Exception exception) {
                    GramophoneFiles.retire(path);
                    throw exception;
                }
            });
        } catch (RuntimeException exception) {
            media.error = "媒体队列已满或来源无效，请稍后重新进入：" + exception.getMessage();
        }
        return media;
    }

    /** Main-thread lease: a preview shares the device's transfer and metadata, never its player. */
    static final class PreviewLease implements AutoCloseable {

        private final String key;
        private final Media media;
        private boolean closed;

        private PreviewLease(String key, Media media) {
            this.key = key;
            this.media = media;
        }

        GramophoneMediaInfo ready() throws Exception {
            if (closed || media.discarded) throw new java.io.IOException("媒体上下文已关闭");
            if (media.error != null) throw new java.io.IOException(media.error);
            if (media.future != null) {
                if (!media.future.isDone()) return null;
                try {
                    media.future.get();
                } catch (Exception exception) {
                    media.error = "音频当前不可用：" + (exception.getCause() == null ? exception.getMessage()
                        : exception.getCause()
                            .getMessage());
                    throw new java.io.IOException(media.error, exception);
                }
            }
            return media.preview;
        }

        @Override
        public void close() {
            if (closed) return;
            closed = true;
            media.pins--;
            media.idle = System.nanoTime();
            if (media.pins == 0 && (media.error != null || media.future != null && !media.future.isDone())) {
                if (MEDIA.get(key) == media) MEDIA.remove(key);
                discard(media);
            }
        }
    }

    static PreviewLease preview(GramophonePacket config) {
        ensureContext();
        return new PreviewLease(config.source, request(config));
    }

    private static void ensureContext() {
        Minecraft mc = Minecraft.getMinecraft();
        Object current = mc.theWorld;
        int currentDimension = mc.thePlayer == null ? 0 : mc.thePlayer.dimension;
        if (context == current && dimension == currentDimension) return;
        reset();
        context = current;
        dimension = currentDimension;
        if (current != null) {
            Path root = mc.mcDataDir.toPath()
                .resolve("DarkGreyRPG/Cache/Gramophone");
            directory = root.resolve(
                java.util.UUID.randomUUID()
                    .toString());
            GramophoneFiles.activate(directory);
            if (!cacheChecked) {
                cacheChecked = true;
                IO.execute(() -> {
                    try {
                        GramophoneFiles.reclaimCache(root);
                    } catch (java.io.IOException exception) {
                        darkgrey.rpg.DarkGreyRpg.LOG.warn("Gramophone cache recovery failed", exception);
                    }
                });
            }
        }
    }

    private static void discard(Media media) {
        synchronized (media) {
            media.discarded = true;
            if (media.future != null) media.future.cancel(true);
            if (media.transfer != null) media.transfer.cancel(true);
            if (media.path != null) {
                GramophoneFiles.retire(media.path);
            }
        }
    }

    private static void reset() {
        GramophoneFiles.release(directory);
        directory = null;
        for (Device device : DEVICES.values()) device.clear();
        DEVICES.clear();
        RANGES.clear();
        SEEN.clear();
        snapshot = false;
        for (Device device : RETIRING) device.clear();
        RETIRING.clear();
        for (Media media : MEDIA.values()) discard(media);
        MEDIA.clear();
    }

    public static String status(String key) {
        Device device = DEVICES.get(key);
        if (device == null || device.media == null) return "未在播放（检查范围、开关与红石）";
        if (device.media.error != null) return device.media.error;
        if (device.audio.failed()) return device.audio.failure();
        return device.media.path == null ? "正在解析并缓冲音频…"
            : device.playback.paused() ? "红石暂停" : device.audio.playing() ? "正在播放" : "音频已就绪，等待播放条件";
    }

    @SubscribeEvent
    public void soundReload(SoundLoadEvent event) {
        reset();
        context = null;
    }

    /** Uses the same camera-relative block origin as vanilla tile entities. */
    static void renderRange(TileGramophone tile, double x, double y, double z) {
        Minecraft mc = Minecraft.getMinecraft();
        if (mc.theWorld == null || mc.thePlayer == null || !GramophoneServer.allowed(mc.thePlayer)) return;
        GuiGramophone editor = mc.currentScreen instanceof GuiGramophone ? (GuiGramophone) mc.currentScreen : null;
        if (!tile.rangeMetadataReady || tile.isInvalid()
            || tile.getWorldObj() != mc.theWorld
            || !mc.theWorld.blockExists(tile.xCoord, tile.yCoord, tile.zCoord)
            || !(mc.theWorld.getBlock(tile.xCoord, tile.yCoord, tile.zCoord) instanceof BlockGramophone)
            || mc.theWorld.getTileEntity(tile.xCoord, tile.yCoord, tile.zCoord) != tile) return;
        GramophonePacket c = tile.rangeSnapshot();
        double[] bounds;
        if (editor != null && editor.deviceKey()
            .equals(c.key())) {
            bounds = editor.previewBounds();
            if (bounds == null) return;
            bounds[0] -= tile.xCoord;
            bounds[3] -= tile.xCoord;
            bounds[1] -= tile.yCoord;
            bounds[4] -= tile.yCoord;
            bounds[2] -= tile.zCoord;
            bounds[5] -= tile.zCoord;
        } else {
            if (!rangeShown(c)) return;
            int r = c.radius;
            bounds = new double[] { -r, -r, -r, r + 1, r + 1, r + 1 };
        }
        drawRange(x, y, z, bounds);
    }

    static Path cacheDirectory() {
        ensureContext();
        return directory;
    }

    static boolean isCacheContext(Path expected) {
        return expected != null && expected.equals(directory);
    }

    static GramophoneMediaInfo retainPreview(String key, GramophoneMediaInfo info) {
        Media media = MEDIA.get(key);
        if (media != null && media.preview != null && !media.discarded && Files.isRegularFile(media.preview.path)) {
            media.pins++;
            if (!media.preview.path.equals(info.path)) GramophoneFiles.retire(info.path);
            return media.preview;
        }
        if (media != null) discard(media);
        // Keep a currently playing device's file and store preview metadata under its own fingerprint key.
        media = new Media();
        media.path = info.path;
        media.bytes = info.bytes;
        media.preview = info;
        media.pins = 1;
        MEDIA.put(key, media);
        return info;
    }

    static void releasePreview(String key, GramophoneMediaInfo info) {
        Media media = MEDIA.get(key);
        if (media != null && media.preview == info && media.pins > 0) {
            media.pins--;
            media.idle = System.nanoTime();
        }
    }

    private static void drawRange(double originX, double originY, double originZ, double[] b) {
        int matrixMode = org.lwjgl.opengl.GL11.glGetInteger(org.lwjgl.opengl.GL11.GL_MATRIX_MODE);
        org.lwjgl.opengl.GL11.glPushAttrib(org.lwjgl.opengl.GL11.GL_ALL_ATTRIB_BITS);
        org.lwjgl.opengl.GL11.glMatrixMode(org.lwjgl.opengl.GL11.GL_MODELVIEW);
        org.lwjgl.opengl.GL11.glPushMatrix();
        try {
            org.lwjgl.opengl.GL11.glTranslated(originX, originY, originZ);
            net.minecraft.client.renderer.OpenGlHelper
                .setActiveTexture(net.minecraft.client.renderer.OpenGlHelper.lightmapTexUnit);
            org.lwjgl.opengl.GL11.glDisable(org.lwjgl.opengl.GL11.GL_TEXTURE_2D);
            net.minecraft.client.renderer.OpenGlHelper
                .setActiveTexture(net.minecraft.client.renderer.OpenGlHelper.defaultTexUnit);
            org.lwjgl.opengl.GL11.glDisable(org.lwjgl.opengl.GL11.GL_TEXTURE_2D);
            org.lwjgl.opengl.GL11.glDisable(org.lwjgl.opengl.GL11.GL_LIGHTING);
            org.lwjgl.opengl.GL11.glEnable(org.lwjgl.opengl.GL11.GL_DEPTH_TEST);
            org.lwjgl.opengl.GL11.glDepthMask(false);
            org.lwjgl.opengl.GL11.glColor4f(1, .76f, .16f, 1);
            org.lwjgl.opengl.GL11.glLineWidth(2);
            org.lwjgl.opengl.GL11.glBegin(org.lwjgl.opengl.GL11.GL_LINES);
            for (int axis = 0; axis < 3; axis++) for (int i = 0; i < 4; i++) {
                double[] a = { b[0], b[1], b[2] };
                int one = (axis + 1) % 3, two = (axis + 2) % 3;
                a[one] = b[one + ((i & 1) == 0 ? 0 : 3)];
                a[two] = b[two + ((i & 2) == 0 ? 0 : 3)];
                org.lwjgl.opengl.GL11.glVertex3d(a[0], a[1], a[2]);
                a[axis] = b[axis + 3];
                org.lwjgl.opengl.GL11.glVertex3d(a[0], a[1], a[2]);
            }
            org.lwjgl.opengl.GL11.glEnd();
            // Twelve sparse moving sparks, evaluated only while this live tile is rendered.
            // No particle entities, queued emissions or lifetime independent of the device.
            double phase = (System.nanoTime() / 1000000000.0) * .18;
            org.lwjgl.opengl.GL11.glPointSize(4);
            org.lwjgl.opengl.GL11.glColor4f(1, .94f, .55f, 1);
            org.lwjgl.opengl.GL11.glBegin(org.lwjgl.opengl.GL11.GL_POINTS);
            for (int axis = 0; axis < 3; axis++) for (int i = 0; i < 4; i++) {
                double[] p = { b[0], b[1], b[2] };
                int one = (axis + 1) % 3, two = (axis + 2) % 3;
                p[one] = b[one + ((i & 1) == 0 ? 0 : 3)];
                p[two] = b[two + ((i & 2) == 0 ? 0 : 3)];
                double t = phase + (axis * 4 + i) * .381966;
                p[axis] += (b[axis + 3] - b[axis]) * (t - Math.floor(t));
                org.lwjgl.opengl.GL11.glVertex3d(p[0], p[1], p[2]);
            }
            org.lwjgl.opengl.GL11.glEnd();
        } finally {
            org.lwjgl.opengl.GL11.glPopMatrix();
            org.lwjgl.opengl.GL11.glPopAttrib();
            org.lwjgl.opengl.GL11.glMatrixMode(matrixMode);
        }
    }

    @SubscribeEvent
    public void tick(TickEvent.ClientTickEvent event) {
        if (event.phase != TickEvent.Phase.END) return;
        GramophoneLocalClient.tick();
        ensureContext();
        Minecraft mc = Minecraft.getMinecraft();
        if (mc.thePlayer == null) return;
        for (Device device : new java.util.ArrayList<Device>(DEVICES.values())) {
            GramophonePacket c = device.config;
            // Never load a chunk to validate an audible device beyond the client's watch distance.
            if (!mc.theWorld.blockExists(c.x, c.y, c.z)) continue;
            net.minecraft.tileentity.TileEntity tile = mc.theWorld.getTileEntity(c.x, c.y, c.z);
            if (!(mc.theWorld.getBlock(c.x, c.y, c.z) instanceof BlockGramophone)
                || tile != null && (!(tile instanceof TileGramophone) || tile.isInvalid()
                    || ((TileGramophone) tile).rangeMetadataReady
                        && !((TileGramophone) tile).instance.equals(c.instance)))
                removeDevice(c.key());
        }
        double now = System.nanoTime() / 1000000000.0;
        Iterator<Device> retiring = RETIRING.iterator();
        while (retiring.hasNext()) {
            Device device = retiring.next();
            device.playback.tick(now, false, false, true);
            if (!device.playback.started()) {
                device.clear();
                retiring.remove();
            }
        }
        for (Device device : DEVICES.values()) {
            GramophonePacket c = device.config;
            boolean inside = GramophonePlayback.contains(
                mc.thePlayer.dimension,
                c.dimension,
                c.x,
                c.y,
                c.z,
                c.radius,
                mc.thePlayer.posX,
                mc.thePlayer.posY,
                mc.thePlayer.posZ);
            if (inside && c.enabled && c.powered && !c.source.isEmpty() && device.media == null)
                device.media = request(c);
            if (device.media != null && device.media.future != null
                && device.media.future.isDone()
                && device.media.error == null) {
                try {
                    device.audio.file(device.media.future.get());
                } catch (Exception exception) {
                    device.media.error = "音频当前不可用：" + (exception.getCause() == null ? exception.getMessage()
                        : exception.getCause()
                            .getMessage());
                }
            }
            device.playback.tick(now, inside, c.enabled && !c.source.isEmpty(), c.powered);
            if ((!inside || !c.enabled || c.source.isEmpty()) && !device.playback.started()) device.clear();
        }
        long idleBytes = 0;
        Iterator<Media> expired = MEDIA.values()
            .iterator();
        while (expired.hasNext()) {
            Media media = expired.next();
            if (media.preview != null && media.pins == 0
                && System.nanoTime() - media.idle > TimeUnit.MINUTES.toNanos(30)) {
                expired.remove();
                discard(media);
            }
        }
        for (Media media : MEDIA.values()) if (media.pins == 0) idleBytes += media.bytes;
        while (idleBytes > IDLE_BUDGET || MEDIA.size() > 128) {
            String oldest = null;
            long age = Long.MAX_VALUE;
            for (Map.Entry<String, Media> entry : MEDIA.entrySet())
                if (entry.getValue().pins == 0 && entry.getValue().idle < age) {
                    oldest = entry.getKey();
                    age = entry.getValue().idle;
                }
            if (oldest == null) break;
            Media media = MEDIA.remove(oldest);
            idleBytes -= media.bytes;
            discard(media);
        }
    }
}
