package darkgrey.rpg.project.packages;

import java.util.ArrayList;
import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.TreeSet;

/** Deterministic classification between accepted package generations. */
public final class StoryPackageGenerationDelta {

    public enum Kind {
        UNCHANGED,
        UPDATED,
        REPLACED,
        ADDED,
        REMOVED
    }

    private StoryPackageGenerationDelta() {}

    public static List<Entry> between(Map<String, PackageGenerationKey> previous,
        Map<String, LoadedStoryPackage> current) {
        Map<String, PackageGenerationKey> currentKeys = new LinkedHashMap<String, PackageGenerationKey>();
        for (LoadedStoryPackage value : current.values())
            currentKeys.put(value.getPackageId(), PackageGenerationKey.from(value));
        return betweenKeys(previous, currentKeys);
    }

    public static List<Entry> betweenKeys(Map<String, PackageGenerationKey> previous,
        Map<String, PackageGenerationKey> current) {
        if (previous == null || current == null) throw new IllegalArgumentException("Generation maps are required.");
        Set<String> packageIds = new TreeSet<String>();
        packageIds.addAll(previous.keySet());
        packageIds.addAll(current.keySet());
        List<Entry> result = new ArrayList<Entry>();
        for (String packageId : packageIds) {
            PackageGenerationKey oldValue = previous.get(packageId);
            PackageGenerationKey newValue = current.get(packageId);
            Kind kind;
            if (oldValue == null) kind = Kind.ADDED;
            else if (newValue == null) kind = Kind.REMOVED;
            else if (!oldValue.getStoryId()
                .equals(newValue.getStoryId())) kind = Kind.REPLACED;
            else if (!oldValue.getContentFingerprint()
                .equals(newValue.getContentFingerprint())) kind = Kind.UPDATED;
            else kind = Kind.UNCHANGED;
            result.add(new Entry(kind, oldValue, newValue));
        }
        return Collections.unmodifiableList(result);
    }

    public static final class Entry {

        private final Kind kind;
        private final PackageGenerationKey previous;
        private final PackageGenerationKey current;

        Entry(Kind kind, PackageGenerationKey previous, PackageGenerationKey current) {
            this.kind = kind;
            this.previous = previous;
            this.current = current;
        }

        public Kind getKind() {
            return kind;
        }

        public PackageGenerationKey getPrevious() {
            return previous;
        }

        public PackageGenerationKey getCurrent() {
            return current;
        }

        public String getPackageId() {
            return current == null ? previous.getPackageId() : current.getPackageId();
        }

        public boolean retiresRuntime() {
            return kind == Kind.UPDATED || kind == Kind.REPLACED || kind == Kind.REMOVED;
        }
    }
}
