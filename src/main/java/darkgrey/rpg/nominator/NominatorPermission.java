package darkgrey.rpg.nominator;

import net.minecraft.entity.player.EntityPlayerMP;

/** Centralized permission gate for all nominator mutations. */
public final class NominatorPermission {

    private NominatorPermission() {}

    public static boolean canUse(EntityPlayerMP player) {
        return player != null
            && (player.capabilities.isCreativeMode || player.canCommandSenderUseCommand(2, "dgrpg nominator"));
    }
}
