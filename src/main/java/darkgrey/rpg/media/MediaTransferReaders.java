package darkgrey.rpg.media;

import java.io.IOException;
import java.util.HashMap;
import java.util.Iterator;
import java.util.Map;

import darkgrey.rpg.network.message.canonical.CanonicalMediaChunk;
import darkgrey.rpg.network.message.canonical.CanonicalMediaRequest;
import darkgrey.rpg.project.packages.LoadedStoryPackage;

/** Bounded, leased readers. Caller performs authorization before and after worker IO. */
public final class MediaTransferReaders {

    private static final long TIMEOUT = 15_000_000_000L;
    private final Map<String, Entry> entries = new HashMap<String, Entry>();
    private static final int LIMIT = 64;
    private final java.util.function.LongSupplier clock;

    public MediaTransferReaders() {
        this(System::nanoTime);
    }

    public MediaTransferReaders(java.util.function.LongSupplier clock) {
        this.clock = clock;
    }

    public synchronized int activeCount() {
        return entries.size();
    }

    public CanonicalMediaChunk read(String owner, LoadedStoryPackage source, CanonicalMediaRequest request)
        throws IOException {
        String key = owner + ":" + request.getRequestId() + ":" + request.getMediaRef();
        Entry entry;
        Entry replaced = null;
        synchronized (this) {
            entry = entries.get(key);
            if (entry != null && entry.source != source) {
                replaced = entry;
                entries.remove(key);
                entry = null;
            }
            if (entry == null) {
                if (entries.size() >= LIMIT) return null;
                entry = new Entry(owner, source, clock.getAsLong());
                entries.put(key, entry);
            }
        }
        if (replaced != null) synchronized (replaced) {
            close(replaced);
        }
        synchronized (entry) {
            if (entry.closed) return null;
            try {
                if (entry.reader == null) {
                    long start = System.nanoTime();
                    entry.reader = source.openMediaReader(request.getMediaRef());
                    MediaLatencyTrace
                        .event("reader_open_verify", key, start, "generation=" + source.getContentFingerprint());
                    if (entry.reader == null) {
                        remove(key, entry);
                        return null;
                    }
                }
                entry.used = clock.getAsLong();
                CanonicalMediaChunk chunk = entry.reader.readChunk(request.getRequestId(), request.getOffset());
                if (chunk == null) remove(key, entry);
                else {
                    entry.chunks.set(chunk.getOffset() / CanonicalMediaChunk.CHUNK_BYTES);
                    if (entry.chunks.cardinality()
                        == (chunk.getTotal() + CanonicalMediaChunk.CHUNK_BYTES - 1) / CanonicalMediaChunk.CHUNK_BYTES)
                        remove(key, entry);
                }
                return chunk;
            } catch (IOException | RuntimeException failure) {
                remove(key, entry);
                throw failure;
            }
        }
    }

    private void remove(String key, Entry entry) {
        synchronized (this) {
            if (entries.get(key) == entry) entries.remove(key);
        }
        close(entry);
    }

    /** Runs on the cleanup worker, including when no new requests arrive. */
    public void expire() {
        release(null, clock.getAsLong());
    }

    public void releaseOwner(String owner) {
        release(owner, Long.MAX_VALUE);
    }

    private void release(String owner, long now) {
        java.util.List<Entry> removed = new java.util.ArrayList<Entry>();
        synchronized (this) {
            Iterator<Entry> it = entries.values()
                .iterator();
            while (it.hasNext()) {
                Entry e = it.next();
                if (owner != null ? owner.equals(e.owner) : now - e.used >= TIMEOUT) {
                    it.remove();
                    removed.add(e);
                }
            }
        }
        for (Entry e : removed) synchronized (e) {
            close(e);
        }
    }

    private static void close(Entry e) {
        e.closed = true;
        if (e.reader != null) try {
            e.reader.close();
        } catch (IOException ignored) {}
        e.reader = null;
    }

    private static final class Entry {

        final String owner;
        final LoadedStoryPackage source;
        volatile long used;
        LoadedStoryPackage.MediaReader reader;
        boolean closed;
        final java.util.BitSet chunks = new java.util.BitSet();

        Entry(String owner, LoadedStoryPackage source, long now) {
            this.owner = owner;
            this.source = source;
            this.used = now;
        }
    }
}
