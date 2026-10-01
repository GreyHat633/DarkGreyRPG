package darkgrey.rpg.network.message.canonical;

import java.util.Objects;

/** Stable option identity and author-facing label; identity is never an array index. */
public final class CanonicalSessionChoiceOption {

    private final String optionId;
    private final String displayText;
    private final boolean enabled;
    private final String hint;

    public CanonicalSessionChoiceOption(String optionId, String displayText) {
        this(optionId, displayText, true, "");
    }

    public CanonicalSessionChoiceOption(String optionId, String displayText, boolean enabled, String hint) {
        this.optionId = CanonicalSessionNetworkCodec
            .requireField(optionId, "option_id", CanonicalSessionNetworkCodec.MAX_ID_BYTES);
        this.displayText = CanonicalSessionNetworkCodec
            .requireField(displayText, "display_text", CanonicalSessionNetworkCodec.MAX_OPTION_TEXT_BYTES);
        this.enabled = enabled;
        this.hint = CanonicalSessionNetworkCodec.requireOptionalField(hint, "hint", 2048);
    }

    public String getOptionId() {
        return optionId;
    }

    public String getDisplayText() {
        return displayText;
    }

    public boolean isEnabled() {
        return enabled;
    }

    public String getHint() {
        return hint;
    }

    @Override
    public boolean equals(Object other) {
        if (this == other) return true;
        if (!(other instanceof CanonicalSessionChoiceOption)) return false;
        CanonicalSessionChoiceOption that = (CanonicalSessionChoiceOption) other;
        return optionId.equals(that.optionId) && displayText.equals(that.displayText)
            && enabled == that.enabled
            && hint.equals(that.hint);
    }

    @Override
    public int hashCode() {
        return Objects.hash(optionId, displayText, enabled, hint);
    }
}
