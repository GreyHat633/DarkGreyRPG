package darkgrey.rpg.client.session;

/** Client-local default. -1 on a session frame means to follow this preference. */
public final class DialoguePreferences {

    private static double speed = 30;

    private DialoguePreferences() {}

    public static double speed() {
        return speed;
    }

    public static void setSpeed(double value) {
        if (Double.isNaN(value) || Double.isInfinite(value) || value < 0 || value > 120)
            throw new IllegalArgumentException("dialogue speed");
        speed = value;
    }

    public static double resolve(double override) {
        return override == -1 ? speed : override;
    }

    /** UI positions 0..120 represent 1..120 characters/s, then instant. */
    public static double sliderPosition() {
        return speed == 0 ? 120 : speed - 1;
    }

    public static void setSliderPosition(double position) {
        int step = (int) Math.round(Math.max(0, Math.min(120, position)));
        setSpeed(step == 120 ? 0 : step + 1);
    }
}
