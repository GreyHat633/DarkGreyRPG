package darkgrey.rpg.client.session;

/** Client presentation only. Never owns story, task, or reward state. */
public final class PlayerUiPreferences {

    public enum Theme {
        CHARCOAL,
        WHITE,
        AZURE
    }

    private static Theme theme = Theme.CHARCOAL;
    private static double opacity = 0.8;
    private static double textScale = 1;
    private static double voiceVolume = 1;
    private static double musicVolume = 1;
    private static double autoWaitSeconds = 3;
    private static boolean automatic;

    public static boolean automatic() {
        return automatic;
    }

    public static void setAutomatic(boolean value) {
        automatic = value;
    }

    private PlayerUiPreferences() {}

    public static Theme theme() {
        return theme;
    }

    public static double opacity() {
        return opacity;
    }

    public static double textScale() {
        return textScale;
    }

    public static double voiceVolume() {
        return voiceVolume;
    }

    public static double musicVolume() {
        return musicVolume;
    }

    public static double autoWaitSeconds() {
        return autoWaitSeconds;
    }

    public static void setTheme(Theme value) {
        theme = value == null ? Theme.CHARCOAL : value;
    }

    public static void setOpacity(double value) {
        opacity = unit(value, 0.8);
    }

    public static void setTextScale(double value) {
        textScale = value == 1.25 || value == 1.5 ? value : 1;
    }

    public static void setVoiceVolume(double value) {
        voiceVolume = unit(value, 1);
    }

    public static void setMusicVolume(double value) {
        musicVolume = unit(value, 1);
    }

    public static void setAutoWaitSeconds(double value) {
        autoWaitSeconds = Double.isNaN(value) || Double.isInfinite(value) || value < 1 || value > 10 ? 3
            : Math.round(value * 2) / 2.0;
    }

    public static void reset() {
        theme = Theme.CHARCOAL;
        opacity = 0.8;
        textScale = voiceVolume = musicVolume = 1;
        autoWaitSeconds = 3;
        DialoguePreferences.setSpeed(30);
    }

    private static double unit(double value, double fallback) {
        return Double.isNaN(value) || Double.isInfinite(value) || value < 0 || value > 1 ? fallback : value;
    }
}
