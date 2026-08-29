package darkgrey.rpg.network;

import cpw.mods.fml.relauncher.Side;
import darkgrey.rpg.network.message.entitytools.C2SCopierTemplateAction;

/** Registers server-authoritative entity-tool GUI actions. */
public final class EntityToolsNetwork {

    private static boolean registered;

    private EntityToolsNetwork() {}

    public static synchronized void registerCommon() {
        if (registered) return;
        DialogueNetwork.CHANNEL
            .registerMessage(C2SCopierTemplateAction.Handler.class, C2SCopierTemplateAction.class, 14, Side.SERVER);
        registered = true;
    }
}
