using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CanonicalProjectStoryGraphServiceTests
{
    [TestMethod]
    public void DiscoveryUnionAndIsolatedStoriesAreRepresented()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        CreateStory(store, "a", "A");
        CreateStory(store, "b", "B");
        store.Memberships.Create(new("a"));
        store.Memberships.Create(new("b"));
        store.Memberships.Create(new("membership_only"));

        var snapshot = new CanonicalProjectStoryGraphService(store).Derive();

        CollectionAssert.AreEqual(new[] { "a", "b", "membership_only" }, snapshot.Nodes.Select(node => node.Id).ToArray());
        Assert.IsEmpty(snapshot.Edges);
        Assert.IsFalse(snapshot.Diagnostics.Any(issue => issue.Code == "project_graph.story.cycle"));
        Assert.IsTrue(snapshot.Diagnostics.Any(issue => issue.Code == "story.discovery.root.missing" && issue.StoryId == "membership_only"));
        Assert.IsTrue(snapshot.Diagnostics.Any(issue => issue.Code == "project_graph.story.isolated" && issue.StoryId == "membership_only"));
    }

    [TestMethod]
    public void DerivationIsDeterministicAndDetached()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        CreateStory(store, "source", "Source");
        CreateStory(store, "target", "Target");
        store.Memberships.Create(new("source"));
        store.Memberships.Create(new("target"));
        var service = new CanonicalProjectStoryGraphService(store);
        var source = store.Stories.Load("source");

        var first = service.Derive();
        var second = service.Derive();
        CollectionAssert.AreEqual(first.Edges.Select(edge => string.Join("/", edge.EnterStoryNodeIds)).ToArray(),
            second.Edges.Select(edge => string.Join("/", edge.EnterStoryNodeIds)).ToArray());
        source.DisplayName = "Mutated source envelope";
        source.Graph!.Nodes.Clear();
        Assert.AreEqual("Source", first.Nodes.Single(node => node.Id == "source").DisplayName);
        Assert.AreEqual("Source", service.Derive().Nodes.Single(node => node.Id == "source").DisplayName);
        Assert.ThrowsExactly<NotSupportedException>(() => ((IList<CanonicalProjectStoryGraphNode>)first.Nodes)
            .Add(first.Nodes[0]));
    }

    [TestMethod]
    public void AbsentRootInspectionDoesNotCreateCanonicalDirectories()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);

        var snapshot = new CanonicalProjectStoryGraphService(store).Derive();

        Assert.IsEmpty(snapshot.Nodes);
        Assert.IsFalse(Directory.Exists(store.CanonicalDirectory));
    }

    private static void CreateStory(
        CanonicalProjectGraphStore store,
        string id,
        string? displayName = null,
        IEnumerable<GraphNode>? nodes = null,
        IEnumerable<GraphConnection>? connections = null)
        => store.Stories.Create(new GraphResourceEnvelope(
            GraphResourceKind.Story,
            id,
            displayName ?? id,
            new GraphDocument(nodes ?? [], connections ?? [])));

}
