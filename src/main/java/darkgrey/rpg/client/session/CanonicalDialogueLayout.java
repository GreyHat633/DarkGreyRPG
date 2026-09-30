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
    private final int headerExtra;

    public CanonicalDialogueLayout(int width, int height) {
        this(width, height, 1, 9);
    }

    public CanonicalDialogueLayout(int width, int height, double textScale, int fontHeight) {
        headerExtra = Math.max(0, (int) Math.ceil(fontHeight * textScale) - fontHeight);
        left = Math.max(4, width / 20);
        right = width - left;
        bottom = height - Math.max(4, height / 40);
        top = Math.max(4, bottom - Math.max(86, height / 3));
        int portraitHeight = Math.max(1, bottom - 6 - bodyTop());
        portraitSize = Math.max(1, Math.min(Math.round(portraitHeight * .9f), (int) ((right - left - 16) * .22f)));
        textLeft = left + portraitSize + 16;
        textWidth = Math.max(1, right - textLeft - 8);
        choiceWidth = Math.min(360, right - left);
    }

    public int speakerWidth(boolean hasPortrait) {
        return hasPortrait ? portraitSize : Math.max(1, right - left - 106);
    }

    public int portraitLeft() {
        // Inset the portrait from the divider while preserving the body text's pagination width.
        return left + 11;
    }

    public int speakerLeft(boolean hasPortrait) {
        return hasPortrait ? portraitLeft() : left + 8;
    }

    public int bodyTop() {
        return top + 27 + headerExtra;
    }

    public int dividerTop() {
        return bodyTop() - 5;
    }

    public int speakerTop(int textHeight) {
        return top + 4 + Math.max(0, (dividerTop() - top - 4 - textHeight) / 2);
    }

    public int buttonTop() {
        return top + 4 + headerExtra / 2;
    }

    public int portraitTop() {
        return bodyTop() + (bottom - 6 - bodyTop() - portraitSize) / 2;
    }

    public int bodyBottom() {
        return Math.max(bodyTop() + 1, bottom - 13);
    }

    public boolean containsDialogue(int x, int y) {
        return x >= left && x < right && y >= top && y < bottom;
    }

}
