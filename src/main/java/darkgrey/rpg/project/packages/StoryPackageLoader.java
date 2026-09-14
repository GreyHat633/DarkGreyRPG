package darkgrey.rpg.project.packages;

import java.io.File;
import java.io.IOException;
import java.nio.file.DirectoryStream;
import java.nio.file.Files;
import java.nio.file.LinkOption;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.Comparator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;

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
    private final DgrsGenerationStore generationStore;
    private volatile Map<String, LoadedStoryPackage> packages = Collections.emptyMap();
    private volatile Map<String, String> packageIdsBySourceName = Collections.emptyMap();
    private volatile ReloadResult lastReload = ReloadResult.empty();

    public StoryPackageLoader(File installDirectory) {
        if (installDirectory == null) throw new IllegalArgumentException("installDirectory cannot be null");
        this.installDirectory = installDirectory.getAbsoluteFile();
        this.generationStore = new DgrsGenerationStore(
            this.installDirectory.toPath()
                .resolve(RUNTIME_CACHE_DIRECTORY));
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
        LoadedStoryPackage previousPackage = packages.get(packageId);
        Map<String, String> nextSources = new LinkedHashMap<String, String>(packageIdsBySourceName);
        LoadedStoryPackage candidate = null;
        try {
            candidate = loadCandidateSource(source);
            if (!packageId.equals(candidate.getPackageId())) throw new ProjectLoadException(
                "Reloaded source declares package_id '" + candidate.getPackageId() + "'");
            next.put(candidate.getPackageId(), candidate);
            StoryPackageSnapshotMerger.merge(next);
            packages = Collections.unmodifiableMap(next);
            nextSources.put(sourceKey(source), candidate.getPackageId());
            packageIdsBySourceName = Collections.unmodifiableMap(nextSources);
            if (previousPackage != null && previousPackage != candidate) previousPackage.close();
            sweepGenerations(next, null);
            lastReload = ReloadResult.success(next.size());
            return lastReload;
        } catch (ProjectLoadException exception) {
            if (candidate != null && candidate != previousPackage) candidate.close();
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
        try {
            generationStore.recoverParts();
        } catch (IOException exception) {
            errors.add("Cannot recover DGRS runtime temporary files: " + exception.getMessage());
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
            for (LoadedStoryPackage value : next.values()) if (!previous.containsValue(value)) value.close();
            next = previous;
            nextSources = previousSources;
        }
        packages = Collections.unmodifiableMap(new LinkedHashMap<String, LoadedStoryPackage>(next));
        packageIdsBySourceName = Collections.unmodifiableMap(new LinkedHashMap<String, String>(nextSources));
        for (Map.Entry<String, LoadedStoryPackage> old : previous.entrySet())
            if (next.get(old.getKey()) != old.getValue()) old.getValue()
                .close();
        sweepGenerations(next, errors);
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
        DgrsArchiveReader reader = DgrsArchiveReader.open(archive);
        reader.readUtf8("manifest.json");
        reader.readUtf8("project.json");
        StoryPackageManifest manifest = StoryPackageManifest
            .read(reader.readBytes("manifest.json"), reader.getSourceIdentity() + "!/manifest.json");
        if (!manifest.isDgrsV1()) throw new ProjectLoadException("Archive manifest is not DGRS v1");
        StoryPackageSnapshotReader.Result result = StoryPackageSnapshotReader.read(reader, manifest);
        if (result.getSnapshot()
            .getStory(manifest.getStoryId()) == null
            && result.getSnapshot()
                .getCanonicalStory(manifest.getStoryId()) == null)
            throw new ProjectLoadException("Manifest story_id is not present in the DGRS payload");
        if (result.getSnapshot()
            .getCanonicalStory(manifest.getStoryId()) != null
            && result.getSnapshot()
                .getCanonicalStoryMembership(manifest.getStoryId()) == null)
            throw new ProjectLoadException("Manifest story_id has no DGRS canonical membership");
        String fingerprint = StoryPackageContentFingerprint.compute(manifest, reader);
        DgrsGenerationStore.Generation generation;
        try {
            generation = generationStore.install(archive, fingerprint);
        } catch (IOException exception) {
            throw new ProjectLoadException("Cannot publish immutable DGRS generation", exception);
        }
        return new LoadedStoryPackage(
            manifest,
            archive.getAbsoluteFile(),
            generationStore,
            generation,
            result.getSnapshot(),
            result.getStoryLogicGraph(),
            result.getDeclaredBytes(),
            fingerprint);
    }

    private LoadedStoryPackage loadCandidate(File directory, String expectedPackageId, boolean requireDgrs)
        throws ProjectLoadException {
        StoryPackageManifest manifest = StoryPackageManifest.read(new File(directory, "manifest.json"));
        if (requireDgrs && !manifest.isDgrsV1()) throw new ProjectLoadException("Archive manifest is not DGRS v1");
        if (expectedPackageId != null && !darkgrey.rpg.identity.DgrResourceId.isFullId(manifest.getPackageId())
            && !manifest.getPackageId()
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
        return new LoadedStoryPackage(
            manifest,
            directory,
            repository.getSnapshot(),
            logicGraph,
            readDeclaredBytes(directory, manifest));
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

    private void sweepGenerations(Map<String, LoadedStoryPackage> current, List<String> errors) {
        java.util.Set<String> fingerprints = new java.util.HashSet<String>();
        for (LoadedStoryPackage value : current.values()) fingerprints.add(value.getContentFingerprint());
        try {
            generationStore.sweepGenerations(fingerprints);
        } catch (IOException exception) {
            if (errors != null) errors.add("Cannot sweep old DGRS generations: " + exception.getMessage());
        }
    }

    /** Legacy residue is now recovered as controlled generations and .part files. */
    private void cleanupRuntimeResidue(List<String> errors) {
        Path install = installDirectory.toPath()
            .toAbsolutePath()
            .normalize();
        Path residue = install.resolve(RUNTIME_CACHE_DIRECTORY)
            .normalize();
        if (!install.equals(residue.getParent())) {
            errors.add("Cannot safely inspect Story Package runtime residue: " + residue);
            return;
        }
        try {
            if (isLinkLike(install)) {
                errors
                    .add("Cannot safely inspect Story Package runtime residue through a symbolic/reparse install path");
                return;
            }
            if (!Files.exists(residue, LinkOption.NOFOLLOW_LINKS)) return;
            verifySafeResidue(residue);
            deleteSafeResidue(residue);
        } catch (IOException | RuntimeException exception) {
            errors
                .add("Cannot safely clean Story Package runtime residue '" + residue + "': " + exception.getMessage());
        }
    }

    private static void verifySafeResidue(Path path) throws IOException {
        if (isLinkLike(path)) throw new IOException("symbolic/reparse link encountered");
        if (!Files.isDirectory(path, LinkOption.NOFOLLOW_LINKS)) throw new IOException("residue is not a directory");
        try (DirectoryStream<Path> children = Files.newDirectoryStream(path)) {
            for (Path child : children) {
                if (isLinkLike(child)) throw new IOException("symbolic/reparse link encountered");
                if (Files.isDirectory(child, LinkOption.NOFOLLOW_LINKS)) verifySafeResidue(child);
            }
        }
    }

    private static void deleteSafeResidue(Path path) throws IOException {
        if (isLinkLike(path)) throw new IOException("symbolic/reparse link encountered");
        if (Files.isDirectory(path, LinkOption.NOFOLLOW_LINKS)) {
            try (DirectoryStream<Path> children = Files.newDirectoryStream(path)) {
                for (Path child : children) deleteSafeResidue(child);
            }
        }
        Files.deleteIfExists(path);
    }

    private static boolean isLinkLike(Path path) throws IOException {
        if (Files.isSymbolicLink(path)) return true;
        try {
            Map<String, Object> dos = Files.readAttributes(path, "dos:reparsePoint", LinkOption.NOFOLLOW_LINKS);
            if (Boolean.TRUE.equals(dos.get("reparsePoint"))) return true;
        } catch (UnsupportedOperationException | IllegalArgumentException exception) {
            // The DOS view is unavailable on non-Windows file systems.
        }
        try {
            return Files
                .readAttributes(path, java.nio.file.attribute.BasicFileAttributes.class, LinkOption.NOFOLLOW_LINKS)
                .isOther();
        } catch (UnsupportedOperationException exception) {
            return false;
        }
    }

    private static void validateRequiredFiles(File directory, StoryPackageManifest manifest)
        throws ProjectLoadException {
        for (String path : requiredPaths(manifest)) {
            File file = new File(directory, path.replace('/', File.separatorChar));
            if (!file.isFile()) throw new ProjectLoadException("Required package resource is missing: " + path);
        }
    }

    private static Map<String, byte[]> readDeclaredBytes(File directory, StoryPackageManifest manifest)
        throws ProjectLoadException {
        Map<String, byte[]> result = new LinkedHashMap<String, byte[]>();
        File project = new File(directory, "project.json");
        try {
            result.put("project.json", readBounded(project.toPath()));
        } catch (java.io.IOException exception) {
            throw new ProjectLoadException("Cannot read declared package resource: project.json", exception);
        }
        long mediaTotal = 0;
        for (String path : requiredPaths(manifest)) {
            File file = new File(directory, path.replace('/', File.separatorChar));
            try {
                long size = java.nio.file.Files.size(file.toPath());
                if (size > 64L * 1024 * 1024 || size > 256L * 1024 * 1024 - mediaTotal)
                    throw new java.io.IOException("Package resources exceed byte bounds");
                byte[] content = readBounded(file.toPath());
                mediaTotal += content.length;
                if (mediaTotal > 256L * 1024 * 1024)
                    throw new java.io.IOException("Package resources exceed total byte bound");
                result.put(path, content);
            } catch (java.io.IOException exception) {
                throw new ProjectLoadException("Cannot read declared package resource: " + path, exception);
            }
        }
        return result;
    }

    private static byte[] readBounded(java.nio.file.Path path) throws java.io.IOException {
        long maximum = 64L * 1024 * 1024;
        if (java.nio.file.Files.size(path) > maximum) throw new java.io.IOException("Package entry exceeds byte bound");
        try (java.io.InputStream input = java.nio.file.Files.newInputStream(path);
            java.io.ByteArrayOutputStream output = new java.io.ByteArrayOutputStream()) {
            byte[] buffer = new byte[8192];
            int count;
            while ((count = input.read(buffer)) != -1) {
                if ((long) output.size() + count > maximum)
                    throw new java.io.IOException("Package entry grew beyond byte bound");
                output.write(buffer, 0, count);
            }
            return output.toByteArray();
        }
    }

    private static List<String> requiredPaths(StoryPackageManifest manifest) {
        StoryPackageManifest.RequiredResources required = manifest.getRequiredResources();
        List<String> paths = new ArrayList<String>();
        paths.add(required.getStory());
        paths.addAll(required.getActors());
        paths.addAll(required.getItems());
        paths.addAll(required.getItemGroups());
        paths.addAll(required.getDialogues());
        paths.addAll(required.getQuests());
        paths.addAll(required.getCanonicalStories());
        paths.addAll(required.getCanonicalMemberships());
        paths.addAll(required.getSessions());
        paths.addAll(required.getTasks());
        paths.addAll(required.getMedia());
        if (required.getStoryLogicGraph() != null) paths.add(required.getStoryLogicGraph());
        return paths;
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
