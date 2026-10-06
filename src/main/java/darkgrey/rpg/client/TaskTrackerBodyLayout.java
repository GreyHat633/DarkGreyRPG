package darkgrey.rpg.client;

/** Whole-row budgeting; every selected task keeps its own header outside this budget. */
public final class TaskTrackerBodyLayout {

    private TaskTrackerBodyLayout() {}

    public static int visibleRows(int[] heights, int budget, int markerHeight) {
        int total = 0;
        for (int height : heights) total += height;
        if (total <= budget) return heights.length;
        int used = 0, count = 0;
        for (int height : heights) {
            if (used + height + markerHeight > budget) break;
            used += height;
            count++;
        }
        return count;
    }
}
