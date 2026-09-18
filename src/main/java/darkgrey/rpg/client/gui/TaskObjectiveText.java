package darkgrey.rpg.client.gui;

import java.util.ArrayList;
import java.util.List;

import net.minecraft.nbt.NBTTagCompound;

/** One read-only projection formatter shared by journal and tracker. */
public final class TaskObjectiveText {

    private TaskObjectiveText() {}

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
