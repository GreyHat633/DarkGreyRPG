package darkgrey.rpg.client.gui;

/** One camera transform for rendering, clipping, zoom anchoring and hit testing. */
public final class RuntimeGraphViewport {

    public final int left, top, width, height;
    public final double zoom, panX, panY;

    public RuntimeGraphViewport(int left, int top, int width, int height, double zoom, double panX, double panY) {
        this.left = left;
        this.top = top;
        this.width = Math.max(0, width);
        this.height = Math.max(0, height);
        this.zoom = zoom;
        this.panX = panX;
        this.panY = panY;
    }

    public boolean contains(int x, int y) {
        return x >= left && x < left + width && y >= top && y < top + height;
    }

    public double modelX(double x) {
        return (x - left - panX) / zoom;
    }

    public double modelY(double y) {
        return (y - top - panY) / zoom;
    }

    public double screenX(double x) {
        return left + panX + x * zoom;
    }

    public double screenY(double y) {
        return top + panY + y * zoom;
    }

    public boolean intersects(double x, double y, double w, double h) {
        return screenX(x) < left + width && screenX(x + w) > left && screenY(y) < top + height && screenY(y + h) > top;
    }

    public RuntimeGraphViewport zoomAt(int x, int y, double factor) {
        double next = Math.max(.05, Math.min(3, zoom * factor));
        return new RuntimeGraphViewport(
            left,
            top,
            width,
            height,
            next,
            x - left - modelX(x) * next,
            y - top - modelY(y) * next);
    }
}
