using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class CanonicalStoryWorkspaceViewModelTests
{
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
        Assert.HasCount(1, workspace.Breadcrumbs);
        Assert.AreEqual(GraphResourceKind.Story, workspace.Breadcrumbs[0].ResourceKind);
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
            Envelope(GraphResourceKind.Story, "story", "Story"),
            [
                new ActorResourceInfo("referenced", "参考角色", "referenced.json", []),
                new ActorResourceInfo("owned", "自有角色", "owned.json", []),
            ],
            [new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session",
                new GraphDocument([line]))]);

        var session = workspace.SessionItems.Single();
        Assert.IsTrue(workspace.OpenGraphResource(session));
        Assert.IsTrue(workspace.SelectGraphNode(workspace.ActiveGraphHost.Nodes.Single()));

        Assert.IsNotNull(workspace.NodeInspector);
        CollectionAssert.AreEqual(new[] { "", "referenced", "owned" },
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
        Assert.HasCount(2, workspace.Breadcrumbs);
        Assert.AreEqual(GraphResourceKind.Session, workspace.Breadcrumbs[1].ResourceKind);
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
    public void UnsavedChangesGateCanBlockLocalGraphSwitchAndReturn()
    {
        using var workspace = Workspace();
        var session = workspace.SessionItems.Single();
        var task = workspace.TaskItems.Single();
        Assert.IsTrue(workspace.OpenGraphResource(session));
        workspace.CanLeaveGraph = editor => !ReferenceEquals(editor, session.Editor);

        Assert.IsFalse(workspace.OpenGraphResource(task));
        Assert.IsFalse(workspace.ReturnToStory());
        Assert.AreSame(session.Editor, workspace.ActiveEditor);
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
        var story = Envelope(GraphResourceKind.Story, "story", "Story");
        var wrong = Envelope(GraphResourceKind.Task, "wrong", "Wrong");
        Assert.ThrowsExactly<ArgumentException>(
            () => new CanonicalStoryWorkspaceViewModel(story, sessions: [wrong]));
        var duplicate = Envelope(GraphResourceKind.Session, "same", "Same");
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
            Envelope(GraphResourceKind.Session, "foreign", "Foreign"));
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
        actors.SaveActor(actors.CreateActor("actor", "Actor"));
        store.Stories.Create(Envelope(GraphResourceKind.Story, "story", "Story"));
        store.Sessions.Create(Envelope(GraphResourceKind.Session, "session", "Session"));
        store.Memberships.Create(new CanonicalStoryMembershipManifest(
            "story",
            new CanonicalStoryMembershipSet
            {
                Actors = ["missing_actor", "actor"],
                Tasks = ["missing_task"],
            },
            new CanonicalStoryMembershipSet { Sessions = ["session"] }));

        using var workspace = new CanonicalStoryWorkspaceViewModel(
            new CanonicalStoryWorkspaceLoader(store, actors).Load("story"));

        var actorFolder = workspace.Folders.Single(folder => folder.Kind == CanonicalStoryFolderKind.Actors);
        CollectionAssert.AreEqual(
            new[] { "missing_actor", "actor" },
            actorFolder.Items.Select(item => item.Id).ToArray());
        Assert.IsTrue(workspace.ActorItems.Single().IsOwned);
        Assert.IsTrue(workspace.SessionItems.Single().IsReferenced);
        Assert.HasCount(2, workspace.MissingItems);
        Assert.HasCount(2, workspace.ValidationIssues);

        var missing = workspace.MissingItems.Single(item => item.Id == "missing_task");
        var storyHost = workspace.ActiveGraphHost;
        Assert.IsTrue(workspace.SelectTreeItem(missing));
        Assert.AreSame(storyHost, workspace.ActiveGraphHost);
        Assert.AreSame(missing, workspace.SelectedMissingResource);
        Assert.IsFalse(workspace.OpenSelectedResourceCommand.CanExecute(null));
        Assert.AreEqual("缺失任务", workspace.InspectorKindText);
        Assert.AreEqual("资源缺失", workspace.InspectorSaveStateText);
        StringAssert.Contains(workspace.InspectorValidationText, "missing_task");
    }

    private static CanonicalStoryWorkspaceViewModel Workspace()
        => new(
            Envelope(GraphResourceKind.Story, "story", "Story"),
            [new ActorResourceInfo("actor", "Actor", "actor.json", [])],
            [Envelope(GraphResourceKind.Session, "session", "Session")],
            [Envelope(GraphResourceKind.Task, "task", "Task")]);

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
