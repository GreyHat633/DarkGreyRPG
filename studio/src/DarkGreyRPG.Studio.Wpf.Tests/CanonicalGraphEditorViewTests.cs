using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Input;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
[DoNotParallelize]
public sealed class CanonicalGraphEditorViewTests
{
    [STATestMethod]
    public void IncrementalNodeAndConnectionChangesKeepExistingVisualViewportAndSelection()
    {
        var host = new GraphEditorHostViewModel(Graph(GraphScope.StoryFlow), GraphScope.StoryFlow);
        var view = Arrange(host);
        var sourceVisual = view.NodeVisuals.Single(node => node.Node?.NodeId == "source");
        var targetVisual = view.NodeVisuals.Single(node => node.Node?.NodeId == "target");
        view.ViewportController.PanBy(37, -19);
        view.ViewportController.SetZoomAt(1.25, new Point(200, 150));
        var viewport = (view.ViewportController.Zoom, view.ViewportController.PanX, view.ViewportController.PanY);
        Assert.IsTrue(view.SelectNode("target"));

        Assert.IsTrue(host.AddNode(GraphNodeFactory.Create(GraphScope.StoryFlow, "action", "action", "Action")));
        Assert.AreSame(sourceVisual, view.NodeVisuals.Single(node => node.Node?.NodeId == "source"));
        Assert.AreSame(targetVisual, view.NodeVisuals.Single(node => node.Node?.NodeId == "target"));
        Assert.AreEqual("target", view.SelectedNode?.NodeId);
        Assert.AreEqual(viewport, (view.ViewportController.Zoom, view.ViewportController.PanX, view.ViewportController.PanY));

        Assert.IsTrue(host.Connect(GraphEditorEndpoint.Output("source", "out", GraphInterfaceKind.Flow),
            GraphEditorEndpoint.Input("action", "flow_in", GraphInterfaceKind.Flow)));
        Assert.AreSame(sourceVisual, view.NodeVisuals.Single(node => node.Node?.NodeId == "source"));
        Assert.AreSame(targetVisual, view.NodeVisuals.Single(node => node.Node?.NodeId == "target"));
        Assert.HasCount(1, view.ConnectionVisuals);

        Assert.IsTrue(host.Disconnect(host.Connections.Single()));
        Assert.AreSame(sourceVisual, view.NodeVisuals.Single(node => node.Node?.NodeId == "source"));
        Assert.AreSame(targetVisual, view.NodeVisuals.Single(node => node.Node?.NodeId == "target"));
        Assert.IsEmpty(view.ConnectionVisuals);
    }

    [STATestMethod]
    public void OneTypedViewAcceptsStorySessionAndTaskHosts()
    {
        foreach (var scope in new[] { GraphScope.StoryFlow, GraphScope.Session, GraphScope.Task })
        {
            var host = new GraphEditorHostViewModel(Graph(scope), scope);
            var view = Arrange(host);
            Assert.AreSame(host, view.Host);
            Assert.AreEqual(scope, view.Host!.Scope);
            Assert.IsTrue(host.Nodes.All(node => node.Position.IsFinite));
            Assert.HasCount(2, view.NodeVisuals);
            Assert.HasCount(2, view.PortVisuals);
        }
    }

    [STATestMethod]
    public void ConnectionAndDisconnectRouteThroughHostWithoutChangingLayout()
    {
        var graph = Graph(GraphScope.StoryFlow);
        var host = new GraphEditorHostViewModel(graph, GraphScope.StoryFlow);
        var view = Arrange(host);
        var before = graph.ToJson();
        Assert.IsTrue(host.Connect(GraphEditorEndpoint.Output("source", "out", GraphInterfaceKind.Flow),
            GraphEditorEndpoint.Input("target", "in", GraphInterfaceKind.Flow)));
        Assert.IsTrue(host.Disconnect(host.Connections.Single()));
        Assert.AreEqual(before, graph.ToJson());
        Assert.IsTrue(view.ViewportController.Zoom is > 0 and <= 8);
    }

    [STATestMethod]
    public void ArrangedViewProjectsOrderedPortIdentityKindAndStyledHitPaths()
    {
        var graph = new GraphDocument([
            new GraphNode("source", "objective", "Source", [
                new("z", "Same", false, GraphInterfaceKind.Logic, 2),
                new("a", "Same", false, GraphInterfaceKind.Logic, 1)]),
            new GraphNode("target", "settle", "Target", [new("in", "Input", true, GraphInterfaceKind.Logic)])]);
        var host = new GraphEditorHostViewModel(graph, GraphScope.Task);
        Assert.IsTrue(host.Connect(GraphEditorEndpoint.Output("source", "a", GraphInterfaceKind.Logic),
            GraphEditorEndpoint.Input("target", "in", GraphInterfaceKind.Logic)));
        var view = Arrange(host);
        var source = view.NodeVisuals.Single(node => node.Node?.NodeId == "source");

        CollectionAssert.AreEqual(new[] { "a", "z" }, source.Node!.Outputs.Select(port => port.PortId).ToArray());
        Assert.HasCount(3, view.PortVisuals);
        Assert.IsTrue(view.PortVisuals.Any(port => AutomationProperties.GetAutomationId(port) == "CanonicalGraphPort_source_a"));
        Assert.IsTrue(view.PortVisuals.Any(port => AutomationProperties.GetAutomationId(port) == "CanonicalGraphPort_target_in"));
        Assert.IsTrue(view.PortVisuals.All(port => AutomationProperties.GetName(port).Contains("Logic", StringComparison.Ordinal)));

        Assert.HasCount(1, view.ConnectionVisuals);
        Assert.HasCount(1, view.ConnectionHitTargets);
        var wire = view.ConnectionVisuals.Single();
        Assert.AreEqual(GraphConnectionVisualStyle.LogicNormalColor, ((SolidColorBrush)wire.Stroke).Color);
        StringAssert.Contains(AutomationProperties.GetName(view.ConnectionHitTargets.Single()), "Logic");
        StringAssert.Contains(AutomationProperties.GetAutomationId(view.ConnectionHitTargets.Single()), "source_a_target_in");
    }

    [STATestMethod]
    public void HostNullClearsArrangedVisualsWithoutMutatingGraph()
    {
        var graph = Graph(GraphScope.StoryFlow);
        var host = new GraphEditorHostViewModel(graph, GraphScope.StoryFlow);
        var view = Arrange(host);
        var before = graph.ToJson();

        view.Host = null;

        Assert.IsEmpty(view.NodeVisuals);
        Assert.IsEmpty(view.PortVisuals);
        Assert.IsEmpty(view.ConnectionHitTargets);
        Assert.IsEmpty(view.ConnectionVisuals);
        Assert.AreEqual(before, graph.ToJson());
    }

    [STATestMethod]
    public void NodeSelectionIsMutuallyExclusiveAndSurvivesRefreshByUniqueId()
    {
        var host = new GraphEditorHostViewModel(Graph(GraphScope.StoryFlow), GraphScope.StoryFlow);
        var view = Arrange(host);
        var target = view.NodeVisuals.Single(node => node.Node?.NodeId == "target");
        var source = view.NodeVisuals.Single(node => node.Node?.NodeId == "source");

        Assert.IsTrue(view.SelectNode(target.Node));
        Assert.AreSame(target.Node, view.SelectedNode);
        Assert.IsTrue(target.IsSelected);
        Assert.IsFalse(source.IsSelected);

        host.Refresh();

        Assert.AreEqual("target", view.SelectedNode?.NodeId);
        Assert.IsTrue(view.NodeVisuals.Single(node => node.Node?.NodeId == "target").IsSelected);
    }

    [STATestMethod]
    public void ConnectionSelectionClearsNodeAndDeleteRemovesOnlyConnection()
    {
        var graph = new GraphDocument([
            new GraphNode("source", "objective", "Source", [new("out", "Output", false, GraphInterfaceKind.Logic)]),
            new GraphNode("target", "settle", "Target", [new("in", "Input", true, GraphInterfaceKind.Logic)])]);
        var host = new GraphEditorHostViewModel(graph, GraphScope.Task);
        Assert.IsTrue(host.Connect(GraphEditorEndpoint.Output("source", "out", GraphInterfaceKind.Logic),
            GraphEditorEndpoint.Input("target", "in", GraphInterfaceKind.Logic)));
        var view = Arrange(host);
        Assert.IsTrue(view.SelectNode("target"));

        var connection = host.Connections.Single();
        Assert.IsTrue(view.BeginExistingConnectionDrag(connection));
        Assert.IsTrue(view.HandleKeyboardCommand(Key.Escape));
        Assert.IsNull(view.SelectedNode);
        Assert.AreSame(connection, view.SelectedConnection);

        Assert.IsTrue(view.HandleKeyboardCommand(Key.Delete));
        Assert.IsNull(view.SelectedConnection);
        Assert.IsEmpty(host.Connections);
        Assert.HasCount(2, host.Nodes);
    }

    [STATestMethod]
    public void UnreferencedNodeDeleteUsesHostAndClearsSelection()
    {
        var graph = Graph(GraphScope.StoryFlow);
        var host = new GraphEditorHostViewModel(graph, GraphScope.StoryFlow);
        var view = Arrange(host);
        Assert.IsTrue(view.SelectNode("target"));
        var before = graph.ToJson();

        Assert.IsTrue(view.RemoveSelectedNode());
        Assert.IsNull(view.SelectedNode);
        Assert.IsFalse(host.Nodes.Any(node => node.NodeId == "target"));
        Assert.AreNotEqual(before, graph.ToJson());
    }

    [STATestMethod]
    public void ReferencedDeleteFailsClosedThenConfirmedDeleteSucceeds()
    {
        var graph = new GraphDocument([
            new GraphNode("source", "line", "Source", [new("flow_out", "Output", false, GraphInterfaceKind.Flow)]),
            new GraphNode("target", "end", "Target", [new("flow_in", "Input", true, GraphInterfaceKind.Flow)])]);
        var host = new GraphEditorHostViewModel(graph, GraphScope.Session);
        Assert.IsTrue(host.CompleteConnectionDrag(GraphEditorEndpoint.Output("source", "flow_out", GraphInterfaceKind.Flow),
            GraphEditorEndpoint.Input("target", "flow_in", GraphInterfaceKind.Flow)));
        var view = Arrange(host);
        Assert.IsTrue(view.SelectNode("source"));

        Assert.IsFalse(view.RemoveSelectedNode());
        Assert.AreEqual("source", view.SelectedNode?.NodeId);
        Assert.IsTrue(host.LastValidationIssues.Any(issue => issue.Code == "graph.node.references.confirmation_required"),
            string.Join(",", host.LastValidationIssues.Select(issue => issue.Code)));

        Assert.IsTrue(view.RemoveSelectedNode(confirmReferencedRemoval: true));
        Assert.IsNull(view.SelectedNode);
        Assert.HasCount(1, host.Nodes);
        Assert.IsEmpty(host.Connections);
    }

    [STATestMethod]
    public void NonDeletableDeleteRetainsSelectionAndProblem()
    {
        var host = new GraphEditorHostViewModel(Graph(GraphScope.StoryFlow), GraphScope.StoryFlow);
        var view = Arrange(host);
        Assert.IsTrue(view.SelectNode("source"));

        Assert.IsFalse(view.RemoveSelectedNode());
        Assert.AreEqual("source", view.SelectedNode?.NodeId);
        Assert.IsTrue(host.LastValidationIssues.Any(issue => issue.Code == "graph.node.not_deletable"));
    }

    [STATestMethod]
    public void BlankOrDuplicateNodeIdsDoNotPreserveAmbiguousSelection()
    {
        var graph = new GraphDocument([
            new GraphNode("dup", "line", "One", []),
            new GraphNode("dup", "line", "Two", []),
            new GraphNode("", "line", "Blank", [])]);
        var host = new GraphEditorHostViewModel(graph, GraphScope.Session);
        var view = Arrange(host);

        Assert.IsFalse(view.SelectNode("dup"));
        Assert.IsNull(view.SelectedNode);
        Assert.IsFalse(view.SelectNode(""));
        Assert.IsNull(view.SelectedNode);
        Assert.IsTrue(view.SelectNode(view.NodeVisuals.Single(node => node.Node?.DisplayName == "One").Node));

        host.Refresh();

        Assert.IsNull(view.SelectedNode);
        Assert.IsFalse(view.NodeVisuals.Any(node => node.IsSelected));
    }

    [STATestMethod]
    public void ImplicitDataContextHostClearsWhenContextBecomesNonHost()
    {
        var graph = Graph(GraphScope.StoryFlow);
        var host = new GraphEditorHostViewModel(graph, GraphScope.StoryFlow);
        var view = ArrangeWithoutHost();
        view.DataContext = host;
        Assert.AreSame(host, view.Host);
        var before = graph.ToJson();

        view.DataContext = new object();

        Assert.IsNull(view.Host);
        Assert.IsEmpty(view.NodeVisuals);
        Assert.IsEmpty(view.ConnectionVisuals);
        Assert.AreEqual(before, graph.ToJson());
    }

    [STATestMethod]
    public void ExplicitHostSurvivesUnrelatedDataContextChanges()
    {
        var host = new GraphEditorHostViewModel(Graph(GraphScope.StoryFlow), GraphScope.StoryFlow);
        var view = Arrange(host);

        view.DataContext = new object();

        Assert.AreSame(host, view.Host);
        Assert.HasCount(2, view.NodeVisuals);
    }

    [STATestMethod]
    public void CanvasFocusTargetUsesRoutedKeyboardPathAndEscapeCancelsWire()
    {
        var graph = Graph(GraphScope.StoryFlow);
        var host = new GraphEditorHostViewModel(graph, GraphScope.StoryFlow);
        var view = Arrange(host);
        var output = view.PortVisuals.Single(port => port.NodeId == "source");
        var before = graph.ToJson();

        Assert.AreEqual("CanonicalGraphCanvas", AutomationProperties.GetAutomationId(view.KeyboardCommandTarget));
        Assert.IsTrue(view.KeyboardCommandTarget.Focusable);
        _ = view.KeyboardCommandTarget.Focus();
        Assert.IsTrue(view.BeginNewConnectionDrag(output));
        Assert.IsTrue(view.HandleKeyboardCommand(Key.Escape));
        Assert.AreEqual(before, graph.ToJson());
    }

    [STATestMethod]
    public void DirectConnectedLogicOutputCreatesFanOutWhileExplicitHitReconnectCarriesOriginal()
    {
        var graph = new GraphDocument([
            new GraphNode("source", "objective", "Source", [new("out", "Output", false, GraphInterfaceKind.Logic)]),
            new GraphNode("one", "settle", "One", [new("in", "Input", true, GraphInterfaceKind.Logic)]),
            new GraphNode("two", "settle", "Two", [new("in", "Input", true, GraphInterfaceKind.Logic)]),
            new GraphNode("three", "settle", "Three", [new("in", "Input", true, GraphInterfaceKind.Logic)])]);
        var host = new GraphEditorHostViewModel(graph, GraphScope.Task);
        Assert.IsTrue(host.Connect(GraphEditorEndpoint.Output("source", "out", GraphInterfaceKind.Logic),
            GraphEditorEndpoint.Input("one", "in", GraphInterfaceKind.Logic)));
        var view = Arrange(host);
        var output = view.PortVisuals.Single(port => port.NodeId == "source");
        var secondInput = view.PortVisuals.Single(port => port.NodeId == "two");
        var thirdInput = view.PortVisuals.Single(port => port.NodeId == "three");

        Assert.IsTrue(view.BeginNewConnectionDrag(output));
        Assert.IsTrue(view.CompleteConnectionDrag(secondInput));
        Assert.HasCount(2, host.Connections);

        var original = host.Connections.Single(connection => connection.ToNodeId == "one");
        Assert.IsTrue(view.BeginExistingConnectionDrag(host.Connections.Single(connection => connection.ToNodeId == "one")));
        Assert.IsTrue(view.CompleteConnectionDrag(thirdInput));
        Assert.HasCount(2, host.Connections);
        Assert.IsFalse(host.Connections.Any(connection => connection.ToNodeId == original.ToNodeId));
        Assert.IsTrue(host.Connections.Any(connection => connection.ToNodeId == "three"));
    }

    [STATestMethod]
    public void ExistingConnectionHitNearInputReplacesInputAndKeepsOutput()
    {
        var graph = new GraphDocument([
            new GraphNode("source", "objective", "Source", [new("out", "Output", false, GraphInterfaceKind.Logic)]),
            new GraphNode("one", "settle", "One", [new("in", "Input", true, GraphInterfaceKind.Logic)]),
            new GraphNode("two", "settle", "Two", [new("in", "Input", true, GraphInterfaceKind.Logic)])]);
        var host = new GraphEditorHostViewModel(graph, GraphScope.Task);
        Assert.IsTrue(host.Connect(GraphEditorEndpoint.Output("source", "out", GraphInterfaceKind.Logic),
            GraphEditorEndpoint.Input("one", "in", GraphInterfaceKind.Logic)));
        var view = Arrange(host);
        var connection = host.Connections.Single();
        var input = view.PortVisuals.Single(port => port.NodeId == "one");
        var hit = view.ConnectionHitTargets.Single();
        var geometry = (PathGeometry)hit.Data;
        var inputPoint = ((BezierSegment)geometry.Figures[0].Segments[0]).Point3;

        Assert.IsTrue(view.BeginExistingConnectionDrag(connection, inputPoint));
        Assert.IsTrue(view.PortVisuals.Single(port => port.NodeId == "source").IsConnecting);
        Assert.IsFalse(input.IsConnecting);
        Assert.IsTrue(view.CompleteConnectionDrag(view.PortVisuals.Single(port => port.NodeId == "two")));

        var replacement = host.Connections.Single();
        Assert.AreEqual("source", replacement.FromNodeId);
        Assert.AreEqual("out", replacement.FromPortId);
        Assert.AreEqual("two", replacement.ToNodeId);
        Assert.AreEqual("in", replacement.ToPortId);
    }

    [STATestMethod]
    public void ExistingConnectionHitNearOutputReplacesOutputAndKeepsInput()
    {
        var graph = new GraphDocument([
            new GraphNode("source", "objective", "Source", [new("out", "Output", false, GraphInterfaceKind.Logic)]),
            new GraphNode("replacement", "objective", "Replacement", [new("out", "Output", false, GraphInterfaceKind.Logic)]),
            new GraphNode("target", "settle", "Target", [new("in", "Input", true, GraphInterfaceKind.Logic)])]);
        var host = new GraphEditorHostViewModel(graph, GraphScope.Task);
        Assert.IsTrue(host.Connect(GraphEditorEndpoint.Output("source", "out", GraphInterfaceKind.Logic),
            GraphEditorEndpoint.Input("target", "in", GraphInterfaceKind.Logic)));
        var view = Arrange(host);
        var connection = host.Connections.Single();
        var hit = view.ConnectionHitTargets.Single();
        var geometry = (PathGeometry)hit.Data;
        var outputPoint = geometry.Figures[0].StartPoint;

        Assert.IsTrue(view.BeginExistingConnectionDrag(connection, outputPoint));
        Assert.IsTrue(view.PortVisuals.Single(port => port.NodeId == "target").IsConnecting);
        Assert.IsFalse(view.PortVisuals.Single(port => port.NodeId == "source").IsConnecting);
        var beforeWrongDirectionDrop = graph.ToJson();
        Assert.IsFalse(view.CompleteConnectionDrag(view.PortVisuals.Single(port => port.NodeId == "target")));
        Assert.AreEqual(beforeWrongDirectionDrop, graph.ToJson());

        Assert.IsTrue(view.BeginExistingConnectionDrag(connection, outputPoint));
        Assert.IsTrue(view.CompleteConnectionDrag(view.PortVisuals.Single(port => port.NodeId == "replacement")));

        var replacement = host.Connections.Single();
        Assert.AreEqual("replacement", replacement.FromNodeId);
        Assert.AreEqual("out", replacement.FromPortId);
        Assert.AreEqual("target", replacement.ToNodeId);
        Assert.AreEqual("in", replacement.ToPortId);
    }

    [STATestMethod]
    public void ExistingConnectionBlankDropDisconnectsFromInputSide()
    {
        var graph = new GraphDocument([
            new GraphNode("source", "objective", "Source", [new("out", "Output", false, GraphInterfaceKind.Logic)]),
            new GraphNode("target", "settle", "Target", [new("in", "Input", true, GraphInterfaceKind.Logic)])]);
        var host = new GraphEditorHostViewModel(graph, GraphScope.Task);
        Assert.IsTrue(host.Connect(GraphEditorEndpoint.Output("source", "out", GraphInterfaceKind.Logic),
            GraphEditorEndpoint.Input("target", "in", GraphInterfaceKind.Logic)));
        var view = Arrange(host);
        var connection = host.Connections.Single();
        var hit = view.ConnectionHitTargets.Single();
        var geometry = (PathGeometry)hit.Data;
        var inputPoint = ((BezierSegment)geometry.Figures[0].Segments[0]).Point3;

        Assert.IsTrue(view.BeginExistingConnectionDrag(connection, inputPoint));
        Assert.IsTrue(view.CompleteConnectionDrag(null));
        Assert.IsEmpty(host.Connections);
    }

    [STATestMethod]
    public void MalformedProjectionDoesNotThrowOrMutateGraph()
    {
        var graph = new GraphDocument([new GraphNode("", "line", "bad", [new("", "", false, GraphInterfaceKind.Flow)]), null!]);
        var before = graph.ToJson();
        var host = new GraphEditorHostViewModel(graph, GraphScope.Session);
        _ = new CanonicalGraphEditorView(host);
        Assert.AreEqual(before, graph.ToJson());
        Assert.IsTrue(host.Nodes.All(node => node.Position.IsFinite));
    }

    [STATestMethod]
    public void AuthoringCatalogUsesScopedCanonicalOrderAndFirstCategoryAppearance()
    {
        var host = new GraphEditorHostViewModel(Graph(GraphScope.Session), GraphScope.Session);
        var view = Arrange(host);

        CollectionAssert.AreEqual(
            new[] { "start", "line", "choice", "narration", "condition", "and", "or", "not", "logic_output", "logic_input", "end" },
            view.AuthoringDefinitions.Select(definition => definition.Type).ToArray());
        Assert.IsFalse(view.AuthoringDefinitions.Any(definition => definition.Type == "legacy_jump"));
        CollectionAssert.AreEqual(new[] { "会话", "逻辑", "结束" },
            view.AuthoringCategories.Select(category => category.Name).ToArray());
        Assert.IsTrue(view.CanAuthorNodeType("choice"));
        Assert.IsFalse(view.CanAuthorNodeType("start"));
        Assert.IsFalse(view.CanAuthorNodeType("legacy_jump"));
        Assert.IsTrue(view.CanAuthorNodeType("line"));

        var noHost = ArrangeWithoutHost();
        Assert.IsEmpty(noHost.AuthoringDefinitions);
        Assert.IsEmpty(noHost.AuthoringCategories);
    }

    [STATestMethod]
    public void StoryAggregateTypesAreVisibleButRequireResourceDragInitializer()
    {
        var host = new GraphEditorHostViewModel(Graph(GraphScope.StoryFlow), GraphScope.StoryFlow);
        var view = Arrange(host);

        Assert.IsTrue(view.AuthoringDefinitions.Any(definition => definition.Type == "session"));
        Assert.IsTrue(view.AuthoringDefinitions.Any(definition => definition.Type == "task"));
        Assert.IsFalse(view.CanAuthorNodeType("session"));
        Assert.IsFalse(view.CanAuthorNodeType("task"));
    }

    [STATestMethod]
    public void CanvasContextMenuUsesCanonicalGroupsAvailabilityAndGraphPosition()
    {
        var ids = new Queue<string?>(["menu-line"]);
        var host = new GraphEditorHostViewModel(Graph(GraphScope.Session), GraphScope.Session);
        var view = Arrange(host, ids.Dequeue);

        var menu = view.CreateCanvasContextMenu(new Point(73.5, 144.25));
        Assert.HasCount(1, menu.Items);
        var add = (MenuItem)menu.Items[0];
        Assert.AreEqual("添加节点", add.Header);
        CollectionAssert.AreEqual(
            new[] { "会话", "逻辑", "结束" },
            add.Items.Cast<MenuItem>().Select(item => item.Header).ToArray());

        var session = add.Items.Cast<MenuItem>().Single(item => Equals(item.Header, "会话"));
        var start = session.Items.Cast<MenuItem>().Single(item => Equals(item.Header, "起始"));
        var line = session.Items.Cast<MenuItem>().Single(item => Equals(item.Header, "台词"));
        var choice = session.Items.Cast<MenuItem>().Single(item => Equals(item.Header, "选择"));
        Assert.IsFalse(start.IsEnabled);
        Assert.IsTrue(choice.IsEnabled);
        Assert.IsTrue(line.IsEnabled);

        line.RaiseEvent(new RoutedEventArgs(MenuItem.ClickEvent));

        Assert.AreEqual(new GraphEditorNodePosition(73.5, 144.25), host.Layout["menu-line"]);
        Assert.AreEqual("menu-line", view.SelectedNode?.NodeId);
        Assert.IsEmpty(ids);
    }

    [STATestMethod]
    public void AddNodeAtCommitsFixedCandidateAtRequestedPositionAndSelectsIt()
    {
        var ids = new Queue<string?>(["line-1"]);
        var host = new GraphEditorHostViewModel(Graph(GraphScope.Session), GraphScope.Session);
        var view = Arrange(host, ids.Dequeue, () => "unused");
        Assert.IsTrue(view.SelectNode("source"));
        var connection = host.Connections.SingleOrDefault();

        var before = host.Graph.ToJson();
        Assert.IsTrue(view.AddNodeAt("line", 321.5, 98.25));

        Assert.AreNotEqual(before, host.Graph.ToJson());
        Assert.AreEqual(new GraphEditorNodePosition(321.5, 98.25), host.Layout["line-1"]);
        Assert.AreEqual("line-1", view.SelectedNode?.NodeId);
        Assert.IsNull(view.SelectedConnection);
        Assert.IsEmpty(view.LastAuthoringIssues);
        Assert.IsEmpty(host.LastValidationIssues);
        Assert.IsEmpty(ids);
        _ = connection;
    }

    [STATestMethod]
    public void AddAndOrUsesExactlyTwoInjectedDynamicIdsAndNoAvailabilityConsumption()
    {
        var nodeIds = new Queue<string?>(["and-1"]);
        var dynamicIds = new Queue<string?>(["logic-a", "logic-b"]);
        var nodeCalls = 0;
        var dynamicCalls = 0;
        var host = new GraphEditorHostViewModel(Graph(GraphScope.Task), GraphScope.Task);
        var view = Arrange(host, () => { nodeCalls++; return nodeIds.Dequeue(); },
            () => { dynamicCalls++; return dynamicIds.Dequeue(); });

        Assert.IsTrue(view.CanAuthorNodeType("and"));
        Assert.AreEqual(0, nodeCalls);
        Assert.AreEqual(0, dynamicCalls);
        Assert.IsTrue(view.AddNodeAt("and", 41, 52));

        var added = host.Graph.Nodes.Single(node => node.Id == "and-1");
        CollectionAssert.AreEqual(
            new[] { "logic-a", "logic-b" },
            added.Ports.Where(port => port.Id is "logic-a" or "logic-b").Select(port => port.Id).ToArray());
        Assert.IsEmpty(GraphNodeShapeValidator.Validate(added, GraphScope.Task));
        Assert.AreEqual(1, nodeCalls);
        Assert.AreEqual(2, dynamicCalls);
        Assert.AreEqual(new GraphEditorNodePosition(41, 52), host.Layout["and-1"]);
    }

    [STATestMethod]
    public void FailedBlankOrDuplicateGeneratedIdDoesNotRetryOrMutateGraphLayoutOrSelection()
    {
        var sourceCalls = 0;
        var host = new GraphEditorHostViewModel(Graph(GraphScope.Session), GraphScope.Session);
        var view = Arrange(host, () => { sourceCalls++; return ""; });
        Assert.IsTrue(view.SelectNode("source"));
        var beforeJson = host.Graph.ToJson();
        var beforeLayout = host.Layout.ToDictionary(pair => pair.Key, pair => pair.Value);

        Assert.IsFalse(view.AddNodeAt("line", 10, 20));
        Assert.AreEqual(1, sourceCalls);
        Assert.AreEqual(beforeJson, host.Graph.ToJson());
        CollectionAssert.AreEquivalent(beforeLayout.ToArray(), host.Layout.ToArray());
        Assert.AreEqual("source", view.SelectedNode?.NodeId);
        Assert.AreEqual("graph.node.create.id.required", view.LastAuthoringIssues.Single().Code);

        sourceCalls = 0;
        var duplicateView = Arrange(host, () => { sourceCalls++; return "source"; });
        Assert.IsFalse(duplicateView.AddNodeAt("line", 10, 20));
        Assert.AreEqual(1, sourceCalls);
        Assert.AreEqual(beforeJson, host.Graph.ToJson());
        Assert.AreEqual("graph.node.create.id.duplicate", duplicateView.LastAuthoringIssues.Single().Code);
    }

    private static GraphDocument Graph(GraphScope scope)
    {
        var kind = scope == GraphScope.Task ? GraphInterfaceKind.Logic : GraphInterfaceKind.Flow;
        var targetType = scope switch { GraphScope.StoryFlow => "terminate", GraphScope.Session => "end", _ => "settle" };
        return new GraphDocument([
            new GraphNode("source", scope == GraphScope.Task ? "objective" : "start", "Source", [new("out", "Output", false, kind)]),
            new GraphNode("target", targetType, "Target", [new("in", "Input", true, kind)])]);
    }

    private static CanonicalGraphEditorView Arrange(GraphEditorHostViewModel host,
        Func<string?>? nodeIdSource = null, Func<string?>? dynamicPortIdSource = null)
    {
        var view = new CanonicalGraphEditorView(host, nodeIdSource, dynamicPortIdSource);
        var root = new Grid { Width = 900, Height = 600 };
        root.Children.Add(view);
        root.Measure(new Size(900, 600));
        root.Arrange(new Rect(0, 0, 900, 600));
        root.UpdateLayout();
        return view;
    }

    private static CanonicalGraphEditorView ArrangeWithoutHost()
    {
        var view = new CanonicalGraphEditorView();
        var root = new Grid { Width = 900, Height = 600 };
        root.Children.Add(view);
        root.Measure(new Size(900, 600));
        root.Arrange(new Rect(0, 0, 900, 600));
        root.UpdateLayout();
        return view;
    }
}
