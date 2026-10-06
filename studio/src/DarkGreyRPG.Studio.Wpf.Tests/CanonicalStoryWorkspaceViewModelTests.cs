using DarkGreyRPG.Studio.Core.Actors;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class CanonicalStoryWorkspaceViewModelTests
{
    [TestMethod]
    public void DisplayOrderInterleavesItemKindsAndReorderPublishesStableHandles()
    {
        using var directory = new TemporaryProjectDirectory();
        var store = new CanonicalProjectGraphStore(directory.Path);
        var items = new ItemRepository(directory.Path);
        items.SaveItem(new IndividualItemResource { ItemId = "ST-2345-6789-ABCD-EFGH~item~item_a", DisplayName = "A" });
        items.SaveItem(new IndividualItemResource { ItemId = "ST-2345-6789-ABCD-EFGH~item~item_b", DisplayName = "B" });
        items.SaveGroup(new CollectiveItemResource { GroupId = "ST-2345-6789-ABCD-EFGH~item_group~group_a", DisplayName = "Group" });
        store.Stories.Create(Envelope(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", "Story"));
        var membership = new CanonicalStoryMembershipManifest(
            "ST-2345-6789-ABCD-EFGH",
            new CanonicalStoryMembershipSet { Items = ["ST-2345-6789-ABCD-EFGH~item~item_a", "ST-2345-6789-ABCD-EFGH~item~item_b"] },
            new CanonicalStoryMembershipSet { ItemGroups = ["ST-2345-6789-ABCD-EFGH~item_group~group_a"] })
        {
            DisplayOrder = new CanonicalStoryDisplayOrder
            {
                Items = ["item_group:ST-2345-6789-ABCD-EFGH~item_group~group_a", "item:ST-2345-6789-ABCD-EFGH~item~item_b", "item:ST-2345-6789-ABCD-EFGH~item~item_a"],
            },
        };
        store.Memberships.Create(membership);
        using var workspace = new CanonicalStoryWorkspaceViewModel(
            new CanonicalStoryWorkspaceLoader(store, items: items).Load("ST-2345-6789-ABCD-EFGH"));
        var folder = workspace.Folders.Single(candidate => candidate.Kind == CanonicalStoryFolderKind.Items);
        CollectionAssert.AreEqual(new[] { "ST-2345-6789-ABCD-EFGH~item_group~group_a", "ST-2345-6789-ABCD-EFGH~item~item_b", "ST-2345-6789-ABCD-EFGH~item~item_a" },
            folder.Items.Select(item => item.Id).ToArray());
        IReadOnlyList<string>? persisted = null;
        workspace.ResourceOrderChangeRequested = (kind, handles) =>
        {
            Assert.AreEqual(CanonicalStoryFolderKind.Items, kind);
            persisted = handles;
            return true;
        };

        Assert.IsTrue(workspace.ReorderResource(folder.Items[2], folder.Items[0]));

        CollectionAssert.AreEqual(new[] { "ST-2345-6789-ABCD-EFGH~item~item_a", "ST-2345-6789-ABCD-EFGH~item_group~group_a", "ST-2345-6789-ABCD-EFGH~item~item_b" },
            folder.Items.Select(item => item.Id).ToArray());
        CollectionAssert.AreEqual(new[] { "item:ST-2345-6789-ABCD-EFGH~item~item_a", "item_group:ST-2345-6789-ABCD-EFGH~item_group~group_a", "item:ST-2345-6789-ABCD-EFGH~item~item_b" },
            persisted!.ToArray());
        Assert.IsTrue(workspace.ItemItems.Single(item => item.Id == "ST-2345-6789-ABCD-EFGH~item~item_a").IsOwned);
        Assert.IsTrue(workspace.ItemItems.Single(item => item.Id == "ST-2345-6789-ABCD-EFGH~item_group~group_a").IsReferenced);
    }

    [TestMethod]
    public void StaleDisplayOrderIsFilteredAndNewResourcesAppendAfterReload()
    {
        using var directory = new TemporaryProjectDirectory();
        var store = new CanonicalProjectGraphStore(directory.Path);
        var items = new ItemRepository(directory.Path);
        items.SaveItem(new IndividualItemResource { ItemId = "ST-2345-6789-ABCD-EFGH~item~item_a", DisplayName = "A" });
        items.SaveItem(new IndividualItemResource { ItemId = "ST-2345-6789-ABCD-EFGH~item~item_c", DisplayName = "C" });
        items.SaveItem(new IndividualItemResource { ItemId = "ST-2345-6789-ABCD-EFGH~item~item_d", DisplayName = "D" });
        store.Stories.Create(Envelope(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", "Story"));
        store.Memberships.Create(new CanonicalStoryMembershipManifest(
            "ST-2345-6789-ABCD-EFGH",
            new CanonicalStoryMembershipSet { Items = ["ST-2345-6789-ABCD-EFGH~item~item_a", "ST-2345-6789-ABCD-EFGH~item~item_c", "ST-2345-6789-ABCD-EFGH~item~item_d"] })
        {
            DisplayOrder = new CanonicalStoryDisplayOrder
            {
                Items = ["item:ST-2345-6789-ABCD-EFGH~item~item_c", "item:ST-2345-6789-ABCD-EFGH~item~item_b", "item:ST-2345-6789-ABCD-EFGH~item~item_a"],
            },
        });

        using var workspace = new CanonicalStoryWorkspaceViewModel(
            new CanonicalStoryWorkspaceLoader(store, items: items).Load("ST-2345-6789-ABCD-EFGH"));

        CollectionAssert.AreEqual(
            new[] { "ST-2345-6789-ABCD-EFGH~item~item_c", "ST-2345-6789-ABCD-EFGH~item~item_a", "ST-2345-6789-ABCD-EFGH~item~item_d" },
            workspace.ItemItems.Select(item => item.Id).ToArray());
    }

    [TestMethod]
    public void ResourceSnapshotAddsOnlyChangedEntriesAndKeepsUnrelatedIdentitySelectionAndEditor()
    {
        using var directory = new TemporaryProjectDirectory();
        var store = new CanonicalProjectGraphStore(directory.Path);
        var actors = new ActorRepository(directory.Path);
        var items = new ItemRepository(directory.Path);
        actors.SaveActor(actors.CreateActor("ST-2345-6789-ABCD-EFGH~actor~actor_a", "Actor A"));
        actors.SaveActor(actors.CreateActor("ST-2345-6789-ABCD-EFGH~actor~actor_b", "Actor B"));
        items.SaveItem(new IndividualItemResource { ItemId = "ST-2345-6789-ABCD-EFGH~item~item_a", DisplayName = "Item A" });
        items.SaveItem(new IndividualItemResource { ItemId = "ST-2345-6789-ABCD-EFGH~item~item_b", DisplayName = "Item B" });
        store.Stories.Create(Envelope(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", "Story"));
        store.Sessions.Create(Envelope(GraphResourceKind.Session, "ST-2345-6789-ABCD-EFGH~session~session_a", "Session A"));
        store.Sessions.Create(Envelope(GraphResourceKind.Session, "ST-2345-6789-ABCD-EFGH~session~session_b", "Session B"));
        store.Tasks.Create(Envelope(GraphResourceKind.Task, "ST-2345-6789-ABCD-EFGH~task~task_a", "Task A"));
        store.Tasks.Create(Envelope(GraphResourceKind.Task, "ST-2345-6789-ABCD-EFGH~task~task_b", "Task B"));
        store.Memberships.Create(new CanonicalStoryMembershipManifest(
            "ST-2345-6789-ABCD-EFGH",
            new CanonicalStoryMembershipSet
            {
                Actors = ["ST-2345-6789-ABCD-EFGH~actor~actor_a"],
                Items = ["ST-2345-6789-ABCD-EFGH~item~item_a"],
                Sessions = ["ST-2345-6789-ABCD-EFGH~session~session_a"],
                Tasks = ["ST-2345-6789-ABCD-EFGH~task~task_a"],
            },
            new CanonicalStoryMembershipSet()));
        var loader = new CanonicalStoryWorkspaceLoader(store, actors, items);
        using var workspace = new CanonicalStoryWorkspaceViewModel(loader.Load("ST-2345-6789-ABCD-EFGH"));
        var folders = workspace.Folders.ToDictionary(folder => folder.Kind);
        var actorA = workspace.ActorItems.Single();
        var itemA = workspace.ItemItems.Single();
        var sessionA = workspace.SessionItems.Single();
        var taskA = workspace.TaskItems.Single();
        Assert.IsTrue(workspace.OpenGraphResource(sessionA));

        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "ST-2345-6789-ABCD-EFGH",
            new CanonicalStoryMembershipSet
            {
                Actors = ["ST-2345-6789-ABCD-EFGH~actor~actor_a", "ST-2345-6789-ABCD-EFGH~actor~actor_b"],
                Items = ["ST-2345-6789-ABCD-EFGH~item~item_a", "ST-2345-6789-ABCD-EFGH~item~item_b"],
                Sessions = ["ST-2345-6789-ABCD-EFGH~session~session_a", "ST-2345-6789-ABCD-EFGH~session~session_b"],
                Tasks = ["ST-2345-6789-ABCD-EFGH~task~task_a", "ST-2345-6789-ABCD-EFGH~task~task_b"],
            },
            new CanonicalStoryMembershipSet()));
        workspace.ApplyResourceSnapshot(loader.Load("ST-2345-6789-ABCD-EFGH"), CanonicalStoryFolderKind.Sessions, "ST-2345-6789-ABCD-EFGH~session~session_a");

        foreach (var folder in workspace.Folders)
            Assert.AreSame(folders[folder.Kind], folder);
        Assert.AreSame(actorA, workspace.ActorItems.Single(item => item.Id == "ST-2345-6789-ABCD-EFGH~actor~actor_a"));
        Assert.AreSame(itemA, workspace.ItemItems.Single(item => item.Id == "ST-2345-6789-ABCD-EFGH~item~item_a"));
        Assert.AreSame(sessionA, workspace.SessionItems.Single(item => item.Id == "ST-2345-6789-ABCD-EFGH~session~session_a"));
        Assert.AreSame(taskA, workspace.TaskItems.Single(item => item.Id == "ST-2345-6789-ABCD-EFGH~task~task_a"));
        Assert.AreSame(sessionA.Editor, workspace.ActiveEditor);
        Assert.AreSame(sessionA, workspace.SelectedTreeItem);
        Assert.HasCount(2, workspace.ActorItems);
        Assert.HasCount(2, workspace.ItemItems);
        Assert.HasCount(2, workspace.SessionItems);
        Assert.HasCount(2, workspace.TaskItems);
    }

    [TestMethod]
    public void ResourceSnapshotAdoptsLifecycleStoryCleanupAndCannotResurrectDeletedPlacement()
    {
        using var directory = new TemporaryProjectDirectory();
        var store = new CanonicalProjectGraphStore(directory.Path);
        var session = Envelope(GraphResourceKind.Session, "ST-2345-6789-ABCD-EFGH~session~session", "Session");
        var start = GraphNodeFactory.CreateStoryStart("start", "trigger");
        var aggregate = CanonicalAggregateNodeFactory.Create(session, "session-placement").Candidate!;
        var story = new GraphResourceEnvelope(
            GraphResourceKind.Story,
            "ST-2345-6789-ABCD-EFGH",
            "Story",
            new GraphDocument(
                [start, aggregate],
                [new GraphConnection("start", "trigger", "session-placement", "flow_in", GraphInterfaceKind.Flow)]));
        store.Stories.Create(story);
        store.Sessions.Create(session);
        store.Memberships.Create(new CanonicalStoryMembershipManifest(
            "ST-2345-6789-ABCD-EFGH", new CanonicalStoryMembershipSet { Sessions = ["ST-2345-6789-ABCD-EFGH~session~session"] }));
        var loader = new CanonicalStoryWorkspaceLoader(store);
        using var workspace = new CanonicalStoryWorkspaceViewModel(loader.Load("ST-2345-6789-ABCD-EFGH"));
        var storyEditor = workspace.StoryEditor;

        Assert.IsTrue(storyEditor.Host.SetStoryStartRepeatPolicy("start", StoryStartSchema.Repeatable));
        store.Stories.Replace(storyEditor.CreatePersistenceSnapshot());
        storyEditor.MarkSaved();
        Assert.IsTrue(storyEditor.Host.CanUndo);

        new CanonicalStoryResourceLifecycleService(store).DeleteOwnedSession("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~session~session");
        workspace.ApplyResourceSnapshot(loader.Load("ST-2345-6789-ABCD-EFGH"), CanonicalStoryFolderKind.Sessions);

        Assert.AreSame(storyEditor, workspace.StoryEditor);
        CollectionAssert.AreEqual(new[] { "start" }, storyEditor.Host.Graph.Nodes.Select(node => node.Id).ToArray());
        Assert.IsEmpty(storyEditor.Host.Graph.Connections);
        Assert.IsFalse(storyEditor.Host.CanUndo);
        Assert.IsFalse(storyEditor.IsGraphDirty);
        Assert.IsTrue(storyEditor.IsLayoutDirty,
            "Removing a persisted node leaves its stale layout entry pending cleanup until the next save.");
        new CanonicalGraphResourceSaveCoordinator(store).Replace(storyEditor);
        Assert.IsFalse(storyEditor.IsDirty);
        CollectionAssert.AreEqual(
            new[] { "start" },
            new CanonicalGraphLayoutStore(directory.Path).Load(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH").Keys.ToArray());

        Assert.IsTrue(storyEditor.Host.SetStoryStartRepeatPolicy("start", StoryStartSchema.Once));
        CollectionAssert.AreEqual(new[] { "start" }, storyEditor.CreatePersistenceSnapshot().Graph!.Nodes.Select(node => node.Id).ToArray());
    }

    [TestMethod]
    public void StoryFlowIsDefaultWithActorSessionTaskFolders()
    {
        using var workspace = Workspace();

        Assert.AreSame(workspace.StoryEditor, workspace.ActiveEditor);
        Assert.IsTrue(workspace.IsStoryFlowActive);
        CollectionAssert.AreEqual(
            new[] { CanonicalStoryFolderKind.Actors, CanonicalStoryFolderKind.Items, CanonicalStoryFolderKind.Sessions, CanonicalStoryFolderKind.Tasks },
            workspace.Folders.Select(folder => folder.Kind).ToArray());
        CollectionAssert.AreEqual(new[] { "角色", "物品", "会话", "任务" },
            workspace.Folders.Select(folder => folder.DisplayName).ToArray());
        Assert.HasCount(2, workspace.Breadcrumbs);
        Assert.AreEqual(CanonicalStoryBreadcrumbKind.Project, workspace.Breadcrumbs[0].Kind);
        Assert.AreEqual(GraphResourceKind.Story, workspace.Breadcrumbs[1].ResourceKind);
    }

    [TestMethod]
    public void ActorSingleClickChangesInspectorWithoutChangingMiddleGraph()
    {
        using var workspace = Workspace();
        var actor = workspace.ActorItems.Single();
        var host = workspace.ActiveGraphHost;

        Assert.IsTrue(workspace.SelectTreeItem(actor));

        Assert.AreSame(host, workspace.ActiveGraphHost);
        Assert.AreSame(actor, workspace.InspectorSelection);
        Assert.AreSame(actor, workspace.SelectedActor);
        Assert.IsTrue(workspace.IsStoryFlowActive);
    }

    [TestMethod]
    public void SessionLineInspectorUsesResolvedActorsFromCurrentStoryWorkspace()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line-1");
        using var workspace = new CanonicalStoryWorkspaceViewModel(
            Envelope(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", "Story"),
            [
                new ActorResourceInfo("ST-2345-6789-ABCD-EFGH~actor~referenced", "参考角色", "referenced.json", []),
                new ActorResourceInfo("ST-2345-6789-ABCD-EFGH~actor~owned", "自有角色", "owned.json", []),
            ],
            [new GraphResourceEnvelope(GraphResourceKind.Session, "ST-2345-6789-ABCD-EFGH~session~session", "Session",
                new GraphDocument([line]))]);

        var session = workspace.SessionItems.Single();
        Assert.IsTrue(workspace.OpenGraphResource(session));
        Assert.IsTrue(workspace.SelectGraphNode(workspace.ActiveGraphHost.Nodes.Single()));

        Assert.IsNotNull(workspace.NodeInspector);
        CollectionAssert.AreEqual(new[] { "ST-2345-6789-ABCD-EFGH~actor~referenced", "ST-2345-6789-ABCD-EFGH~actor~owned" },
            workspace.NodeInspector!.SpeakerOptions.Select(option => option.Id).ToArray());
    }

    [TestMethod]
    public void SessionRequiresExplicitOpenAndReusesEditorAcrossBreadcrumbReturn()
    {
        using var workspace = Workspace();
        var session = workspace.SessionItems.Single();

        Assert.IsTrue(workspace.SelectTreeItem(session));
        Assert.AreSame(workspace.StoryEditor, workspace.ActiveEditor);
        Assert.IsTrue(workspace.OpenSelectedResourceCommand.CanExecute(null));

        workspace.OpenSelectedResourceCommand.Execute(null);
        Assert.AreSame(session.Editor, workspace.ActiveEditor);
        Assert.HasCount(3, workspace.Breadcrumbs);
        Assert.AreEqual(GraphResourceKind.Session, workspace.Breadcrumbs[2].ResourceKind);
        Assert.IsTrue(session.Editor.Host.AddNode(
            GraphNodeFactory.Create(GraphScope.Session, "line", "line-1")));

        Assert.IsTrue(workspace.ReturnToStory());
        Assert.AreSame(workspace.StoryEditor, workspace.ActiveEditor);
        Assert.IsTrue(workspace.OpenGraphResource(session));
        Assert.AreSame(session.Editor, workspace.ActiveEditor);
        Assert.AreEqual("line-1", workspace.ActiveGraphHost.Graph.Nodes.Single().Id);
        Assert.IsTrue(session.Editor.IsDirty);
    }

    [TestMethod]
    public void StoryNodeFocusReturnsHomeAndCarriesStableFieldAndSequence()
    {
        using var workspace = Workspace();
        Assert.IsTrue(workspace.StoryEditor.Host.AddNode(
            GraphNodeFactory.Create(GraphScope.StoryFlow, "action", "focus-node")));
        Assert.IsTrue(workspace.OpenGraphResource(workspace.SessionItems.Single()));

        Assert.IsTrue(workspace.RequestStoryNodeFocus("focus-node", "target_story_id"));

        Assert.IsTrue(workspace.IsStoryFlowActive);
        Assert.AreEqual("focus-node", workspace.StoryNodeFocusRequest?.NodeId);
        Assert.AreEqual("target_story_id", workspace.StoryNodeFocusRequest?.Field);
        var firstSequence = workspace.StoryNodeFocusRequest!.Sequence;
        Assert.IsTrue(workspace.RequestStoryNodeFocus("focus-node"));
        Assert.IsGreaterThan(firstSequence, workspace.StoryNodeFocusRequest!.Sequence);
        Assert.IsFalse(workspace.RequestStoryNodeFocus("missing"));
    }

    [TestMethod]
    public void DirtyLocalGraphCanSwitchToAnotherGraphAndReturnWithoutLosingDraft()
    {
        using var workspace = Workspace();
        var session = workspace.SessionItems.Single();
        var task = workspace.TaskItems.Single();
        Assert.IsTrue(workspace.OpenGraphResource(session));
        Assert.IsTrue(session.Editor.Host.AddNode(
            GraphNodeFactory.Create(GraphScope.Session, "line", "dirty-line")));

        Assert.IsTrue(workspace.OpenGraphResource(task));
        Assert.IsTrue(workspace.ReturnToStory());
        Assert.IsTrue(session.Editor.IsDirty);
        Assert.IsTrue(session.Editor.Host.Graph.Nodes.Any(node => node.Id == "dirty-line"));
        Assert.AreSame(workspace.StoryEditor, workspace.ActiveEditor);
    }

    [TestMethod]
    public void FolderToggleAndResourceSingleClickDoNotChangeActiveGraph()
    {
        using var workspace = Workspace();
        var folder = workspace.Folders.Single(item => item.Kind == CanonicalStoryFolderKind.Sessions);
        var task = workspace.TaskItems.Single();
        var active = workspace.ActiveEditor;

        folder.ToggleCommand.Execute(null);
        Assert.IsFalse(folder.IsExpanded);
        Assert.IsTrue(workspace.SelectTreeItem(task));

        Assert.AreSame(active, workspace.ActiveEditor);
        Assert.AreSame(task.Editor, workspace.InspectorSelection);
    }

    [TestMethod]
    public void SessionAndTaskResourceActionsFollowCurrentTreeScope()
    {
        using var workspace = Workspace();
        var created = new List<GraphResourceKind>();
        var referenced = new List<GraphResourceKind>();
        var deleted = new List<ICanonicalStoryTreeItem>();
        workspace.CreateResourceRequested = created.Add;
        workspace.ReferenceResourceRequested = referenced.Add;
        workspace.DeleteResourceRequested = deleted.Add;
        workspace.RefreshResourceCommandStates();

        Assert.IsTrue(workspace.SelectFolder(CanonicalStoryFolderKind.Sessions));
        Assert.AreEqual(GraphResourceKind.Session, workspace.SelectedResourceKind);
        workspace.CreateSelectedResourceCommand.Execute(null);
        workspace.ReferenceSelectedResourceCommand.Execute(null);

        var task = workspace.TaskItems.Single();
        Assert.IsTrue(workspace.SelectTreeItem(task));
        Assert.AreEqual(CanonicalStoryFolderKind.Tasks, workspace.SelectedFolderKind);
        workspace.DeleteSelectedResourceCommand.Execute(null);

        CollectionAssert.AreEqual(new[] { GraphResourceKind.Session }, created);
        CollectionAssert.AreEqual(new[] { GraphResourceKind.Session }, referenced);
        CollectionAssert.AreEqual(new ICanonicalStoryTreeItem[] { task }, deleted);
    }

    [TestMethod]
    public void ActorResourceActionsUseActorCallbacksWithoutGraphKindCoercion()
    {
        using var workspace = Workspace();
        workspace.CreateResourceRequested = _ => Assert.Fail("Actor folders must not create graph resources.");
        workspace.ReferenceResourceRequested = _ => Assert.Fail("Actor folders must not reference graph resources.");
        var created = 0;
        var referenced = 0;
        ICanonicalStoryTreeItem? deleted = null;
        workspace.CreateActorRequested = () => created++;
        workspace.ReferenceActorRequested = () => referenced++;
        workspace.DeleteResourceRequested = item => deleted = item;
        workspace.RefreshResourceCommandStates();

        Assert.IsTrue(workspace.SelectFolder(CanonicalStoryFolderKind.Actors));
        Assert.IsNull(workspace.SelectedResourceKind);
        Assert.IsTrue(workspace.CreateSelectedResourceCommand.CanExecute(null));
        Assert.IsTrue(workspace.ReferenceSelectedResourceCommand.CanExecute(null));
        Assert.IsTrue(workspace.RequestCreate(CanonicalStoryFolderKind.Actors));
        Assert.IsTrue(workspace.RequestReference(CanonicalStoryFolderKind.Actors));
        Assert.IsTrue(workspace.RequestDelete(workspace.ActorItems.Single()));
        Assert.AreEqual(1, created);
        Assert.AreEqual(1, referenced);
        Assert.AreSame(workspace.ActorItems.Single(), deleted);
    }

    [TestMethod]
    public void WrongKindsDuplicatesAndForeignItemsFailClosed()
    {
        var story = Envelope(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", "Story");
        var wrong = Envelope(GraphResourceKind.Task, "ST-2345-6789-ABCD-EFGH~task~wrong", "Wrong");
        Assert.ThrowsExactly<ArgumentException>(
            () => new CanonicalStoryWorkspaceViewModel(story, sessions: [wrong]));
        var duplicate = Envelope(GraphResourceKind.Session, "ST-2345-6789-ABCD-EFGH~session~same", "Same");
        Assert.ThrowsExactly<ArgumentException>(
            () => new CanonicalStoryWorkspaceViewModel(story, sessions: [duplicate, duplicate]));
        var actor = new ActorResourceInfo("same", "Same", "same.json", []);
        Assert.ThrowsExactly<ArgumentException>(
            () => new CanonicalStoryWorkspaceViewModel(story, actors: [actor, actor]));

        using var workspace = Workspace();
        var foreign = new CanonicalStoryActorItem(
            new ActorResourceInfo("foreign", "Foreign", "foreign.json", []));
        Assert.IsFalse(workspace.SelectTreeItem(foreign));
        using var foreignEditor = new CanonicalGraphResourceEditorViewModel(
            Envelope(GraphResourceKind.Session, "ST-2345-6789-ABCD-EFGH~session~foreign", "Foreign"));
        Assert.IsFalse(workspace.OpenGraphResource(new CanonicalStoryGraphItem(foreignEditor)));
        Assert.IsNull(workspace.SelectedTreeItem);
        Assert.IsTrue(workspace.IsStoryFlowActive);
    }

    [TestMethod]
    public void LoaderSnapshotRetainsProvenanceOrderAndSelectableMissingProblems()
    {
        using var directory = new TemporaryProjectDirectory();
        var store = new CanonicalProjectGraphStore(directory.Path);
        var actors = new ActorRepository(directory.Path);
        actors.SaveActor(actors.CreateActor("ST-2345-6789-ABCD-EFGH~actor~actor", "Actor"));
        store.Stories.Create(Envelope(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", "Story"));
        store.Sessions.Create(Envelope(GraphResourceKind.Session, "ST-2345-6789-ABCD-EFGH~session~session", "Session"));
        store.Memberships.Create(new CanonicalStoryMembershipManifest(
            "ST-2345-6789-ABCD-EFGH",
            new CanonicalStoryMembershipSet
            {
                Actors = ["ST-2345-6789-ABCD-EFGH~actor~missing_actor", "ST-2345-6789-ABCD-EFGH~actor~actor"],
                Tasks = ["ST-2345-6789-ABCD-EFGH~task~missing_task"],
            },
            new CanonicalStoryMembershipSet { Sessions = ["ST-2345-6789-ABCD-EFGH~session~session"] }));

        using var workspace = new CanonicalStoryWorkspaceViewModel(
            new CanonicalStoryWorkspaceLoader(store, actors).Load("ST-2345-6789-ABCD-EFGH"));

        var actorFolder = workspace.Folders.Single(folder => folder.Kind == CanonicalStoryFolderKind.Actors);
        CollectionAssert.AreEqual(
            new[] { "ST-2345-6789-ABCD-EFGH~actor~missing_actor", "ST-2345-6789-ABCD-EFGH~actor~actor" },
            actorFolder.Items.Select(item => item.Id).ToArray());
        Assert.IsTrue(workspace.ActorItems.Single().IsOwned);
        Assert.IsTrue(workspace.SessionItems.Single().IsReferenced);
        Assert.HasCount(2, workspace.MissingItems);
        Assert.HasCount(2, workspace.ValidationIssues);

        var missing = workspace.MissingItems.Single(item => item.Id == "ST-2345-6789-ABCD-EFGH~task~missing_task");
        var storyHost = workspace.ActiveGraphHost;
        Assert.IsTrue(workspace.SelectTreeItem(missing));
        Assert.AreSame(storyHost, workspace.ActiveGraphHost);
        Assert.AreSame(missing, workspace.SelectedMissingResource);
        Assert.IsFalse(workspace.OpenSelectedResourceCommand.CanExecute(null));
        Assert.AreEqual("缺失任务", workspace.InspectorKindText);
        Assert.AreEqual("资源缺失", workspace.InspectorSaveStateText);
        StringAssert.Contains(workspace.InspectorValidationText, "ST-2345-6789-ABCD-EFGH~task~missing_task");
    }

    [TestMethod]
    public void LoaderSnapshotRetainsCollectiveActorKindAndIdentityLabel()
    {
        using var directory = new TemporaryProjectDirectory();
        var store = new CanonicalProjectGraphStore(directory.Path);
        var actors = new ActorRepository(directory.Path);
        actors.SaveActor(actors.CreateCollective("ST-2345-6789-ABCD-EFGH~actor~guards", "守卫组"));
        store.Stories.Create(Envelope(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", "Story"));
        store.Memberships.Create(new CanonicalStoryMembershipManifest(
            "ST-2345-6789-ABCD-EFGH", new CanonicalStoryMembershipSet { Actors = ["ST-2345-6789-ABCD-EFGH~actor~guards"] }));

        using var workspace = new CanonicalStoryWorkspaceViewModel(
            new CanonicalStoryWorkspaceLoader(store, actors).Load("ST-2345-6789-ABCD-EFGH"));
        var actor = workspace.ActorItems.Single();

        Assert.AreEqual(CollectiveActorResource.ResourceType, actor.Actor.Type);
        Assert.AreEqual("[角色组]", actor.IdentityText);
        Assert.IsTrue(workspace.SelectTreeItem(actor));
        Assert.AreEqual("角色组", workspace.InspectorKindText);
        Assert.AreEqual("Group_ID", workspace.InspectorIdentityLabel);
    }

    [TestMethod]
    public void ActorDropUpdatesSessionSpeakerWithoutChangingInspectorContext()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        line.Properties["speaker_actor_id"] = JsonSerializer.SerializeToElement("ST-2345-6789-ABCD-EFGH~actor~old_actor");
        using var workspace = new CanonicalStoryWorkspaceViewModel(
            Envelope(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", "Story"),
            [new ActorResourceInfo("ST-2345-6789-ABCD-EFGH~actor~actor", "Actor", "actor.json", [])],
            [new GraphResourceEnvelope(GraphResourceKind.Session, "ST-2345-6789-ABCD-EFGH~session~session", "Session", new GraphDocument([line]))]);
        Assert.IsTrue(workspace.OpenGraphResource(workspace.SessionItems.Single()));
        var node = workspace.ActiveGraphHost.Nodes.Single();
        Assert.IsTrue(workspace.SelectGraphNode(node));
        var inspector = workspace.NodeInspector;

        Assert.IsTrue(workspace.ApplyResourceToNodeParameter(node, workspace.ActorItems.Single()));

        Assert.AreSame(inspector, workspace.NodeInspector);
        Assert.AreEqual("ST-2345-6789-ABCD-EFGH~actor~actor", workspace.ActiveGraphHost.Graph.Nodes.Single()
            .Properties["speaker_actor_id"].GetString());
        StringAssert.Contains(workspace.ParameterDropMessage, "Actor");
    }

    [TestMethod]
    public void ActorDropUpdatesExistingStoryStartActorInteraction()
    {
        var start = GraphNodeFactory.CreateStoryStart("start", triggerPortId: "actor-trigger");
        start.Ports.Single(port => port.Id == "actor-trigger").DisplayName = "角色交互";
        start.Properties[StoryStartSchema.TriggersProperty] = JsonSerializer.SerializeToElement(new[]
        {
            new
            {
                port_id = "actor-trigger",
                display_name = "角色交互",
                trigger_type = StoryStartSchema.ActorInteraction,
                trigger_properties = new { actor_id = "ST-2345-6789-ABCD-EFGH~actor~old_actor" },
                order = 0,
            },
        });
        using var workspace = new CanonicalStoryWorkspaceViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", "Story", new GraphDocument([start])),
            [new ActorResourceInfo("ST-2345-6789-ABCD-EFGH~actor~actor", "Actor", "actor.json", [])]);
        var node = workspace.ActiveGraphHost.Nodes.Single();

        Assert.IsTrue(workspace.ApplyResourceToNodeParameter(node, workspace.ActorItems.Single()));

        var slot = StoryStartSchema.ReadTriggers(workspace.ActiveGraphHost.Graph.Nodes.Single()).Single();
        Assert.AreEqual("ST-2345-6789-ABCD-EFGH~actor~actor", slot.TriggerProperties.GetProperty(StoryStartSchema.ActorIdProperty).GetString());
    }

    [TestMethod]
    public void ItemDropUpdatesGiveItemButCollectiveItemFailsWithoutMutation()
    {
        var action = GraphNodeFactory.Create(GraphScope.StoryFlow, CanonicalStoryActionSchema.NodeType, "action");
        Assert.IsTrue(CanonicalStoryActionSchema.TryInitializeType(
            action, CanonicalStoryActionSchema.GiveItem, out var issues), string.Join("; ", issues));
        using var workspace = new CanonicalStoryWorkspaceViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", "Story", new GraphDocument([action])),
            items:
            [
                new IndividualItemResource { ItemId = "ST-2345-6789-ABCD-EFGH~item~coin", DisplayName = "铜币" },
                new CollectiveItemResource { GroupId = "ST-2345-6789-ABCD-EFGH~item_group~ore", DisplayName = "矿石组" },
            ]);
        var node = workspace.ActiveGraphHost.Nodes.Single();
        Assert.IsTrue(workspace.SelectGraphNode(node));
        var inspector = workspace.NodeInspector;

        Assert.IsTrue(workspace.ApplyResourceToNodeParameter(
            node, workspace.ItemItems.Single(item => item.Id == "ST-2345-6789-ABCD-EFGH~item~coin")));
        Assert.AreEqual("ST-2345-6789-ABCD-EFGH~item~coin", workspace.ActiveGraphHost.Graph.Nodes.Single()
            .Properties[CanonicalStoryActionSchema.ItemProperty].GetString());
        var beforeInvalidDrop = workspace.ActiveGraphHost.Graph.ToJson();

        Assert.IsFalse(workspace.ApplyResourceToNodeParameter(
            node, workspace.ItemItems.Single(item => item.Id == "ST-2345-6789-ABCD-EFGH~item_group~ore")));

        Assert.AreEqual(beforeInvalidDrop, workspace.ActiveGraphHost.Graph.ToJson());
        Assert.AreSame(inspector, workspace.NodeInspector);
        StringAssert.Contains(workspace.ParameterDropMessage, "不能使用物品组");
    }

    [TestMethod]
    public void ActorDropUpdatesTaskActorTargetsOnlyForCompatibleObjectiveTypes()
    {
        var objective = GraphNodeFactory.Create(GraphScope.Task, CanonicalTaskObjectiveSchema.NodeType, "objective");
        using var workspace = new CanonicalStoryWorkspaceViewModel(
            Envelope(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", "Story"),
            [new ActorResourceInfo("ST-2345-6789-ABCD-EFGH~actor~slime_group", "史莱姆组", "slime_group.json", [])],
            tasks: [new GraphResourceEnvelope(GraphResourceKind.Task, "ST-2345-6789-ABCD-EFGH~task~task", "Task", new GraphDocument([objective]))]);
        Assert.IsTrue(workspace.OpenGraphResource(workspace.TaskItems.Single()));
        var node = workspace.ActiveGraphHost.Nodes.Single();

        Assert.IsTrue(workspace.ApplyResourceToNodeParameter(node, workspace.ActorItems.Single()));

        Assert.AreEqual("ST-2345-6789-ABCD-EFGH~actor~slime_group", workspace.ActiveGraphHost.Graph.Nodes.Single()
            .Properties[CanonicalTaskObjectiveSchema.EntityProperty].GetString());
    }

    [TestMethod]
    public void HighFrequencyNodesExposeChineseParameterSummariesInTheirGraphProjection()
    {
        var start = GraphNodeFactory.CreateStoryStart("start", "trigger");
        var objective = GraphNodeFactory.Create(GraphScope.Task, CanonicalTaskObjectiveSchema.NodeType, "objective");
        var action = GraphNodeFactory.Create(GraphScope.StoryFlow, CanonicalStoryActionSchema.NodeType, "action");
        Assert.IsTrue(CanonicalStoryActionSchema.TryInitializeType(
            action, CanonicalStoryActionSchema.GiveItem, out var issues), string.Join("; ", issues));
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line");
        line.Properties["speaker_actor_id"] = JsonSerializer.SerializeToElement("ST-2345-6789-ABCD-EFGH~actor~bartender");
        line.Properties.Remove("pages");
        line.Properties["text"] = JsonSerializer.SerializeToElement("欢迎来到酒馆");

        using var storyStart = new CanonicalGraphResourceEditorViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", "Story", new GraphDocument([start])));
        using var task = new CanonicalGraphResourceEditorViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Task, "ST-2345-6789-ABCD-EFGH~task~task", "Task", new GraphDocument([objective])));
        using var storyAction = new CanonicalGraphResourceEditorViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Story, "ST-JKLM-NPQR-STUV-WXYZ", "Story", new GraphDocument([action])));
        using var session = new CanonicalGraphResourceEditorViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Session, "ST-2345-6789-ABCD-EFGH~session~session", "Session", new GraphDocument([line])));

        StringAssert.Contains(storyStart.Host.Nodes.Single().ParameterSummary, "进入区域");
        StringAssert.Contains(task.Host.Nodes.Single().ParameterSummary, "击杀实体");
        Assert.AreEqual("执行「物品给予」", storyAction.Host.Nodes.Single().DisplayName);
        StringAssert.Contains(storyAction.Host.Nodes.Single().ParameterSummary, "物品给予");
        StringAssert.Contains(session.Host.Nodes.Single().ParameterSummary, "欢迎来到酒馆");
        Assert.IsTrue(session.Host.Nodes.Single().HasParameterSummary);
    }

    [TestMethod]
    public void RecreatedWorkspaceLoadsDistinctStorySessionAndTaskLayouts()
    {
        using var directory = new TemporaryProjectDirectory();
        var story = new GraphResourceEnvelope(
            GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", "Story",
            new GraphDocument([GraphNodeFactory.CreateStoryStart("story-node", "trigger")]));
        var session = new GraphResourceEnvelope(
            GraphResourceKind.Session, "ST-2345-6789-ABCD-EFGH~session~session", "Session",
            new GraphDocument([GraphNodeFactory.Create(GraphScope.Session, "line", "session-node")]));
        var task = new GraphResourceEnvelope(
            GraphResourceKind.Task, "ST-2345-6789-ABCD-EFGH~task~task", "Task",
            new GraphDocument([GraphNodeFactory.Create(GraphScope.Task, "objective", "task-node")]));
        var layoutStore = new CanonicalGraphLayoutStore(directory.Path);
        layoutStore.Save(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", Positions(("story-node", 101, 201), ("deleted", 9, 9)));
        layoutStore.Save(GraphResourceKind.Session, "ST-2345-6789-ABCD-EFGH~session~session", Positions(("session-node", 302, 402)));
        layoutStore.Save(GraphResourceKind.Task, "ST-2345-6789-ABCD-EFGH~task~task", Positions(("task-node", 503, 603)));

        using var reopened = new CanonicalStoryWorkspaceViewModel(
            story, sessions: [session], tasks: [task], layoutStore: layoutStore);

        Assert.AreEqual(new GraphEditorNodePosition(101, 201), reopened.StoryEditor.Host.Layout["story-node"]);
        Assert.AreEqual(new GraphEditorNodePosition(302, 402), reopened.SessionEditors.Single().Host.Layout["session-node"]);
        Assert.AreEqual(new GraphEditorNodePosition(503, 603), reopened.TaskEditors.Single().Host.Layout["task-node"]);
        Assert.IsFalse(reopened.StoryEditor.Host.Layout.ContainsKey("deleted"));
        Assert.IsFalse(reopened.HasDirtyEditors);
        Assert.AreNotSame(reopened.StoryEditor.ViewportState, reopened.SessionEditors.Single().ViewportState);
        Assert.AreNotSame(reopened.StoryEditor.ViewportState, reopened.TaskEditors.Single().ViewportState);
    }

    private static IReadOnlyDictionary<string, ProjectGraphNodeLayout> Positions(
        params (string Id, double X, double Y)[] values)
        => values.ToDictionary(
            value => value.Id,
            value => new ProjectGraphNodeLayout { X = value.X, Y = value.Y },
            StringComparer.Ordinal);

    private static CanonicalStoryWorkspaceViewModel Workspace()
        => new(
            Envelope(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", "Story"),
            [new ActorResourceInfo("ST-2345-6789-ABCD-EFGH~actor~actor", "Actor", "actor.json", [])],
            [Envelope(GraphResourceKind.Session, "ST-2345-6789-ABCD-EFGH~session~session", "Session")],
            [Envelope(GraphResourceKind.Task, "ST-2345-6789-ABCD-EFGH~task~task", "Task")]);

    private static GraphResourceEnvelope Envelope(GraphResourceKind kind, string id, string name)
        => new(kind, id, name, new GraphDocument());

    private sealed class TemporaryProjectDirectory : IDisposable
    {
        public TemporaryProjectDirectory()
        {
            Path = System.IO.Path.Combine(
                AppContext.BaseDirectory,
                "temp",
                "darkgrey-rpg-workspace-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path);
        }

        public string Path { get; }

        public void Dispose()
        {
            if (Directory.Exists(Path)) Directory.Delete(Path, recursive: true);
        }
    }
}
