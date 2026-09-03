package darkgrey.rpg.project.packages;

import java.io.ByteArrayOutputStream;
import java.io.File;
import java.io.FileOutputStream;
import java.io.InputStream;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.StandardCopyOption;
import java.util.Enumeration;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.UUID;
import java.util.zip.ZipEntry;
import java.util.zip.ZipFile;
import java.util.zip.ZipOutputStream;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.world.storage.MapStorage;

import com.google.gson.JsonElement;

import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.network.message.nominator.NominatorCatalogCodec;
import darkgrey.rpg.nominator.NominatorCatalog;
import darkgrey.rpg.project.ProjectRepository;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.task.persistence.CanonicalTaskSavedData;
import darkgrey.rpg.task.runtime.CanonicalTaskEvent;
import darkgrey.rpg.task.runtime.CanonicalTaskObjectiveStatus;
import darkgrey.rpg.task.runtime.CanonicalTaskRuntime;
import io.netty.buffer.ByteBuf;
import io.netty.buffer.Unpooled;

/** Loads one Studio-produced DGRS through the Java package/runtime path. */
public final class DgrsPackageRuntimeProbe {

    private DgrsPackageRuntimeProbe() {}

    public static void main(String[] args) throws Exception {
        if (args.length != 2) throw new IllegalArgumentException("Expected <package.dgrs> <E-drive probe root>");
        File source = new File(args[0]).getAbsoluteFile();
        if (!source.isFile()) throw new IllegalArgumentException("DGRS package does not exist: " + source);
        File parent = new File(args[1]).getAbsoluteFile();
        if (!parent.exists() && !parent.mkdirs())
            throw new IllegalStateException("Cannot create probe root: " + parent);
        File install = new File(parent, "install-" + Long.toHexString(System.nanoTime()));
        if (!install.mkdir()) throw new IllegalStateException("Cannot create probe install directory: " + install);
        File baseProject = new File(parent, "base-" + Long.toHexString(System.nanoTime()));
        writeBaseProject(baseProject);
        File replacementSource = new File(parent, "replacement-" + Long.toHexString(System.nanoTime()) + ".dgrs");
        File unrelated = new File(parent, "unrelated-directory-" + Long.toHexString(System.nanoTime()));
        try {
            File installedArchive = new File(install, source.getName());
            Files.copy(source.toPath(), installedArchive.toPath(), StandardCopyOption.REPLACE_EXISTING);
            Files.copy(source.toPath(), replacementSource.toPath(), StandardCopyOption.REPLACE_EXISTING);
            makeRepeatableGeneration(replacementSource);
            File residue = new File(install, ".dgrs-runtime");
            require(residue.mkdir(), "Cannot create legacy runtime residue directory");
            Files.write(new File(residue, "stale.bin").toPath(), new byte[] { 1, 2, 3 });
            require(unrelated.mkdir(), "Cannot create unrelated directory");
            Files.write(new File(unrelated, "keep.bin").toPath(), new byte[] { 4, 5, 6 });
            StoryPackageLoader loader = new StoryPackageLoader(install);
            ProjectRepository repository = new ProjectRepository(baseProject);
            StoryPackageRuntimeReloader.Result startup = StoryPackageRuntimeReloader.startup(repository, loader);
            require(
                startup.isSuccessful(),
                "Runtime rejected DGRS: " + startup.getPackageReload()
                    .getSummary() + " " + startup.getErrors());
            require(
                loader.getPackages()
                    .size() == 1,
                "Expected exactly one loaded DGRS package");
            LoadedStoryPackage loaded = loader.getPackages()
                .values()
                .iterator()
                .next();
            MapStorage generationStorage = new MapStorage(null);
            StoryPackageGenerationLifecycle.Result initialGeneration = StoryPackageGenerationLifecycle
                .reconcile(generationStorage, loader.getPackages());
            require(initialGeneration.isBootstrap(), "First generation reconciliation was not a bootstrap");
            require(
                initialGeneration.getDeltas()
                    .size() == 1
                    && initialGeneration.getDeltas()
                        .get(0)
                        .getKind() == StoryPackageGenerationDelta.Kind.ADDED,
                "First accepted DGRS was not classified ADDED");
            CanonicalGraphResource initialTaskResource = loaded.getSnapshot()
                .getCanonicalTasks()
                .values()
                .iterator()
                .next();
            UUID progressPlayer = UUID.fromString("10000000-0000-0000-0000-000000000003");
            CanonicalTaskSavedData taskData = CanonicalTaskSavedData.get(generationStorage);
            taskData.start(progressPlayer, loaded.getStoryId(), "generation-probe", initialTaskResource, 1L);
            MapStorage restartStorage = savedGenerationAndTaskStorage(generationStorage, taskData);
            CanonicalGraphResource initialPublishedStory = repository.getSnapshot()
                .getCanonicalStory(loaded.getStoryId());
            require(
                loaded.getManifest()
                    .isDgrsV1(),
                "Runtime did not retain DGRS v1 identity");
            require(loaded.getDirectory() == null, "DGRS package still depends on a materialized directory");
            require(
                installedArchive.getAbsoluteFile()
                    .equals(loaded.getSourceArchive()),
                "DGRS source archive identity was not retained");
            require(!new File(install, ".dgrs-runtime").exists(), "Legacy runtime residue was not cleaned");
            require(new File(unrelated, "keep.bin").isFile(), "Residue cleanup touched an unrelated install directory");
            assertNoExtraction(install, installedArchive);
            require(
                loaded.getSnapshot()
                    .getActors()
                    .size() == 2,
                "Expected exactly two Actors in the Frozen-A fixture");
            require(
                loaded.getSnapshot()
                    .getItems()
                    .size() == 1,
                "Expected exactly one Item in the Frozen-A fixture");
            require(
                loaded.getSnapshot()
                    .getItemGroups()
                    .isEmpty(),
                "Expected no Item Groups in the Frozen-A fixture");
            require(
                loaded.getSnapshot()
                    .getCanonicalStories()
                    .size() == 1,
                "Expected exactly one canonical Story in the Frozen-A fixture");
            require(
                loaded.getSnapshot()
                    .getCanonicalSessions()
                    .size() == 2,
                "Expected exactly two Sessions in the Frozen-A fixture");
            require(
                loaded.getSnapshot()
                    .getCanonicalTasks()
                    .size() == 1,
                "Expected exactly one canonical Task in the Frozen-A fixture");
            assertPublishedCounts(repository.getLastReload());
            NominatorCatalog catalog = NominatorCatalog.from(repository.getSnapshot(), loader.getPackages());
            require(
                catalog.getPackageChoices()
                    .size() == 1,
                "Nominator catalog did not expose the loaded package");
            NominatorCatalog.PackageChoice packageChoice = catalog.getPackageChoices()
                .get(0);
            require(
                packageChoice.getActorIds()
                    .size() == 2,
                "Package catalog did not expose two Actors");
            require(
                packageChoice.getItemIds()
                    .size() == 1,
                "Package catalog did not expose one Item");
            require(
                packageChoice.getItemGroupIds()
                    .isEmpty(),
                "Package catalog changed Item Group closure");
            require(
                loaded.getStoryId()
                    .equals(packageChoice.getStoryId()),
                "Package catalog changed canonical Story identity");
            ByteBuf catalogBytes = Unpooled.buffer();
            NominatorCatalogCodec.write(catalogBytes, catalog);
            NominatorCatalog decodedCatalog = NominatorCatalogCodec.read(catalogBytes);
            require(!catalogBytes.isReadable(), "Nominator catalog codec left unread package bytes");
            require(
                decodedCatalog.getPackageChoices()
                    .size() == 1,
                "Nominator package catalog codec lost package");
            require(
                decodedCatalog.getPackageChoices()
                    .get(0)
                    .getActorIds()
                    .equals(packageChoice.getActorIds()),
                "Nominator package catalog codec changed Actor closure");

            for (Map.Entry<String, CanonicalGraphResource> entry : loaded.getSnapshot()
                .getCanonicalTasks()
                .entrySet()) {
                CanonicalTaskRuntime runtime = CanonicalTaskRuntime.start(entry.getValue());
                Map<String, CanonicalGraphNode> objectives = new LinkedHashMap<String, CanonicalGraphNode>();
                for (CanonicalGraphNode node : entry.getValue()
                    .getGraph()
                    .getNodes()) {
                    if (!"objective".equals(node.getType())) continue;
                    objectives.put(node.getId(), node);
                    if (isUnselected(node)) require(
                        runtime.getObjectiveStatuses()
                            .get(node.getId()) == CanonicalTaskObjectiveStatus.INACTIVE,
                        "Unselected Objective was not dormant");
                }
                int remainingSteps = Math.max(1, objectives.size() * 2);
                while (!runtime.isSettled() && remainingSteps-- > 0) {
                    boolean progressed = false;
                    for (Map.Entry<String, CanonicalTaskObjectiveStatus> status : runtime.getObjectiveStatuses()
                        .entrySet()) {
                        if (status.getValue() != CanonicalTaskObjectiveStatus.ACTIVE) continue;
                        CanonicalGraphNode objective = objectives.get(status.getKey());
                        require(objective != null, "Runtime exposed an unknown Objective status");
                        require(
                            runtime.accept(eventFor(objective)),
                            "Active Objective did not accept its configured runtime event: " + objective.getId());
                        progressed = true;
                    }
                    require(progressed || runtime.isSettled(), "Task has no actionable Objective and is not settled");
                }
                require(runtime.isSettled(), "Task did not settle after all configured Objectives were completed");
            }

            Files.copy(replacementSource.toPath(), installedArchive.toPath(), StandardCopyOption.REPLACE_EXISTING);
            StoryPackageRuntimeReloader.Result replacementReload = StoryPackageRuntimeReloader
                .reload(repository, loader);
            require(
                replacementReload.isSuccessful(),
                "Valid replacement DGRS was rejected: " + replacementReload.getErrors());
            LoadedStoryPackage replacement = loader.getPackage(loaded.getPackageId());
            require(replacement != null, "Valid replacement removed the accepted package");
            require(replacement != loaded, "Valid replacement retained the previous package object");
            require(
                replacement.getSourceArchive()
                    .equals(installedArchive.getAbsoluteFile()),
                "Valid replacement did not retain the installed archive identity");
            ProjectSnapshot replacementSnapshot = repository.getSnapshot();
            CanonicalGraphResource replacementPublishedStory = replacementSnapshot
                .getCanonicalStory(replacement.getStoryId());
            require(replacementPublishedStory != null, "Valid replacement did not publish its canonical Story");
            require(replacementPublishedStory != initialPublishedStory, "Valid replacement retained old Story content");
            require(
                !replacement.getContentFingerprint()
                    .equals(loaded.getContentFingerprint()),
                "Story repeat_policy change did not produce a new package generation");
            StoryPackageGenerationLifecycle.Result updatedGeneration = StoryPackageGenerationLifecycle
                .reconcile(generationStorage, loader.getPackages());
            require(
                updatedGeneration.getDeltas()
                    .size() == 1
                    && updatedGeneration.getDeltas()
                        .get(0)
                        .getKind() == StoryPackageGenerationDelta.Kind.UPDATED,
                "Valid content replacement was not classified UPDATED");
            require(
                updatedGeneration.getTaskInstancesRetired() == 1 && taskData.size() == 0,
                "UPDATED generation did not retire old Task progress");
            StoryPackageGenerationLifecycle.Result offlineUpdatedGeneration = StoryPackageGenerationLifecycle
                .reconcile(restartStorage, loader.getPackages());
            require(
                !offlineUpdatedGeneration.isBootstrap() && offlineUpdatedGeneration.getDeltas()
                    .get(0)
                    .getKind() == StoryPackageGenerationDelta.Kind.UPDATED
                    && offlineUpdatedGeneration.getTaskInstancesRetired() == 1,
                "Offline replacement did not reconcile persisted old generation progress on restart");
            CanonicalGraphResource taskResource = replacement.getSnapshot()
                .getCanonicalTasks()
                .values()
                .iterator()
                .next();
            taskData.start(progressPlayer, replacement.getStoryId(), "generation-probe", taskResource, 1L);
            String partialObjectiveId = requiredThreeObjectiveId(taskResource);
            require(
                taskData.dispatch(progressPlayer, CanonicalTaskEvent.killEntity("slimes"), 2L)
                    .getChangedInstanceCount() == 1,
                "Generation probe Task did not accept first progress event");
            require(
                taskData.dispatch(progressPlayer, CanonicalTaskEvent.killEntity("slimes"), 3L)
                    .getChangedInstanceCount() == 1,
                "Generation probe Task did not accept second progress event");
            require(
                taskData.getSnapshot(progressPlayer, replacement.getStoryId(), "generation-probe")
                    .getRuntimeSnapshot()
                    .getProgress()
                    .get(partialObjectiveId)
                    .intValue() == 2,
                "Generation probe Task did not reach exact 2/3 progress");
            assertPublishedCounts(repository.getLastReload());
            assertNoExtraction(install, installedArchive);

            StoryPackageRuntimeReloader.Result unchangedReload = StoryPackageRuntimeReloader.reload(repository, loader);
            require(unchangedReload.isSuccessful(), "Unchanged DGRS reload failed");
            LoadedStoryPackage unchangedAccepted = loader.getPackage(loaded.getPackageId());
            require(
                unchangedAccepted != null && unchangedAccepted.getContentFingerprint()
                    .equals(replacement.getContentFingerprint()),
                "Unchanged reload did not retain the accepted generation fingerprint");
            CanonicalGraphResource unchangedPublishedStory = repository.getSnapshot()
                .getCanonicalStory(unchangedAccepted.getStoryId());
            StoryPackageGenerationLifecycle.Result unchangedGeneration = StoryPackageGenerationLifecycle
                .reconcile(generationStorage, loader.getPackages());
            require(
                unchangedGeneration.getDeltas()
                    .size() == 1
                    && unchangedGeneration.getDeltas()
                        .get(0)
                        .getKind() == StoryPackageGenerationDelta.Kind.UNCHANGED,
                "Byte-identical accepted DGRS was not classified UNCHANGED");
            require(
                unchangedGeneration.getRuntimeStatesRetired() == 0 && taskData.size() == 1,
                "UNCHANGED generation retired active Task progress");
            require(
                taskData.getSnapshot(progressPlayer, replacement.getStoryId(), "generation-probe")
                    .getRuntimeSnapshot()
                    .getProgress()
                    .get(partialObjectiveId)
                    .intValue() == 2,
                "UNCHANGED generation changed 2/3 Task progress");

            Files.write(installedArchive.toPath(), "{".getBytes(StandardCharsets.UTF_8));
            StoryPackageRuntimeReloader.Result corruptReload = StoryPackageRuntimeReloader.reload(repository, loader);
            require(!corruptReload.isSuccessful(), "Corrupt replacement DGRS was accepted");
            require(
                loader.getPackage(loaded.getPackageId()) == unchangedAccepted,
                "Corrupt replacement erased the active DGRS");
            require(
                loader.getPackage(loaded.getPackageId())
                    .getSnapshot() == unchangedAccepted.getSnapshot(),
                "Corrupt replacement changed the accepted package content");
            require(
                repository.getSnapshot()
                    .getCanonicalStory(replacement.getStoryId()) == unchangedPublishedStory,
                "Corrupt replacement changed the published Story content");
            StoryPackageGenerationLifecycle.Result corruptGeneration = StoryPackageGenerationLifecycle
                .reconcile(generationStorage, loader.getPackages());
            require(
                corruptGeneration.getDeltas()
                    .get(0)
                    .getKind() == StoryPackageGenerationDelta.Kind.UNCHANGED && taskData.size() == 1,
                "Corrupt reload changed accepted generation or retired Last-Known-Good progress");
            require(
                taskData.getSnapshot(progressPlayer, replacement.getStoryId(), "generation-probe")
                    .getRuntimeSnapshot()
                    .getProgress()
                    .get(partialObjectiveId)
                    .intValue() == 2,
                "Corrupt replacement changed Last-Known-Good 2/3 Task progress");
            assertNoExtraction(install, installedArchive);

            Files.delete(new File(baseProject, "project.json").toPath());
            Files.delete(installedArchive.toPath());
            StoryPackageRuntimeReloader.Result removeReload = StoryPackageRuntimeReloader.reload(repository, loader);
            require(
                !removeReload.isSuccessful() && !removeReload.getProjectReload()
                    .isSuccessful(),
                "Deleting the DGRS did not report the failed base reload");
            require(
                loader.getPackages()
                    .isEmpty(),
                "Deleting the installed DGRS left a stale package in the registry");
            require(
                loader.getPackage(loaded.getPackageId()) == null,
                "Deleting the installed DGRS left a stale package object");
            require(repository.getSnapshot() != replacementSnapshot, "Deleting the last DGRS retained its snapshot");
            require(
                repository.getSnapshot()
                    .getActors()
                    .isEmpty(),
                "Deleting the last DGRS retained its Actors");
            require(
                repository.getSnapshot()
                    .getItems()
                    .isEmpty(),
                "Deleting the last DGRS retained its Items");
            require(
                repository.getSnapshot()
                    .getCanonicalStories()
                    .isEmpty(),
                "Deleting the last DGRS retained its canonical Story");
            require(
                repository.getSnapshot()
                    .getCanonicalTasks()
                    .isEmpty(),
                "Deleting the last DGRS retained its canonical Task");
            require(
                repository.getSnapshot()
                    .getProject()
                    .getId()
                    .equals("unloaded"),
                "Deleting the last DGRS left the prior snapshot authoritative after base reload failure");
            require(
                !repository.getLastReload()
                    .isSuccessful() && repository.getLastReload()
                        .getSummary()
                        .contains("Missing JSON file"),
                "Clearing the prior snapshot hid the failed base reload from status reporting");
            StoryPackageGenerationLifecycle.Result removedGeneration = StoryPackageGenerationLifecycle
                .reconcile(generationStorage, loader.getPackages());
            require(
                removedGeneration.getDeltas()
                    .size() == 1
                    && removedGeneration.getDeltas()
                        .get(0)
                        .getKind() == StoryPackageGenerationDelta.Kind.REMOVED,
                "Deleted DGRS was not classified REMOVED");
            require(
                removedGeneration.getTaskInstancesRetired() == 1 && taskData.size() == 0,
                "REMOVED generation did not retire active Task progress");
            assertNoExtraction(install, installedArchive);

            Files.copy(replacementSource.toPath(), installedArchive.toPath(), StandardCopyOption.REPLACE_EXISTING);
            StoryPackageRuntimeReloader.Result reinstallReload = StoryPackageRuntimeReloader.reload(repository, loader);
            require(
                reinstallReload.isSuccessful(),
                "Reinstalling the valid DGRS failed: " + reinstallReload.getErrors());
            LoadedStoryPackage reinstalled = loader.getPackage(loaded.getPackageId());
            require(reinstalled != null, "Reinstalling the valid DGRS did not restore the package");
            require(reinstalled != replacement, "Reinstalling the valid DGRS reused a removed package object");
            require(
                loader.getPackages()
                    .size() == 1,
                "Reinstalling the valid DGRS did not restore one package");
            require(
                reinstalled.getSnapshot()
                    .getActors()
                    .size() == 2,
                "Reinstalled DGRS changed Actor count");
            require(
                reinstalled.getSnapshot()
                    .getItems()
                    .size() == 1,
                "Reinstalled DGRS changed Item count");
            require(
                reinstalled.getSnapshot()
                    .getCanonicalSessions()
                    .size() == 2,
                "Reinstalled DGRS changed Session count");
            require(
                reinstalled.getSnapshot()
                    .getCanonicalTasks()
                    .size() == 1,
                "Reinstalled DGRS changed Task count");
            require(
                repository.getSnapshot()
                    .getCanonicalStory(reinstalled.getStoryId()) != null,
                "Reinstalled DGRS was not published");
            StoryPackageGenerationLifecycle.Result reinstalledGeneration = StoryPackageGenerationLifecycle
                .reconcile(generationStorage, loader.getPackages());
            require(
                reinstalledGeneration.getDeltas()
                    .size() == 1
                    && reinstalledGeneration.getDeltas()
                        .get(0)
                        .getKind() == StoryPackageGenerationDelta.Kind.ADDED,
                "Reinstalled DGRS was not classified ADDED");
            assertPublishedCounts(repository.getLastReload());
            assertNoExtraction(install, installedArchive);

            Files.copy(source.toPath(), installedArchive.toPath(), StandardCopyOption.REPLACE_EXISTING);
            File escaped = new File(new File(install, ".dgrs-runtime"), "escaped.txt");
            Files.deleteIfExists(escaped.toPath());
            File unsafeArchive = new File(install, "unsafe.dgrs");
            writeUnsafeArchive(unsafeArchive);
            StoryPackageRuntimeReloader.Result unsafeReload = StoryPackageRuntimeReloader.reload(repository, loader);
            require(!unsafeReload.isSuccessful(), "Traversal DGRS was accepted");
            require(!escaped.exists(), "Traversal DGRS wrote outside its runtime staging directory");
            assertNoExtraction(install, installedArchive);
            assertNoExtraction(install, unsafeArchive);
            require(loader.getPackage(loaded.getPackageId()) != null, "Traversal DGRS disabled the valid package");
            System.out.println("DGRS_JAVA_LOADER=PASS");
            System.out.println("DGRS_DIRECT_NO_EXTRACTION=PASS");
            System.out.println("DGRS_EXACT_COUNTS=PASS");
            System.out.println("DGRS_CANONICAL_TASK_RUNTIME=PASS");
            System.out.println("DGRS_RELOAD_ROLLBACK=PASS");
            System.out.println("DGRS_RELOAD_LIFECYCLE=PASS");
            System.out.println("DGRS_DELETE_LAST_BASE_FAILURE_CLEARS_SNAPSHOT=PASS");
            System.out.println("DGRS_DELETE_LAST_FAILURE_STATUS_RETAINED=PASS");
            System.out.println("DGRS_RUNTIME_RESIDUE_NARROW_CLEANUP=PASS");
            System.out.println("DGRS_AUTHORITATIVE_PUBLICATION=PASS");
            System.out.println("DGRS_NOMINATOR_PACKAGE_CATALOG=PASS");
            System.out.println("DGRS_UNSAFE_PATH_REJECTED=PASS");
            System.out.println("DGRS_PACKAGE_GENERATION_LIFECYCLE=PASS");
            System.out.println("DGRS_UNCHANGED_PROGRESS_PRESERVED=PASS");
            System.out.println("DGRS_CORRUPT_LAST_KNOWN_GOOD_PRESERVED=PASS");
            System.out.println("DGRS_REMOVED_PROGRESS_RETIRED=PASS");
            System.out.println("DGRS_OFFLINE_REPLACEMENT_RECONCILED=PASS");
            System.out.println("DGRS_PACKAGE_ID=" + loaded.getPackageId());
            System.out.println("DGRS_GENERATION_A_ONCE_FINGERPRINT=" + loaded.getContentFingerprint());
            System.out.println("DGRS_GENERATION_B_REPEATABLE_FINGERPRINT=" + replacement.getContentFingerprint());
        } finally {
            delete(install);
            delete(baseProject);
            delete(replacementSource);
            delete(unrelated);
        }
    }

    private static void assertPublishedCounts(ProjectRepository.ReloadResult result) {
        require(result.getStoryCount() == 1, "Published summary did not count the canonical Story");
        require(result.getActorCount() == 2, "Published summary did not count Actors");
        require(result.getItemCount() == 1, "Published summary did not count Items");
        require(result.getItemGroupCount() == 0, "Published summary changed Item Group count");
        require(result.getSessionCount() == 2, "Published summary did not count Sessions");
        require(result.getTaskCount() == 1, "Published summary did not count Tasks");
    }

    private static void writeBaseProject(File directory) throws Exception {
        if (!directory.mkdir()) throw new IllegalStateException("Cannot create base project: " + directory);
        File actors = new File(directory, "actors");
        if (!actors.mkdir()) throw new IllegalStateException("Cannot create base actors directory: " + actors);
        requireDirectory(new File(directory, "dialogues"));
        requireDirectory(new File(directory, "quests"));
        requireDirectory(new File(directory, "stories"));
        Files.write(
            new File(directory, "project.json").toPath(),
            ("{\"schema_version\":1,\"id\":\"probe_base\",\"display_name\":\"Probe Base\"}")
                .getBytes(StandardCharsets.UTF_8));
    }

    private static void requireDirectory(File directory) {
        if (!directory.mkdir()) throw new IllegalStateException("Cannot create base directory: " + directory);
    }

    private static void assertNoExtraction(File install, File archive) {
        require(!new File(install, ".dgrs-runtime").exists(), "DGRS lifecycle created .dgrs-runtime");
        require(
            !new File(install, archiveBaseName(archive)).exists(),
            "DGRS lifecycle created a same-name extraction directory: " + archiveBaseName(archive));
    }

    private static String archiveBaseName(File archive) {
        String name = archive.getName();
        return name.substring(0, name.length() - ".dgrs".length());
    }

    private static boolean isUnselected(CanonicalGraphNode node) {
        String type = node.getProperties()
            .get("objective_type")
            .getAsString();
        String targetProperty = "kill_entity".equals(type) ? "entity"
            : "collect_item".equals(type) ? "item" : "actor_id";
        JsonElement target = node.getProperties()
            .get(targetProperty);
        return target != null && target.getAsString()
            .length() == 0;
    }

    private static CanonicalTaskEvent eventFor(CanonicalGraphNode node) {
        String type = node.getProperties()
            .get("objective_type")
            .getAsString();
        int required = node.getProperties()
            .containsKey("required")
                ? node.getProperties()
                    .get("required")
                    .getAsInt()
                : 1;
        if ("kill_entity".equals(type)) return CanonicalTaskEvent.killEntity(
            node.getProperties()
                .get("entity")
                .getAsString(),
            required);
        if ("collect_item".equals(type)) return CanonicalTaskEvent.collectItem(
            node.getProperties()
                .get("item")
                .getAsString(),
            required);
        if ("interact_actor".equals(type)) return CanonicalTaskEvent.interactActor(
            node.getProperties()
                .get("actor_id")
                .getAsString());
        throw new AssertionError("Unsupported configured Objective type: " + type);
    }

    private static String requiredThreeObjectiveId(CanonicalGraphResource task) {
        for (CanonicalGraphNode node : task.getGraph()
            .getNodes())
            if ("objective".equals(node.getType()) && node.getProperties()
                .containsKey("required")
                && node.getProperties()
                    .get("required")
                    .getAsInt() == 3
                && "kill_entity".equals(
                    node.getProperties()
                        .get("objective_type")
                        .getAsString()))
                return node.getId();
        throw new AssertionError("Real kill_slimes fixture has no required=3 kill Objective");
    }

    private static void writeUnsafeArchive(File file) throws Exception {
        ZipOutputStream output = new ZipOutputStream(new FileOutputStream(file));
        try {
            output.putNextEntry(new ZipEntry("../escaped.txt"));
            output.write("escape".getBytes(StandardCharsets.UTF_8));
            output.closeEntry();
        } finally {
            output.close();
        }
    }

    private static void makeRepeatableGeneration(File archive) throws Exception {
        File rewritten = new File(archive.getParentFile(), archive.getName() + ".rewrite");
        ZipFile input = new ZipFile(archive);
        ZipOutputStream output = new ZipOutputStream(new FileOutputStream(rewritten));
        try {
            Enumeration<? extends ZipEntry> entries = input.entries();
            boolean found = false;
            byte[] buffer = new byte[8192];
            while (entries.hasMoreElements()) {
                ZipEntry original = entries.nextElement();
                ZipEntry replacement = new ZipEntry(original.getName());
                output.putNextEntry(replacement);
                InputStream stream = input.getInputStream(original);
                ByteArrayOutputStream content = new ByteArrayOutputStream();
                try {
                    int read;
                    while ((read = stream.read(buffer)) >= 0) content.write(buffer, 0, read);
                } finally {
                    stream.close();
                }
                byte[] bytes = content.toByteArray();
                if ("resources/canonical/stories/kill_slimes.json".equals(original.getName())) {
                    String once = new String(bytes, StandardCharsets.UTF_8);
                    String repeatable = once
                        .replace("\"repeat_policy\": \"once\"", "\"repeat_policy\": \"repeatable\"");
                    require(!once.equals(repeatable), "DGRS replacement fixture is not authored once");
                    bytes = repeatable.getBytes(StandardCharsets.UTF_8);
                    found = true;
                }
                output.write(bytes);
                output.closeEntry();
            }
            require(found, "DGRS replacement fixture has no kill_slimes canonical Story");
        } finally {
            output.close();
            input.close();
        }
        Files.move(rewritten.toPath(), archive.toPath(), StandardCopyOption.REPLACE_EXISTING);
    }

    private static MapStorage savedGenerationAndTaskStorage(MapStorage source, CanonicalTaskSavedData taskData) {
        NBTTagCompound generations = new NBTTagCompound();
        StoryPackageGenerationSavedData.get(source)
            .writeToNBT(generations);
        StoryPackageGenerationSavedData restartedGenerations = new StoryPackageGenerationSavedData();
        restartedGenerations.readFromNBT(generations);
        NBTTagCompound tasks = new NBTTagCompound();
        taskData.writeToNBT(tasks);
        CanonicalTaskSavedData restartedTasks = new CanonicalTaskSavedData();
        restartedTasks.readFromNBT(tasks);
        MapStorage result = new MapStorage(null);
        result.setData(StoryPackageGenerationSavedData.DATA_NAME, restartedGenerations);
        result.setData(CanonicalTaskSavedData.DATA_NAME, restartedTasks);
        return result;
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }

    private static void delete(File file) {
        if (file.isDirectory()) {
            File[] children = file.listFiles();
            if (children != null) for (File child : children) delete(child);
        }
        file.delete();
    }
}
