using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class GateGWpfTests
{
    [TestMethod]
    [DataRow(GraphResourceKind.Session)]
    [DataRow(GraphResourceKind.Task)]
    public void CopyIntoNonemptyStoryIsIndependentAndUndoRedoRestoresMembership(GraphResourceKind kind)
    {
        using var f = new Fixture(kind);
        var before = File.ReadAllBytes(f.Repository.GetPath(f.SourceId));
        var targetBefore = File.ReadAllBytes(f.Store.Memberships.GetPath(Fixture.Target));
        f.Shell.ProjectHome.SelectedStory = f.Shell.ProjectHome.Stories.Single(story => story.Id == Fixture.Owner);
        f.Dialogs.CopyTarget = new(ProjectResourceType.Story, Fixture.Target, "Target", f.Store.Stories.GetPath(Fixture.Target));
        f.Shell.CopyStoryContentCommand.Execute(null);
        var workspace = f.Shell.CanonicalStoryWorkspace!;
        var items = workspace.SessionItems.Concat(workspace.TaskItems).ToArray();
        Assert.HasCount(2, items);
        var copy = items.Single(item => item.Id != f.ExistingId);
        StringAssert.StartsWith(copy.Id, Fixture.Target + "~");
        Assert.AreNotEqual(f.SourceId, copy.Id);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(f.Repository.GetPath(f.SourceId)));
        Assert.IsTrue(workspace.StoryEditor.Host.Undo());
        CollectionAssert.AreEqual(targetBefore, File.ReadAllBytes(f.Store.Memberships.GetPath(Fixture.Target)));
        Assert.IsFalse(File.Exists(f.Repository.GetPath(copy.Id)));
        Assert.IsTrue(workspace.StoryEditor.Host.Redo());
        Assert.IsTrue(File.Exists(f.Repository.GetPath(copy.Id)));
        copy = workspace.SessionItems.Concat(workspace.TaskItems).Single(item => item.Id == copy.Id);
        f.Dialogs.Rename = "Independent Copy";
        Assert.IsTrue(workspace.RequestRename(copy));
        Assert.AreEqual("Independent Copy", f.Repository.Load(copy.Id).DisplayName);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(f.Repository.GetPath(f.SourceId)));
        Assert.AreEqual(ResourcePickerMode.CopyIntoStory, f.Dialogs.LastCopyMode);
    }

    [TestMethod]
    [DataRow(GraphResourceKind.Session)]
    [DataRow(GraphResourceKind.Task)]
    public void CancelCopyLeavesBothStoriesAndResourcesUnchanged(GraphResourceKind kind)
    {
        using var f = new Fixture(kind);
        var before = Directory.GetFiles(f.Root, "*.json", SearchOption.AllDirectories)
            .ToDictionary(path => path, File.ReadAllBytes);
        f.Shell.ProjectHome.SelectedStory = f.Shell.ProjectHome.Stories.Single(story => story.Id == Fixture.Owner);
        f.Shell.CopyStoryContentCommand.Execute(null);
        foreach (var entry in before) CollectionAssert.AreEqual(entry.Value, File.ReadAllBytes(entry.Key));
        CollectionAssert.AreEquivalent(before.Keys.ToArray(), Directory.GetFiles(f.Root, "*.json", SearchOption.AllDirectories));
        Assert.IsNull(f.Shell.CanonicalStoryWorkspace);
    }

    [TestMethod]
    [DataRow(GraphResourceKind.Session)]
    [DataRow(GraphResourceKind.Task)]
    public void TypedReferenceFiltersMembershipAndRemovalPreservesSource(GraphResourceKind kind)
    {
        using var f = new Fixture(kind);
        var before = File.ReadAllBytes(f.Repository.GetPath(f.SourceId));
        f.OpenTarget();
        f.Dialogs.Reference = new(kind, f.SourceId, "Source");
        Assert.IsTrue(f.Shell.CanonicalStoryWorkspace!.RequestReference(f.Folder));
        Assert.IsTrue(f.Dialogs.Candidates.All(candidate => candidate.ResourceKind == kind));
        Assert.IsFalse(f.Dialogs.Candidates.Any(candidate => candidate.Id == f.ExistingId));
        var workspace = f.Shell.CanonicalStoryWorkspace;
        var reference = workspace.SessionItems.Concat(workspace.TaskItems).Single(item => item.Id == f.SourceId);
        Assert.IsTrue(reference.IsReferenced);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(f.Repository.GetPath(f.SourceId)));
        f.Dialogs.Reference = null;
        Assert.IsTrue(workspace.RequestReference(f.Folder));
        Assert.IsFalse(f.Dialogs.Candidates.Any(candidate => candidate.Id == f.SourceId));
        Assert.IsTrue(workspace.RequestDelete(reference));
        Assert.IsTrue(File.Exists(f.Repository.GetPath(f.SourceId)));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(f.Repository.GetPath(f.SourceId)));
        Assert.IsFalse(workspace.SessionItems.Concat(workspace.TaskItems).Any(item => item.Id == f.SourceId));
    }

    [TestMethod]
    [DataRow(GraphResourceKind.Session)]
    [DataRow(GraphResourceKind.Task)]
    public void ReferencedResourceMetadataRemainsReadOnlyUntilImported(GraphResourceKind kind)
    {
        using var f = new Fixture(kind);
        f.OpenTarget();
        f.Dialogs.Reference = new(kind, f.SourceId, "Source");
        f.Shell.CanonicalStoryWorkspace!.RequestReference(f.Folder);
        var reference = f.Shell.CanonicalStoryWorkspace.SessionItems.Concat(f.Shell.CanonicalStoryWorkspace.TaskItems).Single(item => item.Id == f.SourceId);
        f.Dialogs.Rename = "Shared Edit";
        Assert.IsTrue(reference.IsReadOnly);
        Assert.IsFalse(f.Shell.CanonicalStoryWorkspace.RequestRename(reference));
        Assert.AreEqual("Source", f.Repository.Load(f.SourceId).DisplayName);
        f.Shell.OpenStory(f.Shell.ProjectHome.Stories.Single(story => story.Id == Fixture.Owner));
        Assert.AreEqual("Source", f.Shell.CanonicalStoryWorkspace!.SessionItems.Concat(f.Shell.CanonicalStoryWorkspace.TaskItems).Single().DisplayName);
        Assert.HasCount(2, f.Repository.List());
    }

    private sealed class Fixture : IDisposable
    {
        public const string Owner = "ST-2345-6789-ABCD-EFGH", Target = "ST-JKLM-NPQR-STUV-WXYZ";
        public string Root { get; } = Path.Combine(AppContext.BaseDirectory, ".current-gate-g", Guid.NewGuid().ToString("N"));
        public CanonicalProjectGraphStore Store { get; }
        public GraphResourceRepository Repository { get; }
        public ShellViewModel Shell { get; }
        public Dialogs Dialogs { get; } = new();
        public CanonicalStoryFolderKind Folder { get; }
        public string SourceId { get; }
        public string ExistingId { get; }
        public Fixture(GraphResourceKind kind)
        {
            new ProjectService().CreateProject(Root, "reference_gate", "Reference Gate");
            Store = new(Root);
            var stories = new CanonicalStoryLifecycleService(Store);
            stories.Create(Owner, "Owner"); stories.Create(Target, "Target");
            Repository = kind == GraphResourceKind.Session ? Store.Sessions : Store.Tasks;
            Folder = kind == GraphResourceKind.Session ? CanonicalStoryFolderKind.Sessions : CanonicalStoryFolderKind.Tasks;
            var suffix = kind == GraphResourceKind.Session ? "~session~" : "~task~";
            SourceId = Owner + suffix + "source"; ExistingId = Target + suffix + "existing";
            var resources = new CanonicalStoryResourceLifecycleService(Store);
            resources.CreateOwned(Owner, kind, SourceId, "Source");
            resources.CreateOwned(Target, kind, ExistingId, "Existing");
            Shell = new(new ProjectService(), new Picker(Root), resourceWorkspaceDialogs: Dialogs, canonicalStoryResourceDialogs: Dialogs);
            Shell.OpenProjectCommand.Execute(null);
        }
        public void OpenTarget() => Shell.OpenStory(Shell.ProjectHome.Stories.Single(story => story.Id == Target));
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
    private sealed class Picker(string root) : IProjectFolderPicker { public string? PickProjectFolder() => root; }
    private sealed class Dialogs : ICanonicalStoryResourceDialogs, IResourceWorkspaceDialogs
    {
        public ResourceDescriptor? CopyTarget { get; set; }
        public ResourcePickerMode? LastCopyMode { get; private set; }
        public CanonicalGraphResourceChoice? Reference { get; set; }
        public IReadOnlyList<GraphResourceInfo> Candidates { get; private set; } = [];
        public string? Rename { get; set; }
        public string? RequestDisplayName(string label, string id, string current) => Rename;
        public CanonicalGraphResourceIdentityRequest? RequestCreate(GraphResourceKind kind, string suggestedId) => null;
        public CanonicalGraphResourceChoice? PickReference(GraphResourceKind kind, IReadOnlyList<GraphResourceInfo> candidates, string storyDisplayName) { Candidates = candidates; return Reference; }
        public bool ConfirmRemoveReference(CanonicalGraphResourceChoice resource, string storyDisplayName) => true;
        public bool ConfirmDeleteOwned(CanonicalGraphResourceChoice resource) => true;
        public bool ConfirmAggregateInterfaceRemoval(CanonicalGraphResourceChoice resource, IReadOnlyList<GraphConnection> affectedConnections) => true;
        public void ShowDeleteBlocked(CanonicalGraphResourceChoice resource, IReadOnlyList<string> storyIds) { }
        public ResourceCreationMode? RequestCreationMode(ProjectResourceType type, string name) => null;
        public ResourceIdentityRequest? RequestCreate(ProjectResourceType type, string suggestedId) => null;
        public ResourceIdentityRequest? RequestImportIdentity(ProjectResourceType type, ResourceDescriptor source, string suggestedId) => null;
        public ResourceDescriptor? PickResource(ProjectResourceType type, IReadOnlyList<ResourceDescriptor> candidates, ResourcePickerMode mode, string name) { LastCopyMode = mode; return CopyTarget; }
        public bool ConfirmDelete(ResourceDescriptor resource) => false;
        public bool ConfirmDiscardDraft(ResourceDescriptor resource) => false;
        public bool ConfirmRemoveReference(ResourceDescriptor resource, string storyDisplayName) => false;
        public void ShowReferences(ResourceDescriptor resource, IReadOnlyList<ResourceDescriptor> references) { }
        public bool ConfirmSaveBeforeSwitch(ResourceDescriptor resource) => false;
        public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges(ResourceDescriptor resource) => UnsavedChangesChoice.Cancel;
    }
}
