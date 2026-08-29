package darkgrey.rpg.graph.canonical;

public enum CanonicalGraphInterfaceKind {

    FLOW("flow"),
    LOGIC("logic");

    private final String jsonName;

    CanonicalGraphInterfaceKind(String jsonName) {
        this.jsonName = jsonName;
    }

    public String getJsonName() {
        return jsonName;
    }

    public static CanonicalGraphInterfaceKind parse(String value) {
        for (CanonicalGraphInterfaceKind kind : values()) {
            if (kind.jsonName.equals(value)) return kind;
        }
        throw CanonicalGraphResourceException.failure(
            "graph.port.kind.invalid",
            "Unknown graph interface kind '" + value + "'. Expected 'flow' or 'logic'.");
    }
}
