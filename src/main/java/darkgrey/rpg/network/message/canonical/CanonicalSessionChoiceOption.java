package darkgrey.rpg.network.message.canonical;

import java.util.Objects;

/** Stable option identity and author-facing label; identity is never an array index. */
public final class CanonicalSessionChoiceOption {

    private final String optionId;
    private final String displayText;

    public CanonicalSessionChoiceOption(String optionId, String displayText) {
        this.optionId = CanonicalSessionNetworkCodec
            .requireField(optionId, "option_id", CanonicalSessionNetworkCodec.MAX_ID_BYTES);
        this.displayText = CanonicalSessionNetworkCodec
            .requireField(displayText, "display_text", CanonicalSessionNetworkCodec.MAX_OPTION_TEXT_BYTES);
    }

    public String getOptionId() {
        return optionId;
    }

    public String getDisplayText() {
        return displayText;
    }

    @Override
    public boolean equals(Object other) {
        if (this == other) return true;
        if (!(other instanceof CanonicalSessionChoiceOption)) return false;
        CanonicalSessionChoiceOption that = (CanonicalSessionChoiceOption) other;
        return optionId.equals(that.optionId) && displayText.equals(that.displayText);
    }

    @Override
    public int hashCode() {
        return Objects.hash(optionId, displayText);
    }
}
