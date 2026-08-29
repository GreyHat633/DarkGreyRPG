package darkgrey.rpg.project.packages;

import java.io.File;
import java.io.FileWriter;
import java.nio.file.Files;

/** Focused Stage 3 proof: load, replacement, package isolation, rollback, and shared progress. */
public final class StoryPackageLoaderProbe {

    public static void main(String[] args) throws Exception {
        File root = Files.createTempDirectory("dgr-story-packages")
            .toFile();
        try {
            File first = new File(root, "alpha");
            writePackage(first, "alpha", "Alpha", false);
            addSharedActor(first, "Shared Actor");
            StoryPackageLoader loader = new StoryPackageLoader(root);
            require(
                loader.reload()
                    .isSuccessful(),
                "initial package did not load");
            require(
                "Alpha".equals(
                    loader.getPackage("alpha")
                        .getSnapshot()
                        .getStory("story")
                        .getTitle()),
                "initial title mismatch");

            writePackage(first, "alpha", "Updated", false);
            addSharedActor(first, "Shared Actor");
            require(
                loader.reload()
                    .isSuccessful(),
                "replacement package did not load");
            require(
                "Updated".equals(
                    loader.getPackage("alpha")
                        .getSnapshot()
                        .getStory("story")
                        .getTitle()),
                "replacement title mismatch");

            File second = new File(root, "beta");
            writePackage(second, "beta", "Beta", false, "beta_story");
            addSharedActor(second, "Shared Actor");
            require(
                loader.reload()
                    .isSuccessful() && loader.getPackage("beta") != null,
                "second package with an identical shared Actor did not load");
            addSharedActor(second, "Conflicting Actor");
            StoryPackageLoader.ReloadResult sharedConflict = loader.reload();
            require(!sharedConflict.isSuccessful(), "conflicting shared Actor was accepted");
            require(
                "Shared Actor".equals(
                    loader.getPackage("beta")
                        .getSnapshot()
                        .getActor("shared_actor")
                        .getDisplayName()),
                "conflicting shared Actor replaced the active package set");
            addSharedActor(second, "Shared Actor");
            require(
                loader.reload()
                    .isSuccessful(),
                "shared Actor repair did not reload");
            write(new File(first, "manifest.json"), "{");
            StoryPackageLoader.ReloadResult rejected = loader.reload();
            require(
                !rejected.isSuccessful() && "Updated".equals(
                    loader.getPackage("alpha")
                        .getSnapshot()
                        .getStory("story")
                        .getTitle()),
                "corrupt manifest did not retain prior definition");
            require(loader.getPackage("beta") != null, "corrupt manifest disabled another package");
            writePackage(new File(root, "gamma"), "gamma", "Gamma", false, "beta_story");
            StoryPackageLoader.ReloadResult duplicate = loader.reload();
            require(!duplicate.isSuccessful(), "cross-package duplicate Story ID was accepted");
            require(loader.getPackage("gamma") == null, "invalid merged package set replaced active packages");
            require(
                loader.getPackage("alpha") != null && loader.getPackage("beta") != null,
                "merge conflict disabled active packages");
            System.out.println("STORY_PACKAGE_LOAD_REPLACE_ROLLBACK_ISOLATION=PASS");
        } finally {
            delete(root);
        }
    }

    private static void writePackage(File directory, String packageId, String title, boolean broken) throws Exception {
        writePackage(directory, packageId, title, broken, "story");
    }

    private static void writePackage(File directory, String packageId, String title, boolean broken, String storyId)
        throws Exception {
        new File(directory, "actors").mkdirs();
        new File(directory, "dialogues").mkdirs();
        new File(directory, "quests").mkdirs();
        new File(directory, "stories").mkdirs();
        write(
            new File(directory, "project.json"),
            "{\"schema_version\":1,\"id\":\"" + packageId + "\",\"display_name\":\"" + packageId + "\"}");
        write(
            new File(directory, "stories/" + storyId + ".json"),
            broken ? "{}"
                : "{\"schema_version\":1,\"id\":\"" + storyId
                    + "\",\"title\":\""
                    + title
                    + "\",\"entry\":\"end\",\"nodes\":[{\"id\":\"end\",\"type\":\"END\",\"position\":{\"x\":0,\"y\":0},\"properties\":{}}],\"connections\":[],\"metadata\":{\"notes\":\"\",\"tags\":[]}}");
        write(
            new File(directory, "manifest.json"),
            "{\"schema_version\":1,\"package_id\":\"" + packageId
                + "\",\"package_version\":\"1.0.0\",\"story_id\":\""
                + storyId
                + "\",\"story_schema_version\":1,\"required_resources\":{\"story\":\"stories/"
                + storyId
                + ".json\",\"actors\":[],\"items\":[],\"item_groups\":[],\"dialogues\":[],\"quests\":[],\"canonical_stories\":[],\"canonical_memberships\":[],\"sessions\":[],\"tasks\":[]}}");
    }

    private static void write(File file, String value) throws Exception {
        File parent = file.getParentFile();
        if (parent != null) parent.mkdirs();
        FileWriter writer = new FileWriter(file);
        try {
            writer.write(value);
        } finally {
            writer.close();
        }
    }

    private static void addSharedActor(File directory, String displayName) throws Exception {
        write(
            new File(directory, "actors/shared_actor.json"),
            "{\"schema_version\":3,\"type\":\"individual\",\"npc_id\":\"shared_actor\"," + "\"display_name\":\""
                + displayName
                + "\",\"tags\":[],\"home_story_id\":\"story\"}");
        File manifest = new File(directory, "manifest.json");
        String json = new String(Files.readAllBytes(manifest.toPath()), java.nio.charset.StandardCharsets.UTF_8);
        json = json.replace("\"actors\":[]", "\"actors\":[\"actors/shared_actor.json\"]");
        write(manifest, json);
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
