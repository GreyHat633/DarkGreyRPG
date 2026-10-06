using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.Core.Stories;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CurrentResourceRepositoryTests
{
    [TestMethod]
    public void ActorAndItemDefaultPathsCannotOverwriteDifferentContentIdentities()
    {
        using var project = new TestProjectDirectory();
        const string owner = "ST-2345-6789-ABCD-EFGH";
        var actors = new ActorRepository(project.Root);
        var actorPath = actors.GetActorPath(owner + "~actor~requested");
        Directory.CreateDirectory(Path.GetDirectoryName(actorPath)!);
        var actorBytes = ActorSerializer.Serialize(new IndividualActorResource
        { NpcId = owner + "~actor~occupant", HomeStoryId = owner, DisplayName = "Keep actor" }, ActorIdPolicy.NewResource);
        File.WriteAllText(actorPath, actorBytes);
        Assert.ThrowsExactly<ActorRepositoryException>(() => actors.CreateIndividual(owner + "~actor~requested", "Overwrite"));
        Assert.AreEqual(actorBytes, File.ReadAllText(actorPath));
        Assert.AreEqual("Keep actor", actors.LoadActor(owner + "~actor~occupant").DisplayName);

        var items = new ItemRepository(project.Root);
        var itemPath = items.GetItemPath(owner + "~item~requested");
        Directory.CreateDirectory(Path.GetDirectoryName(itemPath)!);
        var itemBytes = ItemSerializer.Serialize(new IndividualItemResource { ItemId = owner + "~item~occupant", DisplayName = "Keep item" });
        File.WriteAllText(itemPath, itemBytes);
        Assert.ThrowsExactly<ItemRepositoryException>(() => items.SaveItem(new IndividualItemResource
        { ItemId = owner + "~item~requested", DisplayName = "Overwrite" }));
        Assert.AreEqual(itemBytes, File.ReadAllText(itemPath));
        Assert.AreEqual("Keep item", items.LoadItem(owner + "~item~occupant").DisplayName);
    }
    [TestMethod]
    public void GraphRepositoryUsesContentIdsForNestedArbitraryFilesAndReopenCrud()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var root = Path.Combine(project.Root, "resources", "canonical", "stories");
        var repository = new GraphResourceRepository(root, GraphResourceKind.Story);
        var first = new GraphResourceEnvelope(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", "Alpha", new GraphDocument());
        repository.Create(first);

        var arbitraryPath = Path.Combine(root, "custom", "renamed-source.json");
        Directory.CreateDirectory(Path.GetDirectoryName(arbitraryPath)!);
        File.WriteAllText(arbitraryPath,
            GraphResourceEnvelopeSerializer.Serialize(
                new GraphResourceEnvelope(GraphResourceKind.Story, "ST-JKLM-NPQR-STUV-WXYZ", "Other", new GraphDocument())));

        Assert.AreEqual(Path.GetFullPath(arbitraryPath), repository.GetPath("ST-JKLM-NPQR-STUV-WXYZ"));
        CollectionAssert.AreEquivalent(new[] { "ST-2345-6789-ABCD-EFGH", "ST-JKLM-NPQR-STUV-WXYZ" },
            repository.List().Select(item => item.Id).ToArray());

        var reopened = new GraphResourceRepository(root, GraphResourceKind.Story);
        reopened.Replace(new GraphResourceEnvelope(GraphResourceKind.Story, "ST-JKLM-NPQR-STUV-WXYZ", "Replaced", new GraphDocument()));
        Assert.AreEqual("Replaced", reopened.Load("ST-JKLM-NPQR-STUV-WXYZ").DisplayName);
        reopened.Delete("ST-JKLM-NPQR-STUV-WXYZ");
        Assert.IsFalse(File.Exists(arbitraryPath));
    }

    [TestMethod]
    public void DuplicateLogicalIdsFailClosedAcrossNestedPaths()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var root = Path.Combine(project.Root, "resources", "canonical", "tasks");
        var first = new GraphResourceEnvelope(GraphResourceKind.Task, "ST-2345-6789-ABCD-EFGH~task~duplicate", "One", new GraphDocument());
        var json = GraphResourceEnvelopeSerializer.Serialize(first);
        Directory.CreateDirectory(Path.Combine(root, "a"));
        Directory.CreateDirectory(Path.Combine(root, "b"));
        File.WriteAllText(Path.Combine(root, "a", "one.json"), json);
        File.WriteAllText(Path.Combine(root, "b", "two.json"), json);

        var repository = new GraphResourceRepository(root, GraphResourceKind.Task);
        Assert.AreEqual("graph.resource.repository.duplicate_id",
            Assert.ThrowsExactly<GraphResourceRepositoryException>(() => repository.Load("ST-2345-6789-ABCD-EFGH~task~duplicate")).Code);
        Assert.AreEqual("graph.resource.repository.duplicate_id",
            Assert.ThrowsExactly<GraphResourceRepositoryException>(() => repository.List()).Code);
    }

    [TestMethod]
    public void ActorItemAndStoryRepositoriesAcceptCurrentIdsAndKeepRoots()
    {
        using var project = new TestProjectDirectory();

        var actors = new ActorRepository(project.Root);
        var actor = actors.SaveActor(actors.CreateActor("ST-2345-6789-ABCD-EFGH~actor~merchant", "商人"));
        Assert.AreEqual(Path.GetFullPath(Path.Combine(project.Root, "actors", ResourceAddress.FromKey(actor.Id).RelativeDefinitionPath)), actor.SourcePath);
        Assert.AreEqual(actor.Id, new ActorRepository(project.Root).LoadActor(actor.Id).Id);

        var items = new ItemRepository(project.Root);
        items.SaveItem(items.CreateItem("ST-2345-6789-ABCD-EFGH~item~key", "钥匙"));
        Assert.AreEqual("ST-2345-6789-ABCD-EFGH~item~key", new ItemRepository(project.Root).LoadItem("ST-2345-6789-ABCD-EFGH~item~key").ItemId);
        Assert.AreEqual(Path.GetFullPath(Path.Combine(project.Root, "items", ResourceAddress.FromKey("ST-2345-6789-ABCD-EFGH~item~key").RelativeDefinitionPath)),
            items.GetItemPath("ST-2345-6789-ABCD-EFGH~item~key"));

        var stories = new StoryRepository(project.Root);
        stories.CreateStory("ST-2345-6789-ABCD-EFGH", "任务");
        Assert.AreEqual("ST-2345-6789-ABCD-EFGH", new StoryRepository(project.Root).LoadStory("ST-2345-6789-ABCD-EFGH").Id);
        Assert.AreEqual(Path.GetFullPath(Path.Combine(project.Root, "stories", "ST-2345-6789-ABCD-EFGH" + ".json")),
            stories.GetStoryPath("ST-2345-6789-ABCD-EFGH"));
    }

    [TestMethod]
    public void MembershipRepositoryUsesParsedStoryIdForArbitraryNestedFile()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var root = Path.Combine(project.Root, "resources", "canonical", "memberships");
        var arbitraryPath = Path.Combine(root, "nested", "membership-source.json");
        Directory.CreateDirectory(Path.GetDirectoryName(arbitraryPath)!);
        File.WriteAllText(arbitraryPath,
            CanonicalStoryMembershipSerializer.Serialize(new CanonicalStoryMembershipManifest("ST-2345-6789-ABCD-EFGH")));

        var repository = new CanonicalStoryMembershipRepository(root);
        Assert.AreEqual("ST-2345-6789-ABCD-EFGH", repository.Load("ST-2345-6789-ABCD-EFGH").StoryId);
        Assert.AreEqual(Path.GetFullPath(arbitraryPath), repository.GetPath("ST-2345-6789-ABCD-EFGH"));
        repository.Replace(new CanonicalStoryMembershipManifest("ST-2345-6789-ABCD-EFGH")
        {
            DisplayOrder = new CanonicalStoryDisplayOrder { Actors = ["ST-2345-6789-ABCD-EFGH~actor~actor"] },
        });
        repository.Delete("ST-2345-6789-ABCD-EFGH");
        Assert.IsFalse(File.Exists(arbitraryPath));
    }
    [TestMethod]
    public void EqualLocalIdsUnderDifferentOwnersCoexistOnWindowsAndReopenExactly()
    {
        using var project = new TestProjectDirectory();
        var actors = new ActorRepository(project.Root); var items = new ItemRepository(project.Root); var stories = new StoryRepository(project.Root);
        var owners = new[] { "ST-2345-6789-ABCD-EFGH", "ST-JKLM-NPQR-STUV-WXYZ", "ST-AAAA-BBBB-CCCC-DDDD" };
        foreach (var owner in owners)
        {
            var actorId = new ResourceAddress(StoryUid.Parse(owner), ResourceKind.Actor, "guard").ToKey();
            var groupId = new ResourceAddress(StoryUid.Parse(owner), ResourceKind.Actor, "guard_group").ToKey();
            var itemId = new ResourceAddress(StoryUid.Parse(owner), ResourceKind.Item, "guard").ToKey();
            actors.SaveActor(actors.CreateIndividual(actorId, owner));
            actors.SaveActor(actors.CreateCollective(groupId, owner));
            items.SaveItem(items.CreateItem(itemId, owner)); stories.CreateStory(owner, owner);
        }
        foreach (var owner in owners)
        {
            Assert.AreEqual(owner, new ActorRepository(project.Root).LoadActor(owner + "~actor~guard").DisplayName);
            Assert.AreEqual(owner, new ActorRepository(project.Root).LoadActor(owner + "~actor~guard_group").DisplayName);
            Assert.AreEqual(owner, new ItemRepository(project.Root).LoadItem(owner + "~item~guard").DisplayName);
            Assert.AreEqual(owner, new StoryRepository(project.Root).LoadStory(owner).DisplayName);
        }
    }
}
