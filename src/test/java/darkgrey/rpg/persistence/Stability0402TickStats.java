package darkgrey.rpg.persistence;

/** No MC references; bounded complete-tick observation read by the driver at the following tick. */
public final class Stability0402TickStats {

    private static long started;
    public static volatile long completed, lastNanos;
    private static final boolean PROFILE = Boolean.getBoolean("dgr0402.profileDrivers");
    private static final long[] stages = new long[18], opened = new long[18];
    private static final StringBuilder slow = new StringBuilder(
        "full_tick_index,start_ns,wall_ms,full_tick_ns,mixed_callback_ns,authored_callback_ns,fixture_validation_ns,fixture_save_and_log_ns,mixed_operations_ns,fixture_initialization_ns,journal_persist_ns,journal_read_ns,player_checkpoint_ns,mc_world_save_ns,mc_players_save_ns,player_image_validation_ns,journal_compress_ns,task_commit_ns,journal_fsync_ns,checkpoint_write_ns,checkpoint_read_ns,forge_player_save_event_ns\n");
    private static int slowCount;

    private Stability0402TickStats() {}

    public static void begin() {
        started = System.nanoTime();
        if (PROFILE) java.util.Arrays.fill(stages, 0L);
    }

    public static void end() {
        lastNanos = System.nanoTime() - started;
        completed++;
        if (PROFILE && lastNanos >= 50000000L && slowCount++ < 20000) {
            slow.append(completed)
                .append(',')
                .append(started)
                .append(',')
                .append(System.currentTimeMillis())
                .append(',')
                .append(lastNanos);
            for (long value : stages) slow.append(',')
                .append(value);
            slow.append('\n');
        }
    }

    /** Test scopes overlap: callbacks include their operations, validation, and fixture I/O. */
    public static void stage(int scope, long nanos) {
        if (PROFILE) stages[scope] += nanos;
    }

    public static void open(int scope) {
        if (PROFILE) opened[scope] = System.nanoTime();
    }

    public static void close(int scope) {
        if (PROFILE) stage(scope, System.nanoTime() - opened[scope]);
    }

    public static void writeProfile(java.io.File root, long firstIndex, int samples) throws java.io.IOException {
        if (!PROFILE) return;
        java.nio.file.Files.write(
            new java.io.File(root, "driver-tick-profile.csv").toPath(),
            slow.toString()
                .getBytes(java.nio.charset.StandardCharsets.UTF_8));
        java.nio.file.Files.write(
            new java.io.File(root, "driver-tick-profile-inputs.json").toPath(),
            ("{\"first_complete_tick_index\":" + firstIndex
                + ",\"sample_count\":"
                + samples
                + ",\"scopes_overlap\":true,\"record_threshold_ns\":50000000,\"overflow\":"
                + Math.max(0, slowCount - 20000)
                + "}").getBytes(java.nio.charset.StandardCharsets.UTF_8));
    }
}
