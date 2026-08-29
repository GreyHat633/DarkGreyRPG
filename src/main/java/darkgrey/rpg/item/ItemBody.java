package darkgrey.rpg.item;

import java.util.List;

import net.minecraft.entity.player.EntityPlayer;
import net.minecraft.item.Item;
import net.minecraft.item.ItemStack;

/** 0.3.1.0 placeholder only; it deliberately does not create a native NPC. */
public final class ItemBody extends Item {

    @Override
    @SuppressWarnings({ "rawtypes", "unchecked" })
    public void addInformation(ItemStack stack, EntityPlayer player, List lines, boolean advanced) {
        lines.add("DGR 原生 NPC 的未来载体");
        lines.add("0.3.1.0 仅提供占位物品");
    }
}
