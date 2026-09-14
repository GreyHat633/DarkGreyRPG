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
    public void ProjectProjectionAllowsLayoutOnlyAndRejectsContentCrud()
    {
        using var directory = new ProjectGraph0331Directory();
        var store = new CanonicalProjectGraphStore(directory.Root);
        store.Stories.Create(Story("source", Terminate("stop", "停止")));
        store.Stories.Create(Story("target", FlowInput("entry", "入口")));

        var graph = new ProjectGraphViewModel([], projectDirectory: directory.Root);
        var host = graph.CanonicalHost;
        Assert.IsNotNull(host);
        Assert.AreEqual(GraphScope.Project, host!.Scope);
        Assert.HasCount(2, host.Nodes);
        Assert.AreEqual(GraphInterfaceKind.Flow, host.Nodes.Single(node => node.NodeId == "source").Outputs.Single().InterfaceKind);
        Assert.AreEqual(GraphInterfaceKind.Flow, host.Nodes.Single(node => node.NodeId == "target").Inputs.Single().InterfaceKind);

        var beforeSource = store.Stories.Load("source").ToJson();
        Assert.IsFalse(host.AddNode(GraphNodeFactory.Create(GraphScope.StoryFlow, "terminate", "new_stop")));
        Assert.AreEqual("graph.project.projection.readonly", host.LastValidationIssues.Single().Code);
        Assert.IsFalse(host.SetNodeProperty("source", "display_name", JsonSerializer.SerializeToElement("改名")));
        Assert.AreEqual("graph.project.projection.readonly", host.LastValidationIssues.Single().Code);
        Assert.IsFalse(host.RemoveNode("source"));
        Assert.AreEqual("graph.node.not_deletable", host.LastValidationIssues.Single().Code);
        Assert.AreEqual(beforeSource, store.Stories.Load("source").ToJson());

        host.SetNodePosition("target", 321, 654);
        Assert.AreEqual(321, host.Nodes.Single(node => node.NodeId == "target").X);
        Assert.AreEqual(654, host.Nodes.Single(node => node.NodeId == "target").Y);
    }

    [TestMethod]
    public void ProjectGraphConnectUndoRedoAndReopenPersistsTypedEdge()
    {
        using var directory = new ProjectGraph0331Directory();
        var store = new CanonicalProjectGraphStore(directory.Root);
        store.Stories.Create(Story("source", Terminate("stop", "停止")));
        store.Stories.Create(Story("target", FlowInput("entry", "来自第一章")));

        var graph = new ProjectGraphViewModel([], projectDirectory: directory.Root);
        var host = graph.CanonicalHost!;
        var output = GraphEditorEndpoint.Output("source", "stop", GraphInterfaceKind.Flow);
        var input = GraphEditorEndpoint.Input("target", "entry", GraphInterfaceKind.Flow);
        Assert.IsTrue(host.Connect(output, input));
        var expected = new CanonicalStoryLogicConnection("source", "stop", "target", "entry", "Flow");
        Assert.AreEqual(expected, store.StoryLogicGraph.Load().Connections.Single());

        Assert.IsTrue(host.Undo());
        Assert.IsEmpty(host.Graph.Connections);
        Assert.IsEmpty(store.StoryLogicGraph.Load().Connections);

        Assert.IsTrue(host.Redo());
        Assert.AreEqual(expected, store.StoryLogicGraph.Load().Connections.Single());

        var reopened = new ProjectGraphViewModel([], projectDirectory: directory.Root);
        Assert.AreEqual(new GraphConnection("source", "stop", "target", "entry", GraphInterfaceKind.Flow),
            reopened.CanonicalHost!.Graph.Connections.Single());
        Assert.AreEqual(GraphInterfaceKind.Flow, reopened.CanonicalHost.Graph.Connections.Single().InterfaceKind);
    }

    [TestMethod]
    public void LocalSourceMayConnectToReferencedTargetButReferencedSourceIsReadOnly()
    {
        using var provider = new ProjectGraph0331Directory();
        using var consumer = new ProjectGraph0331Directory();
        var providerStore = new CanonicalProjectGraphStore(provider.Root);
        providerStore.Stories.Create(Story("Provider:story", FlowInput("provider_entry", "本地入口"), LogicInput("input", "外部输入"), LogicOutput("output", "外部输出")));
        providerStore.Memberships.Create(new CanonicalStoryMembershipManifest("Provider:story"));
        var package = Path.Combine(provider.Root, "build", "provider.dgrs");
        new DgrsStoryPackageExporter(provider.Root).Build("Provider:story", package, "0.3.3.1");

        var consumerStore = new CanonicalProjectGraphStore(consumer.Root);
        consumerStore.Stories.Create(Story("Local:source", LogicOutput("output", "本地输出")));
        consumerStore.Stories.Create(Story("Local:target", LogicInput("input", "本地输入")));
        Directory.CreateDirectory(Path.Combine(consumer.Root, "references"));
        File.Copy(package, Path.Combine(consumer.Root, "references", "provider.dgrs"));

        var graph = new ProjectGraphViewModel([], projectDirectory: consumer.Root);
        var host = graph.CanonicalHost!;
        Assert.IsTrue(graph.IsReferencedStory("Provider:story"));
        Assert.IsTrue(host.Nodes.Single(node => node.NodeId == "Provider:story").DisplayName.Contains("只读", StringComparison.Ordinal));

        var localEdge = new GraphConnection("Local:source", "output", "Provider:story", "input", GraphInterfaceKind.Logic);
        var localExpected = new CanonicalStoryLogicConnection("Local:source", "output", "Provider:story", "input", "Logic");
        Assert.IsTrue(host.Connect(
            GraphEditorEndpoint.Output("Local:source", "output", GraphInterfaceKind.Logic),
            GraphEditorEndpoint.Input("Provider:story", "input", GraphInterfaceKind.Logic)));
        var savedLocal = consumerStore.StoryLogicGraph.Load().Connections.Single();
        Assert.AreEqual(localExpected, savedLocal);

        var before = consumerStore.StoryLogicGraph.Load().Connections.ToArray();
        var referencedOutput = GraphEditorEndpoint.Output("Provider:story", "output", GraphInterfaceKind.Logic);
        var localTargetInput = GraphEditorEndpoint.Input("Local:target", "input", GraphInterfaceKind.Logic);
        Assert.IsFalse(host.CanConnect(referencedOutput, localTargetInput));
        Assert.AreEqual("story.graph.reference.readonly", host.LastValidationIssues.Single().Code);
        var accepted = host.Connect(
            referencedOutput,
            localTargetInput);
        Assert.IsFalse(accepted);
        Assert.AreEqual("story.graph.reference.readonly", host.LastValidationIssues.Single().Code);
        Assert.IsTrue(graph.HasLogicEditorError);
        CollectionAssert.AreEqual(before, consumerStore.StoryLogicGraph.Load().Connections.ToArray());
        Assert.AreEqual(localEdge, host.Graph.Connections.Single());

        Assert.IsFalse(host.CanReconnect(localEdge, referencedOutput, localTargetInput));
        Assert.AreEqual("story.graph.reference.readonly", host.LastValidationIssues.Single().Code);
        Assert.IsFalse(host.Reconnect(localEdge, referencedOutput, localTargetInput));
        Assert.AreEqual("story.graph.reference.readonly", host.LastValidationIssues.Single().Code);

        var referencedEdge = new GraphConnection(
            "Provider:story", "output", "Local:target", "input", GraphInterfaceKind.Logic);
        host.Graph.Connections.Add(referencedEdge);
        host.Refresh();
        Assert.IsFalse(host.Disconnect(referencedEdge));
        Assert.AreEqual("story.graph.reference.readonly", host.LastValidationIssues.Single().Code);
        Assert.IsFalse(host.CompleteIncidentWireDrag([referencedEdge], referencedOutput, null));
        Assert.AreEqual("story.graph.reference.readonly", host.LastValidationIssues.Single().Code);
        CollectionAssert.Contains(host.Graph.Connections, referencedEdge);
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
        File.WriteAllText(Path.Combine(Root, "project.json"), "{\"schema_version\":1,\"id\":\"test_project\",\"display_name\":\"Test Project\"}");
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
