package darkgrey.rpg.project.packages;

import java.io.File;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.Comparator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

import darkgrey.rpg.project.ProjectLoadException;
import darkgrey.rpg.project.ProjectRepository;

/**
 * Server-only package scanner. Each package is validated independently and
 * committed as one map swap, so a broken candidate cannot disable other
 * packages or erase a previously accepted definition.
 */
public final class StoryPackageLoader {

    private final File installDirectory;
    private volatile Map<String, LoadedStoryPackage> packages = Collections.emptyMap();
    private volatile ReloadResult lastReload = ReloadResult.empty();

    public StoryPackageLoader(File installDirectory) {
        if (installDirectory == null) throw new IllegalArgumentException("installDirectory cannot be null");
        this.installDirectory = installDirectory.getAbsoluteFile();
    }

    public StoryPackageLoader(String installDirectory) {
        this(new File(installDirectory));
    }

    public File getInstallDirectory() {
        return installDirectory;
    }

    public Map<String, LoadedStoryPackage> getPackages() {
        return packages;
    }

    public Map<String, LoadedStoryPackage> getInstalledPackages() {
        return getPackages();
    }

    public LoadedStoryPackage getPackage(String packageId) {
        return packages.get(packageId);
    }

    public LoadedStoryPackage getPackageForStory(String storyId) {
        for (LoadedStoryPackage value : packages.values()) if (value.getStoryId()
            .equals(storyId)) return value;
        return null;
    }

    public ReloadResult load() {
        return reload();
    }

    /** Validates and atomically replaces one package without touching others. */
    public synchronized ReloadResult reloadPackage(String packageId) {
        if (packageId == null || packageId.trim()
            .isEmpty()) return ReloadResult.failure(Collections.<String>emptyList(), "packageId is required");
        File directory = new File(installDirectory, packageId);
        Map<String, LoadedStoryPackage> next = new LinkedHashMap<String, LoadedStoryPackage>(packages);
        try {
            LoadedStoryPackage candidate = loadCandidate(directory);
            next.put(candidate.getPackageId(), candidate);
            StoryPackageSnapshotMerger.merge(next);
            packages = Collections.unmodifiableMap(next);
            lastReload = ReloadResult.success(next.size());
            return lastReload;
        } catch (ProjectLoadException exception) {
            List<String> errors = new ArrayList<String>();
            errors.add(packageId + ": " + exception.getMessage());
            lastReload = ReloadResult.partial(next.size(), errors);
            return lastReload;
        }
    }

    public synchronized ReloadResult reload() {
        Map<String, LoadedStoryPackage> previous = packages;
        Map<String, LoadedStoryPackage> next = new LinkedHashMap<String, LoadedStoryPackage>();
        List<String> errors = new ArrayList<String>();
        if (!installDirectory.exists() && !installDirectory.mkdirs()) {
            lastReload = ReloadResult
                .failure(errors, "Cannot create Story Package install directory: " + installDirectory);
            return lastReload;
        }
        if (!installDirectory.isDirectory()) {
            lastReload = ReloadResult
                .failure(errors, "Story Package install path is not a directory: " + installDirectory);
            return lastReload;
        }
        File[] children = installDirectory.listFiles();
        if (children == null) {
            lastReload = ReloadResult
                .failure(errors, "Cannot list Story Package install directory: " + installDirectory);
            return lastReload;
        }
        Arrays.sort(children, Comparator.comparing(File::getName, String.CASE_INSENSITIVE_ORDER));
        for (File directory : children) {
            if (!directory.isDirectory()) continue;
            try {
                LoadedStoryPackage candidate = loadCandidate(directory);
                if (next.containsKey(candidate.getPackageId()))
                    throw new ProjectLoadException("Duplicate package_id '" + candidate.getPackageId() + "'");
                next.put(candidate.getPackageId(), candidate);
            } catch (ProjectLoadException exception) {
                // The directory name is the only identity available when the
                // candidate manifest cannot be parsed. Valid packages require
                // directory == package_id, so it is safe to retain by name.
                String packageId = directory.getName();
                LoadedStoryPackage retained = previous.get(packageId);
                if (retained != null && !next.containsKey(packageId)) next.put(packageId, retained);
                errors.add(directory.getName() + ": " + exception.getMessage());
            }
        }
        try {
            if (!next.isEmpty()) StoryPackageSnapshotMerger.merge(next);
        } catch (ProjectLoadException exception) {
            errors.add("Package set: " + exception.getMessage());
            next = previous;
        }
        packages = Collections.unmodifiableMap(new LinkedHashMap<String, LoadedStoryPackage>(next));
        lastReload = errors.isEmpty() ? ReloadResult.success(next.size()) : ReloadResult.partial(next.size(), errors);
        return lastReload;
    }

    public ReloadResult getLastReload() {
        return lastReload;
    }

    private LoadedStoryPackage loadCandidate(File directory) throws ProjectLoadException {
        StoryPackageManifest manifest = StoryPackageManifest.read(new File(directory, "manifest.json"));
        if (!manifest.getPackageId()
            .equals(directory.getName()))
            throw new ProjectLoadException("Package directory must be named '" + manifest.getPackageId() + "'");
        validateRequiredFiles(directory, manifest);
        ProjectRepository repository = new ProjectRepository(directory);
        ProjectRepository.ReloadResult result = repository.reload();
        if (!result.isSuccessful()) throw new ProjectLoadException("Project validation failed: " + result.getSummary());
        if (repository.getSnapshot()
            .getStory(manifest.getStoryId()) == null)
            throw new ProjectLoadException("Manifest story_id is not present in the package project");
        return new LoadedStoryPackage(manifest, directory, repository.getSnapshot());
    }

    private static void validateRequiredFiles(File directory, StoryPackageManifest manifest)
        throws ProjectLoadException {
        List<String> paths = new ArrayList<String>();
        paths.add(
            manifest.getRequiredResources()
                .getStory());
        paths.addAll(
            manifest.getRequiredResources()
                .getActors());
        paths.addAll(
            manifest.getRequiredResources()
                .getItems());
        paths.addAll(
            manifest.getRequiredResources()
                .getItemGroups());
        paths.addAll(
            manifest.getRequiredResources()
                .getDialogues());
        paths.addAll(
            manifest.getRequiredResources()
                .getQuests());
        paths.addAll(
            manifest.getRequiredResources()
                .getCanonicalStories());
        paths.addAll(
            manifest.getRequiredResources()
                .getCanonicalMemberships());
        paths.addAll(
            manifest.getRequiredResources()
                .getSessions());
        paths.addAll(
            manifest.getRequiredResources()
                .getTasks());
        for (String path : paths) {
            File file = new File(directory, path.replace('/', File.separatorChar));
            if (!file.isFile()) throw new ProjectLoadException("Required package resource is missing: " + path);
        }
    }

    public static final class ReloadResult {

        private final boolean successful;
        private final int packageCount;
        private final List<String> errors;
        private final String summary;

        private ReloadResult(boolean successful, int packageCount, List<String> errors, String summary) {
            this.successful = successful;
            this.packageCount = packageCount;
            this.errors = Collections.unmodifiableList(new ArrayList<String>(errors));
            this.summary = summary;
        }

        static ReloadResult empty() {
            return new ReloadResult(true, 0, Collections.<String>emptyList(), "No Story Packages loaded");
        }

        static ReloadResult success(int count) {
            return new ReloadResult(
                true,
                count,
                Collections.<String>emptyList(),
                "Loaded " + count + " Story Package(s)");
        }

        static ReloadResult partial(int count, List<String> errors) {
            return new ReloadResult(
                false,
                count,
                errors,
                "Loaded " + count + " Story Package(s); " + errors.size() + " package(s) rejected");
        }

        static ReloadResult failure(List<String> errors, String summary) {
            return new ReloadResult(false, 0, errors, summary);
        }

        public boolean isSuccessful() {
            return successful;
        }

        public int getPackageCount() {
            return packageCount;
        }

        public List<String> getErrors() {
            return errors;
        }

        public String getSummary() {
            return summary;
        }
    }
}
