package darkgrey.rpg.story.canonical.server;

import java.io.File;
import java.nio.file.Files;
import java.util.Collections;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;
import net.minecraft.nbt.NBTTagList;
import net.minecraft.world.storage.MapStorage;

import darkgrey.rpg.client.gui.RuntimeGraphViewport;
import darkgrey.rpg.graph.canonical.CanonicalGraphNode;
import darkgrey.rpg.graph.canonical.CanonicalGraphResource;
import darkgrey.rpg.network.message.canonical.CanonicalSessionAction;
import darkgrey.rpg.network.message.canonical.CanonicalSessionFrame;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.project.packages.LoadedStoryPackage;
import darkgrey.rpg.project.packages.StoryPackageLoader;
import darkgrey.rpg.project.packages.StoryPackageManagerService;
import darkgrey.rpg.project.packages.StoryPackageSnapshotMerger;
import darkgrey.rpg.session.persistence.CanonicalSessionSavedData;
import darkgrey.rpg.session.server.CanonicalSessionDispatch;
import darkgrey.rpg.session.server.CanonicalSessionServerService;
import darkgrey.rpg.story.canonical.CanonicalStorySessionCompletionRouter;
import darkgrey.rpg.story.canonical.instance.CanonicalStoryCompletionHistory;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryStartConfiguration;
import darkgrey.rpg.story.canonical.runtime.CanonicalStoryStartDisposition;
import darkgrey.rpg.task.event.CanonicalTaskDispatchResult;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus;
import darkgrey.rpg.task.persistence.CanonicalTaskSavedData;
import darkgrey.rpg.task.runtime.CanonicalTaskEvent;

/** Current independent Tavern package, historical ONCE admission, and the shared graph camera. */
public final class TavernRepeat0336Probe {

    private static final UUID PLAYER = UUID.fromString("50000000-0000-0000-0000-000000000032");

    public static void main(String[] args) throws Exception {
        File directory = Files.createTempDirectory("dgr-tavern-repeat-")
            .toFile();
        Files.copy(new File(args[0]).toPath(), new File(directory, "tavern.dgrs").toPath());
        StoryPackageLoader loader = new StoryPackageLoader(directory);
        check(
            loader.reload()
                .isSuccessful(),
            "current Tavern package loads");
        ProjectSnapshot project = StoryPackageSnapshotMerger.merge(loader.getPackages());
        String uid = project.getCanonicalStories()
            .keySet()
            .iterator()
            .next();
        String actor = CanonicalStoryStartConfiguration.parse(project.getCanonicalStory(uid))
            .getTriggers()
            .get(0)
            .getString("actor_id");
        CanonicalSessionSavedData fresh = new CanonicalSessionSavedData();
        CanonicalStoryServerService initial = new CanonicalStoryServerService(project, fresh, loader::allowsNewStart);
        check(
            initial.actorCandidates(PLAYER, Collections.singleton(actor))
                .size() == 1,
            "first-start candidate");
        CanonicalStoryDispatch first = initial.startByActor(PLAYER, uid, actor, 1000);
        check(first.getKind() == CanonicalStoryDispatchKind.SESSION, "first click reaches dialogue");
        long activation = fresh.getStorySnapshot(PLAYER, uid)
            .getActivationTime();
        check(
            initial.actorCandidates(PLAYER, Collections.singleton(actor))
                .isEmpty(),
            "ACTIVE cannot restart");
        check(
            fresh.getStorySnapshot(PLAYER, uid)
                .getActivationTime() == activation,
            "active progress unchanged");

        // Mirrors the user's persisted history: this story completed under ONCE before the author changed it.
        NBTTagCompound row = new NBTTagCompound();
        row.setString("player", PLAYER.toString());
        row.setString("story_uid", uid);
        row.setString("status", "TERMINATED");
        row.setString("repeat_policy", "ONCE");
        row.setLong("activation", 100);
        row.setLong("terminal", 200);
        row.setLong("completed_count", 1);
        CanonicalStoryCompletionHistory history = history(row);
        NBTTagCompound before = new NBTTagCompound();
        history.writeToNBT(before);
        MapStorage storage = new MapStorage(null);
        storage.setData(CanonicalStoryCompletionHistory.DATA_NAME, history);
        CanonicalSessionSavedData stories = CanonicalSessionSavedData.get(storage);
        CanonicalStoryServerService service = new CanonicalStoryServerService(project, stories, loader::allowsNewStart);
        check(
            service.startDisposition(PLAYER, uid) == CanonicalStoryStartDisposition.REPEATABLE_RESTART,
            "normal completion uses current repeat policy after package update");
        check(
            service.actorCandidates(PLAYER, Collections.singleton(actor))
                .size() == 1,
            "historical restart candidate");
        NBTTagCompound after = new NBTTagCompound();
        history.writeToNBT(after);
        check(before.equals(after), "admission does not rewrite durable history");
        check(
            new CanonicalStoryServerService(project, stories, id -> false)
                .actorCandidates(PLAYER, Collections.singleton(actor))
                .isEmpty(),
            "disabled container blocks start");
        service = new CanonicalStoryServerService(project, stories, loader::allowsNewStart);
        CanonicalStoryDispatch restart = service.startByActor(PLAYER, uid, actor, 2000);
        CanonicalStoryDispatch rejected = finishSession(project, stories, service, uid, restart, 1, 2100);
        check(rejected.getKind() == CanonicalStoryDispatchKind.TERMINATED, "refusal ends story normally");
        check(
            service.actorCandidates(PLAYER, Collections.singleton(actor))
                .size() == 1,
            "refusal can repeat");
        restart = service.startByActor(PLAYER, uid, actor, 3000);
        CanonicalStoryDispatch task = finishSession(project, stories, service, uid, restart, 0, 3100);
        check(task.getKind() == CanonicalStoryDispatchKind.TASK, "acceptance enters task");
        CanonicalTaskSavedData tasks = new CanonicalTaskSavedData();
        tasks.bind(project::getCanonicalTask);
        CanonicalGraphResource taskResource = project.getCanonicalTask(task.getResourceId());
        tasks.start(PLAYER, uid, task.getPlacementId(), taskResource, 3200);
        check(
            CanonicalNpcArbitration
                .collect(PLAYER, project, service, stories, tasks.snapshots(), Collections.singleton(actor))
                .isEmpty(),
            "locked turn-in cannot restart active story");
        for (CanonicalGraphNode node : taskResource.getGraph()
            .getNodes()) {
            if ("objective".equals(node.getType()) && "kill_entity".equals(
                node.getProperties()
                    .get("objective_type")
                    .getAsString())) {
                tasks.dispatchScoped(
                    PLAYER,
                    uid,
                    task.getPlacementId(),
                    CanonicalTaskEvent.killEntity(
                        node.getProperties()
                            .get("entity")
                            .getAsString(),
                        3),
                    3300);
            }
        }
        check(
            CanonicalNpcArbitration
                .collect(PLAYER, project, service, stories, tasks.snapshots(), Collections.singleton(actor))
                .get(0)
                .isTask(),
            "ready turn-in is continuation, not restart");
        CanonicalTaskDispatchResult settled = tasks
            .dispatchScoped(PLAYER, uid, task.getPlacementId(), CanonicalTaskEvent.interactActor(actor), 3400);
        check(
            settled.getChangedInstanceCount() == 1 && settled.getErroredInstances()
                .isEmpty(),
            "turn-in advances once");
        for (CanonicalGraphNode node : taskResource.getGraph()
            .getNodes()) if ("reward".equals(node.getType())) {
                check(
                    tasks.grantReward(PLAYER, uid, task.getPlacementId(), node.getId(), 3401) != null,
                    "reward acknowledged");
                check(
                    tasks.grantReward(PLAYER, uid, task.getPlacementId(), node.getId(), 3402) == null,
                    "reward receipt only once");
            }
        CanonicalTaskInstanceSnapshot completed = tasks.getSnapshot(PLAYER, uid, task.getPlacementId());
        check(completed.getStatus() == CanonicalTaskInstanceStatus.SETTLED, "turn-in settles after reward receipt");
        check(
            tasks.dispatchScoped(PLAYER, uid, task.getPlacementId(), CanonicalTaskEvent.interactActor(actor), 3401)
                .getChangedInstanceCount() == 0,
            "replayed click does not settle twice");
        CanonicalStoryDispatch thanks = service.resumeTask(PLAYER, uid, completed, 3500);
        check(
            finishSession(project, stories, service, uid, thanks, 0, 3600).getKind()
                == CanonicalStoryDispatchKind.TERMINATED,
            "completion dialogue ends story");
        stories.discardByStoryIds(Collections.singleton(uid));
        NBTTagCompound checkpoint = new NBTTagCompound();
        history.writeToNBT(checkpoint);
        CanonicalStoryCompletionHistory restoredHistory = new CanonicalStoryCompletionHistory();
        restoredHistory.readFromNBT(checkpoint);
        storage = new MapStorage(null);
        storage.setData(CanonicalStoryCompletionHistory.DATA_NAME, restoredHistory);
        CanonicalStoryServerService restored = new CanonicalStoryServerService(
            project,
            CanonicalSessionSavedData.get(storage));
        check(
            restored.actorCandidates(PLAYER, Collections.singleton(actor))
                .size() == 1,
            "saved completion can repeat");
        NBTTagCompound error = (NBTTagCompound) row.copy();
        error.setString("status", "ERROR");
        check(
            history(error).disposition(PLAYER, uid, project.getCanonicalStory(uid), java.time.Clock.systemUTC())
                == CanonicalStoryStartDisposition.ERROR_TERMINAL,
            "error retirement remains blocked");
        viewport();
        explicitEnable(loader, directory, uid);
        System.out.println(
            "TAVERN_REPEAT_0336_PASS: first start, old ONCE history/current repeat, pure admission, active protection, refusal, turn-in, completion, restore, viewport and explicit enable/disable");
    }

    private static void explicitEnable(StoryPackageLoader loader, File directory, String uid) {
        final int[] reloads = { 0 };
        StoryPackageManagerService manager = new StoryPackageManagerService(
            loader,
            new StoryPackageManagerService.RuntimeAccess() {

                public void reload() {
                    reloads[0]++;
                    loader.reload();
                }

                public void retire(java.util.Map<String, LoadedStoryPackage> retained) {
                    throw new AssertionError("enable/disable must not delete or retire the container");
                }

                public int activeCount(java.util.Set<String> stories) {
                    return 1;
                }
            });
        Object owner = new Object();
        NBTTagCompound input = new NBTTagCompound();
        input.setInteger("action", StoryPackageManagerService.OPEN);
        NBTTagCompound response = manager.request(owner, true, 1, input);
        String handle = response.getTagList("rows", 10)
            .getCompoundTagAt(0)
            .getString("handle");
        input.setString("session", response.getString("session"));
        input.setString("handle", handle);
        input.setInteger("action", StoryPackageManagerService.TOGGLE);
        long sequence = 2;
        for (boolean enabled : new boolean[] { false, false, true, true }) {
            input.setLong("revision", response.getLong("revision"));
            input.setBoolean("enabled", enabled);
            response = manager.request(owner, true, sequence++, input);
            check(!response.hasKey("error"), "explicit state request succeeds");
            NBTTagCompound row = response.getTagList("rows", 10)
                .getCompoundTagAt(0);
            check(row.getBoolean("enabled") == enabled, "repeated explicit requests do not invert state");
            check(handle.equals(row.getString("handle")), "enable/disable retains source handle");
            check(loader.allowsNewStart(uid) == enabled, "container start admission follows explicit state");
            check(new File(directory, "tavern.dgrs").isFile(), "source package is never deleted");
        }
        input.setLong("revision", response.getLong("revision"));
        input.removeTag("enabled");
        check(
            manager.request(owner, true, sequence++, input)
                .hasKey("error"),
            "missing state is rejected");
        check(reloads[0] == 5, "only open and four explicit requests reload");
        check(loader.allowsNewStart(uid), "rejected request preserves admission");
        input.setInteger("action", StoryPackageManagerService.CLOSE);
        check(
            manager.request(owner, true, sequence++, input)
                .getBoolean("closed"),
            "close cleans management session");
        input.setInteger("action", StoryPackageManagerService.LIST);
        check(
            manager.request(owner, true, sequence, input)
                .getBoolean("closed"),
            "closed session cannot read directory");
    }

    private static CanonicalStoryCompletionHistory history(NBTTagCompound row) {
        NBTTagCompound root = new NBTTagCompound();
        root.setInteger("schema_version", 1);
        root.setString("identity_format", "story-uid-v1");
        NBTTagList rows = new NBTTagList();
        rows.appendTag(row);
        root.setTag("summaries", rows);
        CanonicalStoryCompletionHistory history = new CanonicalStoryCompletionHistory();
        history.readFromNBT(root);
        return history;
    }

    private static CanonicalStoryDispatch finishSession(ProjectSnapshot project, CanonicalSessionSavedData data,
        CanonicalStoryServerService service, String uid, CanonicalStoryDispatch start, int choice, long time) {
        CanonicalSessionServerService sessions = new CanonicalSessionServerService(project, data);
        CanonicalSessionDispatch step = sessions.start(PLAYER, uid, start.getPlacementId(), true);
        for (int guard = 0; step.getCompletionResult() == null && guard < 100; guard++) {
            CanonicalSessionFrame frame = step.getFrame();
            check(frame != null, "session frame exists");
            boolean choosing = frame.getKind() == CanonicalSessionFrame.Kind.CHOICE;
            step = sessions.dispatch(
                PLAYER,
                new CanonicalSessionAction(
                    frame.getTransportId(),
                    uid,
                    frame.getCurrentNodeId(),
                    choosing ? CanonicalSessionAction.Kind.CHOICE : CanonicalSessionAction.Kind.CONTINUE,
                    choosing ? frame.getChoices()
                        .get(choice)
                        .getOptionId() : null,
                    frame.getLineEpoch()));
        }
        check(step.getCompletionResult() != null, "session terminates");
        data.acceptAndConsume(
            step.getCompletionResult(),
            new CanonicalStorySessionCompletionRouter(project, uid).route(step.getCompletionResult()));
        return service.resumeSession(PLAYER, uid, time);
    }

    private static void viewport() {
        RuntimeGraphViewport view = new RuntimeGraphViewport(20, 30, 100, 80, 1, 0, 0);
        check(view.intersects(-5, 10, 25, 20) && view.intersects(90, 10, 25, 20), "left/right partial nodes visible");
        check(view.intersects(10, -5, 25, 20) && view.intersects(10, 70, 25, 20), "top/bottom partial nodes visible");
        check(!view.intersects(100, 10, 25, 20) && !view.intersects(-25, 10, 25, 20), "fully outside culled");
        for (double factor : new double[] { .05, 1.1, 100, .0001 }) {
            RuntimeGraphViewport next = view.zoomAt(65, 55, factor);
            check(
                Math.abs(next.modelX(65) - view.modelX(65)) < 1e-8
                    && Math.abs(next.modelY(55) - view.modelY(55)) < 1e-8,
                "cursor model position remains anchored including zoom limits");
            check(Math.abs(next.modelX(next.screenX(17)) - 17) < 1e-8, "drawing/hit transform round trip");
            view = next;
        }
        check(!view.contains(19, 55) && !view.contains(65, 110), "header/outside cannot hit graph");
    }

    private static void check(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
