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
        if (!authorized) return NominatorResult.rejected("permission_denied", "没有使用指名器的权限。");
        if (entityUuid == null || project == null || identities == null || selections == null)
            return NominatorResult.rejected("invalid_request", "实体、项目或持久化数据不可用。");
        try {
            selections.requireUsable();
            String individual = blank(individualId) ? null : individualId.trim();
            List<String> groups = cleanGroups(groupIds);
            if (!blank(storyId) && !project.containsStory(storyId.trim()))
                return NominatorResult.rejected("unknown_story", "所选故事尚未加载。");
            if (individual != null) {
                ActorDefinition actor = project.getActor(individual);
                if (actor == null || !actor.isIndividual())
                    return NominatorResult.rejected("invalid_individual", "所选个体角色不可用。");
            }
            for (String group : groups) {
                ActorDefinition actor = project.getActor(group);
                if (actor == null || !actor.isCollective())
                    return NominatorResult.rejected("invalid_group", "所选角色组必须全部是集体角色。");
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
                    NpcHostIdentity occupiedHost = individual == null ? null : identities.getHost(individual);
                    boolean movesHost = occupiedHost != null && !entityUuid.equals(occupiedHost.getEntityUuid());
                    if (movesHost && !transfer) return NominatorResult.rejected("conflict", "角色绑定已被占用；必须明确执行转移。");
                    if (individual != null && currentNpcId != null && !individual.equals(currentNpcId))
                        return NominatorResult.rejected("conflict", "该实体已经绑定另一个角色。");
                    selections.beginBatch();
                    boolean committed = false;
                    try {
                        if (individual == null) {
                            if (currentNpcId != null) identities.unbindHost(entityUuid);
                        } else {
                            if (occupiedHost != null && !entityUuid.equals(occupiedHost.getEntityUuid())) {
                                if (!transfer) return NominatorResult.rejected("conflict", "角色绑定已被占用；必须明确执行转移。");
                                identities.transfer(individual, host);
                                clearTransferredHostSelection(occupiedHost.getEntityUuid(), selections);
                            } else if (currentNpcId != null && !individual.equals(currentNpcId)) {
                                return NominatorResult.rejected("conflict", "该实体已经绑定另一个角色。");
                            } else {
                                if (currentNpcId == null) identities.bind(individual, host);
                                else identities.observe(host);
                            }
                        }
                        if (individual == null && groups.isEmpty()) selections.remove(entityUuid);
                        else if (!selectionSame) selections.put(candidate);
                        committed = true;
                    } finally {
                        selections.endBatch(committed);
                    }
                }
            }
            return NominatorResult.accepted("实体指名已保存。");
        } catch (RuntimeException exception) {
            return NominatorResult.rejected("conflict", safeMessage(exception));
        }
    }

    public static NominatorResult releaseEntityResource(boolean authorized, String id, ProjectSnapshot project,
        NpcIdentitySavedData identities, NominatorSavedData selections) {
        if (!authorized) return NominatorResult.rejected("permission_denied", "没有使用指名器的权限。");
        if (identities == null || selections == null) return NominatorResult.rejected("invalid_request", "持久化数据不可用。");
        selections.requireUsable();
        ActorDefinition actor = project == null ? null : project.getActor(id);
        if (actor == null) return NominatorResult.rejected("invalid_resource", "角色资源不可用。");
        boolean changed = false;
        synchronized (identities) {
            synchronized (selections) {
                selections.beginBatch();
                boolean committed = false;
                try {
                    if (actor.isIndividual()) {
                        changed = identities.unbindNpcId(id);
                        for (NominatorEntityBinding b : selections.bindings()) {
                            if (id.equals(b.getIndividualId())) {
                                clearTransferredHostSelection(b.getEntityUuid(), selections);
                                changed = true;
                            }
                        }
                    } else {
                        for (NominatorEntityBinding b : selections.bindings()) {
                            List<String> groups = new ArrayList<String>(b.getGroupIds());
                            if (groups.remove(id)) {
                                if (groups.isEmpty() && b.getIndividualId() == null)
                                    selections.remove(b.getEntityUuid());
                                else selections.put(
                                    new NominatorEntityBinding(
                                        b.getEntityUuid(),
                                        b.getIndividualId(),
                                        groups,
                                        b.getStoryId()));
                                changed = true;
                            }
                        }
                    }
                    committed = true;
                } finally {
                    selections.endBatch(committed);
                }
            }
        }
        return changed ? NominatorResult.accepted("绑定已释放，资源保留。") : NominatorResult.noop("资源目前没有绑定。");
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
        if (!authorized) return NominatorResult.rejected("permission_denied", "没有使用指名器的权限。");
        if (stack == null || project == null || identities == null)
            return NominatorResult.rejected("invalid_request", "物品、项目或持久化数据不可用。");
        try {
            String exact = blank(itemId) ? null : itemId.trim();
            String exactGroup = blank(exactGroupId) ? null : exactGroupId.trim();
            List<String> fuzzy = cleanGroups(fuzzyGroupIds);
            if (exact == null && exactGroup == null && fuzzy.isEmpty())
                return NominatorResult.rejected("empty_selection", "请选择物品资源 或至少一个物品组。");
            ItemStackDefinition definition = ItemStackDefinition.capture(stack);
            if (exact != null && project.getItem(exact) == null)
                return NominatorResult.rejected("unknown_item_id", "所选物品资源 不在已加载项目中。");
            if (exactGroup != null && project.getItemGroup(exactGroup) == null)
                return NominatorResult.rejected("unknown_group", "所选精确物品组不在已加载项目中。");
            for (String group : fuzzy) if (project.getItemGroup(group) == null)
                return NominatorResult.rejected("unknown_group", "所选模糊物品组不在已加载项目中。");
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
        return "物品指名已保存。";
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
        org.apache.logging.log4j.LogManager.getLogger(NominatorService.class)
            .warn("Nominator binding rejected", exception);
        return "绑定操作失败：资源可能已失效或存在绑定冲突。";
    }
}
