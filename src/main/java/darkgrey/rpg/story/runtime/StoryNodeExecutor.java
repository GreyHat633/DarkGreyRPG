package darkgrey.rpg.story.runtime;

import darkgrey.rpg.story.StoryNode;

public interface StoryNodeExecutor {

    NodeExecutionResult execute(StoryExecutionContext context, StoryNode node, StoryEvent event);
}
