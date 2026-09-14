package darkgrey.rpg.client.session;

import net.minecraft.client.Minecraft;
import net.minecraftforge.client.event.GuiScreenEvent;

import org.lwjgl.opengl.GL11;

import cpw.mods.fml.common.eventhandler.EventPriority;
import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import cpw.mods.fml.common.gameevent.TickEvent;
import darkgrey.rpg.client.CanonicalTaskClientStore;
import darkgrey.rpg.client.gui.GuiCanonicalSessionScreen;

/** Any non-DGR foreground GUI exclusively owns input. No GUI-open cancellation. */
public final class CanonicalPresentationBridge {

    @SubscribeEvent(priority = EventPriority.LOWEST)
    public void tick(TickEvent.ClientTickEvent event) {
        if (event.phase != TickEvent.Phase.END) return;
        CanonicalTaskClientStore.synchronizeWorld(Minecraft.getMinecraft().theWorld);
        CanonicalSessionClientController.restoreForeground();
        darkgrey.rpg.title.CanonicalTitleClient.tick();
    }

    @SubscribeEvent
    public void hud(net.minecraftforge.client.event.RenderGameOverlayEvent.Post event) {
        if (event.type != net.minecraftforge.client.event.RenderGameOverlayEvent.ElementType.ALL) return;
        if (Minecraft.getMinecraft().currentScreen == null)
            CanonicalSessionClientController.drawUnderlay(event.partialTicks);
        darkgrey.rpg.title.CanonicalTitleClient.draw();
    }

    @SubscribeEvent
    public void afterGui(GuiScreenEvent.DrawScreenEvent.Post event) {
        darkgrey.rpg.title.CanonicalTitleClient.draw();
    }

    @SubscribeEvent
    public void beforeGui(GuiScreenEvent.DrawScreenEvent.Pre event) {
        if (event.gui instanceof GuiCanonicalSessionScreen || Minecraft.getMinecraft().theWorld == null) return;
        GL11.glPushAttrib(GL11.GL_ALL_ATTRIB_BITS);
        GL11.glPushMatrix();
        try {
            CanonicalSessionClientController.drawUnderlay(event.renderPartialTicks);
        } finally {
            GL11.glPopMatrix();
            GL11.glPopAttrib();
        }
    }
}
