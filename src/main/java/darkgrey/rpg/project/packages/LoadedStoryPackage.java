package darkgrey.rpg.project.packages;

import java.io.File;

import darkgrey.rpg.project.ProjectSnapshot;

/** Validated immutable package candidate exposed to server integration code. */
public final class LoadedStoryPackage {

    private final StoryPackageManifest manifest;
    private final File directory;
    private final ProjectSnapshot snapshot;

    LoadedStoryPackage(StoryPackageManifest manifest, File directory, ProjectSnapshot snapshot) {
        this.manifest = manifest;
        this.directory = directory;
        this.snapshot = snapshot;
    }

    public StoryPackageManifest getManifest() {
        return manifest;
    }

    public File getDirectory() {
        return directory;
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
}
