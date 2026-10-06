using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Editing;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Packaging;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class PublicOutputContract0336Tests
{
    [TestMethod]
    public void TaskCanDeleteEveryNodeUndoAndRoundTripWithoutOutputs()
    {
        var graph = new GraphDocument();
        var author = new GraphNodeAuthoringService();
        foreach (var id in new[] { "first", "last" }) graph.Nodes.Add(author.Create(graph, GraphScope.Task, "settle", id).Candidate!);
        var original = graph.ToJson();
        var edit = new GraphEditSession(graph, GraphScope.Task);
        Assert.IsTrue(edit.RemoveNodes(["first", "last"]));
        Assert.IsEmpty(graph.Nodes);
        Assert.IsTrue(GraphScopePolicy.IsValid(graph, GraphScope.Task));
        Assert.IsTrue(edit.Undo());
        Assert.AreEqual(original, graph.ToJson());
        Assert.IsTrue(edit.Redo());
        var envelope = new GraphResourceEnvelope(GraphResourceKind.Task, "ST-2345-6789-ABCD-EFGH~task~empty", "Empty", graph);
        Assert.IsEmpty(GraphResourceEnvelope.FromJson(envelope.ToJson()).Graph!.Nodes);
        Assert.IsTrue(CanonicalAggregateNodeFactory.Create(envelope, "aggregate").IsSuccess);
        Assert.IsTrue(edit.AddNode(GraphNodeFactory.Create(GraphScope.Task, "objective", "objective")));
        Assert.IsTrue(edit.RemoveNode("objective"));
        Assert.IsEmpty(graph.Nodes);
    }

    [TestMethod]
    public void ReorderedThreeLayerOutputsAndConnectionsSurvivePackageRoundTrip()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var story = new CanonicalStoryLifecycleService(store).CreateNew("Ports");
        var resources = new CanonicalStoryResourceLifecycleService(store);
        var session = resources.CreateOwned(story.Id, GraphResourceKind.Session, story.Id + "~session~ports", "Session");
        var task = resources.CreateOwned(story.Id, GraphResourceKind.Task, story.Id + "~task~ports", "Task");
        task.Graph = new GraphDocument(); // Output contract fixture does not contain an unfinished objective draft.
        var author = new GraphNodeAuthoringService();
        foreach (var (resource, scope, terminal) in new[]
        {
            (session, GraphScope.Session, "end"), (task, GraphScope.Task, "settle"),
            (story, GraphScope.StoryFlow, "terminate")
        })
        {
            var graph = resource.Graph!;
            foreach (var type in new[] { terminal, terminal, "logic_output", "logic_output" })
                graph.Nodes.Add(author.Create(graph, scope, type, "output_" + graph.Nodes.Count).Candidate!);
            var edit = new GraphEditSession(graph, scope);
            foreach (var kind in new[] { GraphInterfaceKind.Flow, GraphInterfaceKind.Logic })
            {
                var last = graph.Nodes.Last(node => PublicOutputSchema.IsOutput(node) && PublicOutputSchema.Kind(node) == kind);
                Assert.IsTrue(edit.MovePublicOutput(last.Properties["port_id"].GetString()!, 0));
                Assert.IsTrue(edit.SetNodeProperty(last.Id, "display_name", "Renamed " + kind));
            }
            graph.Nodes.Reverse();
            resource.Graph = graph;
        }
        store.Sessions.Replace(session); store.Tasks.Replace(task);
        var storyGraph = story.Graph!;
        var sessionNode = CanonicalAggregateNodeFactory.Create(storyGraph, session, "session").Candidate!;
        storyGraph.Nodes.Add(sessionNode);
        var taskNode = CanonicalAggregateNodeFactory.Create(storyGraph, task, "task").Candidate!;
        storyGraph.Nodes.Add(taskNode);
        var connection = new GraphConnection(sessionNode.Id,
            sessionNode.Ports.First(port => port.IsOutput && port.Kind == GraphInterfaceKind.Flow).Id,
            taskNode.Id, "flow_in", GraphInterfaceKind.Flow);
        storyGraph.Connections.Add(connection);
        story.Graph = storyGraph; store.Stories.Replace(story);
        var path = Path.Combine(project.Root, "Exports", "ports.dgrs");
        new DgrsStoryPackageExporter(project.Root).Build(story.Id, path, "0.3.3.6");
        var reopened = OfflineDgrsPackageReader.Read(path);
        foreach (var original in new[] { story, session, task })
        {
            var graph = reopened.Resources.Single(resource => resource.Id == original.Id).ReadGraphDefinition()!.Graph!;
            string[] Outputs(GraphDocument source) => source.Nodes.Where(PublicOutputSchema.IsOutput)
                .Select(node => $"{node.Properties["port_id"]}|{node.Properties["display_name"]}|{PublicOutputSchema.Order(node)}")
                .OrderBy(value => value, StringComparer.Ordinal).ToArray();
            CollectionAssert.AreEqual(Outputs(original.Graph!), Outputs(graph));
            CollectionAssert.AreEqual(original.Graph!.Connections.ToArray(), graph.Connections.ToArray());
        }
    }

    [TestMethod]
    public void ExplicitOrderIsIndependentOfNodeArrayAndFlowLogicKinds()
    {
        var graph = new GraphDocument();
        var author = new GraphNodeAuthoringService();
        foreach (var type in new[] { "end", "logic_output", "end", "logic_output" })
            graph.Nodes.Add(author.Create(graph, GraphScope.Session, type, "node" + graph.Nodes.Count).Candidate!);
        var expected = graph.Nodes.Select(node => node.Properties["port_id"].GetString()).ToArray();
        var edges = graph.Connections.ToArray();
        var edit = new GraphEditSession(graph, GraphScope.Session);
        Assert.IsTrue(edit.MovePublicOutput(expected[2]!, 0));
        Assert.AreEqual(0, PublicOutputSchema.Order(graph.Nodes[2]));
        Assert.AreEqual(1, PublicOutputSchema.Order(graph.Nodes[0]));
        Assert.AreEqual(0, PublicOutputSchema.Order(graph.Nodes[1]));
        graph.Nodes.Reverse();
        var envelope = new GraphResourceEnvelope(GraphResourceKind.Session,
            "ST-2345-6789-ABCD-EFGH~session~ports", "Ports", graph);
        var reopened = GraphResourceEnvelopeSerializer.Deserialize(envelope.ToJson());
        var projected = CanonicalAggregateNodeFactory.Create(new(), reopened, "aggregate").Candidate!;
        CollectionAssert.AreEqual(new[] { expected[2], expected[0] },
            projected.Ports.Where(port => port.IsOutput && port.InterfaceKind == GraphInterfaceKind.Flow).Select(port => port.Id).ToArray());
        CollectionAssert.AreEquivalent(expected, reopened.Graph!.Nodes.Select(node => node.Properties["port_id"].GetString()).ToArray());
        CollectionAssert.AreEqual(edges, graph.Connections.ToArray());
    }

    [TestMethod]
    public void MissingDuplicateAndLegacyOutputContractsAreRejectedWithoutRepair()
    {
        var graph = new GraphDocument();
        var author = new GraphNodeAuthoringService();
        graph.Nodes.Add(author.Create(graph, GraphScope.Task, "settle", "first").Candidate!);
        graph.Nodes.Add(author.Create(graph, GraphScope.Task, "settle", "second").Candidate!);
        var original = graph.ToJson();
        var edit = new GraphEditSession(graph, GraphScope.Task);
        Assert.IsFalse(edit.SetNodeProperty("second", "display_order", 0));
        Assert.AreEqual(original, graph.ToJson());
        graph.Nodes[1].Properties.Remove("display_order");
        Assert.IsTrue(PublicOutputSchema.Validate(graph.Nodes).Any(issue => issue.Code == "graph.output.order.required"));
        graph.Nodes[1].Properties["display_order"] = JsonSerializer.SerializeToElement(0);
        Assert.IsTrue(PublicOutputSchema.Validate(graph.Nodes).Any(issue => issue.Code == "graph.output.order.duplicate"));
        graph.Nodes[1].Properties["port_id"] = graph.Nodes[0].Properties["port_id"];
        Assert.IsTrue(PublicOutputSchema.Validate(graph.Nodes).Any(issue => issue.Code == "graph.output.port_id.duplicate"));
        var legacy = GraphNodeFactory.Create(GraphScope.Task, "settle", "old");
        legacy.Ports.Clear();
        legacy.Ports.Add(new("result", "Result", true, GraphInterfaceKind.Logic));
        var envelope = new GraphResourceEnvelope(GraphResourceKind.Task,
            "ST-2345-6789-ABCD-EFGH~task~old", "Old", new([legacy]));
        Assert.ThrowsExactly<GraphResourceEnvelopeException>(() => envelope.ToJson());
        Assert.AreEqual("result", legacy.Ports.Single().Id);
    }
}
