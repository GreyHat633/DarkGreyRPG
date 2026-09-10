package darkgrey.rpg.client;

import net.minecraft.client.settings.KeyBinding;

import org.lwjgl.input.Keyboard;

import cpw.mods.fml.client.registry.ClientRegistry;
import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import cpw.mods.fml.common.gameevent.TickEvent;

public final class ClientQuestKeyHandler {

    private final KeyBinding journalKey = new KeyBinding(
        "key.darkgrey_rpg.quest_journal",
        Keyboard.KEY_I,
        "key.categories.darkgrey_rpg");

    public ClientQuestKeyHandler() {
        ClientRegistry.registerKeyBinding(journalKey);
    }

    @SubscribeEvent
    public void onClientTick(TickEvent.ClientTickEvent event) {
        if (event.phase == TickEvent.Phase.END && journalKey.isPressed()) {
            net.minecraft.client.Minecraft mc = net.minecraft.client.Minecraft.getMinecraft();
            if (mc.thePlayer != null && mc.currentScreen == null)
                mc.displayGuiScreen(new darkgrey.rpg.client.gui.GuiCanonicalTaskScreen());
        }
    }
}
