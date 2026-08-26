package darkgrey.rpg.story.runtime;

import net.minecraft.entity.player.EntityPlayerMP;

import darkgrey.rpg.dialogue.runtime.DialogueResult;
import darkgrey.rpg.dialogue.runtime.DialogueResultListener;
import darkgrey.rpg.quest.runtime.QuestCompletionListener;

public final class StoryEventBridge implements DialogueResultListener, QuestCompletionListener {

    private final StoryEventBus eventBus;

    public StoryEventBridge(StoryEventBus eventBus) {
        this.eventBus = eventBus;
    }

    @Override
    public void onDialogueResult(EntityPlayerMP player, DialogueResult result) {
        eventBus.post(player, StoryEvent.dialogueResult(result.getDialogueId(), result.getResult()));
    }

    @Override
    public void onQuestCompleted(EntityPlayerMP player, String questId) {
        eventBus.post(player, StoryEvent.target(StoryEvent.Type.QUEST_COMPLETED, questId));
    }
}
