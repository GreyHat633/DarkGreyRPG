package darkgrey.rpg.story.canonical.server;

/** Next server-side boundary reached by one canonical Story cursor. */
public enum CanonicalStoryDispatchKind {
    SESSION,
    TASK,
    ACTION,
    CONDITION,
    TERMINATED,
    ERROR,
    TITLE
}
