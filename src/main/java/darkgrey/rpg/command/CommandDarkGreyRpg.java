package darkgrey.rpg.command;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.List;

import net.minecraft.command.CommandBase;
import net.minecraft.command.CommandException;
import net.minecraft.command.ICommandSender;
import net.minecraft.command.WrongUsageException;
import net.minecraft.entity.Entity;
import net.minecraft.entity.player.EntityPlayer;
import net.minecraft.entity.player.EntityPlayerMP;

import cpw.mods.fml.common.Loader;
import darkgrey.rpg.compat.customnpcs.CustomNpcActorBinding;
import darkgrey.rpg.dialogue.DialogueDefinition;
import darkgrey.rpg.dialogue.runtime.DialogueResult;
import darkgrey.rpg.dialogue.runtime.DialogueSessionManager;
import darkgrey.rpg.project.ActorDefinition;
import darkgrey.rpg.project.ProjectRepository;
import darkgrey.rpg.quest.QuestDefinition;
import darkgrey.rpg.quest.QuestObjective;
import darkgrey.rpg.quest.runtime.QuestJournalEntry;
import darkgrey.rpg.quest.runtime.QuestRuntimeService;
import darkgrey.rpg.runtime.ActorBindingActions;
import darkgrey.rpg.runtime.ChatMessages;
import darkgrey.rpg.runtime.EditorSessionManager;
import darkgrey.rpg.runtime.EntityTargeting;
import darkgrey.rpg.story.StoryDefinition;
import darkgrey.rpg.story.runtime.StoryInstance;
import darkgrey.rpg.story.runtime.StoryRuntimeService;

public final class CommandDarkGreyRpg extends CommandBase {

    private static final double TARGET_DISTANCE = 8.0D;

    private final ProjectRepository repository;
    private final EditorSessionManager sessions;
    private final DialogueSessionManager dialogueSessions;
    private final QuestRuntimeService questRuntime;
    private final StoryRuntimeService storyRuntime;

    public CommandDarkGreyRpg(ProjectRepository repository, EditorSessionManager sessions,
        DialogueSessionManager dialogueSessions, QuestRuntimeService questRuntime, StoryRuntimeService storyRuntime) {
        this.repository = repository;
        this.sessions = sessions;
        this.dialogueSessions = dialogueSessions;
        this.questRuntime = questRuntime;
        this.storyRuntime = storyRuntime;
    }

    @Override
    public String getCommandName() {
        return "dgrpg";
    }

    @Override
    public String getCommandUsage(ICommandSender sender) {
        return "/dgrpg <status|reload|actor|dialogue|quest|story>";
    }

    @Override
    public int getRequiredPermissionLevel() {
        return 2;
    }

    @Override
    public void processCommand(ICommandSender sender, String[] arguments) {
        if (arguments.length == 0) {
            throw new WrongUsageException(getCommandUsage(sender));
        }

        if ("status".equalsIgnoreCase(arguments[0])) {
            showStatus(sender);
            return;
        }
        if ("reload".equalsIgnoreCase(arguments[0])) {
            reload(sender);
            return;
        }
        if ("actor".equalsIgnoreCase(arguments[0])) {
            processActor(sender, arguments);
            return;
        }
        if ("dialogue".equalsIgnoreCase(arguments[0])) {
            processDialogue(sender, arguments);
            return;
        }
        if ("quest".equalsIgnoreCase(arguments[0])) {
            processQuest(sender, arguments);
            return;
        }
        if ("story".equalsIgnoreCase(arguments[0])) {
            processStory(sender, arguments);
            return;
        }
        throw new WrongUsageException(getCommandUsage(sender));
    }

    private void processStory(ICommandSender sender, String[] arguments) {
        if (arguments.length < 2) {
            throw new WrongUsageException("/dgrpg story <list|info|start|state|reset>");
        }
        String action = arguments[1].toLowerCase();
        if ("list".equals(action)) {
            listStories(sender);
        } else if ("info".equals(action)) {
            requireLength(arguments, 3, "/dgrpg story info <id>");
            showStory(sender, arguments[2]);
        } else if ("state".equals(action)) {
            requireLength(arguments, 3, "/dgrpg story state <id>");
            showStoryState(requireMultiplayerPlayer(sender), arguments[2]);
        } else if ("reset".equals(action)) {
            requireLength(arguments, 3, "/dgrpg story reset <id>");
            EntityPlayerMP player = requireMultiplayerPlayer(sender);
            if (storyRuntime.resetInstance(player, arguments[2])) {
                ChatMessages.success(player, "Story instance reset: " + arguments[2]);
            } else {
                ChatMessages.error(player, "Unknown Story ID: " + arguments[2]);
            }
        } else if ("start".equals(action)) {
            requireLength(arguments, 3, "/dgrpg story start <id>");
            EntityPlayerMP player = requireMultiplayerPlayer(sender);
            if (storyRuntime.start(player, arguments[2])) {
                ChatMessages.success(player, "Story started: " + arguments[2]);
            } else {
                ChatMessages.error(player, "Could not start Story: " + arguments[2]);
            }
        } else {
            throw new WrongUsageException("/dgrpg story <list|info|start|state|reset>");
        }
    }

    private void listStories(ICommandSender sender) {
        if (repository.getSnapshot()
            .getStories()
            .isEmpty()) {
            ChatMessages.info(sender, "No Stories are loaded.");
            return;
        }
        ChatMessages.info(
            sender,
            "Stories (" + repository.getSnapshot()
                .getStories()
                .size() + "):");
        for (StoryDefinition story : repository.getSnapshot()
            .getStories()
            .values()) {
            ChatMessages.info(sender, "- " + story.getId() + " — " + story.getTitle());
        }
    }

    private void showStory(ICommandSender sender, String id) {
        StoryDefinition story = repository.getSnapshot()
            .getStory(id);
        if (story == null) {
            ChatMessages.error(sender, "Unknown Story ID: " + id);
            return;
        }
        ChatMessages.info(sender, "Story: " + story.getId() + " — " + story.getTitle());
        ChatMessages.info(
            sender,
            "Entry: " + story.getEntry()
                + ", Nodes: "
                + story.getNodes()
                    .size()
                + ", Connections: "
                + story.getConnections()
                    .size());
    }

    private void showStoryState(EntityPlayerMP player, String id) {
        StoryDefinition story = repository.getSnapshot()
            .getStory(id);
        if (story == null) {
            ChatMessages.error(player, "Unknown Story ID: " + id);
            return;
        }
        StoryInstance instance = storyRuntime.getInstance(player, story);
        ChatMessages.info(player, "Story " + id + ": " + instance.getState() + " at " + instance.getCurrentNodeId());
        if (!instance.getError()
            .isEmpty()) {
            ChatMessages.error(player, instance.getError());
        }
    }

    private void processQuest(ICommandSender sender, String[] arguments) {
        if (arguments.length < 2) {
            throw new WrongUsageException("/dgrpg quest <list|info|start|journal|progress|reset>");
        }
        String action = arguments[1].toLowerCase();
        if ("list".equals(action)) {
            listQuests(sender);
        } else if ("info".equals(action)) {
            requireLength(arguments, 3, "/dgrpg quest info <id>");
            showQuest(sender, arguments[2]);
        } else if ("start".equals(action)) {
            requireLength(arguments, 3, "/dgrpg quest start <id>");
            questRuntime.start(requireMultiplayerPlayer(sender), arguments[2]);
        } else if ("journal".equals(action)) {
            requireLength(arguments, 2, "/dgrpg quest journal");
            openQuestJournal(requireMultiplayerPlayer(sender));
        } else if ("progress".equals(action)) {
            requireLength(arguments, 2, "/dgrpg quest progress");
            showQuestProgress(requireMultiplayerPlayer(sender));
        } else if ("reset".equals(action)) {
            requireLength(arguments, 3, "/dgrpg quest reset <id>");
            questRuntime.reset(requireMultiplayerPlayer(sender), arguments[2]);
        } else {
            throw new WrongUsageException("/dgrpg quest <list|info|start|journal|progress|reset>");
        }
    }

    private void listQuests(ICommandSender sender) {
        if (repository.getSnapshot()
            .getQuests()
            .isEmpty()) {
            ChatMessages.info(sender, "No Quests are loaded.");
            return;
        }
        ChatMessages.info(
            sender,
            "Quests (" + repository.getSnapshot()
                .getQuests()
                .size() + "):");
        for (QuestDefinition quest : repository.getSnapshot()
            .getQuests()
            .values()) {
            ChatMessages.info(sender, "- " + quest.getId() + " — " + quest.getTitle());
        }
    }

    private void showQuest(ICommandSender sender, String id) {
        QuestDefinition quest = repository.getSnapshot()
            .getQuest(id);
        if (quest == null) {
            ChatMessages.error(sender, "Unknown Quest ID: " + id);
            return;
        }
        ChatMessages.info(sender, "Quest: " + quest.getId() + " — " + quest.getTitle());
        ChatMessages.info(sender, quest.getDescription());
        for (QuestObjective objective : quest.getObjectives()) {
            ChatMessages.info(
                sender,
                "- [" + objective.getType() + "] " + objective.getDescription() + " x" + objective.getRequiredAmount());
        }
    }

    private void openQuestJournal(EntityPlayerMP player) {
        questRuntime.openJournal(player);
    }

    private void showQuestProgress(EntityPlayerMP player) {
        List<QuestJournalEntry> entries = questRuntime.getJournal(player);
        if (entries.isEmpty()) {
            ChatMessages.info(player, "Quest journal is empty.");
            return;
        }
        for (QuestJournalEntry entry : entries) {
            ChatMessages.info(player, entry.getTitle() + " [" + entry.getStatus() + "]");
            for (String objective : entry.getObjectiveLines()) {
                ChatMessages.info(player, "  " + objective);
            }
        }
    }

    private void showStatus(ICommandSender sender) {
        ProjectRepository.ReloadResult result = repository.getLastReload();
        ChatMessages.info(
            sender,
            "Project path: " + repository.getProjectDirectory()
                .getAbsolutePath());
        ChatMessages.info(sender, "CustomNPC+: " + (Loader.isModLoaded("customnpcs") ? "loaded" : "missing"));
        if (result.isSuccessful()) {
            ChatMessages.success(sender, result.getSummary());
        } else {
            ChatMessages.error(sender, result.getSummary());
        }
    }

    private void reload(ICommandSender sender) {
        ProjectRepository.ReloadResult result = repository.reload();
        if (result.isSuccessful()) {
            dialogueSessions.clearSessions();
            storyRuntime.resetInstances();
            ChatMessages.success(sender, result.getSummary());
        } else {
            ChatMessages.error(sender, "Reload failed: " + result.getSummary());
        }
    }

    private void processDialogue(ICommandSender sender, String[] arguments) {
        if (arguments.length < 2) {
            throw new WrongUsageException("/dgrpg dialogue <list|info|play|last-result>");
        }
        String action = arguments[1].toLowerCase();
        if ("list".equals(action)) {
            listDialogues(sender);
        } else if ("info".equals(action)) {
            requireLength(arguments, 3, "/dgrpg dialogue info <id>");
            showDialogue(sender, arguments[2]);
        } else if ("play".equals(action)) {
            requireLength(arguments, 3, "/dgrpg dialogue play <id>");
            dialogueSessions.start(requireMultiplayerPlayer(sender), arguments[2]);
        } else if ("last-result".equals(action)) {
            requireLength(arguments, 2, "/dgrpg dialogue last-result");
            showLastResult(requirePlayer(sender));
        } else {
            throw new WrongUsageException("/dgrpg dialogue <list|info|play|last-result>");
        }
    }

    private void listDialogues(ICommandSender sender) {
        if (repository.getSnapshot()
            .getDialogues()
            .isEmpty()) {
            ChatMessages.info(sender, "No Dialogues are loaded.");
            return;
        }
        ChatMessages.info(
            sender,
            "Dialogues (" + repository.getSnapshot()
                .getDialogues()
                .size() + "):");
        for (DialogueDefinition dialogue : repository.getSnapshot()
            .getDialogues()
            .values()) {
            ChatMessages.info(sender, "- " + dialogue.getId() + " — " + dialogue.getTitle());
        }
    }

    private void showDialogue(ICommandSender sender, String id) {
        DialogueDefinition dialogue = repository.getSnapshot()
            .getDialogue(id);
        if (dialogue == null) {
            ChatMessages.error(sender, "Unknown Dialogue ID: " + id);
            return;
        }
        ChatMessages.info(sender, "Dialogue: " + dialogue.getId() + " — " + dialogue.getTitle());
        ChatMessages.info(sender, "Speakers: " + dialogue.getSpeakers());
        ChatMessages.info(
            sender,
            "Entry: " + dialogue.getEntry()
                + ", Nodes: "
                + dialogue.getNodes()
                    .size());
    }

    private void showLastResult(EntityPlayer player) {
        DialogueResult result = dialogueSessions.getLastResult(player.getUniqueID());
        if (result == null) {
            ChatMessages.info(player, "No Dialogue Result has been returned in this server session.");
            return;
        }
        ChatMessages.info(player, "Last Dialogue Result: " + result.getDialogueId() + " -> " + result.getResult());
    }

    private void processActor(ICommandSender sender, String[] arguments) {
        if (arguments.length < 2) {
            throw new WrongUsageException("/dgrpg actor <list|info|select|bind|unbind>");
        }

        String action = arguments[1].toLowerCase();
        if ("list".equals(action)) {
            listActors(sender);
        } else if ("info".equals(action)) {
            requireLength(arguments, 3, "/dgrpg actor info <id>");
            showActor(sender, arguments[2]);
        } else if ("select".equals(action)) {
            requireLength(arguments, 3, "/dgrpg actor select <id>");
            selectActor(requirePlayer(sender), arguments[2]);
        } else if ("bind".equals(action)) {
            requireLength(arguments, 3, "/dgrpg actor bind <id>");
            bindActor(requirePlayer(sender), arguments[2]);
        } else if ("unbind".equals(action)) {
            requireLength(arguments, 2, "/dgrpg actor unbind");
            unbindActor(requirePlayer(sender));
        } else {
            throw new WrongUsageException("/dgrpg actor <list|info|select|bind|unbind>");
        }
    }

    private void listActors(ICommandSender sender) {
        if (repository.getSnapshot()
            .getActors()
            .isEmpty()) {
            ChatMessages.info(sender, "No Actors are loaded.");
            return;
        }
        ChatMessages.info(
            sender,
            "Actors (" + repository.getSnapshot()
                .getActors()
                .size() + "):");
        for (ActorDefinition actor : repository.getSnapshot()
            .getActors()
            .values()) {
            ChatMessages.info(sender, "- " + actor.getId() + " — " + actor.getDisplayName());
        }
    }

    private void showActor(ICommandSender sender, String id) {
        ActorDefinition actor = repository.getSnapshot()
            .getActor(id);
        if (actor == null) {
            ChatMessages.error(sender, "Unknown Actor ID: " + id);
            return;
        }
        ChatMessages.info(sender, "Actor: " + actor.getId());
        ChatMessages.info(sender, "Display Name: " + actor.getDisplayName());
        ChatMessages.info(sender, "Tags: " + actor.getTags());
        if (!actor.getNotes()
            .isEmpty()) {
            ChatMessages.info(sender, "Notes: " + actor.getNotes());
        }
    }

    private void selectActor(EntityPlayer player, String id) {
        ActorDefinition actor = repository.getSnapshot()
            .getActor(id);
        if (actor == null) {
            ChatMessages.error(player, "Unknown Actor ID: " + id);
            return;
        }
        sessions.selectActor(player, id);
        ChatMessages.success(player, "Selected Actor " + id + ". Right-click a CNPC with the editor tool to bind it.");
    }

    private void bindActor(EntityPlayer player, String id) {
        Entity target = requireTarget(player);
        if (ActorBindingActions.bind(repository, player, target, id)) {
            sessions.selectActor(player, id);
        }
    }

    private void unbindActor(EntityPlayer player) {
        ActorBindingActions.unbind(player, requireTarget(player));
    }

    private static EntityPlayer requirePlayer(ICommandSender sender) {
        if (!(sender instanceof EntityPlayer)) {
            throw new CommandException("This command requires a player.");
        }
        return (EntityPlayer) sender;
    }

    private static EntityPlayerMP requireMultiplayerPlayer(ICommandSender sender) {
        if (!(sender instanceof EntityPlayerMP)) {
            throw new CommandException("This command requires a server-side player.");
        }
        return (EntityPlayerMP) sender;
    }

    private static Entity requireTarget(EntityPlayer player) {
        Entity target = EntityTargeting.findLookedAtEntity(player, TARGET_DISTANCE);
        if (target == null) {
            throw new CommandException("Look directly at a CustomNPC+ NPC within 8 blocks.");
        }
        if (!CustomNpcActorBinding.isCustomNpc(target)) {
            throw new CommandException("The targeted entity is not a CustomNPC+ NPC.");
        }
        return target;
    }

    private static void requireLength(String[] arguments, int expected, String usage) {
        if (arguments.length != expected) {
            throw new WrongUsageException(usage);
        }
    }

    @Override
    @SuppressWarnings("rawtypes")
    public List addTabCompletionOptions(ICommandSender sender, String[] arguments) {
        if (arguments.length == 1) {
            return getListOfStringsMatchingLastWord(
                arguments,
                "status",
                "reload",
                "actor",
                "dialogue",
                "quest",
                "story");
        }
        if (arguments.length == 2 && "actor".equalsIgnoreCase(arguments[0])) {
            return getListOfStringsMatchingLastWord(arguments, "list", "info", "select", "bind", "unbind");
        }
        if (arguments.length == 3 && "actor".equalsIgnoreCase(arguments[0])
            && Arrays.asList("info", "select", "bind")
                .contains(arguments[1].toLowerCase())) {
            List<String> actorIds = new ArrayList<String>(
                repository.getSnapshot()
                    .getActors()
                    .keySet());
            return getListOfStringsFromIterableMatchingLastWord(arguments, actorIds);
        }
        if (arguments.length == 2 && "dialogue".equalsIgnoreCase(arguments[0])) {
            return getListOfStringsMatchingLastWord(arguments, "list", "info", "play", "last-result");
        }
        if (arguments.length == 3 && "dialogue".equalsIgnoreCase(arguments[0])
            && Arrays.asList("info", "play")
                .contains(arguments[1].toLowerCase())) {
            List<String> dialogueIds = new ArrayList<String>(
                repository.getSnapshot()
                    .getDialogues()
                    .keySet());
            return getListOfStringsFromIterableMatchingLastWord(arguments, dialogueIds);
        }
        if (arguments.length == 2 && "quest".equalsIgnoreCase(arguments[0])) {
            return getListOfStringsMatchingLastWord(arguments, "list", "info", "start", "journal", "progress", "reset");
        }
        if (arguments.length == 3 && "quest".equalsIgnoreCase(arguments[0])
            && Arrays.asList("info", "start", "reset")
                .contains(arguments[1].toLowerCase())) {
            List<String> questIds = new ArrayList<String>(
                repository.getSnapshot()
                    .getQuests()
                    .keySet());
            return getListOfStringsFromIterableMatchingLastWord(arguments, questIds);
        }
        if (arguments.length == 2 && "story".equalsIgnoreCase(arguments[0])) {
            return getListOfStringsMatchingLastWord(arguments, "list", "info", "start", "state", "reset");
        }
        if (arguments.length == 3 && "story".equalsIgnoreCase(arguments[0])) {
            List<String> storyIds = new ArrayList<String>(
                repository.getSnapshot()
                    .getStories()
                    .keySet());
            return getListOfStringsFromIterableMatchingLastWord(arguments, storyIds);
        }
        return null;
    }
}
