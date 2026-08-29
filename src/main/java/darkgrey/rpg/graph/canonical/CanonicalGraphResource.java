package darkgrey.rpg.graph.canonical;

/** Immutable schema-version-1 canonical Story/Session/Task envelope. */
public final class CanonicalGraphResource {

    public static final int CURRENT_SCHEMA_VERSION = 1;

    private final int schemaVersion;
    private final CanonicalGraphResourceKind resourceKind;
    private final String id;
    private final String displayName;
    private final CanonicalGraph graph;

    public CanonicalGraphResource(int schemaVersion, CanonicalGraphResourceKind resourceKind, String id,
        String displayName, CanonicalGraph graph) {
        this.schemaVersion = schemaVersion;
        this.resourceKind = resourceKind;
        this.id = id;
        this.displayName = displayName;
        this.graph = graph;
    }

    public int getSchemaVersion() {
        return schemaVersion;
    }

    public CanonicalGraphResourceKind getResourceKind() {
        return resourceKind;
    }

    public CanonicalGraphResourceKind getKind() {
        return resourceKind;
    }

    public String getId() {
        return id;
    }

    public String getDisplayName() {
        return displayName;
    }

    public CanonicalGraph getGraph() {
        return graph;
    }
}
