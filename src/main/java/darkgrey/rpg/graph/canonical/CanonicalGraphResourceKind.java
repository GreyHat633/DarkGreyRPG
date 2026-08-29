package darkgrey.rpg.graph.canonical;

/** Frozen canonical resource kinds and their ordinal, case-sensitive JSON names. */
public enum CanonicalGraphResourceKind {

    STORY("story"),
    SESSION("session"),
    TASK("task");

    private final String jsonName;

    CanonicalGraphResourceKind(String jsonName) {
        this.jsonName = jsonName;
    }

    public String getJsonName() {
        return jsonName;
    }

    public static CanonicalGraphResourceKind parse(String value) {
        for (CanonicalGraphResourceKind kind : values()) {
            if (kind.jsonName.equals(value)) return kind;
        }
        throw CanonicalGraphResourceException.failure(
            "graph.resource.kind.unsupported",
            "Unsupported resource_kind '" + value + "'; expected 'story', 'session', or 'task'.");
    }
}
