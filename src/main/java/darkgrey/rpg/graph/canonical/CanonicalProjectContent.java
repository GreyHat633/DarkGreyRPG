package darkgrey.rpg.graph.canonical;

import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.Map;

/** Immutable, detached snapshot of all canonical project resources. */
public final class CanonicalProjectContent {

    private static final CanonicalProjectContent EMPTY = new CanonicalProjectContent(
        Collections.<String, CanonicalGraphResource>emptyMap(),
        Collections.<String, CanonicalGraphResource>emptyMap(),
        Collections.<String, CanonicalGraphResource>emptyMap(),
        Collections.<String, CanonicalStoryMembership>emptyMap());

    private final Map<String, CanonicalGraphResource> stories;
    private final Map<String, CanonicalGraphResource> sessions;
    private final Map<String, CanonicalGraphResource> tasks;
    private final Map<String, CanonicalStoryMembership> memberships;

    public CanonicalProjectContent(Map<String, CanonicalGraphResource> stories,
        Map<String, CanonicalGraphResource> sessions, Map<String, CanonicalGraphResource> tasks,
        Map<String, CanonicalStoryMembership> memberships) {
        this.stories = immutableCopy(stories, "stories");
        this.sessions = immutableCopy(sessions, "sessions");
        this.tasks = immutableCopy(tasks, "tasks");
        this.memberships = immutableCopy(memberships, "memberships");
    }

    public static CanonicalProjectContent empty() {
        return EMPTY;
    }

    public Map<String, CanonicalGraphResource> getStories() {
        return stories;
    }

    public CanonicalGraphResource getStory(String id) {
        return stories.get(id);
    }

    public Map<String, CanonicalGraphResource> getSessions() {
        return sessions;
    }

    public CanonicalGraphResource getSession(String id) {
        return sessions.get(id);
    }

    public Map<String, CanonicalGraphResource> getTasks() {
        return tasks;
    }

    public CanonicalGraphResource getTask(String id) {
        return tasks.get(id);
    }

    public Map<String, CanonicalStoryMembership> getMemberships() {
        return memberships;
    }

    public CanonicalStoryMembership getMembership(String id) {
        return memberships.get(id);
    }

    public CanonicalStoryMembership getStoryMembership(String id) {
        return memberships.get(id);
    }

    public Map<String, CanonicalStoryMembership> getStoryMemberships() {
        return memberships;
    }

    private static <T> Map<String, T> immutableCopy(Map<String, T> values, String name) {
        if (values == null) throw new IllegalArgumentException(name + " cannot be null.");
        return Collections.unmodifiableMap(new LinkedHashMap<String, T>(values));
    }
}
