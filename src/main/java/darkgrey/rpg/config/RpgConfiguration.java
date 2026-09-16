package darkgrey.rpg.config;

import java.io.File;

import net.minecraftforge.common.config.Configuration;

public final class RpgConfiguration {

    public static final String DEFAULT_PROJECT_DIRECTORY = "DarkGreyRPG/Project";
    public static final String DEFAULT_STORY_PACKAGE_DIRECTORY = "DarkGreyRPG/StoryPackages";
    private static final String LEGACY_PROJECT_DIRECTORY = "darkgrey_rpg_project";
    private static final String LEGACY_STORY_PACKAGE_DIRECTORY = "darkgrey_rpg_story_packages";

    private final String projectDirectory;
    private final String storyPackageDirectory;
    private final boolean liveBridgeEnabled;
    private final int liveBridgePort;

    private RpgConfiguration(String projectDirectory, String storyPackageDirectory, boolean liveBridgeEnabled,
        int liveBridgePort) {
        this.projectDirectory = projectDirectory;
        this.storyPackageDirectory = storyPackageDirectory;
        this.liveBridgeEnabled = liveBridgeEnabled;
        this.liveBridgePort = liveBridgePort;
    }

    public static RpgConfiguration load(File configurationFile) {
        Configuration configuration = new Configuration(configurationFile);
        configuration.load();

        String directory = configuration
            .get(
                "project",
                "directory",
                DEFAULT_PROJECT_DIRECTORY,
                "Absolute path or path relative to the Minecraft working directory.")
            .getString();
        String packageDirectory = configuration
            .get(
                "story_packages",
                "directory",
                DEFAULT_STORY_PACKAGE_DIRECTORY,
                "Server-owned Story Package install directory. Clients cannot override this path.")
            .getString();
        boolean liveEnabled = configuration
            .get(
                "live_bridge",
                "enabled",
                true,
                "Listen only on 127.0.0.1 for the optional Godot Studio JSON Lines connection.")
            .getBoolean(true);
        int livePort = configuration
            .get("live_bridge", "port", 32145, "Localhost TCP port used by DarkGrey RPG Studio.", 1024, 65535)
            .getInt(32145);

        if (configuration.hasChanged()) {
            configuration.save();
        }
        return new RpgConfiguration(directory.trim(), packageDirectory.trim(), liveEnabled, livePort);
    }

    public File resolveProjectDirectory(File modConfigurationDirectory) {
        File gameDirectory = gameDirectory(modConfigurationDirectory);
        File configured = resolveConfigured(gameDirectory, projectDirectory);
        if (isDefaultProjectDirectory(projectDirectory)
            || samePath(configured, new File(gameDirectory, "darkgrey_rpg_project"))
            || samePath(configured, new File(gameDirectory, DEFAULT_PROJECT_DIRECTORY))) {
            return RpgRuntimeDirectories.prepare(gameDirectory)
                .projectDirectory();
        }
        if (configured.isAbsolute()) {
            return configured;
        }

        return new File(gameDirectory, projectDirectory);
    }

    public boolean isLiveBridgeEnabled() {
        return liveBridgeEnabled;
    }

    public File resolveStoryPackageDirectory(File modConfigurationDirectory) {
        File gameDirectory = gameDirectory(modConfigurationDirectory);
        File configured = resolveConfigured(gameDirectory, storyPackageDirectory);
        if (isDefaultStoryPackageDirectory(storyPackageDirectory)
            || samePath(configured, new File(gameDirectory, "darkgrey_rpg_story_packages"))
            || samePath(configured, new File(gameDirectory, DEFAULT_STORY_PACKAGE_DIRECTORY))) {
            return RpgRuntimeDirectories.prepare(gameDirectory)
                .storyPackagesDirectory();
        }
        if (configured.isAbsolute()) return configured;
        return new File(gameDirectory, storyPackageDirectory);
    }

    public int getLiveBridgePort() {
        return liveBridgePort;
    }

    private static File gameDirectory(File modConfigurationDirectory) {
        if (modConfigurationDirectory == null || modConfigurationDirectory.getParentFile() == null) {
            throw new IllegalArgumentException("modConfigurationDirectory");
        }
        return modConfigurationDirectory.getParentFile();
    }

    private static boolean isDefaultProjectDirectory(String value) {
        return isPath(value, DEFAULT_PROJECT_DIRECTORY) || isPath(value, LEGACY_PROJECT_DIRECTORY);
    }

    private static boolean isDefaultStoryPackageDirectory(String value) {
        return isPath(value, DEFAULT_STORY_PACKAGE_DIRECTORY) || isPath(value, LEGACY_STORY_PACKAGE_DIRECTORY);
    }

    private static boolean isPath(String value, String expected) {
        return expected.equals(value == null ? null : value.replace('\\', '/'));
    }

    private static File resolveConfigured(File gameDirectory, String value) {
        File configured = new File(value);
        return configured.isAbsolute() ? configured : new File(gameDirectory, value);
    }

    private static boolean samePath(File first, File second) {
        return first.toPath()
            .toAbsolutePath()
            .normalize()
            .equals(
                second.toPath()
                    .toAbsolutePath()
                    .normalize());
    }
}
