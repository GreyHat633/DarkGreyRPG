package darkgrey.rpg.nominator.container;

import net.minecraft.entity.player.EntityPlayer;
import net.minecraft.world.World;

import cpw.mods.fml.common.network.IGuiHandler;
import darkgrey.rpg.DarkGreyRpg;

/** Creates the paired server/client target-slot container for the Item Nominator. */
public final class NominatorGuiHandler implements IGuiHandler {

    public static final int ITEM_NOMINATOR = 1;

    @Override
    public Object getServerGuiElement(int id, EntityPlayer player, World world, int x, int y, int z) {
        return id == ITEM_NOMINATOR ? new ContainerNominatorInventory(player) : null;
    }

    @Override
    public Object getClientGuiElement(int id, EntityPlayer player, World world, int x, int y, int z) {
        if (id != ITEM_NOMINATOR) return null;
        return DarkGreyRpg.proxy.createNominatorInventoryGui(new ContainerNominatorInventory(player));
    }
}
