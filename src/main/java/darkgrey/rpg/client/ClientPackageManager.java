package darkgrey.rpg.client;

import net.minecraft.client.Minecraft;
import net.minecraft.nbt.NBTTagCompound;

import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.project.packages.StoryPackageManagerPacket;
import darkgrey.rpg.project.packages.StoryPackageManagerService;

/** Open only after server authorization; stale connections and superseded openings cannot steal focus. */
public final class ClientPackageManager {

    private static long sequence, opening, openedAt;
    private static Object connection;

    private ClientPackageManager() {}

    public static long next() {
        return ++sequence;
    }

    public static void open() {
        Minecraft mc = Minecraft.getMinecraft();
        if (mc.thePlayer == null || mc.currentScreen != null) return;
        if (connection == mc.getNetHandler() && opening != 0 && System.currentTimeMillis() - openedAt < 10000) return;
        connection = mc.getNetHandler();
        opening = next();
        openedAt = System.currentTimeMillis();
        NBTTagCompound input = new NBTTagCompound();
        input.setInteger("action", StoryPackageManagerService.OPEN);
        DialogueNetwork.CHANNEL.sendToServer(new StoryPackageManagerPacket(false, opening, input));
    }

    public static void accept(long request, NBTTagCompound data) {
        Minecraft mc = Minecraft.getMinecraft();
        if (request == opening && opening != 0 && connection == mc.getNetHandler()) {
            opening = 0;
            if (mc.currentScreen == null && !data.getBoolean("denied") && !data.hasKey("error"))
                mc.displayGuiScreen(new darkgrey.rpg.client.gui.GuiStoryPackageManager(data));
            return;
        }
        if (mc.currentScreen instanceof darkgrey.rpg.client.gui.GuiStoryPackageManager)
            ((darkgrey.rpg.client.gui.GuiStoryPackageManager) mc.currentScreen).accept(request, data);
    }
}
