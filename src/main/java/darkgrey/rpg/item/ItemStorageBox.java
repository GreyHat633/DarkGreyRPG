package darkgrey.rpg.item;

import java.util.List;

import net.minecraft.client.renderer.texture.IIconRegister;
import net.minecraft.entity.player.EntityPlayer;
import net.minecraft.item.Item;
import net.minecraft.item.ItemStack;
import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.util.IIcon;
import net.minecraft.world.World;

import cpw.mods.fml.relauncher.Side;
import cpw.mods.fml.relauncher.SideOnly;
import darkgrey.rpg.entitytools.StorageBoxState;
import darkgrey.rpg.entitytools.StoragePayload;

/** Survival carrier / creative single-template entity storage. */
public final class ItemStorageBox extends Item {

    private static final String STATE_KEY = "dgr_storage_box";
    @SideOnly(Side.CLIENT)
    private IIcon emptyIcon;
    @SideOnly(Side.CLIENT)
    private IIcon occupiedIcon;

    public static StorageBoxState loadState(ItemStack stack) {
        requireStack(stack);
        StorageBoxState state = new StorageBoxState();
        NBTTagCompound root = stack.getTagCompound();
        if (root != null && root.hasKey(STATE_KEY, 10)) state.readFromNBT(root.getCompoundTag(STATE_KEY));
        return state;
    }

    public static void saveState(ItemStack stack, StorageBoxState state) {
        requireStack(stack);
        if (state == null) throw new IllegalArgumentException("Storage Box state is required.");
        NBTTagCompound root = stack.getTagCompound();
        if (root == null) root = new NBTTagCompound();
        root.setTag(STATE_KEY, state.writeToNBT());
        stack.setTagCompound(root);
        stack.setItemDamage(state.isOccupied() ? 1 : 0);
    }

    @Override
    @SideOnly(Side.CLIENT)
    public void registerIcons(IIconRegister register) {
        emptyIcon = register.registerIcon("darkgrey_rpg:storage_box_open");
        occupiedIcon = register.registerIcon("darkgrey_rpg:storage_box_closed");
        itemIcon = emptyIcon;
    }

    @Override
    @SideOnly(Side.CLIENT)
    public IIcon getIconFromDamage(int damage) {
        return damage == 1 ? occupiedIcon : emptyIcon;
    }

    @Override
    public boolean onItemUse(ItemStack stack, EntityPlayer player, World world, int x, int y, int z, int side,
        float hitX, float hitY, float hitZ) {
        // EntityToolsRuntime handles release on the server after the vanilla use packet arrives.
        return true;
    }

    @Override
    @SuppressWarnings({ "rawtypes", "unchecked" })
    public void addInformation(ItemStack stack, EntityPlayer player, List lines, boolean advanced) {
        try {
            StorageBoxState state = loadState(stack);
            if (!state.isOccupied()) {
                lines.add("空置");
                lines.add("右键生物收纳");
                return;
            }
            StoragePayload payload = state.getPayload();
            lines.add(
                "已收纳: " + payload.getTemplate()
                    .getEntityType());
            lines.add(
                payload.getMode()
                    .name()
                    .equals("SURVIVAL") ? "生存搬运：放出后清空" : "创造模板：可无限生成");
        } catch (RuntimeException exception) {
            lines.add("§c收纳数据损坏，已禁止使用");
        }
    }

    private static void requireStack(ItemStack stack) {
        if (stack == null) throw new IllegalArgumentException("Storage Box ItemStack is required.");
    }
}
