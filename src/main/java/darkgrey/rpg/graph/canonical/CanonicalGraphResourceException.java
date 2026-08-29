package darkgrey.rpg.graph.canonical;

/** Stable, code-bearing failure used by runtime canonical reload diagnostics. */
public final class CanonicalGraphResourceException extends RuntimeException {

    private final String code;

    public CanonicalGraphResourceException(String code, String message) {
        super(code + ": " + message);
        this.code = code;
    }

    public CanonicalGraphResourceException(String code, String message, Throwable cause) {
        super(code + ": " + message, cause);
        this.code = code;
    }

    public String getCode() {
        return code;
    }

    static CanonicalGraphResourceException failure(String code, String message) {
        return new CanonicalGraphResourceException(code, message);
    }
}
