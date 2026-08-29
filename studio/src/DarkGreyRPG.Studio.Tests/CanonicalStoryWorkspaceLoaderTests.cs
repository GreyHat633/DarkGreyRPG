using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Items;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CanonicalStoryWorkspaceLoaderTests
{
    [TestMethod]
    public void LoadsDetachedRootsAndPreservesManifestOrderAndProvenance()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var actorRepository = new ActorRepository(project.Root);
        actorRepository.SaveActor(actorRepository.CreateActor("owned_actor", "Owned Actor"));
        actorRepository.SaveActor(actorRepository.CreateActor("referenced_actor", "Referenced Actor"));
        store.Stories.Create(Envelope(GraphResourceKind.Story, "story", "Story"));
        store.Sessions.Create(Envelope(GraphResourceKind.Session, "owned_session", "Owned Session"));
        store.Sessions.Create(Envelope(GraphResourceKind.Session, "referenced_session", "Referenced Session"));
        store.Tasks.Create(Envelope(GraphResourceKind.Task, "owned_task", "Owned Task"));
        store.Tasks.Create(Envelope(GraphResourceKind.Task, "referenced_task", "Referenced Task"));
        store.Memberships.Create(new CanonicalStoryMembershipManifest(
            "story",
            new CanonicalStoryMembershipSet
            {
                Actors = ["owned_actor"],
                Sessions = ["owned_session"],
                Tasks = ["owned_task"],
            },
            new CanonicalStoryMembershipSet
            {
                Actors = ["referenced_actor"],
                Sessions = ["referenced_session"],
                Tasks = ["referenced_task"],
            }));

        var loader = new CanonicalStoryWorkspaceLoader(store, actorRepository);
        var loaded = loader.Load("story");

        Assert.AreEqual("story", loaded.Story.Id);
        Assert.AreEqual("story", loaded.Membership.StoryId);
        CollectionAssert.AreEqual(new[] { "owned_actor", "referenced_actor" }, loaded.Actors.Select(item => item.Id).ToArray());
        CollectionAssert.AreEqual(new[] { "owned_session", "referenced_session" }, loaded.Sessions.Select(item => item.Id).ToArray());
        CollectionAssert.AreEqual(new[] { "owned_task", "referenced_task" }, loaded.Tasks.Select(item => item.Id).ToArray());
        Assert.IsTrue(loaded.Actors[0].IsOwned);
        Assert.IsTrue(loaded.Actors[1].IsReferenced);
        Assert.IsTrue(loaded.Sessions.All(item => item.IsResolved));
        Assert.IsTrue(loaded.Tasks.All(item => item.IsResolved));

        var story = loaded.Story;
        var graph = story.Graph!;
        graph.Nodes.Clear();
        Assert.HasCount(0, graph.Nodes);
        Assert.HasCount(1, loaded.Story.Graph!.Nodes);
    }

    [TestMethod]
    public void MissingMembersRemainInOrderAndProduceStableErrorsWhileOthersLoad()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var actorRepository = new ActorRepository(project.Root);
        actorRepository.SaveActor(actorRepository.CreateActor("present_actor", "Present"));
        store.Stories.Create(Envelope(GraphResourceKind.Story, "story", "Story"));
        store.Sessions.Create(Envelope(GraphResourceKind.Session, "present_session", "Present"));
        store.Memberships.Create(new CanonicalStoryMembershipManifest(
            "story",
            new CanonicalStoryMembershipSet
            {
                Actors = ["missing_actor", "present_actor"],
                Sessions = ["missing_session", "present_session"],
                Tasks = ["missing_task"],
            }));

        var loaded = new CanonicalStoryWorkspaceLoader(store, actorRepository).Load("story");

        Assert.IsTrue(loaded.Actors[0].IsMissing);
        Assert.IsTrue(loaded.Actors[1].IsResolved);
        Assert.IsTrue(loaded.Sessions[0].IsMissing);
        Assert.IsTrue(loaded.Sessions[1].IsResolved);
        Assert.IsTrue(loaded.Tasks[0].IsMissing);
        Assert.HasCount(3, loaded.ValidationErrors);
        CollectionAssert.AreEquivalent(
            new[] { "missing_actor", "missing_session", "missing_task" },
            loaded.ValidationErrors.Select(issue => issue.NodeId).ToArray());
        Assert.IsTrue(loaded.ValidationErrors.All(issue => issue.Code == "story.workspace.member.missing"));
        StringAssert.Contains(loaded.ValidationErrors.Single(issue => issue.NodeId == "missing_task").Message, "Task");
        StringAssert.Contains(loaded.ValidationErrors.Single(issue => issue.NodeId == "missing_task").Message, "missing_task");
    }

    [TestMethod]
    public void LoadsNamedItemsAndGroupsWithOwnedReferencedProvenance()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var items = new ItemRepository(project.Root);
        items.SaveItem(new IndividualItemResource { ItemId = "owned_item", DisplayName = "Owned Item" });
        items.SaveItem(new IndividualItemResource { ItemId = "referenced_item", DisplayName = "Referenced Item" });
        items.SaveGroup(new CollectiveItemResource { GroupId = "owned_group", DisplayName = "Owned Group" });
        items.SaveGroup(new CollectiveItemResource { GroupId = "referenced_group", DisplayName = "Referenced Group" });
        store.Stories.Create(Envelope(GraphResourceKind.Story, "story", "Story"));
        store.Memberships.Create(new CanonicalStoryMembershipManifest(
            "story",
            new CanonicalStoryMembershipSet
            {
                Items = ["owned_item"],
                ItemGroups = ["owned_group"],
            },
            new CanonicalStoryMembershipSet
            {
                Items = ["referenced_item"],
                ItemGroups = ["referenced_group"],
            }));

        var loaded = new CanonicalStoryWorkspaceLoader(store, items: items).Load("story");

        CollectionAssert.AreEqual(new[] { "owned_item", "referenced_item" },
            loaded.Items.Select(item => item.Id).ToArray());
        CollectionAssert.AreEqual(new[] { "owned_group", "referenced_group" },
            loaded.ItemGroups.Select(group => group.Id).ToArray());
        Assert.IsTrue(loaded.Items[0].IsOwned);
        Assert.IsTrue(loaded.Items[1].IsReferenced);
        Assert.IsTrue(loaded.ItemGroups.All(group => group.IsResolved));
        Assert.IsEmpty(loaded.ValidationIssues);
    }

    [TestMethod]
    public void MissingItemsAndGroupsRemainInOrderAndAreReported()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var items = new ItemRepository(project.Root);
        items.SaveItem(new IndividualItemResource { ItemId = "present_item", DisplayName = "Present Item" });
        items.SaveGroup(new CollectiveItemResource { GroupId = "present_group", DisplayName = "Present Group" });
        store.Stories.Create(Envelope(GraphResourceKind.Story, "story", "Story"));
        store.Memberships.Create(new CanonicalStoryMembershipManifest(
            "story",
            new CanonicalStoryMembershipSet
            {
                Items = ["missing_item", "present_item"],
                ItemGroups = ["missing_group", "present_group"],
            }));

        var loaded = new CanonicalStoryWorkspaceLoader(store, items: items).Load("story");

        Assert.IsTrue(loaded.Items[0].IsMissing);
        Assert.IsTrue(loaded.Items[1].IsResolved);
        Assert.IsTrue(loaded.ItemGroups[0].IsMissing);
        Assert.IsTrue(loaded.ItemGroups[1].IsResolved);
        CollectionAssert.AreEquivalent(new[] { "missing_item", "missing_group" },
            loaded.ValidationErrors.Select(issue => issue.NodeId).ToArray());
        Assert.AreEqual("items", loaded.ValidationErrors.Single(issue => issue.NodeId == "missing_item").Field);
        Assert.AreEqual("item_groups", loaded.ValidationErrors.Single(issue => issue.NodeId == "missing_group").Field);
    }

    [TestMethod]
    public void LoadsOnlyRequestedActorsAndNeverScansLegacyRoots()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var actorRepository = new ActorRepository(project.Root);
        actorRepository.SaveActor(actorRepository.CreateActor("requested", "Requested"));
        File.WriteAllText(project.ActorPath("unrelated"), "not actor json");
        Directory.CreateDirectory(Path.Combine(project.Root, "stories"));
        File.WriteAllText(Path.Combine(project.Root, "stories", "legacy.json"), "not canonical story json");
        Directory.CreateDirectory(Path.Combine(project.Root, "dialogues"));
        File.WriteAllText(Path.Combine(project.Root, "dialogues", "legacy.json"), "not canonical dialogue json");
        Directory.CreateDirectory(Path.Combine(project.Root, "quests"));
        File.WriteAllText(Path.Combine(project.Root, "quests", "legacy.json"), "not canonical quest json");
        store.Stories.Create(Envelope(GraphResourceKind.Story, "story", "Story"));
        store.Memberships.Create(new CanonicalStoryMembershipManifest(
            "story", new CanonicalStoryMembershipSet { Actors = ["requested"] }));

        var loaded = new CanonicalStoryWorkspaceLoader(store, actorRepository).Load("story");

        Assert.AreEqual("requested", loaded.Actors.Single().Resource!.Id);
        Assert.IsEmpty(loaded.ValidationIssues);
    }

    [TestMethod]
    public void RootStoryOrMembershipFailureFailsClosed()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var loader = new CanonicalStoryWorkspaceLoader(store);

        Assert.AreEqual("graph.resource.repository.not_found",
            Assert.ThrowsExactly<GraphResourceRepositoryException>(() => loader.Load("missing")).Code);

        store.Stories.Create(Envelope(GraphResourceKind.Story, "story", "Story"));
        Assert.AreEqual("story.membership.repository.not_found",
            Assert.ThrowsExactly<CanonicalStoryMembershipRepositoryException>(() => loader.Load("story")).Code);
    }

    private static GraphResourceEnvelope Envelope(GraphResourceKind kind, string id, string displayName)
        => new(kind, id, displayName, new GraphDocument([new GraphNode("node", "unknown", "Node")]));
}
