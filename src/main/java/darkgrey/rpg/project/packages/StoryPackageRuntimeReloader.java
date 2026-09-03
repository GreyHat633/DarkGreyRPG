package darkgrey.rpg.project.packages;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

import darkgrey.rpg.project.ProjectLoadException;
import darkgrey.rpg.project.ProjectRepository;

/**
 * Publishes one validated Story Package set into the authoritative repository.
 * Package scanning and snapshot publication stay separate transactions; a
 * failed candidate or merge never replaces the active repository snapshot.
 */
public final class StoryPackageRuntimeReloader {

    private StoryPackageRuntimeReloader() {}

    /** Loads the base project first, then lets any accepted package set replace it. */
    public static Result startup(ProjectRepository repository, StoryPackageLoader loader) {
        require(repository, loader);
        ProjectRepository.ReloadResult base = repository.reload();
        StoryPackageLoader.ReloadResult packages = loader.reload();
        if (loader.getPackages()
            .isEmpty()) {
            if (!base.isSuccessful()) repository.installUnavailableSnapshot(base.getSummary());
            return new Result(packages.isSuccessful() && base.isSuccessful(), packages, base, null);
        }
        return publishAcceptedPackages(repository, loader, packages);
    }

    /** Reloads packages and atomically publishes the accepted set or restored base project. */
    public static Result reload(ProjectRepository repository, StoryPackageLoader loader) {
        require(repository, loader);
        StoryPackageLoader.ReloadResult packages = loader.reload();
        if (!loader.getPackages()
            .isEmpty()) return publishAcceptedPackages(repository, loader, packages);
        ProjectRepository.ReloadResult restoredBase = repository.reload();
        if (!restoredBase.isSuccessful()) repository.installUnavailableSnapshot(restoredBase.getSummary());
        return new Result(packages.isSuccessful() && restoredBase.isSuccessful(), packages, restoredBase, null);
    }

    private static Result publishAcceptedPackages(ProjectRepository repository, StoryPackageLoader loader,
        StoryPackageLoader.ReloadResult packages) {
        ProjectRepository.ReloadResult previous = repository.getLastReload();
        try {
            ProjectRepository.ReloadResult installed = repository
                .installSnapshot(StoryPackageSnapshotMerger.merge(loader.getPackages()));
            return new Result(packages.isSuccessful() && installed.isSuccessful(), packages, installed, null);
        } catch (ProjectLoadException exception) {
            return new Result(false, packages, previous, exception.getMessage());
        }
    }

    private static void require(ProjectRepository repository, StoryPackageLoader loader) {
        if (repository == null) throw new IllegalArgumentException("Project repository is required.");
        if (loader == null) throw new IllegalArgumentException("Story Package loader is required.");
    }

    public static final class Result {

        private final boolean successful;
        private final StoryPackageLoader.ReloadResult packageReload;
        private final ProjectRepository.ReloadResult projectReload;
        private final String publicationError;

        private Result(boolean successful, StoryPackageLoader.ReloadResult packageReload,
            ProjectRepository.ReloadResult projectReload, String publicationError) {
            this.successful = successful;
            this.packageReload = packageReload;
            this.projectReload = projectReload;
            this.publicationError = publicationError;
        }

        public boolean isSuccessful() {
            return successful;
        }

        public StoryPackageLoader.ReloadResult getPackageReload() {
            return packageReload;
        }

        public ProjectRepository.ReloadResult getProjectReload() {
            return projectReload;
        }

        public String getPublicationError() {
            return publicationError;
        }

        public List<String> getErrors() {
            List<String> errors = new ArrayList<String>(packageReload.getErrors());
            if (publicationError != null) errors.add(publicationError);
            if (!projectReload.isSuccessful()) errors.add(projectReload.getSummary());
            return Collections.unmodifiableList(errors);
        }
    }
}
