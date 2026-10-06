package darkgrey.rpg.story.canonical.server;

import java.io.File;
import java.nio.file.Files;
import java.util.Arrays;
import java.util.Collections;
import java.util.List;
import java.util.Map;
import java.util.Set;
import java.util.UUID;

import net.minecraft.nbt.NBTTagCompound;

import darkgrey.rpg.client.gui.RuntimeDirectoryTree;
import darkgrey.rpg.graph.canonical.CanonicalStoryLogicConnection;
import darkgrey.rpg.network.message.nominator.NominatorCatalogCodec;
import darkgrey.rpg.nominator.NominatorCatalog;
import darkgrey.rpg.project.ProjectSnapshot;
import darkgrey.rpg.project.packages.LoadedStoryPackage;
import darkgrey.rpg.project.packages.StoryPackageLoader;
import darkgrey.rpg.project.packages.StoryPackageManagerService;
import darkgrey.rpg.project.packages.StoryPackageSnapshotMerger;
import darkgrey.rpg.session.persistence.CanonicalSessionSavedData;
import darkgrey.rpg.session.server.CanonicalSessionDispatch;
import darkgrey.rpg.session.server.CanonicalSessionServerService;
import darkgrey.rpg.story.canonical.CanonicalStorySessionCompletionRouter;
import darkgrey.rpg.task.instance.CanonicalTaskInstanceSnapshot;
import darkgrey.rpg.task.persistence.CanonicalTaskSavedData;
import darkgrey.rpg.task.runtime.CanonicalTaskEvent;

/** Regression against the user's current A -> B export, with isolated ledgers and files. */
public final class RuntimeInteractionUi0336Probe {

    private static final UUID PLAYER = UUID.fromString("50000000-0000-0000-0000-000000000031");

    public static void main(String[] args) throws Exception {
        File root = Files.createTempDirectory("dgr-runtime-ui-0336-")
            .toFile();
        File packages = new File(root, "StoryPackages");
        packages.mkdirs();
        Files.copy(new File(args[0]).toPath(), new File(packages, "group.dgrs.g").toPath());
        StoryPackageLoader loader = new StoryPackageLoader(packages);
        check(
            loader.reload()
                .isSuccessful(),
            "current export loads");
        ProjectSnapshot project = StoryPackageSnapshotMerger.merge(loader.getPackages());
        CanonicalStoryLogicConnection edge = project.getCanonicalStoryLogicConnections()
            .get(0);
        String a = edge.getSourceStoryId(), b = edge.getTargetStoryId();
        NominatorCatalog names = NominatorCatalog.from(project, loader);
        String actor = names.getActors()
            .get(0)
            .getId();
        check(
            names.getPackageChoice(b)
                .isGroup(),
            "physical group directory metadata");
        check(
            names.getPackageChoice(b)
                .isReference(actor),
            "B marks A's referenced actor");
        check(
            names.ownerName(actor)
                .equals(
                    names.getPackageChoice(a)
                        .getDisplayName()),
            "reference owner name");
        io.netty.buffer.ByteBuf wire = io.netty.buffer.Unpooled.buffer();
        NominatorCatalogCodec.write(wire, names);
        NominatorCatalog decoded = NominatorCatalogCodec.read(wire);
        check(!wire.isReadable(), "catalog fully consumed");
        wire.release();
        check(
            decoded.getPackageChoice(b)
                .isReference(actor)
                && decoded.getPackageChoice(b)
                    .isGroup(),
            "directory metadata round trip");
        check(
            decoded.resourceLabel(actor)
                .equals("[角色] 测试员A"),
            "actor display label");
        check(
            !decoded.resourceLabel("missing")
                .contains("missing"),
            "missing resource never exposes ID");

        CanonicalSessionSavedData stories = new CanonicalSessionSavedData();
        CanonicalStoryServerService service = new CanonicalStoryServerService(project, stories);
        CanonicalStoryDispatch startA = service.startByActor(PLAYER, a, actor, 100);
        CanonicalSessionServerService sessions = new CanonicalSessionServerService(project, stories);
        CanonicalSessionDispatch frame = sessions.start(PLAYER, a, startA.getPlacementId(), true);
        for (int guard = 0; frame.getCompletionResult() == null && guard < 100; guard++) {
            check(frame.getFrame() != null, "A provides a dialogue frame");
            frame = sessions.continueLine(
                PLAYER,
                a,
                frame.getFrame()
                    .getTransportId(),
                frame.getFrame()
                    .getCurrentNodeId());
        }
        check(frame.getCompletionResult() != null, "A dialogue completes");
        stories.acceptAndConsume(
            frame.getCompletionResult(),
            new CanonicalStorySessionCompletionRouter(project, a).route(frame.getCompletionResult()));
        service.resumeSession(PLAYER, a, 101);
        CanonicalStoryDispatch startB = service
            .startByFlow(PLAYER, a, edge.getSourcePortId(), b, edge.getTargetPortId(), 102);
        check(startB != null, "A output starts B");
        CanonicalTaskSavedData tasks = new CanonicalTaskSavedData();
        tasks.bind(project::getCanonicalTask);
        tasks.start(PLAYER, b, startB.getPlacementId(), project.getCanonicalTask(startB.getResourceId()), 103);
        NBTTagCompound beforeStories = new NBTTagCompound(), beforeTasks = new NBTTagCompound();
        stories.writeToNBT(beforeStories);
        tasks.writeToNBT(beforeTasks);
        List<CanonicalActorCandidate> candidates = CanonicalNpcArbitration
            .collect(PLAYER, project, service, stories, tasks.snapshots(), Collections.singleton(actor));
        check(candidates.size() == 2, "A restart and B continue appear together");
        check(
            candidates.get(0)
                .getStatus()
                .equals("restart")
                && candidates.get(1)
                    .getStatus()
                    .equals("continue"),
            "human action semantics");
        NBTTagCompound afterStories = new NBTTagCompound(), afterTasks = new NBTTagCompound();
        stories.writeToNBT(afterStories);
        tasks.writeToNBT(afterTasks);
        check(
            beforeStories.equals(afterStories) && beforeTasks.equals(afterTasks),
            "candidate collection has no side effects");
        CanonicalActorCandidate chosen = candidates.get(1);
        CanonicalActorChoiceStore choices = new CanonicalActorChoiceStore();
        CanonicalActorChoiceStore.Choice offered = choices
            .offer(PLAYER, UUID.randomUUID(), 7, 0, candidates, Collections.singleton(actor), 1000);
        check(offered.matchesBindings(Collections.singleton(actor)), "unchanged binding accepted");
        check(!offered.matchesBindings(Arrays.asList(actor, "new-binding")), "added binding invalidates choice");
        check(choices.consume(UUID.randomUUID(), offered.getToken(), 1001) == null, "another player cannot submit");
        check(choices.consume(PLAYER, offered.getToken(), 1001) == offered, "capability consumed once");
        check(choices.consume(PLAYER, offered.getToken(), 1002) == null, "duplicate choice has no capability");
        offered = choices.offer(PLAYER, UUID.randomUUID(), 7, 0, candidates, Collections.singleton(actor), 1000);
        check(choices.consume(PLAYER, offered.getToken(), 61001) == null, "expired choice has no capability");
        offered = choices.offer(PLAYER, UUID.randomUUID(), 7, 0, candidates, Collections.singleton(actor), 1000);
        choices.forget(PLAYER);
        check(choices.consume(PLAYER, offered.getToken(), 1001) == null, "cancelled choice has no capability");
        check(
            !chosen.matches(PLAYER, StoryPackageSnapshotMerger.merge(loader.getPackages()), chosen),
            "project generation fence");
        check(
            CanonicalNpcArbitration
                .collect(PLAYER, project, service, stories, tasks.snapshots(), Collections.singleton("missing"))
                .isEmpty(),
            "changed binding invalidates choice");
        CanonicalTaskSavedData restored = new CanonicalTaskSavedData();
        restored.readFromNBT(beforeTasks);
        restored.bind(project::getCanonicalTask);
        CanonicalActorCandidate restoredChoice = CanonicalNpcArbitration
            .collect(PLAYER, project, service, stories, restored.snapshots(), Collections.singleton(actor))
            .get(1);
        check(chosen.matches(PLAYER, project, restoredChoice), "save restoration preserves exact task state");
        restored.start(PLAYER, a, "other-story-task", project.getCanonicalTask(startB.getResourceId()), 103);
        restored.start(PLAYER, b, "other-placement", project.getCanonicalTask(startB.getResourceId()), 103);
        darkgrey.rpg.task.event.CanonicalTaskDispatchResult result = restored.dispatchScoped(
            PLAYER,
            b,
            chosen.getTaskPlacementId(),
            CanonicalTaskEvent.interactActors(Arrays.asList("unrelated", actor)),
            104);
        check(
            result.getSettledInstances()
                .size() == 1,
            "selected B settles exactly once");
        check(
            restored.getSnapshot(PLAYER, a, "other-story-task")
                .getStatus() == darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus.ACTIVE,
            "shared actor does not advance another Story task");
        check(
            restored.getSnapshot(PLAYER, b, "other-placement")
                .getStatus() == darkgrey.rpg.task.instance.CanonicalTaskInstanceStatus.ACTIVE,
            "shared actor does not advance another task placement");
        CanonicalTaskInstanceSnapshot settled = result.getSettledInstances()
            .get(0);
        CanonicalStoryDispatch second = service.resumeTask(PLAYER, b, settled, 105);
        check(second.getKind() == CanonicalStoryDispatchKind.SESSION, "B reaches its second dialogue");
        check(
            stories.getStorySnapshot(PLAYER, a)
                .getActivationTime() == 100,
            "A did not restart");
        check(
            restored
                .dispatchScoped(PLAYER, b, chosen.getTaskPlacementId(), CanonicalTaskEvent.interactActor(actor), 106)
                .getChangedInstanceCount() == 0,
            "replay cannot settle twice");
        directory();
        manager(root);
        labels();
        System.out.println(
            "RUNTIME_INTERACTION_UI_0336_PASS: current A restart/B continue, pure collection, scoped settlement, restore, names, tree, chunks, scan count and permissions");
    }

    private static void directory() {
        RuntimeDirectoryTree tree = new RuntimeDirectoryTree();
        List<RuntimeDirectoryTree.Entry> entries = Arrays.asList(
            new RuntimeDirectoryTree.Entry("single", "单故事", "single", "单故事", false),
            new RuntimeDirectoryTree.Entry("a", "成员甲", "g1", "故事组一", true),
            new RuntimeDirectoryTree.Entry("b", "成员乙", "g2", "故事组二", true));
        check(
            tree.rows(entries, "")
                .size() == 3,
            "groups collapse independently even with one member");
        tree.toggle("g1");
        tree.toggle("g2");
        check(
            tree.rows(entries, "")
                .size() == 5,
            "both groups expanded");
        check(
            tree.rows(entries, "")
                .get(0).depth == 0
                && tree.rows(entries, "")
                    .get(1).depth == 0
                && tree.rows(entries, "")
                    .get(2).depth == 1,
            "root alignment and child indent");
        tree.rows(entries, "成员乙");
        check(tree.isExpanded("g1") && tree.isExpanded("g2"), "search never changes saved folds");
        tree.toggle("g1");
        check(tree.isExpanded("g2"), "folding one group leaves the other open");
    }

    private static void labels() {
        String owner = "ST-2345-6789-ABCD-EFGH";
        NominatorCatalog names = new NominatorCatalog(
            Collections.<NominatorCatalog.Story>emptyList(),
            Arrays.asList(
                new NominatorCatalog.Actor(
                    owner + "~actor~a",
                    "同名",
                    "individual",
                    owner,
                    "",
                    Collections.<String>emptyList()),
                new NominatorCatalog.Actor(
                    owner + "~actor~b",
                    "同名",
                    "collective",
                    owner,
                    "",
                    Collections.<String>emptyList())),
            Collections
                .singletonList(new NominatorCatalog.Item(owner + "~item~c", "铜币", Collections.<String>emptyList())),
            Collections.singletonList(
                new NominatorCatalog.Item(owner + "~item_group~d", "剑", Collections.<String>emptyList())));
        check(
            names.resourceLabel(owner + "~actor~b")
                .equals("[角色组] 同名"),
            "actor group classified by definition");
        check(
            names.resourceLabel(owner + "~item~c")
                .equals("[物品] 铜币"),
            "item label");
        check(
            names.resourceLabel(owner + "~item_group~d")
                .equals("[物品组] 剑"),
            "item group label");
    }

    private static void manager(File root) throws Exception {
        File dir = new File(root, "Manager/StoryPackages");
        dir.mkdirs();
        for (int i = 0; i < 29; i++) Files.write(new File(dir, "bad" + i + ".dgrs").toPath(), new byte[] { 0 });
        StoryPackageLoader loader = new StoryPackageLoader(dir);
        final int[] scans = { 0 };
        StoryPackageManagerService manager = new StoryPackageManagerService(
            loader,
            new StoryPackageManagerService.RuntimeAccess() {

                public void reload() {
                    scans[0]++;
                    loader.reload();
                }

                public void retire(Map<String, LoadedStoryPackage> retained) {}

                public int activeCount(Set<String> stories) {
                    return 0;
                }
            });
        Object owner = new Object();
        long sequence = 1;
        NBTTagCompound input = new NBTTagCompound();
        input.setInteger("action", StoryPackageManagerService.OPEN);
        NBTTagCompound open = manager.request(owner, true, sequence++, input);
        check(scans[0] == 1, "opening scans once");
        String handle = open.getTagList("rows", 10)
            .getCompoundTagAt(0)
            .getString("handle");
        input.setString("session", open.getString("session"));
        input.setLong("revision", open.getLong("revision"));
        input.setInteger("action", StoryPackageManagerService.LIST);
        int total = open.getTagList("rows", 10)
            .tagCount();
        for (int page = 1; page < 3; page++) {
            input.setInteger("page", page);
            total += manager.request(owner, true, sequence++, input)
                .getTagList("rows", 10)
                .tagCount();
        }
        check(total == 29 && scans[0] == 1, "all chunks available without rescanning");
        input.setInteger("action", StoryPackageManagerService.RESCAN);
        input.setInteger("page", 0);
        NBTTagCompound rescan = manager.request(owner, true, sequence++, input);
        check(scans[0] == 2, "manual rescan only");
        check(
            handle.equals(
                rescan.getTagList("rows", 10)
                    .getCompoundTagAt(0)
                    .getString("handle")),
            "same source retains opaque handle");
        check(manager.notification(owner, true) == null, "idle manager has no heartbeat notification");
        loader.reload();
        check(
            manager.notification(owner, true)
                .getBoolean("notification"),
            "committed inventory change notifies");
        check(manager.notification(owner, true) == null, "one notification per inventory version");
        sequence = managerChunks(loader, manager, owner, input, sequence, scans);
        check(
            manager.notification(owner, false)
                .getBoolean("denied"),
            "notification rechecks permission");
    }

    /** Exercise bounded transport against a committed snapshot larger than every chunk size. */
    private static long managerChunks(StoryPackageLoader loader, StoryPackageManagerService manager, Object owner,
        NBTTagCompound input, long sequence, int[] scans) throws Exception {
        Set<String> members = new java.util.LinkedHashSet<String>();
        java.util.ArrayList<CanonicalStoryLogicConnection> edges = new java.util.ArrayList<CanonicalStoryLogicConnection>();
        java.util.ArrayList<String> issues = new java.util.ArrayList<String>();
        for (int i = 0; i < 65; i++) {
            String uid = String.format(java.util.Locale.ROOT, "ST-%04d-AAAA-BBBB-CCCC", i);
            members.add(uid);
            edges.add(
                new CanonicalStoryLogicConnection(
                    uid,
                    "out",
                    uid,
                    "in",
                    i % 2 == 0 ? darkgrey.rpg.graph.canonical.CanonicalGraphInterfaceKind.FLOW
                        : darkgrey.rpg.graph.canonical.CanonicalGraphInterfaceKind.LOGIC));
        }
        for (int i = 0; i < 26; i++) issues.add("测试诊断 " + i);
        // Snapshot injection isolates the manager protocol; disk validation is tested above with the user's package.
        java.lang.reflect.Constructor<darkgrey.rpg.project.packages.StoryPackageInventoryEntry> constructor = darkgrey.rpg.project.packages.StoryPackageInventoryEntry.class
            .getDeclaredConstructor(
                String.class,
                String.class,
                Set.class,
                boolean.class,
                List.class,
                List.class,
                Map.class,
                darkgrey.rpg.graph.canonical.CanonicalStoryLogicGraph.class);
        constructor.setAccessible(true);
        Object entry = constructor.newInstance(
            "many.dgrs.g",
            "大型故事组",
            members,
            true,
            issues,
            Collections.emptyList(),
            Collections.emptyMap(),
            new darkgrey.rpg.graph.canonical.CanonicalStoryLogicGraph(2, edges));
        java.lang.reflect.Field inventory = StoryPackageLoader.class.getDeclaredField("inventory");
        inventory.setAccessible(true);
        inventory.set(loader, Collections.singletonList(entry));
        java.lang.reflect.Field revision = StoryPackageLoader.class.getDeclaredField("revision");
        revision.setAccessible(true);
        revision.setLong(loader, loader.getInventoryRevision() + 1);
        input.setLong("revision", loader.getInventoryRevision());
        input.setInteger("action", StoryPackageManagerService.LIST);
        input.setInteger("page", 0);
        NBTTagCompound list = manager.request(owner, true, sequence++, input);
        input.setString(
            "handle",
            list.getTagList("rows", 10)
                .getCompoundTagAt(0)
                .getString("handle"));
        input.setInteger("action", StoryPackageManagerService.GRAPH);
        int memberCount = 0, edgeCount = 0, issueCount = 0;
        for (int page = 0; page < 3; page++) {
            input.setInteger("page", page);
            NBTTagCompound chunk = manager.request(owner, true, sequence++, input);
            check(!chunk.hasKey("error"), "bounded graph request accepted");
            int memberRows = chunk.getTagList("members", 10)
                .tagCount();
            int edgeRows = chunk.getTagList("edges", 10)
                .tagCount();
            int diagnosticRows = chunk.getTagList("diagnostics", 8)
                .tagCount();
            check(memberRows <= 32 && edgeRows <= 32 && diagnosticRows <= 12, "all payloads remain bounded");
            memberCount += memberRows;
            edgeCount += edgeRows;
            issueCount += diagnosticRows;
        }
        check(memberCount == 65 && edgeCount == 65 && issueCount == 26, "all members, graph and diagnostics received");
        check(scans[0] == 2, "automatic chunk requests never scan disk");
        return sequence;
    }

    private static void check(boolean condition, String message) {
        if (!condition) throw new AssertionError(message);
    }
}
