using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.IO;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class CanonicalStoryResourceShellTests
{
    [TestMethod]
    public void SavingSessionSynchronizesEveryStoryAggregateAndPreservesStableConnections()
    {
        using var project = new CanonicalProjectFixture();
        project.AddOwnedSessionWithAggregate(includeConnection: true);
        var dialogs = new FakeCanonicalDialogs();
        var shell = project.OpenShell(dialogs);
        var workspace = shell.CanonicalStoryWorkspace!;
        var session = workspace.SessionItems.Single();
        Assert.IsTrue(workspace.OpenGraphResource(session));
        Assert.IsTrue(session.Editor.Host.SetNodeProperty("end", "display_name", "Renamed"));

        shell.SaveCurrentResourceCommand.Execute(null);

        Assert.IsFalse(session.Editor.IsDirty);
        Assert.IsTrue(workspace.StoryEditor.IsDirty);
        Assert.AreEqual("Renamed", workspace.StoryEditor.Host.Graph.Nodes
            .Single(node => node.Id == "session-placement").Ports.Single(port => port.Id == "accepted").DisplayName);
        Assert.HasCount(1, workspace.StoryEditor.Host.Graph.Connections);
        Assert.AreEqual("Renamed", project.Store.Sessions.Load("opening_session").Graph!.Nodes
            .Single(node => node.Id == "end").Properties["display_name"].GetString());
        Assert.AreEqual(0, dialogs.AggregateRemovalConfirmationCount);
    }

    [TestMethod]
    public void ReferencedAggregatePortRemovalCanCancelWithoutGraphOrDiskMutation()
    {
        using var project = new CanonicalProjectFixture();
        project.AddOwnedSessionWithAggregate(includeConnection: true, connectLogicBoundary: true);
        var dialogs = new FakeCanonicalDialogs { AggregateRemovalConfirmed = false };
        var shell = project.OpenShell(dialogs);
        var workspace = shell.CanonicalStoryWorkspace!;
        var session = workspace.SessionItems.Single();
        Assert.IsTrue(workspace.OpenGraphResource(session));
        // Public boundary IDs are stable and immutable. Removing a named
        // Logic boundary is the real aggregate-port deletion workflow.
        Assert.IsTrue(session.Editor.Host.RemoveNode("logic"));
        var storyBefore = workspace.StoryEditor.Host.Graph.ToJson();

        shell.SaveCurrentResourceCommand.Execute(null);

        Assert.IsTrue(session.Editor.IsDirty);
        Assert.AreEqual(storyBefore, workspace.StoryEditor.Host.Graph.ToJson());
        Assert.AreEqual("accepted", project.Store.Sessions.Load("opening_session").Graph!.Nodes
            .Single(node => node.Id == "end").Properties["port_id"].GetString());
        Assert.IsNotNull(project.Store.Sessions.Load("opening_session").Graph!.Nodes
            .SingleOrDefault(node => node.Id == "logic"));
        Assert.AreEqual(1, dialogs.AggregateRemovalConfirmationCount);
        Assert.HasCount(1, dialogs.LastAggregateReferences);
    }

    [TestMethod]
    public void ConfirmedAggregatePortRemovalSavesChildAndLeavesStoryDirtyForExplicitSave()
    {
        using var project = new CanonicalProjectFixture();
        project.AddOwnedSessionWithAggregate(includeConnection: true, connectLogicBoundary: true);
        var dialogs = new FakeCanonicalDialogs { AggregateRemovalConfirmed = true };
        var shell = project.OpenShell(dialogs);
        var workspace = shell.CanonicalStoryWorkspace!;
        var session = workspace.SessionItems.Single();
        Assert.IsTrue(workspace.OpenGraphResource(session));
        Assert.IsTrue(session.Editor.Host.RemoveNode("logic"));

        shell.SaveCurrentResourceCommand.Execute(null);

        Assert.IsFalse(session.Editor.IsDirty);
        Assert.IsTrue(workspace.StoryEditor.IsDirty);
        Assert.IsEmpty(workspace.StoryEditor.Host.Graph.Connections);
        Assert.IsNotNull(workspace.StoryEditor.Host.Graph.Nodes.Single(node => node.Id == "session-placement")
            .Ports.SingleOrDefault(port => port.Id == "accepted"));
        Assert.IsNull(workspace.StoryEditor.Host.Graph.Nodes.Single(node => node.Id == "session-placement")
            .Ports.SingleOrDefault(port => port.Id == "known"));
        Assert.AreEqual("accepted", project.Store.Sessions.Load("opening_session").Graph!.Nodes
            .Single(node => node.Id == "end").Properties["port_id"].GetString());
        Assert.IsNull(project.Store.Sessions.Load("opening_session").Graph!.Nodes
            .SingleOrDefault(node => node.Id == "logic"));
        Assert.AreEqual(1, dialogs.AggregateRemovalConfirmationCount);
    }

    [TestMethod]
    public void FailedChildWriteRollsBackOnlyDerivedStorySyncAndRestoresHistory()
    {
        using var project = new CanonicalProjectFixture();
        project.AddOwnedSessionWithAggregate(includeConnection: true, connectLogicBoundary: true);
        var dialogs = new FakeCanonicalDialogs { AggregateRemovalConfirmed = true };
        var shell = project.OpenShell(
            dialogs,
            path => new CanonicalProjectGraphStore(path, new ThrowingWriter()));
        var workspace = shell.CanonicalStoryWorkspace!;
        var session = workspace.SessionItems.Single();
        Assert.IsTrue(workspace.OpenGraphResource(session));
        Assert.IsTrue(workspace.StoryEditor.Host.AddNode(
            GraphNodeFactory.Create(GraphScope.StoryFlow, "action", "prior-edit")));
        Assert.IsTrue(workspace.StoryEditor.Host.AddNode(
            GraphNodeFactory.Create(GraphScope.StoryFlow, "action", "redo-edit")));
        Assert.IsTrue(workspace.StoryEditor.Host.Undo());
        Assert.IsTrue(session.Editor.Host.RemoveNode("logic"));
        var storyBefore = workspace.StoryEditor.Host.Graph.ToJson();
        var undoBefore = workspace.StoryEditor.Host.Session.UndoCount;
        var redoBefore = workspace.StoryEditor.Host.Session.RedoCount;

        shell.SaveCurrentResourceCommand.Execute(null);

        Assert.IsTrue(session.Editor.IsDirty);
        Assert.AreEqual(storyBefore, workspace.StoryEditor.Host.Graph.ToJson());
        Assert.AreEqual(undoBefore, workspace.StoryEditor.Host.Session.UndoCount);
        Assert.AreEqual(redoBefore, workspace.StoryEditor.Host.Session.RedoCount);
        Assert.AreEqual("accepted", project.Store.Sessions.Load("opening_session").Graph!.Nodes
            .Single(node => node.Id == "end").Properties["port_id"].GetString());
        StringAssert.Contains(shell.StatusMessage, "失败");
    }

    [TestMethod]
    public void CreateReferenceAndRemoveReferenceReloadTheCanonicalWorkspace()
    {
        using var project = new CanonicalProjectFixture();
        project.Store.Tasks.Create(Envelope(GraphResourceKind.Task, "shared_task", "Shared Task"));
        var dialogs = new FakeCanonicalDialogs
        {
            CreateResult = new CanonicalGraphResourceIdentityRequest("opening_session", "Opening Session"),
            PickResult = new CanonicalGraphResourceChoice(project.Store.Tasks.List().Single()),
            RemoveReferenceConfirmed = true,
        };
        var shell = project.OpenShell(dialogs);

        var workspace = shell.CanonicalStoryWorkspace!;
        Assert.IsTrue(workspace.RequestCreate(CanonicalStoryFolderKind.Sessions));

        Assert.AreEqual("opening_session", project.Store.Sessions.Load("opening_session").Id);
        CollectionAssert.Contains(
            project.Store.Memberships.Load("opening").OwnedResources.Sessions,
            "opening_session");
        Assert.AreEqual("opening_session", shell.CanonicalStoryWorkspace!.SelectedTreeItem?.Id);
        Assert.IsTrue(shell.CanonicalStoryWorkspace.SessionItems.Single().IsOwned);

        Assert.IsTrue(shell.CanonicalStoryWorkspace.RequestReference(CanonicalStoryFolderKind.Tasks));

        CollectionAssert.Contains(
            project.Store.Memberships.Load("opening").ReferencedResources.Tasks,
            "shared_task");
        var referenced = shell.CanonicalStoryWorkspace!.TaskItems.Single(item => item.Id == "shared_task");
        Assert.IsTrue(referenced.IsReferenced);
        Assert.IsTrue(shell.CanonicalStoryWorkspace.RequestDelete(referenced));

        CollectionAssert.DoesNotContain(
            project.Store.Memberships.Load("opening").ReferencedResources.Tasks,
            "shared_task");
        Assert.AreEqual("shared_task", project.Store.Tasks.Load("shared_task").Id);
        Assert.AreEqual(1, dialogs.RemoveReferenceConfirmationCount);
    }

    [TestMethod]
    public void DirtyStoryAllowsActorItemSessionTaskCreationAndGraphNavigationWithoutLosingDraft()
    {
        using var project = new CanonicalProjectFixture();
        project.Store.Tasks.Create(Envelope(GraphResourceKind.Task, "owned_task", "Owned Task"));
        project.Store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "opening",
            new CanonicalStoryMembershipSet { Tasks = ["owned_task"] }));
        project.Store.Stories.Create(Envelope(GraphResourceKind.Story, "other", "Other"));
        project.Store.Memberships.Create(new CanonicalStoryMembershipManifest(
            "other",
            referencedResources: new CanonicalStoryMembershipSet { Tasks = ["owned_task"] }));
        var dialogs = new FakeCanonicalDialogs
        {
            CreateResult = new CanonicalGraphResourceIdentityRequest("must_not_create", "Must Not Create"),
            DeleteOwnedConfirmed = true,
        };
        var actorDialogs = new FakeActorDialogs
        {
            CreateResult = new ActorIdentityRequest("dirty_actor", "Dirty Actor"),
        };
        var itemDialogs = new FakeItemDialogs
        {
            CreateResult = new ItemIdentityRequest(
                CanonicalStoryItemKind.Individual,
                "dirty_item",
                "Dirty Item",
                []),
        };
        var shell = project.OpenShell(dialogs, actorDialogs: actorDialogs, itemDialogs: itemDialogs);
        var owned = shell.CanonicalStoryWorkspace!.TaskItems.Single();

        Assert.IsTrue(shell.CanonicalStoryWorkspace.RequestDelete(owned));

        Assert.AreEqual("owned_task", project.Store.Tasks.Load("owned_task").Id);
        CollectionAssert.AreEqual(new[] { "other" }, dialogs.LastDeleteBlockers.ToArray());
        Assert.AreEqual(0, dialogs.DeleteOwnedConfirmationCount);

        project.Store.Memberships.Replace(new CanonicalStoryMembershipManifest("other"));
        Assert.IsTrue(shell.CanonicalStoryWorkspace.RequestDelete(owned));
        Assert.IsFalse(File.Exists(project.Store.Tasks.GetPath("owned_task")));
        CollectionAssert.DoesNotContain(
            project.Store.Memberships.Load("opening").OwnedResources.Tasks,
            "owned_task");

        var workspace = shell.CanonicalStoryWorkspace!;
        Assert.IsTrue(workspace.StoryEditor.Host.AddNode(
            GraphNodeFactory.Create(GraphScope.StoryFlow, "action", "dirty_action")));
        Assert.IsTrue(workspace.RequestCreate(CanonicalStoryFolderKind.Actors));
        Assert.IsTrue(workspace.RequestCreate(CanonicalStoryFolderKind.Items));
        Assert.IsTrue(workspace.RequestCreate(CanonicalStoryFolderKind.Sessions));
        Assert.IsTrue(workspace.RequestCreate(CanonicalStoryFolderKind.Tasks));

        Assert.AreSame(workspace, shell.CanonicalStoryWorkspace);
        Assert.AreEqual(1, actorDialogs.CreateRequestCount);
        Assert.AreEqual(1, itemDialogs.CreateRequestCount);
        Assert.AreEqual(2, dialogs.CreateRequestCount);
        Assert.IsTrue(workspace.ActorItems.Any(item => item.Id == "dirty_actor"));
        Assert.IsTrue(workspace.ItemItems.Any(item => item.Id == "dirty_item"));
        Assert.IsTrue(File.Exists(project.Store.Sessions.GetPath("must_not_create")));
        Assert.IsTrue(File.Exists(project.Store.Tasks.GetPath("must_not_create")));
        Assert.IsTrue(workspace.OpenGraphResource(workspace.SessionItems.Single(item => item.Id == "must_not_create")));
        Assert.IsTrue(workspace.OpenGraphResource(workspace.TaskItems.Single(item => item.Id == "must_not_create")));
        Assert.IsTrue(workspace.ReturnToStory());
        Assert.IsTrue(workspace.StoryEditor.IsDirty);
        Assert.IsTrue(workspace.StoryEditor.Host.Graph.Nodes.Any(node => node.Id == "dirty_action"));
        Assert.IsFalse(shell.Output.Entries.Any(entry =>
            entry.Message.Contains("请先保存", StringComparison.Ordinal)
            || entry.Message.Contains("请逐个保存", StringComparison.Ordinal)));
    }

    private static GraphResourceEnvelope Envelope(
        GraphResourceKind kind,
        string id,
        string displayName)
    {
        var graph = kind == GraphResourceKind.Story
            ? new GraphDocument([GraphNodeFactory.Create(GraphScope.StoryFlow, "start", "start")])
            : new GraphDocument();
        return new GraphResourceEnvelope(kind, id, displayName, graph);
    }

    private sealed class CanonicalProjectFixture : IDisposable
    {
        public CanonicalProjectFixture()
        {
            Root = Path.Combine(
                AppContext.BaseDirectory,
                "temp",
                "canonical-shell-" + Guid.NewGuid().ToString("N"));
            var setup = new ProjectService();
            setup.CreateProject(Root, "test_project", "Test Project");
            setup.CreateStory("opening", "Opening");
            Store = new CanonicalProjectGraphStore(Root);
            Store.Stories.Create(Envelope(GraphResourceKind.Story, "opening", "Opening"));
            Store.Memberships.Create(new CanonicalStoryMembershipManifest("opening"));
        }

        public string Root { get; }
        public CanonicalProjectGraphStore Store { get; }

        public void AddOwnedSessionWithAggregate(bool includeConnection, bool connectLogicBoundary = false)
        {
            var session = Session("opening_session", "Opening Session", "accepted", "Accepted");
            Store.Sessions.Create(session);
            Store.Memberships.Replace(new CanonicalStoryMembershipManifest(
                "opening",
                new CanonicalStoryMembershipSet { Sessions = ["opening_session"] }));
            var story = Store.Stories.Load("opening");
            var aggregate = CanonicalAggregateNodeFactory.Create(session, "session-placement").Candidate!;
            var target = connectLogicBoundary
                ? GraphNodeFactory.Create(GraphScope.StoryFlow, "logic_output", "target")
                : new GraphNode(
                    "target",
                    "choice",
                    "Target",
                    [new("in", "In", true, GraphInterfaceKind.Flow)]);
            var graph = story.Graph!;
            graph.Nodes.Add(aggregate);
            graph.Nodes.Add(target);
            if (includeConnection)
                graph.Connections.Add(connectLogicBoundary
                    ? new(aggregate.Id, "known", target.Id, "logic_in", GraphInterfaceKind.Logic)
                    : new(aggregate.Id, "accepted", target.Id, "in", GraphInterfaceKind.Flow));
            story.Graph = graph;
            Store.Stories.Replace(story);
        }

        public ShellViewModel OpenShell(
            ICanonicalStoryResourceDialogs dialogs,
            Func<string, CanonicalProjectGraphStore>? storeFactory = null,
            IActorWorkspaceDialogs? actorDialogs = null,
            IItemWorkspaceDialogs? itemDialogs = null)
        {
            var shell = new ShellViewModel(
                new ProjectService(),
                new FixedProjectFolderPicker(Root),
                actorWorkspaceDialogs: actorDialogs,
                canonicalStoryResourceDialogs: dialogs,
                canonicalGraphStoreFactory: storeFactory,
                itemWorkspaceDialogs: itemDialogs);
            shell.OpenProjectCommand.Execute(null);
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

    private sealed class FakeCanonicalDialogs : ICanonicalStoryResourceDialogs
    {
        public CanonicalGraphResourceIdentityRequest? CreateResult { get; init; }
        public CanonicalGraphResourceChoice? PickResult { get; init; }
        public bool RemoveReferenceConfirmed { get; init; }
        public bool DeleteOwnedConfirmed { get; init; }
        public bool AggregateRemovalConfirmed { get; init; }
        public int CreateRequestCount { get; private set; }
        public int RemoveReferenceConfirmationCount { get; private set; }
        public int DeleteOwnedConfirmationCount { get; private set; }
        public int AggregateRemovalConfirmationCount { get; private set; }
        public IReadOnlyList<string> LastDeleteBlockers { get; private set; } = [];
        public IReadOnlyList<GraphConnection> LastAggregateReferences { get; private set; } = [];

        public CanonicalGraphResourceIdentityRequest? RequestCreate(
            GraphResourceKind resourceKind,
            string suggestedId)
        {
            CreateRequestCount++;
            return CreateResult;
        }

        public CanonicalGraphResourceChoice? PickReference(
            GraphResourceKind resourceKind,
            IReadOnlyList<GraphResourceInfo> candidates,
            string storyDisplayName)
            => PickResult;

        public bool ConfirmRemoveReference(
            CanonicalGraphResourceChoice resource,
            string storyDisplayName)
        {
            RemoveReferenceConfirmationCount++;
            return RemoveReferenceConfirmed;
        }

        public bool ConfirmDeleteOwned(CanonicalGraphResourceChoice resource)
        {
            DeleteOwnedConfirmationCount++;
            return DeleteOwnedConfirmed;
        }

        public bool ConfirmAggregateInterfaceRemoval(
            CanonicalGraphResourceChoice resource,
            IReadOnlyList<GraphConnection> affectedConnections)
        {
            AggregateRemovalConfirmationCount++;
            LastAggregateReferences = affectedConnections.ToArray();
            return AggregateRemovalConfirmed;
        }

        public void ShowDeleteBlocked(
            CanonicalGraphResourceChoice resource,
            IReadOnlyList<string> storyIds)
            => LastDeleteBlockers = storyIds.ToArray();
    }

    private sealed class FakeActorDialogs : IActorWorkspaceDialogs
    {
        public ActorIdentityRequest? CreateResult { get; init; }
        public int CreateRequestCount { get; private set; }

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
            string storyDisplayName) => null;
        public string? RequestRename(ActorResourceInfo actor, string suggestedId) => null;
        public bool ConfirmDelete(ActorResourceInfo actor) => false;
        public bool ConfirmRemoveReference(ActorResourceInfo actor, string storyDisplayName) => false;
        public void ShowReferences(ActorResourceInfo actor, IReadOnlyList<ResourceDescriptor> references) { }
        public bool ConfirmSaveBeforeSwitch(ActorResourceInfo actor) => false;
        public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges(ActorResourceInfo actor)
            => UnsavedChangesChoice.Cancel;
    }

    private sealed class FakeItemDialogs : IItemWorkspaceDialogs
    {
        public ItemIdentityRequest? CreateResult { get; init; }
        public int CreateRequestCount { get; private set; }

        public ItemCreationMode? RequestCreationMode(string storyDisplayName) => ItemCreationMode.Individual;
        public ItemIdentityRequest? RequestCreate(CanonicalStoryItemKind kind, string suggestedId)
        {
            CreateRequestCount++;
            return CreateResult;
        }
        public ItemWorkspaceChoice? PickReference(
            IReadOnlyList<ItemResourceInfo> candidates,
            string storyDisplayName) => null;
        public bool ConfirmRemoveReference(ItemWorkspaceChoice resource, string storyDisplayName) => false;
        public bool ConfirmDeleteOwned(ItemWorkspaceChoice resource) => false;
        public void ShowDeleteBlocked(ItemWorkspaceChoice resource, IReadOnlyList<string> storyIds) { }
    }

    private sealed class ThrowingWriter : IAtomicFileWriter
    {
        public void Write(string destinationPath, string contents, Action<string>? validateTemporaryFile = null)
            => throw new IOException("simulated write failure");
    }

    private static GraphResourceEnvelope Session(
        string id,
        string displayName,
        string portId,
        string portDisplayName)
    {
        var end = GraphNodeFactory.Create(GraphScope.Session, "end", "end");
        end.Properties["port_id"] = System.Text.Json.JsonSerializer.SerializeToElement(portId);
        end.Properties["display_name"] = System.Text.Json.JsonSerializer.SerializeToElement(portDisplayName);
        var logic = GraphNodeFactory.Create(GraphScope.Session, "logic_output", "logic");
        logic.Properties["port_id"] = System.Text.Json.JsonSerializer.SerializeToElement("known");
        logic.Properties["display_name"] = System.Text.Json.JsonSerializer.SerializeToElement("Known");
        return new(GraphResourceKind.Session, id, displayName, new GraphDocument([end, logic]));
    }
}
