package darkgrey.rpg;

import java.io.File;

import net.minecraftforge.common.MinecraftForge;

import org.apache.logging.log4j.LogManager;
import org.apache.logging.log4j.Logger;

import cpw.mods.fml.common.FMLCommonHandler;
import cpw.mods.fml.common.Mod;
import cpw.mods.fml.common.Mod.EventHandler;
import cpw.mods.fml.common.Mod.Instance;
import cpw.mods.fml.common.SidedProxy;
import cpw.mods.fml.common.event.FMLInitializationEvent;
import cpw.mods.fml.common.event.FMLPreInitializationEvent;
import cpw.mods.fml.common.event.FMLServerStartingEvent;
import cpw.mods.fml.common.event.FMLServerStoppingEvent;
import darkgrey.rpg.command.CommandDarkGreyRpg;
import darkgrey.rpg.config.RpgConfiguration;
import darkgrey.rpg.config.RpgRuntimeDirectories;
import darkgrey.rpg.content.ModItems;
import darkgrey.rpg.entitytools.forge.EntityToolsRuntime;
import darkgrey.rpg.live.LiveBridgeController;
import darkgrey.rpg.live.LiveBridgeServer;
import darkgrey.rpg.live.LivePickService;
import darkgrey.rpg.network.DialogueNetwork;
import darkgrey.rpg.network.EntityToolsNetwork;
import darkgrey.rpg.network.MainThreadScheduler;
import darkgrey.rpg.network.NominatorNetwork;
import darkgrey.rpg.nominator.container.NominatorGuiHandler;
import darkgrey.rpg.project.ProjectRepository;
import darkgrey.rpg.project.packages.StoryPackageGenerationLifecycle;
import darkgrey.rpg.project.packages.StoryPackageLoader;
import darkgrey.rpg.project.packages.StoryPackageRuntimeReloader;
import darkgrey.rpg.proxy.CommonProxy;
import darkgrey.rpg.runtime.EditorSessionManager;
import darkgrey.rpg.runtime.EditorToolEventHandler;
import darkgrey.rpg.session.forge.CanonicalSessionForgeManager;
import darkgrey.rpg.story.canonical.forge.CanonicalStoryForgeManager;
import darkgrey.rpg.task.forge.CanonicalTaskEventAdapter;
import darkgrey.rpg.task.forge.CanonicalTaskForgeManager;

@Mod(modid = DarkGreyRpg.MOD_ID, name = DarkGreyRpg.MOD_NAME, version = Tags.VERSION, dependencies = "after:customnpcs")
public final class DarkGreyRpg {

    public static final String MOD_ID = "darkgrey_rpg";
    public static final String MOD_NAME = "DarkGrey RPG";
    public static final Logger LOG = LogManager.getLogger(MOD_NAME);

    @Instance(MOD_ID)
    public static DarkGreyRpg instance;

    @SidedProxy(clientSide = "darkgrey.rpg.client.ClientProxy", serverSide = "darkgrey.rpg.proxy.CommonProxy")
    public static CommonProxy proxy;

    private static ProjectRepository projectRepository;
    private static EditorSessionManager editorSessions;
    private static RpgConfiguration configuration;
    private static LiveBridgeServer liveBridge;
    private static LivePickService livePicks;
    private static CanonicalSessionForgeManager canonicalSessionManager;
    private static CanonicalTaskForgeManager canonicalTaskManager;
    private static CanonicalStoryForgeManager canonicalStoryManager;
    private static StoryPackageLoader storyPackageLoader;
    private static StoryPackageRuntimeReloader.Result packageStartup;

    @EventHandler
    public void preInit(FMLPreInitializationEvent event) {
        File gameDirectory = event.getModConfigurationDirectory()
            .getParentFile();
        RpgRuntimeDirectories runtimeDirectories = RpgRuntimeDirectories.prepare(gameDirectory);
        darkgrey.rpg.gramophone.GramophoneLocalServer.initialize(
            runtimeDirectories.root()
                .toPath());
        for (String conflict : runtimeDirectories.migrationConflicts())
            LOG.warn("Preserved colliding legacy DarkGrey RPG runtime path: {}", conflict);
        configuration = RpgConfiguration
            .load(runtimeDirectories.configurationFile(event.getSuggestedConfigurationFile()));
        File projectDirectory = configuration.resolveProjectDirectory(event.getModConfigurationDirectory());

        projectRepository = new ProjectRepository(projectDirectory);
        storyPackageLoader = new StoryPackageLoader(
            configuration.resolveStoryPackageDirectory(event.getModConfigurationDirectory()),
            runtimeDirectories.cacheDirectory());
        canonicalSessionManager = new CanonicalSessionForgeManager(projectRepository);
        canonicalTaskManager = new CanonicalTaskForgeManager(projectRepository);
        canonicalStoryManager = new CanonicalStoryForgeManager(
            projectRepository,
            canonicalSessionManager,
            canonicalTaskManager);
        canonicalStoryManager.bindAggregateListeners();
        editorSessions = new EditorSessionManager();
        livePicks = new LivePickService();

        StoryPackageRuntimeReloader.Result startup = StoryPackageRuntimeReloader
            .startup(projectRepository, storyPackageLoader);
        packageStartup = startup;
        ProjectRepository.ReloadResult result = startup.getProjectReload();
        StoryPackageLoader.ReloadResult packageResult = startup.getPackageReload();
        if (!startup.isSuccessful()) {
            LOG.error("Story Package startup load: {}", packageResult.getSummary());
            for (String error : startup.getErrors()) LOG.error("Story Package startup detail: {}", error);
        }
        if (result.isSuccessful()) {
            LOG.info(
                "Loaded DarkGrey RPG project '{}' with {} story/stories, {} actor(s), {} item(s), {} item group(s), {} session(s), and {} task(s) from {}",
                result.getProjectDisplayName(),
                result.getStoryCount(),
                result.getActorCount(),
                result.getItemCount(),
                result.getItemGroupCount(),
                result.getSessionCount(),
                result.getTaskCount(),
                projectDirectory.getAbsolutePath());
        } else {
            LOG.error("DarkGrey RPG project reload failed: {}", result.getSummary());
        }

        ModItems.register();
        cpw.mods.fml.common.registry.GameRegistry.registerBlock(
            new darkgrey.rpg.gramophone.BlockGramophone(),
            darkgrey.rpg.gramophone.ItemGramophone.class,
            "gramophone");
        cpw.mods.fml.common.registry.GameRegistry
            .registerTileEntity(darkgrey.rpg.gramophone.TileGramophone.class, "darkgrey_rpg.gramophone");
        darkgrey.rpg.gramophone.GramophoneNetwork.register();
        darkgrey.rpg.gramophone.GramophoneServer gramophones = new darkgrey.rpg.gramophone.GramophoneServer();
        MinecraftForge.EVENT_BUS.register(gramophones);
        FMLCommonHandler.instance()
            .bus()
            .register(gramophones);
        cpw.mods.fml.common.network.NetworkRegistry.INSTANCE.registerGuiHandler(this, new NominatorGuiHandler());
        MinecraftForge.EVENT_BUS.register(new EditorToolEventHandler(projectRepository, editorSessions, livePicks));
        MinecraftForge.EVENT_BUS.register(new EntityToolsRuntime());
        darkgrey.rpg.story.canonical.forge.CanonicalStoryEventAdapter storyEventAdapter = new darkgrey.rpg.story.canonical.forge.CanonicalStoryEventAdapter(
            canonicalStoryManager);
        MinecraftForge.EVENT_BUS.register(storyEventAdapter);
        CanonicalTaskEventAdapter canonicalTaskEvents = new CanonicalTaskEventAdapter(canonicalTaskManager);
        MinecraftForge.EVENT_BUS.register(canonicalTaskEvents);
        FMLCommonHandler.instance()
            .bus()
            .register(canonicalTaskEvents);
        FMLCommonHandler.instance()
            .bus()
            .register(new MainThreadScheduler());
        FMLCommonHandler.instance()
            .bus()
            .register(storyEventAdapter);
        FMLCommonHandler.instance()
            .bus()
            .register(new darkgrey.rpg.creator.CanonicalTaskPresentationServer());
        DialogueNetwork.registerCommon();
        NominatorNetwork.registerCommon();
        EntityToolsNetwork.registerCommon();
        DialogueNetwork.CHANNEL.registerMessage(
            darkgrey.rpg.creator.CreatorSnapshot.Handler.class,
            darkgrey.rpg.creator.CreatorSnapshot.class,
            17,
            cpw.mods.fml.relauncher.Side.CLIENT);
        DialogueNetwork.CHANNEL.registerMessage(
            darkgrey.rpg.creator.CanonicalTaskUiRequest.Handler.class,
            darkgrey.rpg.creator.CanonicalTaskUiRequest.class,
            18,
            cpw.mods.fml.relauncher.Side.SERVER);
        FMLCommonHandler.instance()
            .bus()
            .register(new darkgrey.rpg.creator.CreatorInspectServer());
        proxy.registerClientDialogueNetwork();
    }

    @EventHandler
    public void init(FMLInitializationEvent event) {
        LOG.info("DarkGrey RPG Phase 5 runtime initialized");
    }

    @EventHandler
    public void serverStarting(FMLServerStartingEvent event) {
        darkgrey.rpg.story.canonical.forge.CanonicalBuffCatalog.rebuild();
        if (packageStartup != null && packageStartup.isPackageSetCommitted()) StoryPackageGenerationLifecycle.reconcile(
            event.getServer()
                .worldServerForDimension(0).mapStorage,
            storyPackageLoader.getPackages());
        event.registerServerCommand(
            new CommandDarkGreyRpg(
                projectRepository,
                editorSessions,
                null,
                null,
                null,
                canonicalSessionManager,
                canonicalTaskManager,
                canonicalStoryManager,
                storyPackageLoader));
        if (configuration.isLiveBridgeEnabled()) {
            liveBridge = new LiveBridgeServer(
                configuration.getLiveBridgePort(),
                new LiveBridgeController(projectRepository, livePicks, storyPackageLoader));
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
        if (liveBridge != null) {
            liveBridge.stop();
            liveBridge = null;
        }
    }

    public static ProjectRepository getProjectRepository() {
        return projectRepository;
    }

    public static StoryPackageLoader getStoryPackageLoader() {
        return storyPackageLoader;
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
