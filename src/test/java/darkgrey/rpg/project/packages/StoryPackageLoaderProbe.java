package darkgrey.rpg.project.packages;

import java.io.File;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.util.Arrays;

/** Focused Stage 3 proof: load, replacement, package isolation, rollback, and shared progress. */
public final class StoryPackageLoaderProbe {

    public static void main(String[] args) throws Exception {
        File scratch = Files.createTempDirectory("dgr-story-packages")
            .toFile();
        File root = new File(scratch, "Install");
        root.mkdirs();
        try {
            File first = new File(root, "ST-2345-6789-ABCD-EFGH");
            writePackage(first, "ST-2345-6789-ABCD-EFGH", "Alpha", false);
            addSharedActor(first, "Shared Actor");
            StoryPackageLoader loader = new StoryPackageLoader(root);
            require(
                loader.reload()
                    .isSuccessful(),
                "initial package did not load: " + loader.getLastReload()
                    .getErrors());
            require(
                "Alpha".equals(
                    loader.getPackage("ST-2345-6789-ABCD-EFGH")
                        .getSnapshot()
                        .getCanonicalStory("ST-2345-6789-ABCD-EFGH")
                        .getDisplayName()),
                "initial title mismatch");

            writePackage(first, "ST-2345-6789-ABCD-EFGH", "Updated", false);
            addSharedActor(first, "Shared Actor");
            require(
                loader.reload()
                    .isSuccessful(),
                "replacement package did not load");
            require(
                "Updated".equals(
                    loader.getPackage("ST-2345-6789-ABCD-EFGH")
                        .getSnapshot()
                        .getCanonicalStory("ST-2345-6789-ABCD-EFGH")
                        .getDisplayName()),
                "replacement title mismatch");

            File second = new File(root, "ST-JKLM-NPQR-STUV-WXYZ");
            writePackage(second, "ST-JKLM-NPQR-STUV-WXYZ", "Beta", false, "ST-JKLM-NPQR-STUV-WXYZ");
            addSharedActor(second, "Shared Actor");
            require(
                loader.reload()
                    .isSuccessful() && loader.getPackage("ST-JKLM-NPQR-STUV-WXYZ") != null,
                "second package with an identical shared Actor did not load");
            writePackage(
                new File(root, "independent"),
                CurrentPackageProbeFixtures.C,
                "Independent",
                false,
                CurrentPackageProbeFixtures.C);
            require(
                loader.reload()
                    .isSuccessful(),
                "unrelated package loaded");
            addSharedActor(second, "Conflicting Actor");
            StoryPackageLoader.ReloadResult sharedConflict = loader.reload();
            require(!sharedConflict.isSuccessful(), "conflicting shared Actor was accepted");
            require(
                loader.getPackage(CurrentPackageProbeFixtures.A) == null
                    && loader.getPackage(CurrentPackageProbeFixtures.B) == null
                    && loader.getPackage(CurrentPackageProbeFixtures.C) != null,
                "shared inconsistency blocks affected containers and preserves unrelated package: "
                    + loader.getPackages()
                        .keySet()
                    + " / "
                    + loader.getLastReload()
                        .getErrors());
            require(
                loader.getInventory()
                    .stream()
                    .filter(row -> row.getState() == StoryPackageInventoryEntry.State.ERROR)
                    .count() == 2,
                "both inconsistent containers reported as Error");
            addSharedActor(second, "Shared Actor");
            require(
                loader.reload()
                    .isSuccessful(),
                "shared Actor repair did not reload");
            writeLogicPackage(new File(root, "ST-2222-3333-4444-5555"), "ST-2222-3333-4444-5555", true);
            writeLogicPackage(new File(root, "ST-3456-789A-BCDE-FGHJ"), "ST-3456-789A-BCDE-FGHJ", false);
            writeLogicGroup(root);
            StoryPackageLoader.ReloadResult logicReload = loader.reload();
            require(
                logicReload.isSuccessful(),
                "cross-Story Logic packages did not load as one set: " + logicReload.getErrors());
            darkgrey.rpg.project.ProjectSnapshot merged = StoryPackageSnapshotMerger.merge(loader.getPackages());
            require(
                merged.getCanonicalStoryLogicConnections()
                    .size() == 1 && "ST-3456-789A-BCDE-FGHJ".equals(
                        merged.getCanonicalStoryLogicConnections()
                            .get(0)
                            .getTargetStoryId()),
                "package-owned Story Logic graph was not retained after merge");
            write(new File(first, "manifest.json"), "{");
            archive(first);
            StoryPackageLoader.ReloadResult rejected = loader.reload();
            require(
                !rejected.isSuccessful() && loader.getPackage(CurrentPackageProbeFixtures.A) == null
                    && loader.getPackage(CurrentPackageProbeFixtures.C) != null,
                "corrupt container excluded without disabling unrelated package");
            require(
                "{".equals(
                    new String(
                        Files.readAllBytes(new File(first, "manifest.json").toPath()),
                        java.nio.charset.StandardCharsets.UTF_8)),
                "corrupt input not rewritten");
            require(loader.getPackage("ST-JKLM-NPQR-STUV-WXYZ") != null, "corrupt manifest disabled another package");
            writePackage(first, "ST-2345-6789-ABCD-EFGH", "Updated", false);
            addSharedActor(first, "Shared Actor");
            writePackage(
                new File(root, "duplicate_beta"),
                "ST-AAAA-BBBB-CCCC-DDDD",
                "Gamma",
                false,
                "ST-JKLM-NPQR-STUV-WXYZ");
            StoryPackageLoader.ReloadResult duplicate = loader.reload();
            require(!duplicate.isSuccessful(), "cross-package duplicate Story ID was accepted");
            require(
                loader.getPackage(CurrentPackageProbeFixtures.B) == null
                    && loader.getPackage(CurrentPackageProbeFixtures.A) != null
                    && loader.getPackage(CurrentPackageProbeFixtures.C) != null,
                "duplicate UID blocks both claims while unrelated containers remain active");
            require(
                loader.getInventory()
                    .stream()
                    .filter(row -> row.getState() == StoryPackageInventoryEntry.State.CONFLICT)
                    .count() == 2,
                "duplicate UID conflict is symmetric");
            System.out.println("STORY_PACKAGE_CURRENT_LOAD_REPLACE_ERROR_CONFLICT_ISOLATION=PASS");
            System.out.println("STORY_PACKAGE_CROSS_STORY_LOGIC_MERGE=PASS");
        } finally {
            delete(scratch);
        }
    }

    private static void writePackage(File directory, String packageId, String title, boolean broken) throws Exception {
        writePackage(directory, packageId, title, broken, CurrentPackageProbeFixtures.A);
    }

    private static void writePackage(File directory, String packageId, String title, boolean broken, String storyId)
        throws Exception {
        write(
            new File(directory, "project.json"),
            CurrentPackageProbeFixtures.project()
                .toString());
        write(
            new File(directory, CurrentPackageProbeFixtures.storyPath(storyId)),
            broken ? "{}"
                : CurrentPackageProbeFixtures.story(storyId, title)
                    .toString());
        write(
            new File(directory, CurrentPackageProbeFixtures.membershipPath(storyId)),
            CurrentPackageProbeFixtures.membership(storyId)
                .toString());
        write(
            new File(directory, "manifest.json"),
            CurrentPackageProbeFixtures.manifest(storyId)
                .toString());
        archive(directory);
    }

    private static void write(File file, String value) throws Exception {
        if (file.getParentFile() != null) file.getParentFile()
            .mkdirs();
        Files.write(file.toPath(), value.getBytes(java.nio.charset.StandardCharsets.UTF_8));
    }

    private static void addSharedActor(File directory, String displayName) throws Exception {
        String key = CurrentPackageProbeFixtures.A + "~actor~shared_actor";
        write(
            new File(directory, "actors/shared_actor.json"),
            CurrentPackageProbeFixtures.actor(key, displayName, "")
                .toString());
        File file = new File(directory, "manifest.json");
        com.google.gson.JsonObject manifest = new com.google.gson.JsonParser()
            .parse(new String(Files.readAllBytes(file.toPath()), java.nio.charset.StandardCharsets.UTF_8))
            .getAsJsonObject();
        manifest.getAsJsonObject("required_resources")
            .add("actors", CurrentPackageProbeFixtures.list("actors/shared_actor.json"));
        String uid = manifest.get("story_id")
            .getAsString();
        com.google.gson.JsonObject membership = CurrentPackageProbeFixtures.membership(uid);
        com.google.gson.JsonArray actors = new com.google.gson.JsonArray();
        actors.add(CurrentPackageProbeFixtures.address(key));
        membership
            .getAsJsonObject(CurrentPackageProbeFixtures.A.equals(uid) ? "owned_resources" : "referenced_resources")
            .add("actors", actors);
        write(new File(directory, CurrentPackageProbeFixtures.membershipPath(uid)), membership.toString());
        write(file, manifest.toString());
        archive(directory);
    }

    private static void writeLogicPackage(File directory, String storyId, boolean source) throws Exception {
        writePackage(directory, storyId, storyId, false, storyId);
        com.google.gson.JsonObject story = CurrentPackageProbeFixtures.story(storyId, storyId);
        String nodeId = source ? "output" : "input";
        com.google.gson.JsonObject node = new com.google.gson.JsonObject();
        node.addProperty("id", nodeId);
        node.addProperty("display_name", nodeId);
        node.addProperty("type", source ? "logic_output" : "logic_input");
        com.google.gson.JsonObject port = new com.google.gson.JsonObject();
        port.addProperty("port_id", source ? "logic_in" : "logic_out");
        port.addProperty("display_name", "Logic");
        port.addProperty("direction", source ? "input" : "output");
        port.addProperty("kind", "logic");
        port.addProperty("order", 0);
        com.google.gson.JsonArray ports = new com.google.gson.JsonArray();
        ports.add(port);
        node.add("ports", ports);
        com.google.gson.JsonObject properties = new com.google.gson.JsonObject();
        properties.addProperty("port_id", source ? "signal" : "gate");
        properties.addProperty("display_name", nodeId);
        if (source) properties.addProperty("display_order", 0);
        node.add("properties", properties);
        story.getAsJsonObject("graph")
            .getAsJsonArray("nodes")
            .add(node);
        write(new File(directory, CurrentPackageProbeFixtures.storyPath(storyId)), story.toString());
        if (source) {
            com.google.gson.JsonObject logic = new com.google.gson.JsonObject();
            logic.addProperty("schema_version", 2);
            com.google.gson.JsonObject connection = new com.google.gson.JsonObject();
            connection.addProperty("source_story_id", CurrentPackageProbeFixtures.SOURCE);
            connection.addProperty("source_port_id", "signal");
            connection.addProperty("target_story_id", CurrentPackageProbeFixtures.TARGET);
            connection.addProperty("target_port_id", "gate");
            connection.addProperty("interface_kind", "Logic");
            com.google.gson.JsonArray connections = new com.google.gson.JsonArray();
            connections.add(connection);
            logic.add("connections", connections);
            write(new File(directory, "resources/story_logic_graph.json"), logic.toString());
            com.google.gson.JsonObject manifest = CurrentPackageProbeFixtures.manifest(storyId);
            manifest.getAsJsonObject("required_resources")
                .addProperty("story_logic_graph", "resources/story_logic_graph.json");
            write(new File(directory, "manifest.json"), manifest.toString());
        }
        archive(directory);
    }

    private static void writeLogicGroup(File root) throws Exception {
        com.google.gson.JsonObject group = new com.google.gson.JsonObject();
        group.addProperty("format", "dgrs.g");
        group.addProperty("format_version", 3);
        group.addProperty("identity_format", "story-uid-v1");
        group.addProperty("display_name", "Logic group");
        group.addProperty("connections", StoryGroupPackageReader.CONNECTIONS);
        com.google.gson.JsonArray members = new com.google.gson.JsonArray();
        members.add(CurrentPackageProbeFixtures.manifest(CurrentPackageProbeFixtures.SOURCE));
        members.add(CurrentPackageProbeFixtures.manifest(CurrentPackageProbeFixtures.TARGET));
        group.add("members", members);
        java.util.Map<String, byte[]> entries = new java.util.LinkedHashMap<String, byte[]>();
        entries.put(
            "manifest.json",
            group.toString()
                .getBytes(StandardCharsets.UTF_8));
        entries.put(
            "project.json",
            Files.readAllBytes(new File(root, CurrentPackageProbeFixtures.SOURCE + "/project.json").toPath()));
        entries.put(
            StoryGroupPackageReader.CONNECTIONS,
            Files.readAllBytes(
                new File(root, CurrentPackageProbeFixtures.SOURCE + "/resources/story_logic_graph.json").toPath()));
        for (String uid : Arrays.asList(CurrentPackageProbeFixtures.SOURCE, CurrentPackageProbeFixtures.TARGET)) {
            File directory = new File(root, uid);
            for (String relative : Arrays
                .asList(CurrentPackageProbeFixtures.storyPath(uid), CurrentPackageProbeFixtures.membershipPath(uid))) {
                entries.put(relative, Files.readAllBytes(new File(directory, relative).toPath()));
            }
            Files.delete(new File(root, uid + ".dgrs").toPath());
        }
        try (java.util.zip.ZipOutputStream zip = new java.util.zip.ZipOutputStream(
            new java.io.FileOutputStream(new File(root, "logic.dgrs.g")))) {
            for (java.util.Map.Entry<String, byte[]> entry : entries.entrySet()) {
                zip.putNextEntry(new java.util.zip.ZipEntry(entry.getKey()));
                zip.write(entry.getValue());
                zip.closeEntry();
            }
        }
    }

    private static void archive(File directory) throws Exception {
        try (java.util.zip.ZipOutputStream zip = new java.util.zip.ZipOutputStream(
            new java.io.FileOutputStream(new File(directory.getParentFile(), directory.getName() + ".dgrs")))) {
            try (java.util.stream.Stream<java.nio.file.Path> files = Files.walk(directory.toPath())) {
                for (java.nio.file.Path path : (Iterable<java.nio.file.Path>) files
                    .filter(Files::isRegularFile)::iterator) {
                    zip.putNextEntry(
                        new java.util.zip.ZipEntry(
                            directory.toPath()
                                .relativize(path)
                                .toString()
                                .replace('\\', '/')));
                    zip.write(Files.readAllBytes(path));
                    zip.closeEntry();
                }
            }
        }
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new IllegalStateException(message);
    }

    private static void delete(File file) {
        if (file.isDirectory()) {
            File[] children = file.listFiles();
            if (children != null) for (File child : children) delete(child);
        }
        file.delete();
    }
}
