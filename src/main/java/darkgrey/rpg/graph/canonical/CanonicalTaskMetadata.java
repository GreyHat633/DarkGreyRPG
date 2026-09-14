package darkgrey.rpg.graph.canonical;

/** Task-only author text, independent of Objective descriptions. */
public final class CanonicalTaskMetadata {

    private final String description;

    public CanonicalTaskMetadata(String description) {
        if (description == null) throw new IllegalArgumentException("Task description is required.");
        this.description = description;
    }

    public String getDescription() {
        return description;
    }
}
