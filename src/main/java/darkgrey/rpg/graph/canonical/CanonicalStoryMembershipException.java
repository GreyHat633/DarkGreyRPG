package darkgrey.rpg.graph.canonical;

/** Stable, code-bearing failure used by canonical Story membership loading. */
public final class CanonicalStoryMembershipException extends RuntimeException {

    private final String code;

    public CanonicalStoryMembershipException(String code, String message) {
        super(code + ": " + message);
        this.code = code;
    }

    public CanonicalStoryMembershipException(String code, String message, Throwable cause) {
        super(code + ": " + message, cause);
        this.code = code;
    }

    public String getCode() {
        return code;
    }

    static CanonicalStoryMembershipException failure(String code, String message) {
        return new CanonicalStoryMembershipException(code, message);
    }
}
