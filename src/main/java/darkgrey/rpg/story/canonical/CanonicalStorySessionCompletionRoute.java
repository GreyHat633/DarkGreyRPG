package darkgrey.rpg.story.canonical;

import java.util.Map;

/** Pure detached result of routing one completed canonical Session. */
public final class CanonicalStorySessionCompletionRoute {

    private final CanonicalStoryFlowTransition nextFlow;
    private final CanonicalStoryPublicLogicSnapshot publicLogic;

    public CanonicalStorySessionCompletionRoute(CanonicalStoryFlowTransition nextFlow,
        CanonicalStoryPublicLogicSnapshot publicLogic) {
        if (nextFlow == null || publicLogic == null) throw new IllegalArgumentException("Route values are required.");
        this.nextFlow = nextFlow;
        this.publicLogic = publicLogic;
    }

    public CanonicalStoryFlowTransition getNextFlow() {
        return nextFlow;
    }

    public CanonicalStoryFlowTransition getNextFlowTransition() {
        return nextFlow;
    }

    public CanonicalStoryFlowTransition getTransition() {
        return nextFlow;
    }

    public CanonicalStoryPublicLogicSnapshot getPublicLogic() {
        return publicLogic;
    }

    public CanonicalStoryPublicLogicSnapshot getPublicLogicSnapshot() {
        return publicLogic;
    }

    public CanonicalStoryPublicLogicSnapshot getLogicSnapshot() {
        return publicLogic;
    }

    public Map<String, Boolean> getPublicLogicOutputs() {
        return publicLogic.getValues();
    }

    public Map<String, Boolean> getPublicLogicOutputMap() {
        return publicLogic.getValues();
    }
}
