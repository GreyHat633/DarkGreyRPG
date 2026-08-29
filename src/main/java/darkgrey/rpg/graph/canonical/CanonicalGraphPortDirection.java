package darkgrey.rpg.graph.canonical;

public enum CanonicalGraphPortDirection {

    INPUT("input"),
    OUTPUT("output");

    private final String jsonName;

    CanonicalGraphPortDirection(String jsonName) {
        this.jsonName = jsonName;
    }

    public String getJsonName() {
        return jsonName;
    }

    public static CanonicalGraphPortDirection parse(String value) {
        for (CanonicalGraphPortDirection direction : values()) {
            if (direction.jsonName.equals(value)) return direction;
        }
        throw CanonicalGraphResourceException.failure(
            "graph.port.direction.invalid",
            "Unknown graph port direction '" + value + "'. Expected 'input' or 'output'.");
    }
}
