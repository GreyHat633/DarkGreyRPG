using System.Text.Json;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class CanonicalStoryLifecycleShellTests
{
    [TestMethod]
    public void DeleteCanonicalStoryRemovesOwnedResourcesAndPreservesReferencedOwner()
    {
        using var project = new LifecycleProjectFixture();
        project.CreateCanonicalStory("ST-2345-6789-ABCD-EFGH", "Canonical Shared");
        new CanonicalStoryActorLifecycleService(project.Store, project.Session.Actors)
            .CreateOwned("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~actor~owned_actor", "Owned Actor");
        var resources = new CanonicalStoryResourceLifecycleService(project.Store);
        resources.CreateOwnedSession("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~session~owned_session", "Owned Session");
        resources.CreateOwnedTask("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~task~owned_task", "Owned Task");
        project.CreateCanonicalStory("ST-JKLM-NPQR-STUV-WXYZ", "Other");
        new CanonicalStoryActorLifecycleService(project.Store, project.Session.Actors)
            .CreateOwned("ST-JKLM-NPQR-STUV-WXYZ", "ST-JKLM-NPQR-STUV-WXYZ~actor~referenced_actor", "Referenced Actor");
        var membership = project.Store.Memberships.Load("ST-2345-6789-ABCD-EFGH");
        project.Store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "ST-2345-6789-ABCD-EFGH",
            membership.OwnedResources,
            new CanonicalStoryMembershipSet { Actors = ["ST-JKLM-NPQR-STUV-WXYZ~actor~referenced_actor"] }));

        var dialogs = new FakeProjectWorkspaceDialogs { CanonicalDeleteConfirmed = true };
        var shell = project.OpenShell(dialogs);
        shell.ProjectHome.SelectedStory = shell.ProjectHome.Stories.Single(story => story.Id == "ST-2345-6789-ABCD-EFGH");

        Assert.IsTrue(shell.DeleteSelectedStoryCommand.CanExecute(null));
        shell.DeleteSelectedStoryCommand.Execute(null);

        Assert.AreEqual(1, dialogs.CanonicalDeleteConfirmationCount);
        CollectionAssert.AreEquivalent(
            new[] { "角色：ST-2345-6789-ABCD-EFGH~actor~owned_actor", "会话：ST-2345-6789-ABCD-EFGH~session~owned_session", "任务：ST-2345-6789-ABCD-EFGH~task~owned_task" },
            dialogs.LastResources.ToArray());
        Assert.IsFalse(File.Exists(project.Store.Stories.GetPath("ST-2345-6789-ABCD-EFGH")));
        Assert.IsFalse(File.Exists(project.Store.Memberships.GetPath("ST-2345-6789-ABCD-EFGH")));
        Assert.IsFalse(File.Exists(project.ActorPath("ST-2345-6789-ABCD-EFGH~actor~owned_actor")));
        Assert.IsFalse(File.Exists(project.Store.Sessions.GetPath("ST-2345-6789-ABCD-EFGH~session~owned_session")));
        Assert.IsFalse(File.Exists(project.Store.Tasks.GetPath("ST-2345-6789-ABCD-EFGH~task~owned_task")));
        Assert.IsTrue(File.Exists(project.ActorPath("ST-JKLM-NPQR-STUV-WXYZ~actor~referenced_actor")));
        Assert.IsTrue(File.Exists(project.Store.Stories.GetPath("ST-JKLM-NPQR-STUV-WXYZ")));
    }

    [TestMethod]
    public void RejectedStandaloneTransitionLeavesNormalDeletionAvailable()
    {
        using var project = new LifecycleProjectFixture();
        project.CreateCanonicalStory("ST-JKLM-NPQR-STUV-WXYZ", "Target");
        project.CreateCanonicalStory("ST-2345-6789-ABCD-EFGH", "Source");
        var before = File.ReadAllBytes(project.Store.Stories.GetPath("ST-2345-6789-ABCD-EFGH"));
        Assert.ThrowsExactly<GraphResourceRepositoryException>(() => project.Store.Stories.Replace(
            new GraphResourceEnvelope(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", "Source",
                new GraphDocument([new GraphNode("old", "enter_story", "Old")]))));
        var dialogs = new FakeProjectWorkspaceDialogs { CanonicalDeleteConfirmed = true };
        var shell = project.OpenShell(dialogs);
        shell.ProjectHome.SelectedStory = shell.ProjectHome.Stories.Single(story => story.Id == "ST-JKLM-NPQR-STUV-WXYZ");
        shell.DeleteSelectedStoryCommand.Execute(null);
        Assert.AreEqual(1, dialogs.CanonicalDeleteConfirmationCount);
        Assert.IsFalse(File.Exists(project.Store.Stories.GetPath("ST-JKLM-NPQR-STUV-WXYZ")));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(project.Store.Stories.GetPath("ST-2345-6789-ABCD-EFGH")));
    }

    [TestMethod]
    public void ConfirmedDeletionDiscardsDirtyOwnedActorWithoutRequiringSave()
    {
        using var project = new LifecycleProjectFixture();
        project.CreateCanonicalStory("ST-2345-6789-ABCD-EFGH", "Opening");
        new CanonicalStoryActorLifecycleService(project.Store, project.Session.Actors)
            .CreateOwned("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~actor~teacher", "Teacher");

        var dialogs = new FakeProjectWorkspaceDialogs { CanonicalDeleteConfirmed = true };
        var shell = project.OpenShell(dialogs);
        shell.OpenStory(shell.ProjectHome.Stories.Single());
        shell.CanonicalStoryWorkspace!.SelectTreeItem(shell.CanonicalStoryWorkspace.ActorItems.Single());
        shell.CanonicalStoryWorkspace.InspectorPortraitEditor!.DisplayName = "unsaved";
        shell.ProjectHome.SelectedStory = shell.ProjectHome.Stories.Single(story => story.Id == "ST-2345-6789-ABCD-EFGH");

        shell.DeleteSelectedStoryCommand.Execute(null);

        Assert.AreEqual(1, dialogs.CanonicalDeleteConfirmationCount);
        Assert.IsFalse(File.Exists(project.Store.Stories.GetPath("ST-2345-6789-ABCD-EFGH")));
        Assert.IsFalse(File.Exists(project.Store.Memberships.GetPath("ST-2345-6789-ABCD-EFGH")));
        Assert.IsFalse(File.Exists(project.ActorPath("ST-2345-6789-ABCD-EFGH~actor~teacher")));
        Assert.AreEqual(OutputKind.Success, shell.Output.Entries.Last().Kind);
    }

    [TestMethod]
    public void SwitchingStoriesRetainsAllGraphsAndNormalSaveSavesTheWholeProject()
    {
        using var project = new LifecycleProjectFixture();
        project.CreateCanonicalStory("ST-2345-6789-ABCD-EFGH", "First");
        project.CreateCanonicalStory("ST-JKLM-NPQR-STUV-WXYZ", "Second");
        var resources = new CanonicalStoryResourceLifecycleService(project.Store);
        resources.CreateOwnedSession("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~session~session", "Session");
        resources.CreateOwnedTask("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~task~task", "Task");
        var shell = project.OpenShell(new FakeProjectWorkspaceDialogs());
        shell.OpenStory(shell.ProjectHome.Stories.Single(story => story.Id == "ST-2345-6789-ABCD-EFGH"));
        var first = shell.CanonicalStoryWorkspace!;
        foreach (var editor in first.SessionEditors.Concat(first.TaskEditors).Append(first.StoryEditor))
            editor.Host.SetNodePosition(editor.Document.Graph!.Nodes.First().Id, 321, 123);
        Assert.IsTrue(first.HasDirtyEditors);
        shell.ShowProjectHomeCommand.Execute(null);
        shell.OpenStory(shell.ProjectHome.Stories.Single(story => story.Id == "ST-JKLM-NPQR-STUV-WXYZ"));
        Assert.AreEqual("ST-JKLM-NPQR-STUV-WXYZ", shell.CanonicalStoryWorkspace!.StoryEditor.Id);
        Assert.IsTrue(shell.SaveCurrentResourceCommand.CanExecute(null));
        shell.SaveCurrentResourceCommand.Execute(null);
        Assert.IsFalse(first.HasDirtyEditors, shell.StatusMessage);
        var layouts = new CanonicalGraphLayoutStore(project.Root);
        foreach (var editor in first.SessionEditors.Concat(first.TaskEditors).Append(first.StoryEditor))
            Assert.AreEqual(321d, layouts.Load(editor.ResourceKind, editor.Id)[editor.Document.Graph!.Nodes.First().Id].X);
        shell.OpenStory(shell.ProjectHome.Stories.Single(story => story.Id == "ST-2345-6789-ABCD-EFGH"));
        Assert.IsFalse(shell.CanonicalStoryWorkspace!.HasDirtyEditors);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void DeletingDirtyStoryPreservesOtherDraftsAndSaveCannotResurrectDeletedStory(bool deleteRetained)
    {
        using var project = new LifecycleProjectFixture();
        project.CreateCanonicalStory("ST-2345-6789-ABCD-EFGH", "First");
        project.CreateCanonicalStory("ST-JKLM-NPQR-STUV-WXYZ", "Second");
        var shell = project.OpenShell(new FakeProjectWorkspaceDialogs { CanonicalDeleteConfirmed = true });
        shell.OpenStory(shell.ProjectHome.Stories.Single(story => story.Id == "ST-2345-6789-ABCD-EFGH"));
        var first = shell.CanonicalStoryWorkspace!;
        first.StoryEditor.Host.SetNodePosition(first.StoryEditor.Document.Graph!.Nodes.First().Id, 321, 123);
        shell.OpenStory(shell.ProjectHome.Stories.Single(story => story.Id == "ST-JKLM-NPQR-STUV-WXYZ"));
        var second = shell.CanonicalStoryWorkspace!;
        second.StoryEditor.Host.SetNodePosition(second.StoryEditor.Document.Graph!.Nodes.First().Id, 222, 111);
        var deletedId = deleteRetained ? "ST-2345-6789-ABCD-EFGH" : "ST-JKLM-NPQR-STUV-WXYZ";
        var survivor = deleteRetained ? second : first;
        shell.ProjectHome.SelectedStory = shell.ProjectHome.Stories.Single(story => story.Id == deletedId);
        shell.DeleteSelectedStoryCommand.Execute(null);
        Assert.IsTrue(survivor.HasDirtyEditors);
        shell.SaveCurrentResourceCommand.Execute(null);
        Assert.IsFalse(survivor.HasDirtyEditors, shell.StatusMessage);
        Assert.IsFalse(File.Exists(project.Store.Stories.GetPath(deletedId)));
    }

    [TestMethod]
    public void CancellingDirtyStoryDeletionPreservesDraftAndDisk()
    {
        using var project = new LifecycleProjectFixture();
        project.CreateCanonicalStory("ST-2345-6789-ABCD-EFGH", "Story");
        var shell = project.OpenShell(new FakeProjectWorkspaceDialogs { CanonicalDeleteConfirmed = false });
        shell.OpenStory(shell.ProjectHome.Stories.Single(story => story.Id == "ST-2345-6789-ABCD-EFGH"));
        var workspace = shell.CanonicalStoryWorkspace!;
        workspace.StoryEditor.Host.SetNodePosition(workspace.StoryEditor.Document.Graph!.Nodes.First().Id, 321, 123);
        shell.DeleteSelectedStoryCommand.Execute(null);
        Assert.AreSame(workspace, shell.CanonicalStoryWorkspace);
        Assert.IsTrue(workspace.HasDirtyEditors);
        Assert.IsTrue(File.Exists(project.Store.Stories.GetPath("ST-2345-6789-ABCD-EFGH")));
        shell.SaveCurrentResourceCommand.Execute(null);
        Assert.IsFalse(workspace.HasDirtyEditors);
    }

    private sealed class LifecycleProjectFixture : IDisposable
    {
        private readonly ProjectService _setup = new();

        public LifecycleProjectFixture()
        {
            Root = Path.Combine(
                AppContext.BaseDirectory,
                "temp",
                "canonical-story-lifecycle-shell-" + Guid.NewGuid().ToString("N"));
            Session = _setup.CreateProject(Root, "lifecycle", "Lifecycle");
            Store = new CanonicalProjectGraphStore(Root);
        }

        public string Root { get; }
        public ProjectSession Session { get; }
        public CanonicalProjectGraphStore Store { get; }
        public string ActorPath(string id) => Session.Actors.GetActorPath(id);

        public void CreateCanonicalStory(string id, string displayName)
            => new CanonicalStoryLifecycleService(Store, Session.Actors)
                .Create(id, displayName);

        public ShellViewModel OpenShell(IProjectWorkspaceDialogs dialogs)
        {
            _setup.CloseProject(discardUnsavedChanges: true);
            var shell = new ShellViewModel(
                new ProjectService(),
                new FixedProjectFolderPicker(Root),
                projectWorkspaceDialogs: dialogs);
            shell.OpenProjectCommand.Execute(null);
            return shell;
        }

        public void Dispose()
        {
            _setup.CloseProject(discardUnsavedChanges: true);
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class FixedProjectFolderPicker(string projectDirectory) : IProjectFolderPicker
    {
        public string? PickProjectFolder() => projectDirectory;
    }

    private sealed class FakeProjectWorkspaceDialogs : IProjectWorkspaceDialogs
    {
        public bool CanonicalDeleteConfirmed { get; init; }
        public int CanonicalDeleteConfirmationCount { get; private set; }
        public IReadOnlyList<string> LastResources { get; private set; } = [];

        public ProjectCreationRequest? RequestCreate(string? initialParentDirectory = null) => null;
        public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges() => UnsavedChangesChoice.Cancel;
        public bool ConfirmDeleteStory(string storyId, string displayName, IReadOnlyList<string> resourcesToDelete)
            => false;

        public bool ConfirmDeleteCanonicalStory(
            string storyId,
            string displayName,
            IReadOnlyList<string> resourcesToDelete)
        {
            CanonicalDeleteConfirmationCount++;
            LastResources = resourcesToDelete;
            return CanonicalDeleteConfirmed;
        }
    }
}
