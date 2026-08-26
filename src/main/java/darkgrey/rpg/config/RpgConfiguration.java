package darkgrey.rpg.config;

import java.io.File;

import net.minecraftforge.common.config.Configuration;

public final class RpgConfiguration {

    private final String projectDirectory;
    private final boolean liveBridgeEnabled;
    private final int liveBridgePort;

    private RpgConfiguration(String projectDirectory, boolean liveBridgeEnabled, int liveBridgePort) {
        this.projectDirectory = projectDirectory;
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
                "darkgrey_rpg_project",
                "Absolute path or path relative to the Minecraft working directory.")
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
        return new RpgConfiguration(directory.trim(), liveEnabled, livePort);
    }

    public File resolveProjectDirectory(File modConfigurationDirectory) {
        File configured = new File(projectDirectory);
        if (configured.isAbsolute()) {
            return configured;
        }

        File gameDirectory = modConfigurationDirectory.getParentFile();
        return new File(gameDirectory, projectDirectory);
    }

    public boolean isLiveBridgeEnabled() {
        return liveBridgeEnabled;
    }

    public int getLiveBridgePort() {
        return liveBridgePort;
    }
}
