package darkgrey.rpg.item;

import java.util.List;

import net.minecraft.entity.player.EntityPlayer;
import net.minecraft.item.Item;
import net.minecraft.item.ItemStack;

/** Selection tool. The client opens a view; only request packets perform writes. */
public final class ItemNominator extends Item {

    @Override
    @SuppressWarnings({ "rawtypes", "unchecked" })
    public void addInformation(ItemStack stack, EntityPlayer player, List lines, boolean advanced) {
        lines.add("右键生物：指名剧情角色");
        lines.add("右键空气：指名背包物品");
        lines.add("仅 OP 或 DGR 编辑权限可用");
    }
}
