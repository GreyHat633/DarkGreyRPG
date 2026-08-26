package darkgrey.rpg.client;

import net.minecraft.client.settings.KeyBinding;

import org.lwjgl.input.Keyboard;

import cpw.mods.fml.client.registry.ClientRegistry;
import cpw.mods.fml.common.eventhandler.SubscribeEvent;
import cpw.mods.fml.common.gameevent.TickEvent;
import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.message.C2SQuestJournalRequest;

public final class ClientQuestKeyHandler {

    private final KeyBinding journalKey = new KeyBinding(
        "key.darkgrey_rpg.quest_journal",
        Keyboard.KEY_J,
        "key.categories.darkgrey_rpg");

    public ClientQuestKeyHandler() {
        ClientRegistry.registerKeyBinding(journalKey);
    }

    @SubscribeEvent
    public void onClientTick(TickEvent.ClientTickEvent event) {
        if (event.phase == TickEvent.Phase.END && journalKey.isPressed()) {
            DialogueNetwork.CHANNEL.sendToServer(new C2SQuestJournalRequest());
        }
    }
}
