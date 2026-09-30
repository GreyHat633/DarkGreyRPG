package darkgrey.rpg.media;

/** Opt-in developer timings; never enabled by normal player configuration. */
public final class MediaLatencyTrace {

    public static final boolean ENABLED = Boolean.getBoolean("dgr.mediaTrace");

    private MediaLatencyTrace() {}

    private static String frameIdentity;
    private static long frameStart, lastDraw;

    public static void frame(String identity) {
        if (!ENABLED) return;
        frameIdentity = identity;
        frameStart = System.nanoTime();
        lastDraw = 0;
        event("frame_received", identity, 0, "accepted");
    }

    public static void draw() {
        if (!ENABLED) return;
        long now = System.nanoTime();
        if (frameStart != 0) {
            event("first_draw", frameIdentity, frameStart, "dialogue");
            frameStart = 0;
        }
        if (lastDraw != 0 && now - lastDraw > 50_000_000L) event("draw_gap", frameIdentity, lastDraw, "over_50ms");
        lastDraw = now;
    }

    public static void event(String stage, String identity, long started, String detail) {
        if (ENABLED) org.apache.logging.log4j.LogManager.getLogger("DGR-MediaTiming")
            .info(
                "stage={} id={} elapsed_us={} detail={} at_ns={}",
                stage,
                identity,
                started == 0 ? 0 : (System.nanoTime() - started) / 1000L,
                detail,
                System.nanoTime());
    }
}
