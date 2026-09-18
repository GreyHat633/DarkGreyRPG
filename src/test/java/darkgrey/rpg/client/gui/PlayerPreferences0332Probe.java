package darkgrey.rpg.client.gui;

import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.nio.file.Files;
import java.util.Properties;

import darkgrey.rpg.client.session.DialoguePreferences;
import darkgrey.rpg.client.session.PlayerUiPreferences;

/** Persistence, recovery and preference-only reset contract. */
public final class PlayerPreferences0332Probe {

    public static void main(String[] args) throws Exception {
        File file = File.createTempFile("dgr-0332-preferences", ".properties");
        try {
            Properties source = new Properties();
            source.setProperty("unknown.future", "keep");
            source.setProperty("window.task.width", "444");
            source.setProperty("ui.theme", "NAVY");
            source.setProperty("dialogue.opacity", "NaN");
            source.setProperty("audio.voice", "-1");
            source.setProperty("audio.sessionMusic", "0.5");
            try (FileOutputStream out = new FileOutputStream(file)) {
                source.store(out, "probe");
            }
            UtilityWindowSettings settings = new UtilityWindowSettings(file);
            settings.loadPlayerPreferences();
            check(PlayerUiPreferences.theme() == PlayerUiPreferences.Theme.CHARCOAL, "invalid theme");
            check(PlayerUiPreferences.opacity() == 0.8 && PlayerUiPreferences.voiceVolume() == 1, "field recovery");
            check(PlayerUiPreferences.musicVolume() == 0.5, "valid neighbor retained");
            PlayerUiPreferences.setTheme(PlayerUiPreferences.Theme.WHITE);
            PlayerUiPreferences.setTextScale(1.5);
            PlayerUiPreferences.setOpacity(0);
            DialoguePreferences.setSpeed(0);
            settings.savePlayerPreferences();
            PlayerUiPreferences.reset();
            new UtilityWindowSettings(file).loadPlayerPreferences();
            check(
                PlayerUiPreferences.theme() == PlayerUiPreferences.Theme.WHITE
                    && PlayerUiPreferences.textScale() == 1.5,
                "round trip");
            check(PlayerUiPreferences.opacity() == 0 && DialoguePreferences.speed() == 0, "zero persists");
            DgrUiPalette.apply();
            check((DgrUiPalette.dialoguePanel() >>> 24) == 0, "zero alpha");
            check(DgrUiPalette.TEXT == 0xFF202A30, "white theme readable body");
            PlayerUiPreferences.setOpacity(1);
            check((DgrUiPalette.dialoguePanel() >>> 24) == 255, "opaque alpha");
            PlayerUiPreferences.reset();
            settings.savePlayerPreferences();
            Properties persisted = new Properties();
            try (FileInputStream in = new FileInputStream(file)) {
                persisted.load(in);
            }
            check("keep".equals(persisted.getProperty("unknown.future")), "unknown retained");
            check("444".equals(persisted.getProperty("window.task.width")), "reset preserves window");
            check(PlayerUiPreferences.Theme.values().length == 3, "exactly three themes");
            System.out.println("PLAYER_PREFERENCES_0332=PASS");
        } finally {
            Files.deleteIfExists(file.toPath());
            PlayerUiPreferences.reset();
            DgrUiPalette.apply();
        }
    }

    private static void check(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
