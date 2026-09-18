package darkgrey.rpg.client;

import java.util.Arrays;
import java.util.Collections;
import java.util.LinkedHashSet;

public final class TaskTracking0332Probe {

    public static void main(String[] args) {
        TaskTrackingSelection selection = new TaskTrackingSelection();
        LinkedHashSet<String> active = new LinkedHashSet<String>(Arrays.asList("a:1", "b:1", "c:1", "d:1"));
        selection.reconcile(active, Collections.<String>emptyList());
        check(
            selection.selected()
                .isEmpty(),
            "initial is not received");
        selection.reconcile(active, Arrays.asList("a:1", "b:1", "c:1", "d:1"));
        check(
            selection.selected()
                .size() == 3
                && !selection.selected()
                    .contains("d:1"),
            "new task never evicts");
        check(!selection.track("d:1"), "fourth rejected");
        selection.untrack("b:1");
        selection.reconcile(active, Collections.<String>emptyList());
        check(
            !selection.selected()
                .contains("b:1"),
            "cancel does not bounce back");
        active.remove("a:1");
        selection.reconcile(active, Collections.<String>emptyList());
        check(
            selection.selected()
                .size() == 1,
            "no old task autofill");
        active.add("a:2");
        selection.reconcile(active, Collections.singleton("a:2"));
        check(
            selection.selected()
                .contains("a:2")
                && !selection.selected()
                    .contains("a:1"),
            "run identity");
        System.out.println("TASK_TRACKING_0332=PASS");
    }

    private static void check(boolean condition, String label) {
        if (!condition) throw new AssertionError(label);
    }
}
