package darkgrey.rpg.config;

import java.io.IOException;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.nio.file.Paths;
import java.util.Comparator;

/** Focused pure-Java probe for the game-owned runtime layout and legacy migration. */
public final class RpgRuntimeDirectoriesProbe {

    private RpgRuntimeDirectoriesProbe() {}

    public static void main(String[] args) throws Exception {
        if (args.length > 1 && "--migrate".equals(args[0])) {
            for (int i = 1; i < args.length; i++) {
                RpgRuntimeDirectories layout = RpgRuntimeDirectories.prepare(Paths.get(args[i]));
                if (!layout.migrationConflicts()
                    .isEmpty())
                    throw new IllegalStateException(
                        layout.migrationConflicts()
                            .toString());
                System.out.println("MIGRATED=" + layout.root());
            }
            return;
        }
        Path parent = args.length == 0 ? Files.createTempDirectory("dgr-runtime-layout-")
            : Paths.get(args[0])
                .toAbsolutePath()
                .normalize();
        Files.createDirectories(parent);
        parent = Files.createTempDirectory(parent, "RuntimeLayout-");
        try {
            freshLayout(parent);
            legacyMigration(parent);
            collisionIsPreserved(parent);
            repeatIsIdempotent(parent);
            configuredPaths(parent);
            System.out.println("RPG_RUNTIME_DIRECTORY_LAYOUT_PROBE=PASS");
        } finally {
            deleteTree(parent);
        }
    }

    private static void configuredPaths(Path parent) throws Exception {
        Path game = Files.createDirectories(parent.resolve("Configured"));
        Path configDir = Files.createDirectories(game.resolve("config"));
        java.lang.reflect.Constructor<RpgConfiguration> constructor = RpgConfiguration.class
            .getDeclaredConstructor(String.class, String.class, boolean.class, int.class);
        constructor.setAccessible(true);
        for (String value : new String[] { "./darkgrey_rpg_project", game.resolve("darkgrey_rpg_project")
            .toString(), "CustomProject" }) {
            RpgConfiguration config = constructor.newInstance(value, "./darkgrey_rpg_story_packages", true, 32145);
            require(
                config.resolveProjectDirectory(configDir.toFile())
                    .toPath()
                    .equals(game.resolve("CustomProject".equals(value) ? value : "DarkGreyRPG/Project")),
                "configured path preserved or redirected");
            require(
                config.resolveStoryPackageDirectory(configDir.toFile())
                    .toPath()
                    .equals(game.resolve("DarkGreyRPG/StoryPackages")),
                "package alias redirected");
        }
    }

    private static void freshLayout(Path game) {
        RpgRuntimeDirectories layout = RpgRuntimeDirectories.prepare(game);
        require(
            layout.root()
                .toPath()
                .equals(game.resolve("DarkGreyRPG")),
            "root uses the requested game directory");
        require(
            layout.cacheDirectory()
                .isDirectory(),
            "Cache is created");
        require(
            layout.projectDirectory()
                .isDirectory(),
            "Project is created");
        require(
            layout.storyPackagesDirectory()
                .isDirectory(),
            "StoryPackages is created");
        require(
            layout.configurationFile()
                .toPath()
                .equals(game.resolve("DarkGreyRPG/Config/darkgrey_rpg.cfg")),
            "config path");
        require(
            layout.settingsFile()
                .toPath()
                .equals(game.resolve("DarkGreyRPG/Config/darkgrey-rpg-windows.properties")),
            "settings path");
    }

    private static void legacyMigration(Path game) throws IOException {
        Path legacyProject = game.resolve("darkgrey_rpg_project");
        Path legacyPackages = game.resolve("darkgrey_rpg_story_packages");
        Path legacyCache = game.resolve("darkgrey_rpg_media_cache/media");
        Files.createDirectories(legacyProject.resolve("resources"));
        Files.createDirectories(legacyPackages.resolve("one"));
        Files.createDirectories(legacyCache);
        Files.write(legacyProject.resolve("resources/project.json"), bytes("project"));
        Files.write(legacyPackages.resolve("one/manifest.json"), bytes("package"));
        Files.write(legacyCache.resolve("media-file.png"), bytes("media"));
        Files.createDirectories(game.resolve("config"));
        Files.write(game.resolve("config/darkgrey_rpg.cfg"), bytes("cfg"));
        Files.write(game.resolve("config/darkgrey-rpg-windows.properties"), bytes("settings"));

        RpgRuntimeDirectories layout = RpgRuntimeDirectories.prepare(game);
        require(
            Files.readAllBytes(
                layout.projectDirectory()
                    .toPath()
                    .resolve("resources/project.json")).length
                > 0,
            "project migrated");
        require(
            Files.exists(
                layout.storyPackagesDirectory()
                    .toPath()
                    .resolve("one/manifest.json")),
            "story packages migrated");
        require(
            Files.exists(
                layout.cacheDirectory()
                    .toPath()
                    .resolve("media/media-file.png")),
            "media cache migrated");
        require(
            Files.exists(
                layout.configurationFile()
                    .toPath()),
            "DGR config migrated");
        require(
            Files.exists(
                layout.settingsFile()
                    .toPath()),
            "window settings migrated");
        require(!Files.exists(legacyProject), "empty legacy project root removed");
        require(!Files.exists(legacyPackages), "empty legacy package root removed");
    }

    private static void collisionIsPreserved(Path game) throws IOException {
        Path legacy = game.resolve("darkgrey_rpg_project");
        Path current = game.resolve("DarkGreyRPG/Project");
        Files.createDirectories(legacy);
        Files.createDirectories(current);
        Files.write(legacy.resolve("same.txt"), bytes("legacy"));
        Files.write(legacy.resolve("new.txt"), bytes("new"));
        Files.write(current.resolve("same.txt"), bytes("current"));
        RpgRuntimeDirectories layout = RpgRuntimeDirectories.prepare(game);
        require("current".equals(text(current.resolve("same.txt"))), "collision does not overwrite destination");
        require("legacy".equals(text(legacy.resolve("same.txt"))), "collision keeps legacy source");
        require("new".equals(text(current.resolve("new.txt"))), "non-conflicting file migrates");
        require(
            !layout.migrationConflicts()
                .isEmpty(),
            "collision is surfaced for operator review");
    }

    private static void repeatIsIdempotent(Path game) throws IOException {
        RpgRuntimeDirectories first = RpgRuntimeDirectories.prepare(game);
        byte[] before = Files.readAllBytes(
            first.projectDirectory()
                .toPath()
                .resolve("same.txt"));
        RpgRuntimeDirectories second = RpgRuntimeDirectories.prepare(game);
        require(
            before.length == Files.readAllBytes(
                second.projectDirectory()
                    .toPath()
                    .resolve("same.txt")).length,
            "repeat preserves bytes");
        require(
            "current".equals(
                text(
                    second.projectDirectory()
                        .toPath()
                        .resolve("same.txt"))),
            "repeat preserves winner");
    }

    private static byte[] bytes(String value) {
        return value.getBytes(StandardCharsets.UTF_8);
    }

    private static String text(Path path) throws IOException {
        return new String(Files.readAllBytes(path), StandardCharsets.UTF_8);
    }

    private static void deleteTree(Path root) throws IOException {
        if (!Files.exists(root)) return;
        Files.walk(root)
            .sorted(Comparator.reverseOrder())
            .forEach(path -> {
                try {
                    Files.deleteIfExists(path);
                } catch (IOException failure) {
                    throw new RuntimeException(failure);
                }
            });
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
