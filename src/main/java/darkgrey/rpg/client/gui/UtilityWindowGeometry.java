package darkgrey.rpg.client.gui;

/**
 * Pure client-side geometry and mouse interaction for a utility window.
 *
 * <p>
 * The owner supplies the current logical (scaled) screen size when it
 * restores a window. Mouse movement only changes this object; it never
 * performs persistence or any other client/server work.
 * </p>
 */
public final class UtilityWindowGeometry {

    /** Height of the title-bar hit area in logical pixels. */
    public static final int TITLE_BAR_HEIGHT = 18;
    /** Size of the corner resize hit area in logical pixels. */
    public static final int RESIZE_GRIP_SIZE = 12;

    /** Public rectangle fields make layout code cheap to consume. */
    public int x;
    public int y;
    public int width;
    public int height;

    private final int minWidth;
    private final int minHeight;
    private final int defaultWidth;
    private final int defaultHeight;

    private int screenWidth = 1;
    private int screenHeight = 1;
    private int mode;
    private boolean resizeLeft;
    private boolean resizeTop;
    private int anchorMouseX;
    private int anchorMouseY;
    private int anchorX;
    private int anchorY;
    private int anchorWidth;
    private int anchorHeight;

    /**
     * Creates a window with the requested minimum and default dimensions.
     * Defaults smaller than the minimum are raised to the minimum.
     */
    public UtilityWindowGeometry(int minWidth, int minHeight, int defaultWidth, int defaultHeight) {
        this.minWidth = positive(minWidth, 1);
        this.minHeight = positive(minHeight, 1);
        this.defaultWidth = Math.max(this.minWidth, positive(defaultWidth, this.minWidth));
        this.defaultHeight = Math.max(this.minHeight, positive(defaultHeight, this.minHeight));
        this.width = this.defaultWidth;
        this.height = this.defaultHeight;
    }

    /** Returns the configured minimum width. */
    public int minWidth() {
        return minWidth;
    }

    /** Returns the configured minimum height. */
    public int minHeight() {
        return minHeight;
    }

    /** Returns the configured default rectangle dimensions. */
    public int defaultWidth() {
        return defaultWidth;
    }

    /** Returns the configured default rectangle dimensions. */
    public int defaultHeight() {
        return defaultHeight;
    }

    /**
     * Returns a default persisted state. It is useful when a settings file
     * has no entry for this window.
     */
    public Snapshot defaultSnapshot() {
        return new Snapshot(0.5D, 0.5D, defaultWidth, defaultHeight);
    }

    /**
     * Restores normalized center position and logical dimensions for a
     * screen. Invalid saved values are ignored by {@link Snapshot#sanitized}.
     */
    public void restore(int screenWidth, int screenHeight, Snapshot saved) {
        this.screenWidth = positive(screenWidth, 1);
        this.screenHeight = positive(screenHeight, 1);
        Snapshot value = saved == null ? defaultSnapshot() : saved.sanitized(defaultSnapshot());
        width = clampDimension(value.width, minWidth, this.screenWidth);
        height = clampDimension(value.height, minHeight, this.screenHeight);
        x = roundedCenter(value.centerXRatio, this.screenWidth, width);
        y = roundedCenter(value.centerYRatio, this.screenHeight, height);
        clampPosition();
        mode = 0;
    }

    /**
     * Starts a left-button drag or resize. The caller should process modal
     * controls and inventory slots before calling this method.
     *
     * @return true when the pointer was in this window's title bar or resize
     *         grip and an interaction was started
     */
    public boolean begin(int mouseX, int mouseY, int button) {
        if (button != 0 || active()) return false;
        if (inResizeGrip(mouseX, mouseY)) {
            mode = 2;
            resizeLeft = mouseX < x + width / 2;
            resizeTop = mouseY < y + height / 2;
        } else if (inTitleBar(mouseX, mouseY)) {
            mode = 1;
        } else {
            return false;
        }
        anchorMouseX = mouseX;
        anchorMouseY = mouseY;
        anchorX = x;
        anchorY = y;
        anchorWidth = width;
        anchorHeight = height;
        return true;
    }

    /** Applies one mouse-motion sample to the active interaction. */
    public void move(int mouseX, int mouseY) {
        if (mode == 1) {
            x = anchorX + mouseX - anchorMouseX;
            y = anchorY + mouseY - anchorMouseY;
            clampPosition();
        } else if (mode == 2) {
            int dx = mouseX - anchorMouseX, dy = mouseY - anchorMouseY;
            int right = anchorX + anchorWidth, bottom = anchorY + anchorHeight;
            if (resizeLeft) {
                x = clamp(anchorX + dx, 0, right - Math.min(minWidth, anchorWidth));
                width = right - x;
            } else {
                x = anchorX;
                width = clampDimension(anchorWidth + dx, minWidth, screenWidth - anchorX);
            }
            if (resizeTop) {
                y = clamp(anchorY + dy, 0, bottom - Math.min(minHeight, anchorHeight));
                height = bottom - y;
            } else {
                y = anchorY;
                height = clampDimension(anchorHeight + dy, minHeight, screenHeight - anchorY);
            }
        }
    }

    /** Ends the current drag or resize. Persistence remains the caller's job. */
    public void end() {
        mode = 0;
    }

    /** Returns whether a drag or resize interaction is currently active. */
    public boolean active() {
        return mode != 0;
    }

    /**
     * Takes a normalized-center snapshot using the screen supplied to the
     * last restore call.
     */
    public Snapshot snapshot() {
        return snapshot(screenWidth, screenHeight);
    }

    /** Takes a normalized-center snapshot for an explicitly supplied screen. */
    public Snapshot snapshot(int currentScreenWidth, int currentScreenHeight) {
        int sw = positive(currentScreenWidth, 1);
        int sh = positive(currentScreenHeight, 1);
        return new Snapshot(((double) x + width / 2.0D) / sw, ((double) y + height / 2.0D) / sh, width, height);
    }

    /** Returns whether a pointer is in the title bar hit area. */
    public boolean inTitleBar(int mouseX, int mouseY) {
        return mouseX >= x && mouseX < x + width
            && mouseY >= y
            && mouseY < y + Math.min(TITLE_BAR_HEIGHT, height)
            && !inResizeGrip(mouseX, mouseY);
    }

    /** Returns whether a pointer is in the corner resize hit area. */
    public boolean inResizeGrip(int mouseX, int mouseY) {
        int grip = Math.min(RESIZE_GRIP_SIZE, Math.min(width, height));
        return mouseX >= x && mouseX < x + width
            && mouseY >= y
            && mouseY < y + height
            && (mouseX < x + grip || mouseX >= x + width - grip)
            && (mouseY < y + grip || mouseY >= y + height - grip);
    }

    private void clampPosition() {
        int maxX = Math.max(0, screenWidth - width);
        int maxY = Math.max(0, screenHeight - height);
        x = clamp(x, 0, maxX);
        y = clamp(y, 0, maxY);
    }

    private static int roundedCenter(double ratio, int screen, int size) {
        double safe = finite(ratio) ? clamp(ratio, 0.0D, 1.0D) : 0.5D;
        long result = Math.round(safe * screen - size / 2.0D);
        if (result < Integer.MIN_VALUE) return Integer.MIN_VALUE;
        if (result > Integer.MAX_VALUE) return Integer.MAX_VALUE;
        return (int) result;
    }

    private static int clampDimension(int value, int minimum, int screen) {
        int maximum = Math.max(1, screen);
        int lower = Math.min(minimum, maximum);
        return clamp(value, lower, maximum);
    }

    private static int positive(int value, int fallback) {
        return value > 0 ? value : fallback;
    }

    private static int clamp(int value, int lower, int upper) {
        return value < lower ? lower : (value > upper ? upper : value);
    }

    private static double clamp(double value, double lower, double upper) {
        return value < lower ? lower : (value > upper ? upper : value);
    }

    private static boolean finite(double value) {
        return !Double.isNaN(value) && !Double.isInfinite(value);
    }

    /** Immutable persisted state. */
    public static final class Snapshot {

        public final double centerXRatio;
        public final double centerYRatio;
        public final int width;
        public final int height;

        public Snapshot(double centerXRatio, double centerYRatio, int width, int height) {
            this.centerXRatio = centerXRatio;
            this.centerYRatio = centerYRatio;
            this.width = width;
            this.height = height;
        }

        private Snapshot sanitized(Snapshot fallback) {
            return new Snapshot(
                finite(centerXRatio) ? clamp(centerXRatio, 0.0D, 1.0D) : fallback.centerXRatio,
                finite(centerYRatio) ? clamp(centerYRatio, 0.0D, 1.0D) : fallback.centerYRatio,
                width > 0 ? width : fallback.width,
                height > 0 ? height : fallback.height);
        }
    }
}
