package darkgrey.rpg.gramophone;

import java.io.IOException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.concurrent.ArrayBlockingQueue;
import java.util.concurrent.Future;
import java.util.concurrent.ThreadPoolExecutor;
import java.util.concurrent.TimeUnit;

import net.minecraft.client.Minecraft;

import darkgrey.rpg.network.MainThreadScheduler;

/** One editor owns one draft and one preview source. Closing it never changes a server device. */
public final class GramophoneDraft {

    private static final ThreadPoolExecutor IO = new ThreadPoolExecutor(
        2,
        2,
        30,
        TimeUnit.SECONDS,
        new ArrayBlockingQueue<Runnable>(8),
        task -> {
            Thread t = new Thread(task, "DGR-Gramophone-Draft");
            t.setDaemon(true);
            return t;
        });
    private final GuiGramophone owner;
    private final Path directory;
    private Future<?> future;
    private volatile boolean closed;
    private volatile int generation;
    public GramophoneMediaInfo media;
    public String label = "尚未导入";
    public String onlineInput, canonical;
    private GramophoneAudio preview;
    private double position;
    private boolean playing;
    private boolean resumeAfterSeek;
    private long startedAt;

    public GramophoneDraft(GuiGramophone owner) {
        this.owner = owner;
        directory = Minecraft.getMinecraft().mcDataDir.toPath()
            .resolve("DarkGreyRPG/Cache/Gramophone")
            .resolve(
                java.util.UUID.randomUUID()
                    .toString());
        GramophoneFiles.activate(directory);
    }

    public void choose() {
        if (closed) return;
        java.awt.EventQueue.invokeLater(() -> {
            java.awt.FileDialog dialog = new java.awt.FileDialog(
                (java.awt.Frame) null,
                "导入留声机 MP3（不超过 32 MiB / 15 分钟）",
                java.awt.FileDialog.LOAD);
            dialog.setFile("*.mp3");
            dialog.setVisible(true);
            String file = dialog.getFile(), folder = dialog.getDirectory();
            dialog.dispose();
            if (file != null && folder != null && !closed)
                MainThreadScheduler.scheduleClient(() -> load(new java.io.File(folder, file).toPath(), null));
        });
    }

    public void online(String url) {
        load(null, url);
    }

    private void load(Path local, String online) {
        if (closed) return;
        clear();
        int ticket = ++generation;
        label = "正在检查音频…";
        try {
            future = IO.submit(() -> {
                Path target = null;
                String normalizedSource = null;
                try {
                    GramophoneFiles.directory(directory);
                    if (local != null) {
                        if (!Files.isRegularFile(local) || Files.size(local) > OnlineMusicResolver.MAX_BYTES)
                            throw new IOException("请选择不超过 32 MiB 的 MP3 文件");
                        target = Files.createTempFile(directory, "draft-", ".dgrmp3");
                        try (java.io.InputStream input = Files.newInputStream(local);
                            java.io.OutputStream output = Files.newOutputStream(target)) {
                            byte[] buffer = new byte[16384];
                            int count;
                            long total = 0;
                            while ((count = input.read(buffer)) != -1) {
                                if (closed || ticket != generation
                                    || Thread.currentThread()
                                        .isInterrupted())
                                    throw new IOException("草稿已取消");
                                total += count;
                                if (total > OnlineMusicResolver.MAX_BYTES) throw new IOException("文件超过 32 MiB");
                                output.write(buffer, 0, count);
                            }
                        }
                    } else {
                        OnlineMusicSource resolved = OnlineMusicResolver.normalize(online);
                        normalizedSource = resolved.canonical();
                        target = OnlineMusicResolver.download(resolved, directory);
                    }
                    GramophoneMediaInfo info = GramophoneMediaInfo.inspect(target);
                    final String normalized = normalizedSource;
                    MainThreadScheduler.scheduleClient(() -> {
                        if (closed || ticket != generation) {
                            GramophoneFiles.retire(info.path);
                            return;
                        }
                        media = info;
                        label = local == null ? "在线试听已就绪"
                            : local.getFileName()
                                .toString();
                        onlineInput = online;
                        canonical = normalized;
                    });
                } catch (Exception e) {
                    GramophoneFiles.retire(target);
                    MainThreadScheduler.scheduleClient(
                        () -> { if (!closed && ticket == generation) label = "音频不可用：" + e.getMessage(); });
                }
            });
        } catch (java.util.concurrent.RejectedExecutionException e) {
            label = "试听检查队列已满，请稍后重试";
        }
    }

    public void toggle() {
        if (media == null) return;
        if (playing) {
            position = seconds();
            if (preview != null) preview.stop();
            preview = null;
            playing = false;
        } else start(position >= media.seconds - .1 ? 0 : position);
    }

    public void seek(double fraction) {
        if (media == null) return;
        position = Math.max(0, Math.min(media.seconds - .05, fraction * media.seconds));
    }

    public void beginSeek() {
        resumeAfterSeek = playing;
        if (preview != null) preview.stop();
        preview = null;
        playing = false;
    }

    public void endSeek() {
        if (resumeAfterSeek && media != null) start(position);
        resumeAfterSeek = false;
    }

    private void start(double at) {
        preview = new GramophoneAudio();
        preview.file(media.path);
        preview.preview(at);
        playing = preview.start();
        position = at;
        startedAt = System.nanoTime();
        if (!playing) label = "试听通道无法建立";
    }

    public double seconds() {
        return preview == null ? position : Math.min(media.seconds, preview.seconds());
    }

    public boolean playing() {
        return playing;
    }

    public void tick() {
        if (preview == null) return;
        preview.volume(1);
        if (playing && !preview.playing() && System.nanoTime() - startedAt > TimeUnit.SECONDS.toNanos(1)) {
            double expected = position + (System.nanoTime() - startedAt) / 1000000000.0;
            if (expected >= media.seconds - .2) position = media.seconds;
            else label = "试听已停止，音频通道当前不可用";
            preview.stop();
            preview = null;
            playing = false;
        }
    }

    public void clear() {
        generation++;
        if (future != null) future.cancel(true);
        future = null;
        IO.purge();
        if (preview != null) preview.stop();
        preview = null;
        playing = false;
        resumeAfterSeek = false;
        position = 0;
        if (media != null) GramophoneFiles.retire(media.path);
        media = null;
        onlineInput = null;
        canonical = null;
    }

    public void close() {
        closed = true;
        clear();
        GramophoneFiles.release(directory);
    }
}
