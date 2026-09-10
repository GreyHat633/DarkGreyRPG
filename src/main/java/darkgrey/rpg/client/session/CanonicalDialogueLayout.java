package darkgrey.rpg.client.session;

/** Geometry in Minecraft scaled GUI coordinates, shared by drawing and hit testing. */
public final class CanonicalDialogueLayout {

    public final int left;
    public final int right;
    public final int top;
    public final int bottom;
    public final int portraitSize;
    public final int textLeft;
    public final int textWidth;
    public final int choiceWidth;
    public final int choicesPerPage;

    public CanonicalDialogueLayout(int width, int height) {
        left = Math.max(4, width / 20);
        right = width - left;
        bottom = height - Math.max(4, height / 40);
        top = bottom - Math.max(48, height / 4);
        portraitSize = Math.min(64, bottom - top - 16);
        textLeft = left + portraitSize + 16;
        textWidth = Math.max(1, right - textLeft - 8);
        choiceWidth = Math.min(360, right - left);
        choicesPerPage = Math.max(1, Math.min(5, (top - 36) / 24));
    }

    public boolean containsDialogue(int x, int y) {
        return x >= left && x < right && y >= top && y < bottom;
    }

    public int choiceTop(int visibleCount) {
        return Math.max(4, (top - 28 - visibleCount * 24) / 2);
    }
}
