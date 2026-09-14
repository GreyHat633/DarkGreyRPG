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
    public void DeleteCanonicalStoryRemovesOwnedResourcesAndPreservesReferencesAndLegacyMirror()
    {
        using var project = new LifecycleProjectFixture();
        project.CreateCanonicalStory("shared", "Canonical Shared");
        new CanonicalStoryActorLifecycleService(project.Store, project.Session.Actors, project.Session.Stories)
            .CreateOwned("shared", "owned_actor", "Owned Actor");
        var resources = new CanonicalStoryResourceLifecycleService(project.Store);
        resources.CreateOwnedSession("shared", "owned_session", "Owned Session");
        resources.CreateOwnedTask("shared", "owned_task", "Owned Task");
        var referenced = project.Session.Actors.CreateActor("referenced_actor", "Referenced Actor");
        project.Session.Actors.SaveActor(referenced);
        var membership = project.Store.Memberships.Load("shared");
        project.Store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "shared",
            membership.OwnedResources,
            new CanonicalStoryMembershipSet { Actors = ["referenced_actor"] }));
        project.Session.Stories.CreateStory("shared", "Legacy Shared");

        var dialogs = new FakeProjectWorkspaceDialogs { CanonicalDeleteConfirmed = true };
        var shell = project.OpenShell(dialogs);
        shell.ProjectHome.SelectedStory = shell.ProjectHome.Stories.Single(story => story.Id == "shared");

        Assert.IsTrue(shell.DeleteSelectedStoryCommand.CanExecute(null));
        shell.DeleteSelectedStoryCommand.Execute(null);

        Assert.AreEqual(1, dialogs.CanonicalDeleteConfirmationCount);
        CollectionAssert.AreEquivalent(
            new[] { "角色：owned_actor", "会话：owned_session", "任务：owned_task" },
            dialogs.LastResources.ToArray());
        Assert.IsFalse(File.Exists(project.Store.Stories.GetPath("shared")));
        Assert.IsFalse(File.Exists(project.Store.Memberships.GetPath("shared")));
        Assert.IsFalse(File.Exists(project.ActorPath("owned_actor")));
        Assert.IsFalse(File.Exists(project.Store.Sessions.GetPath("owned_session")));
        Assert.IsFalse(File.Exists(project.Store.Tasks.GetPath("owned_task")));
        Assert.IsTrue(File.Exists(project.ActorPath("referenced_actor")));
        Assert.IsTrue(File.Exists(Path.Combine(project.Root, "stories", "shared.json")));
        Assert.IsTrue(shell.ProjectHome.SelectedStory?.CanDeleteLegacyStory);
        Assert.IsFalse(shell.ProjectHome.SelectedStory?.HasCanonicalStory);
    }

    [TestMethod]
    public void RejectedStandaloneTransitionLeavesNormalDeletionAvailable()
    {
        using var project = new LifecycleProjectFixture();
        project.CreateCanonicalStory("target", "Target");
        project.CreateCanonicalStory("source", "Source");
        var before = File.ReadAllBytes(project.Store.Stories.GetPath("source"));
        Assert.ThrowsExactly<GraphResourceRepositoryException>(() => project.Store.Stories.Replace(
            new GraphResourceEnvelope(GraphResourceKind.Story, "source", "Source",
                new GraphDocument([new GraphNode("old", "enter_story", "Old")]))));
        var dialogs = new FakeProjectWorkspaceDialogs { CanonicalDeleteConfirmed = true };
        var shell = project.OpenShell(dialogs);
        shell.ProjectHome.SelectedStory = shell.ProjectHome.Stories.Single(story => story.Id == "target");
        shell.DeleteSelectedStoryCommand.Execute(null);
        Assert.AreEqual(1, dialogs.CanonicalDeleteConfirmationCount);
        Assert.IsFalse(File.Exists(project.Store.Stories.GetPath("target")));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(project.Store.Stories.GetPath("source")));
    }

    [TestMethod]
    public void DirtyOwnedActorCacheBlocksDeleteBeforeAnyDiskMutation()
    {
        using var project = new LifecycleProjectFixture();
        project.CreateCanonicalStory("opening", "Opening");
        new CanonicalStoryActorLifecycleService(project.Store, project.Session.Actors, project.Session.Stories)
            .CreateOwned("opening", "teacher", "Teacher");
        var storyBytes = File.ReadAllBytes(project.Store.Stories.GetPath("opening"));
        var membershipBytes = File.ReadAllBytes(project.Store.Memberships.GetPath("opening"));
        var actorBytes = File.ReadAllBytes(project.ActorPath("teacher"));

        var dialogs = new FakeProjectWorkspaceDialogs { CanonicalDeleteConfirmed = true };
        var shell = project.OpenShell(dialogs);
        shell.SelectedActor = shell.Actors.Single(actor => actor.Id == "teacher");
        shell.CurrentActor!.Notes = "unsaved";
        shell.ProjectHome.SelectedStory = shell.ProjectHome.Stories.Single(story => story.Id == "opening");

        shell.DeleteSelectedStoryCommand.Execute(null);

        Assert.AreEqual(1, dialogs.CanonicalDeleteConfirmationCount);
        CollectionAssert.AreEqual(storyBytes, File.ReadAllBytes(project.Store.Stories.GetPath("opening")));
        CollectionAssert.AreEqual(membershipBytes, File.ReadAllBytes(project.Store.Memberships.GetPath("opening")));
        CollectionAssert.AreEqual(actorBytes, File.ReadAllBytes(project.ActorPath("teacher")));
        Assert.AreEqual(OutputKind.Error, shell.Output.Entries.Last().Kind);
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
        public string ActorPath(string id) => Path.Combine(Root, "actors", id + ".json");

        public void CreateCanonicalStory(string id, string displayName)
            => new CanonicalStoryLifecycleService(Store, Session.Actors, Session.Stories)
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
