package darkgrey.rpg.client.gui;

import java.io.BufferedInputStream;
import java.io.BufferedOutputStream;
import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.io.IOException;
import java.nio.file.AtomicMoveNotSupportedException;
import java.nio.file.Files;
import java.nio.file.StandardCopyOption;
import java.util.Properties;

/**
 * Explicit, client-local persistence for utility-window layouts.
 *
 * <p>
 * The caller supplies the Minecraft config file location. This class has
 * no Minecraft dependency and never writes while geometry is being moved.
 * </p>
 */
public final class UtilityWindowSettings {

    private static final String PREFIX = "window.";
    private static final String CENTER_X = "centerXRatio";
    private static final String CENTER_Y = "centerYRatio";
    private static final String WIDTH = "width";
    private static final String HEIGHT = "height";

    private final File file;
    private final Properties properties = new Properties();

    /** Loads the explicitly supplied file if it exists; missing/corrupt data is ignored. */
    public UtilityWindowSettings(File file) {
        if (file == null) throw new IllegalArgumentException("file");
        this.file = file;
        loadFromDisk();
    }

    /** Returns the explicit file supplied by the caller. */
    public File file() {
        return file;
    }

    /**
     * Loads one independent window entry. Missing or malformed properties
     * fall back field-by-field to the geometry defaults.
     */
    public UtilityWindowGeometry.Snapshot load(String key, UtilityWindowGeometry geometry) {
        if (geometry == null) throw new IllegalArgumentException("geometry");
        String prefix = prefix(key);
        UtilityWindowGeometry.Snapshot fallback = geometry.defaultSnapshot();
        return new UtilityWindowGeometry.Snapshot(
            finiteDouble(properties.getProperty(prefix + CENTER_X), fallback.centerXRatio),
            finiteDouble(properties.getProperty(prefix + CENTER_Y), fallback.centerYRatio),
            positiveInt(properties.getProperty(prefix + WIDTH), fallback.width),
            positiveInt(properties.getProperty(prefix + HEIGHT), fallback.height));
    }

    /** Loads and immediately restores one entry for the current logical screen. */
    public UtilityWindowGeometry.Snapshot load(String key, UtilityWindowGeometry geometry, int screenWidth,
        int screenHeight) {
        UtilityWindowGeometry.Snapshot snapshot = load(key, geometry);
        geometry.restore(screenWidth, screenHeight, snapshot);
        return snapshot;
    }

    /**
     * Updates one entry and explicitly writes the complete properties file.
     * Other window entries and unknown properties are preserved.
     */
    public void save(String key, UtilityWindowGeometry geometry, int screenWidth, int screenHeight) {
        if (geometry == null) throw new IllegalArgumentException("geometry");
        reloadFromDisk();
        String prefix = prefix(key);
        UtilityWindowGeometry.Snapshot snapshot = geometry.snapshot(screenWidth, screenHeight);
        properties.setProperty(prefix + CENTER_X, Double.toString(safeRatio(snapshot.centerXRatio)));
        properties.setProperty(prefix + CENTER_Y, Double.toString(safeRatio(snapshot.centerYRatio)));
        properties.setProperty(prefix + WIDTH, Integer.toString(Math.max(1, snapshot.width)));
        properties.setProperty(prefix + HEIGHT, Integer.toString(Math.max(1, snapshot.height)));
        saveAll();
    }

    /** Writes all currently loaded/updated properties. */
    public void saveAll() {
        File parent = file.getParentFile();
        if (parent != null && !parent.exists() && !parent.mkdirs() && !parent.isDirectory()) {
            throw new IllegalStateException("Cannot create settings directory: " + parent);
        }
        File directory = parent == null ? new File(".") : parent;
        File temporary = null;
        try {
            temporary = File.createTempFile(file.getName(), ".tmp", directory);
            try (BufferedOutputStream output = new BufferedOutputStream(new FileOutputStream(temporary))) {
                properties.store(output, "DarkGrey RPG utility window layout");
            }
            try {
                Files.move(
                    temporary.toPath(),
                    file.toPath(),
                    StandardCopyOption.REPLACE_EXISTING,
                    StandardCopyOption.ATOMIC_MOVE);
            } catch (AtomicMoveNotSupportedException unsupported) {
                Files.move(temporary.toPath(), file.toPath(), StandardCopyOption.REPLACE_EXISTING);
            }
        } catch (IOException error) {
            throw new IllegalStateException("Cannot save utility window settings: " + file, error);
        } finally {
            // Covers a failed write/move without touching the existing target.
            if (temporary != null && temporary.exists()) {
                try {
                    Files.deleteIfExists(temporary.toPath());
                } catch (IOException ignored) {
                    // A stale temporary file is harmless and does not replace the target.
                }
            }
        }
    }

    private void loadFromDisk() {
        properties.clear();
        if (!file.isFile()) return;
        try (BufferedInputStream input = new BufferedInputStream(new FileInputStream(file))) {
            properties.load(input);
        } catch (IOException | IllegalArgumentException ignored) {
            // A broken local preferences file must not prevent the GUI opening.
        }
    }

    private void reloadFromDisk() {
        loadFromDisk();
    }

    private static String prefix(String key) {
        if (key == null) throw new IllegalArgumentException("key");
        String trimmed = key.trim();
        if (!trimmed.matches("[A-Za-z0-9_.-]+")) {
            throw new IllegalArgumentException("Invalid utility window key: " + key);
        }
        return PREFIX + trimmed + ".";
    }

    private static double finiteDouble(String text, double fallback) {
        if (text == null) return fallback;
        try {
            double value = Double.parseDouble(text.trim());
            return !Double.isNaN(value) && !Double.isInfinite(value) ? value : fallback;
        } catch (RuntimeException ignored) {
            return fallback;
        }
    }

    private static int positiveInt(String text, int fallback) {
        if (text == null) return fallback;
        try {
            int value = Integer.parseInt(text.trim());
            return value > 0 ? value : fallback;
        } catch (RuntimeException ignored) {
            return fallback;
        }
    }

    private static double safeRatio(double value) {
        if (Double.isNaN(value) || Double.isInfinite(value)) return 0.5D;
        return value < 0.0D ? 0.0D : (value > 1.0D ? 1.0D : value);
    }
}
