using System.Text.Json;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.Tests;

// Current equivalents of the retired flat Story deletion/migration contracts.
[TestClass]
public sealed class StoryDeletionCoreTests
{
    private const string A = "ST-2345-6789-ABCD-EFGH", B = "ST-JKLM-NPQR-STUV-WXYZ", C = "ST-AAAA-BBBB-CCCC-DDDD";

    [TestMethod]
    public void DeleteEmptyStoryPreservesOtherStoryAndDoesNotRecreateDefault()
    {
        using var directory = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(directory.Root);
        var service = new CanonicalStoryLifecycleService(store);
        service.Create(A, "Delete"); service.Create(B, "Keep");
        var before = File.ReadAllBytes(store.Stories.GetPath(B));
        service.Delete(A);
        Assert.IsFalse(File.Exists(store.Stories.GetPath(A)));
        Assert.IsFalse(File.Exists(store.Memberships.GetPath(A)));
        new ProjectService().OpenProject(directory.Root);
        CollectionAssert.AreEqual(before, File.ReadAllBytes(store.Stories.GetPath(B)));
        Assert.AreEqual(1, store.Stories.List().Count);
        Assert.IsFalse(File.Exists(Path.Combine(directory.Root, "stories", "uncategorized.json")));
    }

    [TestMethod]
    public void DeleteLastStoryLeavesEmptyCurrentProjectAfterReopen()
    {
        using var directory = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(directory.Root);
        var service = new CanonicalStoryLifecycleService(store);
        service.Create(A, "Only"); service.Delete(A);
        new ProjectService().OpenProject(directory.Root);
        Assert.IsEmpty(store.Stories.List());
    }

    [TestMethod]
    public void StoryDeletionIncludesAllOwnedResourceKinds()
    {
        using var directory = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(directory.Root);
        var service = new CanonicalStoryLifecycleService(store); service.Create(A, "Owner");
        var actors = new CanonicalStoryActorLifecycleService(store);
        actors.CreateOwned(A, A + "~actor~guard", "Guard");
        var resources = new CanonicalStoryResourceLifecycleService(store);
        resources.CreateOwnedSession(A, A + "~session~talk", "Talk");
        resources.CreateOwnedTask(A, A + "~task~job", "Job");
        var plan = service.GetDeletionPlan(A);
        CollectionAssert.AreEqual(new[] { A + "~actor~guard" }, plan.ActorIds.ToArray());
        CollectionAssert.AreEqual(new[] { A + "~session~talk" }, plan.SessionIds.ToArray());
        CollectionAssert.AreEqual(new[] { A + "~task~job" }, plan.TaskIds.ToArray());
        service.Delete(A);
        foreach (var path in new[] { actors.Actors.GetActorPath(A + "~actor~guard"), store.Sessions.GetPath(A + "~session~talk"), store.Tasks.GetPath(A + "~task~job") })
            Assert.IsFalse(File.Exists(path));
    }

    [TestMethod]
    public void DeletingGroupMemberRemovesOnlyItsEdgesAndLeavesValidGraph()
    {
        using var directory = new TestProjectDirectory();
        var store = Connected(directory.Root);
        var keep = File.ReadAllBytes(store.Stories.GetPath(C));
        new CanonicalStoryLifecycleService(store).Delete(B);
        var edge = store.StoryLogicGraph.Load().Connections.Single();
        Assert.AreEqual(A, edge.SourceStoryId); Assert.AreEqual(C, edge.TargetStoryId);
        CollectionAssert.AreEqual(keep, File.ReadAllBytes(store.Stories.GetPath(C)));
        new ProjectService().OpenProject(directory.Root);
    }

    [TestMethod]
    public void FailedGroupMemberDeletionRestoresRootsAndEdges()
    {
        using var directory = new TestProjectDirectory();
        var store = Connected(directory.Root);
        var paths = new[] { store.Stories.GetPath(B), store.Memberships.GetPath(B), store.StoryLogicGraph.Path };
        var bytes = paths.Select(File.ReadAllBytes).ToArray();
        var calls = 0;
        var service = new CanonicalStoryLifecycleService(store, deleteFile: path =>
        {
            if (++calls == 2) throw new IOException("injected delete failure");
            File.Delete(path);
        });
        Assert.ThrowsExactly<CanonicalStoryLifecycleException>(() => service.Delete(B));
        for (var i = 0; i < paths.Length; i++) CollectionAssert.AreEqual(bytes[i], File.ReadAllBytes(paths[i]));
        Assert.AreEqual(2, store.StoryLogicGraph.Load().Connections.Count);
    }

    [TestMethod]
    public void RepositoryDeleteRejectsTraversalBeforeResolvingPath()
    {
        using var directory = new TestProjectDirectory();
        var outside = Path.Combine(directory.Root, "outside.json"); File.WriteAllText(outside, "preserve");
        var service = new CanonicalStoryLifecycleService(new CanonicalProjectGraphStore(directory.Root));
        Assert.ThrowsExactly<CanonicalStoryLifecycleException>(() => service.Delete("../outside"));
        Assert.AreEqual("preserve", File.ReadAllText(outside));
    }

    [TestMethod]
    public void LegacyMembershipIsNotAutomaticallyRepairedOrMigrated()
    {
        using var directory = new TestProjectDirectory();
        var path = Path.Combine(directory.Root, "stories", "uncategorized.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        const string legacy = "{\"schema_version\":2,\"id\":\"uncategorized\",\"owned_resources\":{\"actors\":[\"guard\"]}}";
        File.WriteAllText(path, legacy);
        Assert.ThrowsExactly<ProjectException>(() => new ProjectService().OpenProject(directory.Root));
        Assert.AreEqual(legacy, File.ReadAllText(path));
        Assert.IsFalse(File.Exists(Path.Combine(directory.Root, "migration.log")));
    }

    [TestMethod]
    public void ExternalResourceReferenceBlocksCascadeWithoutChangingFiles()
    {
        using var directory = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(directory.Root);
        var stories = new CanonicalStoryLifecycleService(store); stories.Create(A, "Owner"); stories.Create(B, "Consumer");
        var actors = new CanonicalStoryActorLifecycleService(store);
        actors.CreateOwned(A, A + "~actor~guard", "Guard"); actors.AddReference(B, A + "~actor~guard");
        var path = actors.Actors.GetActorPath(A + "~actor~guard"); var before = File.ReadAllBytes(path);
        Assert.IsFalse(stories.GetDeletionPlan(A).CanDelete);
        Assert.ThrowsExactly<CanonicalStoryLifecycleException>(() => stories.Delete(A));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(path));
        Assert.IsTrue(File.Exists(store.Stories.GetPath(A)));
    }

    private static CanonicalProjectGraphStore Connected(string root)
    {
        var store = new CanonicalProjectGraphStore(root); var stories = new CanonicalStoryLifecycleService(store);
        foreach (var uid in new[] { A, B, C })
        {
            stories.Create(uid, uid);
            var story = store.Stories.Load(uid);
            var node = GraphNodeFactory.Create(GraphScope.StoryFlow, uid == A ? "logic_output" : "logic_input", "boundary");
            node.Properties["port_id"] = JsonSerializer.SerializeToElement("edge");
            node.Properties["display_name"] = JsonSerializer.SerializeToElement("Edge");
            var graph = story.Graph!; graph.Nodes.Add(node); story.Graph = graph; store.Stories.Replace(story);
        }
        store.StoryLogicGraph.Save([new(A, "edge", B, "edge"), new(A, "edge", C, "edge")]);
        return store;
    }
}
