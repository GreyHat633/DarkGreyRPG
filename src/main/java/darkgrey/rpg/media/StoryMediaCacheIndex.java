package darkgrey.rpg.media;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.AtomicMoveNotSupportedException;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardCopyOption;
import java.nio.file.StandardOpenOption;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Set;
import java.util.UUID;

import com.google.gson.Gson;
import com.google.gson.JsonArray;
import com.google.gson.JsonElement;
import com.google.gson.JsonObject;
import com.google.gson.JsonParser;
import com.google.gson.JsonPrimitive;

import darkgrey.rpg.graph.canonical.CanonicalMediaReference;

/**
 * Thread-safe policy state for the story media package cache.
 *
 * <p>
 * The index owns package metadata, queue order, verified reference metadata,
 * and the ten-package/three-download policy. It does not touch media files;
 * callers own pinning, deletion, and the actual transfer work.
 * </p>
 */
public final class StoryMediaCacheIndex {

    private static final int MAX_ADMITTED = 10;
    private static final int MAX_DOWNLOADING = 3;
    /* Ten manifests may each carry thousands of canonical references. */
    private static final long MAX_JOURNAL_BYTES = 8L * 1024L * 1024L;

    private final LinkedHashMap<String, Entry> admitted = new LinkedHashMap<String, Entry>();
    private final LinkedHashMap<String, Entry> queued = new LinkedHashMap<String, Entry>();
    private final Set<String> verifiedRefs = new HashSet<String>();
    private final Set<String> runningIds = new HashSet<String>();
    private final LinkedHashSet<String> evictedRefs = new LinkedHashSet<String>();
    private long sequence;

    public StoryMediaCacheIndex() {}

    /**
     * Offers or refreshes a package descriptor. The running argument updates
     * the current running set; a later setRunning call is authoritative.
     */
    public synchronized void offer(String id, String version, List<String> refs, boolean running) {
        validateDescriptor(id, version, refs);
        List<String> copy = copyRefs(refs);
        Entry existing = admitted.get(id);
        if (existing != null) {
            if (!existing.version.equals(version) || !existing.refs.equals(copy)) {
                Entry replacement = new Entry(this, id, version, copy, existing.lastUsed, sequence++, false);
                admitted.put(id, replacement);
                for (String ref : existing.refs) if (!ownedRef(ref)) evictedRefs.add(ref);
            } else {
                existing.lastUsed = stamp();
                existing.dormant = false;
            }
        } else {
            existing = queued.get(id);
            if (existing != null) {
                if (!existing.version.equals(version) || !existing.refs.equals(copy)) {
                    queued.put(id, new Entry(this, id, version, copy, existing.lastUsed, existing.order, false));
                }
            } else {
                queued.put(id, new Entry(this, id, version, copy, stamp(), sequence++, false));
            }
        }
        if (running) runningIds.add(id);
        else runningIds.remove(id);
    }

    /** Replaces the protected running set, including IDs that are still queued. */
    public synchronized void setRunning(Set<String> ids) {
        runningIds.clear();
        if (ids != null) runningIds.addAll(ids);
    }

    /** Ends preload while retaining metadata and in-memory verification. */
    public synchronized void suspend() {
        queued.clear();
        runningIds.clear();
        for (Entry entry : admitted.values()) {
            entry.dormant = true;
            entry.downloading = false;
        }
    }

    /** Pauses one package, retaining its descriptor and verification metadata. */
    public synchronized void pause(String id) {
        Entry entry = admitted.get(id);
        if (entry != null) {
            entry.dormant = true;
            entry.downloading = false;
        } else {
            queued.remove(id);
        }
        runningIds.remove(id);
    }

    /** Admits queued packages in running-first insertion order where policy permits. */
    public synchronized void pump() {
        scheduleDownloads();
        for (;;) {
            Entry candidate = nextAdmissibleCandidate();
            if (candidate == null) break;
            if (admitted.size() >= MAX_ADMITTED && evictOne() == null) break;
            queued.remove(candidate.id);
            candidate.lastUsed = stamp();
            candidate.dormant = false;
            candidate.downloading = !ready(candidate);
            admitted.put(candidate.id, candidate);
            scheduleDownloads();
        }
        scheduleDownloads();
    }

    /** Records a verified content-addressed reference for all packages using it. */
    public synchronized void markReady(String ref) {
        validateRef(ref);
        verifiedRefs.add(ref);
        for (Entry entry : admitted.values()) if (ready(entry)) entry.downloading = false;
    }

    /** Removes a verified reference after the caller detects invalid or deleted content. */
    public synchronized void invalidate(String ref) {
        validateRef(ref);
        verifiedRefs.remove(ref);
    }

    /** Returns the currently admitted package descriptors in admission order. */
    public synchronized List<Entry> entries() {
        return Collections.unmodifiableList(new ArrayList<Entry>(admitted.values()));
    }

    /** Returns admitted packages which still have at least one unverified reference. */
    public synchronized List<Entry> downloading() {
        List<Entry> result = new ArrayList<Entry>();
        for (Entry entry : admitted.values()) if (entry.downloading) result.add(entry);
        return Collections.unmodifiableList(result);
    }

    /** Returns every reference owned by an admitted package, including shared references. */
    public synchronized Set<String> ownedRefs() {
        Set<String> result = new LinkedHashSet<String>();
        for (Entry entry : admitted.values()) result.addAll(entry.refs);
        return Collections.unmodifiableSet(result);
    }

    /**
     * Drains references orphaned by eviction. References still owned by an
     * admitted package remain pending for a later drain.
     */
    public synchronized List<String> drainEvictedRefs() {
        List<String> result = new ArrayList<String>();
        java.util.Iterator<String> iterator = evictedRefs.iterator();
        while (iterator.hasNext()) {
            String ref = iterator.next();
            if (!ownedRef(ref)) {
                result.add(ref);
                // The caller may defer physical deletion, so forget this
                // verification now and require a fresh file check on reuse.
                verifiedRefs.remove(ref);
                iterator.remove();
            }
        }
        return Collections.unmodifiableList(result);
    }

    /** Refreshes the LRU position of an admitted package. */
    public synchronized void touch(String id) {
        Entry entry = admitted.get(id);
        if (entry != null) entry.lastUsed = stamp();
    }

    public synchronized boolean admits(String id) {
        return admitted.containsKey(id);
    }

    public synchronized int queuedCount() {
        return queued.size();
    }

    /** Drops all not-yet-admitted descriptors, for a disconnect or session reset. */
    public synchronized void clearQueued() {
        queued.clear();
    }

    /**
     * Removes a package descriptor from either state. References are offered
     * to the same orphan drain used for eviction; the caller still decides
     * whether a pinned file may actually be deleted.
     */
    public synchronized void remove(String id) {
        Entry removed = admitted.remove(id);
        if (removed == null) removed = queued.remove(id);
        else queued.remove(id);
        runningIds.remove(id);
        if (removed != null) for (String ref : removed.refs) if (!ownedRef(ref)) evictedRefs.add(ref);
    }

    /** Atomically replaces a JSON metadata snapshot. Verified/active state is not serialized. */
    public void save(Path journal) throws IOException {
        if (journal == null) throw new IllegalArgumentException("journal is null");
        JsonObject root = new JsonObject();
        root.addProperty("schema", 1);
        JsonArray packages = new JsonArray();
        synchronized (this) {
            for (Entry entry : admitted.values()) {
                JsonObject value = new JsonObject();
                value.addProperty("id", entry.id);
                value.addProperty("version", entry.version);
                value.addProperty("lru", entry.lastUsed);
                JsonArray refs = new JsonArray();
                for (String ref : entry.refs) refs.add(new JsonPrimitive(ref));
                value.add("refs", refs);
                packages.add(value);
            }
        }
        root.add("entries", packages);
        byte[] bytes = new Gson().toJson(root)
            .getBytes(StandardCharsets.UTF_8);
        Path absolute = journal.toAbsolutePath()
            .normalize();
        Path parent = absolute.getParent();
        if (parent != null) Files.createDirectories(parent);
        Path temporary = absolute.resolveSibling(
            absolute.getFileName()
                .toString() + ".tmp-"
                + UUID.randomUUID());
        try {
            Files.write(temporary, bytes, StandardOpenOption.CREATE_NEW, StandardOpenOption.WRITE);
            try {
                Files.move(temporary, absolute, StandardCopyOption.ATOMIC_MOVE, StandardCopyOption.REPLACE_EXISTING);
            } catch (AtomicMoveNotSupportedException unsupported) {
                Files.move(temporary, absolute, StandardCopyOption.REPLACE_EXISTING);
            }
        } finally {
            Files.deleteIfExists(temporary);
        }
    }

    /**
     * Loads admitted metadata and LRU order. Malformed journals are ignored as
     * an empty index; ready bits and running flags are intentionally discarded.
     */
    public static StoryMediaCacheIndex load(Path journal) {
        StoryMediaCacheIndex result = new StoryMediaCacheIndex();
        if (journal == null) throw new IllegalArgumentException("journal is null");
        try {
            Path path = journal.toAbsolutePath()
                .normalize();
            if (!Files.isRegularFile(path) || Files.size(path) > MAX_JOURNAL_BYTES) return result;
            JsonElement parsed = new JsonParser().parse(new String(Files.readAllBytes(path), StandardCharsets.UTF_8));
            if (parsed == null || !parsed.isJsonObject()) return result;
            JsonElement entries = parsed.getAsJsonObject()
                .get("entries");
            if (entries == null || !entries.isJsonArray()) return result;
            List<Loaded> loaded = new ArrayList<Loaded>();
            for (JsonElement element : entries.getAsJsonArray()) {
                Loaded value = parseLoaded(element);
                if (value != null) loaded.add(value);
            }
            Collections.sort(loaded, new Comparator<Loaded>() {

                @Override
                public int compare(Loaded left, Loaded right) {
                    return Long.compare(left.lru, right.lru);
                }
            });
            for (Loaded value : loaded) {
                if (result.admitted.containsKey(value.id) || result.admitted.size() >= MAX_ADMITTED) continue;
                long lru = value.lru < 0 ? result.stamp() : value.lru;
                result.sequence = Math.max(result.sequence, lru);
                result.admitted.put(
                    value.id,
                    new Entry(result, value.id, value.version, value.refs, lru, result.sequence++, true));
            }
        } catch (IOException | RuntimeException malformed) {
            return new StoryMediaCacheIndex();
        }
        return result;
    }

    private Entry nextAdmissibleCandidate() {
        List<Entry> candidates = new ArrayList<Entry>(queued.values());
        Collections.sort(candidates, new Comparator<Entry>() {

            @Override
            public int compare(Entry left, Entry right) {
                boolean leftRunning = runningIds.contains(left.id);
                boolean rightRunning = runningIds.contains(right.id);
                if (leftRunning != rightRunning) return leftRunning ? -1 : 1;
                return Long.compare(left.order, right.order);
            }
        });
        int activeDownloads = downloadingCount();
        for (Entry entry : candidates) {
            if (!ready(entry) && activeDownloads >= MAX_DOWNLOADING) continue;
            if (admitted.size() < MAX_ADMITTED || hasEvictable()) return entry;
        }
        return null;
    }

    private Entry evictOne() {
        Entry candidate = null;
        for (Entry entry : admitted.values()) {
            if (runningIds.contains(entry.id) || entry.downloading) continue;
            if (candidate == null || entry.lastUsed < candidate.lastUsed
                || (entry.lastUsed == candidate.lastUsed && entry.order < candidate.order)) candidate = entry;
        }
        if (candidate == null) return null;
        admitted.remove(candidate.id);
        for (String ref : candidate.refs) if (!ownedRef(ref)) evictedRefs.add(ref);
        return candidate;
    }

    private boolean hasEvictable() {
        for (Entry entry : admitted.values()) if (!runningIds.contains(entry.id) && !entry.downloading) return true;
        return false;
    }

    private int downloadingCount() {
        int count = 0;
        for (Entry entry : admitted.values()) if (entry.downloading) count++;
        return count;
    }

    private void scheduleDownloads() {
        int active = downloadingCount();
        List<Entry> pending = new ArrayList<Entry>();
        for (Entry entry : admitted.values()) {
            if (!entry.dormant && !entry.downloading && !ready(entry)) pending.add(entry);
        }
        Collections.sort(pending, new Comparator<Entry>() {

            @Override
            public int compare(Entry left, Entry right) {
                boolean leftRunning = runningIds.contains(left.id);
                boolean rightRunning = runningIds.contains(right.id);
                if (leftRunning != rightRunning) return leftRunning ? -1 : 1;
                return Long.compare(left.order, right.order);
            }
        });
        for (Entry entry : pending) {
            if (active >= MAX_DOWNLOADING) break;
            entry.downloading = true;
            active++;
        }
    }

    private boolean ownedRef(String ref) {
        for (Entry entry : admitted.values()) if (entry.refs.contains(ref)) return true;
        return false;
    }

    private boolean ready(Entry entry) {
        if (entry.dormant) return false;
        for (String ref : entry.refs) if (!verifiedRefs.contains(ref)) return false;
        return true;
    }

    private long stamp() {
        if (sequence == Long.MAX_VALUE) sequence = 0;
        return ++sequence;
    }

    private static List<String> copyRefs(List<String> refs) {
        LinkedHashSet<String> unique = new LinkedHashSet<String>();
        for (String ref : refs) {
            validateRef(ref);
            unique.add(ref);
        }
        return Collections.unmodifiableList(new ArrayList<String>(unique));
    }

    private static void validateDescriptor(String id, String version, List<String> refs) {
        if (!validText(id) || !validText(version) || refs == null)
            throw new IllegalArgumentException("Invalid story media descriptor");
    }

    private static boolean validText(String value) {
        if (value == null || value.length() == 0 || value.length() > 1024) return false;
        for (int index = 0; index < value.length(); index++)
            if (Character.isISOControl(value.charAt(index))) return false;
        return true;
    }

    private static void validateRef(String ref) {
        if (!CanonicalMediaReference.isValid(ref))
            throw new IllegalArgumentException("Invalid media reference: " + ref);
    }

    private static Loaded parseLoaded(JsonElement element) {
        try {
            if (element == null || !element.isJsonObject()) return null;
            JsonObject object = element.getAsJsonObject();
            JsonElement idElement = object.get("id");
            JsonElement versionElement = object.get("version");
            JsonElement refsElement = object.get("refs");
            JsonElement lruElement = object.get("lru");
            if (idElement == null || versionElement == null
                || refsElement == null
                || !idElement.isJsonPrimitive()
                || !versionElement.isJsonPrimitive()
                || !refsElement.isJsonArray()
                || lruElement == null
                || !lruElement.isJsonPrimitive()) return null;
            String id = idElement.getAsString();
            String version = versionElement.getAsString();
            if (!validText(id) || !validText(version)) return null;
            JsonPrimitive lruPrimitive = lruElement.getAsJsonPrimitive();
            if (!lruPrimitive.isNumber()) return null;
            long lru = lruPrimitive.getAsLong();
            if (lru < 0) return null;
            List<String> refs = new ArrayList<String>();
            for (JsonElement refElement : refsElement.getAsJsonArray()) {
                if (refElement == null || !refElement.isJsonPrimitive()) return null;
                String ref = refElement.getAsString();
                validateRef(ref);
                if (!refs.contains(ref)) refs.add(ref);
            }
            return new Loaded(id, version, Collections.unmodifiableList(refs), lru);
        } catch (RuntimeException malformed) {
            return null;
        }
    }

    public static final class Entry {

        public final String id;
        public final String version;
        public final List<String> refs;
        private final StoryMediaCacheIndex owner;
        private final long order;
        private long lastUsed;
        private boolean dormant;
        private boolean downloading;

        private Entry(StoryMediaCacheIndex owner, String id, String version, List<String> refs, long lastUsed,
            long order, boolean dormant) {
            this.owner = owner;
            this.id = id;
            this.version = version;
            this.refs = refs;
            this.lastUsed = lastUsed;
            this.order = order;
            this.dormant = dormant;
            this.downloading = false;
        }

        public boolean isReady() {
            synchronized (owner) {
                return owner.admitted.get(id) == this && owner.ready(this);
            }
        }

        public boolean isRunning() {
            synchronized (owner) {
                return owner.admitted.get(id) == this && owner.runningIds.contains(id);
            }
        }
    }

    private static final class Loaded {

        final String id;
        final String version;
        final List<String> refs;
        final long lru;

        Loaded(String id, String version, List<String> refs, long lru) {
            this.id = id;
            this.version = version;
            this.refs = refs;
            this.lru = lru;
        }
    }
}
