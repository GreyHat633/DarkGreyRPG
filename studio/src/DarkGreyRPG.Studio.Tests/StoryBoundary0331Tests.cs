using System.IO.Compression;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Editing;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Packaging;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class StoryBoundary0331Tests
{
    [TestMethod]
    public void ProjectionRequiresExplicitFlowDrivenInputsAndPreservesStableTypedBoundaryMetadata()
    {
        var start = GraphNodeFactory.CreateStoryStart("start", triggerPortId: "local_entry");
        var story = Story("story", start);

        Assert.IsEmpty(CanonicalStoryBoundaryProjection.Ports(story));

        var graph = story.Graph!;
        var session = new GraphEditSession(graph, GraphScope.StoryFlow, dynamicPortIdSource: () => "flow_hidden");
        Assert.IsTrue(session.AddStoryStartTrigger("start", "隐藏入口", StoryStartSchema.FlowDriven));
        story.Graph = graph;

        var ports = CanonicalStoryBoundaryProjection.Ports(story);
        Assert.HasCount(1, ports);
        Assert.AreEqual("flow_hidden", ports[0].Id);
        Assert.AreEqual("隐藏入口", ports[0].DisplayName);
        Assert.IsTrue(ports[0].IsInput);
        Assert.AreEqual(GraphInterfaceKind.Flow, ports[0].InterfaceKind);

        var source = Story("source", Terminate("stop", "隐藏结局"), LogicOutput("out", "完成"));
        var target = Story("target", FlowInput("entry", "来自第一章"), LogicInput("in", "逻辑门"));
        var sourcePorts = CanonicalStoryBoundaryProjection.Ports(source);
        var targetPorts = CanonicalStoryBoundaryProjection.Ports(target);
        CollectionAssert.AreEquivalent(new[] { "stop", "out" }, sourcePorts.Select(port => port.Id).ToArray());
        CollectionAssert.AreEquivalent(new[] { "entry", "in" }, targetPorts.Select(port => port.Id).ToArray());
        Assert.AreEqual(GraphInterfaceKind.Flow, sourcePorts.Single(port => port.Id == "stop").InterfaceKind);
        Assert.AreEqual(GraphInterfaceKind.Logic, sourcePorts.Single(port => port.Id == "out").InterfaceKind);
        Assert.IsTrue(targetPorts.Single(port => port.Id == "entry").IsInput);
        Assert.IsTrue(targetPorts.Single(port => port.Id == "in").IsInput);
    }

    [TestMethod]
    public void TypedProjectConnectionsEnforceEndpointKindsAndCardinality()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(Story("source", FlowInput("source_entry", "本地入口"), Terminate("stop", "停止"), LogicOutput("out", "完成")));
        store.Stories.Create(Story("target", FlowInput("entry", "入口"), LogicInput("in", "条件")));
        store.Stories.Create(Story("target_two", FlowInput("entry_two", "第二入口"), LogicInput("in_two", "第二条件")));
        store.StoryLogicGraph.Save([
            new("source", "stop", "target", "entry", "Flow"),
            new("source", "out", "target", "in", "Logic"),
        ]);

        var saved = store.StoryLogicGraph.Load();
        Assert.AreEqual("Flow", saved.Connections.Single(connection => connection.SourcePortId == "stop").InterfaceKind);
        Assert.AreEqual("Logic", saved.Connections.Single(connection => connection.SourcePortId == "out").InterfaceKind);

        var wrongSourceKind = Assert.ThrowsExactly<CanonicalStoryLogicGraphRepositoryException>(() =>
            store.StoryLogicGraph.Save([new("source", "stop", "target", "in", "Logic")]));
        Assert.AreEqual("story.logic_graph.port.missing", wrongSourceKind.Code);

        var wrongTargetKind = Assert.ThrowsExactly<CanonicalStoryLogicGraphRepositoryException>(() =>
            store.StoryLogicGraph.Save([new("source", "out", "target", "entry", "Flow")]));
        Assert.AreEqual("story.logic_graph.port.missing", wrongTargetKind.Code);

        var duplicateFlow = Assert.ThrowsExactly<CanonicalStoryLogicGraphRepositoryException>(() =>
            store.StoryLogicGraph.Save([
                new("source", "stop", "target", "entry", "Flow"),
                new("source", "stop", "target_two", "entry_two", "Flow"),
            ]));
        Assert.AreEqual("story.logic_graph.source.multiple_targets", duplicateFlow.Code);

        var duplicateLogic = Assert.ThrowsExactly<CanonicalStoryLogicGraphRepositoryException>(() =>
            store.StoryLogicGraph.Save([
                new("source", "out", "target", "in", "Logic"),
                new("source", "out", "target", "in", "Logic"),
            ]));
        Assert.AreEqual("story.logic_graph.connection.duplicate", duplicateLogic.Code);

        var secondSource = Story("source_two", LogicOutput("out_two", "另一个完成"));
        store.Stories.Create(secondSource);
        var multipleSources = Assert.ThrowsExactly<CanonicalStoryLogicGraphRepositoryException>(() =>
            store.StoryLogicGraph.Save([
                new("source", "out", "target", "in", "Logic"),
                new("source_two", "out_two", "target", "in", "Logic"),
            ]));
        Assert.AreEqual("story.logic_graph.target.multiple_sources", multipleSources.Code);

        var targetWithAmbiguousPort = store.Stories.Load("target");
        targetWithAmbiguousPort.Graph!.Nodes.Add(LogicInput("duplicate", "条件"));
        targetWithAmbiguousPort.Graph.Nodes.Add(LogicInput("duplicate", "条件副本"));
        store.Stories.Replace(targetWithAmbiguousPort);
        var ambiguous = Assert.ThrowsExactly<CanonicalStoryLogicGraphRepositoryException>(() =>
            store.StoryLogicGraph.Save([new("source", "out", "target", "duplicate", "Logic")]));
        Assert.AreEqual("story.logic_graph.port.missing", ambiguous.Code);
    }

    [TestMethod]
    public void DgrsExporterWritesTypedSourceOutgoingEdgesWithoutRecursiveTargetStory()
    {
        using var project = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(Story("source", FlowInput("source_entry", "本地入口"), Terminate("stop", "停止"), LogicOutput("out", "完成")));
        store.Stories.Create(Story("target", FlowInput("entry", "入口"), LogicInput("in", "条件")));
        store.Memberships.Create(new CanonicalStoryMembershipManifest("source"));
        store.Memberships.Create(new CanonicalStoryMembershipManifest("target"));
        store.StoryLogicGraph.Save([
            new("source", "stop", "target", "entry", "Flow"),
            new("source", "out", "target", "in", "Logic"),
        ]);

        var archive = Path.Combine(project.Root, "build", "source.dgrs");
        var result = new DgrsStoryPackageExporter(project.Root).Build("source", archive, "0.3.3.1");

        Assert.AreEqual("resources/story_logic_graph.json", result.Manifest.RequiredResources.StoryLogicGraph);
        using var zip = ZipFile.OpenRead(archive);
        Assert.IsFalse(zip.Entries.Any(entry => entry.FullName.Contains("target", StringComparison.OrdinalIgnoreCase)));
        var graphEntry = zip.GetEntry("resources/story_logic_graph.json");
        Assert.IsNotNull(graphEntry);
        using var reader = new StreamReader(graphEntry!.Open());
        using var json = JsonDocument.Parse(reader.ReadToEnd());
        var connections = json.RootElement.GetProperty("connections").EnumerateArray().ToArray();
        Assert.HasCount(2, connections);
        CollectionAssert.AreEquivalent(new[] { "Flow", "Logic" },
            connections.Select(connection => connection.GetProperty("interface_kind").GetString()).ToArray());
        Assert.IsTrue(connections.All(connection => connection.GetProperty("source_story_id").GetString() == "source"));
        CollectionAssert.AreEquivalent(new[] { "target" },
            connections.Select(connection => connection.GetProperty("target_story_id").GetString()).Distinct().ToArray());
    }

    private static GraphResourceEnvelope Story(string id, params GraphNode[] nodes)
        => new(GraphResourceKind.Story, id, id, new GraphDocument(nodes));

    private static GraphNode FlowInput(string portId, string displayName)
    {
        var node = GraphNodeFactory.Create(GraphScope.StoryFlow, "start", "start_" + portId);
        StoryStartSchema.InitializeDefault(node, portId, StoryStartSchema.FlowDriven);
        SetBoundaryName(node, displayName: displayName);
        return node;
    }

    private static GraphNode Terminate(string portId, string displayName)
    {
        var node = GraphNodeFactory.Create(GraphScope.StoryFlow, "terminate", "terminate_" + portId);
        SetBoundaryName(node, portId, displayName);
        return node;
    }

    private static GraphNode LogicInput(string portId, string displayName)
    {
        var node = GraphNodeFactory.Create(GraphScope.StoryFlow, "logic_input", "logic_input_" + portId);
        SetBoundaryName(node, portId, displayName);
        return node;
    }

    private static GraphNode LogicOutput(string portId, string displayName)
    {
        var node = GraphNodeFactory.Create(GraphScope.StoryFlow, "logic_output", "logic_output_" + portId);
        SetBoundaryName(node, portId, displayName);
        return node;
    }

    private static void SetBoundaryName(GraphNode node, string? portId = null, string? displayName = null)
    {
        if (portId is not null) node.Properties["port_id"] = JsonSerializer.SerializeToElement(portId);
        if (displayName is not null) node.Properties["display_name"] = JsonSerializer.SerializeToElement(displayName);
    }
}
