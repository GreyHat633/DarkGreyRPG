package darkgrey.rpg.client.session;

import java.util.Locale;

import net.minecraft.client.Minecraft;

/** Same connection/save identity as media, without its dimension or connection lifetime. */
public final class PlayerReadingContext {

    private PlayerReadingContext() {}

    public static String current() {
        Minecraft mc = Minecraft.getMinecraft();
        if (mc.thePlayer == null || mc.theWorld == null) return null;
        String scope;
        if (mc.isSingleplayer() && mc.getIntegratedServer() != null) scope = "local:" + mc.getIntegratedServer()
            .getFolderName();
        else if (mc.func_147104_D() != null) scope = "server:" + normalizeServer(mc.func_147104_D().serverIP);
        else return null;
        return scope + "|player:" + mc.thePlayer.getUniqueID();
    }

    public static String normalizeServer(String value) {
        String server = value.trim()
            .toLowerCase(Locale.ROOT);
        if (!server.contains(":")) return server + ":25565";
        if (server.startsWith("[") && server.endsWith("]")) return server + ":25565";
        return server;
    }
}
