package darkgrey.rpg.graph.canonical;

/** Stable, code-bearing failure used by canonical project-content loading. */
public final class CanonicalProjectContentException extends RuntimeException {

    private final String code;

    public CanonicalProjectContentException(String code, String message) {
        super(code + ": " + message);
        this.code = code;
    }

    public CanonicalProjectContentException(String code, String message, Throwable cause) {
        super(code + ": " + message, cause);
        this.code = code;
    }

    public String getCode() {
        return code;
    }

    static CanonicalProjectContentException failure(String code, String message) {
        return new CanonicalProjectContentException(code, message);
    }
}
