package darkgrey.rpg.client.gui;

/** Short frame-rate-independent easing in logical pixels, with a shared render/hit offset. */
public final class SmoothScroll {

    private double position;
    private double target;
    private double maximum;

    public double position() {
        return position;
    }

    public double target() {
        return target;
    }

    public void bounds(double maximum) {
        this.maximum = Math.max(0, maximum);
        position = clamp(position);
        target = clamp(target);
    }

    public void wheel(int delta) {
        if (delta != 0) target = clamp(target + (delta < 0 ? 20 : -20));
    }

    public void jump(double pixels) {
        target = clamp(pixels);
        position = target;
    }

    public void restore(SmoothScroll old) {
        position = clamp(old.position);
        target = clamp(old.target);
    }

    public void advance(double seconds) {
        if (seconds <= 0) return;
        position += (target - position) * (1 - Math.exp(-24 * Math.min(seconds, 0.1)));
        if (Math.abs(target - position) < 0.05) position = target;
        position = clamp(position);
    }

    public int pixelOffset() {
        return (int) Math.floor(position);
    }

    public int rowAt(int viewportY) {
        return (viewportY + pixelOffset()) / 20;
    }

    private double clamp(double value) {
        return Math.max(0, Math.min(maximum, value));
    }
}
