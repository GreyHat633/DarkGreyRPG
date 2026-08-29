package darkgrey.rpg.item.identity;

public enum ItemMatchMode {

    EXACT("exact"),
    FUZZY("fuzzy");

    private final String jsonName;

    ItemMatchMode(String jsonName) {
        this.jsonName = jsonName;
    }

    public String getJsonName() {
        return jsonName;
    }

    public static ItemMatchMode fromName(String value) {
        for (ItemMatchMode mode : values()) if (mode.jsonName.equals(value)) return mode;
        throw new IllegalArgumentException("Item match mode must be exact or fuzzy.");
    }
}
