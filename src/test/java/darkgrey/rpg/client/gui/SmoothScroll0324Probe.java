package darkgrey.rpg.client.gui;

/** Animation and hit-testing share the same integer pixel origin. */
public final class SmoothScroll0324Probe {

    public static void main(String[] args) {
        SmoothScroll scroll = new SmoothScroll();
        scroll.bounds(185);
        scroll.wheel(-120);
        require(scroll.target() == 20 && scroll.position() == 0, "wheel changes target only");
        double previous = 0;
        for (int frame = 0; frame < 60; frame++) {
            scroll.advance(1.0 / 60);
            require(scroll.position() >= previous && scroll.position() <= 20, "monotonic no overshoot");
            for (int y = 0; y < 75; y++) {
                int row = scroll.rowAt(y);
                int renderedTop = row * 20 - scroll.pixelOffset();
                require(y >= renderedTop && y < renderedTop + 20, "click matches rendered row during motion");
            }
            previous = scroll.position();
        }
        require(scroll.position() == 20, "settles promptly");
        for (int i = 0; i < 100; i++) scroll.wheel(-1);
        require(scroll.target() == 185, "bottom clamp");
        scroll.advance(0.016);
        SmoothScroll restored = new SmoothScroll();
        restored.bounds(185);
        restored.restore(scroll);
        require(restored.position() == scroll.position() && restored.target() == scroll.target(), "refresh state");
        restored.bounds(5);
        require(restored.position() <= 5 && restored.target() == 5, "resize clamps both states");
        for (int i = 0; i < 100; i++) restored.wheel(1);
        require(restored.target() == 0, "top clamp");
        System.out.println("NOMINATOR_SMOOTH_SCROLL_MOTION_HIT_CLAMP_REFRESH=PASS");
    }

    private static void require(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
