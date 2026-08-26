package darkgrey.rpg.dialogue.runtime;

import net.minecraft.entity.player.EntityPlayerMP;

public interface DialogueResultListener {

    void onDialogueResult(EntityPlayerMP player, DialogueResult result);
}
