package darkgrey.rpg.item;

import java.util.List;

import net.minecraft.entity.player.EntityPlayer;
import net.minecraft.item.Item;
import net.minecraft.item.ItemStack;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.world.World;

import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.entitytools.CopierState;

/** Multi-template entity copier. All capture/spawn mutations are server-side. */
public final class ItemCopier extends Item {

    private static final String STATE_KEY = "dgr_copier";

    public static CopierState loadState(ItemStack stack) {
        requireStack(stack);
        CopierState state = new CopierState();
        NBTTagCompound root = stack.getTagCompound();
        if (root != null && root.hasKey(STATE_KEY, 10)) state.readFromNBT(root.getCompoundTag(STATE_KEY));
        return state;
    }

    public static void saveState(ItemStack stack, CopierState state) {
        requireStack(stack);
        if (state == null) throw new IllegalArgumentException("Copier state is required.");
        NBTTagCompound root = stack.getTagCompound();
        if (root == null) root = new NBTTagCompound();
        root.setTag(STATE_KEY, state.writeToNBT());
        stack.setTagCompound(root);
    }

    @Override
    public ItemStack onItemRightClick(ItemStack stack, World world, EntityPlayer player) {
        if (world.isRemote) DarkGreyRpg.proxy.openCopierGui(stack);
        return stack;
    }

    @Override
    public boolean onItemUse(ItemStack stack, EntityPlayer player, World world, int x, int y, int z, int side,
        float hitX, float hitY, float hitZ) {
        // The Forge block-interaction event performs the server-authoritative spawn.
        // Returning true here keeps a block click from falling through to the air-only template GUI.
        return true;
    }

    @Override
    @SuppressWarnings({ "rawtypes", "unchecked" })
    public void addInformation(ItemStack stack, EntityPlayer player, List lines, boolean advanced) {
        try {
            CopierState state = loadState(stack);
            int count = state.getTemplates()
                .size();
            lines.add("已保存模板: " + count);
            if (count > 0) lines.add("当前选择: " + (state.getSelectedIndex() + 1) + "/" + count);
            lines.add("右键生物保存；右键方块生成；右键空气管理模板。");
        } catch (RuntimeException exception) {
            lines.add("§c模板数据损坏，已禁止使用");
        }
    }

    private static void requireStack(ItemStack stack) {
        if (stack == null) throw new IllegalArgumentException("Copier ItemStack is required.");
    }
}
