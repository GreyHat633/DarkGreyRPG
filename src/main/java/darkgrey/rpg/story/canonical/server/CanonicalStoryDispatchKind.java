package darkgrey.rpg.story.canonical.server;

/** Next server-side boundary reached by one canonical Story cursor. */
public enum CanonicalStoryDispatchKind {
    SESSION,
    TASK,
    ACTION,
    ACTOR_INTERACT,
    ENTER_REGION,
    /** Compatibility spelling for callers using the old node naming. */
    INTERACT_ACTOR,
    TERMINATED,
    TRANSFERRED,
    ERROR
}
