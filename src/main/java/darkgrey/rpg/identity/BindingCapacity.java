package darkgrey.rpg.identity;

import java.util.function.LongSupplier;

/** Transient sizing policy, not a persisted quota or a player-facing setting. */
public final class BindingCapacity {

    public static final long DEFAULT_CAPACITY = 4096;
    public static final long STEP = 512;
    public static final long SHRINK_DELAY_NANOS = 300_000_000_000L;
    private final LongSupplier clock;
    private long capacity = DEFAULT_CAPACITY;
    private int highWaterCount;
    private boolean lowUsage;
    private long lowSince;

    public BindingCapacity(LongSupplier clock) {
        if (clock == null) throw new IllegalArgumentException("Monotonic clock is required.");
        this.clock = clock;
    }

    public long capacity() {
        return capacity;
    }

    public void loaded(int count) {
        capacity = capacityForCount(count);
        highWaterCount = count;
        lowUsage = false;
    }

    public static long capacityForCount(int count) {
        if (count < 0) throw new IllegalArgumentException("Binding count is invalid.");
        return Math.max(DEFAULT_CAPACITY, roundUp(count * 4L / 3L + 1));
    }

    public void changed(int count, boolean canCompact) {
        capacity = Math.max(capacity, capacityForCount(count));
        highWaterCount = Math.max(highWaterCount, count);
        boolean eligible = count * 2L <= capacity - STEP && capacity > DEFAULT_CAPACITY
            || canCompact && count * 2L <= highWaterCount;
        if (!eligible) lowUsage = false;
        else if (!lowUsage) {
            lowUsage = true;
            lowSince = clock.getAsLong();
        }
    }

    public boolean maintenanceDue(int count, boolean canCompact) {
        changed(count, canCompact);
        return lowUsage && clock.getAsLong() - lowSince >= SHRINK_DELAY_NANOS;
    }

    /** Called only after all replacement maps have been built successfully. */
    public void maintenanceComplete(int count) {
        capacity = Math.min(capacity, Math.max(DEFAULT_CAPACITY, roundUp(count * 2L)));
        highWaterCount = count;
        lowUsage = false;
    }

    private static long roundUp(long value) {
        return (value + STEP - 1) / STEP * STEP;
    }
}
