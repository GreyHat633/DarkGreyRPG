package darkgrey.rpg.graph.canonical;

/** Immutable schema-version-1/2 canonical Story membership manifest. */
public final class CanonicalStoryMembership {

    public static final int LEGACY_SCHEMA_VERSION = 1;
    public static final int CURRENT_SCHEMA_VERSION = 2;

    private final int schemaVersion;
    private final String storyId;
    private final CanonicalStoryMembershipSet ownedResources;
    private final CanonicalStoryMembershipSet referencedResources;

    public CanonicalStoryMembership(int schemaVersion, String storyId, CanonicalStoryMembershipSet ownedResources,
        CanonicalStoryMembershipSet referencedResources) {
        this.schemaVersion = schemaVersion;
        this.storyId = storyId;
        if (ownedResources == null) throw new IllegalArgumentException("ownedResources cannot be null.");
        if (referencedResources == null) throw new IllegalArgumentException("referencedResources cannot be null.");
        this.ownedResources = ownedResources;
        this.referencedResources = referencedResources;
    }

    public CanonicalStoryMembership(String storyId, CanonicalStoryMembershipSet ownedResources,
        CanonicalStoryMembershipSet referencedResources) {
        this(CURRENT_SCHEMA_VERSION, storyId, ownedResources, referencedResources);
    }

    public CanonicalStoryMembership(String storyId, CanonicalStoryMembershipSet ownedResources) {
        this(storyId, ownedResources, new CanonicalStoryMembershipSet());
    }

    public int getSchemaVersion() {
        return schemaVersion;
    }

    public String getStoryId() {
        return storyId;
    }

    public String getId() {
        return storyId;
    }

    public CanonicalStoryMembershipSet getOwnedResources() {
        return ownedResources;
    }

    public CanonicalStoryMembershipSet getReferencedResources() {
        return referencedResources;
    }

    public CanonicalStoryMembershipSet getOwned() {
        return ownedResources;
    }

    public CanonicalStoryMembershipSet getReferenced() {
        return referencedResources;
    }
}
