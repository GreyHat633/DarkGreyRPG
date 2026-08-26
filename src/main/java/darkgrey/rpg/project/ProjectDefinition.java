package darkgrey.rpg.project;

public final class ProjectDefinition {

    private final int schemaVersion;
    private final String id;
    private final String displayName;

    public ProjectDefinition(int schemaVersion, String id, String displayName) {
        this.schemaVersion = schemaVersion;
        this.id = id;
        this.displayName = displayName;
    }

    public int getSchemaVersion() {
        return schemaVersion;
    }

    public String getId() {
        return id;
    }

    public String getDisplayName() {
        return displayName;
    }
}
