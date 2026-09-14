package darkgrey.rpg.project;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.Comparator;
import java.util.stream.Stream;

/** Runtime proof for the 0.3.1 identity-only Actor shapes and reload rollback. */
public final class ActorSchema3ProjectRepositoryProbe {

    private ActorSchema3ProjectRepositoryProbe() {}

    public static void main(String[] args) throws Exception {
        require(args.length == 1, "Expected one repository-local probe directory argument");
        Path project = Paths.get(args[0])
            .toAbsolutePath()
            .normalize();
        delete(project);
        try {
            prepare(project);
            ProjectRepository repository = new ProjectRepository(project.toFile());
            ProjectRepository.ReloadResult accepted = repository.reload();
            require(accepted.isSuccessful(), accepted.getSummary());
            ProjectSnapshot snapshot = repository.getSnapshot();
            ActorDefinition boss = snapshot.getActor("tavern_boss");
            ActorDefinition guards = snapshot.getActor("guards");
            require(boss != null && boss.isIndividual(), "Individual Actor type was not retained");
            require(guards != null && guards.isCollective(), "Collective Actor type was not retained");
            require(
                boss.getNotes()
                    .isEmpty()
                    && guards.getNotes()
                        .isEmpty(),
                "Schema 3 fabricated legacy notes");

            write(
                project.resolve("actors/guards.json"),
                "{\"schema_version\":4,\"type\":\"collective\",\"npc_id\":\"guards\","
                    + "\"group_id\":\"guards\",\"display_name\":\"Guards\",\"tags\":[],"
                    + "\"home_story_id\":\"uncategorized\"}");
            ProjectRepository.ReloadResult rejected = repository.reload();
            require(!rejected.isSuccessful(), "Ambiguous Actor identity was accepted");
            require(repository.getSnapshot() == snapshot, "Rejected Actor reload replaced the accepted snapshot");

            System.out.println("RUNTIME_ACTOR_V3_INDIVIDUAL_COLLECTIVE=PASS");
            System.out.println("RUNTIME_ACTOR_V3_STRICT_IDENTITY=PASS");
            System.out.println("RUNTIME_ACTOR_V3_RELOAD_ROLLBACK=PASS");
        } finally {
            delete(project);
        }
    }

    private static void prepare(Path project) throws IOException {
        Files.createDirectories(project.resolve("actors"));
        Files.createDirectories(project.resolve("dialogues"));
        Files.createDirectories(project.resolve("quests"));
        Files.createDirectories(project.resolve("stories"));
        write(
            project.resolve("project.json"),
            "{\"schema_version\":1,\"id\":\"actor_v3_probe\",\"display_name\":\"Actor v3 Probe\"}");
        write(
            project.resolve("actors/tavern_boss.json"),
            "{\"schema_version\":4,\"type\":\"individual\",\"npc_id\":\"tavern_boss\","
                + "\"display_name\":\"Tavern Boss\",\"tags\":[\"merchant\"],"
                + "\"home_story_id\":\"uncategorized\"}");
        write(
            project.resolve("actors/guards.json"),
            "{\"schema_version\":4,\"type\":\"collective\",\"group_id\":\"guards\","
                + "\"display_name\":\"Guards\",\"tags\":[\"guard\"],"
                + "\"home_story_id\":\"uncategorized\"}");
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
