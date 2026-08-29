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
        CreateCanonicalStory(store, "story");

        var actor = new CanonicalStoryActorLifecycleService(store).CreateOwned("story", "hero", "Hero");

        Assert.AreEqual("story", actor.HomeStoryId);
        Assert.AreEqual("story", ActorSerializer.Deserialize(File.ReadAllText(project.ActorPath("hero"))).HomeStoryId);
        CollectionAssert.AreEqual(new[] { "hero" }, store.Memberships.Load("story").OwnedResources.Actors);
    }

    [TestMethod]
    public void AddAndRemoveReferencePreserveActorMembershipOrder()
    {
        using var project = NewProject();
        var store = NewStore(project.Root);
        CreateCanonicalStory(store, "owner");
        CreateCanonicalStory(store, "other");
        var service = new CanonicalStoryActorLifecycleService(store);
        service.CreateOwned("owner", "before", "Before");
        service.CreateOwned("owner", "shared", "Shared");
        service.CreateOwned("owner", "after", "After");
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "other", referencedResources: new CanonicalStoryMembershipSet { Actors = ["before", "after"] }));

        service.AddReference("other", "shared");
        CollectionAssert.AreEqual(new[] { "before", "after", "shared" },
            store.Memberships.Load("other").ReferencedResources.Actors);
        service.RemoveReference("other", "before");
        CollectionAssert.AreEqual(new[] { "after", "shared" },
            store.Memberships.Load("other").ReferencedResources.Actors);
    }

    [TestMethod]
    public void RemoveReferenceRepairsMissingActorFile()
    {
        using var project = NewProject();
        var store = NewStore(project.Root);
        CreateCanonicalStory(store, "story");
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "story", referencedResources: new CanonicalStoryMembershipSet { Actors = ["missing"] }));

        new CanonicalStoryActorLifecycleService(store).RemoveReference("story", "missing");

        Assert.IsEmpty(store.Memberships.Load("story").ReferencedResources.Actors);
        Assert.IsFalse(File.Exists(project.ActorPath("missing")));
    }

    [TestMethod]
    public void DeletionPlanBlocksCanonicalReferenceAndDuplicateOwnership()
    {
        using var project = NewProject();
        var store = NewStore(project.Root);
        CreateCanonicalStory(store, "owner");
        CreateCanonicalStory(store, "reference");
        CreateCanonicalStory(store, "duplicate");
        var service = new CanonicalStoryActorLifecycleService(store);
        service.CreateOwned("owner", "hero", "Hero");
        service.AddReference("reference", "hero");
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "duplicate", ownedResources: new CanonicalStoryMembershipSet { Actors = ["hero"] }));

        var plan = service.GetDeletionPlan("owner", "hero");

        Assert.IsFalse(plan.CanDelete);
        Assert.HasCount(2, plan.CanonicalBlockers);
        Assert.IsTrue(plan.CanonicalBlockers.Any(item => item.IsReferenced && item.StoryId == "reference"));
        Assert.IsTrue(plan.CanonicalBlockers.Any(item => item.IsOwned && item.StoryId == "duplicate"));
    }

    [TestMethod]
    public void DeletionPlanBlocksLegacyReferenceAndOwnership()
    {
        using var project = NewProject();
        var store = NewStore(project.Root);
        CreateCanonicalStory(store, "owner");
        var service = new CanonicalStoryActorLifecycleService(store);
        service.CreateOwned("owner", "hero", "Hero");
        var stories = new StoryRepository(project.Root);
        stories.SaveStory(LegacyStory("legacy_reference", referenced: true));
        stories.SaveStory(LegacyStory("legacy_owner", owned: true));

        var plan = service.GetDeletionPlan("owner", "hero");

        Assert.IsFalse(plan.CanDelete);
        Assert.HasCount(2, plan.LegacyBlockers);
        Assert.IsTrue(plan.LegacyBlockers.Any(item => item.IsReferenced && item.StoryId == "legacy_reference"));
        Assert.IsTrue(plan.LegacyBlockers.Any(item => item.IsOwned && item.StoryId == "legacy_owner"));
    }

    [TestMethod]
    public void DeletionPlanTreatsSameIdLegacyOwnershipAsMigrationMirror()
    {
        using var project = NewProject();
        var store = NewStore(project.Root);
        CreateCanonicalStory(store, "owner");
        var service = new CanonicalStoryActorLifecycleService(store);
        service.CreateOwned("owner", "hero", "Hero");
        new StoryRepository(project.Root).SaveStory(LegacyStory("owner", owned: true));

        var plan = service.GetDeletionPlan("owner", "hero");

        Assert.IsTrue(plan.CanDelete);
        Assert.IsEmpty(plan.LegacyBlockers);
    }

    [TestMethod]
    public void DeletionPlanKeepsSameIdLegacyReferenceAndDifferentLegacyOwnershipAsBlockers()
    {
        using var project = NewProject();
        var store = NewStore(project.Root);
        CreateCanonicalStory(store, "owner");
        var service = new CanonicalStoryActorLifecycleService(store);
        service.CreateOwned("owner", "hero", "Hero");
        var stories = new StoryRepository(project.Root);
        stories.SaveStory(LegacyStory("owner", referenced: true));
        stories.SaveStory(LegacyStory("legacy_owner", owned: true));

        var plan = service.GetDeletionPlan("owner", "hero");

        Assert.IsFalse(plan.CanDelete);
        Assert.HasCount(2, plan.LegacyBlockers);
        Assert.IsTrue(plan.LegacyBlockers.Any(item => item.IsReferenced && item.StoryId == "owner"));
        Assert.IsTrue(plan.LegacyBlockers.Any(item => item.IsOwned && item.StoryId == "legacy_owner"));
    }

    [TestMethod]
    public void DeleteOwnedRemovesActorAndCanonicalOwnershipWhenUnblocked()
    {
        using var project = NewProject();
        var store = NewStore(project.Root);
        CreateCanonicalStory(store, "owner");
        var service = new CanonicalStoryActorLifecycleService(store);
        service.CreateOwned("owner", "hero", "Hero");

        service.DeleteOwned("owner", "hero");

        Assert.IsFalse(File.Exists(project.ActorPath("hero")));
        Assert.IsEmpty(store.Memberships.Load("owner").OwnedResources.Actors);
    }

    [TestMethod]
    public void CreateRollbackRemovesActorWhenMembershipReplacementFails()
    {
        using var project = NewProject();
        var normal = NewStore(project.Root);
        CreateCanonicalStory(normal, "story");
        var failing = new CanonicalProjectGraphStore(project.Root, new FailOnWrite(1));

        var exception = Assert.ThrowsExactly<CanonicalStoryActorLifecycleException>(
            () => new CanonicalStoryActorLifecycleService(failing).CreateOwned("story", "hero", "Hero"));

        Assert.AreEqual("story.actor.lifecycle.membership_replace_failed", exception.Code);
        Assert.IsFalse(File.Exists(project.ActorPath("hero")));
        Assert.IsEmpty(failing.Memberships.Load("story").OwnedResources.Actors);
    }

    [TestMethod]
    public void DeleteRollbackRestoresExactActorAndMembershipBytes()
    {
        using var project = NewProject();
        var normal = NewStore(project.Root);
        CreateCanonicalStory(normal, "story");
        var normalService = new CanonicalStoryActorLifecycleService(normal);
        normalService.CreateOwned("story", "hero", "Hero");
        var actorBytes = File.ReadAllBytes(project.ActorPath("hero"));
        var membershipBytes = File.ReadAllBytes(normal.Memberships.GetPath("story"));
        var failing = new CanonicalProjectGraphStore(project.Root, new FailOnWrite(1));

        var exception = Assert.ThrowsExactly<CanonicalStoryActorLifecycleException>(
            () => new CanonicalStoryActorLifecycleService(failing).DeleteOwned("story", "hero"));

        Assert.AreEqual("story.actor.lifecycle.membership_replace_failed", exception.Code);
        CollectionAssert.AreEqual(actorBytes, File.ReadAllBytes(project.ActorPath("hero")));
        CollectionAssert.AreEqual(membershipBytes, File.ReadAllBytes(failing.Memberships.GetPath("story")));
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
            OwnedResources = new StoryMembership { Actors = owned ? ["hero"] : [] },
            ReferencedResources = new StoryMembership { Actors = referenced ? ["hero"] : [] },
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
