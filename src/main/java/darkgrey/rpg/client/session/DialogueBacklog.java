package darkgrey.rpg.client.session;

import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;

/** Process-local reading copies. No runtime objects, media references, files or network actions. */
public final class DialogueBacklog {

    public static final int CAPACITY = 250;
    private long revision;

    public synchronized long revision() {
        return revision;
    }

    private final Map<String, LinkedHashMap<String, Entry>> contexts = new LinkedHashMap<String, LinkedHashMap<String, Entry>>();

    public static final class Entry {

        public final String identity;
        public final String speaker;
        public final String text;

        private Entry(String identity, String speaker, String text) {
            this.identity = identity;
            this.speaker = speaker;
            this.text = text;
        }
    }

    public synchronized void upsert(String context, String identity, String speaker, String shownText) {
        if (context == null || identity == null || shownText == null || shownText.isEmpty()) return;
        LinkedHashMap<String, Entry> entries = contexts.get(context);
        if (entries == null) {
            entries = new LinkedHashMap<String, Entry>();
            contexts.put(context, entries);
        }
        Entry previous = entries.get(identity);
        // Reconnection can reveal an already-read line from its start. Never lose the read suffix.
        if (previous != null && previous.text.length() >= shownText.length()) return;
        entries.put(identity, new Entry(identity, speaker == null ? "" : speaker, shownText));
        revision++;
        while (entries.size() > CAPACITY) entries.remove(
            entries.keySet()
                .iterator()
                .next());
    }

    public synchronized List<Entry> entries(String context) {
        LinkedHashMap<String, Entry> entries = contexts.get(context);
        return entries == null ? Collections.<Entry>emptyList()
            : Collections.unmodifiableList(new ArrayList<Entry>(entries.values()));
    }

    public synchronized void clear(String context) {
        if (contexts.remove(context) != null) revision++;
    }
}
