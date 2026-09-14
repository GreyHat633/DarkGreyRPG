package darkgrey.rpg.media;

import java.io.IOException;
import java.nio.file.DirectoryStream;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.StandardOpenOption;
import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.HashSet;
import java.util.List;
import java.util.Set;

import darkgrey.rpg.graph.canonical.CanonicalMediaReference;

/** Independent disk budgets; live pins always win over eviction. Called only on the media worker. */
public final class MediaCacheRetention {

    public enum Kind {
        MUSIC,
        VOICE,
        IMAGE
    }

    private final VerifiedMediaCache cache;
    private final int musicCount;
    private final long voiceBytes;
    private final long imageBytes;

    public MediaCacheRetention(VerifiedMediaCache cache) {
        this(cache, 8, 128L * 1024 * 1024, 256L * 1024 * 1024);
    }

    public MediaCacheRetention(VerifiedMediaCache cache, int musicCount, long voiceBytes, long imageBytes) {
        if (musicCount < 0 || voiceBytes < 0 || imageBytes < 0)
            throw new IllegalArgumentException("Invalid retention budgets");
        this.cache = cache;
        this.musicCount = musicCount;
        this.voiceBytes = voiceBytes;
        this.imageBytes = imageBytes;
    }

    public void touch(String ref, Kind kind) throws IOException {
        if (!CanonicalMediaReference.isValid(ref)) throw new IllegalArgumentException("Invalid media reference");
        Path marker = marker(ref, kind);
        Files.createDirectories(marker.getParent());
        Files.write(marker, new byte[0], StandardOpenOption.CREATE, StandardOpenOption.TRUNCATE_EXISTING);
    }

    private Path marker(String ref, Kind kind) {
        return cache.root()
            .resolve("usage")
            .resolve(ref.substring(6) + "." + kind.name());
    }

    public void sweep() throws IOException {
        Path media = cache.root()
            .resolve("media");
        if (!Files.isDirectory(media)) return;
        List<Entry> entries = new ArrayList<Entry>();
        try (DirectoryStream<Path> files = Files.newDirectoryStream(media)) {
            for (Path path : files) {
                String ref = "media/" + path.getFileName()
                    .toString();
                if (!CanonicalMediaReference.isValid(ref) || !Files.isRegularFile(path) || Files.isSymbolicLink(path))
                    continue;
                Entry entry = new Entry(ref, Files.size(path));
                boolean marked = false;
                for (Kind kind : Kind.values()) {
                    Path marker = marker(ref, kind);
                    if (Files.isRegularFile(marker)) {
                        entry.used[kind.ordinal()] = Files.getLastModifiedTime(marker)
                            .toMillis();
                        marked = true;
                    }
                }
                if (!marked) entry.used[(ref.endsWith(".ogg") ? Kind.VOICE : Kind.IMAGE).ordinal()] = Files
                    .getLastModifiedTime(path)
                    .toMillis();
                entries.add(entry);
            }
        }
        Set<String> keep = new HashSet<String>();
        for (final Kind kind : Kind.values()) {
            List<Entry> ordered = new ArrayList<Entry>();
            for (Entry entry : entries) if (entry.used[kind.ordinal()] != 0) ordered.add(entry);
            Collections.sort(ordered, new Comparator<Entry>() {

                @Override
                public int compare(Entry left, Entry right) {
                    int order = Long.compare(right.used[kind.ordinal()], left.used[kind.ordinal()]);
                    return order == 0 ? left.ref.compareTo(right.ref) : order;
                }
            });
            long used = 0;
            int count = 0;
            for (Entry entry : ordered) {
                boolean retain = kind == Kind.MUSIC ? count < musicCount
                    : used + entry.size <= (kind == Kind.VOICE ? voiceBytes : imageBytes);
                if (retain) {
                    keep.add(entry.ref);
                    used += entry.size;
                    count++;
                }
            }
        }
        for (Entry entry : entries) if (!keep.contains(entry.ref) && cache.deleteIfUnpinned(entry.ref))
            for (Kind kind : Kind.values()) Files.deleteIfExists(marker(entry.ref, kind));
    }

    private static final class Entry {

        final String ref;
        final long size;
        final long[] used = new long[Kind.values().length];

        Entry(String ref, long size) {
            this.ref = ref;
            this.size = size;
        }
    }
}
