package darkgrey.rpg.nominator;

/** Stable server response for GUI requests and probes. */
public final class NominatorResult {

    private final boolean accepted;
    private final String code;
    private final String explanation;

    private NominatorResult(boolean accepted, String code, String explanation) {
        this.accepted = accepted;
        this.code = code;
        this.explanation = explanation;
    }

    public static NominatorResult accepted(String explanation) {
        return new NominatorResult(true, "accepted", explanation);
    }

    public static NominatorResult rejected(String code, String explanation) {
        return new NominatorResult(false, code, explanation);
    }

    public boolean isAccepted() {
        return accepted;
    }

    public String getCode() {
        return code;
    }

    public String getExplanation() {
        return explanation;
    }
}
