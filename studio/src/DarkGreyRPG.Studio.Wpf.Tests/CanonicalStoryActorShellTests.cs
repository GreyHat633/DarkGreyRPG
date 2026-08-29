using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class CanonicalStoryActorShellTests
{
    [TestMethod]
    public void CreateReferenceAndRemoveReferenceReloadTheCanonicalActorFolder()
    {
        using var project = new CanonicalActorProjectFixture();
        var shared = project.SaveActor("shared_actor", "Shared Actor", "opening");
        var dialogs = new FakeActorDialogs
        {
            CreateResult = new ActorIdentityRequest("owned_actor", "Owned Actor"),
            PickResult = shared,
            RemoveReferenceConfirmed = true,
        };
        var shell = project.OpenShell(dialogs);

        Assert.IsTrue(shell.CanonicalStoryWorkspace!.RequestCreate(CanonicalStoryFolderKind.Actors));

        var created = project.Actors.LoadActor("owned_actor");
        Assert.AreEqual("opening", created.HomeStoryId);
        CollectionAssert.Contains(
            project.Store.Memberships.Load("opening").OwnedResources.Actors,
            "owned_actor");
        Assert.AreEqual("owned_actor", shell.CanonicalStoryWorkspace!.SelectedTreeItem?.Id);
        Assert.IsTrue(shell.CanonicalStoryWorkspace.ActorItems.Single(item => item.Id == "owned_actor").IsOwned);

        Assert.IsTrue(shell.CanonicalStoryWorkspace.RequestReference(CanonicalStoryFolderKind.Actors));

        CollectionAssert.Contains(
            project.Store.Memberships.Load("opening").ReferencedResources.Actors,
            "shared_actor");
        var referenced = shell.CanonicalStoryWorkspace!.ActorItems.Single(item => item.Id == "shared_actor");
        Assert.IsTrue(referenced.IsReferenced);
        Assert.IsTrue(shell.CanonicalStoryWorkspace.RequestDelete(referenced));

        CollectionAssert.DoesNotContain(
            project.Store.Memberships.Load("opening").ReferencedResources.Actors,
            "shared_actor");
        Assert.AreEqual("shared_actor", project.Actors.LoadActor("shared_actor").Id);
        Assert.AreEqual(1, dialogs.RemoveReferenceConfirmationCount);
    }

    [TestMethod]
    public void LegacyStoryBlocksOwnedDeleteAndDirtyGraphBlocksActorCreation()
    {
        using var project = new CanonicalActorProjectFixture();
        project.SaveActor("owned_actor", "Owned Actor", "opening");
        project.Store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "opening",
            new CanonicalStoryMembershipSet { Actors = ["owned_actor"] }));
        project.ProjectService.CreateStory("legacy_other", "Legacy Other");
        project.ProjectService.AddActorReference("legacy_other", "owned_actor");
        var dialogs = new FakeActorDialogs
        {
            CreateResult = new ActorIdentityRequest("must_not_create", "Must Not Create"),
            DeleteConfirmed = true,
        };
        var shell = project.OpenShell(dialogs, "owned_actor");
        Assert.HasCount(1, project.ShellProjectService!.OpenActorDocuments);
        var owned = shell.CanonicalStoryWorkspace!.ActorItems.Single();

        Assert.IsTrue(shell.CanonicalStoryWorkspace.RequestDelete(owned));

        Assert.AreEqual("owned_actor", project.Actors.LoadActor("owned_actor").Id);
        CollectionAssert.Contains(dialogs.LastReferenceStoryIds.ToArray(), "legacy_other");
        Assert.AreEqual(0, dialogs.DeleteConfirmationCount);

        project.ProjectService.RemoveActorReference("legacy_other", "owned_actor");
        Assert.IsEmpty(project.ProjectService.CurrentProject!.Stories
            .LoadStory("legacy_other").ReferencedResources.Actors);
        var remainingBlockers = new CanonicalStoryActorLifecycleService(project.Store)
            .GetDeletionPlan("opening", "owned_actor").Blockers;
        Assert.IsEmpty(
            remainingBlockers,
            string.Join(", ", remainingBlockers.Select(blocker =>
                $"{blocker.Source}/{blocker.MembershipKind}/{blocker.StoryId}")));
        Assert.IsTrue(shell.CanonicalStoryWorkspace.RequestDelete(owned));
        Assert.IsFalse(File.Exists(project.ActorPath("owned_actor")), shell.StatusMessage);
        Assert.IsEmpty(project.ShellProjectService!.OpenActorDocuments);
        CollectionAssert.DoesNotContain(
            project.Store.Memberships.Load("opening").OwnedResources.Actors,
            "owned_actor");

        Assert.IsTrue(shell.CanonicalStoryWorkspace!.StoryEditor.Host.AddNode(
            GraphNodeFactory.Create(GraphScope.StoryFlow, "action", "dirty_action")));
        Assert.IsTrue(shell.CanonicalStoryWorkspace.RequestCreate(CanonicalStoryFolderKind.Actors));
        Assert.AreEqual(0, dialogs.CreateRequestCount);
        Assert.IsFalse(File.Exists(project.ActorPath("must_not_create")));
        StringAssert.Contains(shell.StatusMessage, "请先保存");
    }

    private sealed class CanonicalActorProjectFixture : IDisposable
    {
        public CanonicalActorProjectFixture()
        {
            Root = Path.Combine(
                AppContext.BaseDirectory,
                "temp",
                "canonical-actor-shell-" + Guid.NewGuid().ToString("N"));
            ProjectService = new ProjectService();
            ProjectService.CreateProject(Root, "test_project", "Test Project");
            ProjectService.CreateStory("opening", "Opening");
            Actors = ProjectService.CurrentProject!.Actors;
            Store = new CanonicalProjectGraphStore(Root);
            Store.Stories.Create(new GraphResourceEnvelope(
                GraphResourceKind.Story,
                "opening",
                "Opening",
                new GraphDocument([GraphNodeFactory.Create(GraphScope.StoryFlow, "start", "start")])));
            Store.Memberships.Create(new CanonicalStoryMembershipManifest("opening"));
        }

        public string Root { get; }
        public ProjectService ProjectService { get; }
        public ActorRepository Actors { get; }
        public CanonicalProjectGraphStore Store { get; }
        public ProjectService? ShellProjectService { get; private set; }

        public string ActorPath(string id) => Path.Combine(Root, "actors", id + ".json");

        public ActorResourceInfo SaveActor(string id, string displayName, string homeStoryId)
        {
            var document = Actors.CreateActor(id, displayName);
            document.HomeStoryId = homeStoryId;
            Actors.SaveActor(document);
            return Actors.ListActors().Single(actor => actor.Id == id);
        }

        public ShellViewModel OpenShell(IActorWorkspaceDialogs dialogs, string? cacheActorId = null)
        {
            ShellProjectService = new ProjectService();
            var shell = new ShellViewModel(
                ShellProjectService,
                new FixedProjectFolderPicker(Root),
                actorWorkspaceDialogs: dialogs);
            shell.OpenProjectCommand.Execute(null);
            if (cacheActorId is not null) _ = ShellProjectService.OpenActor(cacheActorId);
            shell.OpenStory(shell.ProjectHome.Stories.Single(story => story.Id == "opening"));
            Assert.IsTrue(shell.HasCanonicalStoryWorkspace);
            return shell;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class FixedProjectFolderPicker(string projectDirectory) : IProjectFolderPicker
    {
        public string? PickProjectFolder() => projectDirectory;
    }

    private sealed class FakeActorDialogs : IActorWorkspaceDialogs
    {
        public ActorIdentityRequest? CreateResult { get; init; }
        public ActorResourceInfo? PickResult { get; init; }
        public bool RemoveReferenceConfirmed { get; init; }
        public bool DeleteConfirmed { get; init; }
        public int CreateRequestCount { get; private set; }
        public int RemoveReferenceConfirmationCount { get; private set; }
        public int DeleteConfirmationCount { get; private set; }
        public IReadOnlyList<string> LastReferenceStoryIds { get; private set; } = [];

        public ActorCreationMode? RequestCreationMode(string storyDisplayName) => ActorCreationMode.Blank;

        public ActorIdentityRequest? RequestCreate(string suggestedId)
        {
            CreateRequestCount++;
            return CreateResult;
        }

        public ActorIdentityRequest? RequestImportIdentity(ActorResourceInfo source, string suggestedId) => null;

        public ActorResourceInfo? PickActor(
            IReadOnlyList<ActorResourceInfo> candidates,
            ActorPickerMode mode,
            string storyDisplayName)
            => PickResult;

        public string? RequestRename(ActorResourceInfo actor, string suggestedId) => null;

        public bool ConfirmDelete(ActorResourceInfo actor)
        {
            DeleteConfirmationCount++;
            return DeleteConfirmed;
        }

        public bool ConfirmRemoveReference(ActorResourceInfo actor, string storyDisplayName)
        {
            RemoveReferenceConfirmationCount++;
            return RemoveReferenceConfirmed;
        }

        public void ShowReferences(ActorResourceInfo actor, IReadOnlyList<ResourceDescriptor> references)
            => LastReferenceStoryIds = references.Select(reference => reference.Id).ToArray();

        public bool ConfirmSaveBeforeSwitch(ActorResourceInfo actor) => false;

        public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges(ActorResourceInfo actor)
            => UnsavedChangesChoice.Cancel;
    }
}
