package darkgrey.rpg.client.gui;

import java.io.File;
import java.io.FileInputStream;
import java.io.FileOutputStream;
import java.nio.file.Files;
import java.util.Properties;

/** Direct executable probe for the 0.3.2.4 utility-window primitives. */
public final class UtilityWindow0324Probe {

    private UtilityWindow0324Probe() {}

    public static void main(String[] args) throws Exception {
        geometryMathProbe();
        resizeAndMinimumProbe();
        fourCornerProbe();
        persistenceProbe();
        System.out.println("UTILITY_WINDOW_0324_PROBE=PASS");
    }

    private static void geometryMathProbe() {
        UtilityWindowGeometry window = new UtilityWindowGeometry(120, 80, 300, 180);
        window.restore(1000, 700, new UtilityWindowGeometry.Snapshot(0.25D, 0.75D, 300, 180));
        assertEquals(100, window.x, "normalized center x");
        assertEquals(435, window.y, "normalized center y");

        assertTrue(window.begin(window.x + 30, window.y + 8, 0), "title drag begins");
        window.move(window.x + 130, window.y + 58);
        // The second move is intentionally expressed as an absolute pointer.
        assertTrue(window.active(), "drag remains active");
        window.end();
        assertFalse(window.active(), "drag ends");
        assertFalse(window.begin(window.x + 10, window.y + 30, 0), "client body is not draggable");
        assertFalse(window.begin(window.x + 10, window.y + 10, 1), "right button is ignored");
    }

    private static void resizeAndMinimumProbe() {
        UtilityWindowGeometry window = new UtilityWindowGeometry(120, 80, 300, 180);
        window.restore(160, 100, new UtilityWindowGeometry.Snapshot(0.5D, 0.5D, 1000, 1000));
        assertEquals(160, window.width, "screen width caps saved width");
        assertEquals(100, window.height, "screen height caps saved height");
        assertEquals(0, window.x, "oversize x remains reachable");
        assertEquals(0, window.y, "oversize y remains reachable");

        window.restore(1000, 700, new UtilityWindowGeometry.Snapshot(0.5D, 0.5D, 300, 180));
        int right = window.x + window.width - 2;
        int bottom = window.y + window.height - 2;
        assertTrue(window.begin(right, bottom, 0), "resize grip begins");
        window.move(window.x - 1000, window.y - 1000);
        assertEquals(120, window.width, "resize honors minimum width");
        assertEquals(80, window.height, "resize honors minimum height");
        window.end();

        window.restore(60, 40, new UtilityWindowGeometry.Snapshot(Double.NaN, Double.POSITIVE_INFINITY, -5, 0));
        assertEquals(60, window.width, "screen smaller than minimum width is usable");
        assertEquals(40, window.height, "screen smaller than minimum height is usable");
        assertEquals(0, window.x, "small screen x is clamped");
        assertEquals(0, window.y, "small screen y is clamped");
    }

    private static void fourCornerProbe() {
        for (int sx : new int[] { -1, 1 }) for (int sy : new int[] { -1, 1 }) {
            UtilityWindowGeometry w = new UtilityWindowGeometry(120, 80, 300, 180);
            w.restore(1000, 700, null);
            int x = w.x, y = w.y, right = x + w.width, bottom = y + w.height;
            int px = sx < 0 ? x + 2 : right - 2, py = sy < 0 ? y + 2 : bottom - 2;
            assertTrue(w.begin(px, py, 0), "each corner starts resize");
            w.move(px + sx * 40, py + sy * 30);
            assertEquals(340, w.width, "corner width grows");
            assertEquals(210, w.height, "corner height grows");
            assertEquals(sx < 0 ? right : x, sx < 0 ? w.x + w.width : w.x, "opposite horizontal edge anchored");
            assertEquals(sy < 0 ? bottom : y, sy < 0 ? w.y + w.height : w.y, "opposite vertical edge anchored");
            w.move(px - sx * 2000, py - sy * 2000);
            assertEquals(120, w.width, "each corner minimum width");
            assertEquals(80, w.height, "each corner minimum height");
            w.move(px + sx * 2000, py + sy * 2000);
            assertEquals(sx < 0 ? 0 : 1000, sx < 0 ? w.x : w.x + w.width, "screen edge clamp");
            assertEquals(sy < 0 ? 0 : 700, sy < 0 ? w.y : w.y + w.height, "screen vertical clamp");
            w.end();
        }
    }

    private static void persistenceProbe() throws Exception {
        File file = Files.createTempFile("darkgrey-window-0324", ".properties")
            .toFile();
        try {
            Properties initial = new Properties();
            initial.setProperty("unrelated.value", "preserve-me");
            initial.setProperty("window.entity.centerXRatio", "not-a-number");
            initial.setProperty("window.entity.centerYRatio", "NaN");
            initial.setProperty("window.entity.width", "-4");
            initial.setProperty("window.entity.height", "oops");
            initial.setProperty("window.item.centerXRatio", "0.2");
            FileOutputStream initialOutput = new FileOutputStream(file);
            try {
                initial.store(initialOutput, "probe");
            } finally {
                initialOutput.close();
            }

            UtilityWindowSettings first = new UtilityWindowSettings(file);
            UtilityWindowSettings staleSecond = new UtilityWindowSettings(file);
            UtilityWindowGeometry entity = new UtilityWindowGeometry(120, 80, 300, 180);
            first.load("entity", entity, 1000, 700);
            assertEquals(300, entity.width, "malformed width falls back to default");
            assertEquals(180, entity.height, "malformed height falls back to default");
            assertEquals(350, entity.x, "malformed x ratio falls back to center");
            assertEquals(260, entity.y, "malformed y ratio falls back to center");

            UtilityWindowGeometry item = new UtilityWindowGeometry(100, 70, 220, 140);
            first.load("item", item, 1000, 700);
            assertEquals(0.2D, item.snapshot().centerXRatio, 0.0001D, "item entry is independent");
            first.save("entity", entity, 1000, 700);
            // A second settings object loaded before the first save must still
            // preserve the first object's newer entry when it saves its key.
            staleSecond.save("item", item, 1000, 700);

            Properties written = read(file);
            assertEquals("preserve-me", written.getProperty("unrelated.value"), "unrelated setting preserved");
            assertEquals("0.2", written.getProperty("window.item.centerXRatio"), "other window preserved");
            assertEquals(
                "0.5",
                written.getProperty("window.entity.centerXRatio"),
                "stale settings do not erase newer entry");

            UtilityWindowSettings second = new UtilityWindowSettings(file);
            UtilityWindowGeometry restored = new UtilityWindowGeometry(120, 80, 300, 180);
            second.load("entity", restored, 1600, 900);
            assertEquals(300, restored.width, "saved logical width survives restart");
            assertEquals(180, restored.height, "saved logical height survives restart");
            assertEquals(0.5D, restored.snapshot().centerXRatio, 0.0001D, "center ratio survives resolution change");
            assertEquals(0.5D, restored.snapshot().centerYRatio, 0.0001D, "center ratio survives resolution change");
        } finally {
            Files.deleteIfExists(file.toPath());
        }
    }

    private static Properties read(File file) throws Exception {
        Properties properties = new Properties();
        FileInputStream input = new FileInputStream(file);
        try {
            properties.load(input);
        } finally {
            input.close();
        }
        return properties;
    }

    private static void assertTrue(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }

    private static void assertFalse(boolean value, String message) {
        if (value) throw new AssertionError(message);
    }

    private static void assertEquals(int expected, int actual, String message) {
        if (expected != actual) throw new AssertionError(message + ": expected " + expected + ", got " + actual);
    }

    private static void assertEquals(double expected, double actual, double tolerance, String message) {
        if (Math.abs(expected - actual) > tolerance) {
            throw new AssertionError(message + ": expected " + expected + ", got " + actual);
        }
    }

    private static void assertEquals(String expected, String actual, String message) {
        if (!expected.equals(actual)) throw new AssertionError(message + ": expected " + expected + ", got " + actual);
    }
}
