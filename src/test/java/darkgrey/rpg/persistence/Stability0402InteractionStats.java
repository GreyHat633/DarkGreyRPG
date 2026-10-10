package darkgrey.rpg.persistence;

import java.util.LinkedHashMap;
import java.util.Map;

/** Same-client monotonic request encoding to correctly correlated GUI application. Test coremod only. */
public final class Stability0402InteractionStats {

    private static final Map<Long, Long> requests = new LinkedHashMap<>();
    private static final StringBuilder samples = new StringBuilder();

    private Stability0402InteractionStats() {}

    public static synchronized void sent(int kind, long request) {
        if (kind != 1 || System.getProperty("dgr0402.clientRoot") == null) return;
        if (!requests.containsKey(request)) requests.put(request, System.nanoTime());
        while (requests.size() > 128) requests.remove(
            requests.keySet()
                .iterator()
                .next());
    }

    public static synchronized void applied(Object gui, long request, Object data) {
        Long started = requests.get(request);
        if (started == null) return;
        try {
            if ((Boolean) Stability0402Reflect.read(gui, "loading", "loading")
                || (Long) Stability0402Reflect.read(gui, "request", "request") != request) return;
            requests.remove(request);
            long now = System.nanoTime();
            String error = (String) Stability0402Reflect
                .method(data.getClass(), "getString", "func_74779_i", String.class)
                .invoke(data, "error");
            samples.append(request)
                .append(',')
                .append(started)
                .append(',')
                .append(now)
                .append(',')
                .append(now - started)
                .append(',')
                .append(error.isEmpty())
                .append('\n');
        } catch (Exception failure) {
            throw new IllegalStateException("Client interaction sampler failed", failure);
        }
    }

    public static synchronized String drain() {
        String result = samples.toString();
        samples.setLength(0);
        return result;
    }
}
