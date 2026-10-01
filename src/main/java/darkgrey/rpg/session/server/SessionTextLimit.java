package darkgrey.rpg.session.server;

import java.nio.charset.StandardCharsets;

/** Validate the complete display value before it becomes a persisted presentation. */
public final class SessionTextLimit {

    private SessionTextLimit() {}

    public static String require(String value, String context, int maximum) {
        int bytes = value == null ? -1 : value.getBytes(StandardCharsets.UTF_8).length;
        if (bytes < 0 || bytes > maximum) throw new ProjectionFailure(context, bytes, maximum);
        return value;
    }

    public static final class ProjectionFailure extends IllegalStateException {

        public ProjectionFailure(String context, int actual, int maximum) {
            super("Session display rejected: " + context + " bytes=" + actual + " maximum=" + maximum);
        }
    }
}
