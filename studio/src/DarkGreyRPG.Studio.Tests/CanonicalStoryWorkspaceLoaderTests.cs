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
        actorRepository.SaveActor(actorRepository.CreateActor("ST-2345-6789-ABCD-EFGH~actor~owned_actor", "Owned Actor"));
        actorRepository.SaveActor(actorRepository.CreateActor("ST-JKLM-NPQR-STUV-WXYZ~actor~referenced_actor", "Referenced Actor"));
        store.Stories.Create(Envelope(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", "Story"));
        store.Sessions.Create(Envelope(GraphResourceKind.Session, "ST-2345-6789-ABCD-EFGH~session~owned_session", "Owned Session"));
        store.Sessions.Create(Envelope(GraphResourceKind.Session, "ST-JKLM-NPQR-STUV-WXYZ~session~referenced_session", "Referenced Session"));
        store.Tasks.Create(Envelope(GraphResourceKind.Task, "ST-2345-6789-ABCD-EFGH~task~owned_task", "Owned Task"));
        store.Tasks.Create(Envelope(GraphResourceKind.Task, "ST-JKLM-NPQR-STUV-WXYZ~task~referenced_task", "Referenced Task"));
        store.Memberships.Create(new CanonicalStoryMembershipManifest(
            "ST-2345-6789-ABCD-EFGH",
            new CanonicalStoryMembershipSet
            {
                Actors = ["ST-2345-6789-ABCD-EFGH~actor~owned_actor"],
                Sessions = ["ST-2345-6789-ABCD-EFGH~session~owned_session"],
                Tasks = ["ST-2345-6789-ABCD-EFGH~task~owned_task"],
            },
            new CanonicalStoryMembershipSet
            {
                Actors = ["ST-JKLM-NPQR-STUV-WXYZ~actor~referenced_actor"],
                Sessions = ["ST-JKLM-NPQR-STUV-WXYZ~session~referenced_session"],
                Tasks = ["ST-JKLM-NPQR-STUV-WXYZ~task~referenced_task"],
            }));

        var loader = new CanonicalStoryWorkspaceLoader(store, actorRepository);
        var loaded = loader.Load("ST-2345-6789-ABCD-EFGH");

        Assert.AreEqual("ST-2345-6789-ABCD-EFGH", loaded.Story.Id);
        Assert.AreEqual("ST-2345-6789-ABCD-EFGH", loaded.Membership.StoryId);
        CollectionAssert.AreEqual(new[] { "ST-2345-6789-ABCD-EFGH~actor~owned_actor", "ST-JKLM-NPQR-STUV-WXYZ~actor~referenced_actor" }, loaded.Actors.Select(item => item.Id).ToArray());
        CollectionAssert.AreEqual(new[] { "ST-2345-6789-ABCD-EFGH~session~owned_session", "ST-JKLM-NPQR-STUV-WXYZ~session~referenced_session" }, loaded.Sessions.Select(item => item.Id).ToArray());
        CollectionAssert.AreEqual(new[] { "ST-2345-6789-ABCD-EFGH~task~owned_task", "ST-JKLM-NPQR-STUV-WXYZ~task~referenced_task" }, loaded.Tasks.Select(item => item.Id).ToArray());
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
        actorRepository.SaveActor(actorRepository.CreateActor("ST-2345-6789-ABCD-EFGH~actor~present_actor", "Present"));
        store.Stories.Create(Envelope(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", "Story"));
        store.Sessions.Create(Envelope(GraphResourceKind.Session, "ST-2345-6789-ABCD-EFGH~session~present_session", "Present"));
        store.Memberships.Create(new CanonicalStoryMembershipManifest(
            "ST-2345-6789-ABCD-EFGH",
            new CanonicalStoryMembershipSet
            {
                Actors = ["ST-2345-6789-ABCD-EFGH~actor~missing_actor", "ST-2345-6789-ABCD-EFGH~actor~present_actor"],
                Sessions = ["ST-2345-6789-ABCD-EFGH~session~missing_session", "ST-2345-6789-ABCD-EFGH~session~present_session"],
                Tasks = ["ST-2345-6789-ABCD-EFGH~task~missing_task"],
            }));

        var loaded = new CanonicalStoryWorkspaceLoader(store, actorRepository).Load("ST-2345-6789-ABCD-EFGH");

        Assert.IsTrue(loaded.Actors[0].IsMissing);
        Assert.IsTrue(loaded.Actors[1].IsResolved);
        Assert.IsTrue(loaded.Sessions[0].IsMissing);
        Assert.IsTrue(loaded.Sessions[1].IsResolved);
        Assert.IsTrue(loaded.Tasks[0].IsMissing);
        Assert.HasCount(3, loaded.ValidationErrors);
        CollectionAssert.AreEquivalent(
            new[] { "ST-2345-6789-ABCD-EFGH~actor~missing_actor", "ST-2345-6789-ABCD-EFGH~session~missing_session", "ST-2345-6789-ABCD-EFGH~task~missing_task" },
            loaded.ValidationErrors.Select(issue => issue.NodeId).ToArray());
        Assert.IsTrue(loaded.ValidationErrors.All(issue => issue.Code == "story.workspace.member.missing"));
        StringAssert.Contains(loaded.ValidationErrors.Single(issue => issue.NodeId == "ST-2345-6789-ABCD-EFGH~task~missing_task").Message, "Task");
        StringAssert.Contains(loaded.ValidationErrors.Single(issue => issue.NodeId == "ST-2345-6789-ABCD-EFGH~task~missing_task").Message, "ST-2345-6789-ABCD-EFGH~task~missing_task");
    }

    [TestMethod]
    public void LoadsNamedItemsAndGroupsWithOwnedReferencedProvenance()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var items = new ItemRepository(project.Root);
        items.SaveItem(new IndividualItemResource { ItemId = "ST-2345-6789-ABCD-EFGH~item~owned_item", DisplayName = "Owned Item" });
        items.SaveItem(new IndividualItemResource { ItemId = "ST-JKLM-NPQR-STUV-WXYZ~item~referenced_item", DisplayName = "Referenced Item" });
        items.SaveGroup(new CollectiveItemResource { GroupId = "ST-2345-6789-ABCD-EFGH~item_group~owned_group", DisplayName = "Owned Group" });
        items.SaveGroup(new CollectiveItemResource { GroupId = "ST-JKLM-NPQR-STUV-WXYZ~item_group~referenced_group", DisplayName = "Referenced Group" });
        store.Stories.Create(Envelope(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", "Story"));
        store.Memberships.Create(new CanonicalStoryMembershipManifest(
            "ST-2345-6789-ABCD-EFGH",
            new CanonicalStoryMembershipSet
            {
                Items = ["ST-2345-6789-ABCD-EFGH~item~owned_item"],
                ItemGroups = ["ST-2345-6789-ABCD-EFGH~item_group~owned_group"],
            },
            new CanonicalStoryMembershipSet
            {
                Items = ["ST-JKLM-NPQR-STUV-WXYZ~item~referenced_item"],
                ItemGroups = ["ST-JKLM-NPQR-STUV-WXYZ~item_group~referenced_group"],
            }));

        var loaded = new CanonicalStoryWorkspaceLoader(store, items: items).Load("ST-2345-6789-ABCD-EFGH");

        CollectionAssert.AreEqual(new[] { "ST-2345-6789-ABCD-EFGH~item~owned_item", "ST-JKLM-NPQR-STUV-WXYZ~item~referenced_item" },
            loaded.Items.Select(item => item.Id).ToArray());
        CollectionAssert.AreEqual(new[] { "ST-2345-6789-ABCD-EFGH~item_group~owned_group", "ST-JKLM-NPQR-STUV-WXYZ~item_group~referenced_group" },
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
        items.SaveItem(new IndividualItemResource { ItemId = "ST-2345-6789-ABCD-EFGH~item~present_item", DisplayName = "Present Item" });
        items.SaveGroup(new CollectiveItemResource { GroupId = "ST-2345-6789-ABCD-EFGH~item_group~present_group", DisplayName = "Present Group" });
        store.Stories.Create(Envelope(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", "Story"));
        store.Memberships.Create(new CanonicalStoryMembershipManifest(
            "ST-2345-6789-ABCD-EFGH",
            new CanonicalStoryMembershipSet
            {
                Items = ["ST-2345-6789-ABCD-EFGH~item~missing_item", "ST-2345-6789-ABCD-EFGH~item~present_item"],
                ItemGroups = ["ST-2345-6789-ABCD-EFGH~item_group~missing_group", "ST-2345-6789-ABCD-EFGH~item_group~present_group"],
            }));

        var loaded = new CanonicalStoryWorkspaceLoader(store, items: items).Load("ST-2345-6789-ABCD-EFGH");

        Assert.IsTrue(loaded.Items[0].IsMissing);
        Assert.IsTrue(loaded.Items[1].IsResolved);
        Assert.IsTrue(loaded.ItemGroups[0].IsMissing);
        Assert.IsTrue(loaded.ItemGroups[1].IsResolved);
        CollectionAssert.AreEquivalent(new[] { "ST-2345-6789-ABCD-EFGH~item~missing_item", "ST-2345-6789-ABCD-EFGH~item_group~missing_group" },
            loaded.ValidationErrors.Select(issue => issue.NodeId).ToArray());
        Assert.AreEqual("items", loaded.ValidationErrors.Single(issue => issue.NodeId == "ST-2345-6789-ABCD-EFGH~item~missing_item").Field);
        Assert.AreEqual("item_groups", loaded.ValidationErrors.Single(issue => issue.NodeId == "ST-2345-6789-ABCD-EFGH~item_group~missing_group").Field);
    }

    [TestMethod]
    public void LoadsOnlyRequestedActorsAndNeverScansLegacyRoots()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var actorRepository = new ActorRepository(project.Root);
        actorRepository.SaveActor(actorRepository.CreateActor("ST-2345-6789-ABCD-EFGH~actor~requested", "Requested"));
        File.WriteAllText(project.ActorPath("unrelated"), "not actor json");
        Directory.CreateDirectory(Path.Combine(project.Root, "stories"));
        File.WriteAllText(Path.Combine(project.Root, "stories", "legacy.json"), "not canonical story json");
        Directory.CreateDirectory(Path.Combine(project.Root, "dialogues"));
        File.WriteAllText(Path.Combine(project.Root, "dialogues", "legacy.json"), "not canonical dialogue json");
        Directory.CreateDirectory(Path.Combine(project.Root, "quests"));
        File.WriteAllText(Path.Combine(project.Root, "quests", "legacy.json"), "not canonical quest json");
        store.Stories.Create(Envelope(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", "Story"));
        store.Memberships.Create(new CanonicalStoryMembershipManifest(
            "ST-2345-6789-ABCD-EFGH", new CanonicalStoryMembershipSet { Actors = ["ST-2345-6789-ABCD-EFGH~actor~requested"] }));

        var loaded = new CanonicalStoryWorkspaceLoader(store, actorRepository).Load("ST-2345-6789-ABCD-EFGH");

        Assert.AreEqual("ST-2345-6789-ABCD-EFGH~actor~requested", loaded.Actors.Single().Resource!.Id);
        Assert.IsEmpty(loaded.ValidationIssues);
    }

    [TestMethod]
    public void RootStoryOrMembershipFailureFailsClosed()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var loader = new CanonicalStoryWorkspaceLoader(store);

        Assert.AreEqual("graph.resource.repository.not_found",
            Assert.ThrowsExactly<GraphResourceRepositoryException>(() => loader.Load("ST-JKLM-NPQR-STUV-WXYZ")).Code);

        store.Stories.Create(Envelope(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", "Story"));
        Assert.AreEqual("story.membership.repository.not_found",
            Assert.ThrowsExactly<CanonicalStoryMembershipRepositoryException>(() => loader.Load("ST-2345-6789-ABCD-EFGH")).Code);
    }

    private static GraphResourceEnvelope Envelope(GraphResourceKind kind, string id, string displayName)
        => new(kind, id, displayName, new GraphDocument([new GraphNode("node", "unknown", "Node")]));
}
