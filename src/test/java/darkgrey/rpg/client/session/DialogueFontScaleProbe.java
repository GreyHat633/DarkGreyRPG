package darkgrey.rpg.client.session;

/** Physical glyph-grid and shared layout regression checks, without a Minecraft or GL context. */
public final class DialogueFontScaleProbe {

    public static void main(String[] args) {
        double[] requested = { 1, 1.25, 1.5 };
        for (int factor = 1; factor <= 8; factor++) {
            for (double value : requested) {
                double actual = DialogueFontScale.effective(value, factor);
                require(actual >= value, "enlargement must never round down");
                if (value == 1) require(actual == 1, "100% stays compatible at every GUI scale");
                else {
                    double texel = actual * factor / 2;
                    require(
                        Math.abs(texel - Math.rint(texel)) < 1e-8,
                        "Unicode source pixels map to whole screen pixels");
                    require(actual - value < 2.0 / factor + 1e-8, "smallest compatible enlargement");
                }
                require(
                    Math.abs(
                        DialogueFontScale.snap(20.35, factor) * factor
                            - Math.rint(DialogueFontScale.snap(20.35, factor) * factor))
                        < 1e-8,
                    "centered origin maps to a pixel");
                for (int[] size : new int[][] { { 320, 240 }, { 480, 320 }, { 1280, 800 }, { 854, 480 } }) {
                    CanonicalDialogueLayout layout = new CanonicalDialogueLayout(size[0], size[1], actual, 9);
                    int nameHeight = (int) Math.ceil(9 * actual);
                    require(layout.speakerTop(nameHeight) + nameHeight <= layout.dividerTop(), "name above divider");
                    require(layout.buttonTop() + 16 < layout.bodyTop(), "buttons above body");
                    require(layout.portraitTop() >= layout.bodyTop(), "portrait below header");
                    require(layout.portraitTop() + layout.portraitSize <= layout.bottom - 6, "portrait inside body");
                    require(
                        Math.abs(
                            (layout.portraitTop() * 2 + layout.portraitSize) - (layout.bodyTop() + layout.bottom - 6))
                            <= 1,
                        "portrait stays centered when header grows");
                }
            }
        }
        CanonicalDialogueLayout baseline = new CanonicalDialogueLayout(320, 240, 1, 9);
        require(
            baseline.portraitSize == 48 && baseline.bodyTop() == 175
                && baseline.textLeft == 80
                && baseline.bodyBottom() == 221,
            "standard 100% capacity geometry unchanged");
        require(DialogueFontScale.effective(1.5, 1) == 2, "small GUI 150% maps to 200%");
        require(DialogueFontScale.effective(1.5, 4) == 1.5, "large GUI preserves exact 150%");
        System.out.println(
            "DialogueFontScaleProbe PASS: pixel grid, baseline capacity, scaled heading and portrait centering");
    }

    private static void require(boolean value, String message) {
        if (!value) throw new IllegalStateException(message);
    }
}
