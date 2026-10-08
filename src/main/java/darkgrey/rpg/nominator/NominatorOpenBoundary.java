package darkgrey.rpg.nominator;

import net.minecraft.entity.player.EntityPlayer;
import net.minecraft.util.ChatComponentText;

import darkgrey.rpg.DarkGreyRpg;

/** Isolates tool-open failures at their own entry, without hiding failures in the global tick scheduler. */
public final class NominatorOpenBoundary {

    private NominatorOpenBoundary() {}

    public static boolean run(EntityPlayer player, Runnable open) {
        try {
            open.run();
            return true;
        } catch (NominatorDataUnavailableException failure) {
            DarkGreyRpg.LOG.warn("Nominator data unavailable; open cancelled", failure);
            player.addChatMessage(new ChatComponentText("§c" + NominatorDataUnavailableException.PLAYER_MESSAGE));
        } catch (RuntimeException failure) {
            DarkGreyRpg.LOG.warn("Nominator view open rejected", failure);
            player.addChatMessage(new ChatComponentText("§c指名器打开失败，本次操作已取消。请查看 DGR 日志。"));
        }
        return false;
    }
}
