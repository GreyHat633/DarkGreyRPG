package darkgrey.rpg.client.gui;

/** Text allocation in logical pixels, measured at the same native size used for drawing. */
public final class RuntimeRowText {

    public interface Metrics {

        int width(String text);

        String trim(String text, int width);
    }

    public static final class ResourceLayout {

        public final String name, source;
        public final int sourceOffset, nameWidth;

        private ResourceLayout(String name, String source, int sourceOffset, int nameWidth) {
            this.name = name;
            this.source = source;
            this.sourceOffset = sourceOffset;
            this.nameWidth = nameWidth;
        }
    }

    private RuntimeRowText() {}

    public static String fit(String text, int width, Metrics metrics) {
        if (width <= 0 || text.isEmpty()) return "";
        if (metrics.width(text) <= width) return text;
        int dots = metrics.width("…");
        if (dots > width) return "";
        // A retained bold code also widens the ellipsis; measure the final string, including formatting.
        for (int limit = width - dots; limit >= 0; limit--) {
            String clipped = metrics.trim(text, limit) + "…";
            if (metrics.width(clipped) <= width) return clipped;
        }
        return "…";
    }

    public static ResourceLayout resource(String label, boolean reference, String source, int width, Metrics metrics) {
        width = Math.max(0, width);
        int sourceLimit = Math.min(width * 2 / 5, Math.max(0, width - 64 - 8));
        String right = fit(source, sourceLimit, metrics);
        int rightWidth = metrics.width(right);
        int nameWidth = width - (rightWidth == 0 ? 0 : rightWidth + 8);
        String suffix = reference ? " [引用]" : "";
        String name;
        if (!suffix.isEmpty() && label.endsWith(suffix) && metrics.width(label) > nameWidth) {
            int suffixWidth = metrics.width(suffix);
            name = suffixWidth <= nameWidth
                ? fit(label.substring(0, label.length() - suffix.length()), nameWidth - suffixWidth, metrics) + suffix
                : fit("[引用]", nameWidth, metrics);
        } else name = fit(label, nameWidth, metrics);
        return new ResourceLayout(name, right, width - rightWidth, nameWidth);
    }
}
