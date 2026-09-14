package darkgrey.rpg.story.canonical.runtime;

/** The sole external boundary currently blocking the single Story cursor. */
public enum CanonicalStoryWaitKind {
    NONE,
    SESSION,
    TASK,
    ACTION,
    /** Internal Condition node waiting for a connected branch to become true. */
    CONDITION,
    TITLE
}
