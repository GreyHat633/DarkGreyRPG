package darkgrey.rpg.quest.runtime;

import net.minecraft.entity.player.EntityPlayerMP;

public interface QuestCompletionListener {

    void onQuestCompleted(EntityPlayerMP player, String questId);
}
