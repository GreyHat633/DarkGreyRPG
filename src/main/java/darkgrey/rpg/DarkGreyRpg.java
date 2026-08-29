package darkgrey.rpg;

import java.io.File;

import net.minecraftforge.common.MinecraftForge;

import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;

import cpw.mods.fml.common.FMLCommonHandler;
import cpw.mods.fml.common.Mod;
import cpw.mods.fml.common.Mod.EventHandler;
import cpw.mods.fml.common.SidedProxy;
import cpw.mods.fml.common.event.FMLInitializationEvent;
import cpw.mods.fml.common.event.FMLPreInitializationEvent;
import cpw.mods.fml.common.event.FMLServerStartingEvent;
import cpw.mods.fml.common.event.FMLServerStoppingEvent;
import darkgrey.rpg.command.CommandDarkGreyRpg;
import darkgrey.rpg.config.RpgConfiguration;
import darkgrey.rpg.content.ModItems;
import darkgrey.rpg.dialogue.runtime.DialogueSessionManager;
import darkgrey.rpg.live.LiveBridgeController;
import darkgrey.rpg.live.LiveBridgeServer;
import darkgrey.rpg.live.LivePickService;
import darkgrey.rpg.live.PlayTestManager;
import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.MainThreadScheduler;
import darkgrey.rpg.project.ProjectRepository;
import darkgrey.rpg.proxy.CommonProxy;
import darkgrey.rpg.quest.runtime.QuestEventAdapter;
import darkgrey.rpg.quest.runtime.QuestRuntimeService;
import darkgrey.rpg.runtime.EditorSessionManager;
import darkgrey.rpg.runtime.EditorToolEventHandler;
import darkgrey.rpg.session.forge.CanonicalSessionForgeManager;
import darkgrey.rpg.story.canonical.forge.CanonicalStoryForgeManager;
import darkgrey.rpg.story.runtime.StoryEventAdapter;
import darkgrey.rpg.story.runtime.StoryEventBridge;
import darkgrey.rpg.story.runtime.StoryEventBus;
import darkgrey.rpg.story.runtime.StoryRuntimeService;
import darkgrey.rpg.task.forge.CanonicalTaskEventAdapter;
import darkgrey.rpg.task.forge.CanonicalTaskForgeManager;

@Mod(
    modid = DarkGreyRpg.MOD_ID,
    name = DarkGreyRpg.MOD_NAME,
    version = Tags.VERSION,
    dependencies = "required-after:customnpcs")
public final class DarkGreyRpg {

    public static final String MOD_ID = "darkgrey_rpg";
    public static final String MOD_NAME = "DarkGrey RPG";
    public static final Logger LOG = LogManager.getLogger(MOD_NAME);

    @SidedProxy(clientSide = "darkgrey.rpg.client.ClientProxy", serverSide = "darkgrey.rpg.proxy.CommonProxy")
    public static CommonProxy proxy;

    private static ProjectRepository projectRepository;
    private static EditorSessionManager editorSessions;
    private static DialogueSessionManager dialogueSessions;
    private static QuestRuntimeService questRuntime;
    private static StoryRuntimeService storyRuntime;
    private static RpgConfiguration configuration;
    private static LiveBridgeServer liveBridge;
    private static LivePickService livePicks;
    private static PlayTestManager playTests;
    private static CanonicalSessionForgeManager canonicalSessionManager;
    private static CanonicalTaskForgeManager canonicalTaskManager;
    private static CanonicalStoryForgeManager canonicalStoryManager;

    @EventHandler
    public void preInit(FMLPreInitializationEvent event) {
        configuration = RpgConfiguration.load(event.getSuggestedConfigurationFile());
        File projectDirectory = configuration.resolveProjectDirectory(event.getModConfigurationDirectory());

        projectRepository = new ProjectRepository(projectDirectory);
        canonicalSessionManager = new CanonicalSessionForgeManager(projectRepository);
        canonicalTaskManager = new CanonicalTaskForgeManager(projectRepository);
        canonicalStoryManager = new CanonicalStoryForgeManager(
            projectRepository,
            canonicalSessionManager,
            canonicalTaskManager);
        canonicalStoryManager.bindAggregateListeners();
        editorSessions = new EditorSessionManager();
        dialogueSessions = new DialogueSessionManager(projectRepository);
        questRuntime = new QuestRuntimeService(projectRepository, canonicalTaskManager);
        storyRuntime = new StoryRuntimeService(projectRepository, dialogueSessions, questRuntime);
        livePicks = new LivePickService();
        StoryEventBus storyEvents = new StoryEventBus(storyRuntime);
        StoryEventBridge storyBridge = new StoryEventBridge(storyEvents);
        dialogueSessions.addResultListener(storyBridge);
        questRuntime.addCompletionListener(storyBridge);

        ProjectRepository.ReloadResult result = projectRepository.reload();
        if (result.isSuccessful()) {
            LOG.info(
                "Loaded DarkGrey RPG project '{}' with {} actor(s), {} dialogue(s), {} quest(s), and {} story/stories from {}",
                result.getProjectDisplayName(),
                result.getActorCount(),
                result.getDialogueCount(),
                result.getQuestCount(),
                result.getStoryCount(),
                projectDirectory.getAbsolutePath());
        } else {
            LOG.error("DarkGrey RPG project reload failed: {}", result.getSummary());
        }

        ModItems.register();
        MinecraftForge.EVENT_BUS.register(new EditorToolEventHandler(projectRepository, editorSessions, livePicks));
        QuestEventAdapter questEvents = new QuestEventAdapter(questRuntime);
        MinecraftForge.EVENT_BUS.register(questEvents);
        StoryEventAdapter storyEventAdapter = new StoryEventAdapter(storyEvents, canonicalStoryManager);
        MinecraftForge.EVENT_BUS.register(storyEventAdapter);
        MinecraftForge.EVENT_BUS.register(new CanonicalTaskEventAdapter(canonicalTaskManager));
        FMLCommonHandler.instance()
            .bus()
            .register(new MainThreadScheduler());
        FMLCommonHandler.instance()
            .bus()
            .register(questEvents);
        FMLCommonHandler.instance()
            .bus()
            .register(storyEventAdapter);
        DialogueNetwork.registerCommon();
        proxy.registerClientDialogueNetwork();
    }

    @EventHandler
    public void init(FMLInitializationEvent event) {
        LOG.info("DarkGrey RPG Phase 5 runtime initialized");
    }

    @EventHandler
    public void serverStarting(FMLServerStartingEvent event) {
        event.registerServerCommand(
            new CommandDarkGreyRpg(
                projectRepository,
                editorSessions,
                dialogueSessions,
                questRuntime,
                storyRuntime,
                canonicalSessionManager,
                canonicalTaskManager,
                canonicalStoryManager));
        if (configuration.isLiveBridgeEnabled()) {
            playTests = new PlayTestManager(projectRepository, storyRuntime);
            liveBridge = new LiveBridgeServer(
                configuration.getLiveBridgePort(),
                new LiveBridgeController(projectRepository, dialogueSessions, storyRuntime, livePicks, playTests));
            try {
                liveBridge.start();
            } catch (java.io.IOException exception) {
                LOG.error("Could not start Live Bridge on 127.0.0.1:" + configuration.getLiveBridgePort(), exception);
                liveBridge = null;
            }
        }
    }

    @EventHandler
    public void serverStopping(FMLServerStoppingEvent event) {
        if (playTests != null) {
            playTests.restoreAllOnlinePlayers();
            playTests = null;
        }
        if (liveBridge != null) {
            liveBridge.stop();
            liveBridge = null;
        }
    }

    public static ProjectRepository getProjectRepository() {
        return projectRepository;
    }

    public static DialogueSessionManager getDialogueSessions() {
        return dialogueSessions;
    }

    public static QuestRuntimeService getQuestRuntime() {
        return questRuntime;
    }

    public static StoryRuntimeService getStoryRuntime() {
        return storyRuntime;
    }

    public static CanonicalSessionForgeManager getCanonicalSessionManager() {
        return canonicalSessionManager;
    }

    public static CanonicalTaskForgeManager getCanonicalTaskManager() {
        return canonicalTaskManager;
    }

    public static CanonicalTaskForgeManager getCanonicalTaskForgeManager() {
        return canonicalTaskManager;
    }

    public static CanonicalStoryForgeManager getCanonicalStoryManager() {
        return canonicalStoryManager;
    }
}
