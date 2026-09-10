package darkgrey.rpg.network;

import cpw.mods.fml.relauncher.Side;
import darkgrey.rpg.network.message.nominator.C2SNominatorEntityBind;
import darkgrey.rpg.network.message.nominator.C2SNominatorEntityOpen;
import darkgrey.rpg.network.message.nominator.C2SNominatorInventoryBind;
import darkgrey.rpg.network.message.nominator.C2SNominatorInventoryOpen;
import darkgrey.rpg.network.message.nominator.S2CNominatorEntityOpen;
import darkgrey.rpg.network.message.nominator.S2CNominatorInventoryOpen;

/** Registers nominator request packets; no client packet can mutate state. */
public final class NominatorNetwork {

    private static boolean registered;

    private NominatorNetwork() {}

    public static synchronized void registerCommon() {
        if (registered) return;
        DialogueNetwork.CHANNEL
            .registerMessage(C2SNominatorEntityBind.Handler.class, C2SNominatorEntityBind.class, 8, Side.SERVER);
        DialogueNetwork.CHANNEL
            .registerMessage(C2SNominatorInventoryBind.Handler.class, C2SNominatorInventoryBind.class, 9, Side.SERVER);
        DialogueNetwork.CHANNEL
            .registerMessage(C2SNominatorEntityOpen.Handler.class, C2SNominatorEntityOpen.class, 10, Side.SERVER);
        DialogueNetwork.CHANNEL
            .registerMessage(S2CNominatorEntityOpen.Handler.class, S2CNominatorEntityOpen.class, 11, Side.CLIENT);
        DialogueNetwork.CHANNEL
            .registerMessage(C2SNominatorInventoryOpen.Handler.class, C2SNominatorInventoryOpen.class, 12, Side.SERVER);
        DialogueNetwork.CHANNEL
            .registerMessage(S2CNominatorInventoryOpen.Handler.class, S2CNominatorInventoryOpen.class, 13, Side.CLIENT);
        DialogueNetwork.CHANNEL.registerMessage(
            darkgrey.rpg.network.message.nominator.C2SNominatorAction.Handler.class,
            darkgrey.rpg.network.message.nominator.C2SNominatorAction.class,
            19,
            Side.SERVER);
        DialogueNetwork.CHANNEL.registerMessage(
            darkgrey.rpg.network.message.nominator.S2CNominatorActionResult.Handler.class,
            darkgrey.rpg.network.message.nominator.S2CNominatorActionResult.class,
            20,
            Side.CLIENT);
        registered = true;
    }
}
