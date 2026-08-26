package darkgrey.rpg.dialogue;

import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

public final class DialogueDefinition {

    private final int schemaVersion;
    private final String id;
    private final String title;
    private final List<String> speakers;
    private final String entry;
    private final List<DialogueNode> nodes;
    private final Map<String, DialogueNode> nodesById;
    private final String notes;
    private final List<String> tags;

    public DialogueDefinition(int schemaVersion, String id, String title, List<String> speakers, String entry,
        List<DialogueNode> nodes, String notes, List<String> tags) {
        this.schemaVersion = schemaVersion;
        this.id = id;
        this.title = title;
        this.speakers = Collections.unmodifiableList(new ArrayList<String>(speakers));
        this.entry = entry;
        this.nodes = Collections.unmodifiableList(new ArrayList<DialogueNode>(nodes));
        Map<String, DialogueNode> index = new LinkedHashMap<String, DialogueNode>();
        for (DialogueNode node : nodes) {
            index.put(node.getId(), node);
        }
        this.nodesById = Collections.unmodifiableMap(index);
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

    public List<String> getSpeakers() {
        return speakers;
    }

    public String getEntry() {
        return entry;
    }

    public List<DialogueNode> getNodes() {
        return nodes;
    }

    public DialogueNode getNode(String nodeId) {
        return nodesById.get(nodeId);
    }

    public String getNotes() {
        return notes;
    }

    public List<String> getTags() {
        return tags;
    }
}
