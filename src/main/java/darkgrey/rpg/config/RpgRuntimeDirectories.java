package darkgrey.rpg.config;

import java.io.File;
import java.io.IOException;
import java.nio.file.DirectoryStream;
import java.nio.file.Files;
import java.nio.file.LinkOption;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

/**
 * The game-owned DarkGrey RPG runtime layout.
 *
 * <p>
 * All paths returned by this class are below {@code game/DarkGreyRPG}, except
 * paths explicitly supplied by a user in the Forge configuration. Legacy paths
 * are migrated without replacing an existing destination.
 * </p>
 */
public final class RpgRuntimeDirectories {

    public static final String ROOT_NAME = "DarkGreyRPG";
    public static final String CACHE_NAME = "Cache";
    public static final String PROJECT_NAME = "Project";
    public static final String STORY_PACKAGES_NAME = "StoryPackages";
    public static final String CONFIG_NAME = "Config";
    public static final String EXPORTS_NAME = "Exports";
    public static final String SETTINGS_FILE_NAME = "darkgrey-rpg-windows.properties";
    public static final String CONFIG_FILE_NAME = "darkgrey_rpg.cfg";

    private static final String LEGACY_CACHE_NAME = "darkgrey_rpg_media_cache";
    private static final String LEGACY_PROJECT_NAME = "darkgrey_rpg_project";
    private static final String LEGACY_STORY_PACKAGES_NAME = "darkgrey_rpg_story_packages";

    private final Path gameDirectory;
    private final Path root;
    private final List<String> migrationConflicts = new ArrayList<String>();

    private RpgRuntimeDirectories(Path gameDirectory) {
        this.gameDirectory = normalizeDirectory(gameDirectory);
        this.root = this.gameDirectory.resolve(ROOT_NAME)
            .normalize();
    }

    /** Prepares the layout and performs the idempotent legacy migration. */
    public static RpgRuntimeDirectories prepare(File gameDirectory) {
        RpgRuntimeDirectories layout = new RpgRuntimeDirectories(gameDirectory.toPath());
        layout.migrate();
        return layout;
    }

    /** Same as {@link #prepare(File)} for callers that already have a Path. */
    public static RpgRuntimeDirectories prepare(Path gameDirectory) {
        RpgRuntimeDirectories layout = new RpgRuntimeDirectories(gameDirectory);
        layout.migrate();
        return layout;
    }

    /** Returns the game directory used to derive this layout. */
    public File gameDirectory() {
        return gameDirectory.toFile();
    }

    public File root() {
        return root.toFile();
    }

    public File cacheDirectory() {
        return root.resolve(CACHE_NAME)
            .toFile();
    }

    public File projectDirectory() {
        return root.resolve(PROJECT_NAME)
            .toFile();
    }

    public File storyPackagesDirectory() {
        return root.resolve(STORY_PACKAGES_NAME)
            .toFile();
    }

    public File configDirectory() {
        return root.resolve(CONFIG_NAME)
            .toFile();
    }

    public File configurationFile() {
        return root.resolve(CONFIG_NAME)
            .resolve(CONFIG_FILE_NAME)
            .toFile();
    }

    public File settingsFile() {
        return root.resolve(CONFIG_NAME)
            .resolve(SETTINGS_FILE_NAME)
            .toFile();
    }

    public File exportsDirectory() {
        return root.resolve(EXPORTS_NAME)
            .toFile();
    }

    /** Returns source/destination pairs preserved because migration found a collision. */
    public List<String> migrationConflicts() {
        return Collections.unmodifiableList(new ArrayList<String>(migrationConflicts));
    }

    /** Resolves the Forge suggested config file into the new config location. */
    public File configurationFile(File suggestedConfigurationFile) {
        if (suggestedConfigurationFile != null) {
            migrateFile(suggestedConfigurationFile.toPath(), configurationFile().toPath());
        }
        ensureDirectory(configDirectory().toPath());
        return configurationFile();
    }

    private void migrate() {
        ensureDirectory(root);
        migrateDirectory(gameDirectory.resolve(LEGACY_CACHE_NAME), root.resolve(CACHE_NAME));
        migrateDirectory(gameDirectory.resolve(LEGACY_PROJECT_NAME), root.resolve(PROJECT_NAME));
        migrateDirectory(gameDirectory.resolve(LEGACY_STORY_PACKAGES_NAME), root.resolve(STORY_PACKAGES_NAME));
        migrateFile(
            gameDirectory.resolve("config")
                .resolve(CONFIG_FILE_NAME),
            root.resolve(CONFIG_NAME)
                .resolve(CONFIG_FILE_NAME));
        migrateFile(
            gameDirectory.resolve("config")
                .resolve(SETTINGS_FILE_NAME),
            root.resolve(CONFIG_NAME)
                .resolve(SETTINGS_FILE_NAME));
        ensureDirectory(root.resolve(CACHE_NAME));
        ensureDirectory(root.resolve(PROJECT_NAME));
        ensureDirectory(root.resolve(STORY_PACKAGES_NAME));
        ensureDirectory(root.resolve(CONFIG_NAME));
    }

    private static Path normalizeDirectory(Path value) {
        if (value == null) throw new IllegalArgumentException("gameDirectory");
        return value.toAbsolutePath()
            .normalize();
    }

    private static void ensureDirectory(Path directory) {
        try {
            if (Files.isSymbolicLink(directory)) return;
            Files.createDirectories(directory);
        } catch (IOException failure) {
            throw new IllegalStateException("Cannot create DarkGrey RPG runtime directory: " + directory, failure);
        }
    }

    /** Merges only non-conflicting entries; existing files and symlinks are never replaced. */
    private void migrateDirectory(Path source, Path destination) {
        if (!Files.isDirectory(source, LinkOption.NOFOLLOW_LINKS) || Files.isSymbolicLink(source)) return;
        if (Files.exists(destination, LinkOption.NOFOLLOW_LINKS) && Files.isSymbolicLink(destination)) return;
        if (Files.exists(destination, LinkOption.NOFOLLOW_LINKS)
            && !Files.isDirectory(destination, LinkOption.NOFOLLOW_LINKS)) {
            migrationConflicts.add(source + " -> " + destination);
            return;
        }
        ensureDirectory(destination);
        try (DirectoryStream<Path> children = Files.newDirectoryStream(source)) {
            for (Path child : children) {
                Path target = destination.resolve(
                    child.getFileName()
                        .toString());
                if (Files.isSymbolicLink(child)) continue;
                if (Files.isDirectory(child, LinkOption.NOFOLLOW_LINKS)) {
                    migrateDirectory(child, target);
                    deleteIfEmpty(child);
                } else {
                    migrateFile(child, target);
                }
            }
        } catch (IOException failure) {
            throw new IllegalStateException("Cannot migrate DarkGrey RPG directory: " + source, failure);
        }
        deleteIfEmpty(source);
    }

    private void migrateFile(Path source, Path destination) {
        if (!Files.isRegularFile(source, LinkOption.NOFOLLOW_LINKS) || Files.isSymbolicLink(source)) return;
        if (Files.exists(destination, LinkOption.NOFOLLOW_LINKS)) {
            migrationConflicts.add(source + " -> " + destination);
            return;
        }
        ensureDirectory(destination.getParent());
        try {
            // No REPLACE_EXISTING: a concurrent/new destination must win safely.
            Files.move(source, destination);
        } catch (IOException failure) {
            throw new IllegalStateException("Cannot migrate DarkGrey RPG file: " + source, failure);
        }
    }

    private static void deleteIfEmpty(Path directory) {
        if (Files.isSymbolicLink(directory)) return;
        try (DirectoryStream<Path> children = Files.newDirectoryStream(directory)) {
            if (!children.iterator()
                .hasNext()) Files.deleteIfExists(directory);
        } catch (IOException failure) {
            // A collision, concurrent writer, or read-only legacy tree remains intact.
        }
    }
}
