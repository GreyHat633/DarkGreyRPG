package darkgrey.rpg.runtime;

import net.minecraft.command.ICommandSender;
import net.minecraft.util.ChatComponentText;
import net.minecraft.util.EnumChatFormatting;

public final class ChatMessages {

    private ChatMessages() {}

    public static void info(ICommandSender sender, String message) {
        sender.addChatMessage(
            new ChatComponentText(EnumChatFormatting.AQUA + "[DarkGrey RPG] " + EnumChatFormatting.RESET + message));
    }

    public static void success(ICommandSender sender, String message) {
        sender.addChatMessage(
            new ChatComponentText(EnumChatFormatting.GREEN + "[DarkGrey RPG] " + EnumChatFormatting.RESET + message));
    }

    public static void error(ICommandSender sender, String message) {
        sender.addChatMessage(
            new ChatComponentText(EnumChatFormatting.RED + "[DarkGrey RPG] " + EnumChatFormatting.RESET + message));
    }
}
