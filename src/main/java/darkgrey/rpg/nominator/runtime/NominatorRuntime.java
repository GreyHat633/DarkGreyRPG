package darkgrey.rpg.nominator.runtime;

import net.minecraft.entity.Entity;
import net.minecraft.entity.player.EntityPlayer;
import net.minecraft.item.ItemStack;
import net.minecraftforge.event.entity.player.EntityInteractEvent;

import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import darkgrey.rpg.content.ModItems;
import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.message.nominator.C2SNominatorEntityOpen;

/** Runtime event bridge for the entity-right-click presentation. */
public final class NominatorRuntime {

    @SubscribeEvent
    public void onEntityInteract(EntityInteractEvent event) {
        EntityPlayer player = event.entityPlayer;
        ItemStack held = player.getHeldItem();
        Entity target = event.target;
        if (held != null && held.getItem() == ModItems.nominator && target != null && player.worldObj.isRemote)
            DialogueNetwork.CHANNEL
                .sendToServer(new C2SNominatorEntityOpen(target.getEntityId(), target.getUniqueID()));
    }
}
