package darkgrey.rpg.network;

import cpw.mods.fml.common.network.NetworkRegistry;
import cpw.mods.fml.common.network.simpleimpl.SimpleNetworkWrapper;
import cpw.mods.fml.relauncher.Side;
import darkgrey.rpg.DarkGreyRpg;
import darkgrey.rpg.network.message.C2SQuestJournalRequest;
import darkgrey.rpg.network.message.S2CQuestJournal;
import darkgrey.rpg.network.message.canonical.CanonicalSessionAction;
import darkgrey.rpg.network.message.canonical.CanonicalSessionClose;
import darkgrey.rpg.network.message.canonical.CanonicalSessionFrame;
import darkgrey.rpg.network.message.canonical.CanonicalStoryChooserFrame;
import darkgrey.rpg.network.message.canonical.CanonicalStoryChooserSelection;
import darkgrey.rpg.session.forge.CanonicalSessionActionHandler;
import darkgrey.rpg.session.forge.CanonicalSessionCloseHandler;
import darkgrey.rpg.session.forge.CanonicalSessionFrameHandler;

public final class DialogueNetwork {

    /**
     * Discriminators are shared by every packet family on this channel. Keep the
     * chooser range after the existing nominator and entity-tools registrations.
     */
    public static final int STORY_CHOOSER_FRAME_DISCRIMINATOR = 15;
    public static final int STORY_CHOOSER_SELECTION_DISCRIMINATOR = 16;

    public static final SimpleNetworkWrapper CHANNEL = NetworkRegistry.INSTANCE.newSimpleChannel(DarkGreyRpg.MOD_ID);
    private static boolean registered;

    private DialogueNetwork() {}

    public static synchronized void registerCommon() {
        if (registered) return;
        CHANNEL.registerMessage(S2CQuestJournal.Handler.class, S2CQuestJournal.class, 3, Side.CLIENT);
        CHANNEL.registerMessage(C2SQuestJournalRequest.Handler.class, C2SQuestJournalRequest.class, 4, Side.SERVER);
        CHANNEL.registerMessage(CanonicalSessionActionHandler.class, CanonicalSessionAction.class, 5, Side.SERVER);
        CHANNEL.registerMessage(CanonicalSessionFrameHandler.class, CanonicalSessionFrame.class, 6, Side.CLIENT);
        CHANNEL.registerMessage(CanonicalSessionCloseHandler.class, CanonicalSessionClose.class, 7, Side.CLIENT);
        CHANNEL.registerMessage(
            CanonicalStoryChooserFrame.Handler.class,
            CanonicalStoryChooserFrame.class,
            STORY_CHOOSER_FRAME_DISCRIMINATOR,
            Side.CLIENT);
        CHANNEL.registerMessage(
            CanonicalStoryChooserSelection.Handler.class,
            CanonicalStoryChooserSelection.class,
            STORY_CHOOSER_SELECTION_DISCRIMINATOR,
            Side.SERVER);
        registered = true;
    }
}
