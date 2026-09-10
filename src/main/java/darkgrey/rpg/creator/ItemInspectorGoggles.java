package darkgrey.rpg.creator;

import net.minecraft.item.ItemArmor;
import net.minecraft.item.ItemStack;
import net.minecraftforge.common.util.EnumHelper;

public final class ItemInspectorGoggles extends ItemArmor {

    private static final ArmorMaterial MATERIAL = EnumHelper
        .addArmorMaterial("DGR_INSPECTOR", 0, new int[] { 0, 0, 0, 0 }, 0);

    public ItemInspectorGoggles() {
        super(MATERIAL, 0, 0);
        setMaxDamage(0);
    }

    @Override
    public String getArmorTexture(ItemStack stack, net.minecraft.entity.Entity entity, int slot, String type) {
        return "darkgrey_rpg:textures/models/armor/inspector_goggles.png";
    }

    @Override
    public boolean isDamageable() {
        return false;
    }

    @Override
    public boolean getIsRepairable(ItemStack a, ItemStack b) {
        return false;
    }
}
