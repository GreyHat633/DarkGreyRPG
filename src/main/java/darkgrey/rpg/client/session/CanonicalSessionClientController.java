package darkgrey.rpg.client.session;

import net.minecraft.client.Minecraft;

import darkgrey.rpg.client.gui.GuiCanonicalSessionScreen;
import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.message.canonical.CanonicalSessionAction;
import darkgrey.rpg.network.message.canonical.CanonicalSessionClose;
import darkgrey.rpg.network.message.canonical.CanonicalSessionFrame;

/** Client transport/controller for the independent canonical Session screen. */
public final class CanonicalSessionClientController {

    private static final CanonicalSessionClientModel MODEL = new CanonicalSessionClientModel();

    private CanonicalSessionClientController() {}

    public static void acceptFrame(CanonicalSessionFrame frame) {
        if (!MODEL.acceptFrame(frame)) return;
        Minecraft minecraft = Minecraft.getMinecraft();
        CanonicalSessionFrame accepted = MODEL.getFrame();
        if (minecraft.currentScreen instanceof GuiCanonicalSessionScreen
            && ((GuiCanonicalSessionScreen) minecraft.currentScreen).matches(accepted)) {
            ((GuiCanonicalSessionScreen) minecraft.currentScreen).setFrame(accepted);
        } else {
            minecraft.displayGuiScreen(new GuiCanonicalSessionScreen(accepted));
        }
    }

    public static void showFrame(CanonicalSessionFrame frame) {
        acceptFrame(frame);
    }

    public static void acceptClose(CanonicalSessionClose close) {
        if (!MODEL.acceptClose(close)) return;
        Minecraft minecraft = Minecraft.getMinecraft();
        if (minecraft.currentScreen instanceof GuiCanonicalSessionScreen
            && ((GuiCanonicalSessionScreen) minecraft.currentScreen)
                .matches(close.getTransportId(), close.getStoryId()))
            minecraft.displayGuiScreen(null);
    }

    public static void close(CanonicalSessionClose close) {
        acceptClose(close);
    }

    public static void sendContinue() {
        send(MODEL.continueAction());
    }

    public static String getVisibleText() {
        return MODEL.getVisibleText();
    }

    public static String getVisibleSpeaker() {
        return MODEL.getVisibleSpeaker();
    }

    public static void sendChoice(String optionId) {
        send(MODEL.choiceAction(optionId));
    }

    private static void send(CanonicalSessionAction action) {
        DialogueNetwork.CHANNEL.sendToServer(action);
    }
}
