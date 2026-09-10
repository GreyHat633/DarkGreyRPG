package darkgrey.rpg.nominator.container;

import net.minecraft.entity.player.EntityPlayer;
import net.minecraft.entity.player.InventoryPlayer;
import net.minecraft.inventory.Container;
import net.minecraft.inventory.Slot;
import net.minecraft.item.ItemStack;

/** Server-authoritative nominator target plus the complete player inventory. */
public final class ContainerNominatorInventory extends Container {

    public static final int NOMINATE_SLOT = 0;
    public static final int UNBIND_SLOT = 1;
    public static final int TARGET_SLOT = NOMINATE_SLOT;
    /** Slot coordinates are presentation defaults; the client may re-layout them without changing indices. */
    public static final int TARGET_DEFAULT_X = 286;
    public static final int TARGET_DEFAULT_Y = 177;
    public static final int PLAYER_SLOT_START = 2;
    public static final int PLAYER_SLOT_END = 38;

    private final InventoryNominatorTarget targetInventory = new InventoryNominatorTarget();
    private final EntityPlayer owner;
    private boolean closeHandled;

    public ContainerNominatorInventory(InventoryPlayer inventory, EntityPlayer owner) {
        this.owner = owner;
        addSlotToContainer(new TargetSlot(targetInventory, NOMINATE_SLOT, TARGET_DEFAULT_X, TARGET_DEFAULT_Y));
        addSlotToContainer(new TargetSlot(targetInventory, UNBIND_SLOT, TARGET_DEFAULT_X, TARGET_DEFAULT_Y + 56));

        // Keep the vanilla player-inventory ordering: main inventory, then hotbar.
        for (int row = 0; row < 3; row++) for (int column = 0; column < 9; column++)
            addSlotToContainer(new Slot(inventory, column + row * 9 + 9, 8 + column * 18, 119 + row * 18));
        for (int column = 0; column < 9; column++)
            addSlotToContainer(new Slot(inventory, column, 8 + column * 18, 177));
    }

    public ContainerNominatorInventory(EntityPlayer owner) {
        this(owner.inventory, owner);
    }

    public InventoryNominatorTarget getTargetInventory() {
        return targetInventory;
    }

    @Override
    public boolean canInteractWith(EntityPlayer player) {
        return player != null && player == owner && !player.isDead;
    }

    @Override
    public ItemStack transferStackInSlot(EntityPlayer player, int slotIndex) {
        if (slotIndex < 0 || slotIndex >= inventorySlots.size()) return null;
        Slot slot = (Slot) inventorySlots.get(slotIndex);
        if (slot == null || !slot.getHasStack()) return null;

        ItemStack original = slot.getStack();
        ItemStack result = original.copy();
        if (slotIndex < PLAYER_SLOT_START) {
            if (!mergeItemStack(original, PLAYER_SLOT_START, PLAYER_SLOT_END, false)) return null;
        } else {
            Slot target = (Slot) inventorySlots.get(TARGET_SLOT);
            if (target.getHasStack() || !target.isItemValid(original)) return null;
            ItemStack single = original.copy();
            single.stackSize = 1;
            target.putStack(single);
            target.onSlotChanged();
            original.stackSize--;
        }

        if (original.stackSize == 0) slot.putStack(null);
        else slot.onSlotChanged();
        if (original.stackSize == result.stackSize) return null;
        slot.onPickupFromSlot(player, original);
        return result;
    }

    @Override
    public void onContainerClosed(EntityPlayer player) {
        super.onContainerClosed(player);
        if (closeHandled || player == null || player != owner || player.worldObj == null || player.worldObj.isRemote)
            return;
        closeHandled = true;

        returnTargetToOwner(player);
    }

    void returnTargetToOwner(EntityPlayer player) {
        returnSlotToOwner(player, NOMINATE_SLOT);
        returnSlotToOwner(player, UNBIND_SLOT);
    }

    public void returnSlotToOwner(EntityPlayer player, int slot) {
        if (player != owner) return;
        ItemStack target = targetInventory.getStackInSlotOnClosing(slot);
        if (target == null || target.stackSize <= 0) return;
        player.inventory.addItemStackToInventory(target);
        if (target.stackSize > 0) {
            player.dropPlayerItemWithRandomChoice(target, false);
        }
    }

    private static final class TargetSlot extends Slot {

        private TargetSlot(InventoryNominatorTarget inventory, int slot, int x, int y) {
            super(inventory, slot, x, y);
        }

        @Override
        public int getSlotStackLimit() {
            return 1;
        }

        @Override
        public boolean isItemValid(ItemStack stack) {
            return inventory.isItemValidForSlot(getSlotIndex(), stack);
        }
    }
}
