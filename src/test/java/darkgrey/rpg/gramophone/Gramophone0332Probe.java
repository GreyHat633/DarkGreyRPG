package darkgrey.rpg.gramophone;

public final class Gramophone0332Probe {

    private static final class Backend implements GramophonePlayback.Backend {

        int starts, stops, pauses, resumes;
        float volume;

        public boolean start() {
            starts++;
            return true;
        }

        public void pause() {
            pauses++;
        }

        public void resume() {
            resumes++;
        }

        public void stop() {
            stops++;
        }

        public void volume(float value) {
            volume = value;
        }
    }

    private static void check(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }

    public static void main(String[] args) throws Exception {
        GramophoneStorageProbe.run();
        check(GramophonePlayback.contains(0, 0, -1, 0, 0, 0, -.1, .9, .9), "negative floor/R0");
        check(!GramophonePlayback.contains(0, 0, -1, 0, 0, 0, 0, 0, 0), "boundary outside");
        check(!GramophonePlayback.contains(0, 1, 0, 0, 0, 16, 0, 0, 0), "dimension");
        check(!GramophonePlayback.contains(0, 0, Integer.MIN_VALUE, 0, 0, 128, Integer.MAX_VALUE, 0, 0), "overflow");
        Backend a = new Backend(), b = new Backend();
        GramophonePlayback first = new GramophonePlayback(a), second = new GramophonePlayback(b);
        first.tick(0, true, true, true);
        first.tick(.6, true, true, true);
        check(a.starts == 1 && a.volume == 1, "fade in");
        first.tick(1, true, true, false);
        first.tick(100, true, true, false);
        check(a.pauses == 1 && a.stops == 0, "redstone retains personal position");
        first.tick(101, true, true, true);
        check(a.resumes == 1 && a.starts == 1, "resume no restart");
        second.tick(101, true, true, true);
        check(b.starts == 1 && a.starts == 1, "independent entry");
        first.tick(101.2, false, true, true);
        first.tick(101.3, true, true, true);
        check(a.stops == 0 && a.starts == 1, "fade reversal keeps instance");
        first.tick(102, false, true, true);
        check(a.stops == 1, "full leave releases");
        first.tick(103, true, true, true);
        check(a.starts == 2 && b.stops == 0, "reentry starts only this device");
        first.tick(104, true, true, false);
        first.tick(105, false, true, false);
        check(a.stops == 2, "leave while paused releases");
        String[] bad = { "file:///tmp/music", "https://127.0.0.1/song?id=1",
            "https://music.163.com.evil.test/song?id=1", "https://evil.test@music.163.com/song?id=1",
            "https://music.163.com:999/song?id=1" };
        for (String input : bad) {
            boolean rejected = false;
            try {
                OnlineMusicSource.parse(input);
            } catch (IllegalArgumentException expected) {
                rejected = true;
            }
            check(rejected, "reject " + input);
        }
        check(
            OnlineMusicSource.parse("https://music.163.com/#/song?id=416892104").key.equals("416892104"),
            "netease fragment");
        check(
            OnlineMusicSource.parse("https://y.qq.com/n/ryqq/songDetail/0039MnYb0qxYhV").provider.equals("qq"),
            "qq identity");
        check(!OnlineMusicResolver.allowedHost("music.126.net.evil.test", "netease"), "redirect host boundary");
        if (args.length == 2) {
            java.nio.file.Path file = OnlineMusicResolver
                .download(OnlineMusicSource.parse(args[0]), java.nio.file.Paths.get(args[1]));
            CodecGramophoneMp3 codec = new CodecGramophoneMp3();
            try {
                check(
                    codec.initialize(
                        file.toUri()
                            .toURL()),
                    "actual MP3 decoder initialization");
                long bytes = 0;
                paulscode.sound.SoundBuffer frame;
                javax.sound.sampled.AudioFormat format = codec.getAudioFormat();
                while ((frame = codec.read()) != null) bytes += frame.audioData.length;
                double seconds = bytes / (format.getFrameRate() * format.getFrameSize());
                check(seconds > 0, "nonempty decoded PCM");
                System.out.println("DECODED_SECONDS=" + seconds + " PCM_BYTES=" + bytes + " FILE=" + file);
            } finally {
                codec.cleanup();
            }
        }
        System.out.println("Gramophone0332Probe PASS (policy/decoder, not listening acceptance)");
    }
}
