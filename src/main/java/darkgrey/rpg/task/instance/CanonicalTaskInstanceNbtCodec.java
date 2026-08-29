package darkgrey.rpg.task.instance;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

import darkgrey.rpg.task.runtime.CanonicalTaskObjectiveStatus;
import darkgrey.rpg.task.runtime.CanonicalTaskSnapshot;
import darkgrey.rpg.task.runtime.CanonicalTaskStatus;

/** Strict, deterministic schema-version-1 NBT codec for TaskInstance snapshots. */
public final class CanonicalTaskInstanceNbtCodec {

    public static final int SCHEMA_VERSION = 1;
    private static final int BYTE = 1, INT = 3, LONG = 4, STRING = 8, LIST = 9, COMPOUND = 10;

    private CanonicalTaskInstanceNbtCodec() {}

    public static NBTTagCompound encode(List<CanonicalTaskInstanceSnapshot> snapshots) {
        if (snapshots == null) throw malformed("snapshots");
        return encodeInternal(snapshots);
    }

    public static NBTTagCompound write(List<CanonicalTaskInstanceSnapshot> snapshots) {
        return encode(snapshots);
    }

    private static NBTTagCompound encodeInternal(List<CanonicalTaskInstanceSnapshot> snapshots) {
        Set<String> identities = new HashSet<String>();
        List<CanonicalTaskInstanceSnapshot> ordered = new ArrayList<CanonicalTaskInstanceSnapshot>(snapshots);
        for (CanonicalTaskInstanceSnapshot snapshot : ordered) {
            if (snapshot == null || snapshot.getStatus() == CanonicalTaskInstanceStatus.NOT_STARTED)
                throw malformed("live snapshot status");
            if (!identities.add(identity(snapshot))) throw malformed("duplicate Task identity");
        }
        Collections.sort(ordered, new Comparator<CanonicalTaskInstanceSnapshot>() {

            @Override
            public int compare(CanonicalTaskInstanceSnapshot a, CanonicalTaskInstanceSnapshot b) {
                int c = a.getPlayerUuid()
                    .toString()
                    .compareTo(
                        b.getPlayerUuid()
                            .toString());
                if (c == 0) c = a.getStoryInstanceId()
                    .compareTo(b.getStoryInstanceId());
                return c == 0 ? a.getTaskNodePlacementId()
                    .compareTo(b.getTaskNodePlacementId()) : c;
            }
        });
        NBTTagCompound root = new NBTTagCompound();
        root.setInteger("schema_version", SCHEMA_VERSION);
        NBTTagList list = new NBTTagList();
        for (CanonicalTaskInstanceSnapshot snapshot : ordered) {
            try {
                list.appendTag(encodeInstance(snapshot));
            } catch (RuntimeException exception) {
                throw malformed(exception.getMessage() == null ? "invalid snapshot" : exception.getMessage());
            }
        }
        root.setTag("instances", list);
        return root;
    }

    public static List<CanonicalTaskInstanceSnapshot> decode(NBTTagCompound root) {
        requireKeys(root, set("schema_version", "instances"), "root");
        requireType(root, "schema_version", INT);
        requireType(root, "instances", LIST);
        if (root.getInteger("schema_version") != SCHEMA_VERSION) throw malformed("unsupported schema_version");
        NBTTagList list = (NBTTagList) root.getTag("instances");
        requireListType(list, "instances");
        List<CanonicalTaskInstanceSnapshot> result = new ArrayList<CanonicalTaskInstanceSnapshot>();
        Set<String> identities = new HashSet<String>();
        for (int i = 0; i < list.tagCount(); i++) {
            CanonicalTaskInstanceSnapshot snapshot = decodeInstance(list.getCompoundTagAt(i));
            if (!identities.add(identity(snapshot))) throw malformed("duplicate Task identity");
            result.add(snapshot);
        }
        return Collections.unmodifiableList(result);
    }

    public static List<CanonicalTaskInstanceSnapshot> read(NBTTagCompound root) {
        return decode(root);
    }

    private static NBTTagCompound encodeInstance(CanonicalTaskInstanceSnapshot instance) {
        NBTTagCompound tag = new NBTTagCompound();
        tag.setString(
            "player_uuid",
            instance.getPlayerUuid()
                .toString());
        tag.setString("story_instance_id", instance.getStoryInstanceId());
        tag.setString("task_node_placement_id", instance.getTaskNodePlacementId());
        tag.setString("task_resource_id", instance.getTaskResourceId());
        tag.setString(
            "status",
            instance.getStatus()
                .name());
        tag.setLong("activation_time", instance.getActivationTime());
        if (instance.getSettlementTime() != null) tag.setLong(
            "settlement_time",
            instance.getSettlementTime()
                .longValue());
        CanonicalTaskSnapshot runtime = instance.getRuntimeSnapshot();
        tag.setString("resource_fingerprint", runtime.getResourceFingerprint());
        tag.setString(
            "runtime_status",
            runtime.getStatus()
                .name());
        tag.setTag("progress", integers(runtime.getProgress()));
        tag.setTag("objective_statuses", statuses(runtime.getObjectiveStatuses()));
        tag.setTag("internal_logic_values", booleans(runtime.getInternalLogicValues()));
        tag.setTag("public_logic_outputs", booleans(runtime.getPublicLogicOutputs()));
        tag.setBoolean("activation_logic", runtime.getActivationLogic());
        if (runtime.getResultPortId() != null) tag.setString("result_port_id", runtime.getResultPortId());
        return tag;
    }

    private static CanonicalTaskInstanceSnapshot decodeInstance(NBTTagCompound tag) {
        requireKeys(
            tag,
            set(
                "player_uuid",
                "story_instance_id",
                "task_node_placement_id",
                "task_resource_id",
                "status",
                "activation_time",
                "resource_fingerprint",
                "runtime_status",
                "progress",
                "objective_statuses",
                "internal_logic_values",
                "public_logic_outputs",
                "activation_logic"),
            "instance",
            "settlement_time",
            "result_port_id");
        String player = string(tag, "player_uuid");
        if (player.length() != 36) throw malformed("invalid player_uuid");
        UUID uuid;
        try {
            uuid = UUID.fromString(player);
        } catch (RuntimeException exception) {
            throw malformed("invalid player_uuid");
        }
        if (!uuid.toString()
            .equals(player)) throw malformed("invalid player_uuid");
        String story = string(tag, "story_instance_id");
        String placement = string(tag, "task_node_placement_id");
        String resource = string(tag, "task_resource_id");
        CanonicalTaskInstanceStatus instanceStatus = enumValue(
            string(tag, "status"),
            CanonicalTaskInstanceStatus.class);
        if (instanceStatus == CanonicalTaskInstanceStatus.NOT_STARTED)
            throw malformed("NOT_STARTED is not serializable");
        requireType(tag, "activation_time", LONG);
        long activation = tag.getLong("activation_time");
        if (activation <= 0) throw malformed("activation_time must be positive");
        Long settlement = null;
        if (tag.hasKey("settlement_time")) {
            requireType(tag, "settlement_time", LONG);
            long value = tag.getLong("settlement_time");
            if (value <= 0 || value < activation) throw malformed("invalid settlement_time");
            settlement = Long.valueOf(value);
        }
        CanonicalTaskStatus runtimeStatus = enumValue(string(tag, "runtime_status"), CanonicalTaskStatus.class);
        String fingerprint = string(tag, "resource_fingerprint");
        Map<String, Integer> progress = integers(tag, "progress");
        Map<String, CanonicalTaskObjectiveStatus> objectiveStatuses = statuses(tag, "objective_statuses");
        Map<String, Boolean> internal = booleans(tag, "internal_logic_values");
        Map<String, Boolean> publicLogic = booleans(tag, "public_logic_outputs");
        requireType(tag, "activation_logic", BYTE);
        byte activationByte = tag.getByte("activation_logic");
        if (activationByte != 0 && activationByte != 1) throw malformed("activation_logic must be boolean");
        String result = tag.hasKey("result_port_id") ? string(tag, "result_port_id") : null;
        if (runtimeStatus == CanonicalTaskStatus.ACTIVE && (result != null || activationByte != 1))
            throw malformed("active runtime state is contradictory");
        if (runtimeStatus == CanonicalTaskStatus.SETTLED && (result == null || activationByte != 0))
            throw malformed("settled runtime state is contradictory");
        if (instanceStatus == CanonicalTaskInstanceStatus.ACTIVE
            && (runtimeStatus != CanonicalTaskStatus.ACTIVE || settlement != null))
            throw malformed("active instance state");
        if (instanceStatus == CanonicalTaskInstanceStatus.SETTLED
            && (runtimeStatus != CanonicalTaskStatus.SETTLED || settlement == null))
            throw malformed("settled instance state");
        if (instanceStatus == CanonicalTaskInstanceStatus.CANCELLED_BY_STORY_TERMINATION
            && (runtimeStatus != CanonicalTaskStatus.ACTIVE || settlement != null))
            throw malformed("cancelled instance state");
        if (instanceStatus == CanonicalTaskInstanceStatus.ERROR && settlement != null)
            throw malformed("error instance state");
        CanonicalTaskSnapshot runtime = new CanonicalTaskSnapshot(
            resource,
            fingerprint,
            runtimeStatus,
            progress,
            objectiveStatuses,
            internal,
            publicLogic,
            activationByte == 1,
            result);
        try {
            return new CanonicalTaskInstanceSnapshot(
                uuid,
                story,
                placement,
                resource,
                instanceStatus,
                activation,
                settlement,
                runtime);
        } catch (RuntimeException exception) {
            throw malformed(exception.getMessage());
        }
    }

    private static NBTTagList integers(Map<String, Integer> values) {
        NBTTagList list = new NBTTagList();
        List<String> keys = new ArrayList<String>(values.keySet());
        Collections.sort(keys);
        for (String key : keys) {
            NBTTagCompound entry = new NBTTagCompound();
            entry.setString("key", key);
            entry.setInteger(
                "value",
                values.get(key)
                    .intValue());
            list.appendTag(entry);
        }
        return list;
    }

    private static NBTTagList statuses(Map<String, CanonicalTaskObjectiveStatus> values) {
        NBTTagList list = new NBTTagList();
        List<String> keys = new ArrayList<String>(values.keySet());
        Collections.sort(keys);
        for (String key : keys) {
            NBTTagCompound entry = new NBTTagCompound();
            entry.setString("key", key);
            entry.setString(
                "value",
                values.get(key)
                    .name());
            list.appendTag(entry);
        }
        return list;
    }

    private static NBTTagList booleans(Map<String, Boolean> values) {
        NBTTagList list = new NBTTagList();
        List<String> keys = new ArrayList<String>(values.keySet());
        Collections.sort(keys);
        for (String key : keys) {
            NBTTagCompound entry = new NBTTagCompound();
            entry.setString("key", key);
            entry.setBoolean(
                "value",
                values.get(key)
                    .booleanValue());
            list.appendTag(entry);
        }
        return list;
    }

    private static Map<String, Integer> integers(NBTTagCompound tag, String key) {
        NBTTagList list = list(tag, key);
        Map<String, Integer> result = new LinkedHashMap<String, Integer>();
        for (int i = 0; i < list.tagCount(); i++) {
            NBTTagCompound entry = list.getCompoundTagAt(i);
            requireKeys(entry, set("key", "value"), key + " entry");
            String name = string(entry, "key");
            requireType(entry, "value", INT);
            if (entry.getInteger("value") < 0) throw malformed("negative progress");
            if (result.put(name, Integer.valueOf(entry.getInteger("value"))) != null)
                throw malformed("duplicate " + key + " key");
        }
        return result;
    }

    private static Map<String, CanonicalTaskObjectiveStatus> statuses(NBTTagCompound tag, String key) {
        NBTTagList list = list(tag, key);
        Map<String, CanonicalTaskObjectiveStatus> result = new LinkedHashMap<String, CanonicalTaskObjectiveStatus>();
        for (int i = 0; i < list.tagCount(); i++) {
            NBTTagCompound entry = list.getCompoundTagAt(i);
            requireKeys(entry, set("key", "value"), key + " entry");
            String name = string(entry, "key");
            CanonicalTaskObjectiveStatus value = enumValue(string(entry, "value"), CanonicalTaskObjectiveStatus.class);
            if (result.put(name, value) != null) throw malformed("duplicate " + key + " key");
        }
        return result;
    }

    private static Map<String, Boolean> booleans(NBTTagCompound tag, String key) {
        NBTTagList list = list(tag, key);
        Map<String, Boolean> result = new LinkedHashMap<String, Boolean>();
        for (int i = 0; i < list.tagCount(); i++) {
            NBTTagCompound entry = list.getCompoundTagAt(i);
            requireKeys(entry, set("key", "value"), key + " entry");
            String name = string(entry, "key");
            requireType(entry, "value", BYTE);
            byte value = entry.getByte("value");
            if (value != 0 && value != 1) throw malformed("invalid boolean");
            if (result.put(name, Boolean.valueOf(value == 1)) != null) throw malformed("duplicate " + key + " key");
        }
        return result;
    }

    private static NBTTagList list(NBTTagCompound tag, String key) {
        requireType(tag, key, LIST);
        NBTTagList list = (NBTTagList) tag.getTag(key);
        requireListType(list, key);
        return list;
    }

    private static String string(NBTTagCompound tag, String key) {
        requireType(tag, key, STRING);
        String value = tag.getString(key);
        if (value == null || value.trim()
            .isEmpty()) throw malformed("blank " + key);
        return value;
    }

    private static <T extends Enum<T>> T enumValue(String text, Class<T> type) {
        try {
            return Enum.valueOf(type, text);
        } catch (RuntimeException exception) {
            throw malformed("invalid enum");
        }
    }

    private static void requireType(NBTTagCompound tag, String key, int type) {
        if (tag == null || !tag.hasKey(key, type)) throw malformed("invalid or missing " + key);
    }

    private static void requireListType(NBTTagList list, String key) {
        if (list == null || (list.tagCount() > 0 && list.func_150303_d() != COMPOUND))
            throw malformed("invalid " + key + " entry type");
    }

    private static void requireKeys(NBTTagCompound tag, Set<String> required, String label, String... optional) {
        if (tag == null) throw malformed(label);
        Set<String> actual = new HashSet<String>(tag.func_150296_c());
        Set<String> accepted = new HashSet<String>(required);
        accepted.addAll(java.util.Arrays.asList(optional));
        if (!actual.containsAll(required) || !accepted.containsAll(actual))
            throw malformed("unknown or missing " + label + " key");
    }

    private static Set<String> set(String... values) {
        Set<String> result = new HashSet<String>();
        for (String value : values) result.add(value);
        return result;
    }

    private static String identity(CanonicalTaskInstanceSnapshot snapshot) {
        return snapshot.getPlayerUuid()
            .toString() + "\u0000"
            + snapshot.getStoryInstanceId()
            + "\u0000"
            + snapshot.getTaskNodePlacementId();
    }

    private static IllegalArgumentException malformed(String detail) {
        return new IllegalArgumentException("Malformed Task NBT: " + detail);
    }
}
