package darkgrey.rpg.media;

import java.util.List;
import java.util.Set;
import java.util.function.Function;

/** Package-local single-file admission; backoff never owns the active slot. */
final class MediaDownloadSelection {

    interface State {

        boolean ready();

        boolean busy();

        long retryAt();
    }

    static String next(List<String> refs, Set<String> visible, boolean preload, long now,
        Function<String, ? extends State> states) {
        for (String ref : refs) {
            State state = states.apply(ref);
            if (state != null && !state.ready() && state.busy()) return null;
        }
        String background = null;
        for (String ref : refs) {
            State state = states.apply(ref);
            if (state != null && (state.ready() || now < state.retryAt())) continue;
            if (visible.contains(ref)) return ref;
            if (preload && background == null) background = ref;
        }
        return background;
    }
}
