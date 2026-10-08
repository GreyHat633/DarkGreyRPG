package darkgrey.rpg.network;

import cpw.mods.fml.relauncher.Side;
import darkgrey.rpg.network.message.nominator.C2SNominatorEntityOpen;
import darkgrey.rpg.network.message.nominator.C2SNominatorInventoryOpen;
import darkgrey.rpg.network.message.nominator.S2CNominatorEntityOpen;
import darkgrey.rpg.network.message.nominator.S2CNominatorInventoryOpen;

/** Registers current nominator operations and snapshots. */
public final class NominatorNetwork {

    private static boolean registered;

    private NominatorNetwork() {}

    public static synchronized void registerCommon() {
        if (registered) return;
        // Retired write discriminators 8/9 remain unassigned.
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
