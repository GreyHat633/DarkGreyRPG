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

    private static net.minecraft.world.World world;
    private static GuiCanonicalSessionScreen surface;

    public static void synchronizeWorld() {
        Minecraft mc = Minecraft.getMinecraft();
        if (world != mc.theWorld) {
            DialogueHistoryClient.clearPending();
            if (mc.currentScreen == surface && surface != null) mc.displayGuiScreen(null);
            darkgrey.rpg.media.CanonicalSessionAudio.clear();
            darkgrey.rpg.media.CanonicalSessionScene.clear();
            MODEL.clear();
            darkgrey.rpg.media.CanonicalMediaClient.clear();
            surface = null;
            world = mc.theWorld;
        }
    }

    public static void acceptFrame(CanonicalSessionFrame frame) {
        synchronizeWorld();
        Minecraft minecraft = Minecraft.getMinecraft();
        if (minecraft.theWorld == null || minecraft.thePlayer == null || !MODEL.acceptFrame(frame)) return;
        CanonicalSessionFrame accepted = MODEL.getFrame();
        darkgrey.rpg.media.CanonicalMediaClient.present(accepted);
        darkgrey.rpg.media.CanonicalSessionAudio.present(accepted);
        darkgrey.rpg.media.CanonicalSessionScene.present(accepted);
        if (accepted.getKind() == CanonicalSessionFrame.Kind.PRESENTATION) {
            if (minecraft.currentScreen == surface && surface != null) minecraft.displayGuiScreen(null);
            surface = null;
            return;
        }
        if (surface != null && surface.matches(accepted)) surface.setFrame(accepted);
        else surface = new GuiCanonicalSessionScreen(accepted);
        restoreForeground();
    }

    public static void restoreForeground() {
        synchronizeWorld();
        darkgrey.rpg.media.CanonicalMediaClient.tick();
        darkgrey.rpg.media.CanonicalSessionAudio.tick();
        Minecraft mc = Minecraft.getMinecraft();
        if (surface != null && mc.currentScreen == surface && mc.thePlayer != null && mc.thePlayer.getHealth() <= 0) {
            mc.displayGuiScreen(null); // Vanilla resolves null to its usable death screen.
            return;
        }
        if (surface != null && MODEL.isActive()
            && mc.thePlayer != null
            && !mc.thePlayer.isDead
            && mc.thePlayer.getHealth() > 0
            && mc.currentScreen == null) mc.displayGuiScreen(surface);
    }

    public static void drawUnderlay(float partialTicks) {
        Minecraft mc = Minecraft.getMinecraft();
        if (world != mc.theWorld || !MODEL.isActive() || (surface != null && mc.currentScreen == surface)) return;
        net.minecraft.client.gui.ScaledResolution scaled = new net.minecraft.client.gui.ScaledResolution(
            mc,
            mc.displayWidth,
            mc.displayHeight);
        darkgrey.rpg.media.CanonicalSessionScene.draw(scaled.getScaledWidth(), scaled.getScaledHeight());
        if (surface == null) return;
        if (surface.width != scaled.getScaledWidth() || surface.height != scaled.getScaledHeight())
            surface.setWorldAndResolution(mc, scaled.getScaledWidth(), scaled.getScaledHeight());
        surface.drawUnderlay(partialTicks);
    }

    public static CanonicalSessionFrame getFrame() {
        return MODEL.getFrame();
    }

    public static void showFrame(CanonicalSessionFrame frame) {
        acceptFrame(frame);
    }

    public static void acceptClose(CanonicalSessionClose close) {
        if (!MODEL.acceptClose(close)) return;
        darkgrey.rpg.media.CanonicalSessionAudio.clear();
        darkgrey.rpg.media.CanonicalSessionScene.clear();
        darkgrey.rpg.media.CanonicalMediaClient.clear();
        surface = null;
        Minecraft minecraft = Minecraft.getMinecraft();
        if (minecraft.currentScreen instanceof GuiCanonicalSessionScreen
            && ((GuiCanonicalSessionScreen) minecraft.currentScreen)
                .matches(close.getTransportId(), close.getStoryId()))
            minecraft.displayGuiScreen(null);
    }

    public static void close(CanonicalSessionClose close) {
        acceptClose(close);
    }

    public static boolean sendContinue() {
        if (MODEL.finishVisibleText()) return false;
        send(MODEL.continueAction());
        return true;
    }

    public static String getVisibleText() {
        return MODEL.getVisibleText();
    }

    public static String getVisibleSpeaker() {
        return MODEL.getVisibleSpeaker();
    }

    public static String getVisiblePortraitRef() {
        return MODEL.getVisiblePortraitRef();
    }

    public static void sendChoice(String optionId) {
        CanonicalSessionAction action = MODEL.choiceAction(optionId);
        if (Minecraft.getMinecraft().currentScreen == surface) DialogueHistoryClient.choosing(MODEL.getFrame(), action);
        send(action);
    }

    private static void send(CanonicalSessionAction action) {
        if (Minecraft.getMinecraft().currentScreen == surface) {
            darkgrey.rpg.media.CanonicalSessionAudio.advance();
            DialogueNetwork.CHANNEL.sendToServer(action);
        }
    }
}
