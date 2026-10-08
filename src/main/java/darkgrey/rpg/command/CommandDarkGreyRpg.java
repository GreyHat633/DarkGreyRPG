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

import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;

import cpw.mods.fml.common.Loader;
import darkgrey.rpg.compat.customnpcs.CustomNpcActorBinding;
import darkgrey.rpg.content.ModItems;
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
import darkgrey.rpg.project.ProjectRepository;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.project.packages.StoryPackageGenerationDelta;
import darkgrey.rpg.project.packages.StoryPackageGenerationLifecycle;
import darkgrey.rpg.project.packages.StoryPackageLoader;
import darkgrey.rpg.project.packages.StoryPackageRuntimeReloader;
import darkgrey.rpg.runtime.ActorBindingActions;
import darkgrey.rpg.runtime.ChatMessages;
import darkgrey.rpg.runtime.EditorSessionManager;
import darkgrey.rpg.runtime.EntityTargeting;
import darkgrey.rpg.session.forge.CanonicalSessionForgeManager;
import darkgrey.rpg.story.canonical.forge.CanonicalStoryForgeManager;
import darkgrey.rpg.story.canonical.instance.CanonicalStoryInstanceSnapshot;
import darkgrey.rpg.story.canonical.runtime.CanonicalStorySnapshot;
import darkgrey.rpg.task.event.CanonicalTaskDispatchResult;
import darkgrey.rpg.task.forge.CanonicalTaskForgeManager;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot;
import darkgrey.rpg.task.journal.CanonicalJournalService;
import darkgrey.rpg.task.journal.CanonicalTaskJournalEntry;
import darkgrey.rpg.task.runtime.CanonicalTaskEvent;

public final class CommandDarkGreyRpg extends CommandBase {

    private static final Logger LOG = LogManager.getLogger(CommandDarkGreyRpg.class);
    private static final double TARGET_DISTANCE = 8.0D;

    private final ProjectRepository repository;
    private final EditorSessionManager sessions;
    private final CanonicalSessionForgeManager canonicalSessionManager;
    private final CanonicalTaskForgeManager canonicalTaskManager;
    private final CanonicalStoryForgeManager canonicalStoryManager;
    private final CanonicalJournalService canonicalJournalService;
    private final StoryPackageLoader storyPackageLoader;
    private List<String> lastPackageReloadErrors;

    public CommandDarkGreyRpg(ProjectRepository repository, EditorSessionManager sessions,
        CanonicalSessionForgeManager canonicalSessionManager, CanonicalTaskForgeManager canonicalTaskManager,
        CanonicalStoryForgeManager canonicalStoryManager, StoryPackageLoader storyPackageLoader) {
        if (canonicalSessionManager == null)
            throw new IllegalArgumentException("Canonical Session manager is required.");
        this.repository = repository;
        this.sessions = sessions;
        this.canonicalSessionManager = canonicalSessionManager;
        this.canonicalTaskManager = canonicalTaskManager;
        this.canonicalStoryManager = canonicalStoryManager;
        this.canonicalJournalService = canonicalTaskManager == null ? null
            : new CanonicalJournalService(canonicalTaskManager);
        this.storyPackageLoader = storyPackageLoader;
    }

    @Override
    public String getCommandName() {
        return "dgr";
    }

    /** 0.3.2.0_B public command surface; the historical dgrpg root is intentionally retired. */
    @Override
    public List<String> getCommandAliases() {
        return Collections.emptyList();
    }

    @Override
    public String getCommandUsage(ICommandSender sender) {
        return "/dgr <status|reload|actor|story|session|task|dimension|inspect|debug>";
    }

    @Override
    public int getRequiredPermissionLevel() {
        return 2;
    }

    @Override
    public void processCommand(ICommandSender sender, String[] arguments) {
        try {
            executeCommand(sender, arguments);
        } catch (darkgrey.rpg.nominator.NominatorDataUnavailableException failure) {
            ChatMessages.error(sender, darkgrey.rpg.nominator.NominatorDataUnavailableException.PLAYER_MESSAGE);
        }
    }

    private void executeCommand(ICommandSender sender, String[] arguments) {
        if (arguments.length == 0) {
            throw new WrongUsageException(getCommandUsage(sender));
        }

        if ("buff".equalsIgnoreCase(arguments[0])) {
            if (arguments.length != 2
                || !("list".equalsIgnoreCase(arguments[1]) || "export".equalsIgnoreCase(arguments[1])))
                throw new WrongUsageException("/dgr buff <list|export>");
            java.util.List<String> lines = new java.util.ArrayList<String>();
            for (darkgrey.rpg.story.canonical.forge.CanonicalBuffCatalog.Entry entry : darkgrey.rpg.story.canonical.forge.CanonicalBuffCatalog
                .get()
                .entries()) lines.add(entry.displayLine());
            if ("list".equalsIgnoreCase(arguments[1])) for (String line : lines) ChatMessages.info(sender, line);
            else try {
                java.nio.file.Path directory = darkgrey.rpg.config.RpgRuntimeDirectories.prepare(
                    Loader.instance()
                        .getConfigDir()
                        .getParentFile())
                    .exportsDirectory()
                    .toPath();
                java.nio.file.Files.createDirectories(directory);
                java.nio.file.Path destination = directory.resolve("buffs.txt");
                java.nio.file.Files.write(destination, lines, java.nio.charset.StandardCharsets.UTF_8);
                ChatMessages.info(sender, "BUFF导出：" + destination.toAbsolutePath());
            } catch (java.io.IOException failure) {
                throw new CommandException("BUFF export failed: " + failure.getMessage());
            }
            return;
        }
        if ("dimension".equalsIgnoreCase(arguments[0])) {
            if (arguments.length != 2 || !"id".equalsIgnoreCase(arguments[1]))
                throw new WrongUsageException("/dgr dimension id");
            EntityPlayerMP player = getCommandSenderAsPlayer(sender);
            ChatMessages.info(sender, "当前维度 ID：" + player.dimension);
            return;
        }
        if ("inspect".equalsIgnoreCase(arguments[0])) {
            if (arguments.length != 1) throw new WrongUsageException("/dgr inspect");
            EntityPlayerMP player = getCommandSenderAsPlayer(sender);
            boolean enabled = darkgrey.rpg.creator.CreatorInspectSavedData.get()
                .toggle(player.getUniqueID());
            player.addChatMessage(
                new net.minecraft.util.ChatComponentText("DGR identity display: " + (enabled ? "ON" : "OFF")));
            return;
        }
        if ("status".equalsIgnoreCase(arguments[0])) {
            showStatus(sender);
            return;
        }
        if ("reload".equalsIgnoreCase(arguments[0])) {
            if (arguments.length >= 2 && "errors".equalsIgnoreCase(arguments[1])) {
                if (arguments.length > 3) throw new WrongUsageException("/dgr reload errors [页码]");
                showPackageReloadErrors(sender, arguments.length == 3 ? parseInt(sender, arguments[2]) : 1);
                return;
            }
            requireLength(arguments, 1, "/dgr reload 或 /dgr reload errors [页码]");
            reload(sender);
            return;
        }
        if ("actor".equalsIgnoreCase(arguments[0])) {
            processActor(sender, arguments);
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
        if (arguments.length == 1 && sender instanceof EntityPlayerMP) {
            if (!sender.canCommandSenderUseCommand(2, "dgr")) throw new WrongUsageException("需要管理员权限。");
            darkgrey.rpg.network.DialogueNetwork.CHANNEL.sendTo(
                new darkgrey.rpg.diagnostics.PlayerStatePacket(0, 0, new net.minecraft.nbt.NBTTagCompound()),
                (EntityPlayerMP) sender);
            return;
        }
        String usage = "/dgr debug <player dimension <online_player> <-1|0|1>|item <bind_exact|bind_exact_group|bind_fuzzy> <id>|story <start|set_logic|state> [online_player] ...|task <start|emit_kill|state> [online_player] ...|cnpc <bind_nearest <npc_id>|bind_group_nearest <group_id>|list_nearby>|storage <interact_nearest|release_here|status>>";
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
        if (arguments.length == 4 && "item".equalsIgnoreCase(arguments[1])) {
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
                ChatMessages.success(player, "已启动会话：" + arguments[2] + "（" + arguments[3] + "）");
            } else {
                ChatMessages.error(player, "无法启动会话：" + arguments[2]);
            }
        } else if ("resume".equals(action)) {
            requireLength(arguments, 3, sessionUsage(arguments));
            EntityPlayerMP player = requireMultiplayerPlayer(sender);
            if (canonicalSessionManager.resume(player, arguments[2])) {
                ChatMessages.success(player, "已恢复会话：" + arguments[2]);
            } else {
                ChatMessages.error(player, "无法恢复会话：" + arguments[2]);
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
                ChatMessages.error(player, "任务运行服务当前不可用。");
                return;
            }
            CanonicalTaskInstanceSnapshot snapshot = canonicalTaskManager
                .start(player, arguments[3], arguments[4], arguments[2]);
            ChatMessages.success(
                player,
                "已直接启动任务：" + arguments[2]
                    + "（故事="
                    + arguments[3]
                    + "，节点="
                    + arguments[4]
                    + "，状态="
                    + snapshot.getStatus()
                    + "）");
        } else if ("journal".equals(action)) {
            requireLength(arguments, 2, "/dgr task journal");
            openTaskJournal(requireMultiplayerPlayer(sender));
        } else if ("progress".equals(action)) {
            requireLength(arguments, 2, "/dgr task progress");
            showTaskProgress(requireMultiplayerPlayer(sender));
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
            ChatMessages.info(sender, "当前没有已加载的任务。");
            return;
        }
        ChatMessages.info(sender, "任务（" + ids.size() + "）：");
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
            ChatMessages.error(sender, "未知任务 ID：" + id);
            return;
        }
        ChatMessages.info(sender, "任务：" + task.getId() + " — " + task.getDisplayName());
        for (CanonicalGraphNode node : task.getGraph()
            .getNodes()) {
            if (node != null && "objective".equals(node.getType()))
                ChatMessages.info(sender, "- 目标：" + node.getId() + " — " + node.getDisplayName());
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
            if (canonicalStoryManager != null && canonicalStoryManager.reset(player, arguments[2])) {
                ChatMessages.success(player, "已重置故事实例：" + arguments[2]);
            } else {
                ChatMessages.error(player, "未知故事 ID：" + arguments[2]);
            }
        } else if ("start".equals(action)) {
            requireLength(arguments, 3, "/dgr story start <id>");
            EntityPlayerMP player = requireMultiplayerPlayer(sender);
            boolean started = canonicalStoryManager != null && canonicalStoryManager.startByEntry(player, arguments[2]);
            if (started) {
                ChatMessages.success(player, "已启动故事：" + arguments[2]);
            } else {
                ChatMessages.error(player, "无法启动故事：" + arguments[2]);
            }
        } else {
            throw new WrongUsageException("/dgr story <list|info|start|state|reset>");
        }
    }

    private void listStories(ICommandSender sender) {
        ProjectSnapshot snapshot = repository.getSnapshot();
        List<String> ids = new ArrayList<String>(
            snapshot.getCanonicalStories()
                .keySet());
        Collections.sort(ids);
        if (ids.isEmpty()) {
            ChatMessages.info(sender, "当前没有已加载的故事。");
            return;
        }
        ChatMessages.info(sender, "故事（" + ids.size() + "）：");
        for (String id : ids) {
            CanonicalGraphResource canonical = snapshot.getCanonicalStory(id);
            ChatMessages.info(sender, "- " + id + " — " + canonical.getDisplayName());
        }
    }

    private void showStory(ICommandSender sender, String id) {
        CanonicalGraphResource canonical = repository.getSnapshot()
            .getCanonicalStory(id);
        if (canonical == null) {
            ChatMessages.error(sender, "未知故事 ID：" + id);
            return;
        }
        ChatMessages.info(sender, "故事：" + canonical.getId() + " — " + canonical.getDisplayName());
        ChatMessages.info(
            sender,
            "节点：" + canonical.getGraph()
                .getNodes()
                .size()
                + "，连接："
                + canonical.getGraph()
                    .getConnections()
                    .size());
    }

    private void showStoryState(EntityPlayerMP player, String id) {
        if (canonicalStoryManager == null || repository.getSnapshot()
            .getCanonicalStory(id) == null) {
            ChatMessages.error(player, "未知故事 ID：" + id);
            return;
        }
        CanonicalStoryInstanceSnapshot instance = canonicalStoryManager.snapshot(player, id);
        if (instance == null) {
            ChatMessages.info(player, "故事 " + id + "：未开始");
            return;
        }
        CanonicalStorySnapshot runtime = instance.getRuntimeSnapshot();
        ChatMessages.info(
            player,
            "故事 " + id
                + "："
                + runtime.getStatus()
                + "，等待="
                + runtime.getWaitKind()
                + "，当前节点="
                + runtime.getCurrentNodeId()
                + "，逻辑="
                + runtime.getExternalLogicInputs());
    }

    private void openTaskJournal(EntityPlayerMP player) {
        if (canonicalJournalService == null) {
            ChatMessages.error(player, "规范任务日志服务当前不可用。");
            return;
        }
        canonicalJournalService.openJournal(player);
    }

    private void showTaskProgress(EntityPlayerMP player) {
        if (canonicalJournalService == null) {
            ChatMessages.error(player, "规范任务日志服务当前不可用。");
            return;
        }
        List<CanonicalTaskJournalEntry> entries = canonicalJournalService.getJournal(player);
        if (entries.isEmpty()) {
            ChatMessages.info(player, "任务日志为空。");
            return;
        }
        for (CanonicalTaskJournalEntry entry : entries) {
            ChatMessages.info(player, entry.getTitle() + " [" + entry.getStatus() + "]");
            for (String objective : CanonicalJournalService.objectiveLines(
                entry,
                text -> darkgrey.rpg.session.forge.DynamicContentResolver.resolve(text, player))) {
                ChatMessages.info(player, "  " + objective);
            }
        }
    }

    private void showStatus(ICommandSender sender) {
        ProjectRepository.ReloadResult result = repository.getLastReload();
        ChatMessages.info(
            sender,
            "项目路径：" + repository.getProjectDirectory()
                .getAbsolutePath());
        ChatMessages.info(sender, "CustomNPC+：" + (Loader.isModLoaded("customnpcs") ? "已加载" : "未安装"));
        if (!result.isSuccessful()) {
            ChatMessages.error(sender, result.getSummary());
            return;
        }
        int packageCount = storyPackageLoader == null ? 0
            : storyPackageLoader.getPackages()
                .size();
        ChatMessages.success(sender, "已加载 " + packageCount + " 个故事包。");
        ChatMessages.info(sender, "故事：" + result.getStoryCount());
        ChatMessages.info(sender, "角色：" + result.getActorCount());
        ChatMessages.info(sender, "物品：" + result.getItemCount());
        ChatMessages.info(sender, "物品组：" + result.getItemGroupCount());
        ChatMessages.info(sender, "会话：" + result.getSessionCount());
        ChatMessages.info(sender, "任务：" + result.getTaskCount());
    }

    private void reload(ICommandSender sender) {
        if (storyPackageLoader != null) {
            StoryPackageRuntimeReloader.Result reload = StoryPackageRuntimeReloader
                .reload(repository, storyPackageLoader);
            lastPackageReloadErrors = reload.getErrors();
            StoryPackageGenerationLifecycle.Result generations = null;
            if (reload.isPackageSetCommitted()) generations = StoryPackageGenerationLifecycle.reconcile(
                MinecraftServer.getServer()
                    .worldServerForDimension(0).mapStorage,
                storyPackageLoader.getPackages());
            if (generations != null) showGenerationDelta(sender, generations);
            if (reload.isSuccessful()) {
                ChatMessages.success(
                    sender,
                    "已加载 " + reload.getPackageReload()
                        .getPackageCount()
                        + " 个故事包；"
                        + reload.getProjectReload()
                            .getSummary());
            } else {
                ChatMessages.error(sender, "重新加载未完全成功；无效候选未生效。原因如下：");
                for (String error : reload.getErrors()) LOG.warn("Story Package reload rejected: {}", error);
                showPackageReloadErrors(sender, 1);
            }
            return;
        }
        ProjectRepository.ReloadResult result = repository.reload();
        if (result.isSuccessful()) {
            ChatMessages.success(sender, result.getSummary());
        } else {
            ChatMessages.error(sender, "重新加载失败：" + result.getSummary());
        }
    }

    private void showPackageReloadErrors(ICommandSender sender, int number) {
        if (storyPackageLoader == null) {
            ChatMessages.info(sender, "当前未启用故事包加载器。");
            return;
        }
        List<String> errors = lastPackageReloadErrors == null ? storyPackageLoader.getLastReload()
            .getErrors() : lastPackageReloadErrors;
        if (errors.isEmpty()) {
            if (!storyPackageLoader.getLastReload()
                .isSuccessful())
                errors = Collections.singletonList(
                    storyPackageLoader.getLastReload()
                        .getSummary());
            else {
                ChatMessages.info(sender, "最近一次故事包加载没有错误。");
                return;
            }
        }
        darkgrey.rpg.project.packages.StoryPackageErrorPage page;
        try {
            page = new darkgrey.rpg.project.packages.StoryPackageErrorPage(errors, number);
        } catch (IllegalArgumentException invalidPage) {
            ChatMessages.error(sender, invalidPage.getMessage());
            return;
        }
        ChatMessages.info(sender, "故事包错误详情 " + page.getPage() + "/" + page.getPageCount());
        for (String line : page.getLines()) ChatMessages.error(sender, line);
        if (page.getPageCount() > 1) {
            net.minecraft.util.ChatComponentText navigation = new net.minecraft.util.ChatComponentText(
                "[DarkGrey RPG] ");
            if (number > 1) navigation.appendSibling(errorPageLink("[上一页] ", number - 1));
            if (number < page.getPageCount()) navigation.appendSibling(errorPageLink("[下一页] ", number + 1));
            navigation.appendText("/dgr reload errors <页码>；完整报告已写入服务器日志。");
            sender.addChatMessage(navigation);
        }
    }

    private static net.minecraft.util.ChatComponentText errorPageLink(String label, int page) {
        net.minecraft.util.ChatComponentText link = new net.minecraft.util.ChatComponentText(label);
        link.getChatStyle()
            .setColor(net.minecraft.util.EnumChatFormatting.AQUA)
            .setUnderlined(true)
            .setChatClickEvent(
                new net.minecraft.event.ClickEvent(
                    net.minecraft.event.ClickEvent.Action.RUN_COMMAND,
                    "/dgr reload errors " + page));
        return link;
    }

    private static void showGenerationDelta(ICommandSender sender, StoryPackageGenerationLifecycle.Result result) {
        for (StoryPackageGenerationDelta.Entry delta : result.getDeltas()) {
            if (delta.getKind() == StoryPackageGenerationDelta.Kind.UNCHANGED) continue;
            String oldFingerprint = delta.getPrevious() == null ? "-"
                : delta.getPrevious()
                    .shortFingerprint();
            String newFingerprint = delta.getCurrent() == null ? "-"
                : delta.getCurrent()
                    .shortFingerprint();
            ChatMessages.info(
                sender,
                delta.getPackageId() + " " + delta.getKind() + " " + oldFingerprint + " -> " + newFingerprint);
        }
        if (!result.getAffectedStoryIds()
            .isEmpty())
            ChatMessages.info(
                sender,
                "已退役旧代运行状态：Story " + result.getStoryInstancesRetired()
                    + "，Session "
                    + result.getSessionInstancesRetired()
                    + "，Continuation "
                    + result.getContinuationsRetired()
                    + "，Task "
                    + result.getTaskInstancesRetired()
                    + "。");
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
            ChatMessages.info(sender, "当前没有已加载的角色。");
            return;
        }
        ChatMessages.info(
            sender,
            "角色（" + repository.getSnapshot()
                .getActors()
                .size() + "）：");
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
            ChatMessages.error(sender, "未知角色 ID：" + id);
            return;
        }
        ChatMessages.info(sender, "角色：" + actor.getId());
        ChatMessages.info(sender, "显示名称：" + actor.getDisplayName());
        ChatMessages.info(sender, "标签：" + actor.getTags());
        if (!actor.getNotes()
            .isEmpty()) {
            ChatMessages.info(sender, "备注：" + actor.getNotes());
        }
    }

    private void selectActor(EntityPlayer player, String id) {
        ActorDefinition actor = repository.getSnapshot()
            .getActor(id);
        if (actor == null) {
            ChatMessages.error(player, "未知角色 ID：" + id);
            return;
        }
        sessions.selectActor(player, id);
        ChatMessages.success(player, "已选择角色 " + id + "。请用编辑工具右键 CustomNPC+ 实体完成绑定。");
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
            throw new CommandException("此命令必须由玩家执行。");
        }
        return (EntityPlayer) sender;
    }

    private static EntityPlayerMP requireMultiplayerPlayer(ICommandSender sender) {
        if (!(sender instanceof EntityPlayerMP)) {
            throw new CommandException("此命令必须由服务器中的玩家执行。");
        }
        return (EntityPlayerMP) sender;
    }

    private static EntityPlayerMP namedMultiplayerPlayer(String name) {
        if (name == null || name.trim()
            .isEmpty()) throw new CommandException("必须提供在线玩家名称。");
        MinecraftServer server = MinecraftServer.getServer();
        if (server != null && server.getConfigurationManager() != null)
            for (Object value : server.getConfigurationManager().playerEntityList) if (value instanceof EntityPlayerMP
                && name.equalsIgnoreCase(((EntityPlayerMP) value).getCommandSenderName()))
                return (EntityPlayerMP) value;
        throw new CommandException("未找到在线玩家：" + name);
    }

    private static Entity requireTarget(EntityPlayer player) {
        Entity target = EntityTargeting.findLookedAtEntity(player, TARGET_DISTANCE);
        if (target == null) {
            throw new CommandException("请正对 8 格内的 CustomNPC+ NPC。");
        }
        if (!CustomNpcActorBinding.isCustomNpc(target)) {
            throw new CommandException("目标实体不是 CustomNPC+ NPC。");
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
            List<String> roots = new ArrayList<String>(
                Arrays.asList("inspect", "status", "reload", "actor", "story", "session", "task", "dimension", "buff"));
            if (sender.canCommandSenderUseCommand(2, "dgr")) roots.add("debug");
            return getListOfStringsFromIterableMatchingLastWord(arguments, roots);
        }
        if (arguments.length == 2 && "buff".equalsIgnoreCase(arguments[0]))
            return getListOfStringsMatchingLastWord(arguments, "list", "export");
        if (arguments.length == 2 && "reload".equalsIgnoreCase(arguments[0]))
            return getListOfStringsMatchingLastWord(arguments, "errors");
        if (arguments.length == 2 && "dimension".equalsIgnoreCase(arguments[0]))
            return getListOfStringsMatchingLastWord(arguments, "id");
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
        if (arguments.length == 2 && "story".equalsIgnoreCase(arguments[0])) {
            return getListOfStringsMatchingLastWord(arguments, "list", "info", "start", "state", "reset");
        }
        if (arguments.length == 3 && "story".equalsIgnoreCase(arguments[0])) {
            List<String> storyIds = new ArrayList<String>(
                repository.getSnapshot()
                    .getCanonicalStories()
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
