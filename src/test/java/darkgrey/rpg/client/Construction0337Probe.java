package darkgrey.rpg.client;

import java.io.File;
import java.nio.file.Files;
import java.util.Arrays;
import java.util.Collections;
import java.util.LinkedHashSet;
import java.util.Set;

import darkgrey.rpg.client.gui.SmoothScroll;
import darkgrey.rpg.client.gui.UtilityWindowSettings;
import darkgrey.rpg.client.session.PlayerUiPreferences;

/** Regression vectors for selection, local preferences, whole-row budgets and animated hits. */
public final class Construction0337Probe {

    public static void main(String[] args) throws Exception {
        TaskTrackingSelection selection = new TaskTrackingSelection();
        Set<String> active = new LinkedHashSet<String>(Collections.singleton("old:1"));
        selection.reconcile(active, Collections.<String>emptyList(), true);
        selection.reconcile(active, Collections.singleton("old:1"), true);
        check(
            selection.selected()
                .isEmpty(),
            "initial old task cannot become new through replay");
        active.addAll(Arrays.asList("a:1", "b:1", "c:1", "d:1"));
        selection.reconcile(active, Arrays.asList("a:1", "b:1", "c:1", "d:1"), true);
        check(
            selection.selected()
                .equals(new LinkedHashSet<String>(Arrays.asList("a:1", "b:1", "c:1"))),
            "ordered batch, limit, no eviction");
        selection.untrack("b:1");
        selection.reconcile(active, Arrays.asList("b:1", "d:1"), true);
        check(
            selection.selected()
                .size() == 2
                && !selection.selected()
                    .contains("b:1"),
            "cancel/full events consumed");
        active.add("disabled:1");
        selection.reconcile(active, Collections.singleton("disabled:1"), false);
        selection.reconcile(active, Collections.singleton("disabled:1"), true);
        check(
            selection.selected()
                .size() == 2,
            "disabled event consumed, switch does not fill");
        check(selection.track("disabled:1"), "manual works while auto off");
        active.remove("a:1");
        selection.reconcile(active, Collections.<String>emptyList(), false);
        check(
            selection.selected()
                .size() == 2,
            "completion only removes");
        active.add("a:2");
        selection.reconcile(active, Collections.singleton("a:2"), true);
        check(
            selection.selected()
                .contains("a:2"),
            "new activation accepted");
        TaskTrackingSelection other = new TaskTrackingSelection();
        other.track("b:1");
        other.reconcile(active, Collections.<String>emptyList(), false);
        check(
            other.selected()
                .contains("b:1")
                && other.selected()
                    .size() == 1,
            "context manual restore");
        File preferences = new File(args[0], "task-preferences.properties");
        preferences.getParentFile()
            .mkdirs();
        try {
            new UtilityWindowSettings(preferences).loadPlayerPreferences();
            check(
                PlayerUiPreferences.trackNewTasks()
                    && PlayerUiPreferences.trackerSide() == PlayerUiPreferences.Side.RIGHT
                    && PlayerUiPreferences.notificationSide() == PlayerUiPreferences.Side.RIGHT,
                "defaults");
            for (PlayerUiPreferences.Side tracker : PlayerUiPreferences.Side.values())
                for (PlayerUiPreferences.Side notification : PlayerUiPreferences.Side.values()) {
                    PlayerUiPreferences.setTrackNewTasks(false);
                    PlayerUiPreferences.setTrackerSide(tracker);
                    PlayerUiPreferences.setNotificationSide(notification);
                    new UtilityWindowSettings(preferences).savePlayerPreferences();
                    PlayerUiPreferences.reset();
                    new UtilityWindowSettings(preferences).loadPlayerPreferences();
                    check(
                        !PlayerUiPreferences.trackNewTasks() && PlayerUiPreferences.trackerSide() == tracker
                            && PlayerUiPreferences.notificationSide() == notification,
                        "independent side persistence");
                }
            check(PlayerUiPreferences.parseSide("bad") == PlayerUiPreferences.Side.RIGHT, "malformed side");
            PlayerUiPreferences.reset();
            check(
                PlayerUiPreferences.trackNewTasks() && selection.selected()
                    .size() == 3,
                "reset only preferences");
        } finally {
            Files.deleteIfExists(preferences.toPath());
            PlayerUiPreferences.reset();
        }
        check(
            TaskTrackerBodyLayout.visibleRows(new int[] { 11, 11, 22, 11 }, 35, 11) == 2,
            "whole icon and marker budget");
        check(TaskTrackerBodyLayout.visibleRows(new int[] { 11 }, 35, 11) == 1, "short task natural height");
        check(TaskTrackerBodyLayout.visibleRows(new int[] { 22 }, 16, 11) == 0, "marker keeps task header");
        for (int hz : new int[] { 30, 60, 144 }) for (int rowHeight : new int[] { 20, 24, 28 }) {
            SmoothScroll motion = new SmoothScroll();
            motion.bounds(301);
            motion.wheel(-120, rowHeight * 2);
            for (int frame = 0; frame < hz; frame++) {
                motion.advance(1.0 / hz);
                for (int y = 0; y < 90; y++) {
                    int row = motion.rowAt(y, rowHeight), top = row * rowHeight - motion.pixelOffset();
                    check(y >= top && y < top + rowHeight, "animated hit matches actual row");
                }
            }
            check(motion.position() == rowHeight * 2, "time based settle");
            motion.wheel(120, rowHeight);
            motion.advance(.016);
            check(motion.position() < rowHeight * 2, "reverse from current");
            motion.bounds(3);
            check(motion.position() <= 3 && motion.target() <= 3, "resize clamps both");
        }
        System.out.println("CONSTRUCTION_0337_SELECTION_PREFERENCES_BUDGET_SCROLL=PASS");
    }

    private static void check(boolean value, String label) {
        if (!value) throw new AssertionError(label);
    }
}
