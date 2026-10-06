package darkgrey.rpg.client;

import java.util.Collections;
import java.util.LinkedHashSet;
import java.util.Set;

/** Selection only; reconciliation cannot complete or mutate a Task. */
public final class TaskTrackingSelection {

    public static final int LIMIT = 3;
    private final LinkedHashSet<String> selected = new LinkedHashSet<String>();
    private final Set<String> observed = new LinkedHashSet<String>();

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
        reconcile(active, received, true);
    }

    public void reconcile(Set<String> active, Iterable<String> received, boolean automatic) {
        selected.retainAll(active);
        // Consume identities even while disabled/full. Initial snapshots also establish the baseline.
        for (String id : received) if (observed.add(id) && automatic && active.contains(id)) track(id);
        observed.addAll(active);
    }
}
