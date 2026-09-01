package darkgrey.rpg.session.instance;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.HashSet;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Set;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

import darkgrey.rpg.session.runtime.CanonicalSessionSnapshot;
import darkgrey.rpg.session.runtime.CanonicalSessionStatus;

/** Strict schema-version-1 NBT codec for detached Session instance snapshots. */
public final class CanonicalSessionInstanceNbtCodec {

    public static final int SCHEMA_VERSION = 1;
    private static final int COMPOUND = 10;
    private static final int LIST = 9;
    private static final int BYTE = 1;
    private static final int INT = 3;
    private static final int LONG = 4;
    private static final int STRING = 8;

    private CanonicalSessionInstanceNbtCodec() {}

    public static NBTTagCompound encode(List<CanonicalSessionInstanceSnapshot> snapshots) {
        if (snapshots == null) throw malformed("snapshots");
        long max = 0L;
        for (CanonicalSessionInstanceSnapshot snapshot : snapshots) {
            if (snapshot == null) throw malformed("null snapshot");
            if (snapshot.getTransportId() > max) max = snapshot.getTransportId();
        }
        if (max == Long.MAX_VALUE) throw malformed("cannot choose a greater next_transport_id");
        long next = max + 1L;
        return encode(snapshots, next);
    }

    public static NBTTagCompound encode(List<CanonicalSessionInstanceSnapshot> snapshots, long nextTransportId) {
        if (snapshots == null) throw malformed("snapshots");
        if (nextTransportId <= 0) throw malformed("next_transport_id must be positive");
        Set<String> identities = new HashSet<String>();
        for (CanonicalSessionInstanceSnapshot snapshot : snapshots) {
            if (snapshot == null) throw malformed("null snapshot");
            if (snapshot.getTransportId() >= nextTransportId)
                throw malformed("transport_id is not below next_transport_id");
            String identity = snapshot.getPlayerUuid()
                .toString() + "\u0000"
                + snapshot.getStoryId();
            if (!identities.add(identity)) throw malformed("duplicate player/story instance");
        }
        NBTTagCompound root = new NBTTagCompound();
        root.setInteger("schema_version", SCHEMA_VERSION);
        root.setLong("next_transport_id", nextTransportId);
        NBTTagList instances = new NBTTagList();
        List<CanonicalSessionInstanceSnapshot> ordered = new ArrayList<CanonicalSessionInstanceSnapshot>(snapshots);
        Collections.sort(ordered, new Comparator<CanonicalSessionInstanceSnapshot>() {

            @Override
            public int compare(CanonicalSessionInstanceSnapshot a, CanonicalSessionInstanceSnapshot b) {
                int result = a.getPlayerUuid()
                    .toString()
                    .compareTo(
                        b.getPlayerUuid()
                            .toString());
                if (result == 0) result = a.getStoryId()
                    .compareTo(b.getStoryId());
                return result;
            }
        });
        for (CanonicalSessionInstanceSnapshot snapshot : ordered) instances.appendTag(encodeInstance(snapshot));
        root.setTag("instances", instances);
        return root;
    }

    public static NBTTagCompound write(List<CanonicalSessionInstanceSnapshot> snapshots) {
        return encode(snapshots);
    }

    public static List<CanonicalSessionInstanceSnapshot> decode(NBTTagCompound root) {
        requireKeys(root, set("schema_version", "next_transport_id", "instances"), "root");
        requireType(root, "schema_version", INT);
        requireType(root, "next_transport_id", LONG);
        requireType(root, "instances", LIST);
        if (root.getInteger("schema_version") != SCHEMA_VERSION) throw malformed("unsupported schema_version");
        if (root.getLong("next_transport_id") <= 0) throw malformed("next_transport_id must be positive");
        NBTTagList tags = (NBTTagList) root.getTag("instances");
        requireListType(tags, "instances");
        List<CanonicalSessionInstanceSnapshot> result = new ArrayList<CanonicalSessionInstanceSnapshot>();
        Set<String> identities = new HashSet<String>();
        for (int index = 0; index < tags.tagCount(); index++) {
            NBTTagCompound tag = tags.getCompoundTagAt(index);
            CanonicalSessionInstanceSnapshot snapshot = decodeInstance(tag);
            if (snapshot.getTransportId() >= root.getLong("next_transport_id"))
                throw malformed("transport_id is not below next_transport_id");
            String identity = snapshot.getPlayerUuid()
                .toString() + "\u0000"
                + snapshot.getStoryId();
            if (!identities.add(identity)) throw malformed("duplicate player/story instance");
            result.add(snapshot);
        }
        return Collections.unmodifiableList(result);
    }

    public static long nextTransportId(NBTTagCompound root) {
        requireKeys(root, set("schema_version", "next_transport_id", "instances"), "root");
        requireType(root, "schema_version", INT);
        requireType(root, "next_transport_id", LONG);
        requireType(root, "instances", LIST);
        if (root.getInteger("schema_version") != SCHEMA_VERSION) throw malformed("unsupported schema_version");
        long next = root.getLong("next_transport_id");
        if (next <= 0) throw malformed("next_transport_id must be positive");
        return next;
    }

    public static List<CanonicalSessionInstanceSnapshot> read(NBTTagCompound root) {
        return decode(root);
    }

    private static NBTTagCompound encodeInstance(CanonicalSessionInstanceSnapshot instance) {
        NBTTagCompound tag = new NBTTagCompound();
        tag.setString(
            "player_uuid",
            instance.getPlayerUuid()
                .toString());
        tag.setString("story_id", instance.getStoryId());
        tag.setString("aggregate_placement_id", instance.getAggregatePlacementId());
        tag.setString("session_resource_id", instance.getSessionResourceId());
        tag.setLong("transport_id", instance.getTransportId());
        CanonicalSessionSnapshot runtime = instance.getRuntimeSnapshot();
        tag.setString("current_node_id", runtime.getCurrentNodeId());
        tag.setString(
            "status",
            runtime.getStatus()
                .name());
        tag.setTag("selected_option_ids", strings(runtime.getSelectedOptionIds()));
        tag.setTag("internal_logic_values", booleans(runtime.getInternalLogicValues()));
        if (runtime.getFinalEndPortId() != null) tag.setString("final_end_port_id", runtime.getFinalEndPortId());
        tag.setTag("public_logic_outputs", booleans(runtime.getPublicLogicOutputs()));
        tag.setBoolean("activation_logic", runtime.getActivationLogic());
        tag.setTag("latest_choice_selections", strings(runtime.getLatestChoiceSelections()));
        tag.setTag("selected_choice_node_ids", strings(runtime.getSelectedChoiceNodeIds()));
        tag.setTag("external_logic_inputs", booleans(runtime.getExternalLogicInputs()));
        tag.setTag("executed_flow_judgment_node_ids", strings(runtime.getExecutedFlowJudgmentNodeIds()));
        tag.setBoolean("waiting_condition", runtime.isWaitingCondition());
        if (runtime.isWaitingCondition()) tag.setBoolean(
            "waiting_condition_value",
            runtime.getWaitingConditionValue()
                .booleanValue());
        return tag;
    }

    private static CanonicalSessionInstanceSnapshot decodeInstance(NBTTagCompound tag) {
        requireKeys(
            tag,
            set(
                "player_uuid",
                "story_id",
                "aggregate_placement_id",
                "session_resource_id",
                "transport_id",
                "current_node_id",
                "status",
                "selected_option_ids",
                "internal_logic_values",
                "public_logic_outputs",
                "activation_logic",
                "latest_choice_selections",
                "selected_choice_node_ids"),
            "instance",
            "final_end_port_id",
            "external_logic_inputs",
            "executed_flow_judgment_node_ids",
            "waiting_condition",
            "waiting_condition_value");
        String player = string(tag, "player_uuid");
        UUID uuid;
        if (player.length() != 36) throw malformed("invalid player_uuid");
        try {
            uuid = UUID.fromString(player);
        } catch (RuntimeException exception) {
            throw malformed("invalid player_uuid");
        }
        if (!uuid.toString()
            .equals(player.toLowerCase(Locale.ROOT))) throw malformed("invalid player_uuid");
        String story = string(tag, "story_id");
        String placement = string(tag, "aggregate_placement_id");
        String resource = string(tag, "session_resource_id");
        requireType(tag, "transport_id", LONG);
        long transport = tag.getLong("transport_id");
        if (transport <= 0) throw malformed("transport_id must be positive");
        String current = string(tag, "current_node_id");
        String statusText = string(tag, "status");
        CanonicalSessionStatus status;
        try {
            status = CanonicalSessionStatus.valueOf(statusText);
        } catch (RuntimeException exception) {
            throw malformed("invalid status");
        }
        List<String> options = decodeStrings(tag, "selected_option_ids");
        Map<String, Boolean> internal = decodeBooleans(tag, "internal_logic_values");
        String end = null;
        if (tag.hasKey("final_end_port_id")) end = string(tag, "final_end_port_id");
        Map<String, Boolean> publicLogic = decodeBooleans(tag, "public_logic_outputs");
        requireType(tag, "activation_logic", BYTE);
        boolean activation = tag.getByte("activation_logic") == 1;
        if (tag.getByte("activation_logic") != 0 && tag.getByte("activation_logic") != 1)
            throw malformed("activation_logic must be boolean");
        Map<String, String> latest = decodeStringMap(tag, "latest_choice_selections");
        List<String> choiceNodes = decodeStrings(tag, "selected_choice_node_ids");
        Map<String, Boolean> externalInputs = tag.hasKey("external_logic_inputs")
            ? decodeBooleans(tag, "external_logic_inputs")
            : Collections.<String, Boolean>emptyMap();
        List<String> executedFlowJudgments = tag.hasKey("executed_flow_judgment_node_ids")
            ? decodeStrings(tag, "executed_flow_judgment_node_ids")
            : Collections.<String>emptyList();
        boolean waiting = false;
        if (tag.hasKey("waiting_condition")) {
            requireType(tag, "waiting_condition", BYTE);
            byte value = tag.getByte("waiting_condition");
            if (value != 0 && value != 1) throw malformed("waiting_condition must be boolean");
            waiting = value == 1;
        }
        Boolean waitingValue = null;
        if (tag.hasKey("waiting_condition_value")) {
            requireType(tag, "waiting_condition_value", BYTE);
            byte value = tag.getByte("waiting_condition_value");
            if (value != 0 && value != 1) throw malformed("waiting_condition_value must be boolean");
            waitingValue = Boolean.valueOf(value == 1);
        }
        if (waiting != (waitingValue != null)) throw malformed("incomplete Condition wait state");
        if (status == CanonicalSessionStatus.COMPLETED && end == null) throw malformed("completed end is required");
        if (status != CanonicalSessionStatus.COMPLETED && end != null) throw malformed("only completed has end");
        CanonicalSessionSnapshot runtime = new CanonicalSessionSnapshot(
            resource,
            current,
            status,
            options,
            internal,
            end,
            publicLogic,
            activation,
            latest,
            choiceNodes,
            externalInputs,
            waiting,
            waitingValue,
            executedFlowJudgments);
        return new CanonicalSessionInstanceSnapshot(uuid, story, placement, resource, transport, runtime);
    }

    private static NBTTagList strings(List<String> values) {
        NBTTagList list = new NBTTagList();
        for (String value : values) {
            NBTTagCompound item = new NBTTagCompound();
            item.setString("value", value);
            list.appendTag(item);
        }
        return list;
    }

    private static NBTTagList strings(Map<String, String> values) {
        NBTTagList list = new NBTTagList();
        List<String> keys = new ArrayList<String>(values.keySet());
        Collections.sort(keys);
        for (String key : keys) {
            NBTTagCompound item = new NBTTagCompound();
            item.setString("key", key);
            item.setString("value", values.get(key));
            list.appendTag(item);
        }
        return list;
    }

    private static NBTTagList booleans(Map<String, Boolean> values) {
        NBTTagList list = new NBTTagList();
        List<String> keys = new ArrayList<String>(values.keySet());
        Collections.sort(keys);
        for (String key : keys) {
            NBTTagCompound item = new NBTTagCompound();
            item.setString("key", key);
            item.setBoolean(
                "value",
                values.get(key)
                    .booleanValue());
            list.appendTag(item);
        }
        return list;
    }

    private static List<String> decodeStrings(NBTTagCompound tag, String key) {
        requireType(tag, key, LIST);
        NBTTagList list = (NBTTagList) tag.getTag(key);
        requireListType(list, key);
        List<String> result = new ArrayList<String>();
        for (int i = 0; i < list.tagCount(); i++) {
            NBTTagCompound item = list.getCompoundTagAt(i);
            requireKeys(item, set("value"), key + " entry");
            result.add(string(item, "value"));
        }
        return result;
    }

    private static Map<String, String> decodeStringMap(NBTTagCompound tag, String key) {
        requireType(tag, key, LIST);
        NBTTagList list = (NBTTagList) tag.getTag(key);
        requireListType(list, key);
        java.util.LinkedHashMap<String, String> result = new java.util.LinkedHashMap<String, String>();
        for (int i = 0; i < list.tagCount(); i++) {
            NBTTagCompound item = list.getCompoundTagAt(i);
            requireKeys(item, set("key", "value"), key + " entry");
            String k = string(item, "key");
            if (result.put(k, string(item, "value")) != null) throw malformed("duplicate " + key + " key");
        }
        return result;
    }

    private static Map<String, Boolean> decodeBooleans(NBTTagCompound tag, String key) {
        requireType(tag, key, LIST);
        NBTTagList list = (NBTTagList) tag.getTag(key);
        requireListType(list, key);
        java.util.LinkedHashMap<String, Boolean> result = new java.util.LinkedHashMap<String, Boolean>();
        for (int i = 0; i < list.tagCount(); i++) {
            NBTTagCompound item = list.getCompoundTagAt(i);
            requireKeys(item, set("key", "value"), key + " entry");
            String k = string(item, "key");
            requireType(item, "value", BYTE);
            byte b = item.getByte("value");
            if (b != 0 && b != 1) throw malformed("invalid boolean");
            if (result.put(k, Boolean.valueOf(b == 1)) != null) throw malformed("duplicate " + key + " key");
        }
        return result;
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

    private static void requireListType(NBTTagList list, String key) {
        if (list == null || (list.tagCount() > 0 && list.func_150303_d() != COMPOUND))
            throw malformed("invalid " + key + " entry type");
    }

    private static void requireKeys(NBTTagCompound tag, Set<String> allowed, String label, String... optional) {
        if (tag == null) throw malformed(label);
        Set<String> keys = new HashSet<String>(tag.func_150296_c());
        for (String key : allowed) if (!keys.contains(key)) throw malformed("unknown or missing " + label + " key");
        Set<String> accepted = new HashSet<String>(allowed);
        for (String key : optional) accepted.add(key);
        for (String key : keys) if (!accepted.contains(key)) throw malformed("unknown or missing " + label + " key");
    }

    private static Set<String> set(String... values) {
        Set<String> result = new HashSet<String>();
        for (String value : values) result.add(value);
        return result;
    }

    private static IllegalArgumentException malformed(String detail) {
        return new IllegalArgumentException("Malformed Session NBT: " + detail);
    }
}
