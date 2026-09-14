package darkgrey.rpg.project.packages;

import java.io.File;
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
}
