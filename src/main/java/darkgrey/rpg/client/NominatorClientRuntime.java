package darkgrey.rpg.client;

import net.minecraft.client.Minecraft;
import net.minecraft.entity.Entity;
import net.minecraft.item.ItemStack;
import net.minecraft.util.MovingObjectPosition;
import net.minecraftforge.client.event.MouseEvent;

import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import darkgrey.rpg.content.ModItems;
import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.message.nominator.C2SNominatorEntityOpen;
import darkgrey.rpg.network.message.nominator.C2SNominatorInventoryOpen;

/** Routes Nominator right-clicks before vanilla entity and item-use fallbacks can overlap. */
public final class NominatorClientRuntime {

    @SubscribeEvent
    public void onMouseInput(MouseEvent event) {
        if (event.button != 1 || !event.buttonstate) return;
        Minecraft minecraft = Minecraft.getMinecraft();
        if (minecraft.currentScreen != null || minecraft.thePlayer == null) return;
        ItemStack held = minecraft.thePlayer.getHeldItem();
        if (held == null || held.getItem() != ModItems.nominator) return;
        MovingObjectPosition hit = minecraft.objectMouseOver;
        if (hit == null) return;
        if (hit.typeOfHit == MovingObjectPosition.MovingObjectType.ENTITY && hit.entityHit != null) {
            Entity target = hit.entityHit;
            DialogueNetwork.CHANNEL.sendToServer(new C2SNominatorEntityOpen(target.getEntityId()));
            event.setCanceled(true);
        } else if (hit.typeOfHit == MovingObjectPosition.MovingObjectType.MISS) {
            DialogueNetwork.CHANNEL.sendToServer(new C2SNominatorInventoryOpen());
            event.setCanceled(true);
        }
    }
}
