using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.IO;
using DarkGreyRPG.Studio.Core.Stories;

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
    public void DeletionPlanBlocksLegacyReferenceAndOwnership()
    {
        using var project = NewProject();
        var store = NewStore(project.Root);
        CreateCanonicalStory(store, "ST-2345-6789-ABCD-EFGH");
        var service = new CanonicalStoryActorLifecycleService(store);
        service.CreateOwned("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~actor~hero", "Hero");
        var stories = new StoryRepository(project.Root);
        stories.SaveStory(LegacyStory("ST-4567-89AB-CDEF-GHJK", referenced: true));
        stories.SaveStory(LegacyStory("ST-5678-9ABC-DEFG-HJKL", owned: true));

        var plan = service.GetDeletionPlan("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~actor~hero");

        Assert.IsFalse(plan.CanDelete);
        Assert.HasCount(2, plan.LegacyBlockers);
        Assert.IsTrue(plan.LegacyBlockers.Any(item => item.IsReferenced && item.StoryId == "ST-4567-89AB-CDEF-GHJK"));
        Assert.IsTrue(plan.LegacyBlockers.Any(item => item.IsOwned && item.StoryId == "ST-5678-9ABC-DEFG-HJKL"));
    }

    [TestMethod]
    public void DeletionPlanTreatsSameIdLegacyOwnershipAsMigrationMirror()
    {
        using var project = NewProject();
        var store = NewStore(project.Root);
        CreateCanonicalStory(store, "ST-2345-6789-ABCD-EFGH");
        var service = new CanonicalStoryActorLifecycleService(store);
        service.CreateOwned("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~actor~hero", "Hero");
        new StoryRepository(project.Root).SaveStory(LegacyStory("ST-2345-6789-ABCD-EFGH", owned: true));

        var plan = service.GetDeletionPlan("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~actor~hero");

        Assert.IsTrue(plan.CanDelete);
        Assert.IsEmpty(plan.LegacyBlockers);
    }

    [TestMethod]
    public void DeletionPlanKeepsSameIdLegacyReferenceAndDifferentLegacyOwnershipAsBlockers()
    {
        using var project = NewProject();
        var store = NewStore(project.Root);
        CreateCanonicalStory(store, "ST-2345-6789-ABCD-EFGH");
        var service = new CanonicalStoryActorLifecycleService(store);
        service.CreateOwned("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~actor~hero", "Hero");
        var stories = new StoryRepository(project.Root);
        stories.SaveStory(LegacyStory("ST-2345-6789-ABCD-EFGH", referenced: true));
        stories.SaveStory(LegacyStory("ST-5678-9ABC-DEFG-HJKL", owned: true));

        var plan = service.GetDeletionPlan("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~actor~hero");

        Assert.IsFalse(plan.CanDelete);
        Assert.HasCount(2, plan.LegacyBlockers);
        Assert.IsTrue(plan.LegacyBlockers.Any(item => item.IsReferenced && item.StoryId == "ST-2345-6789-ABCD-EFGH"));
        Assert.IsTrue(plan.LegacyBlockers.Any(item => item.IsOwned && item.StoryId == "ST-5678-9ABC-DEFG-HJKL"));
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

    private static StoryResource LegacyStory(string id, bool owned = false, bool referenced = false)
        => new()
        {
            Id = id,
            DisplayName = id,
            FlowRef = id,
            Title = id,
            Entry = "end",
            Nodes = [new StoryNodeResource { Id = "end", Type = "END" }],
            OwnedResources = new StoryMembership { Actors = owned ? ["ST-2345-6789-ABCD-EFGH~actor~hero"] : [] },
            ReferencedResources = new StoryMembership { Actors = referenced ? ["ST-2345-6789-ABCD-EFGH~actor~hero"] : [] },
        };

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
