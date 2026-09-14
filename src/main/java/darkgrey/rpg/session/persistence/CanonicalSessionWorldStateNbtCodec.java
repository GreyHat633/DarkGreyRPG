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

    public static final int SCHEMA_VERSION = 6;
    public static final String SESSIONS_KEY = "sessions";
    public static final String CONTINUATIONS_KEY = "continuations";
    public static final String STORIES_KEY = "stories";
    public static final String TERMINAL_ROUTES_KEY = "terminal_routes";
    public static final String PENDING_TERMINAL_ROUTES_KEY = "pending_terminal_routes";
    public static final String START_OBSERVATIONS_KEY = "start_observations";
    public static final String TERMINAL_ROUTE_TARGETS_KEY = "terminal_route_targets";
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
        return encode(
            sessions,
            nextTransportId,
            continuations,
            stories,
            Collections.<String>emptyList(),
            Collections.<String, Boolean>emptyMap());
    }

    public static NBTTagCompound encode(List<CanonicalSessionInstanceSnapshot> sessions, long nextTransportId,
        List<CanonicalStoryPendingContinuation> continuations, List<CanonicalStoryInstanceSnapshot> stories,
        java.util.Collection<String> terminalRoutes) {
        return encode(
            sessions,
            nextTransportId,
            continuations,
            stories,
            terminalRoutes,
            Collections.<String, Boolean>emptyMap(),
            Collections.<String>emptyList());
    }

    public static NBTTagCompound encode(List<CanonicalSessionInstanceSnapshot> sessions, long nextTransportId,
        List<CanonicalStoryPendingContinuation> continuations, List<CanonicalStoryInstanceSnapshot> stories,
        java.util.Collection<String> terminalRoutes, Map<String, Boolean> startObservations) {
        return encode(
            sessions,
            nextTransportId,
            continuations,
            stories,
            terminalRoutes,
            startObservations,
            Collections.<String>emptyList(),
            Collections.<String, String>emptyMap());
    }

    public static NBTTagCompound encode(List<CanonicalSessionInstanceSnapshot> sessions, long nextTransportId,
        List<CanonicalStoryPendingContinuation> continuations, List<CanonicalStoryInstanceSnapshot> stories,
        java.util.Collection<String> terminalRoutes, Map<String, Boolean> startObservations,
        java.util.Collection<String> pendingTerminalRoutes) {
        return encode(
            sessions,
            nextTransportId,
            continuations,
            stories,
            terminalRoutes,
            startObservations,
            pendingTerminalRoutes,
            Collections.<String, String>emptyMap());
    }

    public static NBTTagCompound encode(List<CanonicalSessionInstanceSnapshot> sessions, long nextTransportId,
        List<CanonicalStoryPendingContinuation> continuations, List<CanonicalStoryInstanceSnapshot> stories,
        java.util.Collection<String> terminalRoutes, Map<String, Boolean> startObservations,
        java.util.Collection<String> pendingTerminalRoutes, Map<String, String> terminalRouteTargets) {
        if (continuations == null) throw malformed("continuations");
        if (stories == null) throw malformed("stories");
        if (terminalRoutes == null) throw malformed("terminal routes");
        if (startObservations == null) throw malformed("start observations");
        if (pendingTerminalRoutes == null) throw malformed("pending terminal routes");
        if (terminalRouteTargets == null) throw malformed("terminal route targets");
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
        NBTTagList routes = new NBTTagList();
        java.util.Set<String> routeSet = new java.util.TreeSet<String>();
        for (String route : terminalRoutes) if (route == null || route.trim()
            .isEmpty() || !routeSet.add(route)) throw malformed("invalid terminal route");
        for (String route : routeSet) {
            NBTTagCompound item = new NBTTagCompound();
            item.setString("key", route);
            routes.appendTag(item);
        }
        root.setTag(TERMINAL_ROUTES_KEY, routes);
        NBTTagList observations = new NBTTagList();
        java.util.Set<String> observationKeys = new java.util.TreeSet<String>();
        for (Map.Entry<String, Boolean> entry : startObservations.entrySet()) if (entry.getKey() == null
            || entry.getKey()
                .trim()
                .isEmpty()
            || entry.getValue() == null
            || !observationKeys.add(entry.getKey())) throw malformed("invalid start observation");
        for (String key : observationKeys) {
            NBTTagCompound item = new NBTTagCompound();
            item.setString("key", key);
            item.setByte(
                "value",
                (byte) (startObservations.get(key)
                    .booleanValue() ? 1 : 0));
            observations.appendTag(item);
        }
        root.setTag(START_OBSERVATIONS_KEY, observations);
        java.util.Set<String> pendingSet = new java.util.HashSet<String>();
        for (String route : pendingTerminalRoutes) if (route == null || route.trim()
            .isEmpty() || !pendingSet.add(route) || routeSet.contains(route))
            throw malformed("invalid pending terminal route");
        root.setTag(PENDING_TERMINAL_ROUTES_KEY, routeList(pendingSet));
        NBTTagList targetList = new NBTTagList();
        java.util.Set<String> targetKeys = new java.util.TreeSet<String>();
        for (Map.Entry<String, String> entry : terminalRouteTargets.entrySet()) {
            if (entry.getKey() == null || entry.getKey()
                .trim()
                .isEmpty()
                || entry.getValue() == null
                || entry.getValue()
                    .trim()
                    .isEmpty()
                || !targetKeys.add(entry.getKey())) throw malformed("invalid terminal route target");
            NBTTagCompound item = new NBTTagCompound();
            item.setString("source_key", entry.getKey());
            item.setString("target_key", entry.getValue());
            targetList.appendTag(item);
        }
        root.setTag(TERMINAL_ROUTE_TARGETS_KEY, targetList);
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
        else if (schemaVersion == 2)
            requireKeys(root, set("schema_version", SESSIONS_KEY, CONTINUATIONS_KEY, STORIES_KEY), "world state");
        else if (schemaVersion == 3) requireKeys(
            root,
            set("schema_version", SESSIONS_KEY, CONTINUATIONS_KEY, STORIES_KEY, TERMINAL_ROUTES_KEY),
            "world state");
        else if (schemaVersion == 4) requireKeys(
            root,
            set(
                "schema_version",
                SESSIONS_KEY,
                CONTINUATIONS_KEY,
                STORIES_KEY,
                TERMINAL_ROUTES_KEY,
                START_OBSERVATIONS_KEY),
            "world state");
        else if (schemaVersion == 5) requireKeys(
            root,
            set(
                "schema_version",
                SESSIONS_KEY,
                CONTINUATIONS_KEY,
                STORIES_KEY,
                TERMINAL_ROUTES_KEY,
                START_OBSERVATIONS_KEY,
                PENDING_TERMINAL_ROUTES_KEY),
            "world state");
        else if (schemaVersion == SCHEMA_VERSION) requireKeys(
            root,
            set(
                "schema_version",
                SESSIONS_KEY,
                CONTINUATIONS_KEY,
                STORIES_KEY,
                TERMINAL_ROUTES_KEY,
                START_OBSERVATIONS_KEY,
                PENDING_TERMINAL_ROUTES_KEY,
                TERMINAL_ROUTE_TARGETS_KEY),
            "world state");
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
        List<String> terminalRoutes = schemaVersion < 3 ? Collections.<String>emptyList() : decodeTerminalRoutes(root);
        Map<String, Boolean> startObservations = schemaVersion < 4 ? Collections.<String, Boolean>emptyMap()
            : decodeStartObservations(root);
        List<String> pendingRoutes = schemaVersion < 5 ? Collections.<String>emptyList()
            : decodeTerminalRoutes(root, PENDING_TERMINAL_ROUTES_KEY);
        Map<String, String> routeTargets = schemaVersion < 6 ? Collections.<String, String>emptyMap()
            : decodeTerminalRouteTargets(root);
        return new Decoded(
            sessions,
            CanonicalSessionInstanceNbtCodec.nextTransportId(root.getCompoundTag(SESSIONS_KEY)),
            continuations,
            stories,
            terminalRoutes,
            startObservations,
            pendingRoutes,
            routeTargets);
    }

    public static Decoded read(NBTTagCompound root) {
        return decode(root);
    }

    public static final class Decoded {

        private final List<CanonicalSessionInstanceSnapshot> sessions;
        private final long nextTransportId;
        private final List<CanonicalStoryPendingContinuation> continuations;
        private final List<CanonicalStoryInstanceSnapshot> stories;
        private final List<String> terminalRoutes;
        private final Map<String, Boolean> startObservations;
        private final List<String> pendingTerminalRoutes;
        private final Map<String, String> terminalRouteTargets;

        private Decoded(List<CanonicalSessionInstanceSnapshot> sessions, long nextTransportId,
            List<CanonicalStoryPendingContinuation> continuations, List<CanonicalStoryInstanceSnapshot> stories,
            List<String> terminalRoutes, Map<String, Boolean> startObservations) {
            this(
                sessions,
                nextTransportId,
                continuations,
                stories,
                terminalRoutes,
                startObservations,
                Collections.<String>emptyList());
        }

        private Decoded(List<CanonicalSessionInstanceSnapshot> sessions, long nextTransportId,
            List<CanonicalStoryPendingContinuation> continuations, List<CanonicalStoryInstanceSnapshot> stories,
            List<String> terminalRoutes, Map<String, Boolean> startObservations, List<String> pendingTerminalRoutes) {
            this(
                sessions,
                nextTransportId,
                continuations,
                stories,
                terminalRoutes,
                startObservations,
                pendingTerminalRoutes,
                Collections.<String, String>emptyMap());
        }

        private Decoded(List<CanonicalSessionInstanceSnapshot> sessions, long nextTransportId,
            List<CanonicalStoryPendingContinuation> continuations, List<CanonicalStoryInstanceSnapshot> stories,
            List<String> terminalRoutes, Map<String, Boolean> startObservations, List<String> pendingTerminalRoutes,
            Map<String, String> terminalRouteTargets) {
            this.sessions = Collections.unmodifiableList(new ArrayList<CanonicalSessionInstanceSnapshot>(sessions));
            this.nextTransportId = nextTransportId;
            this.continuations = Collections
                .unmodifiableList(new ArrayList<CanonicalStoryPendingContinuation>(continuations));
            this.stories = Collections.unmodifiableList(new ArrayList<CanonicalStoryInstanceSnapshot>(stories));
            this.terminalRoutes = Collections.unmodifiableList(new ArrayList<String>(terminalRoutes));
            this.startObservations = Collections.unmodifiableMap(new LinkedHashMap<String, Boolean>(startObservations));
            this.pendingTerminalRoutes = Collections.unmodifiableList(new ArrayList<String>(pendingTerminalRoutes));
            this.terminalRouteTargets = Collections
                .unmodifiableMap(new LinkedHashMap<String, String>(terminalRouteTargets));
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

        public List<String> getTerminalRoutes() {
            return terminalRoutes;
        }

        public Map<String, Boolean> getStartObservations() {
            return startObservations;
        }

        public List<String> getPendingTerminalRoutes() {
            return pendingTerminalRoutes;
        }

        public Map<String, String> getTerminalRouteTargets() {
            return terminalRouteTargets;
        }
    }

    private static List<CanonicalStoryInstanceSnapshot> decodeStories(NBTTagCompound root) {
        requireType(root, STORIES_KEY, COMPOUND);
        return CanonicalStoryInstanceNbtCodec.decode(root.getCompoundTag(STORIES_KEY));
    }

    private static List<String> decodeTerminalRoutes(NBTTagCompound root) {
        return decodeTerminalRoutes(root, TERMINAL_ROUTES_KEY);
    }

    private static List<String> decodeTerminalRoutes(NBTTagCompound root, String routeKey) {
        requireType(root, routeKey, LIST);
        NBTTagList list = (NBTTagList) root.getTag(routeKey);
        if (list.tagCount() > 0 && list.func_150303_d() != COMPOUND) throw malformed("invalid terminal routes type");
        List<String> result = new ArrayList<String>();
        for (int index = 0; index < list.tagCount(); index++) {
            NBTTagCompound item = list.getCompoundTagAt(index);
            requireKeys(item, set("key"), "terminal route");
            String key = string(item, "key");
            if (!result.isEmpty() && result.contains(key)) throw malformed("duplicate terminal route");
            result.add(key);
        }
        return result;
    }

    private static Map<String, String> decodeTerminalRouteTargets(NBTTagCompound root) {
        requireType(root, TERMINAL_ROUTE_TARGETS_KEY, LIST);
        NBTTagList list = (NBTTagList) root.getTag(TERMINAL_ROUTE_TARGETS_KEY);
        if (list.tagCount() > 0 && list.func_150303_d() != COMPOUND)
            throw malformed("invalid terminal route targets type");
        Map<String, String> result = new LinkedHashMap<String, String>();
        for (int index = 0; index < list.tagCount(); index++) {
            NBTTagCompound item = list.getCompoundTagAt(index);
            requireKeys(item, set("source_key", "target_key"), "terminal route target");
            String source = string(item, "source_key");
            String target = string(item, "target_key");
            if (result.put(source, target) != null) throw malformed("duplicate terminal route target");
        }
        return result;
    }

    private static NBTTagList routeList(java.util.Collection<String> values) {
        NBTTagList result = new NBTTagList();
        java.util.Set<String> ordered = new java.util.TreeSet<String>();
        for (String value : values) if (value == null || value.trim()
            .isEmpty() || !ordered.add(value)) throw malformed("invalid terminal route");
        for (String value : ordered) {
            NBTTagCompound item = new NBTTagCompound();
            item.setString("key", value);
            result.appendTag(item);
        }
        return result;
    }

    private static Map<String, Boolean> decodeStartObservations(NBTTagCompound root) {
        requireType(root, START_OBSERVATIONS_KEY, LIST);
        NBTTagList list = (NBTTagList) root.getTag(START_OBSERVATIONS_KEY);
        if (list.tagCount() > 0 && list.func_150303_d() != COMPOUND) throw malformed("invalid start observations type");
        Map<String, Boolean> result = new LinkedHashMap<String, Boolean>();
        for (int index = 0; index < list.tagCount(); index++) {
            NBTTagCompound item = list.getCompoundTagAt(index);
            requireKeys(item, set("key", "value"), "start observation");
            String key = string(item, "key");
            requireType(item, "value", BYTE);
            byte value = item.getByte("value");
            if (value != 0 && value != 1 || result.put(key, Boolean.valueOf(value == 1)) != null)
                throw malformed("invalid or duplicate start observation");
        }
        return result;
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
