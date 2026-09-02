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
        if (!authorized) return NominatorResult.rejected("permission_denied", "没有使用指名器的权限。");
        if (project == null || selections == null) return NominatorResult.rejected("invalid_request", "项目或持久化数据不可用。");
        if (!safeType(entityType)) return NominatorResult.rejected("unsafe_entity_type", "此包装实体类型需要明确的兼容支持。");
        String group = blank(groupId) ? null : groupId.trim();
        if (group == null) return NominatorResult.rejected("invalid_group", "必须选择一个集体角色组。");
        darkgrey.rpg.project.ActorDefinition actor = project.getActor(group);
        if (actor == null || !actor.isCollective()) return NominatorResult.rejected("invalid_group", "所选角色不是集体角色组。");
        boolean changed = add ? selections.addTypeGroup(entityType, group)
            : selections.removeTypeGroup(entityType, group);
        return NominatorResult.accepted(changed ? "已保存实体类型角色组。" : "实体类型角色组没有变化。");
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
        if (!authorized) return NominatorResult.rejected("permission_denied", "没有使用指名器的权限。");
        if (entityUuid == null || project == null || identities == null || selections == null)
            return NominatorResult.rejected("invalid_request", "实体、项目或持久化数据不可用。");
        try {
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
                    if (individual == null) {
                        if (currentNpcId != null) identities.unbindHost(entityUuid);
                    } else {
                        NpcHostIdentity occupiedHost = identities.getHost(individual);
                        if (occupiedHost != null && !entityUuid.equals(occupiedHost.getEntityUuid())) {
                            if (!transfer)
                                return NominatorResult.rejected("conflict", "NPC ID“" + individual + "”已被占用；必须明确执行转移。");
                            identities.transfer(individual, host);
                            clearTransferredHostSelection(occupiedHost.getEntityUuid(), selections);
                        } else if (currentNpcId != null && !individual.equals(currentNpcId)) {
                            return NominatorResult.rejected("conflict", "该实体已经承载 NPC ID“" + currentNpcId + "”。");
                        } else {
                            if (currentNpcId == null) identities.bind(individual, host);
                            else identities.observe(host);
                        }
                    }
                    if (individual == null && groups.isEmpty()) selections.remove(entityUuid);
                    else if (!selectionSame) selections.put(candidate);
                }
            }
            return NominatorResult.accepted("实体指名已保存。");
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
        if (!authorized) return NominatorResult.rejected("permission_denied", "没有使用指名器的权限。");
        if (stack == null || project == null || identities == null)
            return NominatorResult.rejected("invalid_request", "物品、项目或持久化数据不可用。");
        try {
            String exact = blank(itemId) ? null : itemId.trim();
            String exactGroup = blank(exactGroupId) ? null : exactGroupId.trim();
            List<String> fuzzy = cleanGroups(fuzzyGroupIds);
            if (exact == null && exactGroup == null && fuzzy.isEmpty())
                return NominatorResult.rejected("empty_selection", "请选择物品 ID 或至少一个物品组。");
            ItemStackDefinition definition = ItemStackDefinition.capture(stack);
            if (exact != null && project.getItem(exact) == null)
                return NominatorResult.rejected("unknown_item_id", "所选物品 ID 不在已加载项目中。");
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
        List<String> values = new ArrayList<String>();
        if (item != null) values.add("精确物品 ID“" + item + "”");
        if (exactGroup != null) values.add("精确物品组“" + exactGroup + "”");
        for (String group : fuzzy) values.add("模糊物品组“" + group + "”（仅注册名）");
        return "已绑定：" + join(values) + "。";
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
        return exception.getMessage() == null ? "服务器拒绝了该请求。" : exception.getMessage();
    }
}
