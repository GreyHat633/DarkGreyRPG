package darkgrey.rpg.client.session;

/** Pixel-grid policy for Minecraft's half-GUI-unit Unicode glyphs. No client or GL dependencies. */
public final class DialogueFontScale {

    private DialogueFontScale() {}

    public static double effective(double requested, int guiFactor) {
        if (requested <= 1) return 1;
        int factor = Math.max(1, guiFactor);
        // One source Unicode texel covers half a GUI unit. Never round enlargement down.
        return Math.ceil(requested * factor / 2.0 - 1e-9) * 2.0 / factor;
    }

    public static double snap(double coordinate, int guiFactor) {
        int factor = Math.max(1, guiFactor);
        return Math.round(coordinate * factor) / (double) factor;
    }
}
