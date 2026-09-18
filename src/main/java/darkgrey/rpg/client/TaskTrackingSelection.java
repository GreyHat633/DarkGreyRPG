package darkgrey.rpg.client;

import java.util.Collections;
import java.util.LinkedHashSet;
import java.util.Set;

/** Selection only; reconciliation cannot complete or mutate a Task. */
public final class TaskTrackingSelection {

    public static final int LIMIT = 3;
    private final LinkedHashSet<String> selected = new LinkedHashSet<String>();

    public Set<String> selected() {
        return Collections.unmodifiableSet(new LinkedHashSet<String>(selected));
    }

    public boolean track(String id) {
        if (id == null || id.isEmpty()) return false;
        if (selected.contains(id)) return true;
        return selected.size() < LIMIT && selected.add(id);
    }

    public void untrack(String id) {
        selected.remove(id);
    }

    public void reconcile(Set<String> active, Iterable<String> received) {
        selected.retainAll(active);
        for (String id : received) if (active.contains(id)) track(id);
    }
}
