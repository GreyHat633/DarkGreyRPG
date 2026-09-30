package darkgrey.rpg.client.gui;

import java.util.ArrayList;
import java.util.List;

import net.minecraft.nbt.NBTTagCompound;

/** One read-only projection formatter shared by journal and tracker. */
public final class TaskObjectiveText {

    private TaskObjectiveText() {}

    public static List<String> referenceLines(NBTTagCompound goal) {
        List<String> lines = new ArrayList<String>();
        String text = "● " + goal.getString("text");
        if ("kill_entity".equals(goal.getString("type"))) text += "，要求 " + goal.getInteger("required") + " 只";
        lines.add(text);
        return lines;
    }

    /** Persisted completion summaries contain actual completed goals, never result-port labels. */
    public static List<String> completedLines(NBTTagCompound task) {
        List<String> result = new ArrayList<String>();
        result.add("完成次数：" + task.getLong("completion_count"));
        result.add("");
        result.add("§l已完成目标：");
        net.minecraft.nbt.NBTTagList objectives = task.getTagList("objectives", 10);
        if (objectives.tagCount() == 0) result.add("此旧记录未保存具体目标，无法还原当时的完成详情。");
        for (int i = 0; i < objectives.tagCount(); i++) {
            NBTTagCompound objective = objectives.getCompoundTagAt(i);
            result.add("● " + objective.getString("text"));
            if (objective.getInteger("required") > 1)
                result.add("已完成 " + objective.getInteger("current") + " / " + objective.getInteger("required"));
        }
        return result;
    }

    public static String reward(net.minecraft.nbt.NBTTagCompound row) {
        if (row.hasKey("error")) return row.getString("error");
        long amount = row.getInteger("amount");
        if ("xp".equals(row.getString("type"))) return "经验 " + (amount >= 0 ? "+" : "−") + Math.abs(amount);
        String name = "无";
        if (row.hasKey("stack", 10)) try {
            net.minecraft.item.ItemStack stack = net.minecraft.item.ItemStack
                .loadItemStackFromNBT(row.getCompoundTag("stack"));
            if (stack != null) {
                name = stack.getDisplayName();
                if (name == null || name.isEmpty()) name = "名称不可用";
            }
        } catch (RuntimeException error) {
            name = "名称不可用";
        }
        return (amount < 0 ? "扣除" : "") + name + " ×" + Math.abs(amount);
    }

    public static List<String> lines(NBTTagCompound objective) {
        List<String> result = new ArrayList<String>();
        result.add("● " + objective.getString("text"));
        String type = objective.getString("type");
        if ("kill_entity".equals(type) || "collect_item".equals(type) || "submit_item".equals(type)) {
            boolean held = objective.hasKey("held_count", 3);
            result.add(
                (held ? "持有 " : "进度 ") + objective.getInteger(held ? "held_count" : "current")
                    + " / "
                    + objective.getInteger("required"));
        }
        if (objective.getBoolean("submit")) result.add("请与 " + objective.getString("submit_actor") + " 交互提交物品");
        if (objective.hasKey("x", 3)) result.add(
            "坐标：" + objective.getInteger("x") + " / " + objective.getInteger("y") + " / " + objective.getInteger("z"));
        return result;
    }
}
