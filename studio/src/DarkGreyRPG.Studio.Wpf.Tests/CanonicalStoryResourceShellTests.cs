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
    [DataRow(GraphResourceKind.Session)]
    [DataRow(GraphResourceKind.Task)]
    public void CreateGraphTagsAndEditMetadataKeepUnsavedLocalGraph(GraphResourceKind kind)
    {
        using var project = new CanonicalProjectFixture();
        var dialogs = new FakeCanonicalDialogs { CreateResult = new("resource", "Resource", ["created"]) };
        var shell = project.OpenShell(dialogs);
        var workspace = shell.CanonicalStoryWorkspace!;
        Assert.IsTrue(workspace.RequestCreate(kind == GraphResourceKind.Session ? CanonicalStoryFolderKind.Sessions : CanonicalStoryFolderKind.Tasks));
        var resource = workspace.SessionItems.Concat(workspace.TaskItems).Single();
        var repository = kind == GraphResourceKind.Session ? project.Store.Sessions : project.Store.Tasks;
        CollectionAssert.AreEqual(new[] { "created" }, repository.Load(resource.Id).Tags.ToArray());
        Assert.IsTrue(resource.Editor.Host.AddNode(GraphNodeFactory.Create(kind == GraphResourceKind.Session ? GraphScope.Session : GraphScope.Task,
            kind == GraphResourceKind.Session ? "line" : "objective", "draft")));
        var undo = resource.Editor.Host.Session.UndoCount;
        dialogs.EditResult = new(resource.Id, "Edited", ["changed"]);
        Assert.IsTrue(workspace.RequestRename(resource));
        Assert.AreEqual("Edited", resource.DisplayName);
        CollectionAssert.AreEqual(new[] { "changed" }, resource.Editor.Tags.ToArray());
        Assert.IsTrue(resource.Editor.IsDirty);
        Assert.AreEqual(undo, resource.Editor.Host.Session.UndoCount);
        Assert.IsTrue(resource.Editor.Host.Graph.Nodes.Any(node => node.Id == "draft"));
        Assert.IsFalse(repository.Load(resource.Id).Graph!.Nodes.Any(node => node.Id == "draft"));
        CollectionAssert.AreEqual(new[] { "changed" }, repository.Load(resource.Id).Tags.ToArray());
    }

    [TestMethod]
    [DataRow("actor")]
    [DataRow("actor_group")]
    [DataRow("item")]
    [DataRow("item_group")]
    [DataRow("session")]
    [DataRow("task")]
    public void MetadataEditRefillsChangesAndClearsTagsWithoutSavingGraphDraft(string kind)
    {
        using var project = new CanonicalProjectFixture();
        var actors = new ActorRepository(project.Root);
        var items = new ItemRepository(project.Root);
        if (kind.StartsWith("actor")) new CanonicalStoryActorLifecycleService(project.Store, actors)
            .CreateOwned("opening", kind == "actor" ? CanonicalStoryActorKind.Individual : CanonicalStoryActorKind.Collective, "resource", "Before", ["old"]);
        else if (kind.StartsWith("item")) new CanonicalStoryItemLifecycleService(project.Store, items)
            .CreateOwned("opening", kind == "item" ? CanonicalStoryItemKind.Individual : CanonicalStoryItemKind.Collective, "resource", "Before", ["old"]);
        else new CanonicalStoryResourceLifecycleService(project.Store).CreateOwned("opening",
            kind == "session" ? GraphResourceKind.Session : GraphResourceKind.Task, "resource", "Before", ["old"]);
        var graphDialogs = new FakeCanonicalDialogs();
        var actorDialogs = new FakeActorDialogs();
        var itemDialogs = new FakeItemDialogs();
        var shell = project.OpenShell(graphDialogs, actorDialogs: actorDialogs, itemDialogs: itemDialogs);
        var workspace = shell.CanonicalStoryWorkspace!;
        var beforeStory = project.Store.Stories.Load("opening").ToJson();
        Assert.IsTrue(workspace.StoryEditor.Host.AddNode(GraphNodeFactory.Create(GraphScope.StoryFlow, "terminate", "draft")));
        foreach (var tags in new IReadOnlyList<string>[] { new[] { "edited", "中文标签" }, Array.Empty<string>() })
        {
            var edit = new ResourceRenameRequest("resource", "After", tags);
            graphDialogs.EditResult = actorDialogs.EditResult = itemDialogs.EditResult = edit;
            var resource = workspace.Folders.SelectMany(folder => folder.Items).Single();
            Assert.IsTrue(workspace.RequestRename(resource));
            Assert.AreEqual("After", workspace.Folders.SelectMany(folder => folder.Items).Single().DisplayName, shell.StatusMessage);
            var actual = kind.StartsWith("actor") ? actors.LoadActor("resource").Tags
                : kind == "item" ? items.LoadItem("resource").Tags
                : kind == "item_group" ? items.LoadGroup("resource").Tags
                : kind == "session" ? project.Store.Sessions.Load("resource").Tags : project.Store.Tasks.Load("resource").Tags;
            CollectionAssert.AreEqual(tags.ToArray(), actual.ToArray());
            Assert.IsTrue(workspace.StoryEditor.IsDirty);
            Assert.IsTrue(workspace.StoryEditor.Host.Graph.Nodes.Any(node => node.Id == "draft"));
            Assert.AreEqual(beforeStory, project.Store.Stories.Load("opening").ToJson());
        }
        var captured = kind.StartsWith("actor") ? actorDialogs.LastTags : kind.StartsWith("item") ? itemDialogs.LastTags : graphDialogs.LastTags;
        CollectionAssert.AreEqual(new[] { "edited", "中文标签" }, captured.ToArray());
    }

    [TestMethod]
    public void HomeFoldersFollowLibraryAndNavigateAcrossStoriesWithoutDiscardingDrafts()
    {
        using var project = new CanonicalProjectFixture();
        new CanonicalStoryResourceLifecycleService(project.Store).CreateOwned("opening", GraphResourceKind.Session, "session", "Session");
        project.Store.Stories.Create(Envelope(GraphResourceKind.Story, "second", "Second"));
        project.Store.Memberships.Create(new CanonicalStoryMembershipManifest("second"));
        new CanonicalStoryResourceLifecycleService(project.Store).CreateOwned("second", GraphResourceKind.Task, "task", "Task");
        var membership = project.Store.Memberships.Load("opening");
        var references = membership.ReferencedResources;
        references.Tasks.Add("missing_task"); membership.ReferencedResources = references;
        project.Store.Memberships.Replace(membership);
        var shell = project.OpenShell(new FakeCanonicalDialogs());
        var first = shell.CanonicalStoryWorkspace!;
        first.StoryEditor.Host.AddNode(GraphNodeFactory.Create(GraphScope.StoryFlow, "terminate", "draft"));
        first.ActivateBreadcrumb(first.Breadcrumbs[0]);
        CollectionAssert.AreEqual(new[] { "角色", "物品", "会话", "任务" }, shell.HomeResourceFolders.Select(folder => folder.DisplayName).ToArray());
        Assert.IsFalse(shell.HomeResourceFolders[0].HasResources);
        var missing = shell.HomeResourceFolders[3].Items.Single();
        Assert.IsTrue(missing.IsMissing);
        StringAssert.StartsWith(missing.Label, "[引用]");
        Assert.IsFalse(shell.OpenHomeResource(missing));
        Assert.IsTrue(shell.OpenHomeResource(shell.HomeResourceFolders[2].Items.Single()));
        Assert.AreSame(first.SessionItems.Single().Editor, first.ActiveEditor);
        first.ActivateBreadcrumb(first.Breadcrumbs[0]);
        shell.ProjectHome.SelectedStory = shell.ProjectHome.Stories.Single(story => story.Id == "second");
        Assert.IsTrue(shell.OpenHomeResource(shell.HomeResourceFolders[3].Items.Single()));
        Assert.AreEqual("task", shell.CanonicalStoryWorkspace!.ActiveEditor.Id);
        shell.CanonicalStoryWorkspace.ActivateBreadcrumb(shell.CanonicalStoryWorkspace.Breadcrumbs[0]);
        shell.ProjectHome.SelectedStory = shell.ProjectHome.Stories.Single(story => story.Id == "opening");
        Assert.IsTrue(shell.OpenHomeResource(shell.HomeResourceFolders[2].Items.Single()));
        Assert.AreSame(first, shell.CanonicalStoryWorkspace);
        Assert.IsTrue(first.StoryEditor.IsDirty);
        Assert.IsTrue(first.StoryEditor.Host.Graph.Nodes.Any(node => node.Id == "draft"));
        Assert.IsFalse(project.Store.Stories.Load("opening").Graph!.Nodes.Any(node => node.Id == "draft"));
    }

    [TestMethod]
    public void DisplayNameOnlyRenamePreservesIdentityOwnershipAndSynchronizesGraphAggregates()
    {
        using var project = new CanonicalProjectFixture();
        project.AddOwnedSessionWithAggregate(includeConnection: false);
        var actors = new ActorRepository(project.Root);
        actors.SaveActor(actors.CreateActor("actor", "Actor"));
        var items = new ItemRepository(project.Root);
        items.SaveItem(new IndividualItemResource { ItemId = "item", DisplayName = "Item", Tags = ["tag"] });
        var settle = GraphNodeFactory.Create(GraphScope.Task, "settle", "settle");
        settle.Ports.Add(new GraphPort("result", "Result", true, GraphInterfaceKind.Logic, 0));
        var task = new GraphResourceEnvelope(
            GraphResourceKind.Task,
            "opening_task",
            "Opening Task",
            new GraphDocument([
                GraphNodeFactory.Create(GraphScope.Task, "objective", "objective"),
                settle ]));
        project.Store.Tasks.Create(task);
        var membership = project.Store.Memberships.Load("opening");
        var owned = membership.OwnedResources;
        owned.Actors.Add("actor");
        owned.Items.Add("item");
        owned.Tasks.Add("opening_task");
        membership.OwnedResources = owned;
        project.Store.Memberships.Replace(membership);
        var graphDialogs = new FakeCanonicalDialogs { DisplayNameResult = "Renamed Graph" };
        var actorDialogs = new FakeActorDialogs { DisplayNameResult = "Renamed Actor" };
        var itemDialogs = new FakeItemDialogs { DisplayNameResult = "Renamed Item" };
        var shell = project.OpenShell(graphDialogs, actorDialogs: actorDialogs, itemDialogs: itemDialogs);
        var workspace = shell.CanonicalStoryWorkspace!;
        Assert.IsTrue(workspace.PlaceAggregate(workspace.TaskItems.Single(), "task-placement", 100, 100));
        shell.SaveCurrentResourceCommand.Execute(null);
        Assert.IsFalse(workspace.StoryEditor.IsDirty, shell.StatusMessage);
        var ownershipBefore = project.Store.Memberships.Load("opening").ToJson(indented: false);

        Assert.IsTrue(workspace.RequestRename(workspace.ActorItems.Single()));
        Assert.IsTrue(workspace.RequestRename(workspace.ItemItems.Single()));
        Assert.IsTrue(workspace.RequestRename(workspace.SessionItems.Single()));
        Assert.IsTrue(workspace.RequestRename(workspace.TaskItems.Single()));

        Assert.AreEqual("Renamed Actor", actors.LoadActor("actor").DisplayName);
        Assert.AreEqual("Renamed Item", items.LoadItem("item").DisplayName);
        Assert.AreEqual(IndividualItemResource.ResourceType, items.LoadItem("item").Type);
        CollectionAssert.AreEqual(new[] { "tag" }, items.LoadItem("item").Tags);
        Assert.AreEqual("Renamed Graph", project.Store.Sessions.Load("opening_session").DisplayName);
        Assert.AreEqual("Renamed Graph", project.Store.Tasks.Load("opening_task").DisplayName, shell.StatusMessage);
        Assert.AreEqual("Renamed Actor", workspace.ActorItems.Single().DisplayName);
        Assert.AreEqual("Renamed Item", workspace.ItemItems.Single().DisplayName);
        Assert.AreEqual("Renamed Graph", workspace.SessionItems.Single().DisplayName);
        Assert.AreEqual("Renamed Graph", workspace.TaskItems.Single().DisplayName);
        var persistedStory = project.Store.Stories.Load("opening");
        Assert.AreEqual("Renamed Graph", persistedStory.Graph!.Nodes.Single(node =>
            node.Properties.TryGetValue("resource_id", out var id) && id.GetString() == "opening_session").DisplayName);
        Assert.AreEqual("Renamed Graph", persistedStory.Graph!.Nodes.Single(node =>
            node.Properties.TryGetValue("resource_id", out var id) && id.GetString() == "opening_task").DisplayName);
        Assert.AreEqual(ownershipBefore, project.Store.Memberships.Load("opening").ToJson(indented: false));
    }

    [TestMethod]
    public void ProjectBreadcrumbKeepsDirtyCanonicalWorkspaceAliveAndSameStoryRestoresIt()
    {
        using var project = new CanonicalProjectFixture();
        project.AddOwnedSessionWithAggregate(includeConnection: false);
        var shell = project.OpenShell(new FakeCanonicalDialogs());
        var workspace = shell.CanonicalStoryWorkspace!;
        var session = workspace.SessionItems.Single();
        Assert.IsTrue(workspace.OpenGraphResource(session));
        Assert.IsTrue(session.Editor.Host.AddNode(
            GraphNodeFactory.Create(GraphScope.Session, "line", "draft-line")));
        var projectBreadcrumb = workspace.Breadcrumbs[0];
        Assert.AreEqual("Test Project", projectBreadcrumb.DisplayName);

        Assert.IsTrue(workspace.ActivateBreadcrumb(projectBreadcrumb));

        Assert.IsFalse(shell.IsCanonicalStoryWorkspaceVisible);
        Assert.AreSame(workspace, shell.CanonicalStoryWorkspace);
        Assert.AreSame(session.Editor, workspace.ActiveEditor);
        Assert.IsTrue(session.Editor.IsDirty);
        shell.OpenStory(shell.ProjectHome.Stories.Single(story => story.Id == "opening"));
        Assert.IsTrue(shell.IsCanonicalStoryWorkspaceVisible);
        Assert.AreSame(workspace, shell.CanonicalStoryWorkspace);
        Assert.AreSame(session.Editor, workspace.ActiveEditor);
        Assert.IsTrue(session.Editor.Host.Graph.Nodes.Any(node => node.Id == "draft-line"));
    }

    [TestMethod]
    public void ResourceOrderPersistsAcrossShellReloadAndSurvivesDeleteCreate()
    {
        using var project = new CanonicalProjectFixture();
        var lifecycle = new CanonicalStoryResourceLifecycleService(project.Store);
        lifecycle.CreateOwnedSession("opening", "session_a", "Session A");
        lifecycle.CreateOwnedSession("opening", "session_b", "Session B");
        lifecycle.CreateOwnedSession("opening", "session_c", "Session C");
        var firstShell = project.OpenShell(new FakeCanonicalDialogs());
        var firstFolder = firstShell.CanonicalStoryWorkspace!.Folders
            .Single(folder => folder.Kind == CanonicalStoryFolderKind.Sessions);

        Assert.IsTrue(firstShell.CanonicalStoryWorkspace.ReorderResource(
            firstFolder.Items.Single(item => item.Id == "session_c"),
            firstFolder.Items.Single(item => item.Id == "session_a")));
        CollectionAssert.AreEqual(
            new[] { "session_c", "session_a", "session_b" },
            project.Store.Memberships.Load("opening").DisplayOrder.Sessions);

        var reloadedShell = project.OpenShell(new FakeCanonicalDialogs());
        CollectionAssert.AreEqual(
            new[] { "session_c", "session_a", "session_b" },
            reloadedShell.CanonicalStoryWorkspace!.SessionItems.Select(item => item.Id).ToArray());

        lifecycle.DeleteOwnedSession("opening", "session_a");
        lifecycle.CreateOwnedSession("opening", "session_d", "Session D");
        var changedShell = project.OpenShell(new FakeCanonicalDialogs());
        CollectionAssert.AreEqual(
            new[] { "session_c", "session_b", "session_d" },
            changedShell.CanonicalStoryWorkspace!.SessionItems.Select(item => item.Id).ToArray());
    }

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
        Assert.IsFalse(workspace.StoryEditor.IsDirty);
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
    public void ConfirmedAggregatePortRemovalSavesChildAndStoryTogether()
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
        Assert.IsFalse(workspace.StoryEditor.IsDirty);
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
        Assert.AreEqual(0, dialogs.RemoveReferenceConfirmationCount); // Reversible membership removal needs no extra confirmation.
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
        public ResourceRenameRequest? EditResult { get; set; }
        public IReadOnlyList<string> LastTags { get; private set; } = [];
        public ResourceRenameRequest? RequestResourceRename(string label, string id, string displayName, IReadOnlyList<string> tags)
        {
            LastTags = tags.ToArray();
            return EditResult ?? (DisplayNameResult is { } name ? new(id, name) : null);
        }
        public string? DisplayNameResult { get; init; }
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

        public string? RequestDisplayName(string resourceLabel, string id, string currentDisplayName)
            => DisplayNameResult;

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
        public ResourceRenameRequest? EditResult { get; set; }
        public IReadOnlyList<string> LastTags { get; private set; } = [];
        public ResourceRenameRequest? RequestResourceRename(string label, string id, string displayName, IReadOnlyList<string> tags)
        {
            LastTags = tags.ToArray();
            return EditResult ?? (DisplayNameResult is { } name ? new(id, name) : null);
        }
        public string? DisplayNameResult { get; init; }
        public ActorIdentityRequest? CreateResult { get; init; }
        public int CreateRequestCount { get; private set; }

        public string? RequestDisplayName(string resourceLabel, string id, string currentDisplayName)
            => DisplayNameResult;

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
        public ResourceRenameRequest? EditResult { get; set; }
        public IReadOnlyList<string> LastTags { get; private set; } = [];
        public ResourceRenameRequest? RequestResourceRename(string label, string id, string displayName, IReadOnlyList<string> tags)
        {
            LastTags = tags.ToArray();
            return EditResult ?? (DisplayNameResult is { } name ? new(id, name) : null);
        }
        public string? DisplayNameResult { get; init; }
        public ItemIdentityRequest? CreateResult { get; init; }
        public int CreateRequestCount { get; private set; }

        public string? RequestDisplayName(string resourceLabel, string id, string currentDisplayName)
            => DisplayNameResult;

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
