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

public final class DialogueNetwork {

    public static final SimpleNetworkWrapper CHANNEL = NetworkRegistry.INSTANCE.newSimpleChannel(DarkGreyRpg.MOD_ID);

    private DialogueNetwork() {}

    public static void registerCommon() {
        CHANNEL.registerMessage(C2SDialogueAction.Handler.class, C2SDialogueAction.class, 0, Side.SERVER);
        CHANNEL.registerMessage(S2CDialogueFrame.Handler.class, S2CDialogueFrame.class, 1, Side.CLIENT);
        CHANNEL.registerMessage(S2CDialogueClose.Handler.class, S2CDialogueClose.class, 2, Side.CLIENT);
        CHANNEL.registerMessage(S2CQuestJournal.Handler.class, S2CQuestJournal.class, 3, Side.CLIENT);
        CHANNEL.registerMessage(C2SQuestJournalRequest.Handler.class, C2SQuestJournalRequest.class, 4, Side.SERVER);
    }
}
