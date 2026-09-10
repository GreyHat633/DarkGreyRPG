package darkgrey.rpg.project;

public final class ProjectDefinition {

    private final int schemaVersion;
    private final String id;
    private final String displayName;
    private final String projectOriginCode;

    public ProjectDefinition(int schemaVersion, String id, String displayName) {
        this.schemaVersion = schemaVersion;
        this.id = id;
        this.displayName = displayName;
        this.projectOriginCode = id;
    }

    public ProjectDefinition(int schemaVersion, String id, String displayName, String projectOriginCode) {
        this.schemaVersion = schemaVersion;
        this.id = id;
        this.displayName = displayName;
        if (projectOriginCode == null || projectOriginCode.trim()
            .isEmpty()) {
            throw new IllegalArgumentException("projectOriginCode must be a non-empty string.");
        }
        this.projectOriginCode = projectOriginCode;
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

    /** Stable author/project origin used for cross-package namespace diagnostics. */
    public String getProjectOriginCode() {
        return projectOriginCode;
    }
}
