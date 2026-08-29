package darkgrey.rpg.project;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

/** Identity-only Studio item resource; concrete ItemStack bindings live in world SavedData. */
public final class ItemResourceDefinition {

    public static final String TYPE_INDIVIDUAL = "individual";
    public static final String TYPE_COLLECTIVE = "collective";

    private final int schemaVersion;
    private final String type;
    private final String id;
    private final String displayName;
    private final List<String> tags;

    public ItemResourceDefinition(int schemaVersion, String type, String id, String displayName, List<String> tags) {
        this.schemaVersion = schemaVersion;
        this.type = type;
        this.id = id;
        this.displayName = displayName;
        this.tags = Collections.unmodifiableList(new ArrayList<String>(tags));
    }

    public int getSchemaVersion() {
        return schemaVersion;
    }

    public String getType() {
        return type;
    }

    public String getId() {
        return id;
    }

    public String getDisplayName() {
        return displayName;
    }

    public List<String> getTags() {
        return tags;
    }

    public boolean isIndividual() {
        return TYPE_INDIVIDUAL.equals(type);
    }

    public boolean isCollective() {
        return TYPE_COLLECTIVE.equals(type);
    }
}
