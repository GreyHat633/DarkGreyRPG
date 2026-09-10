package darkgrey.rpg.identity;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.Comparator;
import java.util.List;
import java.util.UUID;
import java.util.stream.Stream;

import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceException;
import darkgrey.rpg.graph.canonical.CanonicalGraphResourceLoader;
import darkgrey.rpg.graph.canonical.CanonicalStoryMembership;
import darkgrey.rpg.graph.canonical.CanonicalStoryMembershipException;
import darkgrey.rpg.graph.canonical.CanonicalStoryMembershipLoader;
import darkgrey.rpg.item.identity.ItemIdentityRegistry;
import darkgrey.rpg.item.identity.ItemStackDefinition;

/** Executable probe for namespaced resource loading and identity coexistence. */
public final class NamespacedResourceLoadingProbe {

    private NamespacedResourceLoadingProbe() {}

    public static void main(String[] args) throws Exception {
        require(args.length == 1, "Expected one E-drive isolated probe directory argument");
        Path parent = Paths.get(args[0])
            .toAbsolutePath()
            .normalize();
        require(
            parent.startsWith(
                Paths.get("E:/Java/MinecraftMod/DarkGrey_RPG/.tooling")
                    .toAbsolutePath()
                    .normalize()),
            "Probe artifacts must stay under the repository E-drive tooling directory");
        Files.createDirectories(parent);
        Path root = Files.createTempDirectory(parent, "namespace-loader-");
        try {
            verifyGraphResources(root.resolve("graphs"));
            verifyMembershipResources(root.resolve("memberships"));
            verifyRegistries();
            System.out.println("NAMESPACED_RESOURCE_LOADING_PROBE=PASS");
        } finally {
            delete(root);
        }
    }

    private static void verifyGraphResources(Path directory) throws Exception {
        Files.createDirectories(directory.resolve("nested"));
        CanonicalGraphResourceLoader loader = new CanonicalGraphResourceLoader();
        write(directory.resolve("nested/arbitrary-name.json"), graph("Team:Guard"));
        write(directory.resolve("second.json"), graph("Team:guard"));
        write(directory.resolve("legacy.json"), graph("legacy"));
        List<CanonicalGraphResource> resources = loader.loadDirectory(directory, null);
        require(resources.size() == 3, "Namespaced graph resources did not coexist");
        require(containsGraphId(resources, "Team:Guard"), "Uppercase local graph ID was lost");
        require(containsGraphId(resources, "Team:guard"), "Case-distinct graph ID was folded");
        require(containsGraphId(resources, "legacy"), "Bare graph compatibility changed");

        expectGraphFailure(
            loader,
            graph("Team:Guard"),
            "payload.json",
            "graph.resource.id.duplicate",
            directory.resolve("duplicate"));
        expectGraphFailure(
            loader,
            graph("Team:_Guard"),
            "invalid.json",
            "graph.resource.id.invalid",
            directory.resolve("invalid"));
        expectGraphFailure(
            loader,
            graph("legacy"),
            "wrong.json",
            "graph.resource.filename.mismatch",
            directory.resolve("bare-name-check"));
    }

    private static void verifyMembershipResources(Path directory) throws Exception {
        Files.createDirectories(directory.resolve("nested"));
        CanonicalStoryMembershipLoader loader = new CanonicalStoryMembershipLoader();
        write(directory.resolve("nested/arbitrary-name.json"), membership("Team:Guard"));
        write(directory.resolve("second.json"), membership("Team:guard"));
        write(directory.resolve("legacy.json"), membership("legacy"));
        List<CanonicalStoryMembership> resources = loader.loadDirectory(directory);
        require(resources.size() == 3, "Namespaced memberships did not coexist");
        require(containsMembershipId(resources, "Team:Guard"), "Uppercase local membership ID was lost");
        require(containsMembershipId(resources, "Team:guard"), "Case-distinct membership ID was folded");
        require(containsMembershipId(resources, "legacy"), "Bare membership compatibility changed");

        expectMembershipFailure(
            loader,
            membership("Team:Guard"),
            "duplicate.json",
            "story.membership.story_id.duplicate",
            directory.resolve("duplicate"));
        expectMembershipFailure(
            loader,
            membership("Team:_Guard"),
            "invalid.json",
            "story.membership.story_id.invalid",
            directory.resolve("invalid"));
        expectMembershipFailure(
            loader,
            membership("legacy"),
            "wrong.json",
            "story.membership.filename.mismatch",
            directory.resolve("bare-name-check"));
    }

    private static void verifyRegistries() {
        NpcIdentityRegistry npcs = new NpcIdentityRegistry();
        NpcHostIdentity first = new NpcHostIdentity(UUID.randomUUID(), "test", 0);
        NpcHostIdentity second = new NpcHostIdentity(UUID.randomUUID(), "test", 0);
        require(npcs.bind("Team:Guard", first), "Full NPC ID was rejected");
        require(npcs.bind("Team:guard", second), "Case-distinct NPC ID did not coexist");
        require(npcs.getHost("Team:Guard") == first, "NPC lookup changed identity");
        require(npcs.getHost("Team:guard") == second, "NPC case-distinct lookup changed identity");
        expectConflict(() -> npcs.bind("Team:Guard", second));

        ItemIdentityRegistry items = new ItemIdentityRegistry();
        ItemStackDefinition definition = new ItemStackDefinition("minecraft:stone", 0, null);
        require(items.bindItem("Team:Token", definition), "Full Item ID was rejected");
        require(items.bindItem("Team:token", definition), "Case-distinct Item ID did not coexist");
        require(items.getItem("Team:Token") == definition, "Item lookup changed identity");
        require(items.getItem("Team:token") == definition, "Item case-distinct lookup changed identity");
        expectConflict(() -> items.bindItem("Team:Token", new ItemStackDefinition("minecraft:dirt", 0, null)));
    }

    private static void expectGraphFailure(CanonicalGraphResourceLoader loader, String json, String fileName,
        String code, Path duplicateDirectory) throws Exception {
        if ("graph.resource.id.duplicate".equals(code)) {
            Files.createDirectories(duplicateDirectory);
            write(duplicateDirectory.resolve("a.json"), json);
            write(duplicateDirectory.resolve("b.json"), json);
            try {
                loader.loadDirectory(duplicateDirectory, null);
                throw new AssertionError("Expected " + code);
            } catch (CanonicalGraphResourceException exception) {
                require(code.equals(exception.getCode()), "Expected " + code + ", got " + exception.getCode());
            }
        } else {
            try {
                loader.load(json.getBytes(StandardCharsets.UTF_8), fileName, null);
                throw new AssertionError("Expected " + code);
            } catch (CanonicalGraphResourceException exception) {
                require(code.equals(exception.getCode()), "Expected " + code + ", got " + exception.getCode());
            }
        }
    }

    private static void expectMembershipFailure(CanonicalStoryMembershipLoader loader, String json, String fileName,
        String code, Path duplicateDirectory) throws Exception {
        if ("story.membership.story_id.duplicate".equals(code)) {
            Files.createDirectories(duplicateDirectory);
            write(duplicateDirectory.resolve("a.json"), json);
            write(duplicateDirectory.resolve("b.json"), json);
            try {
                loader.loadDirectory(duplicateDirectory);
                throw new AssertionError("Expected " + code);
            } catch (CanonicalStoryMembershipException exception) {
                require(code.equals(exception.getCode()), "Expected " + code + ", got " + exception.getCode());
            }
        } else {
            try {
                loader.load(json.getBytes(StandardCharsets.UTF_8), fileName);
                throw new AssertionError("Expected " + code);
            } catch (CanonicalStoryMembershipException exception) {
                require(code.equals(exception.getCode()), "Expected " + code + ", got " + exception.getCode());
            }
        }
    }

    private static boolean containsGraphId(List<CanonicalGraphResource> resources, String id) {
        for (CanonicalGraphResource resource : resources) if (id.equals(resource.getId())) return true;
        return false;
    }

    private static boolean containsMembershipId(List<CanonicalStoryMembership> resources, String id) {
        for (CanonicalStoryMembership resource : resources) if (id.equals(resource.getStoryId())) return true;
        return false;
    }

    private static String graph(String id) {
        return "{\"schema_version\":1,\"resource_kind\":\"story\",\"id\":\"" + id
            + "\",\"display_name\":\"Story\",\"graph\":{\"nodes\":["
            + "{\"id\":\"start\",\"type\":\"start\",\"display_name\":\"Start\",\"ports\":[{\"port_id\":\"out\",\"display_name\":\"Out\",\"direction\":\"output\",\"kind\":\"flow\",\"order\":0}],\"properties\":{}},"
            + "{\"id\":\"end\",\"type\":\"terminate\",\"display_name\":\"End\",\"ports\":[{\"port_id\":\"in\",\"display_name\":\"In\",\"direction\":\"input\",\"kind\":\"flow\",\"order\":0}],\"properties\":{}}],"
            + "\"connections\":[{\"from_node_id\":\"start\",\"from_port_id\":\"out\",\"to_node_id\":\"end\",\"to_port_id\":\"in\",\"interface_kind\":\"flow\"}]}}";
    }

    private static String membership(String id) {
        return "{\"schema_version\":1,\"story_id\":\"" + id
            + "\",\"owned_resources\":{\"actors\":[],\"sessions\":[],\"tasks\":[]},"
            + "\"referenced_resources\":{\"actors\":[],\"sessions\":[],\"tasks\":[]}}";
    }

    private static void write(Path path, String text) throws IOException {
        Files.write(path, text.getBytes(StandardCharsets.UTF_8));
    }

    private static void expectConflict(Runnable action) {
        try {
            action.run();
        } catch (IllegalStateException expected) {
            return;
        }
        throw new AssertionError("Expected identity conflict");
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }

    private static void delete(Path path) throws IOException {
        if (!Files.exists(path)) return;
        try (Stream<Path> paths = Files.walk(path)) {
            Path[] ordered = paths.sorted(Comparator.reverseOrder())
                .toArray(Path[]::new);
            for (Path item : ordered) Files.deleteIfExists(item);
        }
    }
}
