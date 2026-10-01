package darkgrey.rpg.creator;

import net.minecraft.item.ItemStack;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.item.identity.ItemIdentitySavedData;
import darkgrey.rpg.item.identity.ItemStackDefinition;
import darkgrey.rpg.task.forge.CanonicalTaskInventory;

/** Detached presentation only. Never changes bindings, stacks, or inventory matching. */
public final class TaskItemPreview {

    private TaskItemPreview() {}

    public static NBTTagCompound stack(ItemStack stack, String match) {
        NBTTagCompound row = new NBTTagCompound();
        row.setString("type", "item");
        row.setString("match", match);
        try {
            NBTTagCompound data = new NBTTagCompound();
            stack.copy()
                .writeToNBT(data);
            java.io.ByteArrayOutputStream bytes = new java.io.ByteArrayOutputStream();
            net.minecraft.nbt.CompressedStreamTools.write(data, new java.io.DataOutputStream(bytes));
            if (bytes.size() > 8192) row.setString("error", "物品显示数据过大");
            else row.setTag("stack", data);
        } catch (Exception invalid) {
            row.setString("error", "物品显示数据不可用");
        }
        return row;
    }

    public static NBTTagCompound project(CanonicalGraphNode objective, ItemIdentitySavedData bindings) {
        return TaskCandidateIndex.summary(objective, bindings);
    }

    public static void context(NBTTagCompound preview, String story, String placement, String objective,
        long activation, int dimension) {
        preview.setString("story", story);
        preview.setString("placement", placement);
        preview.setString("objective", objective);
        preview.setLong("activation", activation);
        preview.setInteger("dimension", dimension);
    }

    private static void add(NBTTagList rows, ItemStackDefinition definition, boolean fuzzy,
        CanonicalGraphNode objective, ItemIdentitySavedData bindings) {
        try {
            ItemStack example = definition.createStack(1);
            if (fuzzy && objective.getProperties()
                .get("metadata")
                .getAsJsonObject()
                .has("damage"))
                example.setItemDamage(
                    objective.getProperties()
                        .get("metadata")
                        .getAsJsonObject()
                        .get("damage")
                        .getAsInt());
            if (!CanonicalTaskInventory.matches(example, objective, bindings)) return;
            NBTTagCompound row = stack(example, fuzzy ? "允许同类物品；图标仅为示例，仍按任务条件检查" : "需匹配此物品的类型、耐久和附加数据");
            for (int i = 0; i < rows.tagCount(); i++) if (rows.getCompoundTagAt(i)
                .equals(row)) return;
            rows.appendTag(row);
        } catch (RuntimeException invalid) {
            NBTTagCompound row = new NBTTagCompound();
            row.setString("error", "绑定物品已不可用");
            rows.appendTag(row);
        }
    }
}
