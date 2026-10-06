using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Editing;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.IO;
using System.Text.Json;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CanonicalStoryResourceLifecycleServiceTests
{
    [TestMethod]
    public void CreatesScopeValidBlankSessionAndTaskAsOwned()
    {
        using var project = NewProject();
        var store = new CanonicalProjectGraphStore(project.Root);
        CreateStory(store, "ST-2345-6789-ABCD-EFGH");
        var service = new CanonicalStoryResourceLifecycleService(store);

        var session = service.CreateOwned("ST-2345-6789-ABCD-EFGH", GraphResourceKind.Session, "ST-2345-6789-ABCD-EFGH~session~session", "Session");
        var task = service.CreateOwned("ST-2345-6789-ABCD-EFGH", GraphResourceKind.Task, "ST-2345-6789-ABCD-EFGH~task~task", "Task");

        Assert.IsTrue(GraphScopePolicy.IsValid(session.Graph!, GraphScope.Session));
        Assert.IsTrue(GraphScopePolicy.IsValid(task.Graph!, GraphScope.Task));
        CollectionAssert.AreEqual(new[] { "start" }, session.Graph!.Nodes.Select(node => node.Id).ToArray());
        CollectionAssert.AreEqual(new[] { "objective" }, task.Graph!.Nodes.Select(node => node.Id).ToArray());
        var objective = task.Graph.Nodes.Single();
        Assert.AreEqual("objective", objective.Type);
        Assert.HasCount(1, objective.Ports);
        Assert.IsTrue(objective.Ports[0].IsOutput);
        Assert.AreEqual(GraphInterfaceKind.Logic, objective.Ports[0].InterfaceKind);
        Assert.AreEqual(CanonicalTaskObjectiveSchema.CompletionPortId, objective.Ports[0].Id);
        Assert.AreEqual("", objective.Properties[CanonicalTaskObjectiveSchema.EntityProperty].GetString());
        Assert.IsEmpty(task.Graph.Connections);
        CollectionAssert.AreEqual(new[] { "ST-2345-6789-ABCD-EFGH~session~session" }, store.Memberships.Load("ST-2345-6789-ABCD-EFGH").OwnedResources.Sessions);
        CollectionAssert.AreEqual(new[] { "ST-2345-6789-ABCD-EFGH~task~task" }, store.Memberships.Load("ST-2345-6789-ABCD-EFGH").OwnedResources.Tasks);
    }

    [TestMethod]
    public void ReferenceAndRemovalPreserveOtherMembershipOrder()
    {
        using var project = NewProject();
        var store = new CanonicalProjectGraphStore(project.Root);
        CreateStory(store, "ST-2345-6789-ABCD-EFGH");
        CreateStory(store, "ST-JKLM-NPQR-STUV-WXYZ");
        var service = new CanonicalStoryResourceLifecycleService(store);
        service.CreateOwnedSession("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~session~before", "Before");
        service.CreateOwnedSession("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~session~shared", "Shared");
        service.CreateOwnedSession("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~session~after", "After");
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "ST-JKLM-NPQR-STUV-WXYZ",
            referencedResources: new CanonicalStoryMembershipSet { Sessions = ["ST-2345-6789-ABCD-EFGH~session~before", "ST-2345-6789-ABCD-EFGH~session~shared", "ST-2345-6789-ABCD-EFGH~session~after"] }));

        AssertCode(() => service.AddSessionReference("ST-JKLM-NPQR-STUV-WXYZ", "ST-2345-6789-ABCD-EFGH~session~shared"),
            "story.resource.reference.duplicate");
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "ST-JKLM-NPQR-STUV-WXYZ",
            referencedResources: new CanonicalStoryMembershipSet { Sessions = ["ST-2345-6789-ABCD-EFGH~session~before", "ST-2345-6789-ABCD-EFGH~session~after"] }));
        service.AddSessionReference("ST-JKLM-NPQR-STUV-WXYZ", "ST-2345-6789-ABCD-EFGH~session~shared");
        CollectionAssert.AreEqual(new[] { "ST-2345-6789-ABCD-EFGH~session~before", "ST-2345-6789-ABCD-EFGH~session~after", "ST-2345-6789-ABCD-EFGH~session~shared" },
            store.Memberships.Load("ST-JKLM-NPQR-STUV-WXYZ").ReferencedResources.Sessions);
        service.RemoveSessionReference("ST-JKLM-NPQR-STUV-WXYZ", "ST-2345-6789-ABCD-EFGH~session~shared");
        CollectionAssert.AreEqual(new[] { "ST-2345-6789-ABCD-EFGH~session~before", "ST-2345-6789-ABCD-EFGH~session~after" },
            store.Memberships.Load("ST-JKLM-NPQR-STUV-WXYZ").ReferencedResources.Sessions);
    }

    [TestMethod]
    public void DeletionPlanBlocksOtherStoryAndSuccessfulDeleteRemovesOwnerOnly()
    {
        using var project = NewProject();
        var store = new CanonicalProjectGraphStore(project.Root);
        CreateStory(store, "ST-2345-6789-ABCD-EFGH");
        CreateStory(store, "ST-JKLM-NPQR-STUV-WXYZ");
        var service = new CanonicalStoryResourceLifecycleService(store);
        service.CreateOwnedTask("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~task~task", "Task");
        service.AddTaskReference("ST-JKLM-NPQR-STUV-WXYZ", "ST-2345-6789-ABCD-EFGH~task~task");

        var plan = service.GetDeletionPlan("ST-2345-6789-ABCD-EFGH", GraphResourceKind.Task, "ST-2345-6789-ABCD-EFGH~task~task");
        Assert.IsFalse(plan.CanDelete);
        CollectionAssert.AreEqual(new[] { "ST-JKLM-NPQR-STUV-WXYZ" }, plan.ReferencingStoryIds.ToArray());
        AssertCode(() => service.DeleteOwnedTask("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~task~task"), "story.resource.delete.blocked");

        service.RemoveTaskReference("ST-JKLM-NPQR-STUV-WXYZ", "ST-2345-6789-ABCD-EFGH~task~task");
        service.DeleteOwnedTask("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~task~task");
        Assert.IsFalse(File.Exists(store.Tasks.GetPath("ST-2345-6789-ABCD-EFGH~task~task")));
        Assert.IsEmpty(store.Memberships.Load("ST-2345-6789-ABCD-EFGH").OwnedResources.Tasks);
    }

    [TestMethod]
    public void DuplicateOwnershipInAnotherStoryIsRejectedWithoutChangingEitherMembership()
    {
        using var project = NewProject();
        var store = new CanonicalProjectGraphStore(project.Root);
        CreateStory(store, "ST-2345-6789-ABCD-EFGH");
        CreateStory(store, "ST-JKLM-NPQR-STUV-WXYZ");
        var service = new CanonicalStoryResourceLifecycleService(store);
        service.CreateOwnedSession("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~session~shared", "Shared");
        var previous = File.ReadAllBytes(store.Memberships.GetPath("ST-JKLM-NPQR-STUV-WXYZ"));
        Assert.Throws<CanonicalStoryMembershipRepositoryException>(() => store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "ST-JKLM-NPQR-STUV-WXYZ",
            ownedResources: new CanonicalStoryMembershipSet { Sessions = ["ST-2345-6789-ABCD-EFGH~session~shared"] })));
        CollectionAssert.AreEqual(previous, File.ReadAllBytes(store.Memberships.GetPath("ST-JKLM-NPQR-STUV-WXYZ")));
        Assert.IsTrue(File.Exists(store.Sessions.GetPath("ST-2345-6789-ABCD-EFGH~session~shared")));
        CollectionAssert.Contains(store.Memberships.Load("ST-2345-6789-ABCD-EFGH").OwnedResources.Sessions, "ST-2345-6789-ABCD-EFGH~session~shared");
    }

    [TestMethod]
    public void RemovesMissingReferencedResourceMembershipForRepair()
    {
        using var project = NewProject();
        var store = new CanonicalProjectGraphStore(project.Root);
        CreateStory(store, "ST-2345-6789-ABCD-EFGH");
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "ST-2345-6789-ABCD-EFGH",
            referencedResources: new CanonicalStoryMembershipSet { Tasks = ["ST-JKLM-NPQR-STUV-WXYZ~task~missing_task"] }));

        new CanonicalStoryResourceLifecycleService(store).RemoveTaskReference("ST-2345-6789-ABCD-EFGH", "ST-JKLM-NPQR-STUV-WXYZ~task~missing_task");

        Assert.IsEmpty(store.Memberships.Load("ST-2345-6789-ABCD-EFGH").ReferencedResources.Tasks);
        Assert.IsFalse(File.Exists(store.Tasks.GetPath("ST-JKLM-NPQR-STUV-WXYZ~task~missing_task")));
    }

    [TestMethod]
    public void RemovingReferenceCleansEveryAggregatePlacementAndIncidentConnectionOnly()
    {
        using var project = NewProject();
        var store = new CanonicalProjectGraphStore(project.Root);
        CreateStory(store, "ST-2345-6789-ABCD-EFGH");
        CreateStory(store, "ST-JKLM-NPQR-STUV-WXYZ");
        var service = new CanonicalStoryResourceLifecycleService(store);
        service.CreateOwnedTask("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~task~task", "Task");
        service.AddTaskReference("ST-JKLM-NPQR-STUV-WXYZ", "ST-2345-6789-ABCD-EFGH~task~task");
        ReplaceStoryWithPlacements(store, "ST-JKLM-NPQR-STUV-WXYZ", GraphResourceKind.Task, "ST-2345-6789-ABCD-EFGH~task~task");

        service.RemoveTaskReference("ST-JKLM-NPQR-STUV-WXYZ", "ST-2345-6789-ABCD-EFGH~task~task");

        var graph = store.Stories.Load("ST-JKLM-NPQR-STUV-WXYZ").Graph!;
        CollectionAssert.AreEqual(new[] { "keep" }, graph.Nodes.Select(node => node.Id).ToArray());
        Assert.IsEmpty(graph.Connections);
        Assert.IsTrue(File.Exists(store.Tasks.GetPath("ST-2345-6789-ABCD-EFGH~task~task")), "Removing a placement/reference must not delete the resource.");
    }

    [TestMethod]
    public void DeletingAggregatePlacementPreservesOwnedResourceAndMembership()
    {
        using var project = NewProject();
        var store = new CanonicalProjectGraphStore(project.Root);
        CreateStory(store, "ST-2345-6789-ABCD-EFGH");
        var service = new CanonicalStoryResourceLifecycleService(store);
        service.CreateOwnedSession("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~session~session", "Session");
        ReplaceStoryWithPlacements(store, "ST-2345-6789-ABCD-EFGH", GraphResourceKind.Session, "ST-2345-6789-ABCD-EFGH~session~session");
        var story = store.Stories.Load("ST-2345-6789-ABCD-EFGH");
        var session = new GraphEditSession(story.Graph!, GraphScope.StoryFlow);

        Assert.IsTrue(session.RemoveNode("placement-a", confirmReferencedRemoval: true));
        story.Graph = session.Document;
        store.Stories.Replace(story);

        Assert.IsFalse(store.Stories.Load("ST-2345-6789-ABCD-EFGH").Graph!.Nodes.Any(node => node.Id == "placement-a"));
        Assert.IsTrue(File.Exists(store.Sessions.GetPath("ST-2345-6789-ABCD-EFGH~session~session")));
        CollectionAssert.Contains(store.Memberships.Load("ST-2345-6789-ABCD-EFGH").OwnedResources.Sessions, "ST-2345-6789-ABCD-EFGH~session~session");
    }

    [TestMethod]
    public void DeletingOwnedResourceCleansEveryOwnerPlacementAndIncidentConnection()
    {
        using var project = NewProject();
        var store = new CanonicalProjectGraphStore(project.Root);
        CreateStory(store, "ST-2345-6789-ABCD-EFGH");
        var service = new CanonicalStoryResourceLifecycleService(store);
        service.CreateOwnedSession("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~session~session", "Session");
        ReplaceStoryWithPlacements(store, "ST-2345-6789-ABCD-EFGH", GraphResourceKind.Session, "ST-2345-6789-ABCD-EFGH~session~session");

        service.DeleteOwnedSession("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~session~session");

        var graph = store.Stories.Load("ST-2345-6789-ABCD-EFGH").Graph!;
        CollectionAssert.AreEqual(new[] { "keep" }, graph.Nodes.Select(node => node.Id).ToArray());
        Assert.IsEmpty(graph.Connections);
        Assert.IsFalse(File.Exists(store.Sessions.GetPath("ST-2345-6789-ABCD-EFGH~session~session")));
    }

    [TestMethod]
    public void PlacementCleanupRollsBackStoryWhenMembershipWriteFails()
    {
        using var project = NewProject();
        var normal = new CanonicalProjectGraphStore(project.Root);
        CreateStory(normal, "ST-2345-6789-ABCD-EFGH");
        CreateStory(normal, "ST-JKLM-NPQR-STUV-WXYZ");
        var normalService = new CanonicalStoryResourceLifecycleService(normal);
        normalService.CreateOwnedTask("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~task~task", "Task");
        normalService.AddTaskReference("ST-JKLM-NPQR-STUV-WXYZ", "ST-2345-6789-ABCD-EFGH~task~task");
        ReplaceStoryWithPlacements(normal, "ST-JKLM-NPQR-STUV-WXYZ", GraphResourceKind.Task, "ST-2345-6789-ABCD-EFGH~task~task");
        var storyPath = normal.Stories.GetPath("ST-JKLM-NPQR-STUV-WXYZ");
        var membershipPath = normal.Memberships.GetPath("ST-JKLM-NPQR-STUV-WXYZ");
        var storyBytes = File.ReadAllBytes(storyPath);
        var membershipBytes = File.ReadAllBytes(membershipPath);

        var failing = new CanonicalProjectGraphStore(project.Root, new FailOnWrite(2));
        AssertCode(() => new CanonicalStoryResourceLifecycleService(failing)
            .RemoveTaskReference("ST-JKLM-NPQR-STUV-WXYZ", "ST-2345-6789-ABCD-EFGH~task~task"), "story.resource.lifecycle.membership_replace_failed");

        CollectionAssert.AreEqual(storyBytes, File.ReadAllBytes(storyPath));
        CollectionAssert.AreEqual(membershipBytes, File.ReadAllBytes(membershipPath));
    }

    [TestMethod]
    public void CreationMembershipFailureRemovesJustCreatedResource()
    {
        using var project = NewProject();
        var normal = new CanonicalProjectGraphStore(project.Root);
        CreateStory(normal, "ST-2345-6789-ABCD-EFGH");
        var failing = new CanonicalProjectGraphStore(project.Root, new FailOnWrite(2));
        var service = new CanonicalStoryResourceLifecycleService(failing);

        AssertCode(() => service.CreateOwnedSession("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~session~orphan", "Orphan"),
            "story.resource.lifecycle.membership_replace_failed");
        Assert.IsFalse(File.Exists(failing.Sessions.GetPath("ST-2345-6789-ABCD-EFGH~session~orphan")));
        Assert.IsEmpty(failing.Memberships.Load("ST-2345-6789-ABCD-EFGH").OwnedResources.Sessions);
    }

    [TestMethod]
    public void DeletionMembershipFailureRestoresExactResourceAndMembership()
    {
        using var project = NewProject();
        var normal = new CanonicalProjectGraphStore(project.Root);
        CreateStory(normal, "ST-2345-6789-ABCD-EFGH");
        var normalService = new CanonicalStoryResourceLifecycleService(normal);
        normalService.CreateOwnedSession("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~session~session", "Session");
        var resourcePath = normal.Sessions.GetPath("ST-2345-6789-ABCD-EFGH~session~session");
        var membershipPath = normal.Memberships.GetPath("ST-2345-6789-ABCD-EFGH");
        var resourceBytes = File.ReadAllBytes(resourcePath);
        var membershipBytes = File.ReadAllBytes(membershipPath);

        var failing = new CanonicalProjectGraphStore(project.Root, new FailOnWrite(1));
        var service = new CanonicalStoryResourceLifecycleService(failing);
        AssertCode(() => service.DeleteOwnedSession("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~session~session"),
            "story.resource.lifecycle.membership_replace_failed");
        CollectionAssert.AreEqual(resourceBytes, File.ReadAllBytes(resourcePath));
        CollectionAssert.AreEqual(membershipBytes, File.ReadAllBytes(membershipPath));
    }

    [TestMethod]
    public void AlwaysFailingMembershipWriterStillRestoresAndReportsBothFailures()
    {
        using var project = NewProject();
        var normal = new CanonicalProjectGraphStore(project.Root);
        CreateStory(normal, "ST-2345-6789-ABCD-EFGH");
        var normalService = new CanonicalStoryResourceLifecycleService(normal);
        normalService.CreateOwnedTask("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~task~task", "Task");
        var resourcePath = normal.Tasks.GetPath("ST-2345-6789-ABCD-EFGH~task~task");
        var membershipPath = normal.Memberships.GetPath("ST-2345-6789-ABCD-EFGH");
        var resourceBytes = File.ReadAllBytes(resourcePath);
        var membershipBytes = File.ReadAllBytes(membershipPath);

        var failing = new CanonicalProjectGraphStore(project.Root, new AlwaysFailingWriter());
        var exception = Assert.ThrowsExactly<CanonicalStoryResourceLifecycleException>(
            () => new CanonicalStoryResourceLifecycleService(failing).DeleteOwnedTask("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~task~task"));

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
        CreateStory(store, "ST-2345-6789-ABCD-EFGH");
        var service = new CanonicalStoryResourceLifecycleService(store);

        AssertCode(() => service.CreateOwned("ST-2345-6789-ABCD-EFGH", GraphResourceKind.Story, "bad", "Bad"),
            "story.resource.kind.unsupported");
        AssertCode(() => service.AddReference("ST-2345-6789-ABCD-EFGH", GraphResourceKind.Task, "ST-JKLM-NPQR-STUV-WXYZ~task~missing"),
            "story.resource.resource.not_found");
        AssertCode(() => service.RemoveReference("ST-2345-6789-ABCD-EFGH", GraphResourceKind.Task, "ST-JKLM-NPQR-STUV-WXYZ~task~missing"),
            "story.resource.reference.not_found");
    }

    private static TestProjectDirectory NewProject() => new(createProjectFile: false);

    private static void CreateStory(CanonicalProjectGraphStore store, string id)
    {
        store.Stories.Create(new GraphResourceEnvelope(
            GraphResourceKind.Story, id, id, new GraphDocument([new GraphNode("start", "start", "Start")])));
        store.Memberships.Create(new CanonicalStoryMembershipManifest(id));
    }

    private static void ReplaceStoryWithPlacements(CanonicalProjectGraphStore store, string storyId,
        GraphResourceKind kind, string resourceId)
    {
        var type = kind == GraphResourceKind.Session ? "session" : "task";
        GraphNode Placement(string id)
            => new(id, type, id, properties: new Dictionary<string, JsonElement>(StringComparer.Ordinal)
            {
                ["resource_id"] = JsonSerializer.SerializeToElement(resourceId),
            });
        var graph = new GraphDocument(
            [Placement("placement-a"), new GraphNode("keep", "action", "Keep"), Placement("placement-b")],
            [new GraphConnection("placement-a", "out", "keep", "in", GraphInterfaceKind.Flow),
             new GraphConnection("keep", "out", "placement-b", "in", GraphInterfaceKind.Flow)]);
        store.Stories.Replace(new GraphResourceEnvelope(GraphResourceKind.Story, storyId, storyId, graph));
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
