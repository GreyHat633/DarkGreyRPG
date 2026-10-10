package darkgrey.rpg.diagnostics;

import java.time.Clock;
import java.time.Instant;
import java.time.format.DateTimeFormatter;
import java.util.ArrayList;
import java.util.List;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;
import net.minecraft.nbt.NBTTagString;

import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.identity.ResourceAddress;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryRepeatCondition;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryRepeatEligibility;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryRepeatPolicy;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryStartConfiguration;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryStatus;

/** Readable projection of recorded state, without restoring or executing a runtime. */
final class PlayerStateSummary {

    static NBTTagList build(String kind, NBTTagCompound record, CanonicalGraphResource resource,
        ProjectSnapshot project, String source, List<String> technical, Clock clock) {
        List<String> lines = new ArrayList<String>();
        NBTTagCompound state = "Story".equals(kind) ? record.getCompoundTag("runtime") : record;
        String status = state.getString("status");
        boolean missingWait = false;
        lines.add("当前情况");
        lines.add("内容：" + resourceName(resource));
        lines.add("状态：" + stateName(kind, status));
        lines.add("数据来源：" + source);
        if (!"Story".equals(kind)) {
            String owner = record.getString("Session".equals(kind) ? "story_id" : "story_instance_id");
            CanonicalGraphResource story = project.getCanonicalStory(owner);
            lines.add("所属故事：" + resourceName(story));
        }
        if ("ACTIVE".equals(status)) {
            CanonicalGraphNode current = node(resource, state.getString("current_node_id"));
            if (!state.getString("current_node_id")
                .isEmpty()) lines.add("当前步骤：" + nodeName(current));
            if ("Session".equals(kind) && record.hasKey("line_page_index", 3))
                lines.add("当前台词：第 " + (record.getInteger("line_page_index") + 1) + " 句（存档位置）");
            lines.add("");
            lines.add("正在等待什么");
            String wait = state.getString("wait_kind");
            String waiting = "SESSION".equals(wait) ? "等待玩家完成会话"
                : "TASK".equals(wait) ? "等待任务完成"
                    : "CONDITION".equals(wait) ? "等待节点条件满足"
                        : "ACTION".equals(wait) ? "等待动作完成"
                            : "TITLE".equals(wait) ? "等待标题显示完成"
                                : "Session".equals(kind) ? "等待玩家推进当前会话"
                                    : "Task".equals(kind) ? "等待任务目标达成" : "当前没有记录外部等待";
            lines.add(waiting);
            String id = PlayerStateInspection.resourceId(
                state,
                "wait_resource_id",
                "SESSION".equals(wait) ? ResourceAddress.Kind.SESSION : ResourceAddress.Kind.TASK);
            if (!id.isEmpty()) {
                CanonicalGraphResource target = "SESSION".equals(wait) ? project.getCanonicalSession(id)
                    : "TASK".equals(wait) ? project.getCanonicalTask(id) : null;
                if ("SESSION".equals(wait) || "TASK".equals(wait)) {
                    lines.add("等待内容：" + resourceName(target));
                    missingWait = target == null;
                }
            }
        }
        if ("Task".equals(kind)) {
            NBTTagList objectives = state.getTagList("objective_statuses", 10);
            NBTTagList progress = state.getTagList("progress", 10);
            for (int i = 0; i < Math.min(30, objectives.tagCount()); i++) {
                NBTTagCompound objective = objectives.getCompoundTagAt(i);
                String id = objective.getString("key");
                CanonicalGraphNode definition = node(resource, id);
                String count = "";
                for (int j = 0; j < progress.tagCount(); j++) if (id.equals(
                    progress.getCompoundTagAt(j)
                        .getString("key")))
                    count = " · " + progress.getCompoundTagAt(j)
                        .getInteger("value");
                if (definition != null && definition.getProperties()
                    .containsKey("required"))
                    count += " / " + definition.getProperties()
                        .get("required")
                        .toString();
                lines.add(
                    "目标：" + objectiveName(definition, project)
                        + " · "
                        + stateName(kind, objective.getString("value"))
                        + count);
            }
        }
        long ended = record.getLong("terminal_time");
        if (ended > 0) lines.add("结束时间：" + time(ended));
        if (ended > 0 && !"Story".equals(kind)) lines.add(
            "服务器时区：" + CanonicalStoryRepeatCondition.getServerZone()
                .getId());
        if ("Story".equals(kind)) {
            lines.add("");
            lines.add("再次启动条件");
            if (resource == null) lines.add("资源缺失，无法判断是否可再次启动。");
            else try {
                CanonicalStoryStartConfiguration configuration = CanonicalStoryStartConfiguration.parse(resource);
                CanonicalStoryRepeatEligibility result = CanonicalStoryRepeatEligibility.evaluate(
                    CanonicalStoryStatus.valueOf(status),
                    CanonicalStoryRepeatPolicy.fromJsonName(state.getString("repeat_policy")),
                    Long.valueOf(ended),
                    configuration.getRepeatCondition(),
                    clock);
                switch (result.disposition) {
                    case ALREADY_ACTIVE:
                        lines.add("故事仍在进行，不能重复启动。");
                        break;
                    case ONCE_TERMINAL:
                        lines.add("该故事不允许重复。");
                        break;
                    case REPEATABLE_RESTART:
                        lines.add("已可再次启动，仍需触发启动条件。");
                        break;
                    case REPEAT_WAITING:
                        lines.add(
                            "cooldown".equals(
                                configuration.getRepeatCondition()
                                    .getType()) ? "尚在冷却。" : "尚未到计划开放时间。");
                        if (result.nextEligibleAt != null && result.nextEligibleAt.longValue() > clock.millis())
                            lines.add("可再次启动时间：" + time(result.nextEligibleAt.longValue()));
                        break;
                    case ERROR_TERMINAL:
                        lines.add("故事异常结束，不会自动重试。");
                        break;
                    default:
                        lines.add("时间记录或重复配置无效，无法判断。");
                        break;
                }
            } catch (RuntimeException invalid) {
                lines.add("配置或时间记录无法解释。");
            }
            lines.add(
                "服务器时区：" + CanonicalStoryRepeatCondition.getServerZone()
                    .getId());
        }
        lines.add("");
        lines.add("发现的问题");
        boolean problem = false;
        if (missingWait) {
            lines.add("等待的会话或任务资源缺失，无法解析名称。");
            problem = true;
        }
        for (String line : technical) if (line.startsWith("异常：")) {
            lines.add(
                line.replace("Placement", "资源引用")
                    .replace("Story", "故事"));
            problem = true;
        }
        if ("ERROR".equals(status)) {
            lines.add("记录显示异常结束；当前记录没有保存具体原因。");
            problem = true;
        }
        if (!problem) lines.add("当前记录未发现结构或资源引用异常。");
        NBTTagList output = new NBTTagList();
        for (String line : lines) {
            if (output.tagCount() >= 100) break;
            output.appendTag(new NBTTagString(line.length() > 300 ? line.substring(0, 300) + "…" : line));
        }
        return output;
    }

    static String resourceName(CanonicalGraphResource resource) {
        if (resource == null) return "资源已缺失（原始 ID 见技术详情）";
        String name = resource.getDisplayName();
        return name == null || name.trim()
            .isEmpty() || name.equals(resource.getId()) ? "名称未设置（原始 ID 见技术详情）" : name;
    }

    private static String stateName(String kind, String state) {
        if ("TERMINATED".equals(state)) return "故事已正常结束";
        if ("COMPLETED".equals(state)) return "已完成";
        if ("SETTLED".equals(state)) return "已完成并结算";
        if ("ACTIVE".equals(state)) return "进行中";
        if ("ERROR".equals(state) || "FAILED".equals(state)) return "异常结束";
        if ("INACTIVE".equals(state) || "PENDING".equals(state) || "LOCKED".equals(state)) return "尚未开始";
        return "无法识别（原始状态见技术详情）";
    }

    private static CanonicalGraphNode node(CanonicalGraphResource resource, String id) {
        if (resource != null) for (CanonicalGraphNode node : resource.getGraph()
            .getNodes())
            if (node.getId()
                .equals(id)) return node;
        return null;
    }

    private static String objectiveName(CanonicalGraphNode node, final ProjectSnapshot project) {
        if (node != null && node.getProperties()
            .containsKey("description")) try {
                String description = node.getProperties()
                    .get("description")
                    .getAsString();
                if (!description.trim()
                    .isEmpty())
                    return darkgrey.rpg.session.runtime.DynamicContentText
                        .resolve(description, new darkgrey.rpg.session.runtime.DynamicContentText.Resolver() {

                            public String resolve(String type, String item) {
                                if ("actor_name".equals(type)) return project.getActor(item) == null ? "角色引用缺失"
                                    : project.getActor(item)
                                        .getDisplayName();
                                if ("item_name".equals(type)) return project.getItem(item) != null
                                    ? project.getItem(item)
                                        .getDisplayName()
                                    : project.getItemGroup(item) != null ? project.getItemGroup(item)
                                        .getDisplayName() : "物品引用缺失";
                                return "player_name".equals(type) ? "〈玩家名称〉"
                                    : "player_level".equals(type) ? "〈经验等级〉" : "〈物品数量〉";
                            }
                        });
            } catch (RuntimeException invalid) {
                return "目标说明无法读取";
            }
        return nodeName(node);
    }

    private static String nodeName(CanonicalGraphNode node) {
        if (node == null) return "节点缺失或无法定位";
        String name = node.getDisplayName();
        if (name != null && !name.isEmpty() && !name.equals(node.getId())) return name;
        String type = node.getType();
        return "line".equals(type) ? "台词"
            : "choice".equals(type) ? "选项"
                : "condition".equals(type) ? "条件判断"
                    : "session".equals(type) ? "会话"
                        : "task".equals(type) ? "任务"
                            : "objective".equals(type) ? "任务目标"
                                : "title".equals(type) ? "标题"
                                    : "action".equals(type) ? "执行动作"
                                        : "terminate".equals(type) ? "结束" : "未命名步骤（技术详情可定位）";
    }

    static String time(long value) {
        return DateTimeFormatter.ofPattern("yyyy-MM-dd HH:mm:ss")
            .format(
                Instant.ofEpochMilli(value)
                    .atZone(CanonicalStoryRepeatCondition.getServerZone()));
    }
}
