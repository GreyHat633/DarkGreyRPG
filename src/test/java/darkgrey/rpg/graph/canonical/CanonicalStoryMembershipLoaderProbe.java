package darkgrey.rpg.graph.canonical;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.ArrayList;
import java.util.Arrays;
import java.util.Comparator;
import java.util.List;
import java.util.stream.Stream;

/** Executable acceptance matrix for schema-version-1 Story membership loading. */
public final class CanonicalStoryMembershipLoaderProbe {

    private CanonicalStoryMembershipLoaderProbe() {}

    public static void main(String[] args) throws Exception {
        require(args.length == 1, "Expected one repository-local probe directory argument");
        Path root = Paths.get(args[0])
            .toAbsolutePath()
            .normalize();
        delete(root);
        try {
            Path memberships = Files.createDirectories(root.resolve("memberships"));
            write(memberships.resolve("story_b.json"), json("story_b"));
            write(memberships.resolve("story_a.json"), json("story_a"));
            CanonicalStoryMembershipLoader loader = new CanonicalStoryMembershipLoader();
            verifyValid(loader, memberships);
            verifySchemaTwo(loader, memberships);
            verifyFailures(loader, memberships);
            verifyDirectory(loader, root, memberships);
            System.out.println("CANONICAL_STORY_MEMBERSHIP_PROBE=PASS");
        } finally {
            delete(root);
        }
    }

    private static void verifySchemaTwo(CanonicalStoryMembershipLoader loader, Path directory) throws Exception {
        Path file = directory.resolve("story_items.json");
        write(
            file,
            "{\"schema_version\":2,\"story_id\":\"story_items\","
                + "\"owned_resources\":{\"actors\":[],\"items\":[\"royal_key\"],"
                + "\"item_groups\":[\"swords\"],\"sessions\":[],\"tasks\":[]},"
                + "\"referenced_resources\":{\"actors\":[],\"items\":[],\"item_groups\":[],"
                + "\"sessions\":[],\"tasks\":[]}}");
        CanonicalStoryMembership membership = loader.load(file);
        require(membership.getSchemaVersion() == 2, "Schema 2 identity changed");
        require(
            Arrays.asList("royal_key")
                .equals(
                    membership.getOwnedResources()
                        .getItems()),
            "Item membership changed");
        require(
            Arrays.asList("swords")
                .equals(
                    membership.getOwnedResources()
                        .getItemGroups()),
            "Item Group membership changed");
        Files.delete(file);
    }

    private static void verifyValid(CanonicalStoryMembershipLoader loader, Path directory) throws Exception {
        CanonicalStoryMembership membership = loader.load(directory.resolve("story_a.json"));
        require(membership.getSchemaVersion() == 1, "Schema identity changed");
        require("story_a".equals(membership.getStoryId()), "Story identity changed");
        require(
            Arrays.asList("actor_b", "actor_a")
                .equals(
                    membership.getOwnedResources()
                        .getActors()),
            "Actor order changed");
        require(
            Arrays.asList("session_b")
                .equals(
                    membership.getReferencedResources()
                        .getSessions()),
            "Session identity changed");
        try {
            membership.getOwnedResources()
                .getActors()
                .clear();
            throw new AssertionError("Membership list is mutable");
        } catch (UnsupportedOperationException expected) {}
        List<String> source = new ArrayList<String>(Arrays.asList("actor_source"));
        CanonicalStoryMembershipSet detached = new CanonicalStoryMembershipSet(
            source,
            Arrays.asList("session_source"),
            Arrays.asList("task_source"));
        source.set(0, "mutated");
        require(
            "actor_source".equals(
                detached.getActors()
                    .get(0)),
            "Constructor retained a mutable list alias");
    }

    private static void verifyFailures(CanonicalStoryMembershipLoader loader, Path directory) throws Exception {
        Path file = directory.resolve("bad.json");
        expect(loader, file, json("story_a"), "story.membership.filename.mismatch");
        expect(
            loader,
            file,
            json("bad").replace("\"schema_version\":1", "\"schema_version\":99"),
            "story.membership.schema_version.unsupported");
        expect(
            loader,
            file,
            json("bad").replace("\"story_id\":\"bad\"", "\"story_id\":\"BAD\""),
            "story.membership.story_id.invalid");
        expect(
            loader,
            file,
            json("bad").replace("\"actors\":[\"actor_b\",\"actor_a\"]", "\"actors\":[\"actor_a\",\"actor_a\"]"),
            "story.membership.actor.id.duplicate");
        expect(
            loader,
            file,
            json("bad").replace("\"sessions\":[\"session_a\"]", "\"sessions\":[\"session_a\",\"session_a\"]"),
            "story.membership.session.id.duplicate");
        expect(
            loader,
            file,
            json("bad").replace("\"tasks\":[\"task_a\"]", "\"tasks\":[\"task_a\",\"task_a\"]"),
            "story.membership.task.id.duplicate");
        expect(
            loader,
            file,
            json("bad").replace("\"actors\":[\"actor_c\"]", "\"actors\":[\"actor_c\",\"actor_c\"]"),
            "story.membership.actor.id.duplicate");
        expect(
            loader,
            file,
            json("bad").replace("\"sessions\":[\"session_b\"]", "\"sessions\":[\"session_b\",\"session_b\"]"),
            "story.membership.session.id.duplicate");
        expect(
            loader,
            file,
            json("bad").replace("\"tasks\":[\"task_b\"]", "\"tasks\":[\"task_b\",\"task_b\"]"),
            "story.membership.task.id.duplicate");
        expect(
            loader,
            file,
            json("bad").replace("\"actors\":[\"actor_c\"]", "\"actors\":[\"actor_a\"]"),
            "story.membership.actor.ownership.overlap");
        expect(
            loader,
            file,
            json("bad").replace("\"sessions\":[\"session_b\"]", "\"sessions\":[\"session_a\"]"),
            "story.membership.session.ownership.overlap");
        expect(
            loader,
            file,
            json("bad").replace("\"tasks\":[\"task_b\"]", "\"tasks\":[\"task_a\"]")
                .replace("\"tasks\":[]", "\"tasks\":[\"task_a\"]"),
            "story.membership.task.ownership.overlap");
        expect(
            loader,
            file,
            json("bad").replace("\"actors\":[\"actor_b\",\"actor_a\"]", "\"actors\":[1,\"actor_a\"]"),
            "story.membership.id.type");
        expect(
            loader,
            file,
            json("bad").replace("\"referenced_resources\":{", "\"extra\":0,\"referenced_resources\":{"),
            "story.membership.root.member.unsupported");
        expect(
            loader,
            file,
            json("bad").replace(",\"referenced_resources\":", ",\"duplicate\":{},\"referenced_resources\":"),
            "story.membership.root.member.unsupported");
        expect(
            loader,
            file,
            json("bad").replace(
                ",\"referenced_resources\":{\"actors\":[\"actor_c\"],\"sessions\":[\"session_b\"],\"tasks\":[\"task_b\"]}",
                ""),
            "story.membership.root.member.required");
        expect(
            loader,
            file,
            json("bad").replace("\"story_id\":\"bad\"", "\"story_id\":\"bad\",\"story_id\":\"again\""),
            "story.membership.root.member.duplicate");
        expect(
            loader,
            file,
            json("bad")
                .replace("\"actors\":[\"actor_b\",\"actor_a\"]", "\"actors\":[\"actor_b\",\"actor_a\"],\"extra\":[]"),
            "story.membership.set.member.unsupported");
        expect(
            loader,
            file,
            json("bad").replace("\"actors\":[\"actor_b\",\"actor_a\"],", ""),
            "story.membership.set.member.required");
        expect(
            loader,
            file,
            json("bad")
                .replace("\"actors\":[\"actor_b\",\"actor_a\"]", "\"actors\":[\"actor_b\"],\"actors\":[\"actor_a\"]"),
            "story.membership.set.member.duplicate");
        expect(
            loader,
            file,
            json("bad").replace("\"actors\":[\"actor_b\",\"actor_a\"]", "\"actors\":null"),
            "story.membership.list.invalid");
        expect(
            loader,
            file,
            json("bad").replace(
                "\"owned_resources\":{\"actors\":[\"actor_b\",\"actor_a\"],\"sessions\":[\"session_a\"],\"tasks\":[\"task_a\"]}",
                "\"owned_resources\":null"),
            "story.membership.set.invalid");
        expect(
            loader,
            file,
            json("bad").replace("\"story_id\":\"bad\"", "\"story_id\":null"),
            "story.membership.root.member.type");
        expect(
            loader,
            file,
            json("bad").replace("\"schema_version\":1", "\"schema_version\":1.5"),
            "story.membership.root.member.type");
        expect(
            loader,
            file,
            json("bad").replace("\"sessions\":[\"session_a\"]", "\"sessions\":[\"Bad\"]"),
            "story.membership.session.id.invalid");
        expect(loader, file, "[]", "story.membership.root.invalid");
        expect(loader, file, "{bad", "story.membership.invalid");
        expect(loader, file, json("bad") + "{}", "story.membership.invalid");
    }

    private static void verifyDirectory(CanonicalStoryMembershipLoader loader, Path root, Path directory)
        throws Exception {
        Files.deleteIfExists(directory.resolve("bad.json"));
        List<CanonicalStoryMembership> loaded = loader.loadDirectory(directory);
        require(loaded.size() == 2, "Directory count changed");
        require(
            "story_a".equals(
                loaded.get(0)
                    .getStoryId())
                && "story_b".equals(
                    loaded.get(1)
                        .getStoryId()),
            "Directory order is not ordinal");
        try {
            loaded.clear();
            throw new AssertionError("Directory result is mutable");
        } catch (UnsupportedOperationException expected) {}
        Path absent = root.resolve("absent");
        expectDirectory(loader, absent);
        require(!Files.exists(absent), "Missing directory was created");
    }

    private static void expect(CanonicalStoryMembershipLoader loader, Path file, String text, String code)
        throws Exception {
        write(file, text);
        try {
            loader.load(file);
            throw new AssertionError("Expected " + code);
        } catch (CanonicalStoryMembershipException exception) {
            require(code.equals(exception.getCode()), "Expected " + code + ", got " + exception.getCode());
        }
    }

    private static void expectDirectory(CanonicalStoryMembershipLoader loader, Path directory) throws Exception {
        try {
            loader.loadDirectory(directory);
            throw new AssertionError("Expected missing directory");
        } catch (CanonicalStoryMembershipException exception) {
            require("story.membership.directory.missing".equals(exception.getCode()), "Unexpected directory code");
        }
    }

    private static String json(String storyId) {
        return "{\"schema_version\":1,\"story_id\":\"" + storyId
            + "\",\"owned_resources\":{\"actors\":[\"actor_b\",\"actor_a\"],\"sessions\":[\"session_a\"],\"tasks\":[\"task_a\"]},\"referenced_resources\":{\"actors\":[\"actor_c\"],\"sessions\":[\"session_b\"],\"tasks\":[\"task_b\"]}}";
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
