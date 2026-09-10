package darkgrey.rpg.story.canonical.runtime;

import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.project.ProjectSnapshot;

/** Immutable Start-trigger index for one immutable project snapshot. */
public final class CanonicalStoryTriggerIndex {

    private final Map<String, CanonicalStoryStartConfiguration> configurations;

    private CanonicalStoryTriggerIndex(Map<String, CanonicalStoryStartConfiguration> configurations) {
        this.configurations = Collections
            .unmodifiableMap(new LinkedHashMap<String, CanonicalStoryStartConfiguration>(configurations));
    }

    public static CanonicalStoryTriggerIndex build(ProjectSnapshot project) {
        if (project == null) throw new IllegalArgumentException("Project snapshot is required.");
        Map<String, CanonicalStoryStartConfiguration> configurations = new LinkedHashMap<String, CanonicalStoryStartConfiguration>();
        for (Map.Entry<String, CanonicalGraphResource> entry : project.getCanonicalStories()
            .entrySet()) {
            CanonicalGraphResource resource = entry.getValue();
            if (resource == null || !entry.getKey()
                .equals(resource.getId()))
                throw new IllegalArgumentException("Canonical Story trigger index contains an invalid resource entry.");
            configurations.put(entry.getKey(), CanonicalStoryStartConfiguration.parse(resource));
        }
        return new CanonicalStoryTriggerIndex(configurations);
    }

    public List<Match> matchActor(String actorId) {
        return matchActor(actorId, null);
    }

    public List<Match> matchActor(String actorId, java.util.Set<String> eligibleStoryIds) {
        if (actorId == null || actorId.trim()
            .isEmpty()) throw new IllegalArgumentException("Actor ID is required.");
        List<Match> result = new ArrayList<Match>();
        for (Map.Entry<String, CanonicalStoryStartConfiguration> entry : configurations.entrySet()) {
            if (eligibleStoryIds != null && !eligibleStoryIds.contains(entry.getKey())) continue;
            CanonicalStoryStartConfiguration.Trigger trigger = entry.getValue()
                .findActor(actorId);
            if (trigger != null) result.add(new Match(entry.getKey(), trigger.getPortId()));
        }
        return Collections.unmodifiableList(result);
    }

    public List<Match> matchRegion(int dimension, double x, double y, double z) {
        return matchRegion(dimension, x, y, z, null);
    }

    public List<Match> matchRegion(int dimension, double x, double y, double z,
        java.util.Set<String> eligibleStoryIds) {
        if (Double.isNaN(x) || Double
            .isInfinite(x) || Double.isNaN(y) || Double.isInfinite(y) || Double.isNaN(z) || Double.isInfinite(z))
            throw new IllegalArgumentException("Finite region coordinates are required.");
        List<Match> result = new ArrayList<Match>();
        for (Map.Entry<String, CanonicalStoryStartConfiguration> entry : configurations.entrySet()) {
            if (eligibleStoryIds != null && !eligibleStoryIds.contains(entry.getKey())) continue;
            CanonicalStoryStartConfiguration.Trigger trigger = entry.getValue()
                .findRegion(dimension, x, y, z);
            if (trigger != null) result.add(new Match(entry.getKey(), trigger.getPortId()));
        }
        return Collections.unmodifiableList(result);
    }

    public static final class Match {

        private final String storyId;
        private final String portId;

        public Match(String storyId, String portId) {
            if (storyId == null || storyId.trim()
                .isEmpty()
                || portId == null
                || portId.trim()
                    .isEmpty())
                throw new IllegalArgumentException("Story trigger match identity is required.");
            this.storyId = storyId;
            this.portId = portId;
        }

        public String getStoryId() {
            return storyId;
        }

        public String getPortId() {
            return portId;
        }

        @Override
        public boolean equals(Object value) {
            if (this == value) return true;
            if (!(value instanceof Match)) return false;
            Match other = (Match) value;
            return storyId.equals(other.storyId) && portId.equals(other.portId);
        }

        @Override
        public int hashCode() {
            return 31 * storyId.hashCode() + portId.hashCode();
        }

        @Override
        public String toString() {
            return storyId + ":" + portId;
        }
    }
}
