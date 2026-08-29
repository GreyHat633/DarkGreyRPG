package darkgrey.rpg.story.canonical.runtime;

/** Enabled 0.3.0.0 repeat-policy minimum. */
public enum CanonicalStoryRepeatPolicy {

    ONCE,
    REPEATABLE;

    public static CanonicalStoryRepeatPolicy fromJsonName(String value) {
        if ("once".equals(value)) return ONCE;
        if ("repeatable".equals(value)) return REPEATABLE;
        throw new IllegalArgumentException("Unsupported canonical Story repeat policy: " + value);
    }

    public String getJsonName() {
        return this == ONCE ? "once" : "repeatable";
    }
}
