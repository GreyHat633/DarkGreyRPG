package darkgrey.rpg.graph.canonical;

import java.util.ArrayList;
import java.util.Collections;
import java.util.List;

/** Immutable membership lists for actors, sessions, and tasks. */
public final class CanonicalStoryMembershipSet {

    private final List<String> actors;
    private final List<String> sessions;
    private final List<String> tasks;

    public CanonicalStoryMembershipSet() {
        this(Collections.<String>emptyList(), Collections.<String>emptyList(), Collections.<String>emptyList());
    }

    public CanonicalStoryMembershipSet(List<String> actors, List<String> sessions, List<String> tasks) {
        this.actors = copy(actors, "actors");
        this.sessions = copy(sessions, "sessions");
        this.tasks = copy(tasks, "tasks");
    }

    public List<String> getActors() {
        return actors;
    }

    public List<String> getSessions() {
        return sessions;
    }

    public List<String> getTasks() {
        return tasks;
    }

    public List<String> getActorIds() {
        return actors;
    }

    public List<String> getSessionIds() {
        return sessions;
    }

    public List<String> getTaskIds() {
        return tasks;
    }

    private static List<String> copy(List<String> values, String name) {
        if (values == null) throw new IllegalArgumentException(name + " cannot be null.");
        return Collections.unmodifiableList(new ArrayList<String>(values));
    }
}
