using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.IO;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CanonicalStoryActorLifecycleServiceTests
{
    [TestMethod]
    public void CreateOwnedRequiresCanonicalStoryAndRecordsHomeStoryAndMembership()
    {
        using var project = NewProject();
        var store = NewStore(project.Root);
        CreateCanonicalStory(store, "ST-2345-6789-ABCD-EFGH");

        var actor = new CanonicalStoryActorLifecycleService(store).CreateOwned("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~actor~hero", "Hero");

        Assert.AreEqual("ST-2345-6789-ABCD-EFGH", actor.HomeStoryId);
        Assert.AreEqual("ST-2345-6789-ABCD-EFGH", ActorSerializer.Deserialize(File.ReadAllText(new ActorRepository(project.Root).GetActorPath("ST-2345-6789-ABCD-EFGH~actor~hero"))).HomeStoryId);
        CollectionAssert.AreEqual(new[] { "ST-2345-6789-ABCD-EFGH~actor~hero" }, store.Memberships.Load("ST-2345-6789-ABCD-EFGH").OwnedResources.Actors);
    }

    [TestMethod]
    public void AddAndRemoveReferencePreserveActorMembershipOrder()
    {
        using var project = NewProject();
        var store = NewStore(project.Root);
        CreateCanonicalStory(store, "ST-2345-6789-ABCD-EFGH");
        CreateCanonicalStory(store, "ST-JKLM-NPQR-STUV-WXYZ");
        var service = new CanonicalStoryActorLifecycleService(store);
        service.CreateOwned("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~actor~before", "Before");
        service.CreateOwned("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~actor~shared", "Shared");
        service.CreateOwned("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~actor~after", "After");
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "ST-JKLM-NPQR-STUV-WXYZ", referencedResources: new CanonicalStoryMembershipSet { Actors = ["ST-2345-6789-ABCD-EFGH~actor~before", "ST-2345-6789-ABCD-EFGH~actor~after"] }));

        service.AddReference("ST-JKLM-NPQR-STUV-WXYZ", "ST-2345-6789-ABCD-EFGH~actor~shared");
        CollectionAssert.AreEqual(new[] { "ST-2345-6789-ABCD-EFGH~actor~before", "ST-2345-6789-ABCD-EFGH~actor~after", "ST-2345-6789-ABCD-EFGH~actor~shared" },
            store.Memberships.Load("ST-JKLM-NPQR-STUV-WXYZ").ReferencedResources.Actors);
        service.RemoveReference("ST-JKLM-NPQR-STUV-WXYZ", "ST-2345-6789-ABCD-EFGH~actor~before");
        CollectionAssert.AreEqual(new[] { "ST-2345-6789-ABCD-EFGH~actor~after", "ST-2345-6789-ABCD-EFGH~actor~shared" },
            store.Memberships.Load("ST-JKLM-NPQR-STUV-WXYZ").ReferencedResources.Actors);
    }

    [TestMethod]
    public void RemoveReferenceRepairsMissingActorFile()
    {
        using var project = NewProject();
        var store = NewStore(project.Root);
        CreateCanonicalStory(store, "ST-2345-6789-ABCD-EFGH");
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "ST-2345-6789-ABCD-EFGH", referencedResources: new CanonicalStoryMembershipSet { Actors = ["ST-2345-6789-ABCD-EFGH~actor~missing"] }));

        new CanonicalStoryActorLifecycleService(store).RemoveReference("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~actor~missing");

        Assert.IsEmpty(store.Memberships.Load("ST-2345-6789-ABCD-EFGH").ReferencedResources.Actors);
        Assert.IsFalse(File.Exists(new ActorRepository(project.Root).GetActorPath("ST-2345-6789-ABCD-EFGH~actor~missing")));
    }

    [TestMethod]
    public void DeletionPlanBlocksCanonicalReferenceAndDuplicateOwnership()
    {
        using var project = NewProject();
        var store = NewStore(project.Root);
        CreateCanonicalStory(store, "ST-2345-6789-ABCD-EFGH");
        CreateCanonicalStory(store, "ST-JKLM-NPQR-STUV-WXYZ");
        CreateCanonicalStory(store, "ST-3456-789A-BCDE-FGHJ");
        var service = new CanonicalStoryActorLifecycleService(store);
        service.CreateOwned("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~actor~hero", "Hero");
        service.AddReference("ST-JKLM-NPQR-STUV-WXYZ", "ST-2345-6789-ABCD-EFGH~actor~hero");
        Assert.ThrowsExactly<CanonicalStoryMembershipRepositoryException>(() =>
            store.Memberships.Replace(new CanonicalStoryMembershipManifest(
                "ST-3456-789A-BCDE-FGHJ", ownedResources: new CanonicalStoryMembershipSet { Actors = ["ST-2345-6789-ABCD-EFGH~actor~hero"] })));
        Assert.IsEmpty(store.Memberships.Load("ST-3456-789A-BCDE-FGHJ").OwnedResources.Actors);

        var plan = service.GetDeletionPlan("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~actor~hero");

        Assert.IsFalse(plan.CanDelete);
        Assert.HasCount(1, plan.CanonicalBlockers);
        Assert.IsTrue(plan.CanonicalBlockers.Any(item => item.IsReferenced && item.StoryId == "ST-JKLM-NPQR-STUV-WXYZ"));
        Assert.IsFalse(plan.CanonicalBlockers.Any(item => item.IsOwned));
    }

    [TestMethod]
    [DataRow("stories", "{\"owned_resources\":{\"actors\":[\"hero\"]}}")]
    [DataRow("stories", "broken")]
    [DataRow("dialogues", "{\"entry\":\"end\"}")]
    [DataRow("dialogues", "broken")]
    [DataRow("quests", "{\"objectives\":[]}")]
    [DataRow("quests", "broken")]
    public void UnsupportedAuthorDataBlocksActorOperationsWithoutRewriting(string directory, string contents)
    {
        using var project = NewProject();
        var store = NewStore(project.Root);
        const string story = "ST-2345-6789-ABCD-EFGH";
        const string actor = story + "~actor~hero";
        CreateCanonicalStory(store, story);
        var service = new CanonicalStoryActorLifecycleService(store);
        service.CreateOwned(story, actor, "Hero");
        var retired = Path.Combine(project.Root, directory, "retired.json");
        Directory.CreateDirectory(Path.GetDirectoryName(retired)!);
        File.WriteAllText(retired, contents);
        var actorPath = service.Actors.GetActorPath(actor);
        var membershipPath = store.Memberships.GetPath(story);
        var actorBytes = File.ReadAllBytes(actorPath);
        var membershipBytes = File.ReadAllBytes(membershipPath);
        var retiredBytes = File.ReadAllBytes(retired);
        Assert.AreEqual("story.actor.project.unsupported", Assert.ThrowsExactly<CanonicalStoryActorLifecycleException>(
            () => service.GetDeletionPlan(story, actor)).Code);
        Assert.ThrowsExactly<CanonicalStoryActorLifecycleException>(() => service.DeleteOwned(story, actor));
        Assert.ThrowsExactly<CanonicalStoryActorLifecycleException>(() => service.CreateOwned(story, story + "~actor~new", "New"));
        CollectionAssert.AreEqual(actorBytes, File.ReadAllBytes(actorPath));
        CollectionAssert.AreEqual(membershipBytes, File.ReadAllBytes(membershipPath));
        CollectionAssert.AreEqual(retiredBytes, File.ReadAllBytes(retired));
        Assert.IsFalse(File.Exists(service.Actors.GetActorPath(story + "~actor~new")));
    }

    [TestMethod]
    public void DeleteOwnedRemovesActorAndCanonicalOwnershipWhenUnblocked()
    {
        using var project = NewProject();
        var store = NewStore(project.Root);
        CreateCanonicalStory(store, "ST-2345-6789-ABCD-EFGH");
        var service = new CanonicalStoryActorLifecycleService(store);
        service.CreateOwned("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~actor~hero", "Hero");

        service.DeleteOwned("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~actor~hero");

        Assert.IsFalse(File.Exists(new ActorRepository(project.Root).GetActorPath("ST-2345-6789-ABCD-EFGH~actor~hero")));
        Assert.IsEmpty(store.Memberships.Load("ST-2345-6789-ABCD-EFGH").OwnedResources.Actors);
    }

    [TestMethod]
    public void CreateRollbackRemovesActorWhenMembershipReplacementFails()
    {
        using var project = NewProject();
        var normal = NewStore(project.Root);
        CreateCanonicalStory(normal, "ST-2345-6789-ABCD-EFGH");
        var failing = new CanonicalProjectGraphStore(project.Root, new FailOnWrite(1));

        var exception = Assert.ThrowsExactly<CanonicalStoryActorLifecycleException>(
            () => new CanonicalStoryActorLifecycleService(failing).CreateOwned("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~actor~hero", "Hero"));

        Assert.AreEqual("story.actor.lifecycle.membership_replace_failed", exception.Code);
        Assert.IsFalse(File.Exists(new ActorRepository(project.Root).GetActorPath("ST-2345-6789-ABCD-EFGH~actor~hero")));
        Assert.IsEmpty(failing.Memberships.Load("ST-2345-6789-ABCD-EFGH").OwnedResources.Actors);
    }

    [TestMethod]
    public void DeleteRollbackRestoresExactActorAndMembershipBytes()
    {
        using var project = NewProject();
        var normal = NewStore(project.Root);
        CreateCanonicalStory(normal, "ST-2345-6789-ABCD-EFGH");
        var normalService = new CanonicalStoryActorLifecycleService(normal);
        normalService.CreateOwned("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~actor~hero", "Hero");
        var actorBytes = File.ReadAllBytes(new ActorRepository(project.Root).GetActorPath("ST-2345-6789-ABCD-EFGH~actor~hero"));
        var membershipBytes = File.ReadAllBytes(normal.Memberships.GetPath("ST-2345-6789-ABCD-EFGH"));
        var failing = new CanonicalProjectGraphStore(project.Root, new FailOnWrite(1));

        var exception = Assert.ThrowsExactly<CanonicalStoryActorLifecycleException>(
            () => new CanonicalStoryActorLifecycleService(failing).DeleteOwned("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~actor~hero"));

        Assert.AreEqual("story.actor.lifecycle.membership_replace_failed", exception.Code);
        CollectionAssert.AreEqual(actorBytes, File.ReadAllBytes(new ActorRepository(project.Root).GetActorPath("ST-2345-6789-ABCD-EFGH~actor~hero")));
        CollectionAssert.AreEqual(membershipBytes, File.ReadAllBytes(failing.Memberships.GetPath("ST-2345-6789-ABCD-EFGH")));
    }

    private static TestProjectDirectory NewProject() => new(createProjectFile: false);

    private static CanonicalProjectGraphStore NewStore(string root) => new(root);

    private static void CreateCanonicalStory(CanonicalProjectGraphStore store, string id)
    {
        store.Stories.Create(new GraphResourceEnvelope(
            GraphResourceKind.Story, id, id, new GraphDocument([new GraphNode("start", "start", "Start")] )));
        store.Memberships.Create(new CanonicalStoryMembershipManifest(id));
    }

    private sealed class FailOnWrite(int failureNumber) : IAtomicFileWriter
    {
        private int _writes;

        public void Write(string destinationPath, string contents, Action<string>? validateTemporaryFile = null)
        {
            if (++_writes == failureNumber)
                throw new IOException("simulated membership failure");
            new AtomicFileWriter().Write(destinationPath, contents, validateTemporaryFile);
        }
    }
}
