package darkgrey.rpg.creator;

import java.util.LinkedHashSet;
import java.util.Set;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;

import darkgrey.rpg.graph.canonical.CanonicalGraphConnection;
import darkgrey.rpg.graph.canonical.CanonicalGraphInterfaceKind;
import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.item.identity.ItemIdentitySavedData;
import darkgrey.rpg.item.identity.ItemStackDefinition;
import darkgrey.rpg.task.runtime.CanonicalTaskRewardPackage;

/** Bounded, direct-edge display projection; never executes a reward. */
public final class TaskRewardPreview {

    private TaskRewardPreview() {}

    public static NBTTagList project(CanonicalGraphResource resource, String objective,
        ItemIdentitySavedData bindings) {
        NBTTagList result = new NBTTagList();
        Set<String> direct = new LinkedHashSet<String>();
        for (CanonicalGraphConnection edge : resource.getGraph()
            .getConnections())
            if (edge.getInterfaceKind() == CanonicalGraphInterfaceKind.LOGIC && objective.equals(edge.getFromNodeId())
                && "logic_status".equals(edge.getFromPortId())
                && "logic_in".equals(edge.getToPortId())) direct.add(edge.getToNodeId());
        // Resource node order is stable even if duplicate edges are reordered.
        for (CanonicalGraphNode node : resource.getGraph()
            .getNodes()) {
            if (!"reward".equals(node.getType()) || !direct.contains(node.getId())) continue;
            for (CanonicalTaskRewardPackage.Entry entry : CanonicalTaskRewardPackage.read(node)) {
                if (entry.getAmount() == 0) continue;
                NBTTagCompound row = new NBTTagCompound();
                if (result.tagCount() == 128) {
                    row.setString("error", "奖励预览过多，后续条目未展开");
                    result.appendTag(row);
                    return result;
                }
                row.setString("type", entry.getType());
                row.setInteger("amount", entry.getAmount());
                if ("item".equals(entry.getType())) {
                    ItemStackDefinition definition = bindings.getItem(entry.getItem());
                    if (definition == null) row.setString("error", "奖励物品未绑定");
                    if (definition != null) try {
                        NBTTagCompound stack = new NBTTagCompound();
                        definition.createStack(1)
                            .writeToNBT(stack);
                        // Preserve mod/NBT names, but bound individual display payloads.
                        java.io.ByteArrayOutputStream bytes = new java.io.ByteArrayOutputStream();
                        net.minecraft.nbt.CompressedStreamTools.write(stack, new java.io.DataOutputStream(bytes));
                        if (bytes.size() <= 8192) row.setTag("stack", stack);
                        else row.setString("error", "物品显示数据过大");
                    } catch (Exception error) {
                        row.setString("error", "物品显示数据不可用");
                    }
                }
                result.appendTag(row);
            }
        }
        return result;
    }
}
