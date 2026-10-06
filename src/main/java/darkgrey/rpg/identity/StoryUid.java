package darkgrey.rpg.identity;

import java.security.SecureRandom;
import java.util.Objects;
import java.util.Set;

/** Immutable Story identity. Names, paths and container membership do not participate. */
public final class StoryUid {

    public static final String ALPHABET = "23456789ABCDEFGHJKLMNPQRSTUVWXYZ";
    private static final SecureRandom RANDOM = new SecureRandom();
    private final String value;

    private StoryUid(String value) {
        this.value = value;
    }

    public static boolean isValid(String value) {
        if (value == null || value.length() != 22 || !value.startsWith("ST-")) return false;
        for (int i = 3; i < value.length(); i++) {
            if (i == 7 || i == 12 || i == 17) {
                if (value.charAt(i) != '-') return false;
            } else if (ALPHABET.indexOf(value.charAt(i)) < 0) return false;
        }
        return true;
    }

    public static StoryUid parse(String value) {
        if (!isValid(value)) throw new IllegalArgumentException("Invalid Story UID; expected ST-XXXX-XXXX-XXXX-XXXX.");
        return new StoryUid(value);
    }

    /** Allocates an identity for a newly created Story, never modifies an existing one. */
    public static StoryUid create(Set<StoryUid> visibleIdentities) {
        Objects.requireNonNull(visibleIdentities, "visibleIdentities");
        for (int attempt = 0; attempt < 128; attempt++) {
            StringBuilder text = new StringBuilder("ST-");
            for (int i = 0; i < 16; i++) {
                if (i > 0 && i % 4 == 0) text.append('-');
                text.append(ALPHABET.charAt(RANDOM.nextInt(ALPHABET.length())));
            }
            StoryUid uid = new StoryUid(text.toString());
            if (!visibleIdentities.contains(uid)) return uid;
        }
        throw new IllegalStateException("Could not allocate an unused Story UID.");
    }

    public String getValue() {
        return value;
    }

    @Override
    public String toString() {
        return value;
    }

    @Override
    public boolean equals(Object other) {
        return other instanceof StoryUid && value.equals(((StoryUid) other).value);
    }

    @Override
    public int hashCode() {
        return value.hashCode();
    }
}
