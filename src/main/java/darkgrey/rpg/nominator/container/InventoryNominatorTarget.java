package darkgrey.rpg.nominator.container;

import net.minecraft.entity.player.EntityPlayer;
import net.minecraft.inventory.IInventory;
import net.minecraft.item.ItemStack;

import darkgrey.rpg.content.ModItems;

/** The transient two-slot inventory used by the nominator container. */
public final class InventoryNominatorTarget implements IInventory {

    private final ItemStack[] stacks = new ItemStack[2];

    @Override
    public int getSizeInventory() {
        return 2;
    }

    @Override
    public ItemStack getStackInSlot(int slot) {
        return slot >= 0 && slot < stacks.length ? stacks[slot] : null;
    }

    @Override
    public ItemStack decrStackSize(int slot, int amount) {
        if (slot < 0 || slot >= stacks.length || stacks[slot] == null || amount <= 0) return null;
        ItemStack result;
        if (stacks[slot].stackSize <= amount) {
            result = stacks[slot];
            stacks[slot] = null;
        } else {
            result = stacks[slot].splitStack(amount);
            if (stacks[slot].stackSize <= 0) stacks[slot] = null;
        }
        markDirty();
        return result;
    }

    @Override
    public ItemStack getStackInSlotOnClosing(int slot) {
        if (slot < 0 || slot >= stacks.length) return null;
        ItemStack result = stacks[slot];
        stacks[slot] = null;
        return result;
    }

    /** Atomically takes the target for server-side close handling. */
    public ItemStack takeStack() {
        return getStackInSlotOnClosing(0);
    }

    @Override
    public void setInventorySlotContents(int slot, ItemStack value) {
        if (slot < 0 || slot >= stacks.length) return;
        if (value == null) {
            stacks[slot] = null;
        } else if (isItemValidForSlot(slot, value)) {
            stacks[slot] = value;
            if (stacks[slot].stackSize > getInventoryStackLimit()) stacks[slot].stackSize = getInventoryStackLimit();
            if (stacks[slot].stackSize <= 0) stacks[slot] = null;
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
        return slot >= 0 && slot < stacks.length
            && value != null
            && value.getItem() != null
            && value.getItem() != ModItems.nominator;
    }
}
