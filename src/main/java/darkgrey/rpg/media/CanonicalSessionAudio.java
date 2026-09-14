package darkgrey.rpg.media;

import java.lang.reflect.Field;
import java.nio.file.Path;
import java.util.HashMap;
import java.util.Map;

import net.minecraft.client.Minecraft;
import net.minecraft.client.audio.SoundCategory;
import net.minecraft.client.audio.SoundManager;

import darkgrey.rpg.network.message.canonical.CanonicalSessionFrame;
import paulscode.sound.SoundSystem;

/** Streams cached Vorbis through Minecraft's existing audio engine, using DGR-owned sources only. */
public final class CanonicalSessionAudio {

    private static final Map<String, String> PINS = new HashMap<String, String>();
    private static SoundSystem engine;
    private static final SessionAudioPlayback PLAYBACK = new SessionAudioPlayback(new SessionAudioPlayback.Backend() {

        @Override
        public boolean ready(String ref) {
            return engine != null && CanonicalMediaClient.ready(ref) != null;
        }

        @Override
        public boolean play(String channel, String ref, boolean loop) {
            Path path = CanonicalMediaClient.ready(ref);
            if (engine == null || path == null || !CanonicalMediaClient.pin(ref)) return false;
            stop(channel);
            try {
                engine.newStreamingSource(
                    true,
                    channel,
                    path.toUri()
                        .toURL(),
                    ref,
                    loop,
                    0,
                    0,
                    0,
                    0,
                    0);
                engine.setVolume(channel, 0);
                engine.play(channel);
                PINS.put(channel, ref);
                return true;
            } catch (Exception exception) {
                CanonicalMediaClient.unpin(ref);
                try {
                    engine.removeSource(channel);
                } catch (RuntimeException ignored) {}
                return false;
            }
        }

        @Override
        public void volume(String channel, float value) {
            if (engine == null || !PINS.containsKey(channel)) return;
            SoundCategory category = channel.endsWith("voice") ? SoundCategory.PLAYERS : SoundCategory.MUSIC;
            try {
                engine.setVolume(channel, value * Minecraft.getMinecraft().gameSettings.getSoundLevel(category));
            } catch (RuntimeException ignored) {}
        }

        @Override
        public void stop(String channel) {
            try {
                if (engine != null) {
                    engine.stop(channel);
                    engine.removeSource(channel);
                }
            } catch (RuntimeException ignored) {}
            String ref = PINS.remove(channel);
            if (ref != null) CanonicalMediaClient.unpin(ref);
        }
    });

    private CanonicalSessionAudio() {}

    private static double now() {
        return System.nanoTime() / 1000000000.0;
    }

    public static void present(CanonicalSessionFrame frame) {
        refreshEngine();
        PLAYBACK.present(frame, now());
    }

    public static void advance() {
        PLAYBACK.advance();
    }

    public static void clear() {
        PLAYBACK.clear();
    }

    public static void tick() {
        refreshEngine();
        Minecraft mc = Minecraft.getMinecraft();
        PLAYBACK.tick(
            now(),
            mc.theWorld != null && mc.thePlayer != null && !mc.thePlayer.isDead && mc.thePlayer.getHealth() > 0);
    }

    private static void refreshEngine() {
        SoundSystem found = null;
        try {
            Object manager = fieldOfType(
                Minecraft.getMinecraft()
                    .getSoundHandler(),
                SoundManager.class);
            if (manager != null) found = (SoundSystem) fieldOfType(manager, SoundSystem.class);
        } catch (ReflectiveOperationException | RuntimeException ignored) {}
        if (found != engine) {
            PLAYBACK.engineReset();
            engine = found;
        }
    }

    private static Object fieldOfType(Object owner, Class<?> type) throws IllegalAccessException {
        for (Field field : owner.getClass()
            .getDeclaredFields()) if (type.isAssignableFrom(field.getType())) {
                field.setAccessible(true);
                return field.get(owner);
            }
        return null;
    }
}
