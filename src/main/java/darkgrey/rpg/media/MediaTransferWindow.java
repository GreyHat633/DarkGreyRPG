package darkgrey.rpg.media;

import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.TreeMap;

import darkgrey.rpg.network.message.canonical.CanonicalMediaChunk;

/** Main-thread-owned bounded receive window, including chunks being written to disk. */
final class MediaTransferWindow {

    static final int LIMIT = 8;
    private static final long RETRY_NS = 2_000_000_000L;
    private final Map<Integer, Long> sent = new LinkedHashMap<Integer, Long>();
    private final TreeMap<Integer, CanonicalMediaChunk> received = new TreeMap<Integer, CanonicalMediaChunk>();
    private int total = -1, next, committed;
    private List<CanonicalMediaChunk> writing = Collections.emptyList();

    List<Integer> requests(long now) {
        List<Integer> result = new ArrayList<Integer>();
        for (Map.Entry<Integer, Long> entry : sent.entrySet()) {
            if (!received.containsKey(entry.getKey()) && !isWriting(entry.getKey())
                && now - entry.getValue() >= RETRY_NS) {
                entry.setValue(now);
                result.add(entry.getKey());
            }
        }
        int end = total < 0 ? 1 : total;
        while (sent.size() < LIMIT && next < end) {
            sent.put(next, now);
            result.add(next);
            next += CanonicalMediaChunk.CHUNK_BYTES;
        }
        return result;
    }

    boolean accept(CanonicalMediaChunk chunk) {
        if (!sent.containsKey(chunk.getOffset()) || received.containsKey(chunk.getOffset())
            || isWriting(chunk.getOffset())) return false;
        if (total >= 0 && total != chunk.getTotal()) throw new IllegalArgumentException("Changed media total");
        total = chunk.getTotal();
        received.put(chunk.getOffset(), chunk);
        return true;
    }

    List<CanonicalMediaChunk> take() {
        if (!writing.isEmpty()) throw new IllegalStateException("Concurrent disk write");
        List<CanonicalMediaChunk> batch = new ArrayList<CanonicalMediaChunk>();
        int offset = committed;
        while (received.containsKey(offset)) {
            CanonicalMediaChunk chunk = received.remove(offset);
            batch.add(chunk);
            offset += Math.min(CanonicalMediaChunk.CHUNK_BYTES, chunk.getTotal() - chunk.getOffset());
        }
        writing = batch;
        return batch;
    }

    void written() {
        for (CanonicalMediaChunk chunk : writing) {
            sent.remove(chunk.getOffset());
            committed += Math.min(CanonicalMediaChunk.CHUNK_BYTES, chunk.getTotal() - chunk.getOffset());
        }
        writing = Collections.emptyList();
    }

    private boolean isWriting(int offset) {
        for (CanonicalMediaChunk chunk : writing) if (chunk.getOffset() == offset) return true;
        return false;
    }

    void clear() {
        sent.clear();
        received.clear();
        writing = Collections.emptyList();
    }

    int retained() {
        return sent.size();
    }
}
