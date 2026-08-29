package darkgrey.rpg.story.canonical.runtime;

/** The sole external boundary currently blocking the single Story cursor. */
public enum CanonicalStoryWaitKind {

    NONE,
    SESSION,
    TASK,
    ACTION,
    /** Legacy ActorInteract event wait (the cursor stores the required actor ID). */
    ACTOR_INTERACT,
    /** Legacy EnterRegion event wait (the cursor stores the sphere descriptor). */
    ENTER_REGION,
    /** Compatibility spelling used by older callers. */
    INTERACT_ACTOR

    ;

    public boolean isActorInteraction() {
        return this == ACTOR_INTERACT || this == INTERACT_ACTOR;
    }
}
