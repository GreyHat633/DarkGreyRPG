package darkgrey.rpg.project.packages;

import java.io.File;
import java.io.FileOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.Comparator;
import java.util.Enumeration;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Set;
import java.util.zip.ZipEntry;
import java.util.zip.ZipFile;

import darkgrey.rpg.graph.canonical.CanonicalStoryLogicGraph;
import darkgrey.rpg.graph.canonical.CanonicalStoryLogicGraphLoader;
import darkgrey.rpg.project.ProjectLoadException;
import darkgrey.rpg.project.ProjectRepository;

/**
 * Server-only package scanner. Each package is validated independently and
 * committed as one map swap, so a broken candidate cannot disable other
 * packages or erase a previously accepted definition.
 */
public final class StoryPackageLoader {

    private static final String DGRS_EXTENSION = ".dgrs";
    private static final String RUNTIME_CACHE_DIRECTORY = ".dgrs-runtime";
    private final File installDirectory;
    private volatile Map<String, LoadedStoryPackage> packages = Collections.emptyMap();
    private volatile Map<String, String> packageIdsBySourceName = Collections.emptyMap();
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
        File archive = new File(installDirectory, packageId + DGRS_EXTENSION);
        File source = archive.isFile() ? archive : new File(installDirectory, packageId);
        if (!source.exists()) {
            String mappedSourceName = findSourceName(packageId);
            if (mappedSourceName != null) source = new File(installDirectory, mappedSourceName);
        }
        Map<String, LoadedStoryPackage> next = new LinkedHashMap<String, LoadedStoryPackage>(packages);
        Map<String, String> nextSources = new LinkedHashMap<String, String>(packageIdsBySourceName);
        try {
            LoadedStoryPackage candidate = loadCandidateSource(source);
            if (!packageId.equals(candidate.getPackageId())) throw new ProjectLoadException(
                "Reloaded source declares package_id '" + candidate.getPackageId() + "'");
            next.put(candidate.getPackageId(), candidate);
            StoryPackageSnapshotMerger.merge(next);
            packages = Collections.unmodifiableMap(next);
            nextSources.put(sourceKey(source), candidate.getPackageId());
            packageIdsBySourceName = Collections.unmodifiableMap(nextSources);
            cleanupRuntimeCache(packages);
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
        Map<String, String> previousSources = packageIdsBySourceName;
        Map<String, LoadedStoryPackage> next = new LinkedHashMap<String, LoadedStoryPackage>();
        Map<String, String> nextSources = new LinkedHashMap<String, String>();
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
        for (File source : children) {
            if (source.isDirectory() && RUNTIME_CACHE_DIRECTORY.equals(source.getName())) continue;
            if (!source.isDirectory() && !isDgrs(source)) continue;
            try {
                LoadedStoryPackage candidate = loadCandidateSource(source);
                if (next.containsKey(candidate.getPackageId()))
                    throw new ProjectLoadException("Duplicate package_id '" + candidate.getPackageId() + "'");
                next.put(candidate.getPackageId(), candidate);
                nextSources.put(sourceKey(source), candidate.getPackageId());
            } catch (ProjectLoadException exception) {
                // A corrupt archive may no longer expose its manifest identity.
                // Retain the prior package by its successfully observed source
                // mapping; unpacked legacy packages still use their directory name.
                String packageId = previousSources.get(sourceKey(source));
                if (packageId == null && source.isDirectory()) packageId = source.getName();
                if (packageId == null && isDgrs(source)) packageId = archiveBaseName(source);
                LoadedStoryPackage retained = previous.get(packageId);
                if (retained != null && !next.containsKey(packageId)) {
                    next.put(packageId, retained);
                    nextSources.put(sourceKey(source), packageId);
                }
                errors.add(source.getName() + ": " + exception.getMessage());
            }
        }
        try {
            if (!next.isEmpty()) StoryPackageSnapshotMerger.merge(next);
        } catch (ProjectLoadException exception) {
            errors.add("Package set: " + exception.getMessage());
            next = previous;
            nextSources = previousSources;
        }
        packages = Collections.unmodifiableMap(new LinkedHashMap<String, LoadedStoryPackage>(next));
        packageIdsBySourceName = Collections.unmodifiableMap(new LinkedHashMap<String, String>(nextSources));
        cleanupRuntimeCache(packages);
        lastReload = errors.isEmpty() ? ReloadResult.success(next.size()) : ReloadResult.partial(next.size(), errors);
        return lastReload;
    }

    public ReloadResult getLastReload() {
        return lastReload;
    }

    private LoadedStoryPackage loadCandidateSource(File source) throws ProjectLoadException {
        if (isDgrs(source)) return loadArchiveCandidate(source);
        return loadCandidate(source, source.getName(), false);
    }

    private LoadedStoryPackage loadArchiveCandidate(File archive) throws ProjectLoadException {
        File runtimeRoot = new File(installDirectory, RUNTIME_CACHE_DIRECTORY);
        if (!runtimeRoot.exists() && !runtimeRoot.mkdirs())
            throw new ProjectLoadException("Cannot create DGRS runtime cache: " + runtimeRoot);
        File candidate = new File(runtimeRoot, archiveBaseName(archive) + "-" + Long.toHexString(System.nanoTime()));
        if (!candidate.mkdir()) throw new ProjectLoadException("Cannot create DGRS staging directory: " + candidate);
        try {
            extractArchive(archive, candidate);
            ensureLegacyProjectRoots(candidate);
            return loadCandidate(candidate, null, true);
        } catch (ProjectLoadException exception) {
            deleteTree(candidate);
            throw exception;
        }
    }

    private LoadedStoryPackage loadCandidate(File directory, String expectedPackageId, boolean requireDgrs)
        throws ProjectLoadException {
        StoryPackageManifest manifest = StoryPackageManifest.read(new File(directory, "manifest.json"));
        if (requireDgrs && !manifest.isDgrsV1()) throw new ProjectLoadException("Archive manifest is not DGRS v1");
        if (expectedPackageId != null && !manifest.getPackageId()
            .equals(expectedPackageId))
            throw new ProjectLoadException("Package source must be named '" + manifest.getPackageId() + "'");
        validateRequiredFiles(directory, manifest);
        ProjectRepository repository = new ProjectRepository(directory);
        ProjectRepository.ReloadResult result = repository.reload();
        if (!result.isSuccessful()) throw new ProjectLoadException("Project validation failed: " + result.getSummary());
        if (requireDgrs) {
            if (repository.getSnapshot()
                .getCanonicalStory(manifest.getStoryId()) == null)
                throw new ProjectLoadException("Manifest story_id is not present in the DGRS canonical stories");
            if (repository.getSnapshot()
                .getCanonicalStoryMembership(manifest.getStoryId()) == null)
                throw new ProjectLoadException("Manifest story_id has no DGRS canonical membership");
        } else if (repository.getSnapshot()
            .getStory(manifest.getStoryId()) == null)
            throw new ProjectLoadException("Manifest story_id is not present in the package project");
        String logicPath = manifest.getRequiredResources()
            .getStoryLogicGraph();
        CanonicalStoryLogicGraph logicGraph;
        try {
            logicGraph = logicPath == null ? CanonicalStoryLogicGraph.empty()
                : new CanonicalStoryLogicGraphLoader()
                    .loadUnresolved(new File(directory, logicPath.replace('/', File.separatorChar)));
        } catch (darkgrey.rpg.graph.canonical.CanonicalGraphResourceException exception) {
            throw new ProjectLoadException(
                "Invalid Story Package public Logic graph: " + exception.getMessage(),
                exception);
        }
        for (darkgrey.rpg.graph.canonical.CanonicalStoryLogicConnection connection : logicGraph.getConnections())
            if (!manifest.getStoryId()
                .equals(connection.getSourceStoryId()))
                throw new ProjectLoadException(
                    "Story Package may only own public Logic connections sourced by its story_id.");
        return new LoadedStoryPackage(manifest, directory, repository.getSnapshot(), logicGraph);
    }

    private static boolean isDgrs(File file) {
        return file != null && file.isFile()
            && file.getName()
                .toLowerCase(Locale.ROOT)
                .endsWith(DGRS_EXTENSION);
    }

    private static String archiveBaseName(File archive) {
        String name = archive.getName();
        return name.substring(0, name.length() - DGRS_EXTENSION.length());
    }

    private String findSourceName(String packageId) {
        for (Map.Entry<String, String> entry : packageIdsBySourceName.entrySet())
            if (packageId.equals(entry.getValue())) return entry.getKey();
        return null;
    }

    private static String sourceKey(File source) {
        return source.getName();
    }

    private static void extractArchive(File archive, File destination) throws ProjectLoadException {
        Set<String> paths = new HashSet<String>();
        try {
            ZipFile zip = new ZipFile(archive);
            try {
                Enumeration<? extends ZipEntry> entries = zip.entries();
                while (entries.hasMoreElements()) {
                    ZipEntry entry = entries.nextElement();
                    String path = entry.getName();
                    validateEntryPath(path);
                    String normalized = path.toLowerCase(Locale.ROOT);
                    if (!paths.add(normalized))
                        throw new ProjectLoadException("DGRS contains duplicate normalized entry path '" + path + "'");
                    if (entry.isDirectory())
                        throw new ProjectLoadException("DGRS directory entries are not supported: " + path);
                    File target = new File(destination, path.replace('/', File.separatorChar));
                    ensureContained(destination, target, path);
                    File parent = target.getParentFile();
                    if (parent != null && !parent.exists() && !parent.mkdirs())
                        throw new ProjectLoadException("Cannot create DGRS entry directory: " + path);
                    InputStream input = zip.getInputStream(entry);
                    try {
                        OutputStream output = new FileOutputStream(target);
                        try {
                            byte[] buffer = new byte[8192];
                            int count;
                            while ((count = input.read(buffer)) >= 0) output.write(buffer, 0, count);
                        } finally {
                            output.close();
                        }
                    } finally {
                        input.close();
                    }
                }
            } finally {
                zip.close();
            }
        } catch (ProjectLoadException exception) {
            throw exception;
        } catch (IOException | RuntimeException exception) {
            throw new ProjectLoadException("Invalid DGRS archive " + archive, exception);
        }
    }

    private static void ensureLegacyProjectRoots(File destination) throws ProjectLoadException {
        for (String name : Arrays.asList("actors", "dialogues", "quests", "stories")) {
            File directory = new File(destination, name);
            if (!directory.exists() && !directory.mkdirs())
                throw new ProjectLoadException("Cannot create DGRS runtime directory: " + name);
            if (!directory.isDirectory())
                throw new ProjectLoadException("DGRS runtime path is not a directory: " + name);
        }
    }

    private static void validateEntryPath(String path) throws ProjectLoadException {
        if (path == null || path.length() == 0
            || path.indexOf('\\') >= 0
            || path.startsWith("/")
            || path.indexOf(':') >= 0) throw new ProjectLoadException("Unsafe DGRS entry path: " + path);
        String[] segments = path.split("/", -1);
        for (String segment : segments) if (segment.length() == 0 || ".".equals(segment) || "..".equals(segment))
            throw new ProjectLoadException("Unsafe DGRS entry path: " + path);
    }

    private static void ensureContained(File root, File target, String path) throws IOException, ProjectLoadException {
        String rootPath = root.getCanonicalPath() + File.separator;
        String targetPath = target.getCanonicalPath();
        if (!targetPath.startsWith(rootPath)) throw new ProjectLoadException("Unsafe DGRS entry path: " + path);
    }

    private void cleanupRuntimeCache(Map<String, LoadedStoryPackage> active) {
        File runtimeRoot = new File(installDirectory, RUNTIME_CACHE_DIRECTORY);
        File[] candidates = runtimeRoot.listFiles();
        if (candidates == null) return;
        Set<String> retained = new HashSet<String>();
        for (LoadedStoryPackage value : active.values()) try {
            File directory = value.getDirectory();
            if (directory.getParentFile() != null && runtimeRoot.getCanonicalFile()
                .equals(
                    directory.getParentFile()
                        .getCanonicalFile()))
                retained.add(directory.getCanonicalPath());
        } catch (IOException ignored) {}
        for (File candidate : candidates) try {
            if (!retained.contains(candidate.getCanonicalPath())) deleteTree(candidate);
        } catch (IOException ignored) {}
    }

    private static void deleteTree(File file) {
        if (file == null || !file.exists()) return;
        if (file.isDirectory()) {
            File[] children = file.listFiles();
            if (children != null) for (File child : children) deleteTree(child);
        }
        file.delete();
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
        if (manifest.getRequiredResources()
            .getStoryLogicGraph() != null)
            paths.add(
                manifest.getRequiredResources()
                    .getStoryLogicGraph());
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
