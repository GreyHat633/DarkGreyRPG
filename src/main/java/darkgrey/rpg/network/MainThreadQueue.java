package darkgrey.rpg.network;

import java.util.ArrayDeque;
import java.util.ArrayList;
import java.util.List;
import java.util.function.BooleanSupplier;
import java.util.function.LongSupplier;

/** One FIFO with bounded admission and cooperative work between tasks. */
final class MainThreadQueue {

    enum Cancellation {
        FULL,
        RETIRED,
        FAILED
    }

    static final class Scope {

        volatile boolean active = true;
    }

    private static final class Entry {

        final Scope scope;
        final BooleanSupplier current;
        final Runnable action;
        final java.util.function.Consumer<Cancellation> cancel;
        final long submitted;
        final boolean completion;

        Entry(Scope scope, BooleanSupplier current, Runnable action, java.util.function.Consumer<Cancellation> cancel,
            long submitted, boolean completion) {
            this.scope = scope;
            this.current = current;
            this.action = action;
            this.cancel = cancel;
            this.submitted = submitted;
            this.completion = completion;
        }
    }

    private final ArrayDeque<Entry> entries = new ArrayDeque<>();
    private final int capacity, maximumPerTick;
    private final long nanosPerTick;
    private final LongSupplier clock;
    private final java.util.function.Consumer<RuntimeException> failures;
    private long accepted, completed, cancelled, rejected, failed, peak, slow, maxTaskNanos;
    private String slowestTask = "";
    private int ordinaryPending, completionPending;

    MainThreadQueue(int capacity, int maximumPerTick, long nanosPerTick, LongSupplier clock,
        java.util.function.Consumer<RuntimeException> failures) {
        if (capacity < 1 || maximumPerTick < 1 || nanosPerTick < 1)
            throw new IllegalArgumentException("Invalid queue budget");
        this.capacity = capacity;
        this.maximumPerTick = maximumPerTick;
        this.nanosPerTick = nanosPerTick;
        this.clock = clock;
        this.failures = failures;
    }

    boolean offer(Scope scope, BooleanSupplier current, Runnable action,
        java.util.function.Consumer<Cancellation> cancel) {
        return offer(scope, current, action, cancel, false);
    }

    boolean offerCompletion(Scope scope, BooleanSupplier current, Runnable action,
        java.util.function.Consumer<Cancellation> cancel) {
        return offer(scope, current, action, cancel, true);
    }

    private boolean offer(Scope scope, BooleanSupplier current, Runnable action,
        java.util.function.Consumer<Cancellation> cancel, boolean completion) {
        java.util.Objects.requireNonNull(scope);
        java.util.Objects.requireNonNull(action);
        java.util.Objects.requireNonNull(current);
        java.util.Objects.requireNonNull(cancel);
        Cancellation reason;
        synchronized (this) {
            if (!scope.active) reason = Cancellation.RETIRED;
            else if (completion ? completionPending >= 256 : ordinaryPending >= capacity) {
                rejected++;
                reason = Cancellation.FULL;
            } else {
                entries.addLast(new Entry(scope, current, action, cancel, clock.getAsLong(), completion));
                if (completion) completionPending++;
                else ordinaryPending++;
                accepted++;
                peak = Math.max(peak, entries.size());
                return true;
            }
        }
        cancel.accept(reason);
        return false;
    }

    void drain() {
        long started = clock.getAsLong();
        for (int count = 0; count < maximumPerTick; count++) {
            if (count > 0 && clock.getAsLong() - started >= nanosPerTick) return;
            Entry next;
            synchronized (this) {
                next = entries.pollFirst();
                if (next != null) {
                    if (next.completion) completionPending--;
                    else ordinaryPending--;
                }
            }
            if (next == null) return;
            long taskStarted = clock.getAsLong();
            boolean cancelling = false;
            try {
                if (!next.scope.active || !next.current.getAsBoolean()) {
                    cancelling = true;
                    synchronized (this) {
                        cancelled++;
                    }
                    next.cancel.accept(Cancellation.RETIRED);
                } else {
                    next.action.run();
                    synchronized (this) {
                        completed++;
                    }
                }
            } catch (RuntimeException failure) {
                if (!cancelling) {
                    synchronized (this) {
                        failed++;
                    }
                    try {
                        next.cancel.accept(Cancellation.FAILED);
                    } catch (RuntimeException callbackFailure) {
                        failure.addSuppressed(callbackFailure);
                    }
                }
                failures.accept(failure);
            } finally {
                long elapsed = clock.getAsLong() - taskStarted;
                synchronized (this) {
                    if (elapsed > maxTaskNanos) slowestTask = next.action.getClass()
                        .getName();
                    maxTaskNanos = Math.max(maxTaskNanos, elapsed);
                    if (elapsed >= nanosPerTick) slow++;
                }
            }
        }
    }

    void retire(Scope scope) {
        List<Entry> removed = new ArrayList<>();
        synchronized (this) {
            scope.active = false;
            java.util.Iterator<Entry> iterator = entries.iterator();
            while (iterator.hasNext()) {
                Entry next = iterator.next();
                if (next.scope == scope) {
                    iterator.remove();
                    if (next.completion) completionPending--;
                    else ordinaryPending--;
                    removed.add(next);
                    cancelled++;
                }
            }
        }
        for (Entry next : removed) {
            try {
                next.cancel.accept(Cancellation.RETIRED);
            } catch (RuntimeException failure) {
                failures.accept(failure);
            }
        }
    }

    synchronized long[] metrics() {
        return new long[] { entries.size(), peak, accepted, completed, rejected, cancelled, failed,
            entries.isEmpty() ? 0 : clock.getAsLong() - entries.peekFirst().submitted, slow, maxTaskNanos };
    }

    synchronized String slowestTask() {
        return slowestTask;
    }
}
