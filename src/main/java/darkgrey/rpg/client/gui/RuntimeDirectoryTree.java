package darkgrey.rpg.client.gui;

import java.util.ArrayList;
import java.util.LinkedHashMap;
import java.util.LinkedHashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;

/** Presentation state is independent of selection, network refreshes and geometry. */
public final class RuntimeDirectoryTree {

    private final Set<String> expanded = new LinkedHashSet<String>();

    public static final class Entry {

        public final String key, name, containerKey, containerName;
        public final boolean grouped;

        public Entry(String key, String name, String containerKey, String containerName, boolean grouped) {
            this.key = key;
            this.name = name;
            this.containerKey = containerKey;
            this.containerName = containerName;
            this.grouped = grouped;
        }
    }

    public static final class Row {

        public final String key, name;
        public final boolean folder, open;
        public final int depth;

        Row(String key, String name, boolean folder, boolean open, int depth) {
            this.key = key;
            this.name = name;
            this.folder = folder;
            this.open = open;
            this.depth = depth;
        }
    }

    public void toggle(String key) {
        if (!expanded.remove(key)) expanded.add(key);
    }

    public boolean isExpanded(String key) {
        return expanded.contains(key);
    }

    public void restore(RuntimeDirectoryTree old) {
        expanded.clear();
        expanded.addAll(old.expanded);
    }

    public List<Row> rows(List<Entry> entries, String query) {
        return rows(entries, query, false);
    }

    public List<Row> rows(List<Entry> entries, String query, boolean reveal) {
        String needle = query == null ? ""
            : query.trim()
                .toLowerCase(java.util.Locale.ROOT);
        Map<String, List<Entry>> containers = new LinkedHashMap<String, List<Entry>>();
        for (Entry entry : entries) {
            List<Entry> members = containers.get(entry.containerKey);
            if (members == null) {
                members = new ArrayList<Entry>();
                containers.put(entry.containerKey, members);
            }
            members.add(entry);
        }
        List<Row> result = new ArrayList<Row>();
        for (List<Entry> members : containers.values()) {
            Entry first = members.get(0);
            if (!first.grouped) {
                for (Entry member : members) if (needle.isEmpty() || member.name.toLowerCase(java.util.Locale.ROOT)
                    .contains(needle)) result.add(new Row(member.key, member.name, false, false, 0));
                continue;
            }
            List<Entry> matches = new ArrayList<Entry>();
            for (Entry member : members) if (needle.isEmpty() || first.containerName.toLowerCase(java.util.Locale.ROOT)
                .contains(needle)
                || member.name.toLowerCase(java.util.Locale.ROOT)
                    .contains(needle))
                matches.add(member);
            if (matches.isEmpty()) continue;
            boolean open = reveal || !needle.isEmpty() || expanded.contains(first.containerKey);
            result.add(new Row(first.containerKey, first.containerName, true, open, 0));
            if (open) for (Entry member : matches) result.add(new Row(member.key, member.name, false, false, 1));
        }
        return result;
    }
}
