package darkgrey.rpg.task.journal;

import java.util.ArrayList;
import java.util.Collections;
import java.util.Comparator;
import java.util.HashSet;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.UUID;

import com.google.gson.JsonElement;

import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot;
import darkgrey.rpg.task.instance.CanonicalTaskResourceResolver;
import darkgrey.rpg.task.runtime.CanonicalTaskObjectiveStatus;
import darkgrey.rpg.task.runtime.CanonicalTaskRuntime;
import darkgrey.rpg.task.runtime.CanonicalTaskSnapshot;

/**
 * Pure read-only projection from canonical TaskInstance snapshots to Journal
 * values. No world, Forge, event, GUI, or per-tick dependencies are involved.
 */
public final class CanonicalTaskJournalProjector {

    private final CanonicalTaskResourceResolver resolver;
    private final UUID configuredPlayerUuid;
    private final List<CanonicalTaskInstanceSnapshot> configuredSnapshots;

    public CanonicalTaskJournalProjector(CanonicalTaskResourceResolver resolver) {
        if (resolver == null) throw new IllegalArgumentException("Task resource resolver is required.");
        this.resolver = resolver;
        this.configuredPlayerUuid = null;
        this.configuredSnapshots = null;
    }

    public CanonicalTaskJournalProjector() {
        this.resolver = null;
        this.configuredPlayerUuid = null;
        this.configuredSnapshots = null;
    }

    public CanonicalTaskJournalProjector(UUID playerUuid, List<CanonicalTaskInstanceSnapshot> snapshots,
        CanonicalTaskResourceResolver resolver) {
        if (playerUuid == null || snapshots == null || resolver == null)
            throw new IllegalArgumentException("Task Journal projection inputs are required.");
        this.resolver = resolver;
        this.configuredPlayerUuid = playerUuid;
        this.configuredSnapshots = new ArrayList<CanonicalTaskInstanceSnapshot>(snapshots);
    }

    public List<CanonicalTaskJournalEntry> project() {
        if (configuredPlayerUuid == null || configuredSnapshots == null)
            throw new IllegalStateException("Task Journal projection inputs are not configured.");
        return project(configuredPlayerUuid, configuredSnapshots, resolver);
    }

    public List<CanonicalTaskJournalEntry> project(UUID playerUuid, List<CanonicalTaskInstanceSnapshot> snapshots) {
        if (resolver == null) throw new IllegalStateException("Task resource resolver is required.");
        return project(playerUuid, snapshots, resolver);
    }

    public List<CanonicalTaskJournalEntry> project(String playerUuid, List<CanonicalTaskInstanceSnapshot> snapshots) {
        return project(parseUuid(playerUuid), snapshots);
    }

    public static List<CanonicalTaskJournalEntry> project(UUID playerUuid,
        List<CanonicalTaskInstanceSnapshot> snapshots, CanonicalTaskResourceResolver resolver) {
        if (playerUuid == null) throw new IllegalArgumentException("Player UUID is required.");
        if (snapshots == null) throw new IllegalArgumentException("Task snapshots are required.");
        if (resolver == null) throw new IllegalArgumentException("Task resource resolver is required.");

        List<CanonicalTaskJournalEntry> result = new ArrayList<CanonicalTaskJournalEntry>();
        Set<String> identities = new HashSet<String>();
        for (CanonicalTaskInstanceSnapshot snapshot : snapshots) {
            if (snapshot == null) throw new IllegalArgumentException("Task snapshot cannot be null.");
            // Filtering occurs before resource resolution: this projection is for one player only.
            if (!playerUuid.equals(snapshot.getPlayerUuid())) continue;
            String identity = CanonicalTaskJournalEntry
                .identity(snapshot.getPlayerUuid(), snapshot.getStoryInstanceId(), snapshot.getTaskNodePlacementId());
            if (!identities.add(identity)) throw new IllegalArgumentException("Duplicate TaskInstance identity.");
            result.add(projectOne(snapshot, resolver));
        }
        Collections.sort(result, new Comparator<CanonicalTaskJournalEntry>() {

            @Override
            public int compare(CanonicalTaskJournalEntry left, CanonicalTaskJournalEntry right) {
                if (left.getActivationTime() < right.getActivationTime()) return -1;
                if (left.getActivationTime() > right.getActivationTime()) return 1;
                return left.getIdentity()
                    .compareTo(right.getIdentity());
            }
        });
        return Collections.unmodifiableList(result);
    }

    public static List<CanonicalTaskJournalEntry> project(String playerUuid,
        List<CanonicalTaskInstanceSnapshot> snapshots, CanonicalTaskResourceResolver resolver) {
        return project(parseUuid(playerUuid), snapshots, resolver);
    }

    public List<CanonicalTaskJournalEntry> projectForPlayer(UUID playerUuid,
        List<CanonicalTaskInstanceSnapshot> snapshots) {
        return project(playerUuid, snapshots);
    }

    public static List<CanonicalTaskJournalEntry> projectEntries(UUID playerUuid,
        List<CanonicalTaskInstanceSnapshot> snapshots, CanonicalTaskResourceResolver resolver) {
        return project(playerUuid, snapshots, resolver);
    }

    private static CanonicalTaskJournalEntry projectOne(CanonicalTaskInstanceSnapshot snapshot,
        CanonicalTaskResourceResolver resolver) {
        CanonicalGraphResource resource = resolver.resolve(snapshot.getTaskResourceId());
        if (resource == null) throw new IllegalArgumentException("Missing canonical Task resource.");
        if (resource.getResourceKind() == null
            || resource.getResourceKind() != darkgrey.rpg.graph.canonical.CanonicalGraphResourceKind.TASK)
            throw new IllegalArgumentException("Resolved resource is not a canonical Task.");
        if (!snapshot.getTaskResourceId()
            .equals(resource.getId()))
            throw new IllegalArgumentException("Resolved Task resource ID does not match snapshot.");

        CanonicalTaskSnapshot runtimeSnapshot = snapshot.getRuntimeSnapshot();
        if (runtimeSnapshot == null) throw new IllegalArgumentException("Task runtime snapshot is required.");
        // Restore is the canonical corruption gate: fingerprint, objective set,
        // objective progress/status, Logic state, and settlement invariants.
        CanonicalTaskRuntime runtime = CanonicalTaskRuntime.restore(resource, runtimeSnapshot);
        if (runtime.getStatus() == null) throw new IllegalArgumentException("Task runtime status is required.");

        List<CanonicalTaskJournalObjectiveRow> rows = new ArrayList<CanonicalTaskJournalObjectiveRow>();
        for (CanonicalGraphNode node : resource.getGraph()
            .getNodes()) {
            if (!"objective".equals(node.getType())) continue;
            // Inactive prerequisites must not reveal author text to the client.
            if (runtimeSnapshot.getObjectiveStatuses()
                .get(node.getId()) == CanonicalTaskObjectiveStatus.INACTIVE) continue;
            rows.add(row(node, runtimeSnapshot));
        }
        String resultSlot = snapshot.getStatus() == darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus.SETTLED
            ? runtimeSnapshot.getResultPortId()
            : null;
        return new CanonicalTaskJournalEntry(
            snapshot.getPlayerUuid(),
            snapshot.getStoryInstanceId(),
            snapshot.getTaskNodePlacementId(),
            snapshot.getTaskResourceId(),
            resource.getDisplayName(),
            snapshot.getStatus(),
            snapshot.getActivationTime(),
            snapshot.getSettlementTime(),
            resultSlot,
            runtimeSnapshot.getPublicLogicOutputs(),
            rows,
            resource.getTaskMetadata() == null ? ""
                : resource.getTaskMetadata()
                    .getDescription(),
            runtimeSnapshot.getRewardStates()
                .containsValue(Boolean.FALSE));
    }

    private static CanonicalTaskJournalObjectiveRow row(CanonicalGraphNode node,
        CanonicalTaskSnapshot runtimeSnapshot) {
        if (node == null || node.getProperties() == null || blank(node.getId()))
            throw new IllegalArgumentException("Malformed canonical Task objective node.");
        Map<String, JsonElement> properties = node.getProperties();
        String type = string(properties, "objective_type");
        String description = string(properties, "description");
        int required = ("interact_actor".equals(type) || "reach_region".equals(type))
            && !properties.containsKey("required") ? 1 : integer(properties, "required");
        if (required <= 0) throw new IllegalArgumentException("Task objective required progress must be positive.");
        if (!"kill_entity".equals(type) && !"collect_item".equals(type)
            && !"interact_actor".equals(type)
            && !"submit_item".equals(type)
            && !"reach_region".equals(type))
            throw new IllegalArgumentException("Unsupported canonical Task objective type.");
        if ("kill_entity".equals(type)) string(properties, "entity");
        else if ("collect_item".equals(type) || "submit_item".equals(type)) {
            string(properties, "item");
            JsonElement metadata = properties.get("metadata");
            if (metadata == null || !metadata.isJsonObject())
                throw new IllegalArgumentException("Collect objective metadata must be an object.");
        } else if ("interact_actor".equals(type)) string(properties, "actor_id");
        Integer currentValue = runtimeSnapshot.getProgress()
            .get(node.getId());
        CanonicalTaskObjectiveStatus status = runtimeSnapshot.getObjectiveStatuses()
            .get(node.getId());
        if (currentValue == null || status == null)
            throw new IllegalArgumentException("Task objective state is missing.");
        int current = currentValue.intValue();
        if (current < 0 || current > required)
            throw new IllegalArgumentException("Task objective progress is invalid.");
        if (status == CanonicalTaskObjectiveStatus.INACTIVE && current != 0)
            throw new IllegalArgumentException("Inactive Task objective has progress.");
        if (status == CanonicalTaskObjectiveStatus.ACTIVE && current >= required)
            throw new IllegalArgumentException("Active Task objective has complete progress.");
        if (status == CanonicalTaskObjectiveStatus.COMPLETED && current != required)
            throw new IllegalArgumentException("Completed Task objective has incomplete progress.");
        String display = "[" + status.name() + "] " + description + " (" + current + "/" + required + ")";
        return new CanonicalTaskJournalObjectiveRow(
            node.getId(),
            description,
            type,
            status,
            current,
            required,
            true,
            display);
    }

    private static String string(Map<String, JsonElement> properties, String key) {
        JsonElement value = properties.get(key);
        if (value == null || !value.isJsonPrimitive()
            || !value.getAsJsonPrimitive()
                .isString()
            || blank(value.getAsString()))
            throw new IllegalArgumentException("Task objective property is malformed: " + key);
        return value.getAsString();
    }

    private static int integer(Map<String, JsonElement> properties, String key) {
        JsonElement value = properties.get(key);
        if (value == null || !value.isJsonPrimitive()
            || !value.getAsJsonPrimitive()
                .isNumber())
            throw new IllegalArgumentException("Task objective property is not numeric: " + key);
        int result = value.getAsInt();
        if (value.getAsDouble() != result)
            throw new IllegalArgumentException("Task objective property is not integral: " + key);
        return result;
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }

    private static UUID parseUuid(String value) {
        if (value == null || value.length() != 36) throw new IllegalArgumentException("Invalid player UUID.");
        try {
            UUID uuid = UUID.fromString(value);
            if (!uuid.toString()
                .equals(value)) throw new IllegalArgumentException("Invalid player UUID.");
            return uuid;
        } catch (RuntimeException exception) {
            throw new IllegalArgumentException("Invalid player UUID.");
        }
    }
}
