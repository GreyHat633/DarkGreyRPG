package darkgrey.rpg.client;

import net.minecraft.client.Minecraft;
import net.minecraft.util.ChatComponentText;
import net.minecraft.util.EnumChatFormatting;

import darkgrey.rpg.client.gui.GuiDialogueScreen;
import darkgrey.rpg.network.message.S2CDialogueFrame;

public final class DialogueClientController {

    private DialogueClientController() {}

    public static void showFrame(S2CDialogueFrame frame) {
        Minecraft minecraft = Minecraft.getMinecraft();
        if (minecraft.currentScreen instanceof GuiDialogueScreen) {
            ((GuiDialogueScreen) minecraft.currentScreen).setFrame(frame);
        } else {
            minecraft.displayGuiScreen(new GuiDialogueScreen(frame));
        }
    }

    public static void close(long sessionId, String result) {
        Minecraft minecraft = Minecraft.getMinecraft();
        if (minecraft.currentScreen instanceof GuiDialogueScreen
            && ((GuiDialogueScreen) minecraft.currentScreen).getSessionId() == sessionId) {
            minecraft.displayGuiScreen(null);
        }
        if (minecraft.thePlayer != null) {
            minecraft.thePlayer.addChatMessage(
                new ChatComponentText(
                    EnumChatFormatting.AQUA + "[DarkGrey RPG] "
                        + EnumChatFormatting.RESET
                        + "Dialogue Result: "
                        + result));
        }
    }
}
