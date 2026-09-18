package darkgrey.rpg.gramophone;

/** Per-device personal clock policy; shared media never implies shared playback. */
public final class GramophonePlayback {

    public interface Backend {

        boolean start();

        void pause();

        void resume();

        void volume(float volume);

        void stop();
    }

    private final Backend backend;
    private boolean started;
    private boolean paused;
    private float level;
    private double last = Double.NaN;

    public GramophonePlayback(Backend backend) {
        this.backend = backend;
    }

    public static boolean contains(int dimension, int deviceDimension, int x, int y, int z, int radius, double px,
        double py, double pz) {
        return radius >= 0 && dimension == deviceDimension
            && Double.isFinite(px)
            && Double.isFinite(py)
            && Double.isFinite(pz)
            && Math.abs(Math.floor(px) - x) <= radius
            && Math.abs(Math.floor(py) - y) <= radius
            && Math.abs(Math.floor(pz) - z) <= radius;
    }

    public void tick(double now, boolean inside, boolean enabled, boolean powered) {
        double dt = Double.isNaN(last) ? 0 : Math.max(0, Math.min(1, now - last));
        last = now;
        boolean wanted = inside && enabled;
        if (!wanted && paused) {
            clear();
            return;
        }
        if (wanted && !powered) {
            if (started && !paused) {
                backend.pause();
                paused = true;
            }
            return;
        }
        if (wanted && !started) started = backend.start();
        if (wanted && paused) {
            backend.resume();
            paused = false;
        }
        if (!started) return;
        level = (float) Math.max(0, Math.min(1, level + (wanted ? dt : -dt) / 0.6));
        backend.volume(level);
        if (!wanted && level <= 0) clear();
    }

    public void clear() {
        if (started) backend.stop();
        started = false;
        paused = false;
        level = 0;
    }

    public boolean started() {
        return started;
    }

    public boolean paused() {
        return paused;
    }
}
