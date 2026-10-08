package darkgrey.rpg.project;

import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.Arrays;

import darkgrey.rpg.live.LiveBridgeServer;
import darkgrey.rpg.live.LiveStateSnapshotBuilder;

/** Current project load, transactional reload, field rejection, and read-only live projection. */
public final class ProjectRepositoryProbe {

    private ProjectRepositoryProbe() {}

    public static void main(String[] args) throws Exception {
        Path root = Files.createTempDirectory(Paths.get(".tooling"), "0400-project-repository-");
        Path actors = Files.createDirectories(root.resolve("actors"));
        Path project = root.resolve("project.json");
        String meta = "{\"schema_version\":3,\"identity_format\":\"story-uid-v1\",\"id\":\"current_probe\",\"display_name\":\"Current Probe\"}";
        Files.write(project, meta.getBytes(StandardCharsets.UTF_8));
        Path actor = actors.resolve("actor.json");
        String definition = "{\"schema_version\":5,\"identity_format\":\"story-uid-v1\",\"type\":\"individual\","
            + "\"npc_id\":{\"story_uid\":\"ST-2345-6789-ABCD-EFGH\",\"kind\":\"actor\",\"local_id\":\"hero\"},"
            + "\"display_name\":\"Probe Actor\",\"tags\":[],\"home_story_id\":\"ST-2345-6789-ABCD-EFGH\"}";
        Files.write(actor, definition.getBytes(StandardCharsets.UTF_8));
        ProjectRepository repository = new ProjectRepository(root.toFile());
        ProjectRepository.ReloadResult loaded = repository.reload();
        require(loaded.isSuccessful(), loaded.getSummary());
        ProjectSnapshot snapshot = repository.getSnapshot();
        long revision = repository.getSnapshotRevision();
        require(
            loaded.getActorCount() == 1 && loaded.getStoryCount() == 0 && loaded.getTaskCount() == 0,
            "current counts");
        require(
            "Probe Actor".equals(
                snapshot.getActor("ST-2345-6789-ABCD-EFGH~actor~hero")
                    .getDisplayName()),
            "typed actor");
        String invalid = definition.substring(0, definition.length() - 1) + ",\"dialogue\":\"forbidden\"}";
        Files.write(actor, invalid.getBytes(StandardCharsets.UTF_8));
        byte[] originalInvalid = Files.readAllBytes(actor);
        require(
            !repository.reload()
                .isSuccessful() && repository.getSnapshot() == snapshot && repository.getSnapshotRevision() == revision,
            "failed reload retained snapshot and revision");
        require(Arrays.equals(originalInvalid, Files.readAllBytes(actor)), "rejected actor file unchanged");
        Files.write(actor, definition.getBytes(StandardCharsets.UTF_8));
        Path retired = Files.createDirectories(root.resolve("stories"))
            .resolve("retired.json");
        Files.write(retired, "{\"schema_version\":2,\"id\":\"old\"}".getBytes(StandardCharsets.UTF_8));
        byte[] old = Files.readAllBytes(retired);
        require(
            !repository.reload()
                .isSuccessful() && repository.getSnapshot() == snapshot,
            "retired directory rejected without empty fallback");
        require(Arrays.equals(old, Files.readAllBytes(retired)), "retired source file unchanged");
        Files.delete(retired); // Only the generated sample file, never a user project.
        require(
            repository.reload()
                .isSuccessful() && repository.getSnapshotRevision() == revision + 1,
            "successful reload committed exactly one revision");
        com.google.gson.JsonObject live = new LiveStateSnapshotBuilder(repository).build();
        require(
            live.getAsJsonObject("counts")
                .get("actors")
                .getAsInt() == 1
                && !live.getAsJsonObject("counts")
                    .has("quests")
                && !live.getAsJsonObject("counts")
                    .has("dialogues"),
            "current live counts only");
        require(LiveBridgeServer.PROTOCOL_VERSION == 1, "live protocol was unnecessarily bumped");
        System.out.println("CURRENT_PROJECT_LOAD_ACTOR_SCOPE_ATOMIC_REVISION=PASS");
        System.out.println("CURRENT_PROJECT_RETIRED_DIRECTORY_NO_REWRITE=PASS");
        System.out.println("CURRENT_LIVE_READ_ONLY_PROJECTION=PASS");
    }

    private static void require(boolean value, String label) {
        if (!value) throw new AssertionError(label);
    }
}
