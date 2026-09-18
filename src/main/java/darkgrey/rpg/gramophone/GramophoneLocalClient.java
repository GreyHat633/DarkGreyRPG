package darkgrey.rpg.gramophone;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.HashMap;
import java.util.Map;
import java.util.UUID;
import java.util.concurrent.ArrayBlockingQueue;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.Future;
import java.util.concurrent.ThreadPoolExecutor;
import java.util.concurrent.TimeUnit;

import net.minecraft.client.Minecraft;

import darkgrey.rpg.network.MainThreadScheduler;

/** Bounded client transfer work. Draft import/preview never calls upload implicitly. */
public final class GramophoneLocalClient {

    private static final ThreadPoolExecutor IO = new ThreadPoolExecutor(
        1,
        1,
        30,
        TimeUnit.SECONDS,
        new ArrayBlockingQueue<Runnable>(32),
        task -> {
            Thread t = new Thread(task, "DGR-Gramophone-Transfer");
            t.setDaemon(true);
            return t;
        });
    private static final Map<String, Download> DOWNLOADS = new HashMap<String, Download>();
    private static Upload upload;

    private static final class Download {

        final GramophoneMediaPacket request;
        final Object world = Minecraft.getMinecraft().theWorld;
        final CompletableFuture<Path> future = new CompletableFuture<Path>();
        volatile Path path;
        int offset;
        long touched = System.nanoTime();

        Download(GramophoneMediaPacket request) {
            this.request = request;
        }
    }

    private static final class Upload {

        final GramophoneMediaPacket request;
        final GramophoneMediaInfo media;
        final GuiGramophone owner;
        final Object world = Minecraft.getMinecraft().theWorld;
        long touched = System.nanoTime();

        Upload(GramophoneMediaPacket request, GramophoneMediaInfo media, GuiGramophone owner) {
            this.request = request;
            this.media = media;
            this.owner = owner;
        }
    }

    private static GramophoneMediaPacket packet(int operation, GramophonePacket config, String hash) {
        GramophoneMediaPacket packet = new GramophoneMediaPacket();
        packet.operation = operation;
        packet.device = config;
        packet.hash = hash;
        packet.token = UUID.randomUUID()
            .toString();
        return packet;
    }

    public static Future<Path> download(GramophonePacket config, Path directory) {
        GramophoneMediaPacket request = packet(GramophoneMediaPacket.FETCH, config, config.source.substring(6));
        Download download = new Download(request);
        DOWNLOADS.put(request.token, download);
        try {
            IO.execute(() -> {
                try {
                    GramophoneFiles.directory(directory);
                    download.path = Files.createTempFile(directory, "track-", ".dgrmp3");
                    MainThreadScheduler.scheduleClient(() -> {
                        if (current(download)) GramophoneNetwork.CHANNEL.sendToServer(request);
                        else fail(download, "媒体上下文已关闭");
                    });
                } catch (IOException e) {
                    MainThreadScheduler.scheduleClient(() -> fail(download, e.getMessage()));
                }
            });
        } catch (java.util.concurrent.RejectedExecutionException e) {
            fail(download, "媒体传输队列已满");
        }
        return download.future;
    }

    private static boolean current(Download value) {
        return DOWNLOADS.get(value.request.token) == value && Minecraft.getMinecraft().theWorld == value.world
            && !value.future.isCancelled();
    }

    private static void fail(Download value, String message) {
        DOWNLOADS.remove(value.request.token);
        value.future.completeExceptionally(new IOException(message));
        GramophoneFiles.retire(value.path);
    }

    public static void upload(GramophoneMediaInfo media, GramophonePacket config, GuiGramophone owner) {
        cancel(owner);
        GramophoneMediaPacket request = packet(GramophoneMediaPacket.UPLOAD, config, media.hash);
        request.total = (int) media.bytes;
        upload = new Upload(request, media, owner);
        GramophoneNetwork.CHANNEL.sendToServer(request);
        owner.transferStatus("正在申请上传…", false);
    }

    public static void cancel(GuiGramophone owner) {
        if (upload == null || upload.owner != owner) return;
        upload.request.operation = GramophoneMediaPacket.CANCEL;
        if (Minecraft.getMinecraft().theWorld == upload.world) GramophoneNetwork.CHANNEL.sendToServer(upload.request);
        upload = null;
    }

    public static void accept(GramophoneMediaPacket packet) {
        Download download = DOWNLOADS.get(packet.token);
        if (download != null) {
            if (!current(download)) {
                fail(download, "媒体上下文已关闭");
                return;
            }
            if (packet.operation == GramophoneMediaPacket.ERROR) {
                fail(download, packet.message);
                return;
            }
            if (packet.operation != GramophoneMediaPacket.DATA || !packet.hash.equals(download.request.hash)
                || packet.offset != download.offset
                || packet.data.length == 0
                || packet.data.length > packet.total - packet.offset) {
                fail(download, "媒体下载分块不一致");
                return;
            }
            download.offset += packet.data.length;
            download.touched = System.nanoTime();
            try {
                IO.execute(() -> {
                    try {
                        if (download.future.isDone()) return;
                        try (java.io.OutputStream output = Files
                            .newOutputStream(download.path, java.nio.file.StandardOpenOption.APPEND)) {
                            output.write(packet.data);
                        }
                        if (download.offset == packet.total) {
                            GramophoneMediaInfo info = GramophoneMediaInfo.inspect(download.path);
                            if (!info.hash.equals(packet.hash) || info.bytes != packet.total)
                                throw new IOException("媒体下载指纹校验失败");
                            MainThreadScheduler.scheduleClient(() -> {
                                if (!current(download)) {
                                    fail(download, "媒体上下文已关闭");
                                    return;
                                }
                                DOWNLOADS.remove(packet.token);
                                download.future.complete(download.path);
                            });
                        } else MainThreadScheduler.scheduleClient(() -> {
                            if (!current(download)) {
                                fail(download, "媒体上下文已关闭");
                                return;
                            }
                            download.request.offset = download.offset;
                            download.request.total = packet.total;
                            GramophoneNetwork.CHANNEL.sendToServer(download.request);
                        });
                    } catch (Exception e) {
                        MainThreadScheduler.scheduleClient(() -> fail(download, e.getMessage()));
                    }
                });
            } catch (java.util.concurrent.RejectedExecutionException e) {
                fail(download, "媒体下载队列已满");
            }
            return;
        }
        final Upload active = upload;
        if (active == null || !active.request.token.equals(packet.token) || !active.request.hash.equals(packet.hash))
            return;
        if (Minecraft.getMinecraft().theWorld != active.world
            || Minecraft.getMinecraft().currentScreen != active.owner) {
            cancel(active.owner);
            return;
        }
        active.touched = System.nanoTime();
        if (packet.operation == GramophoneMediaPacket.ERROR || packet.operation == GramophoneMediaPacket.COMMITTED) {
            active.owner
                .transferStatus(packet.operation == GramophoneMediaPacket.COMMITTED ? "已保存并上传" : packet.message, true);
            upload = null;
            return;
        }
        if (packet.operation != GramophoneMediaPacket.ACK || packet.offset >= active.media.bytes) return;
        active.owner.transferStatus("正在上传 " + (packet.offset * 100L / active.media.bytes) + "%", false);
        try {
            IO.execute(() -> {
                try {
                    GramophoneMediaPacket next = packet(
                        GramophoneMediaPacket.CHUNK,
                        active.request.device,
                        active.media.hash);
                    next.token = active.request.token;
                    next.total = (int) active.media.bytes;
                    next.offset = packet.offset;
                    next.data = new byte[Math.min(GramophoneMediaPacket.CHUNK_BYTES, next.total - next.offset)];
                    try (
                        java.io.RandomAccessFile file = new java.io.RandomAccessFile(active.media.path.toFile(), "r")) {
                        file.seek(next.offset);
                        file.readFully(next.data);
                    }
                    MainThreadScheduler
                        .scheduleClient(() -> { if (upload == active) GramophoneNetwork.CHANNEL.sendToServer(next); });
                } catch (Exception e) {
                    MainThreadScheduler.scheduleClient(() -> {
                        active.owner.transferStatus("上传读取失败：" + e.getMessage(), true);
                        cancel(active.owner);
                    });
                }
            });
        } catch (java.util.concurrent.RejectedExecutionException e) {
            active.owner.transferStatus("上传队列已满", true);
            cancel(active.owner);
        }
    }

    public static void tick() {
        long now = System.nanoTime();
        for (Download value : new java.util.ArrayList<Download>(DOWNLOADS.values()))
            if (!current(value) || now - value.touched > TimeUnit.SECONDS.toNanos(60)) fail(value, "媒体下载超时或上下文已关闭");
        if (upload != null && (Minecraft.getMinecraft().theWorld != upload.world
            || now - upload.touched > TimeUnit.SECONDS.toNanos(60))) {
            upload.owner.transferStatus("上传超时或上下文已关闭，旧音乐保持", true);
            cancel(upload.owner);
        }
    }
}
