package darkgrey.rpg.project.packages;

import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Set;

/** One physical installed container, including disabled, conflicting and broken files. */
public final class StoryPackageInventoryEntry {

    public enum State {
        ENABLED,
        DISABLED,
        CONFLICT,
        ERROR
    }

    private final String sourceName;
    private final String displayName;
    private final Set<String> storyUids;
    private final boolean userEnabled;
    private final List<String> errors;
    private final List<String> conflicts;
    private final java.util.Map<String, StoryPackageMemberInfo> members;
    private final darkgrey.rpg.graph.canonical.CanonicalStoryLogicGraph graph;

    public StoryPackageInventoryEntry(String sourceName, String displayName, Set<String> storyUids, boolean userEnabled,
        List<String> errors, List<String> conflicts) {
        this(
            sourceName,
            displayName,
            storyUids,
            userEnabled,
            errors,
            conflicts,
            Collections.<String, StoryPackageMemberInfo>emptyMap(),
            new darkgrey.rpg.graph.canonical.CanonicalStoryLogicGraph(
                2,
                Collections.<darkgrey.rpg.graph.canonical.CanonicalStoryLogicConnection>emptyList()));
    }

    StoryPackageInventoryEntry(String sourceName, String displayName, Set<String> storyUids, boolean userEnabled,
        List<String> errors, List<String> conflicts, java.util.Map<String, StoryPackageMemberInfo> members,
        darkgrey.rpg.graph.canonical.CanonicalStoryLogicGraph graph) {
        this.sourceName = sourceName;
        this.displayName = displayName;
        this.storyUids = Collections.unmodifiableSet(new LinkedHashSet<String>(storyUids));
        this.userEnabled = userEnabled;
        this.errors = Collections.unmodifiableList(new ArrayList<String>(errors));
        this.conflicts = Collections.unmodifiableList(new ArrayList<String>(conflicts));
        this.members = Collections
            .unmodifiableMap(new java.util.LinkedHashMap<String, StoryPackageMemberInfo>(members));
        this.graph = graph;
    }

    public java.util.Map<String, StoryPackageMemberInfo> getMembers() {
        return members;
    }

    public darkgrey.rpg.graph.canonical.CanonicalStoryLogicGraph getGraph() {
        return graph;
    }

    public String getSourceName() {
        return sourceName;
    }

    public String getDisplayName() {
        return displayName;
    }

    public Set<String> getStoryUids() {
        return storyUids;
    }

    public boolean isUserEnabled() {
        return userEnabled;
    }

    public List<String> getErrors() {
        return errors;
    }

    public List<String> getConflicts() {
        return conflicts;
    }

    public State getState() {
        return !errors.isEmpty() ? State.ERROR
            : !conflicts.isEmpty() ? State.CONFLICT : userEnabled ? State.ENABLED : State.DISABLED;
    }

    public boolean isValid() {
        return errors.isEmpty() && conflicts.isEmpty();
    }

    public boolean allowsNewStarts() {
        return isValid() && userEnabled;
    }
}
