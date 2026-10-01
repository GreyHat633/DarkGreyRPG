package darkgrey.rpg.session.runtime;

/** Detached option exposed while a canonical Session is paused at a choice. */
public final class CanonicalSessionChoiceOption {

    private final String optionId;
    private final String displayText;
    private final String flowPortId;
    private final String conditionPortId, behavior, hint;
    private final boolean enabled;

    public CanonicalSessionChoiceOption(String optionId, String displayText, String flowPortId) {
        this(optionId, displayText, flowPortId, null, "hide", "", true);
    }

    public CanonicalSessionChoiceOption(String optionId, String displayText, String flowPortId, String conditionPortId,
        String behavior, String hint, boolean enabled) {
        this.optionId = optionId;
        this.displayText = displayText;
        this.flowPortId = flowPortId;
        this.conditionPortId = conditionPortId;
        this.behavior = behavior;
        this.hint = hint;
        this.enabled = enabled;
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

    public String getConditionPortId() {
        return conditionPortId;
    }

    public boolean isEnabled() {
        return enabled;
    }

    public boolean isVisible() {
        return enabled || "disable".equals(behavior);
    }

    public String getHint() {
        return enabled ? "" : hint;
    }
}
