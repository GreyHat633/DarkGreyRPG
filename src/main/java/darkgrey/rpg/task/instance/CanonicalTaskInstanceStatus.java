package darkgrey.rpg.task.instance;

/** Lifecycle of the aggregate TaskInstance (distinct from the pure runtime status). */
public enum CanonicalTaskInstanceStatus {
    NOT_STARTED,
    ACTIVE,
    SETTLED,
    CANCELLED_BY_STORY_TERMINATION,
    ERROR
}
