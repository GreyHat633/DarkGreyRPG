package darkgrey.rpg.project;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.Comparator;
import java.util.stream.Stream;

/** Repository-level proof that canonical content shares the atomic reload boundary. */
public final class CanonicalProjectRepositoryProbe {

    private CanonicalProjectRepositoryProbe() {}

    public static void main(String[] args) throws Exception {
        require(args.length == 1, "Expected one repository-local probe directory argument");
        Path project = Paths.get(args[0])
            .toAbsolutePath()
            .normalize();
        delete(project);
        try {
            prepareLegacyProject(project);
            ProjectRepository repository = new ProjectRepository(project.toFile());
            ProjectRepository.ReloadResult legacyReload = repository.reload();
            require(legacyReload.isSuccessful(), legacyReload.getSummary());
            require(
                repository.getSnapshot()
                    .getCanonicalContent()
                    .getStories()
                    .isEmpty(),
                "Legacy-only project fabricated canonical content");
            require(
                !Files.exists(project.resolve("resources/canonical")),
                "Legacy-only reload created the canonical root");

            prepareCanonicalContent(project);
            ProjectRepository.ReloadResult canonicalReload = repository.reload();
            require(canonicalReload.isSuccessful(), canonicalReload.getSummary());
            ProjectSnapshot accepted = repository.getSnapshot();
            require(accepted.getCanonicalStory("canonical_story") != null, "Canonical Story was not loaded");
            require(accepted.getCanonicalSession("canonical_session") != null, "Canonical Session was not loaded");
            require(accepted.getCanonicalTask("canonical_task") != null, "Canonical Task was not loaded");
            require(
                accepted.getCanonicalStoryMembership("canonical_story") != null,
                "Canonical membership was not loaded");
            require(accepted.getActor("actor_a") != null, "Legacy Actor disappeared during canonical load");

            Path story = project.resolve("resources/canonical/stories/canonical_story.json");
            write(story, "{bad");
            ProjectRepository.ReloadResult rejected = repository.reload();
            require(!rejected.isSuccessful(), "Malformed canonical content was accepted");
            require(repository.getSnapshot() == accepted, "Failed canonical reload replaced the accepted snapshot");
            require(
                repository.getSnapshot()
                    .getCanonicalStory("canonical_story") != null,
                "Failed canonical reload damaged the accepted snapshot");

            System.out.println("RUNTIME_CANONICAL_PROJECT_LOAD=PASS");
            System.out.println("RUNTIME_CANONICAL_ABSENT_ROOT_COMPATIBILITY=PASS");
            System.out.println("RUNTIME_CANONICAL_FAILED_RELOAD_ROLLBACK=PASS");
        } finally {
            delete(project);
        }
    }

    private static void prepareLegacyProject(Path project) throws IOException {
        Files.createDirectories(project.resolve("actors"));
        Files.createDirectories(project.resolve("dialogues"));
        Files.createDirectories(project.resolve("quests"));
        Files.createDirectories(project.resolve("stories"));
        write(
            project.resolve("project.json"),
            "{\"schema_version\":1,\"id\":\"canonical_probe\",\"display_name\":\"Canonical Probe\"}");
        write(
            project.resolve("actors/actor_a.json"),
            "{\"schema_version\":1,\"id\":\"actor_a\",\"display_name\":\"Actor A\",\"notes\":\"\",\"tags\":[],\"home_story_id\":\"\"}");
    }

    private static void prepareCanonicalContent(Path project) throws IOException {
        Path canonical = project.resolve("resources/canonical");
        Files.createDirectories(canonical.resolve("stories"));
        Files.createDirectories(canonical.resolve("sessions"));
        Files.createDirectories(canonical.resolve("tasks"));
        Files.createDirectories(canonical.resolve("memberships"));
        write(canonical.resolve("stories/canonical_story.json"), graphJson("story", "canonical_story", "start"));
        write(canonical.resolve("sessions/canonical_session.json"), graphJson("session", "canonical_session", "start"));
        write(canonical.resolve("tasks/canonical_task.json"), taskJson());
        write(
            canonical.resolve("memberships/canonical_story.json"),
            "{\"schema_version\":1,\"story_id\":\"canonical_story\","
                + "\"owned_resources\":{\"actors\":[\"actor_a\"],\"sessions\":[\"canonical_session\"],\"tasks\":[\"canonical_task\"]},"
                + "\"referenced_resources\":{\"actors\":[],\"sessions\":[],\"tasks\":[]}}");
    }

    private static String graphJson(String kind, String id, String requiredType) {
        return "{\"schema_version\":1,\"resource_kind\":\"" + kind
            + "\",\"id\":\""
            + id
            + "\",\"display_name\":\""
            + id
            + "\",\"graph\":{\"nodes\":[{\"id\":\"required\",\"type\":\""
            + requiredType
            + "\",\"display_name\":\"Required\",\"ports\":[],\"properties\":{}}],\"connections\":[]}}";
    }

    private static String taskJson() {
        return "{\"schema_version\":1,\"resource_kind\":\"task\",\"id\":\"canonical_task\","
            + "\"display_name\":\"canonical_task\",\"graph\":{\"nodes\":["
            + "{\"id\":\"activate\",\"type\":\"activate\",\"display_name\":\"Activate\",\"ports\":[],\"properties\":{}},"
            + "{\"id\":\"settle\",\"type\":\"settle\",\"display_name\":\"Settle\",\"ports\":[],\"properties\":{}}],\"connections\":[]}}";
    }

    private static void write(Path path, String text) throws IOException {
        Files.write(path, text.getBytes(StandardCharsets.UTF_8));
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }

    private static void delete(Path path) throws IOException {
        if (!Files.exists(path)) return;
        try (Stream<Path> paths = Files.walk(path)) {
            Path[] ordered = paths.sorted(Comparator.reverseOrder())
                .toArray(Path[]::new);
            for (Path item : ordered) Files.delete(item);
        }
    }
}
