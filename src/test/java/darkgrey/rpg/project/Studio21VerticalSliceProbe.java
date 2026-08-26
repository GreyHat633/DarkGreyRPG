package darkgrey.rpg.project;

import java.io.File;

import darkgrey.rpg.story.StoryConnection;
import darkgrey.rpg.story.StoryDefinition;
import darkgrey.rpg.story.StoryNode;
import darkgrey.rpg.story.StoryNodeType;
import darkgrey.rpg.story.runtime.BuiltinStoryExecutors;
import darkgrey.rpg.story.runtime.NodeExecutionResult;
import darkgrey.rpg.story.runtime.StoryEvent;
import darkgrey.rpg.story.runtime.StoryExecutionContext;
import darkgrey.rpg.story.runtime.StoryInstance;
import darkgrey.rpg.story.runtime.StoryNodeExecutorRegistry;

public final class Studio21VerticalSliceProbe {

    private Studio21VerticalSliceProbe() {}

    public static void main(String[] arguments) {
        if (arguments.length != 1) {
            throw new IllegalArgumentException("Expected the Studio 2.1 acceptance project directory");
        }
        ProjectRepository repository = new ProjectRepository(new File(arguments[0]));
        ProjectRepository.ReloadResult reload = repository.reload();
        require(reload.isSuccessful(), reload.getSummary());
        require(reload.getActorCount() == 2, "Expected 2 Actors");
        require(reload.getDialogueCount() == 1, "Expected 1 Dialogue");
        require(reload.getQuestCount() == 1, "Expected 1 Quest");
        require(reload.getStoryCount() == 4, "Expected 4 Stories");

        StoryDefinition mystery = repository.getSnapshot()
            .getStory("royal_mystery");
        require(mystery != null, "王城迷案 was not loaded");
        require(
            mystery.getNode("start")
                .getType() == StoryNodeType.STORY_START,
            "StoryStart was not loaded");
        require(
            mystery.getNode("interact_detective")
                .getType() == StoryNodeType.INTERACT_ACTOR,
            "ActorInteract alias was not loaded");
        require(
            mystery.getNode("exit_branch")
                .getType() == StoryNodeType.DIALOGUE_EXIT_BRANCH,
            "DialogueExitBranch was not loaded");
        require(
            mystery.getNode("enter_kingdom")
                .getType() == StoryNodeType.ENTER_STORY,
            "EnterStory was not loaded");
        require(
            mystery.findConnection("play_final", "next") != null,
            "PlayDialogue no longer feeds DialogueExitBranch through next");

        StoryNodeExecutorRegistry executors = BuiltinStoryExecutors.createRegistry();
        StoryInstance instance = new StoryInstance(mystery);
        instance.startAt(mystery.getEntry());
        StoryExecutionContext context = new StoryExecutionContext(null, mystery, instance, null, null);
        NodeExecutionResult start = executors.get(StoryNodeType.STORY_START)
            .execute(context, mystery.getNode("start"), StoryEvent.manual());
        require(
            start.getKind() == NodeExecutionResult.Kind.NEXT && "next".equals(start.getOutput()),
            "StoryStart did not enter the flow");

        assertNamedExit(repository, mystery, context, instance, executors, "hand_over", "kingdom_route");
        StoryInstance.Snapshot kingdomSnapshot = instance.snapshot();
        instance.recordDialogueResult("conceal");
        instance.restore(kingdomSnapshot);
        require(
            "hand_over".equals(instance.getLastDialogueResult()),
            "Dialogue exit was not preserved by StoryInstance snapshots");
        assertNamedExit(repository, mystery, context, instance, executors, "conceal", "empire_route");

        require(
            repository.getSnapshot()
                .getStory("kingdom_route")
                .getNode("start")
                .getType() == StoryNodeType.STORY_START,
            "王国线 entry is not StoryStart");
        require(
            repository.getSnapshot()
                .getStory("empire_route")
                .getNode("start")
                .getType() == StoryNodeType.STORY_START,
            "帝国线 entry is not StoryStart");
        System.out.println("STUDIO_21_PROJECT_RELOAD=PASS");
        System.out.println("STUDIO_21_DIALOGUE_EXIT_HAND_OVER_TO_KINGDOM=PASS");
        System.out.println("STUDIO_21_DIALOGUE_EXIT_CONCEAL_TO_EMPIRE=PASS");
        System.out.println("STUDIO_21_STORY_INSTANCE_SNAPSHOT=PASS");
    }

    private static void assertNamedExit(ProjectRepository repository, StoryDefinition mystery,
        StoryExecutionContext context, StoryInstance instance, StoryNodeExecutorRegistry executors, String exit,
        String expectedStory) {
        instance.recordDialogueResult(exit);
        NodeExecutionResult branch = executors.get(StoryNodeType.DIALOGUE_EXIT_BRANCH)
            .execute(context, mystery.getNode("exit_branch"), StoryEvent.manual());
        require(
            branch.getKind() == NodeExecutionResult.Kind.NEXT && exit.equals(branch.getOutput()),
            "DialogueExitBranch did not route " + exit);
        StoryConnection connection = mystery.findConnection("exit_branch", branch.getOutput());
        require(connection != null, "Missing Story connection for exit " + exit);
        StoryNode enter = mystery.getNode(connection.getTo());
        NodeExecutionResult transition = executors.get(StoryNodeType.ENTER_STORY)
            .execute(context, enter, StoryEvent.manual());
        require(
            transition.getKind() == NodeExecutionResult.Kind.ENTER_STORY
                && expectedStory.equals(transition.getOutput()),
            "Wrong EnterStory target for exit " + exit);
        require(
            repository.getSnapshot()
                .getStory(transition.getOutput()) != null,
            "EnterStory target was not loaded: " + transition.getOutput());
    }

    private static void require(boolean condition, String message) {
        if (!condition) {
            throw new IllegalStateException(message);
        }
    }
}
