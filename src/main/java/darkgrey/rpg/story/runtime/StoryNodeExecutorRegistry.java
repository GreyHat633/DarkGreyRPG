package darkgrey.rpg.story.runtime;

import java.util.EnumMap;
import java.util.Map;

import darkgrey.rpg.story.StoryNodeType;

public final class StoryNodeExecutorRegistry {

    private final Map<StoryNodeType, StoryNodeExecutor> executors = new EnumMap<StoryNodeType, StoryNodeExecutor>(
        StoryNodeType.class);

    public void register(StoryNodeType type, StoryNodeExecutor executor) {
        executors.put(type, executor);
    }

    public StoryNodeExecutor get(StoryNodeType type) {
        return executors.get(type);
    }
}
