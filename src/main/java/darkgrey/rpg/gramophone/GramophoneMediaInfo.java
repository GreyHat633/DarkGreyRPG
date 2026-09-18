package darkgrey.rpg.gramophone;

import java.io.IOException;
import java.io.InputStream;
import java.nio.file.Files;
import java.nio.file.Path;
import java.security.MessageDigest;

import paulscode.sound.SoundBuffer;

/** Streaming validation and a small envelope, no full PCM resident in memory. */
public final class GramophoneMediaInfo {

    public final Path path;
    public final String hash;
    public final long bytes;
    public final double seconds;
    public final float[] envelope;

    private GramophoneMediaInfo(Path path, String hash, long bytes, double seconds, float[] envelope) {
        this.path = path;
        this.hash = hash;
        this.bytes = bytes;
        this.seconds = seconds;
        this.envelope = envelope;
    }

    public static GramophoneMediaInfo inspect(Path path) throws IOException {
        long bytes = Files.size(path);
        if (bytes <= 0 || bytes > OnlineMusicResolver.MAX_BYTES) throw new IOException("MP3 副本须为 1 字节至 32 MiB");
        final MessageDigest digest;
        try {
            digest = MessageDigest.getInstance("SHA-256");
        } catch (java.security.NoSuchAlgorithmException e) {
            throw new IOException(e);
        }
        try (InputStream input = Files.newInputStream(path)) {
            byte[] buffer = new byte[16384];
            int count;
            long total = 0;
            while ((count = input.read(buffer)) != -1) {
                if (Thread.currentThread()
                    .isInterrupted()) throw new IOException("音频检查已取消");
                total += count;
                if (total > OnlineMusicResolver.MAX_BYTES) throw new IOException("文件在读取时超出上限");
                digest.update(buffer, 0, count);
            }
            if (total != bytes) throw new IOException("文件在读取过程中发生变化");
        }
        StringBuilder hash = new StringBuilder();
        for (byte b : digest.digest()) hash.append(String.format(java.util.Locale.ROOT, "%02x", b & 255));
        CodecGramophoneMp3 codec = new CodecGramophoneMp3();
        double seconds = 0;
        float[] peaks = new float[9000];
        try {
            if (!codec.initialize(
                path.toUri()
                    .toURL()))
                throw new IOException("仅支持可解码的 MP3 文件");
            javax.sound.sampled.AudioFormat format = codec.getAudioFormat();
            SoundBuffer frame;
            while ((frame = codec.read()) != null) {
                if (Thread.currentThread()
                    .isInterrupted()) throw new IOException("音频检查已取消");
                if (seconds >= 900) throw new IOException("音乐时长不能超过 15 分钟");
                int bucket = Math.min(peaks.length - 1, (int) (seconds * 10));
                byte[] pcm = frame.audioData;
                for (int i = 0; i + 1 < pcm.length; i += 2) {
                    int sample = format.isBigEndian() ? (short) ((pcm[i] << 8) | (pcm[i + 1] & 255))
                        : (short) ((pcm[i + 1] << 8) | (pcm[i] & 255));
                    peaks[bucket] = Math.max(peaks[bucket], Math.abs(sample) / 32768f);
                }
                seconds += pcm.length / (format.getFrameSize() * (double) format.getFrameRate());
            }
            if (codec.failed() || seconds < .1 || seconds > 900) throw new IOException("音频不完整、损坏或超出时长限制");
        } finally {
            codec.cleanup();
        }
        float[] envelope = new float[256];
        int count = Math.min(peaks.length, (int) Math.ceil(seconds * 10));
        for (int i = 0; i < count; i++) {
            int bucket = Math.min(255, i * 256 / count);
            envelope[bucket] = Math.max(envelope[bucket], peaks[i]);
        }
        return new GramophoneMediaInfo(path, hash.toString(), bytes, seconds, envelope);
    }
}
