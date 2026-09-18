package darkgrey.rpg.client;

import net.minecraft.client.settings.KeyBinding;

import org.lwjgl.input.Keyboard;

import cpw.mods.fml.client.registry.ClientRegistry;
import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import cpw.mods.fml.common.gameevent.TickEvent;

public final class ClientQuestKeyHandler {

    private static final KeyBinding journalKey = new KeyBinding(
        "key.darkgrey_rpg.quest_journal",
        Keyboard.KEY_I,
        "key.categories.darkgrey_rpg");

    private static final KeyBinding settingsKey = new KeyBinding(
        "key.darkgrey_rpg.settings",
        Keyboard.KEY_P,
        "key.categories.darkgrey_rpg");

    public ClientQuestKeyHandler() {
        ClientRegistry.registerKeyBinding(historyKey);
        ClientRegistry.registerKeyBinding(settingsKey);
        darkgrey.rpg.client.gui.GuiDialogueSettings.loadPreferences();
        ClientRegistry.registerKeyBinding(journalKey);
    }

    public static int settingsKeyCode() {
        return settingsKey.getKeyCode();
    }

    public static int journalKeyCode() {
        return journalKey.getKeyCode();
    }

    private static final KeyBinding historyKey = new KeyBinding(
        "key.darkgrey_rpg.history",
        Keyboard.KEY_NONE,
        "key.categories.darkgrey_rpg");

    public static int historyKeyCode() {
        return historyKey.getKeyCode();
    }

    @SubscribeEvent
    public void onClientTick(TickEvent.ClientTickEvent event) {
        if (event.phase == TickEvent.Phase.END && historyKey.isPressed()) {
            net.minecraft.client.Minecraft mc = net.minecraft.client.Minecraft.getMinecraft();
            if (mc.thePlayer != null && (mc.currentScreen == null
                || mc.currentScreen instanceof darkgrey.rpg.client.gui.GuiCanonicalSessionScreen))
                mc.displayGuiScreen(new darkgrey.rpg.client.gui.GuiDialogueHistory());
        }
        if (event.phase == TickEvent.Phase.END && settingsKey.isPressed()) {
            net.minecraft.client.Minecraft mc = net.minecraft.client.Minecraft.getMinecraft();
            if (mc.thePlayer != null && (mc.currentScreen == null
                || mc.currentScreen instanceof darkgrey.rpg.client.gui.GuiCanonicalSessionScreen))
                mc.displayGuiScreen(new darkgrey.rpg.client.gui.GuiDialogueSettings());
        }
        if (event.phase == TickEvent.Phase.END && journalKey.isPressed()) {
            net.minecraft.client.Minecraft mc = net.minecraft.client.Minecraft.getMinecraft();
            if (mc.thePlayer != null && mc.currentScreen == null)
                mc.displayGuiScreen(new darkgrey.rpg.client.gui.GuiCanonicalTaskScreen());
        }
    }
}
