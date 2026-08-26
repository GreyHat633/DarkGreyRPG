package darkgrey.rpg.story.runtime;

import darkgrey.rpg.quest.runtime.QuestStatus;
import darkgrey.rpg.story.StoryNode;
import darkgrey.rpg.story.StoryNodeType;

public final class BuiltinStoryExecutors {

    private BuiltinStoryExecutors() {}

    public static StoryNodeExecutorRegistry createRegistry() {
        StoryNodeExecutorRegistry registry = new StoryNodeExecutorRegistry();
        registry.register(StoryNodeType.STORY_START, new StoryStartExecutor());
        registry.register(StoryNodeType.INTERACT_ACTOR, new InteractActorExecutor());
        registry.register(StoryNodeType.ENTER_REGION, new EnterRegionExecutor());
        registry.register(StoryNodeType.QUEST_COMPLETED, new QuestCompletedExecutor());
        registry.register(StoryNodeType.PLAY_DIALOGUE, new PlayDialogueExecutor());
        registry.register(StoryNodeType.DIALOGUE_EXIT_BRANCH, new DialogueExitBranchExecutor());
        registry.register(StoryNodeType.START_QUEST, new StartQuestExecutor());
        registry.register(StoryNodeType.COMPLETE_QUEST, new CompleteQuestExecutor());
        registry.register(StoryNodeType.BRANCH, new VariableConditionExecutor());
        registry.register(StoryNodeType.SEQUENCE, new SequenceExecutor());
        registry.register(StoryNodeType.QUEST_STATE, new QuestStateExecutor());
        registry.register(StoryNodeType.HAS_ITEM, new HasItemExecutor());
        registry.register(StoryNodeType.VARIABLE_COMPARE, new VariableConditionExecutor());
        registry.register(StoryNodeType.GIVE_ITEM, new GiveItemExecutor());
        registry.register(StoryNodeType.GIVE_XP, new GiveExperienceExecutor());
        registry.register(StoryNodeType.SEND_MESSAGE, new SendMessageExecutor());
        registry.register(StoryNodeType.SET_VARIABLE, new SetVariableExecutor());
        registry.register(StoryNodeType.ENTER_STORY, new EnterStoryExecutor());
        registry.register(StoryNodeType.END_STORY, new EndExecutor());
        registry.register(StoryNodeType.END, new EndExecutor());
        return registry;
    }

    private static final class StoryStartExecutor implements StoryNodeExecutor {

        @Override
        public NodeExecutionResult execute(StoryExecutionContext context, StoryNode node, StoryEvent event) {
            return context.getInstance()
                .getState() == StoryState.RUNNING ? NodeExecutionResult.next("next")
                    : NodeExecutionResult.waitForEvent();
        }
    }

    private static final class InteractActorExecutor implements StoryNodeExecutor {

        @Override
        public NodeExecutionResult execute(StoryExecutionContext context, StoryNode node, StoryEvent event) {
            return event.getType() == StoryEvent.Type.INTERACT_ACTOR && node.getProperty("actor_id")
                .equals(event.getTargetId()) ? NodeExecutionResult.next("next") : NodeExecutionResult.waitForEvent();
        }
    }

    private static final class EnterRegionExecutor implements StoryNodeExecutor {

        @Override
        public NodeExecutionResult execute(StoryExecutionContext context, StoryNode node, StoryEvent event) {
            if (event.getType() != StoryEvent.Type.PLAYER_POSITION
                || node.getIntProperty("dimension") != event.getDimension()) {
                return NodeExecutionResult.waitForEvent();
            }
            double deltaX = node.getDoubleProperty("x") - event.getX();
            double deltaY = node.getDoubleProperty("y") - event.getY();
            double deltaZ = node.getDoubleProperty("z") - event.getZ();
            double radius = node.getDoubleProperty("radius");
            return deltaX * deltaX + deltaY * deltaY + deltaZ * deltaZ <= radius * radius
                ? NodeExecutionResult.next("next")
                : NodeExecutionResult.waitForEvent();
        }
    }

    private static final class QuestCompletedExecutor implements StoryNodeExecutor {

        @Override
        public NodeExecutionResult execute(StoryExecutionContext context, StoryNode node, StoryEvent event) {
            return event.getType() == StoryEvent.Type.QUEST_COMPLETED && node.getProperty("quest_id")
                .equals(event.getTargetId()) ? NodeExecutionResult.next("next") : NodeExecutionResult.waitForEvent();
        }
    }

    private static final class PlayDialogueExecutor implements StoryNodeExecutor {

        @Override
        public NodeExecutionResult execute(StoryExecutionContext context, StoryNode node, StoryEvent event) {
            String dialogueId = node.getProperty("dialogue_id");
            if (context.getInstance()
                .getState() == StoryState.WAITING) {
                if (event.getType() != StoryEvent.Type.DIALOGUE_RESULT || !dialogueId.equals(event.getTargetId())) {
                    return NodeExecutionResult.waitForEvent();
                }
                context.getInstance()
                    .recordDialogueResult(event.getResult());
                return NodeExecutionResult
                    .next(context.hasConnection(node.getId(), "next") ? "next" : event.getResult());
            }
            return context.startDialogue(dialogueId) ? NodeExecutionResult.waitForEvent()
                : NodeExecutionResult.error("Could not start Dialogue '" + dialogueId + "'");
        }
    }

    private static final class DialogueExitBranchExecutor implements StoryNodeExecutor {

        @Override
        public NodeExecutionResult execute(StoryExecutionContext context, StoryNode node, StoryEvent event) {
            String result = context.getInstance()
                .getLastDialogueResult();
            return result.isEmpty() ? NodeExecutionResult.error("DialogueExitBranch has no Dialogue result")
                : NodeExecutionResult.next(result);
        }
    }

    private static final class StartQuestExecutor implements StoryNodeExecutor {

        @Override
        public NodeExecutionResult execute(StoryExecutionContext context, StoryNode node, StoryEvent event) {
            context.startQuest(node.getProperty("quest_id"));
            return NodeExecutionResult.next("next");
        }
    }

    private static final class CompleteQuestExecutor implements StoryNodeExecutor {

        @Override
        public NodeExecutionResult execute(StoryExecutionContext context, StoryNode node, StoryEvent event) {
            return context.completeQuest(node.getProperty("quest_id")) ? NodeExecutionResult.next("next")
                : NodeExecutionResult.error("Could not complete Quest '" + node.getProperty("quest_id") + "'");
        }
    }

    private static final class VariableConditionExecutor implements StoryNodeExecutor {

        @Override
        public NodeExecutionResult execute(StoryExecutionContext context, StoryNode node, StoryEvent event) {
            boolean matches = context
                .compareVariable(node.getProperty("variable"), node.getProperty("operator"), node.getProperty("value"));
            return NodeExecutionResult.next(matches ? "true" : "false");
        }
    }

    private static final class SequenceExecutor implements StoryNodeExecutor {

        @Override
        public NodeExecutionResult execute(StoryExecutionContext context, StoryNode node, StoryEvent event) {
            return NodeExecutionResult.sequence();
        }
    }

    private static final class QuestStateExecutor implements StoryNodeExecutor {

        @Override
        public NodeExecutionResult execute(StoryExecutionContext context, StoryNode node, StoryEvent event) {
            QuestStatus actual = context.getQuestStatus(node.getProperty("quest_id"));
            String actualName = actual == null ? "NOT_STARTED" : actual.name();
            return NodeExecutionResult.next(actualName.equals(node.getProperty("state")) ? "true" : "false");
        }
    }

    private static final class HasItemExecutor implements StoryNodeExecutor {

        @Override
        public NodeExecutionResult execute(StoryExecutionContext context, StoryNode node, StoryEvent event) {
            boolean hasItem = context
                .hasItem(node.getProperty("item"), node.getIntProperty("metadata"), node.getIntProperty("amount"));
            return NodeExecutionResult.next(hasItem ? "true" : "false");
        }
    }

    private static final class GiveItemExecutor implements StoryNodeExecutor {

        @Override
        public NodeExecutionResult execute(StoryExecutionContext context, StoryNode node, StoryEvent event) {
            boolean given = context
                .giveItem(node.getProperty("item"), node.getIntProperty("metadata"), node.getIntProperty("amount"));
            return given ? NodeExecutionResult.next("next")
                : NodeExecutionResult.error("Unknown item '" + node.getProperty("item") + "'");
        }
    }

    private static final class GiveExperienceExecutor implements StoryNodeExecutor {

        @Override
        public NodeExecutionResult execute(StoryExecutionContext context, StoryNode node, StoryEvent event) {
            context.giveExperience(node.getIntProperty("amount"));
            return NodeExecutionResult.next("next");
        }
    }

    private static final class SendMessageExecutor implements StoryNodeExecutor {

        @Override
        public NodeExecutionResult execute(StoryExecutionContext context, StoryNode node, StoryEvent event) {
            context.sendMessage(node.getProperty("message"));
            return NodeExecutionResult.next("next");
        }
    }

    private static final class SetVariableExecutor implements StoryNodeExecutor {

        @Override
        public NodeExecutionResult execute(StoryExecutionContext context, StoryNode node, StoryEvent event) {
            context.setVariable(node.getProperty("variable"), node.getProperty("value"));
            return NodeExecutionResult.next("next");
        }
    }

    private static final class EndExecutor implements StoryNodeExecutor {

        @Override
        public NodeExecutionResult execute(StoryExecutionContext context, StoryNode node, StoryEvent event) {
            return NodeExecutionResult.end();
        }
    }

    private static final class EnterStoryExecutor implements StoryNodeExecutor {

        @Override
        public NodeExecutionResult execute(StoryExecutionContext context, StoryNode node, StoryEvent event) {
            return NodeExecutionResult.enterStory(node.getProperty("target_story_id"));
        }
    }
}
