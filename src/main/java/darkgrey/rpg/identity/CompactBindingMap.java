package darkgrey.rpg.identity;

import java.util.LinkedHashMap;
import java.util.Map;

/** Ordered map with conservative allocation tracking; production does not reflect into the JDK. */
public final class CompactBindingMap<K, V> extends LinkedHashMap<K, V> {

    private static final long serialVersionUID = 1L;
    private static final int MAX_BUCKETS = 1 << 30;
    private final int initialBuckets;
    private int bucketLowerBound;

    public CompactBindingMap() {
        this(16);
    }

    private CompactBindingMap(int buckets) {
        super(buckets);
        initialBuckets = buckets;
    }

    @Override
    public V put(K key, V value) {
        V previous = super.put(key, value);
        if (bucketLowerBound == 0) bucketLowerBound = initialBuckets;
        while (bucketLowerBound < MAX_BUCKETS && size() * 4L > bucketLowerBound * 3L) bucketLowerBound *= 2;
        return previous;
    }

    @Override
    public void putAll(Map<? extends K, ? extends V> values) {
        for (Map.Entry<? extends K, ? extends V> entry : values.entrySet()) put(entry.getKey(), entry.getValue());
    }

    public boolean canCompact() {
        return compactBuckets() < bucketLowerBound;
    }

    public CompactBindingMap<K, V> compacted() {
        if (!canCompact()) return this;
        CompactBindingMap<K, V> replacement = new CompactBindingMap<K, V>(Math.max(16, compactBuckets()));
        replacement.putAll(this);
        return replacement;
    }

    private int compactBuckets() {
        if (isEmpty()) return 0;
        // At least 64 avoids collision-triggered growth while rebuilding small maps on Java 8.
        int buckets = 64;
        while (buckets < MAX_BUCKETS && size() * 4L > buckets * 3L) buckets *= 2;
        return buckets;
    }
}
