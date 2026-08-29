using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CanonicalProjectStoryGraphServiceTests
{
    [TestMethod]
    public void AggregatesValidEnterStoryTransitionsAndRetainsIncomingSources()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        CreateStory(store, "alpha", "Alpha", [
            Enter("go_b", "beta"), Enter("go_b_2", "beta")], [
            new("source_b", "branch", "go_b", "flow_in", GraphInterfaceKind.Flow),
            new("source_a", "other", "go_b_2", "flow_in", GraphInterfaceKind.Flow)]);
        CreateStory(store, "beta", "Beta");
        foreach (var id in new[] { "alpha", "beta" }) store.Memberships.Create(new(id));

        var snapshot = new CanonicalProjectStoryGraphService(store).Derive();

        var edge = snapshot.Edges.Single();
        Assert.AreEqual("alpha", edge.SourceStoryId);
        Assert.AreEqual("beta", edge.TargetStoryId);
        Assert.AreEqual(2, edge.TransitionCount);
        CollectionAssert.AreEqual(new[] { "go_b", "go_b_2" }, edge.EnterStoryNodeIds.ToArray());
        CollectionAssert.AreEqual(new[] { "source_a", "source_b" }, edge.IncomingSourceNodeIds.ToArray());
        Assert.IsTrue(edge.Transitions[0].IncomingSourceNodeIds.SequenceEqual(["source_b"]));
        Assert.IsEmpty(snapshot.Diagnostics.Where(issue => issue.Code == "project_graph.target.missing"));
    }

    [TestMethod]
    public void MissingBlankWrongKindAndUnknownTargetsFailClosed()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        CreateStory(store, "source", "Source", [
            new("missing", "enter_story", "Missing", properties: new Dictionary<string, JsonElement>()),
            Enter("blank", " "),
            new("wrong", "enter_story", "Wrong", properties: new Dictionary<string, JsonElement>
            {
                ["target_story_id"] = JsonSerializer.SerializeToElement(42),
            }),
            Enter("unknown", "does_not_exist")]);
        store.Memberships.Create(new("source"));

        var snapshot = new CanonicalProjectStoryGraphService(store).Derive();

        Assert.IsEmpty(snapshot.Edges);
        CollectionAssert.AreEquivalent(
            new[] { "project_graph.target.missing", "project_graph.target.missing", "project_graph.target.invalid", "project_graph.target.missing" },
            snapshot.Diagnostics.Where(issue => issue.StoryId == "source" && issue.NodeId is not null)
                .Select(issue => issue.Code).ToArray());
    }

    [TestMethod]
    public void DiscoveryUnionCyclesAndIsolatedStoriesAreRepresented()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        CreateStory(store, "a", "A", [Enter("to_b", "b")]);
        CreateStory(store, "b", "B", [Enter("to_a", "a")]);
        store.Memberships.Create(new("a"));
        store.Memberships.Create(new("b"));
        store.Memberships.Create(new("membership_only"));

        var snapshot = new CanonicalProjectStoryGraphService(store).Derive();

        CollectionAssert.AreEqual(new[] { "a", "b", "membership_only" }, snapshot.Nodes.Select(node => node.Id).ToArray());
        CollectionAssert.AreEquivalent(new[] { "a", "b" },
            snapshot.Diagnostics.Where(issue => issue.Code == "project_graph.story.cycle").Select(issue => issue.StoryId).ToArray());
        Assert.IsTrue(snapshot.Diagnostics.Any(issue => issue.Code == "story.discovery.root.missing" && issue.StoryId == "membership_only"));
        Assert.IsTrue(snapshot.Diagnostics.Any(issue => issue.Code == "project_graph.story.isolated" && issue.StoryId == "membership_only"));
    }

    [TestMethod]
    public void DuplicateOrBlankEnterStoryIdsAreAmbiguousAndDoNotCreateEdges()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        CreateStory(store, "source", "Source", [
            Enter("duplicate", "target"), Enter("duplicate", "target"), Enter("", "target")]);
        CreateStory(store, "target", "Target");
        store.Memberships.Create(new("source"));
        store.Memberships.Create(new("target"));

        var snapshot = new CanonicalProjectStoryGraphService(store).Derive();

        Assert.IsEmpty(snapshot.Edges);
        CollectionAssert.Contains(snapshot.Diagnostics.Select(issue => issue.Code).ToArray(), "project_graph.enter_story.ambiguous");
        CollectionAssert.Contains(snapshot.Diagnostics.Select(issue => issue.Code).ToArray(), "project_graph.enter_story.malformed");
    }

    [TestMethod]
    public void EnterStoryIdSharedByAnyOtherNodeIsAmbiguous()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        CreateStory(store, "source", "Source", [
            Enter("shared", "target"),
            new GraphNode("shared", "action", "Action")]);
        CreateStory(store, "target", "Target");
        store.Memberships.Create(new("source"));
        store.Memberships.Create(new("target"));

        var snapshot = new CanonicalProjectStoryGraphService(store).Derive();

        Assert.IsEmpty(snapshot.Edges);
        var issue = snapshot.Diagnostics.Single(item => item.Code == "project_graph.enter_story.ambiguous");
        Assert.AreEqual("shared", issue.NodeId);
        StringAssert.Contains(issue.Message, "shared with another graph node");
        Assert.IsFalse(issue.Message.Contains("multiple enter_story nodes", StringComparison.Ordinal));
    }

    [TestMethod]
    public void DerivationIsDeterministicAndDetached()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        CreateStory(store, "source", "Source", [Enter("z", "target"), Enter("a", "target")]);
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

    private static GraphNode Enter(string id, string target)
        => new(id, "enter_story", id, properties: new Dictionary<string, JsonElement>
        {
            ["target_story_id"] = JsonSerializer.SerializeToElement(target),
        });
}
