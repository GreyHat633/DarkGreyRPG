package darkgrey.rpg.story.canonical.runtime;

/** Story-owned lifecycle decision; Task state never determines whether a new run starts. */
public enum CanonicalStoryStartDisposition {

    NEW,
    ALREADY_ACTIVE,
    ONCE_TERMINAL,
    REPEATABLE_RESTART;

    public boolean isEligible() {
        return this == NEW || this == REPEATABLE_RESTART;
    }
}
