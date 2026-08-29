package darkgrey.rpg.nominator;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;
import java.util.UUID;

/** Persisted nominator selection; identity itself remains in NpcIdentitySavedData. */
public final class NominatorEntityBinding {

    private final UUID entityUuid;
    private final String individualId;
    private final List<String> groupIds;
    private final String storyId;

    public NominatorEntityBinding(UUID entityUuid, String individualId, List<String> groupIds, String storyId) {
        if (entityUuid == null) throw new IllegalArgumentException("Entity UUID is required.");
        this.entityUuid = entityUuid;
        this.individualId = checked(individualId, "Individual ID");
        List<String> values = new ArrayList<String>();
        if (groupIds != null && groupIds.size() > 32) throw new IllegalArgumentException("Too many entity groups.");
        if (groupIds != null) for (String group : groupIds) {
            String value = checked(group, "Group ID");
            if (value != null && !values.contains(value)) values.add(value);
        }
        this.groupIds = Collections.unmodifiableList(values);
        this.storyId = checked(storyId, "Story ID");
    }

    public UUID getEntityUuid() {
        return entityUuid;
    }

    public String getIndividualId() {
        return individualId;
    }

    public List<String> getGroupIds() {
        return groupIds;
    }

    public String getStoryId() {
        return storyId;
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }

    private static String checked(String value, String label) {
        if (blank(value)) return null;
        if (value.trim()
            .length() > 256) throw new IllegalArgumentException(label + " is too long.");
        return value.trim();
    }
}
