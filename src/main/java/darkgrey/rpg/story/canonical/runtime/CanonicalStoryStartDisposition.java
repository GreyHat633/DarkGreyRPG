package darkgrey.rpg.story.canonical.runtime;

/** Story-owned lifecycle decision; Task state never determines whether a new run starts. */
public enum CanonicalStoryStartDisposition {

    NEW,
    ALREADY_ACTIVE,
    ONCE_TERMINAL,
    ERROR_TERMINAL,
    REPEAT_WAITING,
    INVALID_REPEAT_TIME,
    REPEATABLE_RESTART,
    CONTAINER_BLOCKED;

    public boolean isEligible() {
        return this == NEW || this == REPEATABLE_RESTART;
    }
}
