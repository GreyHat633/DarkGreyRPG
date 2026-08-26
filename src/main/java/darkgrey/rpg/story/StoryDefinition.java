package darkgrey.rpg.story;

import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

public final class StoryDefinition {

    private final int schemaVersion;
    private final String id;
    private final String title;
    private final String entry;
    private final List<StoryNode> nodes;
    private final Map<String, StoryNode> nodesById;
    private final List<StoryConnection> connections;
    private final String notes;
    private final List<String> tags;

    public StoryDefinition(int schemaVersion, String id, String title, String entry, List<StoryNode> nodes,
        List<StoryConnection> connections, String notes, List<String> tags) {
        this.schemaVersion = schemaVersion;
        this.id = id;
        this.title = title;
        this.entry = entry;
        this.nodes = Collections.unmodifiableList(new ArrayList<StoryNode>(nodes));
        Map<String, StoryNode> index = new LinkedHashMap<String, StoryNode>();
        for (StoryNode node : nodes) {
            index.put(node.getId(), node);
        }
        this.nodesById = Collections.unmodifiableMap(index);
        this.connections = Collections.unmodifiableList(new ArrayList<StoryConnection>(connections));
        this.notes = notes;
        this.tags = Collections.unmodifiableList(new ArrayList<String>(tags));
    }

    public int getSchemaVersion() {
        return schemaVersion;
    }

    public String getId() {
        return id;
    }

    public String getTitle() {
        return title;
    }

    public String getEntry() {
        return entry;
    }

    public List<StoryNode> getNodes() {
        return nodes;
    }

    public StoryNode getNode(String nodeId) {
        return nodesById.get(nodeId);
    }

    public List<StoryConnection> getConnections() {
        return connections;
    }

    public StoryConnection findConnection(String from, String output) {
        for (StoryConnection connection : connections) {
            if (connection.getFrom()
                .equals(from)
                && connection.getOutput()
                    .equals(output)) {
                return connection;
            }
        }
        return null;
    }

    public List<StoryConnection> getOutgoing(String from) {
        List<StoryConnection> result = new ArrayList<StoryConnection>();
        for (StoryConnection connection : connections) {
            if (connection.getFrom()
                .equals(from)) {
                result.add(connection);
            }
        }
        return result;
    }

    public String getNotes() {
        return notes;
    }

    public List<String> getTags() {
        return tags;
    }
}
