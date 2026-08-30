package darkgrey.rpg.command;

import java.util.ArrayList;
import java.util.Arrays;
import java.util.Collections;
import java.util.Comparator;
import java.util.List;

import net.minecraft.command.CommandBase;
import net.minecraft.command.CommandException;
import net.minecraft.command.ICommandSender;
import net.minecraft.command.WrongUsageException;
import net.minecraft.entity.Entity;
import net.minecraft.entity.player.EntityPlayer;
import net.minecraft.entity.player.EntityPlayerMP;
import net.minecraft.item.ItemStack;
import net.minecraft.server.MinecraftServer;
import net.minecraft.util.MathHelper;
import net.minecraftforge.common.MinecraftForge;
import net.minecraftforge.event.entity.player.EntityInteractEvent;
import net.minecraftforge.event.entity.player.PlayerInteractEvent;

import cpw.mods.fml.common.Loader;
import darkgrey.rpg.compat.customnpcs.CustomNpcActorBinding;
import darkgrey.rpg.content.ModItems;
import darkgrey.rpg.dialogue.DialogueDefinition;
import darkgrey.rpg.dialogue.runtime.DialogueResult;
import darkgrey.rpg.dialogue.runtime.DialogueSessionManager;
import darkgrey.rpg.entitytools.StorageBoxState;
import darkgrey.rpg.entitytools.StoragePayload;
import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.identity.EntityDgrIdentityResolver;
import darkgrey.rpg.identity.NpcIdentitySavedData;
import darkgrey.rpg.item.ItemStorageBox;
import darkgrey.rpg.item.identity.ItemIdentitySavedData;
import darkgrey.rpg.nominator.NominatorResult;
import darkgrey.rpg.nominator.NominatorSavedData;
import darkgrey.rpg.nominator.NominatorService;
import darkgrey.rpg.project.ActorDefinition;
import darkgrey.rpg.project.ProjectLoadException;
import darkgrey.rpg.project.ProjectRepository;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.project.packages.StoryPackageLoader;
import darkgrey.rpg.project.packages.StoryPackageSnapshotMerger;
import darkgrey.rpg.quest.QuestDefinition;
import darkgrey.rpg.quest.QuestObjective;
import darkgrey.rpg.quest.runtime.QuestJournalEntry;
import darkgrey.rpg.quest.runtime.QuestRuntimeService;
import darkgrey.rpg.runtime.ActorBindingActions;
import darkgrey.rpg.runtime.ChatMessages;
import darkgrey.rpg.runtime.EditorSessionManager;
import darkgrey.rpg.runtime.EntityTargeting;
import darkgrey.rpg.session.forge.CanonicalSessionForgeManager;
import darkgrey.rpg.story.StoryDefinition;
import darkgrey.rpg.story.canonical.forge.CanonicalStoryForgeManager;
import darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceSnapshot;
import darkgrey.rpg.story.canonical.runtime.CanonicalStorySnapshot;
import darkgrey.rpg.story.runtime.StoryInstance;
import darkgrey.rpg.story.runtime.StoryRuntimeService;
import darkgrey.rpg.task.event.CanonicalTaskDispatchResult;
import darkgrey.rpg.task.forge.CanonicalTaskForgeManager;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot;
import darkgrey.rpg.task.runtime.CanonicalTaskEvent;

public final class CommandDarkGreyRpg extends CommandBase {

    private static final double TARGET_DISTANCE = 8.0D;

    private final ProjectRepository repository;
    private final EditorSessionManager sessions;
    private final DialogueSessionManager dialogueSessions;
    private final QuestRuntimeService questRuntime;
    private final StoryRuntimeService storyRuntime;
    private final CanonicalSessionForgeManager canonicalSessionManager;
    private final CanonicalTaskForgeManager canonicalTaskManager;
    private final CanonicalStoryForgeManager canonicalStoryManager;
    private final StoryPackageLoader storyPackageLoader;

    /** Original constructor retained for legacy registrations and probes. */
    public CommandDarkGreyRpg(ProjectRepository repository, EditorSessionManager sessions,
        DialogueSessionManager dialogueSessions, QuestRuntimeService questRuntime, StoryRuntimeService storyRuntime) {
        this(
            repository,
            sessions,
            dialogueSessions,
            questRuntime,
            storyRuntime,
            new CanonicalSessionForgeManager(repository),
            null,
            null,
            null);
    }

    public CommandDarkGreyRpg(ProjectRepository repository, EditorSessionManager sessions,
        DialogueSessionManager dialogueSessions, QuestRuntimeService questRuntime, StoryRuntimeService storyRuntime,
        CanonicalSessionForgeManager canonicalSessionManager) {
        this(
            repository,
            sessions,
            dialogueSessions,
            questRuntime,
            storyRuntime,
            canonicalSessionManager,
            null,
            null,
            null);
    }

    /** Compatibility overload for callers that only supply the Stage 4 Task manager. */
    public CommandDarkGreyRpg(ProjectRepository repository, EditorSessionManager sessions,
        DialogueSessionManager dialogueSessions, QuestRuntimeService questRuntime, StoryRuntimeService storyRuntime,
        CanonicalTaskForgeManager canonicalTaskManager) {
        this(
            repository,
            sessions,
            dialogueSessions,
            questRuntime,
            storyRuntime,
            new CanonicalSessionForgeManager(repository),
            canonicalTaskManager,
            null,
            null);
    }

    public CommandDarkGreyRpg(ProjectRepository repository, EditorSessionManager sessions,
        DialogueSessionManager dialogueSessions, QuestRuntimeService questRuntime, StoryRuntimeService storyRuntime,
        CanonicalSessionForgeManager canonicalSessionManager, CanonicalTaskForgeManager canonicalTaskManager) {
        this(
            repository,
            sessions,
            dialogueSessions,
            questRuntime,
            storyRuntime,
            canonicalSessionManager,
            canonicalTaskManager,
            null,
            null);
    }

    public CommandDarkGreyRpg(ProjectRepository repository, EditorSessionManager sessions,
        DialogueSessionManager dialogueSessions, QuestRuntimeService questRuntime, StoryRuntimeService storyRuntime,
        CanonicalSessionForgeManager canonicalSessionManager, CanonicalTaskForgeManager canonicalTaskManager,
        CanonicalStoryForgeManager canonicalStoryManager) {
        this(
            repository,
            sessions,
            dialogueSessions,
            questRuntime,
            storyRuntime,
            canonicalSessionManager,
            canonicalTaskManager,
            canonicalStoryManager,
            null);
    }

    public CommandDarkGreyRpg(ProjectRepository repository, EditorSessionManager sessions,
        DialogueSessionManager dialogueSessions, QuestRuntimeService questRuntime, StoryRuntimeService storyRuntime,
        CanonicalSessionForgeManager canonicalSessionManager, CanonicalTaskForgeManager canonicalTaskManager,
        CanonicalStoryForgeManager canonicalStoryManager, StoryPackageLoader storyPackageLoader) {
        if (canonicalSessionManager == null)
            throw new IllegalArgumentException("Canonical Session manager is required.");
        this.repository = repository;
        this.sessions = sessions;
        this.dialogueSessions = dialogueSessions;
        this.questRuntime = questRuntime;
        this.storyRuntime = storyRuntime;
        this.canonicalSessionManager = canonicalSessionManager;
        this.canonicalTaskManager = canonicalTaskManager;
        this.canonicalStoryManager = canonicalStoryManager;
        this.storyPackageLoader = storyPackageLoader;
    }

    @Override
    public String getCommandName() {
        return "dgrpg";
    }

    /** 0.3.1 public command surface; the historical dgrpg name remains the primary registration key. */
    @Override
    public List<String> getCommandAliases() {
        return Collections.singletonList("dgr");
    }

    @Override
    public String getCommandUsage(ICommandSender sender) {
        return "/dgr <status|reload|actor|dialogue|quest|story|session|task|debug>";
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
        if ("session".equalsIgnoreCase(arguments[0])) {
            processSession(sender, arguments);
            return;
        }
        if ("task".equalsIgnoreCase(arguments[0])) {
            processTask(sender, arguments);
            return;
        }
        if ("debug".equalsIgnoreCase(arguments[0])) {
            processDebug(sender, arguments);
            return;
        }
        throw new WrongUsageException(getCommandUsage(sender));
    }

    /** Temporary acceptance-only entry that directly calls existing DGR services. */
    private void processDebug(ICommandSender sender, String[] arguments) {
        String usage = "/dgr debug <player dimension <online_player> <-1|0|1>|nominator clear_type_group <entity_type> <group_id>|item <bind_exact|bind_exact_group|bind_fuzzy> <id>|story <start|set_logic|state> [online_player] ...|task <start|emit_kill|state> [online_player] ...|cnpc <bind_nearest <npc_id>|bind_group_nearest <group_id>|list_nearby>|storage <interact_nearest|release_here|status>>";
        if (arguments.length == 5 && "player".equalsIgnoreCase(arguments[1])
            && "dimension".equalsIgnoreCase(arguments[2])) {
            EntityPlayerMP player = namedMultiplayerPlayer(arguments[3]);
            int dimension;
            try {
                dimension = Integer.parseInt(arguments[4]);
            } catch (NumberFormatException invalidDimension) {
                throw new WrongUsageException(usage);
            }
            if (dimension < -1 || dimension > 1) throw new WrongUsageException(usage);
            MinecraftServer server = MinecraftServer.getServer();
            if (server == null || server.getConfigurationManager() == null
                || server.worldServerForDimension(dimension) == null)
                throw new CommandException("Target dimension is unavailable: " + dimension);
            server.getConfigurationManager()
                .transferPlayerToDimension(player, dimension);
            ChatMessages.success(sender, "Debug player dimension: " + arguments[3] + " -> " + player.dimension);
            return;
        }
        if (arguments.length >= 3 && "story".equalsIgnoreCase(arguments[1])) {
            if (canonicalStoryManager == null) {
                ChatMessages.error(sender, "Canonical Story manager is unavailable.");
                return;
            }
            if (arguments.length == 5 && "start".equalsIgnoreCase(arguments[2])) {
                EntityPlayerMP player = namedMultiplayerPlayer(arguments[3]);
                boolean routed = canonicalStoryManager.startByEntry(player, arguments[4]);
                ChatMessages.success(sender, "Debug Story start: " + arguments[4] + ", routed=" + routed);
                return;
            }
            boolean namedLogic = arguments.length == 7 && "set_logic".equalsIgnoreCase(arguments[2]);
            boolean directLogic = arguments.length == 6 && "set_logic".equalsIgnoreCase(arguments[2]);
            if (namedLogic || directLogic) {
                EntityPlayerMP player = namedLogic ? namedMultiplayerPlayer(arguments[3])
                    : requireMultiplayerPlayer(sender);
                int storyIndex = namedLogic ? 4 : 3;
                boolean value;
                if ("true".equalsIgnoreCase(arguments[storyIndex + 2])) value = true;
                else if ("false".equalsIgnoreCase(arguments[storyIndex + 2])) value = false;
                else throw new WrongUsageException(usage);
                boolean routed = canonicalStoryManager
                    .setLogicInput(player, arguments[storyIndex], arguments[storyIndex + 1], value);
                ChatMessages.success(
                    sender,
                    "Debug Story Logic: " + arguments[storyIndex]
                        + "."
                        + arguments[storyIndex + 1]
                        + "="
                        + value
                        + ", routed="
                        + routed);
                return;
            }
            boolean namedState = arguments.length == 5 && "state".equalsIgnoreCase(arguments[2]);
            boolean directState = arguments.length == 4 && "state".equalsIgnoreCase(arguments[2]);
            if (namedState || directState) {
                EntityPlayerMP player = namedState ? namedMultiplayerPlayer(arguments[3])
                    : requireMultiplayerPlayer(sender);
                String storyId = arguments[namedState ? 4 : 3];
                CanonicalStoryInstanceSnapshot instance = canonicalStoryManager.snapshot(player, storyId);
                if (instance == null) {
                    ChatMessages.info(sender, "Canonical Story state: absent (" + storyId + ")");
                    return;
                }
                CanonicalStorySnapshot runtime = instance.getRuntimeSnapshot();
                ChatMessages.info(
                    sender,
                    "Canonical Story state: " + storyId
                        + " status="
                        + runtime.getStatus()
                        + ", wait="
                        + runtime.getWaitKind()
                        + ", node="
                        + runtime.getCurrentNodeId()
                        + ", logic="
                        + runtime.getExternalLogicInputs());
                return;
            }
            throw new WrongUsageException(usage);
        }
        if (arguments.length >= 3 && "task".equalsIgnoreCase(arguments[1])) {
            if (canonicalTaskManager == null) {
                ChatMessages.error(sender, "Canonical Task manager is unavailable.");
                return;
            }
            if (arguments.length == 7 && "start".equalsIgnoreCase(arguments[2])) {
                EntityPlayerMP player = namedMultiplayerPlayer(arguments[3]);
                CanonicalTaskInstanceSnapshot snapshot = canonicalTaskManager
                    .start(player, arguments[5], arguments[6], arguments[4]);
                ChatMessages.success(sender, "Debug Task start: " + arguments[4] + " status=" + snapshot.getStatus());
                return;
            }
            if (arguments.length == 6 && "state".equalsIgnoreCase(arguments[2])) {
                EntityPlayerMP player = namedMultiplayerPlayer(arguments[3]);
                CanonicalTaskInstanceSnapshot snapshot = canonicalTaskManager
                    .snapshot(player, arguments[4], arguments[5]);
                if (snapshot == null) {
                    ChatMessages.info(sender, "Canonical Task state: absent.");
                    return;
                }
                ChatMessages.info(
                    sender,
                    "Canonical Task state: status=" + snapshot.getStatus()
                        + ", progress="
                        + snapshot.getRuntimeSnapshot()
                            .getProgress()
                        + ", objectives="
                        + snapshot.getRuntimeSnapshot()
                            .getObjectiveStatuses()
                        + ", logic="
                        + snapshot.getRuntimeSnapshot()
                            .getLogicValues());
                return;
            }
            boolean namedKill = !(sender instanceof EntityPlayerMP) && (arguments.length == 5 || arguments.length == 6)
                && "emit_kill".equalsIgnoreCase(arguments[2]);
            boolean directKill = sender instanceof EntityPlayerMP && (arguments.length == 4 || arguments.length == 5)
                && "emit_kill".equalsIgnoreCase(arguments[2]);
            if (!namedKill && !directKill) throw new WrongUsageException(usage);
            EntityPlayerMP player = namedKill ? namedMultiplayerPlayer(arguments[3]) : requireMultiplayerPlayer(sender);
            int entityIndex = namedKill ? 4 : 3;
            int amount = 1;
            if (arguments.length == entityIndex + 2) {
                try {
                    amount = Integer.parseInt(arguments[entityIndex + 1]);
                } catch (NumberFormatException invalidAmount) {
                    throw new WrongUsageException(usage);
                }
                if (amount <= 0) throw new WrongUsageException(usage);
            }
            CanonicalTaskDispatchResult dispatch = canonicalTaskManager
                .dispatch(player, CanonicalTaskEvent.killEntity(arguments[entityIndex], amount));
            ChatMessages.success(
                sender,
                "Debug Task event: candidates=" + dispatch.getCandidateCount()
                    + ", changed="
                    + dispatch.getChangedInstanceCount()
                    + ", settled="
                    + dispatch.getSettledInstances()
                        .size());
            return;
        }
        if (arguments.length >= 3 && "cnpc".equalsIgnoreCase(arguments[1])) {
            EntityPlayerMP player = requireMultiplayerPlayer(sender);
            if (arguments.length == 4 && "bind_nearest".equalsIgnoreCase(arguments[2])) {
                Entity target = nearestCustomNpc(player, 16.0D);
                if (target == null) {
                    ChatMessages.error(player, "No CustomNPC+ NPC is within 16 blocks.");
                    return;
                }
                NominatorResult binding = NominatorService.bindEntity(
                    true,
                    target.getUniqueID(),
                    NominatorService.entityType(target),
                    target.dimension,
                    arguments[3],
                    Collections.<String>emptyList(),
                    null,
                    true,
                    repository.getSnapshot(),
                    NpcIdentitySavedData.get(),
                    NominatorSavedData.get());
                if (binding.isAccepted()) ChatMessages.success(
                    player,
                    "Debug CNPC bound " + target
                        .getCommandSenderName() + " [" + target.getUniqueID() + "] to " + arguments[3] + ".");
                else ChatMessages.error(player, "Debug CNPC bind rejected: " + binding.getCode());
                return;
            }
            if (arguments.length == 4 && "bind_group_nearest".equalsIgnoreCase(arguments[2])) {
                Entity target = nearestCustomNpc(player, 16.0D);
                if (target == null) {
                    ChatMessages.error(player, "No CustomNPC+ NPC is within 16 blocks.");
                    return;
                }
                String npcId = NpcIdentitySavedData.get()
                    .getNpcId(target.getUniqueID());
                NominatorResult binding = NominatorService.bindEntity(
                    true,
                    target.getUniqueID(),
                    NominatorService.entityType(target),
                    target.dimension,
                    npcId,
                    Collections.singletonList(arguments[3]),
                    null,
                    true,
                    repository.getSnapshot(),
                    NpcIdentitySavedData.get(),
                    NominatorSavedData.get());
                if (binding.isAccepted()) ChatMessages.success(
                    player,
                    "Debug CNPC added group " + arguments[3] + " to " + target.getCommandSenderName() + ".");
                else ChatMessages.error(player, "Debug CNPC group bind rejected: " + binding.getCode());
                return;
            }
            if (arguments.length == 3 && "list_nearby".equalsIgnoreCase(arguments[2])) {
                List<Entity> nearby = nearbyCustomNpcs(player, 16.0D);
                if (nearby.isEmpty()) {
                    ChatMessages.info(player, "No CustomNPC+ NPC is within 16 blocks.");
                    return;
                }
                NpcIdentitySavedData identities = NpcIdentitySavedData.get();
                NominatorSavedData selections = NominatorSavedData.get();
                ChatMessages.info(player, "Nearby CustomNPC+ NPCs (" + nearby.size() + "):");
                for (Entity entity : nearby) {
                    EntityDgrIdentityResolver.Resolution resolved = EntityDgrIdentityResolver
                        .resolve(entity, identities, selections);
                    ChatMessages.info(
                        player,
                        "- " + entity.getCommandSenderName()
                            + " ["
                            + entity.getUniqueID()
                            + "] npc_id="
                            + String.valueOf(resolved.getActorId()));
                }
                return;
            }
            throw new WrongUsageException(usage);
        }
        if (arguments.length == 3 && "storage".equalsIgnoreCase(arguments[1])) {
            EntityPlayerMP player = requireMultiplayerPlayer(sender);
            String action = arguments[2].toLowerCase();
            if ("interact_nearest".equals(action)) {
                Entity target = nearestCustomNpc(player, 16.0D);
                if (target == null) {
                    ChatMessages.error(player, "No CustomNPC+ NPC is within 16 blocks.");
                    return;
                }
                MinecraftForge.EVENT_BUS.post(new EntityInteractEvent(player, target));
                return;
            }
            if ("release_here".equals(action)) {
                MinecraftForge.EVENT_BUS.post(
                    new PlayerInteractEvent(
                        player,
                        PlayerInteractEvent.Action.RIGHT_CLICK_BLOCK,
                        MathHelper.floor_double(player.posX),
                        MathHelper.floor_double(player.posY) - 1,
                        MathHelper.floor_double(player.posZ),
                        1,
                        player.worldObj));
                return;
            }
            if ("status".equals(action)) {
                ItemStack held = player.getHeldItem();
                if (held == null || held.getItem() != ModItems.storageBox) {
                    ChatMessages.error(player, "Hold a DGR Storage Box first.");
                    return;
                }
                StorageBoxState state = ItemStorageBox.loadState(held);
                if (!state.isOccupied()) {
                    ChatMessages.info(player, "Debug Storage: empty.");
                    return;
                }
                StoragePayload payload = state.getPayload();
                ChatMessages.info(
                    player,
                    "Debug Storage: mode=" + payload.getMode()
                        + ", npc_id="
                        + payload.getReservedNpcId()
                        + ", groups="
                        + payload.getTemplate()
                            .getGroups());
                return;
            }
            throw new WrongUsageException(usage);
        }
        NominatorResult result;
        if (arguments.length == 5 && "nominator".equalsIgnoreCase(arguments[1])
            && "clear_type_group".equalsIgnoreCase(arguments[2])) {
            result = NominatorService.bindEntityTypeGroup(
                true,
                arguments[3],
                arguments[4],
                false,
                repository.getSnapshot(),
                NominatorSavedData.get());
        } else if (arguments.length == 4 && "item".equalsIgnoreCase(arguments[1])) {
            EntityPlayerMP player = requireMultiplayerPlayer(sender);
            ItemStack held = player.getHeldItem();
            String action = arguments[2].toLowerCase();
            if ("bind_exact".equals(action)) {
                result = NominatorService.bindInventory(
                    true,
                    held,
                    arguments[3],
                    null,
                    Collections.<String>emptyList(),
                    repository.getSnapshot(),
                    ItemIdentitySavedData.get());
            } else if ("bind_exact_group".equals(action)) {
                result = NominatorService.bindInventory(
                    true,
                    held,
                    null,
                    arguments[3],
                    Collections.<String>emptyList(),
                    repository.getSnapshot(),
                    ItemIdentitySavedData.get());
            } else if ("bind_fuzzy".equals(action)) {
                result = NominatorService.bindInventory(
                    true,
                    held,
                    null,
                    null,
                    Collections.singletonList(arguments[3]),
                    repository.getSnapshot(),
                    ItemIdentitySavedData.get());
            } else throw new WrongUsageException(usage);
        } else throw new WrongUsageException(usage);
        if (result.isAccepted()) ChatMessages.success(sender, "Debug Nominator: " + result.getExplanation());
        else ChatMessages.error(sender, "Debug Nominator rejected: " + result.getCode());
    }

    private static Entity nearestCustomNpc(EntityPlayerMP player, double range) {
        List<Entity> nearby = nearbyCustomNpcs(player, range);
        return nearby.isEmpty() ? null : nearby.get(0);
    }

    @SuppressWarnings("unchecked")
    private static List<Entity> nearbyCustomNpcs(final EntityPlayerMP player, double range) {
        List<Entity> result = new ArrayList<Entity>();
        double maximumDistance = range * range;
        for (Object value : player.worldObj.loadedEntityList) {
            if (!(value instanceof Entity)) continue;
            Entity entity = (Entity) value;
            if (!CustomNpcActorBinding.isCustomNpc(entity) || player.getDistanceSqToEntity(entity) > maximumDistance)
                continue;
            result.add(entity);
        }
        Collections.sort(result, new Comparator<Entity>() {

            @Override
            public int compare(Entity left, Entity right) {
                return Double.compare(player.getDistanceSqToEntity(left), player.getDistanceSqToEntity(right));
            }
        });
        return result;
    }

    private void processSession(ICommandSender sender, String[] arguments) {
        if (arguments.length < 2) {
            throw new WrongUsageException(sessionUsage(arguments));
        }
        String action = arguments[1].toLowerCase();
        if ("play".equals(action)) {
            requireLength(arguments, 4, sessionUsage(arguments));
            EntityPlayerMP player = requireMultiplayerPlayer(sender);
            if (canonicalSessionManager.start(player, arguments[2], arguments[3])) {
                ChatMessages.success(player, "Session started: " + arguments[2] + " (" + arguments[3] + ")");
            } else {
                ChatMessages.error(player, "Could not start Session: " + arguments[2]);
            }
        } else if ("resume".equals(action)) {
            requireLength(arguments, 3, sessionUsage(arguments));
            EntityPlayerMP player = requireMultiplayerPlayer(sender);
            if (canonicalSessionManager.resume(player, arguments[2])) {
                ChatMessages.success(player, "Session resumed: " + arguments[2]);
            } else {
                ChatMessages.error(player, "Could not resume Session: " + arguments[2]);
            }
        } else {
            throw new WrongUsageException(sessionUsage(arguments));
        }
    }

    /** Package-private pure usage seam for command probes; no sender or Minecraft runtime is required. */
    static String sessionUsage(String[] arguments) {
        if (arguments == null || arguments.length < 2) return "/dgr session <play|resume>";
        if ("play".equalsIgnoreCase(arguments[1])) return "/dgr session play <story_id> <aggregate_node_id>";
        if ("resume".equalsIgnoreCase(arguments[1])) return "/dgr session resume <story_id>";
        return "/dgr session <play|resume>";
    }

    private void processTask(ICommandSender sender, String[] arguments) {
        if (arguments.length < 2) throw new WrongUsageException(taskUsage(arguments));
        String action = arguments[1].toLowerCase();
        if ("list".equals(action)) {
            listTasks(sender);
        } else if ("info".equals(action)) {
            requireLength(arguments, 3, "/dgr task info <task_id>");
            showTask(sender, arguments[2]);
        } else if ("start".equals(action)) {
            requireLength(arguments, 5, "/dgr task start <task_id> <story_instance_id> <placement_id>");
            EntityPlayerMP player = requireMultiplayerPlayer(sender);
            if (canonicalTaskManager == null) {
                ChatMessages.error(player, "Canonical Task manager is unavailable.");
                return;
            }
            CanonicalTaskInstanceSnapshot snapshot = canonicalTaskManager
                .start(player, arguments[3], arguments[4], arguments[2]);
            ChatMessages.success(
                player,
                "Direct Stage 4 Task start: " + arguments[2]
                    + " (story="
                    + arguments[3]
                    + ", placement="
                    + arguments[4]
                    + ", status="
                    + snapshot.getStatus()
                    + ")");
        } else if ("journal".equals(action)) {
            requireLength(arguments, 2, "/dgr task journal");
            openQuestJournal(requireMultiplayerPlayer(sender));
        } else if ("progress".equals(action)) {
            requireLength(arguments, 2, "/dgr task progress");
            showQuestProgress(requireMultiplayerPlayer(sender));
        } else {
            throw new WrongUsageException(taskUsage(arguments));
        }
    }

    /** Package-private pure usage seam for the Stage 4 command probe. */
    static String taskUsage(String[] arguments) {
        if (arguments == null || arguments.length < 2) return "/dgr task <list|info|start|journal|progress>";
        if ("info".equalsIgnoreCase(arguments[1])) return "/dgr task info <task_id>";
        if ("start".equalsIgnoreCase(arguments[1]))
            return "/dgr task start <task_id> <story_instance_id> <placement_id>";
        if ("journal".equalsIgnoreCase(arguments[1])) return "/dgr task journal";
        if ("progress".equalsIgnoreCase(arguments[1])) return "/dgr task progress";
        return "/dgr task <list|info|start|journal|progress>";
    }

    private void listTasks(ICommandSender sender) {
        List<String> ids = new ArrayList<String>(
            repository.getSnapshot()
                .getCanonicalTasks()
                .keySet());
        Collections.sort(ids);
        if (ids.isEmpty()) {
            ChatMessages.info(sender, "No canonical Tasks are loaded.");
            return;
        }
        ChatMessages.info(sender, "Tasks (" + ids.size() + "):");
        for (String id : ids) {
            CanonicalGraphResource task = repository.getSnapshot()
                .getCanonicalTask(id);
            ChatMessages.info(sender, "- " + id + " — " + task.getDisplayName());
        }
    }

    private void showTask(ICommandSender sender, String id) {
        CanonicalGraphResource task = repository.getSnapshot()
            .getCanonicalTask(id);
        if (task == null) {
            ChatMessages.error(sender, "Unknown Task ID: " + id);
            return;
        }
        ChatMessages.info(sender, "Task: " + task.getId() + " — " + task.getDisplayName());
        for (CanonicalGraphNode node : task.getGraph()
            .getNodes()) {
            if (node != null && "objective".equals(node.getType()))
                ChatMessages.info(sender, "- Objective: " + node.getId() + " — " + node.getDisplayName());
        }
    }

    private void processStory(ICommandSender sender, String[] arguments) {
        if (arguments.length < 2) {
            throw new WrongUsageException("/dgr story <list|info|start|state|reset>");
        }
        String action = arguments[1].toLowerCase();
        if ("list".equals(action)) {
            listStories(sender);
        } else if ("info".equals(action)) {
            requireLength(arguments, 3, "/dgr story info <id>");
            showStory(sender, arguments[2]);
        } else if ("state".equals(action)) {
            requireLength(arguments, 3, "/dgr story state <id>");
            showStoryState(requireMultiplayerPlayer(sender), arguments[2]);
        } else if ("reset".equals(action)) {
            requireLength(arguments, 3, "/dgr story reset <id>");
            EntityPlayerMP player = requireMultiplayerPlayer(sender);
            if (storyRuntime.resetInstance(player, arguments[2])) {
                ChatMessages.success(player, "Story instance reset: " + arguments[2]);
            } else {
                ChatMessages.error(player, "Unknown Story ID: " + arguments[2]);
            }
        } else if ("start".equals(action)) {
            requireLength(arguments, 3, "/dgr story start <id>");
            EntityPlayerMP player = requireMultiplayerPlayer(sender);
            boolean started = canonicalStoryManager == null ? storyRuntime.start(player, arguments[2])
                : canonicalStoryManager.startByEntry(player, arguments[2]);
            if (started) {
                ChatMessages.success(player, "Story started: " + arguments[2]);
            } else {
                ChatMessages.error(player, "Could not start Story: " + arguments[2]);
            }
        } else {
            throw new WrongUsageException("/dgr story <list|info|start|state|reset>");
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
            throw new WrongUsageException("/dgr quest <list|info|start|journal|progress|reset>");
        }
        String action = arguments[1].toLowerCase();
        if ("list".equals(action)) {
            listQuests(sender);
        } else if ("info".equals(action)) {
            requireLength(arguments, 3, "/dgr quest info <id>");
            showQuest(sender, arguments[2]);
        } else if ("start".equals(action)) {
            requireLength(arguments, 3, "/dgr quest start <id>");
            questRuntime.start(requireMultiplayerPlayer(sender), arguments[2]);
        } else if ("journal".equals(action)) {
            requireLength(arguments, 2, "/dgr quest journal");
            openQuestJournal(requireMultiplayerPlayer(sender));
        } else if ("progress".equals(action)) {
            requireLength(arguments, 2, "/dgr quest progress");
            showQuestProgress(requireMultiplayerPlayer(sender));
        } else if ("reset".equals(action)) {
            requireLength(arguments, 3, "/dgr quest reset <id>");
            questRuntime.reset(requireMultiplayerPlayer(sender), arguments[2]);
        } else {
            throw new WrongUsageException("/dgr quest <list|info|start|journal|progress|reset>");
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
        if (storyPackageLoader != null) {
            StoryPackageLoader.ReloadResult packages = storyPackageLoader.reload();
            if (!storyPackageLoader.getPackages()
                .isEmpty()) try {
                    ProjectSnapshot merged = StoryPackageSnapshotMerger.merge(storyPackageLoader.getPackages());
                    ProjectRepository.ReloadResult installed = repository.installSnapshot(merged);
                    if (packages.isSuccessful())
                        ChatMessages.success(sender, packages.getSummary() + "; " + installed.getSummary());
                    else {
                        ChatMessages.error(
                            sender,
                            "Story Package reload completed with retained definitions: " + packages.getSummary());
                        for (String error : packages.getErrors()) ChatMessages.error(sender, "- " + error);
                    }
                    return;
                } catch (ProjectLoadException exception) {
                    ChatMessages.error(
                        sender,
                        "Story Package merge failed; active definitions were retained: " + exception.getMessage());
                    return;
                }
            if (!packages.isSuccessful()) {
                ChatMessages.error(sender, "Story Package reload failed: " + packages.getSummary());
                return;
            }
        }
        ProjectRepository.ReloadResult result = repository.reload();
        if (result.isSuccessful()) {
            ChatMessages.success(sender, result.getSummary());
        } else {
            ChatMessages.error(sender, "Reload failed: " + result.getSummary());
        }
    }

    private void processDialogue(ICommandSender sender, String[] arguments) {
        if (arguments.length < 2) {
            throw new WrongUsageException("/dgr dialogue <list|info|play|last-result>");
        }
        String action = arguments[1].toLowerCase();
        if ("list".equals(action)) {
            listDialogues(sender);
        } else if ("info".equals(action)) {
            requireLength(arguments, 3, "/dgr dialogue info <id>");
            showDialogue(sender, arguments[2]);
        } else if ("play".equals(action)) {
            requireLength(arguments, 3, "/dgr dialogue play <id>");
            dialogueSessions.start(requireMultiplayerPlayer(sender), arguments[2]);
        } else if ("last-result".equals(action)) {
            requireLength(arguments, 2, "/dgr dialogue last-result");
            showLastResult(requirePlayer(sender));
        } else {
            throw new WrongUsageException("/dgr dialogue <list|info|play|last-result>");
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
            throw new WrongUsageException("/dgr actor <list|info|select|bind|unbind>");
        }

        String action = arguments[1].toLowerCase();
        if ("list".equals(action)) {
            listActors(sender);
        } else if ("info".equals(action)) {
            requireLength(arguments, 3, "/dgr actor info <id>");
            showActor(sender, arguments[2]);
        } else if ("select".equals(action)) {
            requireLength(arguments, 3, "/dgr actor select <id>");
            selectActor(requirePlayer(sender), arguments[2]);
        } else if ("bind".equals(action)) {
            requireLength(arguments, 3, "/dgr actor bind <id>");
            bindActor(requirePlayer(sender), arguments[2]);
        } else if ("unbind".equals(action)) {
            requireLength(arguments, 2, "/dgr actor unbind");
            unbindActor(requirePlayer(sender));
        } else {
            throw new WrongUsageException("/dgr actor <list|info|select|bind|unbind>");
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

    private static EntityPlayerMP namedMultiplayerPlayer(String name) {
        if (name == null || name.trim()
            .isEmpty()) throw new CommandException("Online player name is required.");
        MinecraftServer server = MinecraftServer.getServer();
        if (server != null && server.getConfigurationManager() != null)
            for (Object value : server.getConfigurationManager().playerEntityList) if (value instanceof EntityPlayerMP
                && name.equalsIgnoreCase(((EntityPlayerMP) value).getCommandSenderName()))
                return (EntityPlayerMP) value;
        throw new CommandException("Online player was not found: " + name);
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
                "story",
                "session",
                "task");
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
        if (arguments.length == 2 && "session".equalsIgnoreCase(arguments[0])) {
            return getListOfStringsMatchingLastWord(arguments, "play", "resume");
        }
        if (arguments.length == 3 && "session".equalsIgnoreCase(arguments[0])
            && Arrays.asList("play", "resume")
                .contains(arguments[1].toLowerCase())) {
            return getListOfStringsFromIterableMatchingLastWord(
                arguments,
                canonicalSessionStoryIds(repository.getSnapshot()));
        }
        if (arguments.length == 4 && "session".equalsIgnoreCase(arguments[0])
            && "play".equalsIgnoreCase(arguments[1])) {
            List<String> placementIds = canonicalSessionPlacementIds(repository.getSnapshot(), arguments[2]);
            return getListOfStringsFromIterableMatchingLastWord(arguments, placementIds);
        }
        if (arguments.length == 2 && "task".equalsIgnoreCase(arguments[0])) {
            return getListOfStringsMatchingLastWord(arguments, "list", "info", "start", "journal", "progress");
        }
        if (arguments.length == 3 && "task".equalsIgnoreCase(arguments[0])
            && Arrays.asList("info", "start")
                .contains(arguments[1].toLowerCase())) {
            List<String> taskIds = new ArrayList<String>(
                repository.getSnapshot()
                    .getCanonicalTasks()
                    .keySet());
            Collections.sort(taskIds);
            return getListOfStringsFromIterableMatchingLastWord(arguments, taskIds);
        }
        return null;
    }

    /** Package-private pure completion seam for canonical Story IDs. */
    static List<String> canonicalSessionStoryIds(ProjectSnapshot snapshot) {
        if (snapshot == null) return new ArrayList<String>();
        List<String> storyIds = new ArrayList<String>(
            snapshot.getCanonicalStories()
                .keySet());
        Collections.sort(storyIds);
        return storyIds;
    }

    /** Package-private pure completion seam for Session aggregate placements. */
    static List<String> canonicalSessionPlacementIds(ProjectSnapshot snapshot, String storyId) {
        if (snapshot == null) return new ArrayList<String>();
        CanonicalGraphResource story = snapshot.getCanonicalStory(storyId);
        List<String> placementIds = new ArrayList<String>();
        if (story == null || story.getGraph() == null) return placementIds;
        for (CanonicalGraphNode node : story.getGraph()
            .getNodes()) {
            if (node != null && "session".equals(node.getType()) && node.getId() != null)
                placementIds.add(node.getId());
        }
        Collections.sort(placementIds);
        return placementIds;
    }

}
