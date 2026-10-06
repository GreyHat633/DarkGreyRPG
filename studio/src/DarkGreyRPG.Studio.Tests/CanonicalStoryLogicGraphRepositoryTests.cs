using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CanonicalStoryLogicGraphRepositoryTests
{
    [TestMethod]
    public void RetiredSchemaAndDuplicateProtocolFieldsAreRejected()
    {
        foreach (var json in new[]
        {
            "{\"schema_version\":1,\"connections\":[]}",
            "{\"schema_version\":2,\"schema_version\":2,\"connections\":[]}",
            "{\"schema_version\":2,\"connections\":[],\"connections\":[]}",
            "{\"schema_version\":2,\"connections\":[{\"source_story_id\":\"ST-2345-6789-ABCD-EFGH\",\"source_story_id\":\"ST-2345-6789-ABCD-EFGH\",\"source_port_id\":\"out\",\"target_story_id\":\"ST-JKLM-NPQR-STUV-WXYZ\",\"target_port_id\":\"in\",\"interface_kind\":\"Logic\"}]}",
        })
        {
            using var document = JsonDocument.Parse(json);
            Assert.ThrowsExactly<CanonicalStoryLogicGraphRepositoryException>(() => CanonicalStoryLogicGraphRepository.Parse(document.RootElement));
        }
    }
    [TestMethod]
    public void SaveReloadAndDisplayRenamePreserveStablePortConnections()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(Story("ST-2345-6789-ABCD-EFG2", Boundary("logic_output", "output", "rescued", "公主已获救")));
        store.Stories.Create(Story("ST-2345-6789-ABCD-EFG3", Boundary("logic_input", "input", "kingdom_gate", "王国密令")));
        var connection = new CanonicalStoryLogicConnection("ST-2345-6789-ABCD-EFG2", "rescued", "ST-2345-6789-ABCD-EFG3", "kingdom_gate");

        var saved = store.StoryLogicGraph.Save([connection]);
        Assert.AreEqual(connection, saved.Connections.Single());
        Assert.IsTrue(File.Exists(store.StoryLogicGraph.Path));

        var source = store.Stories.Load("ST-2345-6789-ABCD-EFG2");
        var graph = source.Graph!; graph.Nodes.Single().Properties["display_name"] = JsonSerializer.SerializeToElement("已救出公主"); source.Graph = graph;
        store.Stories.Replace(source);

        Assert.AreEqual(connection, store.StoryLogicGraph.Load().Connections.Single());
    }

    [TestMethod]
    public void DuplicateTargetAndMissingBoundaryFailClosed()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(Story("ST-2345-6789-ABCD-EFG4", Boundary("logic_output", "output", "ready_a", "Ready A")));
        store.Stories.Create(Story("ST-2345-6789-ABCD-EFG5", Boundary("logic_output", "output", "ready_b", "Ready B")));
        store.Stories.Create(Story("ST-2345-6789-ABCD-EFG3", Boundary("logic_input", "input", "gate", "Gate")));

        var duplicateTarget = Assert.ThrowsExactly<CanonicalStoryLogicGraphRepositoryException>(() =>
            store.StoryLogicGraph.Save([
                new("ST-2345-6789-ABCD-EFG4", "ready_a", "ST-2345-6789-ABCD-EFG3", "gate"),
                new("ST-2345-6789-ABCD-EFG5", "ready_b", "ST-2345-6789-ABCD-EFG3", "gate")
            ]));
        Assert.AreEqual("story.logic_graph.target.multiple_sources", duplicateTarget.Code);

        var missing = Assert.ThrowsExactly<CanonicalStoryLogicGraphRepositoryException>(() =>
            store.StoryLogicGraph.Save([new("ST-2345-6789-ABCD-EFG4", "missing", "ST-2345-6789-ABCD-EFG3", "gate")]));
        Assert.AreEqual("story.logic_graph.port.missing", missing.Code);
    }

    private static GraphResourceEnvelope Story(string id, GraphNode boundary)
        => new(GraphResourceKind.Story, id, id, new GraphDocument([boundary]));

    [TestMethod]
    public void GroupNameConcurrentChangeRejectsWholeGraphSave()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(Story("ST-2345-6789-ABCD-EFG6", Boundary("logic_output", "out", "exit", "Exit")));
        store.Stories.Create(Story("ST-2345-6789-ABCD-EFG7", Boundary("logic_input", "in", "entry", "Entry")));
        store.StoryLogicGraph.Save([]);
        var original = File.ReadAllBytes(store.StoryLogicGraph.Path);
        var names = new StoryGroupNameStore(project.Root);
        names.Save(StoryGroupCatalog.Empty);
        var stale = File.ReadAllBytes(names.Path);
        var graph = new CanonicalStoryLogicGraph(2, [new("ST-2345-6789-ABCD-EFG6", "exit", "ST-2345-6789-ABCD-EFG7", "entry")]);
        var groups = StoryGroupCatalog.Derive(["ST-2345-6789-ABCD-EFG6", "ST-2345-6789-ABCD-EFG7"], graph);
        var concurrent = StoryGroupNameStore.Serialize(groups.Rename(groups.Groups[0].Key, "另一处修改"));
        File.WriteAllText(names.Path, concurrent);
        Assert.Throws<Exception>(() => store.StoryLogicGraph.Save(graph.Connections,
            [new DarkGreyRPG.Studio.Core.IO.ProjectFileChange(Path.GetRelativePath(project.Root, names.Path), stale,
                System.Text.Encoding.UTF8.GetBytes(StoryGroupNameStore.Serialize(groups)))]));
        CollectionAssert.AreEqual(original, File.ReadAllBytes(store.StoryLogicGraph.Path));
        Assert.AreEqual(concurrent, File.ReadAllText(names.Path));
    }

    private static GraphNode Boundary(string type, string id, string portId, string displayName)
    {
        var node = GraphNodeFactory.Create(GraphScope.StoryFlow, type, id);
        node.Properties["port_id"] = JsonSerializer.SerializeToElement(portId);
        node.Properties["display_name"] = JsonSerializer.SerializeToElement(displayName);
        return node;
    }
}
