package darkgrey.rpg.identity;

import java.security.SecureRandom;
import java.util.Objects;
import java.util.Set;

/** A definition's owner, kind and hidden local identity, also retained by references. */
public final class ResourceAddress {

    public enum Kind {

        ACTOR("actor"),
        ITEM("item"),
        ITEM_GROUP("item_group"),
        SESSION("session"),
        TASK("task");

        private final String token;

        Kind(String token) {
            this.token = token;
        }

        public String getToken() {
            return token;
        }

        public static Kind parse(String token) {
            for (Kind kind : values()) if (kind.token.equals(token)) return kind;
            throw new IllegalArgumentException("Unknown resource kind.");
        }
    }

    private static final SecureRandom RANDOM = new SecureRandom();
    private static final char[] HEX = "0123456789abcdef".toCharArray();
    private final StoryUid storyUid;
    private final Kind kind;
    private final String localId;

    public ResourceAddress(StoryUid storyUid, Kind kind, String localId) {
        this.storyUid = Objects.requireNonNull(storyUid, "storyUid");
        this.kind = Objects.requireNonNull(kind, "kind");
        if (!isValidLocalId(localId)) throw new IllegalArgumentException("Invalid local resource ID.");
        this.localId = localId;
    }

    public static boolean isValidLocalId(String value) {
        if (value == null || value.length() < 1
            || value.length() > 63
            || value.charAt(0) < 'a'
            || value.charAt(0) > 'z') return false;
        for (int i = 1; i < value.length(); i++) {
            char c = value.charAt(i);
            if (!(c >= 'a' && c <= 'z') && !(c >= '0' && c <= '9') && c != '_') return false;
        }
        return true;
    }

    public static ResourceAddress create(StoryUid owner, Kind kind, Set<ResourceAddress> occupied) {
        Objects.requireNonNull(occupied, "occupied");
        for (int attempt = 0; attempt < 128; attempt++) {
            byte[] bytes = new byte[16];
            RANDOM.nextBytes(bytes);
            StringBuilder local = new StringBuilder("r");
            for (byte b : bytes) local.append(HEX[(b & 255) >>> 4])
                .append(HEX[b & 15]);
            ResourceAddress address = new ResourceAddress(owner, kind, local.toString());
            if (!occupied.contains(address)) return address;
        }
        throw new IllegalStateException("Could not allocate an unused resource address.");
    }

    public StoryUid getStoryUid() {
        return storyUid;
    }

    public Kind getKind() {
        return kind;
    }

    public String getLocalId() {
        return localId;
    }

    public String relativeDefinitionPath() {
        return "stories/" + storyUid + "/resources/" + kind.token + "/r-" + localId + ".json";
    }

    /** Lossless internal graph/index key, never a Namespace ID or an author-editable field. */
    public String toKey() {
        return storyUid.getValue() + "~" + kind.token + "~" + localId;
    }

    public static ResourceAddress fromKey(String value) {
        if (value == null) throw new IllegalArgumentException("A current resource address key is required.");
        String[] parts = value.split("~", -1);
        if (parts.length != 3) throw new IllegalArgumentException("A current resource address key is required.");
        return new ResourceAddress(StoryUid.parse(parts[0]), Kind.parse(parts[1]), parts[2]);
    }

    public static boolean isKey(String value) {
        try {
            fromKey(value);
            return true;
        } catch (IllegalArgumentException exception) {
            return false;
        }
    }

    public String relativeReferencePath() {
        return "referenced_resources/" + storyUid + "/" + kind.token + "/r-" + localId + ".json";
    }

    @Override
    public boolean equals(Object other) {
        if (!(other instanceof ResourceAddress)) return false;
        ResourceAddress value = (ResourceAddress) other;
        return storyUid.equals(value.storyUid) && kind == value.kind && localId.equals(value.localId);
    }

    @Override
    public int hashCode() {
        return Objects.hash(storyUid, kind, localId);
    }
}
