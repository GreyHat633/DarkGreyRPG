package darkgrey.rpg.nominator.container;

import java.util.UUID;

import net.minecraft.entity.item.EntityItem;
import net.minecraft.entity.player.EntityPlayer;
import net.minecraft.entity.player.InventoryPlayer;
import net.minecraft.entity.player.PlayerCapabilities;
import net.minecraft.item.Item;
import net.minecraft.item.ItemStack;
import net.minecraft.util.ChunkCoordinates;
import net.minecraft.util.IChatComponent;

import com.mojang.authlib.GameProfile;

import darkgrey.rpg.content.ModItems;

/** Pure inventory/container accounting probe; real drag interaction remains a live Minecraft gate. */
public final class NominatorItemContainerProbe {

    private NominatorItemContainerProbe() {}

    public static void main(String[] args) throws Exception {
        Item priorNominator = ModItems.nominator;
        try {
            Item nominator = new Item().setMaxStackSize(1);
            Item ordinary = new Item().setMaxStackSize(64);
            Item filler = new Item().setMaxStackSize(64);
            ModItems.nominator = nominator;

            ProbePlayer player = player();
            ContainerNominatorInventory container = new ContainerNominatorInventory(player);
            require(container.inventorySlots.size() == 38, "two targets plus 36 player slots");
            player.inventory.mainInventory[0] = new ItemStack(ordinary, 5);
            require(total(player, ordinary) == 5, "source count");
            require(container.transferStackInSlot(player, 29) != null, "shift into target");
            require(
                container.getTargetInventory()
                    .getStackInSlot(0) != null
                    && container.getTargetInventory()
                        .getStackInSlot(0).stackSize == 1
                    && total(player, ordinary) == 4,
                "target move preserves count target=" + (container.getTargetInventory()
                    .getStackInSlot(0) == null ? "null"
                        : container.getTargetInventory()
                            .getStackInSlot(0).stackSize)
                    + " player="
                    + total(player, ordinary));
            require(container.transferStackInSlot(player, 0) != null, "shift target back");
            require(
                container.getTargetInventory()
                    .getStackInSlot(0) == null && total(player, ordinary) == 5,
                "target return preserves count");

            player.inventory.mainInventory[1] = new ItemStack(nominator, 1);
            require(container.transferStackInSlot(player, 30) == null, "nominator rejected by target");
            require(player.inventory.mainInventory[1].stackSize == 1, "rejected tool retained");

            container.getTargetInventory()
                .setInventorySlotContents(0, new ItemStack(ordinary, 1));
            int beforeReturn = total(player, ordinary);
            container.returnTargetToOwner(player);
            require(
                container.getTargetInventory()
                    .getStackInSlot(0) == null && total(player, ordinary) == beforeReturn + 1,
                "close return to inventory");

            for (int slot = 0; slot < player.inventory.mainInventory.length; slot++)
                player.inventory.mainInventory[slot] = new ItemStack(filler, 64);
            container.getTargetInventory()
                .setInventorySlotContents(0, new ItemStack(ordinary, 1));
            player.dropped = null;
            container.returnTargetToOwner(player);
            require(player.dropped != null && player.dropped.getItem() == ordinary, "full inventory drop fallback");
            require(
                container.getTargetInventory()
                    .getStackInSlot(0) == null,
                "drop fallback clears target once");

            player.inventory.mainInventory[0] = null;
            container.getTargetInventory()
                .setInventorySlotContents(ContainerNominatorInventory.UNBIND_SLOT, new ItemStack(ordinary, 1));
            int beforeSecond = total(player, ordinary);
            container.returnSlotToOwner(player, ContainerNominatorInventory.UNBIND_SLOT);
            container.returnSlotToOwner(player, ContainerNominatorInventory.UNBIND_SLOT);
            require(
                total(player, ordinary) == beforeSecond + 1 && container.getTargetInventory()
                    .getStackInSlot(ContainerNominatorInventory.UNBIND_SLOT) == null,
                "unbind return exactly once");

            System.out.println("NOMINATOR_ITEM_CONTAINER=PASS target-slot move return drop no-dup no-loss");
        } finally {
            ModItems.nominator = priorNominator;
        }
    }

    private static int total(ProbePlayer player, Item item) {
        int total = 0;
        for (ItemStack stack : player.inventory.mainInventory)
            if (stack != null && stack.getItem() == item) total += stack.stackSize;
        return total;
    }

    private static ProbePlayer player() throws Exception {
        Class<?> unsafeType = Class.forName("sun.misc.Unsafe");
        java.lang.reflect.Field field = unsafeType.getDeclaredField("theUnsafe");
        field.setAccessible(true);
        Object unsafe = field.get(null);
        ProbePlayer player = (ProbePlayer) unsafeType.getMethod("allocateInstance", Class.class)
            .invoke(unsafe, ProbePlayer.class);
        player.inventory = new InventoryPlayer(player);
        player.capabilities = new PlayerCapabilities();
        return player;
    }

    private static void require(boolean value, String label) {
        if (!value) throw new IllegalStateException("Probe failure: " + label);
    }

    private static final class ProbePlayer extends EntityPlayer {

        private ItemStack dropped;

        private ProbePlayer() {
            super(null, new GameProfile(UUID.fromString("11111111-1111-1111-1111-111111111111"), "probe"));
        }

        @Override
        public EntityItem dropPlayerItemWithRandomChoice(ItemStack stack, boolean randomOffset) {
            dropped = stack.copy();
            return null;
        }

        @Override
        public void addChatComponentMessage(IChatComponent message) {}

        @Override
        public void addChatMessage(IChatComponent message) {}

        @Override
        public ChunkCoordinates getPlayerCoordinates() {
            return new ChunkCoordinates(0, 0, 0);
        }

        @Override
        public boolean canCommandSenderUseCommand(int permissionLevel, String command) {
            return true;
        }
    }

}
