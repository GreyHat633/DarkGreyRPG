package darkgrey.rpg.nominator;

import java.util.ArrayList;
import java.util.List;
import java.util.UUID;

import net.minecraft.entity.EntityList;
import net.minecraft.item.ItemStack;

import darkgrey.rpg.identity.NpcHostIdentity;
import darkgrey.rpg.identity.NpcIdentitySavedData;
import darkgrey.rpg.item.identity.ItemGroupMember;
import darkgrey.rpg.item.identity.ItemIdentitySavedData;
import darkgrey.rpg.item.identity.ItemMatchMode;
import darkgrey.rpg.item.identity.ItemStackDefinition;
import darkgrey.rpg.project.ActorDefinition;
import darkgrey.rpg.project.ProjectSnapshot;

/** Server-side application service. Callers must pass server-owned snapshots/data. */
public final class NominatorService {

    private NominatorService() {}

    /** Applies a collective group to one exact, safe Forge registry type. */
    public static NominatorResult bindEntityTypeGroup(boolean authorized, String entityType, String groupId,
        boolean add, ProjectSnapshot project, NominatorSavedData selections) {
        if (!authorized) return NominatorResult.rejected("permission_denied", "Nominator permission is required.");
        if (project == null || selections == null)
            return NominatorResult.rejected("invalid_request", "Project and persistence are required.");
        if (!safeType(entityType)) return NominatorResult
            .rejected("unsafe_entity_type", "This wrapper entity type requires explicit compatibility support.");
        String group = blank(groupId) ? null : groupId.trim();
        if (group == null) return NominatorResult.rejected("invalid_group", "A collective group is required.");
        darkgrey.rpg.project.ActorDefinition actor = project.getActor(group);
        if (actor == null || !actor.isCollective())
            return NominatorResult.rejected("invalid_group", "Group is not collective.");
        boolean changed = add ? selections.addTypeGroup(entityType, group)
            : selections.removeTypeGroup(entityType, group);
        return NominatorResult.accepted(changed ? "Entity type group persisted." : "Entity type group unchanged.");
    }

    /** Shared safety policy: never fan out across CNPC/DGR wrapper classes implicitly. */
    public static boolean safeType(String entityType) {
        if (blank(entityType)) return false;
        String value = entityType.trim()
            .toLowerCase();
        return value.indexOf("customnpc") < 0 && value.indexOf("customnpcs") < 0
            && value.indexOf("darkgrey") < 0
            && value.indexOf("dark_grey") < 0
            && value.indexOf("dgr") < 0;
    }

    public static String entityType(net.minecraft.entity.Entity entity) {
        String type = entity == null ? null : EntityList.getEntityString(entity);
        return blank(type) ? "unknown" : type.trim();
    }

    public static NominatorResult bindEntity(boolean authorized, UUID entityUuid, String entityType, int dimension,
        String individualId, List<String> groupIds, String storyId, ProjectSnapshot project,
        NpcIdentitySavedData identities, NominatorSavedData selections) {
        return bindEntity(
            authorized,
            entityUuid,
            entityType,
            dimension,
            individualId,
            groupIds,
            storyId,
            false,
            project,
            identities,
            selections);
    }

    /** Explicit server-side unbind operation; equivalent to an empty selection. */
    public static NominatorResult unbindEntity(boolean authorized, UUID entityUuid, String entityType, int dimension,
        ProjectSnapshot project, NpcIdentitySavedData identities, NominatorSavedData selections) {
        return bindEntity(
            authorized,
            entityUuid,
            entityType,
            dimension,
            null,
            java.util.Collections.<String>emptyList(),
            null,
            false,
            project,
            identities,
            selections);
    }

    /**
     * Applies one complete entity selection. Empty individual and groups is
     * an explicit unbind. Transfer is intentionally opt-in because it moves a
     * unique NPC ID away from its former host.
     */
    public static NominatorResult bindEntity(boolean authorized, UUID entityUuid, String entityType, int dimension,
        String individualId, List<String> groupIds, String storyId, boolean transfer, ProjectSnapshot project,
        NpcIdentitySavedData identities, NominatorSavedData selections) {
        if (!authorized) return NominatorResult.rejected("permission_denied", "Nominator permission is required.");
        if (entityUuid == null || project == null || identities == null || selections == null)
            return NominatorResult.rejected("invalid_request", "Entity, project, and persistence are required.");
        try {
            String individual = blank(individualId) ? null : individualId.trim();
            List<String> groups = cleanGroups(groupIds);
            if (!blank(storyId) && project.getStory(storyId.trim()) == null)
                return NominatorResult.rejected("unknown_story", "Selected story is not loaded.");
            if (individual != null) {
                ActorDefinition actor = project.getActor(individual);
                if (actor == null || !actor.isIndividual())
                    return NominatorResult.rejected("invalid_individual", "Individual actor is not available.");
            }
            for (String group : groups) {
                ActorDefinition actor = project.getActor(group);
                if (actor == null || !actor.isCollective()) return NominatorResult
                    .rejected("invalid_group", "Every selected group must be a collective actor.");
            }
            NominatorEntityBinding candidate = new NominatorEntityBinding(entityUuid, individual, groups, storyId);
            synchronized (identities) {
                synchronized (selections) {
                    NominatorEntityBinding existing = selections.get(entityUuid);
                    boolean selectionSame = existing != null && same(existing, candidate);
                    NpcHostIdentity host = new NpcHostIdentity(
                        entityUuid,
                        entityType == null ? "unknown" : entityType,
                        dimension);
                    String currentNpcId = identities.getNpcId(entityUuid);
                    if (individual == null) {
                        if (currentNpcId != null) identities.unbindHost(entityUuid);
                    } else {
                        NpcHostIdentity occupiedHost = identities.getHost(individual);
                        if (occupiedHost != null && !entityUuid.equals(occupiedHost.getEntityUuid())) {
                            if (!transfer) return NominatorResult.rejected(
                                "conflict",
                                "NPC ID '" + individual + "' is already occupied; explicit transfer is required.");
                            identities.transfer(individual, host);
                            clearTransferredHostSelection(occupiedHost.getEntityUuid(), selections);
                        } else if (currentNpcId != null && !individual.equals(currentNpcId)) {
                            return NominatorResult
                                .rejected("conflict", "Entity already hosts NPC ID '" + currentNpcId + "'.");
                        } else {
                            if (currentNpcId == null) identities.bind(individual, host);
                            else identities.observe(host);
                        }
                    }
                    if (individual == null && groups.isEmpty()) selections.remove(entityUuid);
                    else if (!selectionSame) selections.put(candidate);
                }
            }
            return NominatorResult.accepted("Entity selection persisted.");
        } catch (RuntimeException exception) {
            return NominatorResult.rejected("conflict", safeMessage(exception));
        }
    }

    private static void clearTransferredHostSelection(UUID hostUuid, NominatorSavedData selections) {
        NominatorEntityBinding previous = selections.get(hostUuid);
        if (previous == null || previous.getIndividualId() == null) return;
        if (previous.getGroupIds()
            .isEmpty()) selections.remove(hostUuid);
        else selections.put(new NominatorEntityBinding(hostUuid, null, previous.getGroupIds(), previous.getStoryId()));
    }

    public static NominatorResult bindInventory(boolean authorized, ItemStack stack, String itemId, String exactGroupId,
        List<String> fuzzyGroupIds, ProjectSnapshot project, ItemIdentitySavedData identities) {
        if (!authorized) return NominatorResult.rejected("permission_denied", "Nominator permission is required.");
        if (stack == null || project == null || identities == null)
            return NominatorResult.rejected("invalid_request", "Item, project, and persistence are required.");
        try {
            String exact = blank(itemId) ? null : itemId.trim();
            String exactGroup = blank(exactGroupId) ? null : exactGroupId.trim();
            List<String> fuzzy = cleanGroups(fuzzyGroupIds);
            if (exact == null && exactGroup == null && fuzzy.isEmpty())
                return NominatorResult.rejected("empty_selection", "Choose an Item ID or at least one group.");
            ItemStackDefinition definition = ItemStackDefinition.capture(stack);
            if (exact != null && project.getItem(exact) == null)
                return NominatorResult.rejected("unknown_item_id", "Item ID is not available in the loaded project.");
            if (exactGroup != null && project.getItemGroup(exactGroup) == null)
                return NominatorResult.rejected("unknown_group", "Exact group is not available in the loaded project.");
            for (String group : fuzzy) if (project.getItemGroup(group) == null)
                return NominatorResult.rejected("unknown_group", "Fuzzy group is not available in the loaded project.");
            if (exact != null) identities.bindItem(exact, definition);
            if (exactGroup != null)
                identities.addGroupMember(exactGroup, new ItemGroupMember(ItemMatchMode.EXACT, definition));
            for (String group : fuzzy)
                identities.addGroupMember(group, new ItemGroupMember(ItemMatchMode.FUZZY, definition));
            return NominatorResult.accepted(explanation(exact, exactGroup, fuzzy));
        } catch (RuntimeException exception) {
            return NominatorResult.rejected("conflict", safeMessage(exception));
        }
    }

    private static String explanation(String item, String exactGroup, List<String> fuzzy) {
        List<String> values = new ArrayList<String>();
        if (item != null) values.add("exact Item ID '" + item + "'");
        if (exactGroup != null) values.add("exact group '" + exactGroup + "'");
        for (String group : fuzzy) values.add("fuzzy group '" + group + "' (registry name only)");
        return "Bound " + join(values) + ".";
    }

    private static boolean same(NominatorEntityBinding left, NominatorEntityBinding right) {
        return equal(left.getIndividualId(), right.getIndividualId()) && left.getGroupIds()
            .equals(right.getGroupIds()) && equal(left.getStoryId(), right.getStoryId());
    }

    private static List<String> cleanGroups(List<String> groups) {
        if (groups != null && groups.size() > 32) throw new IllegalArgumentException("Too many groups.");
        List<String> result = new ArrayList<String>();
        if (groups != null) for (String value : groups) if (!blank(value)) {
            if (value.trim()
                .length() > 256) throw new IllegalArgumentException("Group ID is too long.");
            if (!result.contains(value.trim())) result.add(value.trim());
        }
        return result;
    }

    private static boolean equal(Object left, Object right) {
        return left == null ? right == null : left.equals(right);
    }

    private static boolean blank(String value) {
        return value == null || value.trim()
            .isEmpty();
    }

    private static String join(List<String> values) {
        StringBuilder result = new StringBuilder();
        for (String value : values) {
            if (result.length() > 0) result.append(", ");
            result.append(value);
        }
        return result.toString();
    }

    private static String safeMessage(RuntimeException exception) {
        return exception.getMessage() == null ? "Request was rejected by the server." : exception.getMessage();
    }
}
