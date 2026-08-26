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
