package darkgrey.rpg.story.runtime;

import darkgrey.rpg.quest.runtime.QuestStatus;
import darkgrey.rpg.story.StoryNode;
import darkgrey.rpg.story.StoryNodeType;

public final class StoryDebugExplainer {

    private StoryDebugExplainer() {}

    public static String explain(StoryExecutionContext context, StoryNode node, StoryEvent event,
        NodeExecutionResult result) {
        StoryNodeType type = node.getType();
        if (type == StoryNodeType.INTERACT_ACTOR) {
            return expectedActual(
                "Actor",
                node.getProperty("actor_id"),
                event.getType() == StoryEvent.Type.INTERACT_ACTOR ? event.getTargetId()
                    : event.getType()
                        .name());
        }
        if (type == StoryNodeType.QUEST_COMPLETED) {
            return expectedActual(
                "QuestCompleted",
                node.getProperty("quest_id"),
                event.getType() == StoryEvent.Type.QUEST_COMPLETED ? event.getTargetId()
                    : event.getType()
                        .name());
        }
        if (type == StoryNodeType.QUEST_STATE) {
            QuestStatus status = context.getQuestStatus(node.getProperty("quest_id"));
            return expectedActual(
                "QuestState",
                node.getProperty("state"),
                status == null ? "NOT_STARTED" : status.name());
        }
        if (type == StoryNodeType.HAS_ITEM) {
            boolean actual = context
                .hasItem(node.getProperty("item"), node.getIntProperty("metadata"), node.getIntProperty("amount"));
            return expectedActual(
                "HasItem",
                node.getProperty("item") + " x" + node.getProperty("amount"),
                Boolean.toString(actual));
        }
        if (type == StoryNodeType.BRANCH || type == StoryNodeType.VARIABLE_COMPARE) {
            return "Variable " + node.getProperty("variable")
                + " actual='"
                + context.getVariable(node.getProperty("variable"))
                + "', operator="
                + node.getProperty("operator")
                + ", expected='"
                + node.getProperty("value")
                + "', route="
                + result.getOutput();
        }
        if (type == StoryNodeType.ENTER_REGION) {
            return result.getKind() == NodeExecutionResult.Kind.WAIT ? "Waiting for player to enter configured region"
                : "Player entered configured region";
        }
        if (type == StoryNodeType.PLAY_DIALOGUE) {
            return result.getKind() == NodeExecutionResult.Kind.WAIT
                ? "Waiting for Dialogue Result from " + node.getProperty("dialogue_id")
                : "Dialogue Result route=" + result.getOutput();
        }
        if (type == StoryNodeType.DIALOGUE_EXIT_BRANCH) {
            return "Last Dialogue Result='" + context.getInstance()
                .getLastDialogueResult() + "', route=" + result.getOutput();
        }
        if (type == StoryNodeType.ENTER_STORY) {
            return "Enter target Story='" + result.getOutput() + "'";
        }
        if (type == StoryNodeType.STORY_START) {
            return result.getKind() == NodeExecutionResult.Kind.WAIT ? "Story is idle until explicitly started"
                : "Story entry activated";
        }
        return result.getKind()
            .name()
            + (result.getOutput()
                .isEmpty() ? "" : " output=" + result.getOutput());
    }

    private static String expectedActual(String label, String expected, String actual) {
        return label + " expected='" + expected + "', actual='" + actual + "', matches=" + expected.equals(actual);
    }
}
