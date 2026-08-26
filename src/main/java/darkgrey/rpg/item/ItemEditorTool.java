package darkgrey.rpg.item;

import net.minecraft.item.Item;
import net.minecraft.item.ItemStack;

public final class ItemEditorTool extends Item {

    @Override
    public boolean hasEffect(ItemStack stack, int pass) {
        return true;
    }
}
