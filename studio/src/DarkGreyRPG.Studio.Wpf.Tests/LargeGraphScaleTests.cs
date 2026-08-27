using System.Text.Json;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class LargeGraphScaleTests
{
    [TestMethod]
    public void FlowBuildsTwoHundredNodesAndFourHundredConnections()
    {
        var nodes = Enumerable.Range(0, 200)
            .Select(index => new StoryNodeResource
            {
                Id = $"branch_{index}",
                Type = "branch",
                Position = new StoryNodePosition { X = (index % 20) * 260, Y = (index / 20) * 180 },
                Properties = new Dictionary<string, JsonElement>
                {
                    ["variable"] = JsonSerializer.SerializeToElement($"route_{index}"),
                    ["operator"] = JsonSerializer.SerializeToElement("=="),
                    ["value"] = JsonSerializer.SerializeToElement("ready"),
                },
            })
            .ToList();
        var connections = Enumerable.Range(0, 200)
            .SelectMany(index => new[]
            {
                new StoryConnectionResource { From = $"branch_{index}", Output = "true", To = $"branch_{(index + 1) % 200}" },
                new StoryConnectionResource { From = $"branch_{index}", Output = "false", To = $"branch_{(index + 2) % 200}" },
            })
            .ToList();
        var resource = new StoryResource
        {
            Id = "large_flow",
            DisplayName = "Large Flow",
            FlowRef = "large_flow",
            Title = "Large Flow",
            Entry = "branch_0",
            Nodes = nodes,
            Connections = connections,
        };

        var editor = new StoryFlowEditorViewModel(
            StoryDocument.FromResource(resource), [], [], [], ["large_flow"]);

        Assert.HasCount(200, editor.Nodes);
        Assert.HasCount(400, editor.Connections);
        Assert.IsTrue(editor.Nodes.All(node => node.Outputs.SequenceEqual(["true", "false"])));
    }

    [TestMethod]
    public void ProjectGraphLaysOutTwoHundredStoriesAndFourHundredTransitionsInOneScc()
    {
        var stories = Enumerable.Range(0, 200)
            .Select(index =>
            {
                var target = $"story_{(index + 1) % 200}";
                return new StoryResource
                {
                    Id = $"story_{index}",
                    DisplayName = $"Story {index}",
                    Title = $"Story {index}",
                    Entry = "enter_0",
                    Nodes =
                    [
                        Enter("enter_0", target),
                        Enter("enter_1", target),
                    ],
                };
            })
            .ToList();

        var graph = new ProjectGraphViewModel(stories, "story_0");
        graph.AutoLayout();

        Assert.HasCount(200, graph.Nodes);
        Assert.HasCount(200, graph.Edges);
        Assert.AreEqual(400, graph.Edges.Sum(edge => edge.Count));
        Assert.IsTrue(graph.Nodes.All(node => double.IsFinite(node.X) && double.IsFinite(node.Y)));
        Assert.HasCount(200, graph.Diagnostics.Where(issue => issue.Code == "project_graph.story.cycle"));
    }

    private static StoryNodeResource Enter(string id, string target) => new()
    {
        Id = id,
        Type = "enter_story",
        Properties = new Dictionary<string, JsonElement>
        {
            ["target_story_id"] = JsonSerializer.SerializeToElement(target),
        },
    };
}
