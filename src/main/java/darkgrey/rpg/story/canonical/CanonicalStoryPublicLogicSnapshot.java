package darkgrey.rpg.story.canonical;

import java.util.Collections;
import java.util.LinkedHashMap;
import java.util.Map;

/** Ordered, detached, immutable public Logic values at a Story boundary. */
public final class CanonicalStoryPublicLogicSnapshot {

    private final Map<String, Boolean> values;

    public CanonicalStoryPublicLogicSnapshot(Map<String, Boolean> values) {
        if (values == null) throw new IllegalArgumentException("Public Logic values are required.");
        LinkedHashMap<String, Boolean> copy = new LinkedHashMap<String, Boolean>();
        for (Map.Entry<String, Boolean> entry : values.entrySet()) {
            if (entry.getKey() == null || entry.getKey()
                .trim()
                .isEmpty() || entry.getValue() == null)
                throw new IllegalArgumentException("Invalid public Logic value.");
            copy.put(entry.getKey(), entry.getValue());
        }
        this.values = Collections.unmodifiableMap(copy);
    }

    public Map<String, Boolean> getValues() {
        return values;
    }

    public Map<String, Boolean> getPublicLogicOutputs() {
        return values;
    }

    public Map<String, Boolean> getPublicLogicOutputMap() {
        return values;
    }

    public Map<String, Boolean> asMap() {
        return values;
    }
}
