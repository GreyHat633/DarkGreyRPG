package darkgrey.rpg.story.canonical.runtime;

import java.util.Map;

import com.google.gson.JsonElement;

/** Bounded title content and wall-clock animation timings (seconds). */
public final class CanonicalTitleConfiguration {

    public final String main;
    public final String subtitle;
    public final double fadeIn;
    public final double stay;
    public final double fadeOut;
    public final boolean waitForCompletion;

    public CanonicalTitleConfiguration(String main, String subtitle, double fadeIn, double stay, double fadeOut) {
        this(main, subtitle, fadeIn, stay, fadeOut, true);
    }

    public CanonicalTitleConfiguration(String main, String subtitle, double fadeIn, double stay, double fadeOut,
        boolean waitForCompletion) {
        if (main == null || main.trim()
            .isEmpty() || main.length() > 1024 || subtitle == null || subtitle.length() > 1024)
            throw new IllegalArgumentException("Invalid title text");
        for (double value : new double[] { fadeIn, stay, fadeOut })
            if (Double.isNaN(value) || Double.isInfinite(value) || value < 0 || value > 60)
                throw new IllegalArgumentException("Invalid title timing");
        this.main = main;
        this.subtitle = subtitle;
        this.fadeIn = fadeIn;
        this.stay = stay;
        this.fadeOut = fadeOut;
        this.waitForCompletion = waitForCompletion;
    }

    public double duration() {
        return fadeIn + stay + fadeOut;
    }

    public float alpha(double elapsed) {
        if (elapsed < 0 || elapsed >= duration()) return 0;
        if (fadeIn > 0 && elapsed < fadeIn) return (float) (elapsed / fadeIn);
        if (elapsed < fadeIn + stay) return 1;
        return fadeOut == 0 ? 0 : (float) Math.max(0, (duration() - elapsed) / fadeOut);
    }

    public static CanonicalTitleConfiguration parse(Map<String, JsonElement> properties) {
        for (String key : properties.keySet())
            if (!java.util.Arrays.asList("main", "subtitle", "fade_in", "stay", "fade_out", "wait_for_completion")
                .contains(key)) throw new IllegalArgumentException("Unexpected title property: " + key);
        JsonElement wait = properties.get("wait_for_completion");
        if (wait != null && (!wait.isJsonPrimitive() || !wait.getAsJsonPrimitive()
            .isBoolean())) throw new IllegalArgumentException("Invalid title wait_for_completion");
        return new CanonicalTitleConfiguration(
            text(properties, "main"),
            text(properties, "subtitle"),
            number(properties, "fade_in"),
            number(properties, "stay"),
            number(properties, "fade_out"),
            wait == null || wait.getAsBoolean());
    }

    private static String text(Map<String, JsonElement> properties, String key) {
        JsonElement value = properties.get(key);
        if (value == null || !value.isJsonPrimitive()
            || !value.getAsJsonPrimitive()
                .isString())
            throw new IllegalArgumentException("Invalid title " + key);
        return value.getAsString();
    }

    private static double number(Map<String, JsonElement> properties, String key) {
        JsonElement value = properties.get(key);
        if (value == null || !value.isJsonPrimitive()
            || !value.getAsJsonPrimitive()
                .isNumber())
            throw new IllegalArgumentException("Invalid title " + key);
        return value.getAsDouble();
    }
}
