package darkgrey.rpg.session.runtime;

/** Detached option exposed while a canonical Session is paused at a choice. */
public final class CanonicalSessionChoiceOption {

    private final String optionId;
    private final String displayText;
    private final String flowPortId;

    public CanonicalSessionChoiceOption(String optionId, String displayText, String flowPortId) {
        this.optionId = optionId;
        this.displayText = displayText;
        this.flowPortId = flowPortId;
    }

    public String getOptionId() {
        return optionId;
    }

    public String getDisplayText() {
        return displayText;
    }

    public String getFlowPortId() {
        return flowPortId;
    }
}
