package darkgrey.rpg.diagnostics;

import java.util.ArrayList;
import java.util.List;
import java.util.UUID;

import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;
import net.minecraft.server.MinecraftServer;
import net.minecraft.world.WorldServer;

import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryRepeatEligibility;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryRepeatPolicy;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryStartConfiguration;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryStatus;

/** Detached, bounded, tolerant projection. It never restores a runtime or writes a player record. */
public final class PlayerStateInspection {

    public static final int PAGE_SIZE = 16;

    private PlayerStateInspection() {}

    public static NBTTagCompound query(String name, int page) {
        NBTTagCompound result = new NBTTagCompound();
        result.setString("name", name);
        result.setInteger("page", page);
        result.setLong("queried", System.currentTimeMillis());
        if (name == null || !name.matches("[A-Za-z0-9_]{1,16}") || page < 0 || page > 100000) {
            result.setString("error", "请输入有效的玩家 ID / 玩家名。");
            return result;
        }
        UUID id;
        try {
            id = PlayerNameIndex.lookup(name);
        } catch (RuntimeException failure) {
            result.setString("error", "无法读取本服名称记录。");
            return result;
        }
        if (id == null) {
            result.setString("error", "无法从本服现有记录定位该玩家。");
            return result;
        }
        MinecraftServer server = MinecraftServer.getServer();
        for (Object value : server.getConfigurationManager().playerEntityList)
            if (id.equals(((EntityPlayerMP) value).getUniqueID())) result.setBoolean("online", true);
        List<NBTTagCompound> rows = new ArrayList<NBTTagCompound>();
        WorldServer world = server.worldServerForDimension(0);
        ProjectSnapshot project = DarkGreyRpg.getProjectRepository()
            .getSnapshot();
        try {
            NBTTagCompound sessions = ReadOnlyStateSource
                .read(world, darkgrey.rpg.session.persistence.CanonicalSessionSavedData.DATA_NAME);
            if (sessions != null) {
                collect(
                    rows,
                    "Story",
                    sessions.getCompoundTag("stories"),
                    id,
                    sessions.getString("diagnostic_source"),
                    project);
                collect(
                    rows,
                    "Session",
                    sessions.hasKey("sessions", 10) ? sessions.getCompoundTag("sessions") : sessions,
                    id,
                    sessions.getString("diagnostic_source"),
                    project);
                collectContinuations(rows, sessions, id, project);
            }
        } catch (Exception failure) {
            result.setString("error", "Story / Session 存档读取失败；其余可读数据仍显示。");
        }
        try {
            NBTTagCompound tasks = ReadOnlyStateSource
                .read(world, darkgrey.rpg.task.persistence.CanonicalTaskSavedData.DATA_NAME);
            if (tasks != null) collect(rows, "Task", tasks, id, tasks.getString("diagnostic_source"), project);
        } catch (Exception failure) {
            result.setString("error", result.getString("error") + " Task 存档读取失败。");
        }
        result.setInteger("total", rows.size());
        NBTTagList selected = new NBTTagList();
        for (int i = page * PAGE_SIZE; i < Math.min(rows.size(), (page + 1) * PAGE_SIZE); i++)
            selected.appendTag(rows.get(i));
        result.setTag("rows", selected);
        if (rows.isEmpty() && result.getString("error")
            .isEmpty()) result.setString("message", "已定位玩家，但没有 DGR 记录。");
        return result;
    }

    private static void collectContinuations(List<NBTTagCompound> rows, NBTTagCompound root, UUID player, ProjectSnapshot project) {
        NBTTagList pending = root.getTagList("continuations", 10);
        for (int i = 0; i < pending.tagCount(); i++) {
            NBTTagCompound entry = pending.getCompoundTagAt(i);
            if (!player.toString()
                .equals(entry.getString("player_uuid"))) continue;
            NBTTagCompound row = new NBTTagCompound();
            CanonicalGraphResource story = project.getCanonicalStory(entry.getString("story_id"));
            row.setString("title", "待续接 · " + PlayerStateSummary.resourceName(story));
            List<String> lines = new ArrayList<String>();
            lines.add("数据来源：" + root.getString("diagnostic_source"));
            lines.add("状态：已存待续接记录；未尝试执行或修复。");
            add(lines, "所属 Story", entry, "story_id");
            add(lines, "Placement", entry, "aggregate_placement_id");
            add(lines, "关联 Session", entry, "session_resource_id");
            lines.add("会话实例编号：" + entry.getLong("transport_id"));
            add(lines, "选定出口", entry, "selected_end_port_id");
            add(lines, "续接目标节点", entry, "target_node_id");
            add(lines, "续接目标端口", entry, "target_port_id");
            flags(lines, entry, "public_logic", "出口逻辑");
            NBTTagList details = new NBTTagList();
            for (String line : lines) details.appendTag(
                new net.minecraft.nbt.NBTTagString(line.length() > 300 ? line.substring(0, 300) + "…" : line));
            row.setTag("details", details);
            NBTTagList summary = new NBTTagList();
            summary.appendTag(new net.minecraft.nbt.NBTTagString("当前情况"));
            summary.appendTag(new net.minecraft.nbt.NBTTagString("所属故事：" + PlayerStateSummary.resourceName(story)));
            summary.appendTag(new net.minecraft.nbt.NBTTagString("会话已结束，存在等待故事继续推进的记录。"));
            summary.appendTag(new net.minecraft.nbt.NBTTagString("此页面仅查看记录，不执行续接或修复。"));
            row.setTag("summary", summary);
            rows.add(row);
        }
    }

    public static void collect(List<NBTTagCompound> rows, String kind, NBTTagCompound root, UUID player, String source,
        ProjectSnapshot project) {
        if (!root.hasKey("instances", 9)) {
            if (!root.hasNoTags()) throw new IllegalArgumentException("Missing instances");
            return;
        }
        NBTTagList instances = root.getTagList("instances", 10);
        for (int i = 0; i < instances.tagCount(); i++) {
            NBTTagCompound record = instances.getCompoundTagAt(i);
            if (!player.toString()
                .equals(record.getString("player_uuid"))) continue;
            NBTTagCompound runtime = "Story".equals(kind) ? record.getCompoundTag("runtime") : record;
            String resourceId = record.getString(
                "Story".equals(kind) ? "story_id"
                    : "Session".equals(kind) ? "session_resource_id" : "task_resource_id");
            CanonicalGraphResource resource = "Story".equals(kind) ? project.getCanonicalStory(resourceId)
                : "Session".equals(kind) ? project.getCanonicalSession(resourceId)
                    : project.getCanonicalTask(resourceId);
            NBTTagCompound row = new NBTTagCompound();
            row.setString("title", ("Story".equals(kind) ? "故事" : "Session".equals(kind) ? "会话" : "任务") + " · " + PlayerStateSummary.resourceName(resource));
            List<String> lines = new ArrayList<String>();
            lines.add("数据来源：" + source);
            lines.add("资源：" + resourceId);
            lines.add("状态：" + status(runtime.getString("status")));
            add(lines, "所属 Story", record, "story_id");
            add(lines, "所属 Story", record, "story_instance_id");
            add(lines, "Placement", record, "aggregate_placement_id");
            add(lines, "Placement", record, "task_node_placement_id");
            if (!"Story".equals(kind)) {
                String owner = record.getString("Session".equals(kind) ? "story_id" : "story_instance_id");
                String placement = record.getString("Session".equals(kind) ? "aggregate_placement_id" : "task_node_placement_id");
                CanonicalGraphResource story = project.getCanonicalStory(owner);
                if (story == null) lines.add("异常：所属 Story 资源缺失，保留原记录。");
                else {
                    darkgrey.rpg.graph.canonical.CanonicalGraphNode matched = null;
                    for (darkgrey.rpg.graph.canonical.CanonicalGraphNode node : story.getGraph().getNodes())
                        if (node.getId().equals(placement)) matched = node;
                    if (matched == null) lines.add("异常：所属 Story 中的 Placement 节点缺失，保留原记录。");
                    else if (!kind.toLowerCase(java.util.Locale.ROOT).equals(matched.getType())
                        || !matched.getProperties().containsKey("resource_id")
                        || !matched.getProperties().get("resource_id").isJsonPrimitive()
                        || !resourceId.equals(matched.getProperties().get("resource_id").getAsString()))
                        lines.add("异常：Placement 类型或关联资源与记录不一致，保留原记录。");
                }
            }
            add(lines, "当前节点", runtime, "current_node_id");
            add(lines, "开始入口", runtime, "trigger_port_id");
            add(lines, "等待种类", runtime, "wait_kind");
            add(lines, "等待对象", runtime, "wait_resource_id");
            if ("Session".equals(kind) && record.hasKey("line_page_index", 3))
                lines.add("作者页位置：" + (record.getInteger("line_page_index") + 1) + "（不推断离线屏幕）");
            if ("Session".equals(kind) && record.hasKey("transport_id", 4))
                lines.add("会话实例编号：" + record.getLong("transport_id"));
            if (record.hasKey("terminal_time", 4) && record.getLong("terminal_time") > 0)
                lines.add("终止时刻：" + time(record.getLong("terminal_time")));
            if (record.hasKey("settlement_time", 4)) lines.add("结算时刻：" + time(record.getLong("settlement_time")));
            if (resource == null) lines.add("异常：关联包或资源缺失，保留原记录。");
            else if (!runtime.getString("current_node_id")
                .isEmpty()) {
                    boolean found = false;
                    for (darkgrey.rpg.graph.canonical.CanonicalGraphNode node : resource.getGraph()
                        .getNodes())
                        if (node.getId()
                            .equals(runtime.getString("current_node_id"))) found = true;
                    if (!found) lines.add("异常：当前节点不存在于已安装资源。");
                }
            if ("Story".equals(kind) && resource != null) {
                try {
                    CanonicalStoryStartConfiguration configuration = CanonicalStoryStartConfiguration.parse(resource);
                    CanonicalStoryRepeatEligibility eligibility = CanonicalStoryRepeatEligibility.evaluate(
                        CanonicalStoryStatus.valueOf(runtime.getString("status")),
                        CanonicalStoryRepeatPolicy.fromJsonName(runtime.getString("repeat_policy")),
                        Long.valueOf(record.getLong("terminal_time")),
                        configuration.getRepeatCondition(),
                        java.time.Clock.systemUTC());
                    lines.add("重复资格：" + eligibility.explanation);
                    lines.add(
                        "服务器时区：" + darkgrey.rpg.story.canonical.runtime.CanonicalStoryRepeatCondition.getServerZone()
                            .getId());
                    if (eligibility.nextEligibleAt != null && eligibility.nextEligibleAt.longValue() > System.currentTimeMillis())
                        lines.add("下次可用：" + time(eligibility.nextEligibleAt.longValue()));
                } catch (RuntimeException invalid) {
                    lines.add("重复资格：配置或时间记录无法解释。");
                }
            }
            flags(lines, runtime, "logic", "已存逻辑");
            flags(lines, runtime, "internal_logic_values", "已存逻辑");
            flags(lines, runtime, "external_logic_inputs", "外部逻辑输入");
            flags(lines, runtime, "public_logic_outputs", "公开逻辑输出");
            flags(lines, runtime, "objective_statuses", "目标状态");
            flags(lines, runtime, "progress", "目标进度");
            flags(lines, runtime, "reward_states", "奖励记录");
            try {
                validate(kind, root, record);
            } catch (RuntimeException invalid) {
                lines.add("异常：该条记录未通过结构校验；以上仅为可读字段。未进行修复或恢复执行。");
            }
            lines.add("说明：快照不能证明历史崩溃原因。");
            row.setTag("summary", PlayerStateSummary.build(kind, record, resource, project, source, lines, java.time.Clock.systemUTC()));
            NBTTagList details = new NBTTagList();
            for (String line : lines) {
                if (details.tagCount() >= 100) break;
                details.appendTag(
                    new net.minecraft.nbt.NBTTagString(line.length() > 300 ? line.substring(0, 300) + "…" : line));
            }
            row.setTag("details", details);
            rows.add(row);
        }
    }

    private static void validate(String kind, NBTTagCompound root, NBTTagCompound record) {
        NBTTagCompound single = new NBTTagCompound();
        for (String key : root.func_150296_c())
            if (!"instances".equals(key) && !"diagnostic_source".equals(key)) single.setTag(
                key,
                root.getTag(key)
                    .copy());
        NBTTagList list = new NBTTagList();
        list.appendTag(record.copy());
        single.setTag("instances", list);
        if ("Story".equals(kind)) darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceNbtCodec.decode(single);
        else if ("Session".equals(kind)) darkgrey.rpg.session.instance.CanonicalSessionInstanceNbtCodec.decode(single);
        else darkgrey.rpg.task.instance.CanonicalTaskInstanceNbtCodec.decode(single);
    }

    private static void flags(List<String> lines, NBTTagCompound data, String key, String label) {
        NBTTagList list = data.getTagList(key, 10);
        for (int i = 0; i < Math.min(40, list.tagCount()); i++) {
            NBTTagCompound entry = list.getCompoundTagAt(i);
            String name = entry.hasKey("key", 8) ? entry.getString("key") : entry.getString("endpoint");
            String value = entry.hasKey("value", 8) ? status(entry.getString("value"))
                : entry.hasKey("value", 3) ? Integer.toString(entry.getInteger("value"))
                    : Boolean.toString(entry.getBoolean("value"));
            lines.add(label + " · " + name + "：" + value);
        }
        if (list.tagCount() > 40) lines.add(label + "：其余 " + (list.tagCount() - 40) + " 条未展开。");
    }

    private static void add(List<String> lines, String label, NBTTagCompound record, String key) {
        if (record.hasKey(key, 8) && !record.getString(key)
            .isEmpty()) lines.add(label + "：" + record.getString(key));
    }

    private static String status(String value) {
        return "ACTIVE".equals(value) ? "进行中"
            : "TERMINATED".equals(value) || "COMPLETED".equals(value) ? "正常结束"
                : "SETTLED".equals(value) ? "已结算" : "ERROR".equals(value) ? "异常" : value.isEmpty() ? "无法读取" : value;
    }

    private static String time(long value) {
        return PlayerStateSummary.time(value);
    }
}
