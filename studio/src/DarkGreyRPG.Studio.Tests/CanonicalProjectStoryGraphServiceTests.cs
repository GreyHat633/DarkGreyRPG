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
        CreateStory(store, "ST-2345-6789-ABCD-EFGH", "A");
        CreateStory(store, "ST-3456-789A-BCDE-FGHJ", "B");
        store.Memberships.Create(new("ST-2345-6789-ABCD-EFGH"));
        store.Memberships.Create(new("ST-3456-789A-BCDE-FGHJ"));
        store.Memberships.Create(new("ST-JKLM-NPQR-STUV-WXYZ"));

        var snapshot = new CanonicalProjectStoryGraphService(store).Derive();

        CollectionAssert.AreEqual(new[] { "ST-2345-6789-ABCD-EFGH", "ST-3456-789A-BCDE-FGHJ", "ST-JKLM-NPQR-STUV-WXYZ" }, snapshot.Nodes.Select(node => node.Id).ToArray());
        Assert.IsEmpty(snapshot.Edges);
        Assert.IsFalse(snapshot.Diagnostics.Any(issue => issue.Code == "project_graph.story.cycle"));
        Assert.IsTrue(snapshot.Diagnostics.Any(issue => issue.Code == "story.discovery.root.missing" && issue.StoryId == "ST-JKLM-NPQR-STUV-WXYZ"));
        Assert.IsFalse(snapshot.Diagnostics.Any(issue => issue.Code == "project_graph.story.isolated"));
    }

    [TestMethod]
    public void DerivationIsDeterministicAndDetached()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        CreateStory(store, "ST-2345-6789-ABCD-EFGH", "Source");
        CreateStory(store, "ST-JKLM-NPQR-STUV-WXYZ", "Target");
        store.Memberships.Create(new("ST-2345-6789-ABCD-EFGH"));
        store.Memberships.Create(new("ST-JKLM-NPQR-STUV-WXYZ"));
        var service = new CanonicalProjectStoryGraphService(store);
        var source = store.Stories.Load("ST-2345-6789-ABCD-EFGH");

        var first = service.Derive();
        var second = service.Derive();
        CollectionAssert.AreEqual(first.Edges.Select(edge => string.Join("/", edge.EnterStoryNodeIds)).ToArray(),
            second.Edges.Select(edge => string.Join("/", edge.EnterStoryNodeIds)).ToArray());
        source.DisplayName = "Mutated source envelope";
        source.Graph!.Nodes.Clear();
        Assert.AreEqual("Source", first.Nodes.Single(node => node.Id == "ST-2345-6789-ABCD-EFGH").DisplayName);
        Assert.AreEqual("Source", service.Derive().Nodes.Single(node => node.Id == "ST-2345-6789-ABCD-EFGH").DisplayName);
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
