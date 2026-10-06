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
        var story = Story("ST-2345-6789-ABCD-EFGH", start);

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

        var source = Story("ST-2345-6789-ABCD-EFGH", Terminate("stop", "隐藏结局"), LogicOutput("out", "完成"));
        var target = Story("ST-JKLM-NPQR-STUV-WXYZ", FlowInput("entry", "来自第一章"), LogicInput("in", "逻辑门"));
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
        store.Stories.Create(Story("ST-2345-6789-ABCD-EFGH", FlowInput("source_entry", "本地入口"), Terminate("stop", "停止"), LogicOutput("out", "完成")));
        store.Stories.Create(Story("ST-JKLM-NPQR-STUV-WXYZ", FlowInput("entry", "入口"), LogicInput("in", "条件")));
        store.Stories.Create(Story("ST-4567-89AB-CDEF-GHJK", FlowInput("entry_two", "第二入口"), LogicInput("in_two", "第二条件")));
        store.StoryLogicGraph.Save([
            new("ST-2345-6789-ABCD-EFGH", "stop", "ST-JKLM-NPQR-STUV-WXYZ", "entry", "Flow"),
            new("ST-2345-6789-ABCD-EFGH", "out", "ST-JKLM-NPQR-STUV-WXYZ", "in", "Logic"),
        ]);

        var saved = store.StoryLogicGraph.Load();
        Assert.AreEqual("Flow", saved.Connections.Single(connection => connection.SourcePortId == "stop").InterfaceKind);
        Assert.AreEqual("Logic", saved.Connections.Single(connection => connection.SourcePortId == "out").InterfaceKind);

        var wrongSourceKind = Assert.ThrowsExactly<CanonicalStoryLogicGraphRepositoryException>(() =>
            store.StoryLogicGraph.Save([new("ST-2345-6789-ABCD-EFGH", "stop", "ST-JKLM-NPQR-STUV-WXYZ", "in", "Logic")]));
        Assert.AreEqual("story.logic_graph.port.missing", wrongSourceKind.Code);

        var wrongTargetKind = Assert.ThrowsExactly<CanonicalStoryLogicGraphRepositoryException>(() =>
            store.StoryLogicGraph.Save([new("ST-2345-6789-ABCD-EFGH", "out", "ST-JKLM-NPQR-STUV-WXYZ", "entry", "Flow")]));
        Assert.AreEqual("story.logic_graph.port.missing", wrongTargetKind.Code);

        var duplicateFlow = Assert.ThrowsExactly<CanonicalStoryLogicGraphRepositoryException>(() =>
            store.StoryLogicGraph.Save([
                new("ST-2345-6789-ABCD-EFGH", "stop", "ST-JKLM-NPQR-STUV-WXYZ", "entry", "Flow"),
                new("ST-2345-6789-ABCD-EFGH", "stop", "ST-4567-89AB-CDEF-GHJK", "entry_two", "Flow"),
            ]));
        Assert.AreEqual("story.logic_graph.source.multiple_targets", duplicateFlow.Code);

        var duplicateLogic = Assert.ThrowsExactly<CanonicalStoryLogicGraphRepositoryException>(() =>
            store.StoryLogicGraph.Save([
                new("ST-2345-6789-ABCD-EFGH", "out", "ST-JKLM-NPQR-STUV-WXYZ", "in", "Logic"),
                new("ST-2345-6789-ABCD-EFGH", "out", "ST-JKLM-NPQR-STUV-WXYZ", "in", "Logic"),
            ]));
        Assert.AreEqual("story.logic_graph.connection.duplicate", duplicateLogic.Code);

        var secondSource = Story("ST-3456-789A-BCDE-FGHJ", LogicOutput("out_two", "另一个完成"));
        store.Stories.Create(secondSource);
        var multipleSources = Assert.ThrowsExactly<CanonicalStoryLogicGraphRepositoryException>(() =>
            store.StoryLogicGraph.Save([
                new("ST-2345-6789-ABCD-EFGH", "out", "ST-JKLM-NPQR-STUV-WXYZ", "in", "Logic"),
                new("ST-3456-789A-BCDE-FGHJ", "out_two", "ST-JKLM-NPQR-STUV-WXYZ", "in", "Logic"),
            ]));
        Assert.AreEqual("story.logic_graph.target.multiple_sources", multipleSources.Code);

        var targetWithAmbiguousPort = store.Stories.Load("ST-JKLM-NPQR-STUV-WXYZ");
        var ambiguousGraph = targetWithAmbiguousPort.Graph!;
        ambiguousGraph.Nodes.Add(LogicInput("duplicate", "条件"));
        ambiguousGraph.Nodes.Add(LogicInput("duplicate", "条件副本"));
        targetWithAmbiguousPort.Graph = ambiguousGraph;
        store.Stories.Replace(targetWithAmbiguousPort);
        var ambiguous = Assert.ThrowsExactly<CanonicalStoryLogicGraphRepositoryException>(() =>
            store.StoryLogicGraph.Save([new("ST-2345-6789-ABCD-EFGH", "out", "ST-JKLM-NPQR-STUV-WXYZ", "duplicate", "Logic")]));
        Assert.AreEqual("story.logic_graph.port.missing", ambiguous.Code);
    }

    [TestMethod]
    public void LinkedStoriesRequireWholeGroupExportWithTypedEdges()
    {
        using var project = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(Story("ST-2345-6789-ABCD-EFGH", FlowInput("source_entry", "本地入口"), Terminate("stop", "停止"), LogicOutput("out", "完成")));
        store.Stories.Create(Story("ST-JKLM-NPQR-STUV-WXYZ", FlowInput("entry", "入口"), LogicInput("in", "条件")));
        store.Memberships.Create(new CanonicalStoryMembershipManifest("ST-2345-6789-ABCD-EFGH"));
        store.Memberships.Create(new CanonicalStoryMembershipManifest("ST-JKLM-NPQR-STUV-WXYZ"));
        store.StoryLogicGraph.Save([
            new("ST-2345-6789-ABCD-EFGH", "stop", "ST-JKLM-NPQR-STUV-WXYZ", "entry", "Flow"),
            new("ST-2345-6789-ABCD-EFGH", "out", "ST-JKLM-NPQR-STUV-WXYZ", "in", "Logic"),
        ]);

        var archive = Path.Combine(project.Root, "build", "source.dgrs");
        Assert.ThrowsExactly<StoryPackageException>(() => new DgrsStoryPackageExporter(project.Root).Build("ST-2345-6789-ABCD-EFGH", archive));
        var result = new DgrsGroupPackageExporter(project.Root).Build("ST-2345-6789-ABCD-EFGH", archive + ".g", "0.3.3.6");
        CollectionAssert.AreEquivalent(new[] { "ST-2345-6789-ABCD-EFGH", "ST-JKLM-NPQR-STUV-WXYZ" }, result.Manifest.Members.Select(member => member.StoryId).ToArray());
        Assert.HasCount(2, result.Connections.Connections);
        CollectionAssert.AreEquivalent(new[] { "Flow", "Logic" }, result.Connections.Connections.Select(edge => edge.InterfaceKind).ToArray());
        Assert.IsTrue(result.Connections.Connections.All(edge => edge.SourceStoryId == "ST-2345-6789-ABCD-EFGH" && edge.TargetStoryId == "ST-JKLM-NPQR-STUV-WXYZ"));
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
