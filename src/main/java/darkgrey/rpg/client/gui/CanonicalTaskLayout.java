package darkgrey.rpg.client.gui;

/** Resolution-safe geometry for the canonical Task journal. */
final class CanonicalTaskLayout {

    final int panelLeft;
    final int panelTop;
    final int panelRight;
    final int panelBottom;
    final int listLeft;
    final int listTop;
    final int listRight;
    final int listBottom;
    final int detailLeft;
    final int detailTop;
    final int detailRight;
    final int detailBottom;
    final boolean stacked;

    CanonicalTaskLayout(int screenWidth, int screenHeight) {
        this(screenWidth, screenHeight, 260);
    }

    CanonicalTaskLayout(int screenWidth, int screenHeight, int preferredHeight) {
        int panelWidth = Math.max(1, Math.min(620, screenWidth - 24));
        int panelHeight = Math.max(1, Math.min(preferredHeight, screenHeight - 24));
        panelLeft = Math.max(8, (screenWidth - panelWidth) / 2);
        panelTop = Math.max(8, (screenHeight - panelHeight) / 2);
        panelRight = panelLeft + panelWidth;
        panelBottom = panelTop + panelHeight;
        stacked = panelWidth < 470 || screenHeight < 280;

        int contentTop = Math.min(panelBottom - 1, panelTop + 28);
        int contentBottom = Math.max(contentTop, panelBottom - 20);
        if (stacked) {
            int listHeight = Math.max(36, (contentBottom - contentTop) / 3);
            listLeft = panelLeft + 8;
            listTop = contentTop + 4;
            listRight = panelRight - 8;
            listBottom = Math.min(contentBottom, listTop + listHeight);
            detailLeft = listLeft;
            detailTop = Math.min(contentBottom, listBottom + 8);
            detailRight = listRight;
            detailBottom = contentBottom;
        } else {
            int split = panelLeft + panelWidth / 3;
            listLeft = panelLeft + 8;
            listTop = contentTop + 4;
            listRight = split - 4;
            listBottom = contentBottom;
            detailLeft = split + 4;
            detailTop = listTop;
            detailRight = panelRight - 8;
            detailBottom = contentBottom;
        }
    }

    boolean containsList(int x, int y) {
        return x >= listLeft && x < listRight && y >= listTop && y < listBottom;
    }

    boolean containsDetail(int x, int y) {
        return x >= detailLeft && x < detailRight && y >= detailTop && y < detailBottom;
    }
}
