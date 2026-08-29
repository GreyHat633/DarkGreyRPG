package darkgrey.rpg.graph.canonical;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

/** Immutable canonical graph document. */
public final class CanonicalGraph {

    private final List<CanonicalGraphNode> nodes;
    private final List<CanonicalGraphConnection> connections;

    public CanonicalGraph(List<CanonicalGraphNode> nodes, List<CanonicalGraphConnection> connections) {
        this.nodes = Collections.unmodifiableList(new ArrayList<CanonicalGraphNode>(nodes));
        this.connections = Collections.unmodifiableList(new ArrayList<CanonicalGraphConnection>(connections));
    }

    public List<CanonicalGraphNode> getNodes() {
        return nodes;
    }

    public List<CanonicalGraphConnection> getConnections() {
        return connections;
    }

    public List<CanonicalGraphConnection> getEdges() {
        return connections;
    }
}
