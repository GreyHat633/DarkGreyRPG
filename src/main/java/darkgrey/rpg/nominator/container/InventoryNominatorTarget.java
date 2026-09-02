package darkgrey.rpg.nominator.container;

import net.minecraft.entity.player.EntityPlayer;
import net.minecraft.inventory.IInventory;
import net.minecraft.item.ItemStack;

import darkgrey.rpg.content.ModItems;

/** The transient, one-stack target inventory used by the nominator container. */
public final class InventoryNominatorTarget implements IInventory {

    private ItemStack stack;

    @Override
    public int getSizeInventory() {
        return 1;
    }

    @Override
    public ItemStack getStackInSlot(int slot) {
        return slot == 0 ? stack : null;
    }

    @Override
    public ItemStack decrStackSize(int slot, int amount) {
        if (slot != 0 || stack == null || amount <= 0) return null;
        ItemStack result;
        if (stack.stackSize <= amount) {
            result = stack;
            stack = null;
        } else {
            result = stack.splitStack(amount);
            if (stack.stackSize <= 0) stack = null;
        }
        markDirty();
        return result;
    }

    @Override
    public ItemStack getStackInSlotOnClosing(int slot) {
        if (slot != 0) return null;
        ItemStack result = stack;
        stack = null;
        return result;
    }

    /** Atomically takes the target for server-side close handling. */
    public ItemStack takeStack() {
        return getStackInSlotOnClosing(0);
    }

    @Override
    public void setInventorySlotContents(int slot, ItemStack value) {
        if (slot != 0) return;
        if (value == null) {
            stack = null;
        } else if (isItemValidForSlot(0, value)) {
            stack = value;
            if (stack.stackSize > getInventoryStackLimit()) stack.stackSize = getInventoryStackLimit();
            if (stack.stackSize <= 0) stack = null;
        }
        markDirty();
    }

    @Override
    public String getInventoryName() {
        return "container.darkgrey_rpg.nominator_target";
    }

    @Override
    public boolean hasCustomInventoryName() {
        return false;
    }

    @Override
    public int getInventoryStackLimit() {
        return 1;
    }

    @Override
    public void markDirty() {}

    @Override
    public boolean isUseableByPlayer(EntityPlayer player) {
        return player != null;
    }

    @Override
    public void openInventory() {}

    @Override
    public void closeInventory() {}

    @Override
    public boolean isItemValidForSlot(int slot, ItemStack value) {
        return slot == 0 && value != null && value.getItem() != null && value.getItem() != ModItems.nominator;
    }
}
