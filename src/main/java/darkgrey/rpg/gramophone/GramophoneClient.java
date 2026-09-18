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

    private static final class Media {

        Future<Path> future;
        Future<Path> transfer;
        Path path;
        volatile boolean discarded;
        String error;
        long idle;
        int pins;
        long bytes;
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
                media = null;
            }
        }
    }

    public static void accept(GramophonePacket packet) {
        Minecraft mc = Minecraft.getMinecraft();
        if (mc.theWorld == null || mc.thePlayer == null || packet.dimension != mc.thePlayer.dimension) return;
        ensureContext();
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

    private static Media request(GramophonePacket config) {
        String source = config.source;
        Media cached = MEDIA.get(source);
        if (cached != null) {
            cached.pins++;
            return cached;
        }
        final Media media = new Media();
        media.pins = 1;
        MEDIA.put(source, media);
        final Path target = directory;
        try {
            final Future<Path> local = source.startsWith("local:") ? GramophoneLocalClient.download(config, target)
                : null;
            media.transfer = local;
            final OnlineMusicSource online = local == null ? OnlineMusicSource.parse(source) : null;
            media.future = IO.submit(() -> {
                Path path = local == null ? OnlineMusicResolver.download(online, target)
                    : local.get(65, TimeUnit.SECONDS);
                try {
                    CodecGramophoneMp3 codec = new CodecGramophoneMp3();
                    try {
                        if (!codec.initialize(
                            path.toUri()
                                .toURL()))
                            throw new java.io.IOException("音频无法按 MP3 解码");
                    } finally {
                        codec.cleanup();
                    }
                    synchronized (media) {
                        if (media.discarded) {
                            GramophoneFiles.retire(path);
                            throw new java.io.IOException("媒体上下文已关闭");
                        }
                        media.bytes = Files.size(path);
                        media.path = path;
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

    @SubscribeEvent
    public void render(net.minecraftforge.client.event.RenderWorldLastEvent event) {
        Minecraft mc = Minecraft.getMinecraft();
        if (!(mc.currentScreen instanceof GuiGramophone)) return;
        double[] b = ((GuiGramophone) mc.currentScreen).previewBounds();
        if (b == null) return;
        org.lwjgl.opengl.GL11.glPushAttrib(org.lwjgl.opengl.GL11.GL_ALL_ATTRIB_BITS);
        org.lwjgl.opengl.GL11.glPushMatrix();
        try {
            org.lwjgl.opengl.GL11.glTranslated(
                -net.minecraft.client.renderer.entity.RenderManager.instance.viewerPosX,
                -net.minecraft.client.renderer.entity.RenderManager.instance.viewerPosY,
                -net.minecraft.client.renderer.entity.RenderManager.instance.viewerPosZ);
            org.lwjgl.opengl.GL11.glDisable(org.lwjgl.opengl.GL11.GL_TEXTURE_2D);
            org.lwjgl.opengl.GL11.glDisable(org.lwjgl.opengl.GL11.GL_LIGHTING);
            org.lwjgl.opengl.GL11.glDisable(org.lwjgl.opengl.GL11.GL_DEPTH_TEST);
            org.lwjgl.opengl.GL11.glDepthMask(false);
            org.lwjgl.opengl.GL11.glColor4f(.3f, .8f, 1, 1);
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
        } finally {
            org.lwjgl.opengl.GL11.glPopMatrix();
            org.lwjgl.opengl.GL11.glPopAttrib();
        }
    }

    @SubscribeEvent
    public void tick(TickEvent.ClientTickEvent event) {
        if (event.phase != TickEvent.Phase.END) return;
        GramophoneLocalClient.tick();
        ensureContext();
        Minecraft mc = Minecraft.getMinecraft();
        if (mc.thePlayer == null) return;
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
