package darkgrey.rpg.project.packages;

import java.io.File;
import java.io.FileOutputStream;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.StandardCopyOption;
import java.util.LinkedHashMap;
import java.util.Map;
import java.util.zip.ZipEntry;
import java.util.zip.ZipOutputStream;

import com.google.gson.JsonElement;

import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.task.runtime.CanonicalTaskEvent;
import darkgrey.rpg.task.runtime.CanonicalTaskObjectiveStatus;
import darkgrey.rpg.task.runtime.CanonicalTaskRuntime;

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
        try {
            File installedArchive = new File(install, source.getName());
            Files.copy(source.toPath(), installedArchive.toPath(), StandardCopyOption.REPLACE_EXISTING);
            StoryPackageLoader loader = new StoryPackageLoader(install);
            StoryPackageLoader.ReloadResult result = loader.reload();
            require(result.isSuccessful(), "Runtime rejected DGRS: " + result.getSummary() + " " + result.getErrors());
            require(
                loader.getPackages()
                    .size() == 1,
                "Expected exactly one loaded DGRS package");
            LoadedStoryPackage loaded = loader.getPackages()
                .values()
                .iterator()
                .next();
            require(
                loaded.getManifest()
                    .isDgrsV1(),
                "Runtime did not retain DGRS v1 identity");
            require(
                !loaded.getSnapshot()
                    .getCanonicalTasks()
                    .isEmpty(),
                "DGRS contains no canonical Task");

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

            Files.write(installedArchive.toPath(), "{".getBytes(StandardCharsets.UTF_8));
            StoryPackageLoader.ReloadResult corruptReload = loader.reload();
            require(!corruptReload.isSuccessful(), "Corrupt replacement DGRS was accepted");
            require(loader.getPackage(loaded.getPackageId()) == loaded, "Corrupt replacement erased the active DGRS");

            Files.copy(source.toPath(), installedArchive.toPath(), StandardCopyOption.REPLACE_EXISTING);
            File escaped = new File(new File(install, ".dgrs-runtime"), "escaped.txt");
            Files.deleteIfExists(escaped.toPath());
            writeUnsafeArchive(new File(install, "unsafe.dgrs"));
            StoryPackageLoader.ReloadResult unsafeReload = loader.reload();
            require(!unsafeReload.isSuccessful(), "Traversal DGRS was accepted");
            require(!escaped.exists(), "Traversal DGRS wrote outside its runtime staging directory");
            require(loader.getPackage(loaded.getPackageId()) != null, "Traversal DGRS disabled the valid package");
            System.out.println("DGRS_JAVA_LOADER=PASS");
            System.out.println("DGRS_CANONICAL_TASK_RUNTIME=PASS");
            System.out.println("DGRS_RELOAD_ROLLBACK=PASS");
            System.out.println("DGRS_UNSAFE_PATH_REJECTED=PASS");
            System.out.println("DGRS_PACKAGE_ID=" + loaded.getPackageId());
        } finally {
            delete(install);
        }
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
