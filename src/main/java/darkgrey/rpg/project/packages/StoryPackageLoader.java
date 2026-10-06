package darkgrey.rpg.project.packages;

import java.io.File;
import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.AtomicMoveNotSupportedException;
import java.nio.file.Files;
import java.nio.file.LinkOption;
import java.nio.file.Path;
import java.nio.file.StandardCopyOption;
import java.nio.file.StandardOpenOption;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.Comparator;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.UUID;

import com.google.gson.JsonElement;
import com.google.gson.JsonObject;

import darkgrey.rpg.graph.canonical.CanonicalStoryLogicConnection;
import darkgrey.rpg.graph.canonical.CanonicalStoryLogicGraph;
import darkgrey.rpg.project.ProjectLoadException;

/** Complete installed-container inventory. UID overlaps block every claimant; no old-content fallback. */
public final class StoryPackageLoader {

    private final File installDirectory;
    private final DgrsGenerationStore generationStore;
    private final Path settingsPath;
    private volatile Map<String, LoadedStoryPackage> packages = Collections.emptyMap();
    private volatile List<StoryPackageInventoryEntry> inventory = Collections.emptyList();
    private volatile ReloadResult lastReload = ReloadResult.empty();
    private long revision;
    private Map<String, Boolean> enabled = new LinkedHashMap<String, Boolean>();

    public StoryPackageLoader(File installDirectory) {
        this(
            installDirectory,
            new File(
                installDirectory.getAbsoluteFile()
                    .getParentFile(),
                "Cache"));
    }

    public StoryPackageLoader(File installDirectory, File cacheDirectory) {
        if (installDirectory == null || cacheDirectory == null)
            throw new IllegalArgumentException("Package and cache directories are required");
        this.installDirectory = installDirectory.getAbsoluteFile();
        generationStore = new DgrsGenerationStore(
            PackageRuntimeMigration.cacheRoot(this.installDirectory, cacheDirectory));
        settingsPath = this.installDirectory.toPath()
            .getParent()
            .resolve("Config")
            .resolve("story-packages.json");
    }

    public StoryPackageLoader(String directory) {
        this(new File(directory));
    }

    public File getInstallDirectory() {
        return installDirectory;
    }

    public Map<String, LoadedStoryPackage> getPackages() {
        return packages;
    }

    public Map<String, LoadedStoryPackage> getInstalledPackages() {
        return packages;
    }

    public LoadedStoryPackage getPackage(String uid) {
        return packages.get(uid);
    }

    public LoadedStoryPackage getPackageForStory(String uid) {
        return packages.get(uid);
    }

    public List<StoryPackageInventoryEntry> getInventory() {
        return inventory;
    }

    public synchronized long getInventoryRevision() {
        return revision;
    }

    public synchronized String deletionFingerprint(String sourceName, long expectedRevision) throws IOException {
        requireCurrentSource(sourceName, expectedRevision);
        return DgrsGenerationStore.containerHash(managedSource(sourceName).toFile());
    }

    /**
     * Stop exact members before removing the confirmed physical source; history is owned by the retirement callback.
     */
    public synchronized void deleteContainer(String sourceName, long expectedRevision, String fingerprint,
        java.util.function.Consumer<Map<String, LoadedStoryPackage>> retire) throws IOException {
        StoryPackageInventoryEntry row = requireCurrentSource(sourceName, expectedRevision);
        Path source = managedSource(sourceName);
        if (!DgrsGenerationStore.containerHash(source.toFile())
            .equals(fingerprint)) throw new IOException("Source changed since deletion confirmation; rescan first");
        Map<String, LoadedStoryPackage> retained = new LinkedHashMap<String, LoadedStoryPackage>(packages);
        for (String uid : row.getStoryUids()) retained.remove(uid);
        retire.accept(Collections.unmodifiableMap(retained));
        for (String uid : row.getStoryUids()) {
            LoadedStoryPackage previous = packages.get(uid);
            if (previous != null) previous.close();
        }
        packages = Collections.unmodifiableMap(retained);
        revision++;
        // The same-directory rename pins the actual source selected for deletion. A racing
        // replacement fails the second fingerprint check and is restored without overwriting.
        Path staged = source.resolveSibling(
            ".delete-" + UUID.randomUUID()
                .toString() + ".pending");
        try {
            Files.move(source, staged, StandardCopyOption.ATOMIC_MOVE);
            if (!DgrsGenerationStore.containerHash(staged.toFile())
                .equals(fingerprint))
                throw new IOException("Source changed while deleting; original bytes were retained");
            Files.delete(staged);
        } catch (IOException failure) {
            if (Files.exists(staged) && !Files.exists(source)) Files.move(staged, source);
            throw failure;
        } finally {
            reload();
        }
    }

    private Path managedSource(String sourceName) throws IOException {
        Path root = installDirectory.toPath()
            .toAbsolutePath()
            .normalize();
        Path source = root.resolve(sourceName)
            .normalize();
        if (!source.getParent()
            .equals(root)
            || !source.getFileName()
                .toString()
                .equals(sourceName)
            || isLink(root)
            || !Files.isRegularFile(source, LinkOption.NOFOLLOW_LINKS)
            || isLink(source)) throw new IOException("Container source is not a managed physical file");
        return source;
    }

    public ReloadResult getLastReload() {
        return lastReload;
    }

    public ReloadResult load() {
        return reload();
    }

    /** Fail closed if the accepted set cannot be published after its per-container validation. */
    synchronized void rejectPublication(String message) {
        List<StoryPackageInventoryEntry> rejected = new ArrayList<StoryPackageInventoryEntry>();
        for (StoryPackageInventoryEntry row : inventory) {
            if (!row.isValid()) {
                rejected.add(row);
                continue;
            }
            rejected.add(
                new StoryPackageInventoryEntry(
                    row.getSourceName(),
                    row.getDisplayName(),
                    row.getStoryUids(),
                    row.isUserEnabled(),
                    Collections.singletonList(message),
                    row.getConflicts()));
        }
        for (LoadedStoryPackage member : packages.values()) member.close();
        packages = Collections.emptyMap();
        inventory = Collections.unmodifiableList(rejected);
        revision++;
        lastReload = ReloadResult.failure(Collections.singletonList(message), message);
    }

    // Every reload rechecks the complete claim set, including disabled files.
    public synchronized ReloadResult reloadPackage(String uid) {
        return reload();
    }

    public boolean allowsNewStart(String uid) {
        for (StoryPackageInventoryEntry entry : inventory) if (entry.getStoryUids()
            .contains(uid) && entry.allowsNewStarts()) return true;
        return false;
    }

    public synchronized ReloadResult reload() {
        List<String> errors = new ArrayList<String>();
        List<Candidate> candidates = new ArrayList<Candidate>();
        try {
            if (!installDirectory.exists() && !installDirectory.mkdirs())
                throw new IOException("Cannot create package directory");
            if (!installDirectory.isDirectory() || isLink(installDirectory.toPath()))
                throw new IOException("Package install directory is unavailable or unsafe");
            File[] files = installDirectory.listFiles();
            if (files == null) throw new IOException("Cannot enumerate package directory");
            enabled = readSettings();
            Arrays.sort(files, Comparator.comparing(File::getName, String.CASE_INSENSITIVE_ORDER));
            for (File source : files) {
                String name = source.getName()
                    .toLowerCase(Locale.ROOT);
                if (!name.endsWith(".dgrs") && !name.endsWith(".dgrs.g")) continue;
                if (candidates.size() >= 4096) throw new IOException("Installed container count exceeds 4096");
                Candidate candidate = new Candidate(source, enabled.getOrDefault(source.getName(), true));
                candidates.add(candidate);
                try {
                    if (!source.isFile() || isLink(source.toPath()))
                        throw new ProjectLoadException("Container must be a physical file");
                    byte[] manifestBytes = DgrsArchiveReader.readManifestClaims(source);
                    if (name.endsWith(".dgrs.g")) candidate.manifests
                        .putAll(StoryGroupPackageReader.readManifests(manifestBytes, source.toString()));
                    else {
                        StoryPackageManifest manifest = StoryPackageManifest.read(manifestBytes, source.toString());
                        candidate.manifests.put(manifest.getStoryId(), manifest);
                    }
                } catch (Exception exception) {
                    candidate.errors.add(exception.getMessage());
                }
            }
        } catch (Exception exception) {
            // A failed directory scan is not evidence that installed content disappeared.
            lastReload = ReloadResult.failure(errors, "Inventory scan failed: " + exception.getMessage());
            return lastReload;
        }
        Map<String, List<Candidate>> claims = new LinkedHashMap<String, List<Candidate>>();
        for (Candidate candidate : candidates) for (String uid : candidate.manifests.keySet())
            claims.computeIfAbsent(uid, key -> new ArrayList<Candidate>())
                .add(candidate);
        for (Map.Entry<String, List<Candidate>> claim : claims.entrySet()) if (claim.getValue()
            .size() > 1) {
                List<String> sources = new ArrayList<String>();
                for (Candidate source : claim.getValue()) sources.add(source.source.getName());
                String message = "Story UID " + claim.getKey() + " is declared by: " + String.join(", ", sources);
                for (Candidate source : claim.getValue()) source.conflicts.add(message);
            }
        for (Candidate candidate : candidates) if (candidate.errors.isEmpty()) {
            try {
                loadMembers(candidate);
            } catch (Exception exception) {
                candidate.errors.add(exception.getMessage());
                candidate.close();
            }
        }
        // Shared resource inconsistency is an Error on the involved containers,
        // not UID Conflict and not a reason to discard unrelated containers.
        for (int left = 0; left < candidates.size(); left++)
            for (int right = left + 1; right < candidates.size(); right++) {
                Candidate a = candidates.get(left), b = candidates.get(right);
                if (!a.conflicts.isEmpty() || !b.conflicts.isEmpty() || a.loaded.isEmpty() || b.loaded.isEmpty())
                    continue;
                Map<String, LoadedStoryPackage> pair = new LinkedHashMap<String, LoadedStoryPackage>(a.loaded);
                pair.putAll(b.loaded);
                try {
                    StoryPackageSnapshotMerger.merge(pair);
                } catch (ProjectLoadException exception) {
                    String message = "Dependency definitions conflict between " + a.source
                        .getName() + " and " + b.source.getName() + ": " + exception.getMessage();
                    a.errors.add(message);
                    b.errors.add(message);
                }
            }
        Map<String, LoadedStoryPackage> next = new LinkedHashMap<String, LoadedStoryPackage>();
        List<StoryPackageInventoryEntry> rows = new ArrayList<StoryPackageInventoryEntry>();
        for (Candidate candidate : candidates) {
            StoryPackageInventoryEntry row = candidate.row();
            rows.add(row);
            if (row.isValid()) {
                for (LoadedStoryPackage member : candidate.loaded.values()) {
                    LoadedStoryPackage prior = packages.get(member.getStoryId());
                    if (prior != null && prior.getContentFingerprint()
                        .equals(member.getContentFingerprint())) {
                        member.close();
                        next.put(prior.getStoryId(), prior);
                    } else next.put(member.getStoryId(), member);
                }
            } else candidate.close();
            for (String error : row.getErrors()) errors.add(row.getSourceName() + ": " + error);
            for (String conflict : row.getConflicts()) errors.add(row.getSourceName() + ": " + conflict);
        }
        Map<String, LoadedStoryPackage> previous = packages;
        packages = Collections.unmodifiableMap(next);
        inventory = Collections.unmodifiableList(rows);
        revision++;
        for (LoadedStoryPackage old : previous.values()) if (next.get(old.getStoryId()) != old) old.close();
        try {
            generationStore.sweepGenerations(Collections.<String>emptySet());
        } catch (IOException exception) {
            errors.add("Container cache cleanup: " + exception.getMessage());
        }
        lastReload = errors.isEmpty() ? ReloadResult.success(next.size()) : ReloadResult.partial(next.size(), errors);
        return lastReload;
    }

    private void loadMembers(Candidate candidate) throws Exception {
        String archiveHash = DgrsGenerationStore.containerHash(candidate.source);
        candidate.reader = DgrsArchiveReader.open(candidate.source);
        Map<String, StoryPackageSnapshotReader.Result> members = new LinkedHashMap<String, StoryPackageSnapshotReader.Result>();
        CanonicalStoryLogicGraph connections;
        if (candidate.source.getName()
            .toLowerCase(Locale.ROOT)
            .endsWith(".dgrs.g")) {
            StoryGroupPackageReader.Result group = StoryGroupPackageReader.read(candidate.reader);
            if (!candidate.manifests.keySet()
                .equals(
                    group.getManifests()
                        .keySet()))
                throw new ProjectLoadException("Container member claims changed during inventory scan");
            candidate.displayName = group.getDisplayName();
            members.putAll(group.getSnapshots());
            connections = group.getConnections();
        } else {
            StoryPackageManifest manifest = candidate.manifests.values()
                .iterator()
                .next();
            StoryPackageSnapshotReader.Result member = StoryPackageSnapshotReader.read(candidate.reader, manifest);
            members.put(manifest.getStoryId(), member);
            connections = member.getStoryLogicGraph();
            new darkgrey.rpg.graph.canonical.CanonicalStoryLogicGraphLoader().validate(
                connections,
                member.getSnapshot()
                    .getCanonicalStories());
            candidate.displayName = member.getSnapshot()
                .getCanonicalStory(manifest.getStoryId())
                .getDisplayName();
        }
        if (!archiveHash.equals(DgrsGenerationStore.containerHash(candidate.source)))
            throw new IOException("Container changed during validation");
        DgrsGenerationStore.Generation owner = generationStore.installContainer(candidate.source, archiveHash);
        candidate.graph = connections;
        try {
            for (Map.Entry<String, StoryPackageSnapshotReader.Result> member : members.entrySet()) {
                StoryPackageManifest manifest = candidate.manifests.get(member.getKey());
                StoryPackageSnapshotReader.Result content = member.getValue();
                String fingerprint = StoryPackageContentFingerprint
                    .compute(manifest, content.getDeclaredBytes(), connections);
                List<CanonicalStoryLogicConnection> outgoing = new ArrayList<CanonicalStoryLogicConnection>();
                for (CanonicalStoryLogicConnection edge : connections.getConnections()) if (member.getKey()
                    .equals(edge.getSourceStoryId())) outgoing.add(edge);
                candidate.loaded.put(
                    member.getKey(),
                    new LoadedStoryPackage(
                        manifest,
                        candidate.source,
                        generationStore,
                        owner.forkOwner(),
                        content.getSnapshot(),
                        new CanonicalStoryLogicGraph(2, outgoing),
                        content.getDeclaredBytes(),
                        fingerprint));
                candidate.members
                    .put(member.getKey(), new StoryPackageMemberInfo(candidate.loaded.get(member.getKey())));
            }
        } finally {
            owner.retire();
        }
    }

    public synchronized ReloadResult setEnabled(String sourceName, boolean value, long expectedRevision)
        throws IOException {
        requireCurrentSource(sourceName, expectedRevision);
        Map<String, Boolean> next = new LinkedHashMap<String, Boolean>(enabled);
        next.put(sourceName, value);
        writeSettings(next);
        enabled = next;
        return reload();
    }

    private StoryPackageInventoryEntry requireCurrentSource(String sourceName, long expectedRevision)
        throws IOException {
        if (expectedRevision != revision)
            throw new IOException("Inventory changed; refresh before changing a container");
        for (StoryPackageInventoryEntry row : inventory) if (row.getSourceName()
            .equals(sourceName)) return row;
        throw new IOException("Container is no longer installed");
    }

    private Map<String, Boolean> readSettings() throws Exception {
        Map<String, Boolean> result = new LinkedHashMap<String, Boolean>();
        if (!Files.exists(settingsPath)) return result;
        if (isLink(settingsPath) || Files.size(settingsPath) > 1024 * 1024)
            throw new IOException("Unsafe package settings");
        JsonObject root = StrictPackageJson
            .read(new java.io.StringReader(new String(Files.readAllBytes(settingsPath), StandardCharsets.UTF_8)))
            .getAsJsonObject();
        if (root.entrySet()
            .size() != 2 || !root.has("schema_version")
            || !root.has("enabled")
            || !root.get("schema_version")
                .isJsonPrimitive()
            || !root.get("schema_version")
                .getAsJsonPrimitive()
                .isNumber()
            || root.get("schema_version")
                .getAsBigDecimal()
                .intValueExact() != 1)
            throw new IOException("Unsupported package settings");
        JsonObject values = root.getAsJsonObject("enabled");
        for (Map.Entry<String, JsonElement> entry : values.entrySet()) {
            if (!entry.getValue()
                .isJsonPrimitive()
                || !entry.getValue()
                    .getAsJsonPrimitive()
                    .isBoolean())
                throw new IOException("Invalid enabled preference");
            result.put(
                entry.getKey(),
                entry.getValue()
                    .getAsBoolean());
        }
        return result;
    }

    private void writeSettings(Map<String, Boolean> values) throws IOException {
        Files.createDirectories(settingsPath.getParent());
        if (isLink(settingsPath.getParent()) || Files.exists(settingsPath) && isLink(settingsPath))
            throw new IOException("Unsafe package settings destination");
        JsonObject root = new JsonObject(), entries = new JsonObject();
        root.addProperty("schema_version", 1);
        for (Map.Entry<String, Boolean> entry : values.entrySet())
            entries.addProperty(entry.getKey(), entry.getValue());
        root.add("enabled", entries);
        Path temporary = settingsPath.resolveSibling(
            ".story-packages-" + UUID.randomUUID()
                .toString() + ".tmp");
        try {
            Files.write(
                temporary,
                root.toString()
                    .getBytes(StandardCharsets.UTF_8),
                StandardOpenOption.CREATE_NEW);
            try {
                Files
                    .move(temporary, settingsPath, StandardCopyOption.ATOMIC_MOVE, StandardCopyOption.REPLACE_EXISTING);
            } catch (AtomicMoveNotSupportedException exception) {
                Files.move(temporary, settingsPath, StandardCopyOption.REPLACE_EXISTING);
            }
        } finally {
            Files.deleteIfExists(temporary);
        }
    }

    private static boolean isLink(Path path) throws IOException {
        return Files.isSymbolicLink(path) || !path.toAbsolutePath()
            .normalize()
            .equals(path.toRealPath());
    }

    private static final class Candidate {

        final File source;
        final boolean enabled;
        String displayName;
        DgrsArchiveReader reader;
        final Map<String, StoryPackageManifest> manifests = new LinkedHashMap<String, StoryPackageManifest>();
        final Map<String, LoadedStoryPackage> loaded = new LinkedHashMap<String, LoadedStoryPackage>();
        final List<String> errors = new ArrayList<String>(), conflicts = new ArrayList<String>();
        final Map<String, StoryPackageMemberInfo> members = new LinkedHashMap<String, StoryPackageMemberInfo>();
        CanonicalStoryLogicGraph graph = new CanonicalStoryLogicGraph(
            2,
            Collections.<CanonicalStoryLogicConnection>emptyList());

        Candidate(File source, boolean enabled) {
            this.source = source;
            this.enabled = enabled;
            displayName = source.getName();
        }

        void close() {
            for (LoadedStoryPackage member : loaded.values()) member.close();
            loaded.clear();
        }

        StoryPackageInventoryEntry row() {
            return new StoryPackageInventoryEntry(
                source.getName(),
                displayName,
                manifests.keySet(),
                enabled,
                errors,
                conflicts,
                members,
                graph);
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
