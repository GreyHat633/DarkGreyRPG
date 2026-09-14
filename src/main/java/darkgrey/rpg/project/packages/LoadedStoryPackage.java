package darkgrey.rpg.project.packages;

import java.io.File;
import java.io.IOException;
import java.io.RandomAccessFile;
import java.nio.file.Path;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.Map;

import darkgrey.rpg.graph.canonical.CanonicalStoryLogicGraph;
import darkgrey.rpg.project.ProjectSnapshot;

/** Validated immutable package candidate exposed to server integration code. */
public final class LoadedStoryPackage {

    private final StoryPackageManifest manifest;
    private final File directory;
    private final File sourceArchive;
    private final ProjectSnapshot snapshot;
    private final CanonicalStoryLogicGraph storyLogicGraph;
    private final Map<String, byte[]> declaredResourceBytes;
    private final String contentFingerprint;
    private final DgrsGenerationStore generationStore;
    private final DgrsGenerationStore.Generation generation;
    private final DgrsArchiveReader mediaSource;
    private volatile boolean closed;

    LoadedStoryPackage(StoryPackageManifest manifest, File directory, ProjectSnapshot snapshot,
        CanonicalStoryLogicGraph storyLogicGraph, Map<String, byte[]> declaredResourceBytes)
        throws darkgrey.rpg.project.ProjectLoadException {
        this(manifest, directory, null, snapshot, storyLogicGraph, declaredResourceBytes);
    }

    LoadedStoryPackage(StoryPackageManifest manifest, File directory, File sourceArchive, ProjectSnapshot snapshot,
        CanonicalStoryLogicGraph storyLogicGraph, Map<String, byte[]> declaredResourceBytes)
        throws darkgrey.rpg.project.ProjectLoadException {
        StoryPackageMediaValidation.validate(manifest, declaredResourceBytes);
        this.manifest = manifest;
        this.directory = directory;
        this.sourceArchive = sourceArchive;
        this.snapshot = snapshot;
        this.storyLogicGraph = storyLogicGraph;
        Map<String, byte[]> detached = new LinkedHashMap<String, byte[]>();
        for (Map.Entry<String, byte[]> entry : declaredResourceBytes.entrySet()) detached.put(
            entry.getKey(),
            entry.getValue()
                .clone());
        this.declaredResourceBytes = Collections.unmodifiableMap(detached);
        contentFingerprint = StoryPackageContentFingerprint.compute(manifest, this.declaredResourceBytes);
        generationStore = null;
        generation = null;
        mediaSource = null;
    }

    LoadedStoryPackage(StoryPackageManifest manifest, File originalSourceArchive, DgrsGenerationStore generationStore,
        DgrsGenerationStore.Generation generation, ProjectSnapshot snapshot, CanonicalStoryLogicGraph storyLogicGraph,
        Map<String, byte[]> declaredResourceBytes, String contentFingerprint)
        throws darkgrey.rpg.project.ProjectLoadException {
        this.manifest = manifest;
        this.directory = null;
        this.sourceArchive = originalSourceArchive;
        this.snapshot = snapshot;
        this.storyLogicGraph = storyLogicGraph;
        this.declaredResourceBytes = Collections
            .unmodifiableMap(new LinkedHashMap<String, byte[]>(declaredResourceBytes));
        this.contentFingerprint = contentFingerprint;
        this.generationStore = generationStore;
        this.generation = generation;
        try {
            this.mediaSource = DgrsArchiveReader.open(
                generation.getArchive()
                    .toFile());
        } catch (darkgrey.rpg.project.ProjectLoadException exception) {
            try {
                generation.retire();
            } catch (IOException ignored) {}
            throw exception;
        }
    }

    public StoryPackageManifest getManifest() {
        return manifest;
    }

    public File getDirectory() {
        return directory;
    }

    public File getSourceArchive() {
        return sourceArchive;
    }

    public ProjectSnapshot getSnapshot() {
        return snapshot;
    }

    public String getPackageId() {
        return manifest.getPackageId();
    }

    public String getStoryId() {
        return manifest.getStoryId();
    }

    public String getContentFingerprint() {
        return contentFingerprint;
    }

    public CanonicalStoryLogicGraph getStoryLogicGraph() {
        return storyLogicGraph;
    }

    public darkgrey.rpg.network.message.canonical.CanonicalMediaChunk readMediaChunk(long requestId, String ref,
        int offset) {
        if (!manifest.getRequiredResources()
            .getMedia()
            .contains(ref)) return null;
        if (mediaSource != null) return readMediaChunkFromDisk(requestId, ref, offset);
        byte[] bytes = declaredResourceBytes.get(ref);
        if (bytes == null || offset < 0 || offset >= bytes.length || offset % 32768 != 0) return null;
        return new darkgrey.rpg.network.message.canonical.CanonicalMediaChunk(
            requestId,
            ref,
            bytes.length,
            offset,
            java.util.Arrays.copyOfRange(bytes, offset, Math.min(bytes.length, offset + 32768)));
    }

    byte[] getDeclaredResourceBytes(String path) {
        byte[] bytes = declaredResourceBytes.get(path);
        return bytes == null ? null : bytes.clone();
    }

    private darkgrey.rpg.network.message.canonical.CanonicalMediaChunk readMediaChunkFromDisk(long requestId,
        String ref, int offset) {
        if (closed || offset < 0 || offset % 32768 != 0) return null;
        try {
            long size = mediaSource.getEntrySize(ref);
            if (offset >= size || size > Integer.MAX_VALUE) return null;
            if (!generation.retain()) return null;
            try {
                Path file = generationStore.materialize(ref, mediaSource);
                int length = (int) Math.min(32768L, size - offset);
                byte[] chunk = new byte[length];
                try (RandomAccessFile input = new RandomAccessFile(file.toFile(), "r")) {
                    input.seek(offset);
                    input.readFully(chunk);
                }
                return new darkgrey.rpg.network.message.canonical.CanonicalMediaChunk(
                    requestId,
                    ref,
                    (int) size,
                    offset,
                    chunk);
            } finally {
                generation.release();
            }
        } catch (IOException | darkgrey.rpg.project.ProjectLoadException exception) {
            return null;
        }
    }

    /** Called by StoryPackageLoader once this package is no longer current. */
    void close() {
        if (closed) return;
        closed = true;
        if (generation != null) try {
            generation.retire();
        } catch (IOException ignored) {}
    }

    /** Pins the immutable source across an asynchronously queued media request. */
    public boolean retainMediaRequest() {
        return mediaSource == null || (!closed && generation.retain());
    }

    public void releaseMediaRequest() {
        if (generation != null) try {
            generation.release();
        } catch (IOException ignored) {}
    }
}
