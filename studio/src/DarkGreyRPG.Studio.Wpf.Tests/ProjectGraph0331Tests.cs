using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Packaging;
using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
[DoNotParallelize]
public sealed class ProjectGraph0331Tests
{
    [TestMethod]
    public void ProjectProjectionAllowsLayoutAndRejectsInternalContentCrud()
    {
        using var directory = new ProjectGraph0331Directory();
        var store = new CanonicalProjectGraphStore(directory.Root);
        store.Stories.Create(Story("ST-2345-6789-ABCD-EFGH", Terminate("stop", "停止")));
        store.Stories.Create(Story("ST-JKLM-NPQR-STUV-WXYZ", FlowInput("entry", "入口")));

        var graph = new ProjectGraphViewModel([], projectDirectory: directory.Root);
        var host = graph.CanonicalHost;
        Assert.IsNotNull(host);
        Assert.AreEqual(GraphScope.Project, host!.Scope);
        Assert.HasCount(2, host.Nodes);
        Assert.AreEqual(GraphInterfaceKind.Flow, host.Nodes.Single(node => node.NodeId == "ST-2345-6789-ABCD-EFGH").Outputs.Single().InterfaceKind);
        Assert.AreEqual(GraphInterfaceKind.Flow, host.Nodes.Single(node => node.NodeId == "ST-JKLM-NPQR-STUV-WXYZ").Inputs.Single().InterfaceKind);

        var beforeSource = store.Stories.Load("ST-2345-6789-ABCD-EFGH").ToJson();
        Assert.IsFalse(host.AddNode(new GraphNodeAuthoringService().Create(new GraphDocument(), GraphScope.StoryFlow, "terminate", "new_stop").Candidate!));
        Assert.AreEqual("graph.project.projection.readonly", host.LastValidationIssues.Single().Code);
        Assert.IsFalse(host.SetNodeProperty("ST-2345-6789-ABCD-EFGH", "display_name", JsonSerializer.SerializeToElement("改名")));
        Assert.AreEqual("graph.project.projection.readonly", host.LastValidationIssues.Single().Code);
        Assert.IsFalse(host.RemoveNode("ST-2345-6789-ABCD-EFGH"));
        Assert.AreEqual("graph.node.not_deletable", host.LastValidationIssues.Single().Code);
        Assert.AreEqual(beforeSource, store.Stories.Load("ST-2345-6789-ABCD-EFGH").ToJson());

        host.SetNodePosition("ST-JKLM-NPQR-STUV-WXYZ", 321, 654);
        Assert.AreEqual(321, host.Nodes.Single(node => node.NodeId == "ST-JKLM-NPQR-STUV-WXYZ").X);
        Assert.AreEqual(654, host.Nodes.Single(node => node.NodeId == "ST-JKLM-NPQR-STUV-WXYZ").Y);
    }

    [TestMethod]
    public void ProjectGraphLayoutUndoRedoPreservesEditableTypedEdge()
    {
        using var directory = new ProjectGraph0331Directory();
        var store = new CanonicalProjectGraphStore(directory.Root);
        store.Stories.Create(Story("ST-2345-6789-ABCD-EFGH", Terminate("stop", "停止")));
        store.Stories.Create(Story("ST-JKLM-NPQR-STUV-WXYZ", FlowInput("entry", "来自第一章")));

        var expected = new CanonicalStoryLogicConnection("ST-2345-6789-ABCD-EFGH", "stop", "ST-JKLM-NPQR-STUV-WXYZ", "entry", "Flow");
        store.StoryLogicGraph.Save([expected]);
        var graph = new ProjectGraphViewModel([], projectDirectory: directory.Root);
        var host = graph.CanonicalHost!;
        var output = GraphEditorEndpoint.Output(expected.SourceStoryId, expected.SourcePortId, GraphInterfaceKind.Flow);
        var input = GraphEditorEndpoint.Input(expected.TargetStoryId, expected.TargetPortId, GraphInterfaceKind.Flow);
        Assert.IsFalse(host.Connect(output, input));
        Assert.IsTrue(host.Disconnect(host.Graph.Connections.Single()));
        Assert.IsEmpty(store.StoryLogicGraph.Load().Connections);
        Assert.IsTrue(host.Undo());
        Assert.AreEqual(expected, store.StoryLogicGraph.Load().Connections.Single());
        Assert.IsTrue(host.BeginLayoutMove([expected.SourceStoryId]));
        var before = host.Nodes.Single(n => n.NodeId == expected.SourceStoryId).Position;
        host.SetNodePosition(expected.SourceStoryId, before.X + 90, before.Y + 30);
        Assert.IsTrue(host.CommitLayoutMove());
        Assert.IsTrue(host.Undo());
        Assert.AreEqual(before, host.Nodes.Single(n => n.NodeId == expected.SourceStoryId).Position);
        Assert.IsTrue(host.Redo());
        Assert.AreEqual(expected, store.StoryLogicGraph.Load().Connections.Single());

        var reopened = new ProjectGraphViewModel([], projectDirectory: directory.Root);
        Assert.AreEqual(new GraphConnection("ST-2345-6789-ABCD-EFGH", "stop", "ST-JKLM-NPQR-STUV-WXYZ", "entry", GraphInterfaceKind.Flow),
            reopened.CanonicalHost!.Graph.Connections.Single());
        Assert.AreEqual(GraphInterfaceKind.Flow, reopened.CanonicalHost.Graph.Connections.Single().InterfaceKind);
    }

    [TestMethod]
    public void LocalConnectionsAreEditableWhileReferencedSourceConnectionsRemainReadOnly()
    {
        using var provider = new ProjectGraph0331Directory();
        using var consumer = new ProjectGraph0331Directory();
        var providerStore = new CanonicalProjectGraphStore(provider.Root);
        providerStore.Stories.Create(Story("ST-AAAA-BBBB-CCCC-DDDD", FlowInput("provider_entry", "本地入口"), LogicInput("input", "外部输入"), LogicOutput("output", "外部输出")));
        providerStore.Memberships.Create(new CanonicalStoryMembershipManifest("ST-AAAA-BBBB-CCCC-DDDD"));
        var package = Path.Combine(provider.Root, "build", "provider.dgrs");
        new DgrsStoryPackageExporter(provider.Root).Build("ST-AAAA-BBBB-CCCC-DDDD", package, "0.3.3.1");

        var consumerStore = new CanonicalProjectGraphStore(consumer.Root);
        consumerStore.Stories.Create(Story("ST-2345-6789-ABCD-EFGH", LogicOutput("output", "本地输出")));
        consumerStore.Stories.Create(Story("ST-JKLM-NPQR-STUV-WXYZ", LogicInput("input", "本地输入")));
        Directory.CreateDirectory(Path.Combine(consumer.Root, "references"));
        File.Copy(package, Path.Combine(consumer.Root, "references", "provider.dgrs"));

        consumerStore.StoryLogicGraph.Save([new("ST-2345-6789-ABCD-EFGH", "output", "ST-AAAA-BBBB-CCCC-DDDD", "input", "Logic")]);
        var graph = new ProjectGraphViewModel([], projectDirectory: consumer.Root);
        var host = graph.CanonicalHost!;
        Assert.IsTrue(graph.IsReferencedStory("ST-AAAA-BBBB-CCCC-DDDD"));
        Assert.IsTrue(host.Nodes.Single(node => node.NodeId == "ST-AAAA-BBBB-CCCC-DDDD").DisplayName.Contains("只读", StringComparison.Ordinal));

        var localEdge = new GraphConnection("ST-2345-6789-ABCD-EFGH", "output", "ST-AAAA-BBBB-CCCC-DDDD", "input", GraphInterfaceKind.Logic);
        var localExpected = new CanonicalStoryLogicConnection("ST-2345-6789-ABCD-EFGH", "output", "ST-AAAA-BBBB-CCCC-DDDD", "input", "Logic");
        Assert.IsFalse(host.Connect(
            GraphEditorEndpoint.Output("ST-2345-6789-ABCD-EFGH", "output", GraphInterfaceKind.Logic),
            GraphEditorEndpoint.Input("ST-AAAA-BBBB-CCCC-DDDD", "input", GraphInterfaceKind.Logic)));
        var savedLocal = consumerStore.StoryLogicGraph.Load().Connections.Single();
        Assert.AreEqual(localExpected, savedLocal);
        Assert.IsTrue(host.Disconnect(localEdge));
        Assert.IsTrue(host.Connect(
            GraphEditorEndpoint.Output("ST-2345-6789-ABCD-EFGH", "output", GraphInterfaceKind.Logic),
            GraphEditorEndpoint.Input("ST-AAAA-BBBB-CCCC-DDDD", "input", GraphInterfaceKind.Logic)));
        Assert.AreEqual(localExpected, consumerStore.StoryLogicGraph.Load().Connections.Single());

        var before = consumerStore.StoryLogicGraph.Load().Connections.ToArray();
        var referencedOutput = GraphEditorEndpoint.Output("ST-AAAA-BBBB-CCCC-DDDD", "output", GraphInterfaceKind.Logic);
        var localTargetInput = GraphEditorEndpoint.Input("ST-JKLM-NPQR-STUV-WXYZ", "input", GraphInterfaceKind.Logic);
        Assert.IsFalse(host.CanConnect(referencedOutput, localTargetInput));
        Assert.AreEqual("story.graph.reference.readonly", host.LastValidationIssues.Single().Code);
        var accepted = host.Connect(
            referencedOutput,
            localTargetInput);
        Assert.IsFalse(accepted);
        Assert.AreEqual("story.graph.reference.readonly", host.LastValidationIssues.Single().Code);
        Assert.IsNotEmpty(host.LastValidationIssues);
        CollectionAssert.AreEqual(before, consumerStore.StoryLogicGraph.Load().Connections.ToArray());
        Assert.AreEqual(localEdge, host.Graph.Connections.Single());

        Assert.IsFalse(host.CanReconnect(localEdge, referencedOutput, localTargetInput));
        Assert.AreEqual("story.graph.reference.readonly", host.LastValidationIssues.Single().Code);
        Assert.IsFalse(host.Reconnect(localEdge, referencedOutput, localTargetInput));
        Assert.AreEqual("story.graph.reference.readonly", host.LastValidationIssues.Single().Code);

        var referencedEdge = new GraphConnection(
            "ST-AAAA-BBBB-CCCC-DDDD", "output", "ST-JKLM-NPQR-STUV-WXYZ", "input", GraphInterfaceKind.Logic);
        host.Graph.Connections.Add(referencedEdge);
        host.Refresh();
        Assert.IsFalse(host.Disconnect(referencedEdge));
        Assert.AreEqual("story.graph.reference.readonly", host.LastValidationIssues.Single().Code);
        Assert.IsFalse(host.CompleteIncidentWireDrag([referencedEdge], referencedOutput, null));
        Assert.AreEqual("story.graph.reference.readonly", host.LastValidationIssues.Single().Code);
        CollectionAssert.Contains(host.Graph.Connections, referencedEdge);
    }

    [STATestMethod]
    [DataRow("Flow")]
    [DataRow("Logic")]
    public void ProjectPortGesturesCreateReconnectDisconnectPersistAndUndo(string interfaceKind)
    {
        using var directory = new ProjectGraph0331Directory();
        var kind = Enum.Parse<GraphInterfaceKind>(interfaceKind);
        var store = new CanonicalProjectGraphStore(directory.Root);
        const string source = "ST-2345-6789-ABCD-EFGH", first = "ST-JKLM-NPQR-STUV-WXYZ", second = "ST-AAAA-BBBB-CCCC-DDDD";
        store.Stories.Create(Story(source, kind == GraphInterfaceKind.Flow ? Terminate("out", "出口") : LogicOutput("out", "出口")));
        foreach (var id in new[] { first, second })
            store.Stories.Create(Story(id, kind == GraphInterfaceKind.Flow ? FlowInput("in", "入口") : LogicInput("in", "入口")));
        var graph = new ProjectGraphViewModel([], projectDirectory: directory.Root);
        var host = graph.CanonicalHost!;
        var view = new CanonicalGraphEditorView(host);
        string? summary = null;
        graph.PropertyChanged += (_, e) => { if (e.PropertyName == nameof(graph.ConnectionSummary)) summary = graph.ConnectionSummary; };
        var root = new System.Windows.Controls.Grid { Width = 900, Height = 700 };
        root.Children.Add(view);
        try
        {
            root.Measure(new(900, 700)); root.Arrange(new(0, 0, 900, 700)); root.UpdateLayout();
            var output = view.PortVisuals.Single(port => port.NodeId == source);
            Assert.IsTrue(view.BeginPendingConnectionPress(output, new(0, 0)));
            Assert.IsTrue(view.AdvancePendingConnectionPress(new(30, 30)), "Project scope must admit the real port drag gesture.");
            Assert.IsTrue(view.CompleteConnectionDrag(view.PortVisuals.Single(port => port.NodeId == first)));
            Assert.AreEqual(first, store.StoryLogicGraph.Load().Connections.Single().TargetStoryId);
            Assert.AreEqual("3 个故事 · 1 条连接", summary);
            Assert.IsTrue(view.BeginExistingConnectionDrag(host.Connections.Single()));
            Assert.IsTrue(view.CompleteConnectionDrag(view.PortVisuals.Single(port => port.NodeId == second)));
            Assert.AreEqual(second, store.StoryLogicGraph.Load().Connections.Single().TargetStoryId);
            Assert.IsTrue(host.Undo());
            Assert.AreEqual(first, store.StoryLogicGraph.Load().Connections.Single().TargetStoryId);
            Assert.IsTrue(host.Redo());
            Assert.AreEqual(second, new ProjectGraphViewModel([], projectDirectory: directory.Root).CanonicalHost!.Graph.Connections.Single().ToNodeId);
            view.SetScissorsMode(true);
            Assert.IsTrue(view.IsScissorsMode);
            Assert.IsTrue(host.Disconnect(host.Graph.Connections.Single()));
            Assert.IsEmpty(store.StoryLogicGraph.Load().Connections);
            Assert.IsTrue(host.Undo());
            Assert.AreEqual(second, store.StoryLogicGraph.Load().Connections.Single().TargetStoryId);
        }
        finally { view.HandleKeyboardCommand(System.Windows.Input.Key.Escape); }
    }

    private static GraphResourceEnvelope Story(string id, params GraphNode[] nodes)
        => new(GraphResourceKind.Story, id, id, new GraphDocument(nodes));

    private static GraphNode FlowInput(string portId, string displayName)
    {
        var node = GraphNodeFactory.Create(GraphScope.StoryFlow, "start", "start_" + portId);
        StoryStartSchema.InitializeDefault(node, portId, StoryStartSchema.FlowDriven);
        node.Properties["display_name"] = JsonSerializer.SerializeToElement(displayName);
        return node;
    }

    private static GraphNode Terminate(string portId, string displayName)
    {
        var node = GraphNodeFactory.Create(GraphScope.StoryFlow, "terminate", "terminate_" + portId);
        node.Properties["port_id"] = JsonSerializer.SerializeToElement(portId);
        node.Properties["display_name"] = JsonSerializer.SerializeToElement(displayName);
        return node;
    }

    private static GraphNode LogicInput(string portId, string displayName)
    {
        var node = GraphNodeFactory.Create(GraphScope.StoryFlow, "logic_input", "logic_input_" + portId);
        node.Properties["port_id"] = JsonSerializer.SerializeToElement(portId);
        node.Properties["display_name"] = JsonSerializer.SerializeToElement(displayName);
        return node;
    }

    private static GraphNode LogicOutput(string portId, string displayName)
    {
        var node = GraphNodeFactory.Create(GraphScope.StoryFlow, "logic_output", "logic_output_" + portId);
        node.Properties["port_id"] = JsonSerializer.SerializeToElement(portId);
        node.Properties["display_name"] = JsonSerializer.SerializeToElement(displayName);
        return node;
    }
}

internal sealed class ProjectGraph0331Directory : IDisposable
{
    public ProjectGraph0331Directory()
    {
        Root = Path.Combine(AppContext.BaseDirectory, ".project-graph-0331", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Path.Combine(Root, "actors"));
        File.WriteAllText(Path.Combine(Root, "project.json"), "{\"schema_version\":3,\"identity_format\":\"story-uid-v1\",\"id\":\"test_project\",\"display_name\":\"Test Project\"}");
    }

    public string Root { get; }

    public void Dispose()
    {
        var safeRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".project-graph-0331"));
        var full = Path.GetFullPath(Root);
        if (!full.StartsWith(safeRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Unsafe Project Graph test cleanup path.");
        if (Directory.Exists(full)) Directory.Delete(full, recursive: true);
    }
}
