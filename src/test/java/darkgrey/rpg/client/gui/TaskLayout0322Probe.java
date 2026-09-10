package darkgrey.rpg.client.gui;

/** Checks readable panel bounds and non-overlapping hit regions across real GUI scales. */
public final class TaskLayout0322Probe {

    public static void main(String[] args) {
        for (int[] resolution : new int[][] { { 320, 240 }, { 427, 240 }, { 550, 370 }, { 960, 540 } }) {
            for (int preferred : new int[] { 180, 260, 300 }) {
                CanonicalTaskLayout layout = new CanonicalTaskLayout(resolution[0], resolution[1], preferred);
                require(
                    layout.panelLeft >= 0 && layout.panelTop >= 0
                        && layout.panelRight <= resolution[0]
                        && layout.panelBottom <= resolution[1],
                    "screen bounds");
                require(
                    layout.listBottom > layout.listTop + 30 && layout.detailBottom > layout.detailTop + 30,
                    "usable regions");
                require(
                    layout.listRight <= layout.detailLeft || layout.listBottom <= layout.detailTop,
                    "non-overlapping panes");
                require(layout.containsList(layout.listLeft, layout.listTop), "list hit region");
                require(!layout.containsDetail(layout.listLeft, layout.listTop), "list cannot click detail");
            }
        }
        require(new CanonicalTaskLayout(320, 240).stacked, "320x240 compact layout");
        require(!new CanonicalTaskLayout(550, 370, 180).stacked, "sparse normal menu keeps columns");
        System.out.println("TASK_LAYOUT_NORMAL_COMPACT_HIT_REGIONS=PASS");
    }

    private static void require(boolean value, String message) {
        if (!value) throw new AssertionError(message);
    }
}
