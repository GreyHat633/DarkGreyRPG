package darkgrey.rpg.story.canonical.instance;

import java.util.ArrayList;
import java.util.Collections;
import java.util.HashSet;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Locale;
import java.util.Map;
import java.util.Set;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

import darkgrey.rpg.story.canonical.runtime.CanonicalStoryRepeatPolicy;
import darkgrey.rpg.story.canonical.runtime.CanonicalStorySnapshot;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryStatus;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryWaitKind;

/** Strict versioned NBT codec for canonical Story instances. */
public final class CanonicalStoryInstanceNbtCodec {

    public static final int SCHEMA_VERSION = 1;
    private static final int BYTE = 1;
    private static final int LONG = 4;
    private static final int INT = 3;
    private static final int STRING = 8;
    private static final int LIST = 9;
    private static final int COMPOUND = 10;

    private CanonicalStoryInstanceNbtCodec() {}

    public static NBTTagCompound encode(List<CanonicalStoryInstanceSnapshot> snapshots) {
        if (snapshots == null) throw malformed("instances");
        NBTTagCompound root = new NBTTagCompound();
        root.setInteger("schema_version", SCHEMA_VERSION);
        NBTTagList instances = new NBTTagList();
        Set<String> identities = new HashSet<String>();
        for (CanonicalStoryInstanceSnapshot snapshot : snapshots) {
            if (snapshot == null || !identities.add(identity(snapshot.getPlayerUuid(), snapshot.getStoryId())))
                throw malformed("duplicate or null instance");
            instances.appendTag(encodeInstance(snapshot));
        }
        root.setTag("instances", instances);
        return root;
    }

    public static List<CanonicalStoryInstanceSnapshot> decode(NBTTagCompound root) {
        requireKeys(root, set("schema_version", "instances"), "root");
        requireType(root, "schema_version", INT);
        if (root.getInteger("schema_version") != SCHEMA_VERSION) throw malformed("unsupported schema_version");
        requireType(root, "instances", LIST);
        NBTTagList instances = (NBTTagList) root.getTag("instances");
        if (instances.tagCount() > 0 && instances.func_150303_d() != COMPOUND)
            throw malformed("invalid instances list type");
        List<CanonicalStoryInstanceSnapshot> result = new ArrayList<CanonicalStoryInstanceSnapshot>();
        Set<String> identities = new HashSet<String>();
        for (int index = 0; index < instances.tagCount(); index++) {
            CanonicalStoryInstanceSnapshot snapshot = decodeInstance(instances.getCompoundTagAt(index));
            if (!identities.add(identity(snapshot.getPlayerUuid(), snapshot.getStoryId())))
                throw malformed("duplicate instance identity");
            result.add(snapshot);
        }
        return result;
    }

    private static NBTTagCompound encodeInstance(CanonicalStoryInstanceSnapshot value) {
        NBTTagCompound tag = new NBTTagCompound();
        tag.setString(
            "player_uuid",
            value.getPlayerUuid()
                .toString());
        tag.setString("story_id", value.getStoryId());
        tag.setLong("activation_time", value.getActivationTime());
        tag.setLong(
            "terminal_time",
            value.getTerminalTime() == null ? 0L
                : value.getTerminalTime()
                    .longValue());
        tag.setTag("runtime", encodeRuntime(value.getRuntimeSnapshot()));
        return tag;
    }

    private static CanonicalStoryInstanceSnapshot decodeInstance(NBTTagCompound tag) {
        requireKeys(tag, set("player_uuid", "story_id", "activation_time", "terminal_time", "runtime"), "instance");
        UUID player = uuid(tag, "player_uuid");
        String story = string(tag, "story_id");
        requireType(tag, "activation_time", LONG);
        requireType(tag, "terminal_time", LONG);
        requireType(tag, "runtime", COMPOUND);
        long activation = tag.getLong("activation_time");
        long terminal = tag.getLong("terminal_time");
        if (activation <= 0 || terminal < 0) throw malformed("invalid instance timestamps");
        return new CanonicalStoryInstanceSnapshot(
            player,
            story,
            activation,
            terminal == 0 ? null : Long.valueOf(terminal),
            decodeRuntime(tag.getCompoundTag("runtime")));
    }

    private static NBTTagCompound encodeRuntime(CanonicalStorySnapshot value) {
        NBTTagCompound tag = new NBTTagCompound();
        tag.setString("resource_id", value.getResourceId());
        tag.setString("resource_fingerprint", value.getResourceFingerprint());
        tag.setString(
            "status",
            value.getStatus()
                .name());
        tag.setString(
            "repeat_policy",
            value.getRepeatPolicy()
                .getJsonName());
        tag.setString("trigger_port_id", value.getTriggerPortId());
        tag.setString("current_node_id", nullable(value.getCurrentNodeId()));
        tag.setString("current_input_port_id", nullable(value.getCurrentInputPortId()));
        tag.setString(
            "wait_kind",
            value.getWaitKind()
                .name());
        tag.setString("wait_resource_id", nullable(value.getWaitResourceId()));
        tag.setString(
            "wait_dimension",
            value.getWaitDimension() == null ? ""
                : value.getWaitDimension()
                    .toString());
        tag.setString(
            "wait_x",
            value.getWaitX() == null ? ""
                : value.getWaitX()
                    .toString());
        tag.setString(
            "wait_y",
            value.getWaitY() == null ? ""
                : value.getWaitY()
                    .toString());
        tag.setString(
            "wait_z",
            value.getWaitZ() == null ? ""
                : value.getWaitZ()
                    .toString());
        tag.setString(
            "wait_radius",
            value.getWaitRadius() == null ? ""
                : value.getWaitRadius()
                    .toString());
        tag.setString("target_story_id", nullable(value.getTargetStoryId()));
        NBTTagList logic = new NBTTagList();
        for (Map.Entry<String, Boolean> entry : value.getLogicValues()
            .entrySet()) {
            NBTTagCompound item = new NBTTagCompound();
            item.setString("endpoint", entry.getKey());
            item.setByte(
                "value",
                (byte) (entry.getValue()
                    .booleanValue() ? 1 : 0));
            logic.appendTag(item);
        }
        tag.setTag("logic", logic);
        tag.setTag("external_logic_inputs", booleans(value.getExternalLogicInputs()));
        if (value.getWaitingConditionValue() != null) tag.setByte(
            "waiting_condition_value",
            (byte) (value.getWaitingConditionValue()
                .booleanValue() ? 1 : 0));
        return tag;
    }

    private static CanonicalStorySnapshot decodeRuntime(NBTTagCompound tag) {
        Set<String> required = set(
            "resource_id",
            "resource_fingerprint",
            "status",
            "repeat_policy",
            "trigger_port_id",
            "current_node_id",
            "current_input_port_id",
            "wait_kind",
            "wait_resource_id",
            "wait_dimension",
            "wait_x",
            "wait_y",
            "wait_z",
            "wait_radius",
            "target_story_id",
            "logic");
        Set<String> legacy = new HashSet<String>(required);
        legacy.remove("wait_dimension");
        legacy.remove("wait_x");
        legacy.remove("wait_y");
        legacy.remove("wait_z");
        legacy.remove("wait_radius");
        Set<String> extended = new HashSet<String>(required);
        extended.add("external_logic_inputs");
        Set<String> extendedWait = new HashSet<String>(required);
        extendedWait.add("waiting_condition_value");
        Set<String> extendedFull = new HashSet<String>(extended);
        extendedFull.add("waiting_condition_value");
        Set<String> extendedLegacy = new HashSet<String>(legacy);
        extendedLegacy.add("external_logic_inputs");
        Set<String> extendedLegacyWait = new HashSet<String>(legacy);
        extendedLegacyWait.add("waiting_condition_value");
        Set<String> extendedLegacyFull = new HashSet<String>(extendedLegacy);
        extendedLegacyFull.add("waiting_condition_value");
        if (!keys(tag).equals(required) && !keys(tag).equals(legacy)
            && !keys(tag).equals(extended)
            && !keys(tag).equals(extendedWait)
            && !keys(tag).equals(extendedFull)
            && !keys(tag).equals(extendedLegacy)
            && !keys(tag).equals(extendedLegacyWait)
            && !keys(tag).equals(extendedLegacyFull)) throw malformed("unknown or missing runtime key");
        CanonicalStoryStatus status = enumeration(CanonicalStoryStatus.class, string(tag, "status"), "status");
        CanonicalStoryRepeatPolicy repeat;
        try {
            repeat = CanonicalStoryRepeatPolicy.fromJsonName(string(tag, "repeat_policy"));
        } catch (RuntimeException exception) {
            throw malformed("invalid repeat_policy");
        }
        CanonicalStoryWaitKind wait = enumeration(CanonicalStoryWaitKind.class, string(tag, "wait_kind"), "wait_kind");
        requireType(tag, "logic", LIST);
        NBTTagList values = (NBTTagList) tag.getTag("logic");
        if (values.tagCount() > 0 && values.func_150303_d() != COMPOUND) throw malformed("invalid logic list type");
        LinkedHashMap<String, Boolean> logic = new LinkedHashMap<String, Boolean>();
        for (int index = 0; index < values.tagCount(); index++) {
            NBTTagCompound item = values.getCompoundTagAt(index);
            requireKeys(item, set("endpoint", "value"), "logic entry");
            String endpoint = string(item, "endpoint");
            requireType(item, "value", BYTE);
            byte value = item.getByte("value");
            if (value != 0 && value != 1) throw malformed("invalid logic value");
            if (logic.put(endpoint, Boolean.valueOf(value == 1)) != null) throw malformed("duplicate logic endpoint");
        }
        Map<String, Boolean> external = tag.hasKey("external_logic_inputs")
            ? decodeBooleans(tag, "external_logic_inputs")
            : Collections.<String, Boolean>emptyMap();
        Boolean waitingValue = null;
        if (tag.hasKey("waiting_condition_value")) {
            requireType(tag, "waiting_condition_value", BYTE);
            byte value = tag.getByte("waiting_condition_value");
            if (value != 0 && value != 1) throw malformed("invalid waiting_condition_value");
            waitingValue = Boolean.valueOf(value == 1);
        }
        if (wait != CanonicalStoryWaitKind.CONDITION && waitingValue != null)
            throw malformed("waiting_condition_value requires Condition wait");
        return new CanonicalStorySnapshot(
            string(tag, "resource_id"),
            string(tag, "resource_fingerprint"),
            status,
            repeat,
            string(tag, "trigger_port_id"),
            optionalString(tag, "current_node_id"),
            optionalString(tag, "current_input_port_id"),
            wait,
            optionalString(tag, "wait_resource_id"),
            optionalInteger(tag, "wait_dimension"),
            optionalDouble(tag, "wait_x"),
            optionalDouble(tag, "wait_y"),
            optionalDouble(tag, "wait_z"),
            optionalDouble(tag, "wait_radius"),
            logic,
            optionalString(tag, "target_story_id"),
            external,
            waitingValue);
    }

    private static NBTTagList booleans(Map<String, Boolean> values) {
        NBTTagList list = new NBTTagList();
        java.util.List<String> keys = new java.util.ArrayList<String>(values.keySet());
        java.util.Collections.sort(keys);
        for (String key : keys) {
            NBTTagCompound item = new NBTTagCompound();
            item.setString("key", key);
            item.setByte(
                "value",
                (byte) (values.get(key)
                    .booleanValue() ? 1 : 0));
            list.appendTag(item);
        }
        return list;
    }

    private static Map<String, Boolean> decodeBooleans(NBTTagCompound tag, String key) {
        requireType(tag, key, LIST);
        NBTTagList list = (NBTTagList) tag.getTag(key);
        if (list.tagCount() > 0 && list.func_150303_d() != COMPOUND) throw malformed("invalid " + key + " list type");
        java.util.LinkedHashMap<String, Boolean> result = new java.util.LinkedHashMap<String, Boolean>();
        for (int index = 0; index < list.tagCount(); index++) {
            NBTTagCompound item = list.getCompoundTagAt(index);
            requireKeys(item, set("key", "value"), key + " entry");
            String name = string(item, "key");
            requireType(item, "value", BYTE);
            byte value = item.getByte("value");
            if (value != 0 && value != 1) throw malformed("invalid " + key + " value");
            if (result.put(name, Boolean.valueOf(value == 1)) != null) throw malformed("duplicate " + key + " key");
        }
        return result;
    }

    private static UUID uuid(NBTTagCompound tag, String key) {
        String value = string(tag, key);
        if (value.length() != 36) throw malformed("invalid " + key);
        try {
            UUID result = UUID.fromString(value);
            if (!result.toString()
                .equals(value.toLowerCase(Locale.ROOT))) throw malformed("invalid " + key);
            return result;
        } catch (RuntimeException exception) {
            throw malformed("invalid " + key);
        }
    }

    private static <T extends Enum<T>> T enumeration(Class<T> type, String value, String key) {
        try {
            return Enum.valueOf(type, value);
        } catch (RuntimeException exception) {
            throw malformed("invalid " + key);
        }
    }

    private static String string(NBTTagCompound tag, String key) {
        requireType(tag, key, STRING);
        String value = tag.getString(key);
        if (value == null || value.trim()
            .isEmpty()) throw malformed("blank " + key);
        return value;
    }

    private static String optionalString(NBTTagCompound tag, String key) {
        if (tag == null || !tag.hasKey(key)) return null;
        requireType(tag, key, STRING);
        String value = tag.getString(key);
        return value == null || value.isEmpty() ? null : value;
    }

    private static Integer optionalInteger(NBTTagCompound tag, String key) {
        String value = optionalString(tag, key);
        if (value == null) return null;
        try {
            return Integer.valueOf(value);
        } catch (RuntimeException exception) {
            throw malformed("invalid " + key);
        }
    }

    private static Double optionalDouble(NBTTagCompound tag, String key) {
        String value = optionalString(tag, key);
        if (value == null) return null;
        try {
            double result = Double.parseDouble(value);
            if (Double.isNaN(result) || Double.isInfinite(result)) throw new NumberFormatException();
            return Double.valueOf(result);
        } catch (RuntimeException exception) {
            throw malformed("invalid " + key);
        }
    }

    private static String nullable(String value) {
        return value == null ? "" : value;
    }

    private static String identity(UUID player, String story) {
        return player.toString() + "\u0000" + story;
    }

    private static void requireType(NBTTagCompound tag, String key, int type) {
        if (tag == null || !tag.hasKey(key, type)) throw malformed("invalid or missing " + key);
    }

    private static void requireKeys(NBTTagCompound tag, Set<String> allowed, String label) {
        if (tag == null) throw malformed(label);
        Set<String> keys = new HashSet<String>(tag.func_150296_c());
        if (!keys.equals(allowed)) throw malformed("unknown or missing " + label + " key");
    }

    private static Set<String> keys(NBTTagCompound tag) {
        return tag == null ? java.util.Collections.<String>emptySet() : new HashSet<String>(tag.func_150296_c());
    }

    private static Set<String> set(String... values) {
        Set<String> result = new HashSet<String>();
        for (String value : values) result.add(value);
        return result;
    }

    private static IllegalArgumentException malformed(String detail) {
        return new IllegalArgumentException("Malformed canonical Story instance NBT: " + detail);
    }
}
