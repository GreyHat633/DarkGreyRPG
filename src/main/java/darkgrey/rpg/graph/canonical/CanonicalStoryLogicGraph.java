package darkgrey.rpg.graph.canonical;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

/** Immutable schema-versioned aggregate of cross-Story Flow and Logic boundary connections. */
public final class CanonicalStoryLogicGraph {

    public static final int CURRENT_SCHEMA_VERSION = 2;
    private static final CanonicalStoryLogicGraph EMPTY = new CanonicalStoryLogicGraph(
        CURRENT_SCHEMA_VERSION,
        Collections.<CanonicalStoryLogicConnection>emptyList());

    private final int schemaVersion;
    private final List<CanonicalStoryLogicConnection> connections;

    public CanonicalStoryLogicGraph(int schemaVersion, List<CanonicalStoryLogicConnection> connections) {
        if (connections == null) throw new IllegalArgumentException("connections cannot be null.");
        this.schemaVersion = schemaVersion;
        this.connections = Collections.unmodifiableList(new ArrayList<CanonicalStoryLogicConnection>(connections));
    }

    public CanonicalStoryLogicGraph(List<CanonicalStoryLogicConnection> connections) {
        this(CURRENT_SCHEMA_VERSION, connections);
    }

    public static CanonicalStoryLogicGraph empty() {
        return EMPTY;
    }

    public int getSchemaVersion() {
        return schemaVersion;
    }

    public List<CanonicalStoryLogicConnection> getConnections() {
        return connections;
    }

    public List<CanonicalStoryLogicConnection> getEdges() {
        return connections;
    }
}
