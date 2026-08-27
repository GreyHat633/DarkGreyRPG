using System.Text.Json;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class M7ProjectGraphViewModelTests
{
    [TestMethod]
    public void DerivesOnlyValidEnterStoryEdgesAndReportsMissingAndIsolatedStories()
    {
        var main = Story("mystery", "王城迷案",
            Enter("kingdom_exit", "kingdom"), Enter("missing_exit", "missing_story"));
        var kingdom = Story("kingdom", "王国线");
        var isolated = Story("isolated", "孤立支线");

        var graph = new ProjectGraphViewModel([main, kingdom, isolated], "mystery");

        Assert.HasCount(1, graph.Edges);
        Assert.AreEqual("mystery", graph.Edges[0].SourceStoryId);
        Assert.AreEqual("kingdom", graph.Edges[0].TargetStoryId);
        Assert.IsTrue(graph.Diagnostics.Any(issue => issue.Code == "project_graph.target.missing" && issue.StoryId == "mystery"));
        Assert.IsTrue(graph.Diagnostics.Any(issue => issue.Code == "project_graph.story.isolated" && issue.StoryId == "isolated"));

        graph.SearchText = "王国";
        Assert.IsTrue(graph.Nodes.Single(node => node.Id == "kingdom").IsVisible);
        Assert.IsFalse(graph.Nodes.Single(node => node.Id == "mystery").IsVisible);
        graph.SearchText = string.Empty;
        graph.SelectedFilter = "孤立";
        Assert.IsTrue(graph.Nodes.Single(node => node.Id == "isolated").IsVisible);
        Assert.IsFalse(graph.Nodes.Single(node => node.Id == "kingdom").IsVisible);
    }

    [TestMethod]
    public void LayoutPersistsSeparatelyAndOpenRequestTargetsStoryFlow()
    {
        using var directory = new GraphDirectory();
        var stories = new[] { Story("mystery", "王城迷案", Enter("to_kingdom", "kingdom")), Story("kingdom", "王国线") };
        var graph = new ProjectGraphViewModel(stories, "mystery", directory.Root);
        string? opened = null;
        graph.OpenStoryRequested += (_, storyId) => opened = storyId;

        graph.MoveNode("kingdom", 777, 333);
        graph.OpenStoryFlow("kingdom");

        Assert.AreEqual("kingdom", opened);
        var layoutPath = Path.Combine(directory.Root, "resources", "editor", "story-graph-layout.json");
        Assert.IsTrue(File.Exists(layoutPath));
        Assert.IsFalse(File.Exists(Path.Combine(directory.Root, "stories", "kingdom.json")));
        var restored = new ProjectGraphViewModel(stories, "mystery", directory.Root);
        Assert.AreEqual(777, restored.Nodes.Single(node => node.Id == "kingdom").X);
        Assert.AreEqual(333, restored.Nodes.Single(node => node.Id == "kingdom").Y);
    }

    [TestMethod]
    public void DeletingEnterStoryRemovesDerivedEdgeOnRefresh()
    {
        var main = Story("mystery", "王城迷案", Enter("to_empire", "empire"));
        var empire = Story("empire", "帝国线");
        var before = new ProjectGraphViewModel([main, empire]);
        Assert.HasCount(1, before.Edges);

        main.Nodes.Clear();
        var after = new ProjectGraphViewModel([main, empire]);

        Assert.IsEmpty(after.Edges);
        Assert.IsTrue(after.Diagnostics.Any(issue => issue.Code == "project_graph.story.isolated" && issue.StoryId == "mystery"));
        Assert.IsTrue(after.Diagnostics.Any(issue => issue.Code == "project_graph.story.isolated" && issue.StoryId == "empire"));
    }

    [TestMethod]
    public void SavingFlowWithoutEnterStoryRemovesEdgeFromReloadedProjectGraph()
    {
        using var directory = new GraphDirectory();
        var project = new ProjectService().CreateProject(directory.Root, "m7_gate", "M7 Gate");
        project.Stories.CreateStory("mystery", "王城迷案");
        project.Stories.CreateStory("empire", "帝国线");
        var document = project.Stories.LoadStoryDocument("mystery");
        document.AddNode(Enter("to_empire", "empire"));
        project.Stories.SaveStory(document);
        Assert.HasCount(1, new ProjectGraphViewModel(project.Stories.ListStories()).Edges);

        Assert.IsTrue(document.RemoveNode("to_empire"));
        project.Stories.SaveStory(document);

        Assert.IsEmpty(new ProjectGraphViewModel(project.Stories.ListStories()).Edges);
    }

    [TestMethod]
    public void CycleDiagnosticsAreWarningsAndAutomaticLayoutTerminatesWithFinitePositions()
    {
        var alpha = Story("alpha", "Alpha", Enter("to_beta", "beta"));
        var beta = Story("beta", "Beta", Enter("to_alpha", "alpha"));
        var graph = new ProjectGraphViewModel([alpha, beta], "alpha");

        Assert.HasCount(2, graph.Edges);
        Assert.HasCount(2, graph.Diagnostics.Where(issue => issue.Code == "project_graph.story.cycle"));
        Assert.IsTrue(graph.Nodes.All(node => node.HasWarning));

        graph.AutoLayout();

        Assert.IsTrue(graph.Nodes.All(node => double.IsFinite(node.X) && double.IsFinite(node.Y)));
        Assert.AreEqual(graph.Nodes[0].X, graph.Nodes[1].X);
        Assert.AreNotEqual(graph.Nodes[0].Y, graph.Nodes[1].Y);
    }

    [TestMethod]
    public void SelfLoopProducesCycleWarningWithoutBlockingGraphConstruction()
    {
        var loop = Story("loop", "Loop", Enter("to_self", "loop"));
        var graph = new ProjectGraphViewModel([loop]);

        Assert.HasCount(1, graph.Edges);
        Assert.IsTrue(graph.Diagnostics.Any(issue => issue.Code == "project_graph.story.cycle" && issue.StoryId == "loop"));

        graph.AutoLayout();

        var node = graph.Nodes.Single();
        Assert.IsTrue(double.IsFinite(node.X) && double.IsFinite(node.Y));
    }

    [TestMethod]
    public void ParallelEnterStoryNodesAggregateWithStableBranchDetailsAndSelfLoopFlag()
    {
        var source = Story("source", "Source",
            Enter("z_enter", "target"), Enter("a_enter", "target"), Enter("m_enter", "target"));
        source.Connections.Add(new StoryConnectionResource { From = "branch", Output = "conceal", To = "z_enter" });
        source.Connections.Add(new StoryConnectionResource { From = "branch", Output = "accept", To = "z_enter" });
        source.Connections.Add(new StoryConnectionResource { From = "branch", Output = "accept", To = "z_enter" });
        source.Connections.Add(new StoryConnectionResource { From = "branch", Output = "fallback", To = "a_enter" });
        var target = Story("target", "Target");

        var graph = new ProjectGraphViewModel([source, target]);

        var edge = graph.Edges.Single();
        Assert.AreEqual(3, edge.Count);
        Assert.AreEqual("a_enter", edge.NodeId);
        CollectionAssert.AreEqual(new[] { "a_enter", "m_enter", "z_enter" }, edge.Transitions.Select(item => item.NodeId).ToArray());
        CollectionAssert.AreEqual(new[] { "accept", "conceal" }, edge.Transitions.Single(item => item.NodeId == "z_enter").IncomingBranchOutputs.ToArray());
        CollectionAssert.AreEqual(new[] { "a_enter", "m_enter", "z_enter" }, edge.EnterStoryNodeIds.ToArray());
        Assert.IsFalse(edge.IsSelfLoop);
        StringAssert.Contains(edge.Tooltip, "z_enter");

        var self = new ProjectGraphViewModel([Story("self", "Self", Enter("self_enter", "self"))]).Edges.Single();
        Assert.IsTrue(self.IsSelfLoop);
        Assert.AreEqual(1, self.Count);
    }

    [TestMethod]
    public void MissingTargetDiagnosticCarriesPreciseStoryAndEnterStoryNodeIds()
    {
        var graph = new ProjectGraphViewModel([Story("source", "Source", Enter("enter_missing", "missing"))]);

        var issue = graph.Diagnostics.Single(item => item.Code == "project_graph.target.missing");
        Assert.AreEqual("source", issue.StoryId);
        Assert.AreEqual("enter_missing", issue.NodeId);
        StringAssert.Contains(issue.Message, "source.enter_missing");
    }

    [TestMethod]
    public void ProblemFocusRequestMakesStoryVisibleAndCarriesStableSequence()
    {
        var graph = new ProjectGraphViewModel([Story("source", "Source"), Story("other", "Other")]);
        graph.SearchText = "other";
        Assert.IsFalse(graph.Nodes.Single(node => node.Id == "source").IsVisible);

        Assert.IsTrue(graph.RequestProblemFocus("source"));

        Assert.AreEqual("source", graph.ProblemFocusRequest?.StoryId);
        Assert.IsTrue(graph.Nodes.Single(node => node.Id == "source").IsVisible);
        Assert.IsFalse(graph.RequestProblemFocus("missing"));
    }

    private static StoryResource Story(string id, string displayName, params StoryNodeResource[] nodes) => new()
    {
        Id = id, DisplayName = displayName, Title = displayName,
        Entry = nodes.FirstOrDefault()?.Id ?? "end",
        Nodes = nodes.Length == 0 ? [new StoryNodeResource { Id = "end", Type = "end" }] : [.. nodes],
    };

    private static StoryNodeResource Enter(string id, string target) => new()
    {
        Id = id, Type = "enter_story",
        Properties = new Dictionary<string, JsonElement> { ["target_story_id"] = JsonSerializer.SerializeToElement(target) },
    };

    private sealed class GraphDirectory : IDisposable
    {
        public GraphDirectory()
        {
            Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".m7-test-data", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(Root);
        }
        public string Root { get; }
        public void Dispose()
        {
            var safeRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".m7-test-data"));
            if (!Root.StartsWith(safeRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unsafe M7 cleanup path.");
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
}
