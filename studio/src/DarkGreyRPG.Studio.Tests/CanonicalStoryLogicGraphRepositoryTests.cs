using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CanonicalStoryLogicGraphRepositoryTests
{
    [TestMethod]
    public void SaveReloadAndDisplayRenamePreserveStablePortConnections()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(Story("source", Boundary("logic_output", "output", "rescued", "公主已获救")));
        store.Stories.Create(Story("target", Boundary("logic_input", "input", "kingdom_gate", "王国密令")));
        var connection = new CanonicalStoryLogicConnection("source", "rescued", "target", "kingdom_gate");

        var saved = store.StoryLogicGraph.Save([connection]);
        Assert.AreEqual(connection, saved.Connections.Single());
        Assert.IsTrue(File.Exists(store.StoryLogicGraph.Path));

        var source = store.Stories.Load("source");
        source.Graph!.Nodes.Single().Properties["display_name"] = JsonSerializer.SerializeToElement("已救出公主");
        store.Stories.Replace(source);

        Assert.AreEqual(connection, store.StoryLogicGraph.Load().Connections.Single());
    }

    [TestMethod]
    public void DuplicateTargetAndMissingBoundaryFailClosed()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(Story("source_a", Boundary("logic_output", "output", "ready_a", "Ready A")));
        store.Stories.Create(Story("source_b", Boundary("logic_output", "output", "ready_b", "Ready B")));
        store.Stories.Create(Story("target", Boundary("logic_input", "input", "gate", "Gate")));

        var duplicateTarget = Assert.ThrowsExactly<CanonicalStoryLogicGraphRepositoryException>(() =>
            store.StoryLogicGraph.Save([
                new("source_a", "ready_a", "target", "gate"),
                new("source_b", "ready_b", "target", "gate")
            ]));
        Assert.AreEqual("story.logic_graph.target.multiple_sources", duplicateTarget.Code);

        var missing = Assert.ThrowsExactly<CanonicalStoryLogicGraphRepositoryException>(() =>
            store.StoryLogicGraph.Save([new("source_a", "missing", "target", "gate")]));
        Assert.AreEqual("story.logic_graph.port.missing", missing.Code);
    }

    private static GraphResourceEnvelope Story(string id, GraphNode boundary)
        => new(GraphResourceKind.Story, id, id, new GraphDocument([boundary]));

    private static GraphNode Boundary(string type, string id, string portId, string displayName)
    {
        var node = GraphNodeFactory.Create(GraphScope.StoryFlow, type, id);
        node.Properties["port_id"] = JsonSerializer.SerializeToElement(portId);
        node.Properties["display_name"] = JsonSerializer.SerializeToElement(displayName);
        return node;
    }
}
