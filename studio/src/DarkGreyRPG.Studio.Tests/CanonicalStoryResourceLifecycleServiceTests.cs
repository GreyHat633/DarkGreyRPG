using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.IO;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CanonicalStoryResourceLifecycleServiceTests
{
    [TestMethod]
    public void CreatesScopeValidBlankSessionAndTaskAsOwned()
    {
        using var project = NewProject();
        var store = new CanonicalProjectGraphStore(project.Root);
        CreateStory(store, "story");
        var service = new CanonicalStoryResourceLifecycleService(store);

        var session = service.CreateOwned("story", GraphResourceKind.Session, "session", "Session");
        var task = service.CreateOwned("story", GraphResourceKind.Task, "task", "Task");

        Assert.IsTrue(GraphScopePolicy.IsValid(session.Graph!, GraphScope.Session));
        Assert.IsTrue(GraphScopePolicy.IsValid(task.Graph!, GraphScope.Task));
        CollectionAssert.AreEqual(new[] { "start" }, session.Graph!.Nodes.Select(node => node.Id).ToArray());
        CollectionAssert.AreEqual(new[] { "activate", "settle" }, task.Graph!.Nodes.Select(node => node.Id).ToArray());
        var settle = task.Graph.Nodes.Single(node => node.Type == "settle");
        Assert.HasCount(1, settle.Ports);
        Assert.IsTrue(settle.Ports[0].IsInput);
        Assert.AreEqual(GraphInterfaceKind.Logic, settle.Ports[0].InterfaceKind);
        StringAssert.StartsWith(settle.Ports[0].Id, "dynamic_port_");
        CollectionAssert.AreEqual(new[] { "session" }, store.Memberships.Load("story").OwnedResources.Sessions);
        CollectionAssert.AreEqual(new[] { "task" }, store.Memberships.Load("story").OwnedResources.Tasks);
    }

    [TestMethod]
    public void ReferenceAndRemovalPreserveOtherMembershipOrder()
    {
        using var project = NewProject();
        var store = new CanonicalProjectGraphStore(project.Root);
        CreateStory(store, "owner");
        CreateStory(store, "other");
        var service = new CanonicalStoryResourceLifecycleService(store);
        service.CreateOwnedSession("owner", "before", "Before");
        service.CreateOwnedSession("owner", "shared", "Shared");
        service.CreateOwnedSession("owner", "after", "After");
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "other",
            referencedResources: new CanonicalStoryMembershipSet { Sessions = ["before", "shared", "after"] }));

        AssertCode(() => service.AddSessionReference("other", "shared"),
            "story.resource.reference.duplicate");
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "other",
            referencedResources: new CanonicalStoryMembershipSet { Sessions = ["before", "after"] }));
        service.AddSessionReference("other", "shared");
        CollectionAssert.AreEqual(new[] { "before", "after", "shared" },
            store.Memberships.Load("other").ReferencedResources.Sessions);
        service.RemoveSessionReference("other", "shared");
        CollectionAssert.AreEqual(new[] { "before", "after" },
            store.Memberships.Load("other").ReferencedResources.Sessions);
    }

    [TestMethod]
    public void DeletionPlanBlocksOtherStoryAndSuccessfulDeleteRemovesOwnerOnly()
    {
        using var project = NewProject();
        var store = new CanonicalProjectGraphStore(project.Root);
        CreateStory(store, "owner");
        CreateStory(store, "other");
        var service = new CanonicalStoryResourceLifecycleService(store);
        service.CreateOwnedTask("owner", "task", "Task");
        service.AddTaskReference("other", "task");

        var plan = service.GetDeletionPlan("owner", GraphResourceKind.Task, "task");
        Assert.IsFalse(plan.CanDelete);
        CollectionAssert.AreEqual(new[] { "other" }, plan.ReferencingStoryIds.ToArray());
        AssertCode(() => service.DeleteOwnedTask("owner", "task"), "story.resource.delete.blocked");

        service.RemoveTaskReference("other", "task");
        service.DeleteOwnedTask("owner", "task");
        Assert.IsFalse(File.Exists(store.Tasks.GetPath("task")));
        Assert.IsEmpty(store.Memberships.Load("owner").OwnedResources.Tasks);
    }

    [TestMethod]
    public void DeletionPlanBlocksCorruptDuplicateOwnershipInAnotherStory()
    {
        using var project = NewProject();
        var store = new CanonicalProjectGraphStore(project.Root);
        CreateStory(store, "owner");
        CreateStory(store, "other");
        var service = new CanonicalStoryResourceLifecycleService(store);
        service.CreateOwnedSession("owner", "shared", "Shared");
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "other",
            ownedResources: new CanonicalStoryMembershipSet { Sessions = ["shared"] }));

        var plan = service.GetDeletionPlan("owner", GraphResourceKind.Session, "shared");

        Assert.IsFalse(plan.CanDelete);
        CollectionAssert.AreEqual(new[] { "other" }, plan.ReferencingStoryIds.ToArray());
    }

    [TestMethod]
    public void RemovesMissingReferencedResourceMembershipForRepair()
    {
        using var project = NewProject();
        var store = new CanonicalProjectGraphStore(project.Root);
        CreateStory(store, "story");
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "story",
            referencedResources: new CanonicalStoryMembershipSet { Tasks = ["missing_task"] }));

        new CanonicalStoryResourceLifecycleService(store).RemoveTaskReference("story", "missing_task");

        Assert.IsEmpty(store.Memberships.Load("story").ReferencedResources.Tasks);
        Assert.IsFalse(File.Exists(store.Tasks.GetPath("missing_task")));
    }

    [TestMethod]
    public void CreationMembershipFailureRemovesJustCreatedResource()
    {
        using var project = NewProject();
        var normal = new CanonicalProjectGraphStore(project.Root);
        CreateStory(normal, "story");
        var failing = new CanonicalProjectGraphStore(project.Root, new FailOnWrite(2));
        var service = new CanonicalStoryResourceLifecycleService(failing);

        AssertCode(() => service.CreateOwnedSession("story", "orphan", "Orphan"),
            "story.resource.lifecycle.membership_replace_failed");
        Assert.IsFalse(File.Exists(failing.Sessions.GetPath("orphan")));
        Assert.IsEmpty(failing.Memberships.Load("story").OwnedResources.Sessions);
    }

    [TestMethod]
    public void DeletionMembershipFailureRestoresExactResourceAndMembership()
    {
        using var project = NewProject();
        var normal = new CanonicalProjectGraphStore(project.Root);
        CreateStory(normal, "story");
        var normalService = new CanonicalStoryResourceLifecycleService(normal);
        normalService.CreateOwnedSession("story", "session", "Session");
        var resourcePath = normal.Sessions.GetPath("session");
        var membershipPath = normal.Memberships.GetPath("story");
        var resourceBytes = File.ReadAllBytes(resourcePath);
        var membershipBytes = File.ReadAllBytes(membershipPath);

        var failing = new CanonicalProjectGraphStore(project.Root, new FailOnWrite(1));
        var service = new CanonicalStoryResourceLifecycleService(failing);
        AssertCode(() => service.DeleteOwnedSession("story", "session"),
            "story.resource.lifecycle.membership_replace_failed");
        CollectionAssert.AreEqual(resourceBytes, File.ReadAllBytes(resourcePath));
        CollectionAssert.AreEqual(membershipBytes, File.ReadAllBytes(membershipPath));
    }

    [TestMethod]
    public void AlwaysFailingMembershipWriterStillRestoresAndReportsBothFailures()
    {
        using var project = NewProject();
        var normal = new CanonicalProjectGraphStore(project.Root);
        CreateStory(normal, "story");
        var normalService = new CanonicalStoryResourceLifecycleService(normal);
        normalService.CreateOwnedTask("story", "task", "Task");
        var resourcePath = normal.Tasks.GetPath("task");
        var membershipPath = normal.Memberships.GetPath("story");
        var resourceBytes = File.ReadAllBytes(resourcePath);
        var membershipBytes = File.ReadAllBytes(membershipPath);

        var failing = new CanonicalProjectGraphStore(project.Root, new AlwaysFailingWriter());
        var exception = Assert.ThrowsExactly<CanonicalStoryResourceLifecycleException>(
            () => new CanonicalStoryResourceLifecycleService(failing).DeleteOwnedTask("story", "task"));

        Assert.AreEqual("story.resource.lifecycle.membership_replace_failed", exception.Code);
        CollectionAssert.AreEqual(resourceBytes, File.ReadAllBytes(resourcePath));
        CollectionAssert.AreEqual(membershipBytes, File.ReadAllBytes(membershipPath));
        Assert.IsNotNull(exception.InnerException);
    }

    [TestMethod]
    public void StoryKindAndInvalidOwnershipAreRejectedWithStableCodes()
    {
        using var project = NewProject();
        var store = new CanonicalProjectGraphStore(project.Root);
        CreateStory(store, "story");
        var service = new CanonicalStoryResourceLifecycleService(store);

        AssertCode(() => service.CreateOwned("story", GraphResourceKind.Story, "bad", "Bad"),
            "story.resource.kind.unsupported");
        AssertCode(() => service.AddReference("story", GraphResourceKind.Task, "missing"),
            "story.resource.resource.not_found");
        AssertCode(() => service.RemoveReference("story", GraphResourceKind.Task, "missing"),
            "story.resource.reference.not_found");
    }

    private static TestProjectDirectory NewProject() => new(createProjectFile: false);

    private static void CreateStory(CanonicalProjectGraphStore store, string id)
    {
        store.Stories.Create(new GraphResourceEnvelope(
            GraphResourceKind.Story, id, id, new GraphDocument([new GraphNode("start", "start", "Start")])));
        store.Memberships.Create(new CanonicalStoryMembershipManifest(id));
    }

    private static void AssertCode(Action action, string code)
        => Assert.AreEqual(code, Assert.ThrowsExactly<CanonicalStoryResourceLifecycleException>(action).Code);

    private sealed class FailOnWrite(int failureNumber) : IAtomicFileWriter
    {
        private int _writes;

        public void Write(string destinationPath, string contents, Action<string>? validateTemporaryFile = null)
        {
            if (++_writes == failureNumber) throw new IOException("simulated membership failure");
            new AtomicFileWriter().Write(destinationPath, contents, validateTemporaryFile);
        }
    }

    private sealed class AlwaysFailingWriter : IAtomicFileWriter
    {
        public void Write(string destinationPath, string contents, Action<string>? validateTemporaryFile = null)
            => throw new IOException("simulated membership failure");
    }
}
