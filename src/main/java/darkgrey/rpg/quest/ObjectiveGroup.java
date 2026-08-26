package darkgrey.rpg.quest;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

public final class ObjectiveGroup {

    private final String id;
    private final ObjectiveGroupMode mode;
    private final List<String> objectiveIds;

    public ObjectiveGroup(String id, ObjectiveGroupMode mode, List<String> objectiveIds) {
        this.id = id;
        this.mode = mode;
        this.objectiveIds = Collections.unmodifiableList(new ArrayList<String>(objectiveIds));
    }

    public String getId() {
        return id;
    }

    public ObjectiveGroupMode getMode() {
        return mode;
    }

    public List<String> getObjectiveIds() {
        return objectiveIds;
    }
}
