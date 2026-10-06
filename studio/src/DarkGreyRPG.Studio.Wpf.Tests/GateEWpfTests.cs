using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

// Replaces flat Dialogue/Quest draft gates with current Session/Task lifecycle contracts.
[TestClass]
public sealed class GateEWpfTests
{
    [TestMethod]
    [DataRow(GraphResourceKind.Session)]
    [DataRow(GraphResourceKind.Task)]
    public void CreateAllocatesOwnedMinimalResourceWithoutFakeActor(GraphResourceKind kind)
    {
        using var f = new Fixture(kind);
        var item = f.Create();
        var saved = f.Repository.Load(item.Id);
        Assert.AreEqual(kind, saved.ResourceKind);
        Assert.AreEqual("Created", saved.DisplayName);
        StringAssert.StartsWith(item.Id, Fixture.Owner + "~");
        Assert.AreNotEqual("ignored_identity", item.Id);
        Assert.HasCount(1, saved.Graph!.Nodes);
        Assert.AreEqual(kind == GraphResourceKind.Session ? "start" : "objective", saved.Graph.Nodes.Single().Type);
        Assert.AreEqual(item.Id, f.Workspace.SelectedTreeItem?.Id);
        Assert.IsFalse(item.Editor.IsDirty);
        Assert.IsEmpty(f.Shell.Actors);
        Assert.AreEqual(1, f.Dialogs.CreateCalls);
    }

    [TestMethod]
    [DataRow(GraphResourceKind.Session)]
    [DataRow(GraphResourceKind.Task)]
    public void CancelCreationPreservesMembershipAndFiles(GraphResourceKind kind)
    {
        using var f = new Fixture(kind);
        f.Dialogs.CreateResult = null;
        var before = File.ReadAllBytes(f.Store.Memberships.GetPath(Fixture.Owner));
        Assert.IsTrue(f.Workspace.RequestCreate(f.Folder));
        Assert.IsEmpty(f.Items);
        Assert.IsEmpty(f.Repository.List());
        CollectionAssert.AreEqual(before, File.ReadAllBytes(f.Store.Memberships.GetPath(Fixture.Owner)));
    }

    [TestMethod]
    [DataRow(GraphResourceKind.Session)]
    [DataRow(GraphResourceKind.Task)]
    public void EditUndoRedoSaveRetainsSelection(GraphResourceKind kind)
    {
        using var f = new Fixture(kind);
        var item = f.Create();
        f.Edit(item);
        Assert.HasCount(1, f.Repository.Load(item.Id).Graph!.Nodes);
        item.Editor.UndoCommand.Execute(null);
        Assert.HasCount(1, item.Editor.Host.Graph.Nodes);
        item.Editor.RedoCommand.Execute(null);
        Assert.HasCount(2, item.Editor.Host.Graph.Nodes);
        f.Shell.SaveCurrentResourceCommand.Execute(null);
        Assert.IsFalse(item.Editor.IsDirty, f.Shell.StatusMessage);
        Assert.AreSame(item.Editor, f.Workspace.ActiveEditor);
        Assert.HasCount(2, f.Repository.Load(item.Id).Graph!.Nodes);
        Assert.IsEmpty(f.Shell.Actors);
    }

    [TestMethod]
    [DataRow(GraphResourceKind.Session)]
    [DataRow(GraphResourceKind.Task)]
    public void NavigationRetainsDetachedDraft(GraphResourceKind kind)
    {
        using var f = new Fixture(kind);
        var first = f.Create();
        var second = f.Create();
        f.Edit(first);
        Assert.IsTrue(f.Workspace.OpenGraphResource(second));
        Assert.IsTrue(first.Editor.IsDirty);
        f.Shell.ShowProjectHomeCommand.Execute(null);
        f.Shell.OpenStory(f.Shell.ProjectHome.Stories.Single());
        Assert.AreSame(f.Workspace, f.Shell.CanonicalStoryWorkspace);
        Assert.IsTrue(f.Workspace.OpenGraphResource(first));
        Assert.AreSame(first.Editor, f.Workspace.ActiveEditor);
        Assert.HasCount(2, first.Editor.Host.Graph.Nodes);
        Assert.HasCount(1, f.Repository.Load(first.Id).Graph!.Nodes);
    }

    [TestMethod]
    [DataRow(GraphResourceKind.Session, UnsavedChangesChoice.Save)]
    [DataRow(GraphResourceKind.Session, UnsavedChangesChoice.Discard)]
    [DataRow(GraphResourceKind.Session, UnsavedChangesChoice.Cancel)]
    [DataRow(GraphResourceKind.Task, UnsavedChangesChoice.Save)]
    [DataRow(GraphResourceKind.Task, UnsavedChangesChoice.Discard)]
    [DataRow(GraphResourceKind.Task, UnsavedChangesChoice.Cancel)]
    public void CloseChoicesRespectPersistedBaseline(GraphResourceKind kind, UnsavedChangesChoice choice)
    {
        using var f = new Fixture(kind);
        var item = f.Create();
        f.Edit(item);
        f.Dialogs.CloseChoice = choice;
        Assert.AreEqual(choice != UnsavedChangesChoice.Cancel, f.Shell.TryClose());
        Assert.AreEqual(1, f.Dialogs.CloseCalls);
        Assert.HasCount(choice == UnsavedChangesChoice.Save ? 2 : 1, f.Repository.Load(item.Id).Graph!.Nodes);
        if (choice == UnsavedChangesChoice.Cancel)
        {
            Assert.AreSame(item.Editor, f.Workspace.ActiveEditor);
            Assert.IsTrue(item.Editor.IsDirty);
        }
    }

    [TestMethod]
    [DataRow(GraphResourceKind.Session)]
    [DataRow(GraphResourceKind.Task)]
    public void FailedSaveRetainsExactEditableDraftAndRetrySucceeds(GraphResourceKind kind)
    {
        using var f = new Fixture(kind);
        var item = f.Create();
        f.Edit(item);
        f.Dialogs.CloseChoice = UnsavedChangesChoice.Save;
        var path = f.Repository.GetPath(item.Id);
        var before = File.ReadAllBytes(path);
        using (var held = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None))
            Assert.IsFalse(f.Shell.TryClose());
        CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
        Assert.AreSame(item.Editor, f.Workspace.ActiveEditor);
        Assert.IsTrue(item.Editor.IsDirty);
        Assert.HasCount(2, item.Editor.Host.Graph.Nodes);
        Assert.IsTrue(f.Shell.TryClose(), f.Shell.StatusMessage);
        Assert.HasCount(2, f.Repository.Load(item.Id).Graph!.Nodes);
    }

    [TestMethod]
    [DataRow(GraphResourceKind.Session)]
    [DataRow(GraphResourceKind.Task)]
    public void DeleteRemovesOwnedFileAndMembership(GraphResourceKind kind)
    {
        using var f = new Fixture(kind);
        var item = f.Create();
        Assert.IsTrue(f.Workspace.RequestDelete(item));
        Assert.IsFalse(File.Exists(f.Repository.GetPath(item.Id)));
        Assert.IsEmpty(f.Items);
        var owned = f.Store.Memberships.Load(Fixture.Owner).OwnedResources;
        Assert.IsEmpty(kind == GraphResourceKind.Session ? owned.Sessions : owned.Tasks);
    }

    private sealed class Fixture : IDisposable
    {
        public const string Owner = "ST-2345-6789-ABCD-EFGH";
        public string Root { get; } = Path.Combine(AppContext.BaseDirectory, ".current-gate-ef", Guid.NewGuid().ToString("N"));
        public CanonicalProjectGraphStore Store { get; }
        public GraphResourceRepository Repository { get; }
        public ShellViewModel Shell { get; }
        public CanonicalStoryWorkspaceViewModel Workspace { get; }
        public Dialogs Dialogs { get; } = new();
        public CanonicalStoryFolderKind Folder { get; }
        public GraphScope Scope { get; }
        public IEnumerable<CanonicalStoryGraphItem> Items => Workspace.SessionItems.Concat(Workspace.TaskItems);
        public Fixture(GraphResourceKind kind)
        {
            new ProjectService().CreateProject(Root, "current_gate", "Current Gate");
            Store = new(Root);
            new CanonicalStoryLifecycleService(Store).Create(Owner, "Owner");
            Repository = kind == GraphResourceKind.Session ? Store.Sessions : Store.Tasks;
            Folder = kind == GraphResourceKind.Session ? CanonicalStoryFolderKind.Sessions : CanonicalStoryFolderKind.Tasks;
            Scope = kind == GraphResourceKind.Session ? GraphScope.Session : GraphScope.Task;
            Shell = new(new ProjectService(), new Picker(Root), projectWorkspaceDialogs: Dialogs, canonicalStoryResourceDialogs: Dialogs);
            Shell.OpenProjectCommand.Execute(null);
            Shell.OpenStory(Shell.ProjectHome.Stories.Single());
            Workspace = Shell.CanonicalStoryWorkspace!;
        }
        public CanonicalStoryGraphItem Create()
        {
            var previous = Items.Select(item => item.Id).ToHashSet();
            Assert.IsTrue(Workspace.RequestCreate(Folder));
            return Items.Single(item => !previous.Contains(item.Id));
        }
        public void Edit(CanonicalStoryGraphItem item)
        {
            Assert.IsTrue(Workspace.OpenGraphResource(item));
            var node = GraphNodeFactory.Create(Scope, "logic_output", "extra");
            node.Properties["port_id"] = System.Text.Json.JsonSerializer.SerializeToElement("extra");
            node.Properties["display_name"] = System.Text.Json.JsonSerializer.SerializeToElement("Extra");
            Assert.IsTrue(item.Editor.Host.AddNode(node));
            Assert.IsTrue(item.Editor.IsDirty);
        }
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
    private sealed class Picker(string root) : IProjectFolderPicker { public string? PickProjectFolder() => root; }
    private sealed class Dialogs : ICanonicalStoryResourceDialogs, IProjectWorkspaceDialogs
    {
        public CanonicalGraphResourceIdentityRequest? CreateResult { get; set; } = new("ignored_identity", "Created");
        public UnsavedChangesChoice CloseChoice { get; set; } = UnsavedChangesChoice.Cancel;
        public int CreateCalls { get; private set; }
        public int CloseCalls { get; private set; }
        public CanonicalGraphResourceIdentityRequest? RequestCreate(GraphResourceKind kind, string suggestedId) { CreateCalls++; return CreateResult; }
        public CanonicalGraphResourceChoice? PickReference(GraphResourceKind kind, IReadOnlyList<GraphResourceInfo> candidates, string storyDisplayName) => null;
        public bool ConfirmRemoveReference(CanonicalGraphResourceChoice resource, string storyDisplayName) => true;
        public bool ConfirmDeleteOwned(CanonicalGraphResourceChoice resource) => true;
        public bool ConfirmAggregateInterfaceRemoval(CanonicalGraphResourceChoice resource, IReadOnlyList<GraphConnection> affectedConnections) => true;
        public void ShowDeleteBlocked(CanonicalGraphResourceChoice resource, IReadOnlyList<string> storyIds) { }
        public ProjectCreationRequest? RequestCreate(string? initialParentDirectory = null) => null;
        public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges() { CloseCalls++; return CloseChoice; }
        public bool ConfirmDeleteStory(string storyId, string displayName, IReadOnlyList<string> resourcesToDelete) => false;
    }
}
