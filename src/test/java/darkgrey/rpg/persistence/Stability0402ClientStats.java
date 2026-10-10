package darkgrey.rpg.persistence;

/** Complete rendered-frame intervals; sampler exists only in the isolated driver JAR. */
public final class Stability0402ClientStats {

    public static boolean enabled;
    private static long previous;
    private static final long[] intervals = new long[8192];
    private static int size;
    private static long overflow;

    private Stability0402ClientStats() {}

    public static void frame() {
        long now = System.nanoTime();
        if (!enabled) {
            previous = 0;
            return;
        }
        if (previous != 0) {
            if (size < intervals.length) intervals[size++] = now - previous;
            else overflow++;
        }
        previous = now;
    }

    public static long[] drain() {
        long[] result = java.util.Arrays.copyOf(intervals, size);
        size = 0;
        return result;
    }

    public static long overflow() {
        return overflow;
    }
}
