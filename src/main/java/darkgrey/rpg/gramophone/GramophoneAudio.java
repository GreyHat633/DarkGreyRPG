package darkgrey.rpg.gramophone;

import java.nio.file.Path;
import java.util.HashMap;
import java.util.Iterator;
import java.util.Map;
import java.util.concurrent.ConcurrentHashMap;

import javax.sound.sampled.AudioFormat;
import javax.sound.sampled.AudioSystem;
import javax.sound.sampled.SourceDataLine;

import net.minecraft.client.Minecraft;
import net.minecraft.client.audio.SoundCategory;

import darkgrey.rpg.client.session.PlayerUiPreferences;
import paulscode.sound.SoundBuffer;

/** A gramophone-only software mixer. Paused devices own their decoder; no vanilla channel is stolen. */
public final class GramophoneAudio implements GramophonePlayback.Backend {

    private static final int RATE = 48000, FRAMES = 512, MAX_SOURCES = 64;
    private static final Object WAKE = new Object();
    private static final ConcurrentHashMap<GramophoneAudio, Integer> ACTIVE = new ConcurrentHashMap<GramophoneAudio, Integer>();
    private static boolean workerStarted;
    private final String channel = "dgr_gramophone_" + java.util.UUID.randomUUID()
        .toString();
    private Path file;
    private boolean loop = true;
    private double offset;
    private volatile boolean failed, paused, playing;
    private volatile float gain;
    private volatile double position;
    private volatile int generation;
    private volatile String failure = "";

    public void file(Path file) {
        this.file = file;
    }

    public void preview(double offset) {
        this.offset = offset;
        loop = false;
    }

    public double seconds() {
        return position;
    }

    public boolean playing() {
        return playing && !paused;
    }

    public boolean failed() {
        return failed;
    }

    public String failure() {
        return failure;
    }

    void resetFailure() {
        failed = false;
        failure = "";
    }

    private void diagnostic(String action) {
        if (Boolean.getBoolean("darkgrey.gramophone.diagnostics"))
            darkgrey.rpg.DarkGreyRpg.LOG.info("Gramophone {} {} seconds={}", channel, action, seconds());
    }

    @Override
    public boolean start() {
        if (file == null || failed) return false;
        synchronized (WAKE) {
            if (ACTIVE.size() >= MAX_SOURCES && !ACTIVE.containsKey(this)) {
                failed = true;
                failure = "留声机及试听音源达到 64 个上限；已在播放的音源保持";
                return false;
            }
            position = offset;
            paused = false;
            playing = false;
            gain = 0;
            ACTIVE.put(this, ++generation);
            if (!workerStarted) {
                workerStarted = true;
                Thread thread = new Thread(GramophoneAudio::mix, "DGR-Gramophone-Mixer");
                thread.setDaemon(true);
                thread.start();
            }
            WAKE.notifyAll();
            diagnostic("START");
            return true;
        }
    }

    @Override
    public void pause() {
        paused = true;
        diagnostic("PAUSE");
    }

    @Override
    public void resume() {
        diagnostic("RESUME");
        paused = false;
    }

    @Override
    public void volume(float value) {
        Minecraft mc = Minecraft.getMinecraft();
        gain = value * (float) PlayerUiPreferences.gramophoneVolume()
            * mc.gameSettings.getSoundLevel(SoundCategory.MUSIC)
            * mc.gameSettings.getSoundLevel(SoundCategory.MASTER);
    }

    @Override
    public void stop() {
        diagnostic("STOP");
        ACTIVE.remove(this);
        playing = false;
        paused = false;
    }

    private static void mix() {
        Map<GramophoneAudio, Cursor> cursors = new HashMap<GramophoneAudio, Cursor>();
        SourceDataLine line = null;
        float[] samples = new float[FRAMES * 2];
        byte[] output = new byte[FRAMES * 4];
        for (;;) {
            try {
                Iterator<Map.Entry<GramophoneAudio, Cursor>> iterator = cursors.entrySet()
                    .iterator();
                while (iterator.hasNext()) {
                    Map.Entry<GramophoneAudio, Cursor> entry = iterator.next();
                    Integer version = ACTIVE.get(entry.getKey());
                    if (version == null || version.intValue() != entry.getValue().generation) {
                        entry.getValue()
                            .close();
                        iterator.remove();
                    }
                }
                if (ACTIVE.isEmpty()) {
                    if (line != null) {
                        line.stop();
                        line.flush();
                        line.close();
                        line = null;
                    }
                    synchronized (WAKE) {
                        if (ACTIVE.isEmpty()) WAKE.wait(1000);
                    }
                    continue;
                }
                if (line == null) {
                    AudioFormat format = new AudioFormat(RATE, 16, 2, true, false);
                    line = AudioSystem.getSourceDataLine(format);
                    line.open(format, FRAMES * 4 * 4);
                    line.start();
                }
                java.util.Arrays.fill(samples, 0);
                for (Map.Entry<GramophoneAudio, Integer> entry : ACTIVE.entrySet()) {
                    GramophoneAudio audio = entry.getKey();
                    if (audio.paused) continue;
                    Cursor cursor = cursors.get(audio);
                    try {
                        if (cursor != null && cursor.generation != entry.getValue()
                            .intValue()) {
                            cursor.close();
                            cursors.remove(audio);
                            cursor = null;
                        }
                        if (cursor == null) {
                            cursor = new Cursor(audio, entry.getValue());
                            cursors.put(audio, cursor);
                        }
                        for (int i = 0; i < FRAMES; i++) {
                            if (audio.paused || audio.generation != entry.getValue()
                                .intValue() || !ACTIVE.containsKey(audio)) break;
                            if (!cursor.next()) {
                                ACTIVE.remove(audio, entry.getValue());
                                if (audio.generation == entry.getValue()
                                    .intValue()) audio.playing = false;
                                break;
                            }
                            float volume = audio.gain;
                            audio.playing = true;
                            samples[i * 2] += cursor.left * volume;
                            samples[i * 2 + 1] += cursor.right * volume;
                            audio.position += 1.0 / RATE;
                        }
                    } catch (Exception e) {
                        if (audio.generation == entry.getValue()
                            .intValue()) {
                            audio.failed = true;
                            audio.failure = "音频解码或文件读取失败";
                            audio.playing = false;
                        }
                        ACTIVE.remove(audio, entry.getValue());
                        darkgrey.rpg.DarkGreyRpg.LOG.warn("Gramophone decoder failed", e);
                    }
                }
                for (int i = 0; i < samples.length; i++) {
                    int value = Math.round(Math.max(-1, Math.min(1, samples[i])) * 32767);
                    output[i * 2] = (byte) value;
                    output[i * 2 + 1] = (byte) (value >> 8);
                }
                int written = 0;
                while (written < output.length) written += line.write(output, written, output.length - written);
            } catch (Exception e) {
                for (GramophoneAudio audio : ACTIVE.keySet()) {
                    audio.failed = true;
                    audio.failure = "系统音频输出不可用，请检查输出设备";
                    audio.playing = false;
                }
                ACTIVE.clear();
                for (Cursor cursor : cursors.values()) cursor.close();
                cursors.clear();
                if (line != null) {
                    line.stop();
                    line.flush();
                    line.close();
                    line = null;
                }
                darkgrey.rpg.DarkGreyRpg.LOG.warn("Gramophone output unavailable", e);
            }
        }
    }

    /** One frame of decoded PCM plus a linear resampler per device; paused cursors never advance. */
    static final class Cursor {

        final GramophoneAudio owner;
        final int generation;
        final CodecGramophoneMp3 codec = new CodecGramophoneMp3();
        AudioFormat format;
        byte[] pcm;
        int index;
        double fraction;
        float currentL, currentR, nextL, nextR, inputL, inputR, left, right;
        boolean primed;

        Cursor(GramophoneAudio owner, int generation) throws java.io.IOException {
            this.owner = owner;
            this.generation = generation;
            java.net.URL url = new java.net.URL(
                owner.file.toUri()
                    .toURL()
                    .toString() + (owner.offset > 0 ? "#" + owner.offset : ""));
            if (!codec.initialize(url)) throw new java.io.IOException("Cannot initialize gramophone decoder");
            format = codec.getAudioFormat();
        }

        boolean sample() throws java.io.IOException {
            if (pcm == null || index + format.getFrameSize() > pcm.length) {
                SoundBuffer frame = codec.read();
                if (frame == null) {
                    if (codec.failed()) throw new java.io.IOException("Gramophone decode error");
                    if (!owner.loop) return false;
                    if (!codec.initialize(
                        owner.file.toUri()
                            .toURL()))
                        throw new java.io.IOException("Cannot loop gramophone");
                    frame = codec.read();
                    owner.position = 0;
                    if (frame == null) throw new java.io.IOException("Empty loop");
                }
                pcm = frame.audioData;
                index = 0;
            }
            inputL = value(index);
            inputR = format.getChannels() == 1 ? inputL : value(index + 2);
            index += format.getFrameSize();
            return true;
        }

        float value(int offset) {
            int value = format.isBigEndian() ? (short) ((pcm[offset] << 8) | (pcm[offset + 1] & 255))
                : (short) ((pcm[offset + 1] << 8) | (pcm[offset] & 255));
            return value / 32768f;
        }

        boolean next() throws java.io.IOException {
            if (!primed) {
                if (!sample()) return false;
                currentL = inputL;
                currentR = inputR;
                if (!sample()) return false;
                nextL = inputL;
                nextR = inputR;
                primed = true;
            }
            left = currentL + (nextL - currentL) * (float) fraction;
            right = currentR + (nextR - currentR) * (float) fraction;
            fraction += format.getSampleRate() / RATE;
            while (fraction >= 1) {
                currentL = nextL;
                currentR = nextR;
                if (!sample()) return false;
                nextL = inputL;
                nextR = inputR;
                fraction -= 1;
            }
            return true;
        }

        void close() {
            codec.cleanup();
            pcm = null;
        }
    }
}
