using System.Text.Json;
using System.Collections.Specialized;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class GraphEditorHostViewModelTests
{
    [TestMethod]
    public void SuccessfulMutationsReconcileCollectionsWithoutReset()
    {
        var host = new GraphEditorHostViewModel(ScopedGraph(GraphScope.StoryFlow), GraphScope.StoryFlow);
        var nodeActions = new List<NotifyCollectionChangedAction>();
        var connectionActions = new List<NotifyCollectionChangedAction>();
        host.Nodes.CollectionChanged += (_, args) => nodeActions.Add(args.Action);
        host.Connections.CollectionChanged += (_, args) => connectionActions.Add(args.Action);

        Assert.IsTrue(host.AddNode(GraphNodeFactory.Create(GraphScope.StoryFlow, "action", "action", "Action")));
        Assert.IsTrue(host.Connect(GraphEditorEndpoint.Output("source", "out", GraphInterfaceKind.Flow),
            GraphEditorEndpoint.Input("action", "flow_in", GraphInterfaceKind.Flow)));
        Assert.IsTrue(host.Disconnect(host.Connections.Single()));

        CollectionAssert.DoesNotContain(nodeActions, NotifyCollectionChangedAction.Reset);
        CollectionAssert.DoesNotContain(connectionActions, NotifyCollectionChangedAction.Reset);
        CollectionAssert.Contains(nodeActions, NotifyCollectionChangedAction.Add);
        CollectionAssert.Contains(connectionActions, NotifyCollectionChangedAction.Add);
        CollectionAssert.Contains(connectionActions, NotifyCollectionChangedAction.Remove);
    }

    [TestMethod]
    public void MultiWireReconnectAndDisconnectEachUseOneUndoUnit()
    {
        var graph = new GraphDocument([
            new GraphNode("left_a", "action", "Left A", [new("out", "Out", false, GraphInterfaceKind.Flow)]),
            new GraphNode("left_b", "action", "Left B", [new("out", "Out", false, GraphInterfaceKind.Flow)]),
            new GraphNode("right_a", "action", "Right A", [new("in", "In", true, GraphInterfaceKind.Flow)]),
            new GraphNode("right_b", "action", "Right B", [new("in", "In", true, GraphInterfaceKind.Flow)])], [
            new GraphConnection("left_a", "out", "right_a", "in", GraphInterfaceKind.Flow),
            new GraphConnection("left_b", "out", "right_a", "in", GraphInterfaceKind.Flow)]);
        var host = new GraphEditorHostViewModel(graph, GraphScope.StoryFlow);
        var originals = graph.Connections.ToArray();
        var moving = GraphEditorEndpoint.Input("right_a", "in", GraphInterfaceKind.Flow);
        var target = GraphEditorEndpoint.Input("right_b", "in", GraphInterfaceKind.Flow);

        Assert.IsTrue(host.CompleteIncidentWireDrag(originals, moving, target));
        Assert.AreEqual(1, host.Session.UndoCount);
        Assert.IsTrue(graph.Connections.All(connection => connection.ToNodeId == "right_b"));
        Assert.IsTrue(host.Undo());
        Assert.IsTrue(graph.Connections.All(connection => connection.ToNodeId == "right_a"));

        var restored = graph.Connections.ToArray();
        Assert.IsTrue(host.CompleteIncidentWireDrag(restored, moving, null));
        Assert.AreEqual(1, host.Session.UndoCount);
        Assert.IsEmpty(graph.Connections);
        Assert.IsTrue(host.Undo());
        Assert.HasCount(2, graph.Connections);
    }

    [TestMethod]
    public void NodeCrudRefreshesProjectionPreservesIdentityPublishesValidationAndUndoRedo()
    {
        var host = new GraphEditorHostViewModel(ScopedGraph(GraphScope.StoryFlow), GraphScope.StoryFlow);
        var source = host.Nodes.Single(node => node.NodeId == "source");
        var added = GraphNodeFactory.Create(GraphScope.StoryFlow, "action", "action", "Action");

        Assert.IsTrue(host.AddNode(added));
        Assert.AreSame(source, host.Nodes.Single(node => node.NodeId == "source"));
        Assert.AreEqual("执行「物品给予」", host.Nodes.Single(node => node.NodeId == "action").DisplayName);
        Assert.IsEmpty(host.LastValidationIssues);
        Assert.IsTrue(host.Connect(GraphEditorEndpoint.Output("source", "out", GraphInterfaceKind.Flow),
            GraphEditorEndpoint.Input("action", "flow_in", GraphInterfaceKind.Flow)));
        Assert.IsTrue(host.Connect(GraphEditorEndpoint.Output("action", "flow_out", GraphInterfaceKind.Flow),
            GraphEditorEndpoint.Input("target", "in", GraphInterfaceKind.Flow)));

        Assert.IsFalse(host.RemoveNode("action"));
        Assert.IsTrue(host.LastValidationIssues.Any(issue => issue.Code == "graph.node.references.confirmation_required"));
        Assert.IsTrue(host.RemoveNode("action", confirmReferencedRemoval: true));
        Assert.IsFalse(host.Nodes.Any(node => node.NodeId == "action"));
        Assert.IsEmpty(host.Connections);
        Assert.IsTrue(host.Undo());
        Assert.IsTrue(host.Nodes.Any(node => node.NodeId == "action"));
        Assert.HasCount(2, host.Connections);
        Assert.IsTrue(host.Redo());
        Assert.IsFalse(host.Nodes.Any(node => node.NodeId == "action"));
        Assert.IsEmpty(host.Connections);
    }

    [TestMethod]
    public void OneHostProjectsAllThreeScopesThroughTheSameSurface()
    {
        foreach (var scope in new[] { GraphScope.StoryFlow, GraphScope.Session, GraphScope.Task })
        {
            var graph = ScopedGraph(scope);
            var host = new GraphEditorHostViewModel(graph, scope);

            Assert.AreSame(graph, host.Graph);
            Assert.AreEqual(scope, host.Scope);
            Assert.AreSame(graph, host.Session.Document);
            Assert.AreSame(graph, host.CommandBridge.Graph);
            Assert.AreEqual(scope, host.CommandBridge.Scope);
            Assert.HasCount(2, host.Nodes, scope.ToString());
            Assert.IsEmpty(host.Connections);
        }
    }

    [TestMethod]
    public void PortsUseOrdinalOrderAndConnectionsUseIdsRatherThanLabels()
    {
        var graph = new GraphDocument([
            new GraphNode("source", "start", "Source", [
                new("z", "Same label", false, GraphInterfaceKind.Flow, 1),
                new("a", "Same label", false, GraphInterfaceKind.Flow, 1)]),
            new GraphNode("target", "terminate", "Target", [
                new("in", "Same label", true, GraphInterfaceKind.Flow)])]);
        var host = new GraphEditorHostViewModel(graph, GraphScope.StoryFlow);

        CollectionAssert.AreEqual(new[] { "a", "z" }, host.Nodes[0].Outputs.Select(port => port.PortId).ToArray());
        Assert.IsTrue(host.Connect(GraphEditorEndpoint.Output("source", "z", GraphInterfaceKind.Flow),
            GraphEditorEndpoint.Input("target", "in", GraphInterfaceKind.Flow)));
        var edge = host.Connections.Single();
        Assert.AreEqual("source", edge.FromNodeId);
        Assert.AreEqual("z", edge.FromPortId);

        graph.Nodes[0].Ports[1].DisplayName = "Renamed";
        host.Refresh();
        Assert.AreEqual("Renamed", host.Nodes.Single(node => node.NodeId == "source").Outputs.Single(port => port.PortId == "a").DisplayName);
        Assert.AreEqual("z", host.Connections.Single().FromPortId);
    }

    [TestMethod]
    public void SuccessfulMutationUndoRedoRefreshesProjectionAndHistoryState()
    {
        var graph = ScopedGraph(GraphScope.Session);
        var host = new GraphEditorHostViewModel(graph, GraphScope.Session);
        var source = host.Nodes.Single(node => node.NodeId == "source");
        var notifications = new List<string?>();
        host.PropertyChanged += (_, args) => notifications.Add(args.PropertyName);

        Assert.IsTrue(host.Connect(GraphEditorEndpoint.Output("source", "out", GraphInterfaceKind.Flow),
            GraphEditorEndpoint.Input("target", "in", GraphInterfaceKind.Flow)));
        Assert.IsTrue(host.CanUndo);
        Assert.Contains(nameof(host.CanUndo), notifications);
        Assert.HasCount(1, host.Connections);
        Assert.AreSame(source, host.Nodes.Single(node => node.NodeId == "source"));

        Assert.IsTrue(host.Undo());
        Assert.IsTrue(host.CanRedo);
        Assert.IsEmpty(host.Connections);
        Assert.IsTrue(host.Redo());
        Assert.HasCount(1, host.Connections);
        Assert.AreSame(source, host.Nodes.Single(node => node.NodeId == "source"));
    }

    [TestMethod]
    public void PreviewAndFailedCommandsDoNotReplaceStableNodes()
    {
        var host = new GraphEditorHostViewModel(ScopedGraph(GraphScope.StoryFlow), GraphScope.StoryFlow);
        var source = host.Nodes.Single(node => node.NodeId == "source");
        var before = host.Graph.ToJson();

        Assert.IsFalse(host.CanConnect(GraphEditorEndpoint.Output("source", "missing", GraphInterfaceKind.Flow),
            GraphEditorEndpoint.Input("target", "in", GraphInterfaceKind.Flow)));
        Assert.AreSame(source, host.Nodes.Single(node => node.NodeId == "source"));
        Assert.AreEqual(before, host.Graph.ToJson());
        Assert.IsFalse(host.Connect(GraphEditorEndpoint.Output("source", "missing", GraphInterfaceKind.Flow),
            GraphEditorEndpoint.Input("target", "in", GraphInterfaceKind.Flow)));
        Assert.AreSame(source, host.Nodes.Single(node => node.NodeId == "source"));
        Assert.IsEmpty(host.Connections);
    }

    [TestMethod]
    public void SuccessfulSessionMutationReplacesStaleBridgeIssues()
    {
        var host = new GraphEditorHostViewModel(ScopedGraph(GraphScope.StoryFlow), GraphScope.StoryFlow);
        var source = host.Nodes.Single(node => node.NodeId == "source");

        Assert.IsFalse(host.CanConnect(GraphEditorEndpoint.Output("source", "missing", GraphInterfaceKind.Flow),
            GraphEditorEndpoint.Input("target", "in", GraphInterfaceKind.Flow)));
        Assert.IsNotEmpty(host.LastValidationIssues);
        Assert.IsNotEmpty(host.CommandBridge.LastValidationIssues);

        Assert.IsTrue(host.AddDynamicPort("source", "Dynamic", GraphPortDirection.Output, GraphInterfaceKind.Flow));
        Assert.IsEmpty(host.LastValidationIssues);
        Assert.IsEmpty(host.Session.LastValidationIssues);
        Assert.AreSame(source, host.Nodes.Single(node => node.NodeId == "source"));
    }

    [TestMethod]
    public void DynamicPortMutationReportsOnlyTheAffectedNode()
    {
        var host = new GraphEditorHostViewModel(ScopedGraph(GraphScope.StoryFlow), GraphScope.StoryFlow);
        var source = host.Nodes.Single(node => node.NodeId == "source");
        var target = host.Nodes.Single(node => node.NodeId == "target");
        var changed = new List<string>();
        host.PortsChanged += (_, args) => changed.AddRange(args.NodeIds);

        Assert.IsTrue(host.AddDynamicPort("source", "Dynamic", GraphPortDirection.Output, GraphInterfaceKind.Flow));
        Assert.AreEqual("source", changed.Single());
        Assert.AreSame(source, host.Nodes.Single(node => node.NodeId == "source"));
        Assert.AreSame(target, host.Nodes.Single(node => node.NodeId == "target"));

        changed.Clear();
        var portId = host.Graph.Nodes.Single(node => node.Id == "source").Ports
            .Single(port => port.DisplayName == "Dynamic").Id;
        Assert.IsTrue(host.RenamePortDisplayName("source", portId, "Renamed"));
        Assert.AreEqual("source", changed.Single());

        changed.Clear();
        Assert.IsTrue(host.RemoveDynamicPort("source", portId));
        Assert.AreEqual("source", changed.Single());
    }

    [TestMethod]
    public void DynamicPortReferencesRequireConfirmationAndUndoRestoresCleanup()
    {
        var graph = ScopedGraph(GraphScope.StoryFlow);
        var host = new GraphEditorHostViewModel(graph, GraphScope.StoryFlow);
        Assert.IsTrue(host.AddDynamicPort("source", "Dynamic", GraphPortDirection.Output, GraphInterfaceKind.Flow));
        var dynamicPort = graph.Nodes.Single(node => node.Id == "source").Ports.Single(port => port.DisplayName == "Dynamic");

        Assert.IsTrue(host.Connect(GraphEditorEndpoint.Output("source", dynamicPort.Id, GraphInterfaceKind.Flow),
            GraphEditorEndpoint.Input("target", "in", GraphInterfaceKind.Flow)));
        Assert.HasCount(1, host.GetPortReferences("source", dynamicPort.Id));
        Assert.IsFalse(host.RemoveDynamicPort("source", dynamicPort.Id));
        Assert.HasCount(1, graph.Connections);
        Assert.IsTrue(host.RemoveDynamicPort("source", dynamicPort.Id, confirmReferencedRemoval: true));
        Assert.IsEmpty(graph.Connections);
        Assert.IsFalse(graph.Nodes.Single(node => node.Id == "source").Ports.Any(port => port.Id == dynamicPort.Id));

        Assert.IsTrue(host.Undo());
        Assert.HasCount(1, graph.Connections);
        Assert.IsTrue(graph.Nodes.Single(node => node.Id == "source").Ports.Any(port => port.Id == dynamicPort.Id));
    }

    [TestMethod]
    public void RefreshPreservesUniqueNodeIdentityAndUpdatesProjection()
    {
        var graph = ScopedGraph(GraphScope.Session);
        var host = new GraphEditorHostViewModel(graph, GraphScope.Session);
        var node = host.Nodes.Single(item => item.NodeId == "source");
        var port = node.Outputs.Single();

        graph.Nodes[0].Type = "line";
        graph.Nodes[0].DisplayName = "Changed";
        graph.Nodes[0].Ports[0].DisplayName = "Changed port";
        graph.Nodes[0].Ports[0].Order = 4;
        host.Refresh();

        var updated = host.Nodes.Single(item => item.NodeId == "source");
        Assert.AreSame(node, updated);
        Assert.AreEqual("line", updated.Type);
        Assert.AreEqual("台词「Changed」", updated.DisplayName);
        Assert.AreSame(port, updated.Outputs.Single());
        Assert.AreEqual("Changed port", updated.Outputs.Single().DisplayName);
        Assert.AreEqual(4, updated.Outputs.Single().Order);
    }

    [TestMethod]
    public void NodePropertyProjectionIsReadOnlyClonedAndHostPreservesIdentity()
    {
        var graph = ScopedGraph(GraphScope.Session);
        graph.Nodes[0].Properties["config"] = JsonSerializer.SerializeToElement(new { enabled = true });
        var host = new GraphEditorHostViewModel(graph, GraphScope.Session);
        var node = host.Nodes.Single(item => item.NodeId == "source");
        var projection = node.Properties["config"];

        Assert.IsTrue(host.SetNodeProperty("source", "count", JsonSerializer.SerializeToElement(3)));
        Assert.AreSame(node, host.Nodes.Single(item => item.NodeId == "source"));
        Assert.IsTrue(host.Nodes.Single(item => item.NodeId == "source").Properties.ContainsKey("count"));
        Assert.IsTrue(host.RemoveNodeProperty("source", "count"));
        Assert.IsFalse(host.Nodes.Single(item => item.NodeId == "source").Properties.ContainsKey("count"));
        Assert.IsTrue(JsonElement.DeepEquals(projection, graph.Nodes[0].Properties["config"]));
    }

    [TestMethod]
    public void LayoutIsFiniteDeterministicAndExcludedFromGraphJson()
    {
        var graph = ScopedGraph(GraphScope.StoryFlow);
        var layout = new Dictionary<string, GraphEditorNodePosition>(StringComparer.Ordinal)
        {
            ["target"] = new(777, 333),
            ["ignored"] = new(double.NaN, double.PositiveInfinity),
        };
        var host = new GraphEditorHostViewModel(graph, GraphScope.StoryFlow, layout);
        var json = graph.ToJson();
        Assert.AreEqual(new GraphEditorNodePosition(777, 333), host.Nodes.Single(node => node.NodeId == "target").Position);
        Assert.IsTrue(host.Nodes.All(node => node.Position.IsFinite));

        var second = new GraphEditorHostViewModel(ScopedGraph(GraphScope.StoryFlow), GraphScope.StoryFlow);
        Assert.AreEqual(host.Nodes.Single(node => node.NodeId == "source").Position,
            second.Nodes.Single(node => node.NodeId == "source").Position);

        host.SetNodePosition("source", 123, 456);
        host.Refresh();
        Assert.AreEqual(new GraphEditorNodePosition(123, 456), host.Nodes.Single(node => node.NodeId == "source").Position);
        Assert.AreEqual(json, graph.ToJson());
    }

    [TestMethod]
    public void DuplicateAndBlankIdsAreProjectedWithoutThrowing()
    {
        var graph = new GraphDocument([
            new GraphNode("", "line", "Blank", [new("p", "P", true, GraphInterfaceKind.Flow)]),
            new GraphNode("duplicate", "line", "One", []),
            new GraphNode("duplicate", "line", "Two", []),
            null!]);
        var host = new GraphEditorHostViewModel(graph, GraphScope.Session);

        Assert.HasCount(3, host.Nodes);
        host.Refresh();
        Assert.HasCount(3, host.Nodes);
        Assert.AreEqual(2, host.Nodes.Count(node => node.NodeId == "duplicate"));
    }

    private static GraphDocument ScopedGraph(GraphScope scope)
    {
        var targetType = scope switch
        {
            GraphScope.StoryFlow => "terminate",
            GraphScope.Session => "end",
            _ => "settle",
        };
        var kind = scope == GraphScope.Task ? GraphInterfaceKind.Logic : GraphInterfaceKind.Flow;
        return new GraphDocument([
            new GraphNode("source", scope == GraphScope.Task ? "objective" : "start", "Source",
                [new("out", "Out", false, kind)]),
            new GraphNode("target", targetType, "Target", [new("in", "In", true, kind)])]);
    }
}
