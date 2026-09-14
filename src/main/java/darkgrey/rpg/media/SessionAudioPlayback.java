package darkgrey.rpg.media;

import darkgrey.rpg.network.message.canonical.CanonicalSessionFrame;
import darkgrey.rpg.session.runtime.CanonicalSessionPresentation;

/** Clock-driven playback policy. Backend owns only this Session's audio channels. */
public final class SessionAudioPlayback {

    public interface Backend {

        boolean ready(String ref);

        boolean play(String channel, String ref, boolean loop);

        void volume(String channel, float volume);

        void stop(String channel);
    }

    private final Backend backend;
    private CanonicalSessionFrame frame;
    private long musicRevision = -1;
    private long lineEpoch = -1;
    private boolean voiceConsumed;
    private boolean musicStarted;
    private boolean suspended;
    private String musicChannel = "dgr_session_music_a";
    private String fadingChannel;
    private double musicStartedAt;
    private double fadeStartedAt;
    private double fadeDuration;
    private float fadeStartVolume;
    private float currentVolume;
    private static final String VOICE = "dgr_session_voice";

    public SessionAudioPlayback(Backend backend) {
        this.backend = backend;
    }

    public void present(CanonicalSessionFrame update, double now) {
        if (frame != null && (frame.getTransportId() != update.getTransportId() || !frame.getStoryId()
            .equals(update.getStoryId()))) clear();
        CanonicalSessionPresentation state = update.getPresentation();
        if (musicRevision != state.getMusicRevision()) {
            stopFading();
            if (musicStarted) {
                fadingChannel = musicChannel;
                fadeStartVolume = currentVolume;
                fadeStartedAt = now;
                fadeDuration = state.getFadeOut();
                musicChannel = musicChannel.endsWith("_a") ? "dgr_session_music_b" : "dgr_session_music_a";
            }
            musicRevision = state.getMusicRevision();
            musicStarted = false;
        }
        if (frame == null || lineEpoch != update.getLineEpoch()
            || update.getKind() != CanonicalSessionFrame.Kind.LINE) {
            backend.stop(VOICE);
            lineEpoch = update.getLineEpoch();
            voiceConsumed = !update.shouldPlayVoice();
        } else if (!update.shouldPlayVoice()) {
            backend.stop(VOICE);
            voiceConsumed = true;
        }
        frame = update;
        tick(now, !suspended);
    }

    public void advance() {
        backend.stop(VOICE);
        voiceConsumed = true;
    }

    public void tick(double now, boolean alive) {
        if (frame == null) return;
        if (!alive) {
            if (!suspended) {
                backend.stop(musicChannel);
                stopFading();
                advance();
                musicStarted = false;
            }
            suspended = true;
            return;
        }
        suspended = false;
        CanonicalSessionPresentation state = frame.getPresentation();
        if (fadingChannel != null) {
            double fraction = fadeDuration <= 0 ? 1 : Math.max(0, (now - fadeStartedAt) / fadeDuration);
            if (fraction >= 1) stopFading();
            else backend.volume(fadingChannel, (float) (fadeStartVolume * (1 - fraction)));
        }
        if (!musicStarted && state.getMusicRef() != null && backend.ready(state.getMusicRef())) {
            musicStarted = backend.play(musicChannel, state.getMusicRef(), state.isLoop());
            if (musicStarted) musicStartedAt = now;
        }
        if (musicStarted) {
            currentVolume = state.getFadeIn() <= 0 ? 1
                : (float) Math.min(1, Math.max(0, (now - musicStartedAt) / state.getFadeIn()));
            backend.volume(musicChannel, currentVolume);
        }
        if (!voiceConsumed && frame.getVoiceRef() != null && backend.ready(frame.getVoiceRef())) {
            voiceConsumed = backend.play(VOICE, frame.getVoiceRef(), false);
        }
        backend.volume(VOICE, 1);
    }

    /** Sound engine reload restarts music, but never repeats the active line. */
    public void engineReset() {
        backend.stop(musicChannel);
        stopFading();
        advance();
        musicStarted = false;
    }

    private void stopFading() {
        if (fadingChannel != null) backend.stop(fadingChannel);
        fadingChannel = null;
    }

    public void clear() {
        backend.stop(VOICE);
        backend.stop(musicChannel);
        stopFading();
        frame = null;
        musicRevision = -1;
        lineEpoch = -1;
        voiceConsumed = true;
        musicStarted = false;
        suspended = false;
    }
}
