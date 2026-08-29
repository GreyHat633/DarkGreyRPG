package darkgrey.rpg.session.persistence;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Set;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

import darkgrey.rpg.session.instance.CanonicalSessionInstanceNbtCodec;
import darkgrey.rpg.session.instance.CanonicalSessionInstanceSnapshot;
import darkgrey.rpg.story.canonical.CanonicalStoryPendingContinuation;
import darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceNbtCodec;
import darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceSnapshot;

/** Strict wrapper for canonical Session world state and pending Story cursors. */
public final class CanonicalSessionWorldStateNbtCodec {

    public static final int SCHEMA_VERSION = 2;
    public static final String SESSIONS_KEY = "sessions";
    public static final String CONTINUATIONS_KEY = "continuations";
    public static final String STORIES_KEY = "stories";
    private static final int COMPOUND = 10;
    private static final int LIST = 9;
    private static final int BYTE = 1;
    private static final int LONG = 4;
    private static final int INT = 3;
    private static final int STRING = 8;

    private CanonicalSessionWorldStateNbtCodec() {}

    public static NBTTagCompound encode(List<CanonicalSessionInstanceSnapshot> sessions,
        List<CanonicalStoryPendingContinuation> continuations) {
        long next = 1L;
        if (sessions != null) for (CanonicalSessionInstanceSnapshot session : sessions)
            if (session != null && session.getTransportId() >= next) next = session.getTransportId() + 1L;
        return encode(sessions, next, continuations);
    }

    public static NBTTagCompound encode(List<CanonicalSessionInstanceSnapshot> sessions, long nextTransportId,
        List<CanonicalStoryPendingContinuation> continuations) {
        return encode(
            sessions,
            nextTransportId,
            continuations,
            Collections.<CanonicalStoryInstanceSnapshot>emptyList());
    }

    public static NBTTagCompound encode(List<CanonicalSessionInstanceSnapshot> sessions, long nextTransportId,
        List<CanonicalStoryPendingContinuation> continuations, List<CanonicalStoryInstanceSnapshot> stories) {
        if (continuations == null) throw malformed("continuations");
        if (stories == null) throw malformed("stories");
        Set<String> sessionIdentities = new HashSet<String>();
        if (sessions != null) for (CanonicalSessionInstanceSnapshot session : sessions) {
            if (session == null) throw malformed("null Session snapshot");
            if (!sessionIdentities.add(identity(session.getPlayerUuid(), session.getStoryId())))
                throw malformed("duplicate Session player/story");
        }
        List<CanonicalStoryPendingContinuation> ordered = new ArrayList<CanonicalStoryPendingContinuation>(
            continuations);
        Set<String> identities = new HashSet<String>();
        for (CanonicalStoryPendingContinuation continuation : ordered) {
            if (continuation == null
                || !identities.add(identity(continuation.getPlayerUuid(), continuation.getStoryId())))
                throw malformed("duplicate continuation player/story");
            if (sessionIdentities.contains(identity(continuation.getPlayerUuid(), continuation.getStoryId())))
                throw malformed("Session and continuation overlap player/story");
        }
        Collections.sort(ordered, new Comparator<CanonicalStoryPendingContinuation>() {

            @Override
            public int compare(CanonicalStoryPendingContinuation left, CanonicalStoryPendingContinuation right) {
                int result = left.getPlayerUuid()
                    .toString()
                    .compareTo(
                        right.getPlayerUuid()
                            .toString());
                return result == 0 ? left.getStoryId()
                    .compareTo(right.getStoryId()) : result;
            }
        });
        NBTTagCompound root = new NBTTagCompound();
        root.setInteger("schema_version", SCHEMA_VERSION);
        root.setTag(SESSIONS_KEY, CanonicalSessionInstanceNbtCodec.encode(sessions, nextTransportId));
        NBTTagList list = new NBTTagList();
        for (CanonicalStoryPendingContinuation continuation : ordered) list.appendTag(encodeContinuation(continuation));
        root.setTag(CONTINUATIONS_KEY, list);
        root.setTag(STORIES_KEY, CanonicalStoryInstanceNbtCodec.encode(stories));
        return root;
    }

    public static NBTTagCompound write(List<CanonicalSessionInstanceSnapshot> sessions, long nextTransportId,
        List<CanonicalStoryPendingContinuation> continuations) {
        return encode(sessions, nextTransportId, continuations);
    }

    public static Decoded decode(NBTTagCompound root) {
        requireType(root, "schema_version", INT);
        int schemaVersion = root.getInteger("schema_version");
        if (schemaVersion == 1)
            requireKeys(root, set("schema_version", SESSIONS_KEY, CONTINUATIONS_KEY), "world state");
        else if (schemaVersion == SCHEMA_VERSION)
            requireKeys(root, set("schema_version", SESSIONS_KEY, CONTINUATIONS_KEY, STORIES_KEY), "world state");
        else throw malformed("unsupported schema_version");
        requireType(root, SESSIONS_KEY, COMPOUND);
        requireType(root, CONTINUATIONS_KEY, LIST);
        List<CanonicalSessionInstanceSnapshot> sessions = CanonicalSessionInstanceNbtCodec
            .decode(root.getCompoundTag(SESSIONS_KEY));
        Set<String> sessionIdentities = new HashSet<String>();
        for (CanonicalSessionInstanceSnapshot session : sessions)
            sessionIdentities.add(identity(session.getPlayerUuid(), session.getStoryId()));
        NBTTagList tags = (NBTTagList) root.getTag(CONTINUATIONS_KEY);
        if (tags.tagCount() > 0 && tags.func_150303_d() != COMPOUND) throw malformed("invalid continuation list type");
        List<CanonicalStoryPendingContinuation> continuations = new ArrayList<CanonicalStoryPendingContinuation>();
        Set<String> identities = new HashSet<String>();
        for (int index = 0; index < tags.tagCount(); index++) {
            CanonicalStoryPendingContinuation continuation = decodeContinuation(tags.getCompoundTagAt(index));
            if (!identities.add(identity(continuation.getPlayerUuid(), continuation.getStoryId())))
                throw malformed("duplicate continuation player/story");
            if (sessionIdentities.contains(identity(continuation.getPlayerUuid(), continuation.getStoryId())))
                throw malformed("Session and continuation overlap player/story");
            continuations.add(continuation);
        }
        List<CanonicalStoryInstanceSnapshot> stories = schemaVersion == 1
            ? Collections.<CanonicalStoryInstanceSnapshot>emptyList()
            : decodeStories(root);
        return new Decoded(
            sessions,
            CanonicalSessionInstanceNbtCodec.nextTransportId(root.getCompoundTag(SESSIONS_KEY)),
            continuations,
            stories);
    }

    public static Decoded read(NBTTagCompound root) {
        return decode(root);
    }

    public static final class Decoded {

        private final List<CanonicalSessionInstanceSnapshot> sessions;
        private final long nextTransportId;
        private final List<CanonicalStoryPendingContinuation> continuations;
        private final List<CanonicalStoryInstanceSnapshot> stories;

        private Decoded(List<CanonicalSessionInstanceSnapshot> sessions, long nextTransportId,
            List<CanonicalStoryPendingContinuation> continuations, List<CanonicalStoryInstanceSnapshot> stories) {
            this.sessions = Collections.unmodifiableList(new ArrayList<CanonicalSessionInstanceSnapshot>(sessions));
            this.nextTransportId = nextTransportId;
            this.continuations = Collections
                .unmodifiableList(new ArrayList<CanonicalStoryPendingContinuation>(continuations));
            this.stories = Collections.unmodifiableList(new ArrayList<CanonicalStoryInstanceSnapshot>(stories));
        }

        public List<CanonicalSessionInstanceSnapshot> getSessions() {
            return sessions;
        }

        public long getNextTransportId() {
            return nextTransportId;
        }

        public List<CanonicalStoryPendingContinuation> getContinuations() {
            return continuations;
        }

        public List<CanonicalStoryInstanceSnapshot> getStories() {
            return stories;
        }
    }

    private static List<CanonicalStoryInstanceSnapshot> decodeStories(NBTTagCompound root) {
        requireType(root, STORIES_KEY, COMPOUND);
        return CanonicalStoryInstanceNbtCodec.decode(root.getCompoundTag(STORIES_KEY));
    }

    private static NBTTagCompound encodeContinuation(CanonicalStoryPendingContinuation value) {
        NBTTagCompound tag = new NBTTagCompound();
        tag.setString(
            "player_uuid",
            value.getPlayerUuid()
                .toString());
        tag.setString("story_id", value.getStoryId());
        tag.setString("aggregate_placement_id", value.getAggregatePlacementId());
        tag.setString("session_resource_id", value.getSessionResourceId());
        tag.setLong("transport_id", value.getTransportId());
        tag.setString("selected_end_port_id", value.getSelectedEndPortId());
        tag.setString("target_node_id", value.getTargetNodeId());
        tag.setString("target_port_id", value.getTargetPortId());
        NBTTagList logic = new NBTTagList();
        for (Map.Entry<String, Boolean> entry : value.getPublicLogic()
            .entrySet()) {
            NBTTagCompound item = new NBTTagCompound();
            item.setString("key", entry.getKey());
            item.setByte(
                "value",
                (byte) (entry.getValue()
                    .booleanValue() ? 1 : 0));
            logic.appendTag(item);
        }
        tag.setTag("public_logic", logic);
        return tag;
    }

    private static CanonicalStoryPendingContinuation decodeContinuation(NBTTagCompound tag) {
        requireKeys(
            tag,
            set(
                "player_uuid",
                "story_id",
                "aggregate_placement_id",
                "session_resource_id",
                "transport_id",
                "selected_end_port_id",
                "target_node_id",
                "target_port_id",
                "public_logic"),
            "continuation");
        String player = string(tag, "player_uuid");
        if (player.length() != 36) throw malformed("invalid player_uuid");
        UUID uuid;
        try {
            uuid = UUID.fromString(player);
        } catch (RuntimeException exception) {
            throw malformed("invalid player_uuid");
        }
        if (!uuid.toString()
            .equals(player.toLowerCase(Locale.ROOT))) throw malformed("invalid player_uuid");
        requireType(tag, "transport_id", LONG);
        long transport = tag.getLong("transport_id");
        if (transport <= 0) throw malformed("transport_id must be positive");
        requireType(tag, "public_logic", LIST);
        NBTTagList list = (NBTTagList) tag.getTag("public_logic");
        if (list.tagCount() > 0 && list.func_150303_d() != COMPOUND) throw malformed("invalid public_logic type");
        LinkedHashMap<String, Boolean> logic = new LinkedHashMap<String, Boolean>();
        for (int index = 0; index < list.tagCount(); index++) {
            NBTTagCompound item = list.getCompoundTagAt(index);
            requireKeys(item, set("key", "value"), "public_logic entry");
            String key = string(item, "key");
            requireType(item, "value", BYTE);
            byte value = item.getByte("value");
            if (value != 0 && value != 1) throw malformed("invalid public_logic value");
            if (logic.put(key, Boolean.valueOf(value == 1)) != null) throw malformed("duplicate public_logic key");
        }
        return new CanonicalStoryPendingContinuation(
            uuid,
            string(tag, "story_id"),
            string(tag, "aggregate_placement_id"),
            string(tag, "session_resource_id"),
            transport,
            string(tag, "selected_end_port_id"),
            string(tag, "target_node_id"),
            string(tag, "target_port_id"),
            logic);
    }

    private static String identity(UUID player, String story) {
        return player.toString() + "\u0000" + story;
    }

    private static String string(NBTTagCompound tag, String key) {
        requireType(tag, key, STRING);
        String value = tag.getString(key);
        if (value == null || value.trim()
            .isEmpty()) throw malformed("blank " + key);
        return value;
    }

    private static void requireType(NBTTagCompound tag, String key, int type) {
        if (tag == null || !tag.hasKey(key, type)) throw malformed("invalid or missing " + key);
    }

    private static void requireKeys(NBTTagCompound tag, Set<String> allowed, String label) {
        if (tag == null) throw malformed(label);
        Set<String> keys = new HashSet<String>(tag.func_150296_c());
        for (String key : allowed) if (!keys.contains(key)) throw malformed("unknown or missing " + label + " key");
        for (String key : keys) if (!allowed.contains(key)) throw malformed("unknown or missing " + label + " key");
    }

    private static Set<String> set(String... values) {
        Set<String> result = new HashSet<String>();
        for (String value : values) result.add(value);
        return result;
    }

    private static IllegalArgumentException malformed(String detail) {
        return new IllegalArgumentException("Malformed canonical Session world state NBT: " + detail);
    }
}
