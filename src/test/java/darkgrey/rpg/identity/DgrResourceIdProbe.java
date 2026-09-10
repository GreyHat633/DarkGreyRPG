package darkgrey.rpg.identity;

/** Direct executable probe for the Java/C# identity contract vectors. */
public final class DgrResourceIdProbe {

    private DgrResourceIdProbe() {}

    public static void main(String[] args) {
        assertTrue(DgrResourceId.isValidNamespace("Author"));
        assertTrue(DgrResourceId.isValidNamespace("a_0-9"));
        assertFalse(DgrResourceId.isValidNamespace(" author"));
        assertFalse(DgrResourceId.isValidNamespace("作者"));
        assertFalse(DgrResourceId.isValidNamespace(""));
        assertFalse(DgrResourceId.isValidNamespace(repeat('a', 33)));

        assertTrue(DgrResourceId.isFullId("Author:boss"));
        assertTrue(DgrResourceId.isFullId("Team:Guard"));
        assertTrue(DgrResourceId.isFullId("Team:guard"));
        assertTrue(DgrResourceId.isFullId("Team:G.u-a_r.d9"));
        assertFalse(DgrResourceId.isFullId("boss"));
        assertFalse(DgrResourceId.isFullId("Author: bad"));
        assertFalse(DgrResourceId.isFullId("Author:_bad"));
        assertFalse(DgrResourceId.isFullId("Author:" + repeat('b', 64)));
        assertFalse(DgrResourceId.isFullId("Author:boss:extra"));
        assertFalse(DgrResourceId.isFullId("Author/boss"));
        assertFalse(DgrResourceId.isFullId("Author: boss"));
        assertTrue(DgrResourceId.isCompatibleId("legacy.actor"));
        assertTrue(DgrResourceId.isCompatibleId(repeat('a', 200)));
        assertFalse(DgrResourceId.isCompatibleId("LegacyActor"));
        assertFalse(DgrResourceId.isCompatibleId("legacy actor"));

        assertEquals("Author:boss", DgrResourceId.qualify("Author", "boss"));
        assertEquals("Team:Guard", DgrResourceId.qualify("Team", "Guard"));
        assertEquals("Guard", DgrResourceId.localId("Team:Guard"));
        assertEquals("Team", DgrResourceId.namespace("Team:Guard"));
        assertEquals("boss", DgrResourceId.localId("Author:boss"));
        assertEquals("Author", DgrResourceId.namespace("Author:boss"));
        assertEquals("legacy.actor", DgrResourceId.localId("legacy.actor"));
        assertEquals("", DgrResourceId.namespace("legacy.actor"));
        assertEquals("x417574686f72/x626f7373.json", DgrResourceId.relativeJsonPath("Author:boss"));
        assertEquals("x417574686f72_x626f7373.dgrs", DgrResourceId.packageFileName("Author:boss"));
        assertEquals("legacy.actor.json", DgrResourceId.relativeJsonPath("legacy.actor"));
        assertEquals("legacy.actor.dgrs", DgrResourceId.packageFileName("legacy.actor"));
        assertEquals("x434f4e/x636f6e.json", DgrResourceId.relativeJsonPath("CON:con"));
        require(
            !DgrResourceId.relativeJsonPath("Team:Guard")
                .equals(DgrResourceId.relativeJsonPath("Team:guard")),
            "case-distinct namespaced paths collided");
        require(
            !DgrResourceId.packageFileName("Team:Guard")
                .equals(DgrResourceId.packageFileName("Team:guard")),
            "case-distinct package paths collided");
        assertFalse(
            DgrResourceId.relativeJsonPath("a:b")
                .contains(":"));
        assertFalse(
            DgrResourceId.relativeJsonPath("a:b")
                .contains("\\"));

        assertThrows(() -> DgrResourceId.qualify(" Author", "boss"));
        assertThrows(() -> DgrResourceId.qualify("Author", "_Boss"));
        assertThrows(() -> DgrResourceId.localId("Author/boss"));
        assertThrows(() -> DgrResourceId.namespace("Author:boss:extra"));
        assertThrows(() -> DgrResourceId.packageFileName("作者:boss"));

        String namespace = repeat('a', 32);
        String local = repeat('b', 63);
        String full = DgrResourceId.qualify(namespace, local);
        assertEquals(96, full.length());
        assertTrue(DgrResourceId.isFullId(full));
        assertFalse(DgrResourceId.isFullId(full + "x"));
        System.out.println("DGR_RESOURCE_ID_PROBE=PASS");
    }

    private static void require(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }

    private static void assertTrue(boolean value) {
        if (!value) throw new AssertionError("Expected true.");
    }

    private static void assertFalse(boolean value) {
        if (value) throw new AssertionError("Expected false.");
    }

    private static void assertEquals(Object expected, Object actual) {
        if (!expected.equals(actual)) throw new AssertionError("Expected '" + expected + "', got '" + actual + "'.");
    }

    private static void assertThrows(Runnable action) {
        try {
            action.run();
        } catch (IllegalArgumentException expected) {
            return;
        }
        throw new AssertionError("Expected IllegalArgumentException.");
    }

    private static String repeat(char value, int count) {
        char[] result = new char[count];
        java.util.Arrays.fill(result, value);
        return new String(result);
    }
}
