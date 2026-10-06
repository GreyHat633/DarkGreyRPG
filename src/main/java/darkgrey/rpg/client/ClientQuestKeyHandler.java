package darkgrey.rpg.client;

import net.minecraft.client.settings.KeyBinding;

import org.lwjgl.input.Keyboard;

import cpw.mods.fml.client.registry.ClientRegistry;
import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import cpw.mods.fml.common.gameevent.TickEvent;

public final class ClientQuestKeyHandler {

    private float lastVanillaMusicVolume = Float.NaN;

    private void synchronizeVanillaMusicVolume() {
        net.minecraft.client.Minecraft mc = net.minecraft.client.Minecraft.getMinecraft();
        if (mc.gameSettings == null) return;
        float volume = mc.gameSettings.getSoundLevel(net.minecraft.client.audio.SoundCategory.MUSIC);
        if (Float.compare(volume, lastVanillaMusicVolume) == 0) return;
        // 1.7.10 GameSettings notifies SoundManager before storing the new category value.
        // Reapply after that write, so an already-playing vanilla track uses the actual slider value.
        // SoundHandler limits this to its MUSIC category; DGR's independent sources are untouched.
        mc.getSoundHandler()
            .setSoundLevel(net.minecraft.client.audio.SoundCategory.MUSIC, volume);
        lastVanillaMusicVolume = volume;
    }

    private static final KeyBinding journalKey = new KeyBinding(
        "key.darkgrey_rpg.quest_journal",
        Keyboard.KEY_I,
        "key.categories.darkgrey_rpg");

    private static final KeyBinding settingsKey = new KeyBinding(
        "key.darkgrey_rpg.settings",
        Keyboard.KEY_P,
        "key.categories.darkgrey_rpg");

    public ClientQuestKeyHandler() {
        ClientRegistry.registerKeyBinding(packageKey);
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

    private static final KeyBinding packageKey = new KeyBinding(
        "key.darkgrey_rpg.packages",
        Keyboard.KEY_O,
        "key.categories.darkgrey_rpg");

    public static int historyKeyCode() {
        return historyKey.getKeyCode();
    }

    public static int packageKeyCode() {
        return packageKey.getKeyCode();
    }

    public static void clearPackageKey() {
        KeyBinding.setKeyBindState(packageKey.getKeyCode(), false);
        while (packageKey.isPressed()) {
            // Consume queued presses so closing cannot immediately reopen the manager.
        }
    }

    @SubscribeEvent
    public void onClientTick(TickEvent.ClientTickEvent event) {
        if (event.phase == TickEvent.Phase.END && packageKey.isPressed()) ClientPackageManager.open();
        if (event.phase == TickEvent.Phase.END) synchronizeVanillaMusicVolume();
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
