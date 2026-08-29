package darkgrey.rpg.network;

import cpw.mods.fml.common.network.NetworkRegistry;
import cpw.mods.fml.common.network.simpleimpl.SimpleNetworkWrapper;
import cpw.mods.fml.relauncher.Side;
import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.network.message.C2SDialogueAction;
import darkgrey.rpg.network.message.C2SQuestJournalRequest;
import darkgrey.rpg.network.message.S2CDialogueClose;
import darkgrey.rpg.network.message.S2CDialogueFrame;
import darkgrey.rpg.network.message.S2CQuestJournal;
import darkgrey.rpg.network.message.canonical.CanonicalSessionAction;
import darkgrey.rpg.network.message.canonical.CanonicalSessionClose;
import darkgrey.rpg.network.message.canonical.CanonicalSessionFrame;
import darkgrey.rpg.session.forge.CanonicalSessionActionHandler;
import darkgrey.rpg.session.forge.CanonicalSessionCloseHandler;
import darkgrey.rpg.session.forge.CanonicalSessionFrameHandler;

public final class DialogueNetwork {

    public static final SimpleNetworkWrapper CHANNEL = NetworkRegistry.INSTANCE.newSimpleChannel(DarkGreyRpg.MOD_ID);
    private static boolean registered;

    private DialogueNetwork() {}

    public static synchronized void registerCommon() {
        if (registered) return;
        CHANNEL.registerMessage(C2SDialogueAction.Handler.class, C2SDialogueAction.class, 0, Side.SERVER);
        CHANNEL.registerMessage(S2CDialogueFrame.Handler.class, S2CDialogueFrame.class, 1, Side.CLIENT);
        CHANNEL.registerMessage(S2CDialogueClose.Handler.class, S2CDialogueClose.class, 2, Side.CLIENT);
        CHANNEL.registerMessage(S2CQuestJournal.Handler.class, S2CQuestJournal.class, 3, Side.CLIENT);
        CHANNEL.registerMessage(C2SQuestJournalRequest.Handler.class, C2SQuestJournalRequest.class, 4, Side.SERVER);
        CHANNEL.registerMessage(CanonicalSessionActionHandler.class, CanonicalSessionAction.class, 5, Side.SERVER);
        CHANNEL.registerMessage(CanonicalSessionFrameHandler.class, CanonicalSessionFrame.class, 6, Side.CLIENT);
        CHANNEL.registerMessage(CanonicalSessionCloseHandler.class, CanonicalSessionClose.class, 7, Side.CLIENT);
        registered = true;
    }
}
