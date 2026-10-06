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
        var shared = project.SaveActor("ST-2345-6789-ABCD-EFGH~actor~shared_actor", "Shared Actor", "ST-2345-6789-ABCD-EFGH");
        var dialogs = new FakeActorDialogs
        {
            CreateResult = new ActorIdentityRequest("ST-2345-6789-ABCD-EFGH~actor~owned_actor", "Owned Actor"),
            PickResult = shared,
            RemoveReferenceConfirmed = true,
        };
        var shell = project.OpenShell(dialogs);

        Assert.IsTrue(shell.CanonicalStoryWorkspace!.RequestCreate(CanonicalStoryFolderKind.Actors));

        var createdId = shell.CanonicalStoryWorkspace!.ActorItems.Single(item => item.IsOwned).Id;
        var created = project.Actors.LoadActor(createdId);
        Assert.AreEqual("ST-2345-6789-ABCD-EFGH", created.HomeStoryId);
        CollectionAssert.Contains(project.Store.Memberships.Load("ST-2345-6789-ABCD-EFGH").OwnedResources.Actors, createdId);
        Assert.AreEqual(createdId, shell.CanonicalStoryWorkspace.SelectedTreeItem?.Id);

        Assert.IsTrue(shell.CanonicalStoryWorkspace.RequestReference(CanonicalStoryFolderKind.Actors));

        CollectionAssert.Contains(
            project.Store.Memberships.Load("ST-2345-6789-ABCD-EFGH").ReferencedResources.Actors,
            "ST-2345-6789-ABCD-EFGH~actor~shared_actor");
        var referenced = shell.CanonicalStoryWorkspace!.ActorItems.Single(item => item.Id == "ST-2345-6789-ABCD-EFGH~actor~shared_actor");
        Assert.IsTrue(referenced.IsReferenced);
        Assert.IsTrue(shell.CanonicalStoryWorkspace.RequestDelete(referenced));

        CollectionAssert.DoesNotContain(
            project.Store.Memberships.Load("ST-2345-6789-ABCD-EFGH").ReferencedResources.Actors,
            "ST-2345-6789-ABCD-EFGH~actor~shared_actor");
        Assert.AreEqual("ST-2345-6789-ABCD-EFGH~actor~shared_actor", project.Actors.LoadActor("ST-2345-6789-ABCD-EFGH~actor~shared_actor").Id);
        Assert.AreEqual(0, dialogs.RemoveReferenceConfirmationCount); // Reversible membership removal needs no extra confirmation.
    }

    [TestMethod]
    public void OtherStoryReferenceBlocksOwnedDeleteButDirtyGraphAllowsActorCreation()
    {
        using var project = new CanonicalActorProjectFixture();
        project.SaveActor("ST-2345-6789-ABCD-EFGH~actor~owned_actor", "Owned Actor", "ST-2345-6789-ABCD-EFGH");
        project.Store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "ST-2345-6789-ABCD-EFGH",
            new CanonicalStoryMembershipSet { Actors = ["ST-2345-6789-ABCD-EFGH~actor~owned_actor"] }));
        new CanonicalStoryLifecycleService(project.Store).Create("ST-JKLM-NPQR-STUV-WXYZ", "Other");
        project.ProjectService.AddActorReference("ST-JKLM-NPQR-STUV-WXYZ", "ST-2345-6789-ABCD-EFGH~actor~owned_actor");
        var dialogs = new FakeActorDialogs
        {
            CreateResult = new ActorIdentityRequest("must_not_create", "Must Not Create"),
            DeleteConfirmed = true,
        };
        var shell = project.OpenShell(dialogs, "ST-2345-6789-ABCD-EFGH~actor~owned_actor");
        Assert.HasCount(1, project.ShellProjectService!.OpenActorDocuments);
        var owned = shell.CanonicalStoryWorkspace!.ActorItems.Single();

        Assert.IsTrue(shell.CanonicalStoryWorkspace.RequestDelete(owned));

        Assert.AreEqual("ST-2345-6789-ABCD-EFGH~actor~owned_actor", project.Actors.LoadActor("ST-2345-6789-ABCD-EFGH~actor~owned_actor").Id);
        CollectionAssert.Contains(dialogs.LastReferenceStoryIds.ToArray(), "ST-JKLM-NPQR-STUV-WXYZ");
        Assert.AreEqual(0, dialogs.DeleteConfirmationCount);

        project.ProjectService.RemoveActorReference("ST-JKLM-NPQR-STUV-WXYZ", "ST-2345-6789-ABCD-EFGH~actor~owned_actor");
        Assert.IsEmpty(project.Store.Memberships.Load("ST-JKLM-NPQR-STUV-WXYZ").ReferencedResources.Actors);
        var remainingBlockers = new CanonicalStoryActorLifecycleService(project.Store)
            .GetDeletionPlan("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~actor~owned_actor").Blockers;
        Assert.IsEmpty(
            remainingBlockers,
            string.Join(", ", remainingBlockers.Select(blocker =>
                $"{blocker.Source}/{blocker.MembershipKind}/{blocker.StoryId}")));
        Assert.IsTrue(shell.CanonicalStoryWorkspace.RequestDelete(owned));
        Assert.IsFalse(File.Exists(project.ActorPath("ST-2345-6789-ABCD-EFGH~actor~owned_actor")), shell.StatusMessage);
        Assert.IsEmpty(project.ShellProjectService!.OpenActorDocuments);
        CollectionAssert.DoesNotContain(
            project.Store.Memberships.Load("ST-2345-6789-ABCD-EFGH").OwnedResources.Actors,
            "ST-2345-6789-ABCD-EFGH~actor~owned_actor");

        Assert.IsTrue(shell.CanonicalStoryWorkspace!.StoryEditor.Host.AddNode(
            GraphNodeFactory.Create(GraphScope.StoryFlow, "action", "dirty_action")));
        Assert.IsTrue(shell.CanonicalStoryWorkspace.RequestCreate(CanonicalStoryFolderKind.Actors));
        Assert.AreEqual(1, dialogs.CreateRequestCount);
        Assert.IsTrue(File.Exists(project.ActorPath(shell.CanonicalStoryWorkspace.ActorItems.Single().Id)));
        Assert.IsTrue(shell.CanonicalStoryWorkspace.StoryEditor.IsDirty);
        Assert.IsTrue(shell.CanonicalStoryWorkspace.StoryEditor.Host.Graph.Nodes.Any(node => node.Id == "dirty_action"));
        Assert.IsFalse(shell.StatusMessage.Contains("请先保存", StringComparison.Ordinal));
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
            Actors = ProjectService.CurrentProject!.Actors;
            Store = new CanonicalProjectGraphStore(Root);
            Store.Stories.Create(new GraphResourceEnvelope(
                GraphResourceKind.Story,
                "ST-2345-6789-ABCD-EFGH",
                "Opening",
                new GraphDocument([GraphNodeFactory.Create(GraphScope.StoryFlow, "start", "start")])));
            Store.Memberships.Create(new CanonicalStoryMembershipManifest("ST-2345-6789-ABCD-EFGH"));
        }

        public string Root { get; }
        public ProjectService ProjectService { get; }
        public ActorRepository Actors { get; }
        public CanonicalProjectGraphStore Store { get; }
        public ProjectService? ShellProjectService { get; private set; }

        public string ActorPath(string id) => Actors.GetActorPath(id);

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
            shell.OpenStory(shell.ProjectHome.Stories.Single(story => story.Id == "ST-2345-6789-ABCD-EFGH"));
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
