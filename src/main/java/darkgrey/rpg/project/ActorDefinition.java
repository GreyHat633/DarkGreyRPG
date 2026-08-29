package darkgrey.rpg.project;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

public final class ActorDefinition {

    public static final String TYPE_LEGACY = "legacy";
    public static final String TYPE_INDIVIDUAL = "individual";
    public static final String TYPE_COLLECTIVE = "collective";

    private final int schemaVersion;
    private final String id;
    private final String displayName;
    private final String notes;
    private final List<String> tags;
    private final String homeStoryId;
    private final String type;

    public ActorDefinition(int schemaVersion, String id, String displayName, String notes, List<String> tags,
        String homeStoryId) {
        this(schemaVersion, TYPE_LEGACY, id, displayName, notes, tags, homeStoryId);
    }

    public ActorDefinition(int schemaVersion, String type, String id, String displayName, String notes,
        List<String> tags, String homeStoryId) {
        this.schemaVersion = schemaVersion;
        this.type = type;
        this.id = id;
        this.displayName = displayName;
        this.notes = notes;
        this.tags = Collections.unmodifiableList(new ArrayList<String>(tags));
        this.homeStoryId = homeStoryId;
    }

    public int getSchemaVersion() {
        return schemaVersion;
    }

    public String getId() {
        return id;
    }

    public String getType() {
        return type;
    }

    public boolean isIndividual() {
        return TYPE_INDIVIDUAL.equals(type);
    }

    public boolean isCollective() {
        return TYPE_COLLECTIVE.equals(type);
    }

    public String getDisplayName() {
        return displayName;
    }

    public String getNotes() {
        return notes;
    }

    public List<String> getTags() {
        return tags;
    }

    public String getHomeStoryId() {
        return homeStoryId;
    }
}
