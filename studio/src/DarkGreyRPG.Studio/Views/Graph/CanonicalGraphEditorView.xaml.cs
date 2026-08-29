using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Validation;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views;

namespace DarkGreyRPG.Studio.Views.Graph;

/// <summary>One category in the read-only canonical authoring catalog.</summary>
public sealed record GraphNodeAuthoringCategory(
    string Name,
    IReadOnlyList<GraphNodeDefinition> Definitions);

public enum GraphSelectionKind
{
    Clear,
    Node,
    Connection,
}

public sealed class GraphSelectionChangedEventArgs : EventArgs
{
    public GraphSelectionChangedEventArgs(GraphSelectionKind kind,
        GraphEditorNodeViewModel? node = null,
        GraphEditorConnectionViewModel? connection = null)
    {
        Kind = kind;
        Node = node;
        Connection = connection;
    }

    public GraphSelectionKind Kind { get; }
    public GraphSelectionKind SelectionKind => Kind;
    public GraphEditorNodeViewModel? Node { get; }
    public GraphEditorNodeViewModel? SelectedNode => Node;
    public GraphEditorConnectionViewModel? Connection { get; }
    public GraphEditorConnectionViewModel? SelectedConnection => Connection;
    public bool IsClear => Kind == GraphSelectionKind.Clear;
}

/// <summary>
/// The single visual host for canonical Story Flow, Session, and Task graphs.
/// It owns only transient visual state; graph edits go through the host VM.
/// </summary>
public partial class CanonicalGraphEditorView : UserControl
{
    private const double NodeWidth = 232;
    private const double NodeHeight = 100;
    private readonly GraphViewportController _viewportController = new();
    private readonly GraphPointerState _pointerState = new();
    private readonly Dictionary<GraphEditorNodeViewModel, CanonicalGraphNodeControl> _nodeVisuals = [];
    private readonly Dictionary<string, FlowPortControl> _ports = new(StringComparer.Ordinal);
    private readonly Dictionary<Path, GraphEditorConnectionViewModel> _connectionHits = [];
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly TranslateTransform _translate = new();
    private GraphEditorHostViewModel? _host;
    private GraphEditorNodeViewModel? _dragNode;
    private Point _pointerStart;
    private Point _dragOrigin;
    private GraphEditorEndpoint? _wireStart;
    private GraphConnection? _wireOriginal;
    private Path? _draftWire;
    private GraphEditorConnectionViewModel? _selectedConnection;
    private GraphEditorNodeViewModel? _selectedNode;
    private string? _pendingSelectedNodeId;
    private readonly Func<string?> _nodeIdSource;
    private readonly GraphNodeAuthoringService _authoringService;
    private IReadOnlyList<ValidationIssue> _lastAuthoringIssues = [];
    private Point _contextGraphPoint;
    private bool _canceling;
    private bool _hostEventsAttached;
    private bool _hostFromDataContext;
    private bool _settingHostFromDataContext;

    public static readonly DependencyProperty HostProperty = DependencyProperty.Register(
        nameof(Host), typeof(GraphEditorHostViewModel), typeof(CanonicalGraphEditorView),
        new PropertyMetadata(null, OnHostChanged));

    public CanonicalGraphEditorView()
        : this(host: null, nodeIdSource: null, authoringService: null, initialize: true)
    {
    }

    public CanonicalGraphEditorView(GraphEditorHostViewModel host,
        Func<string?>? nodeIdSource = null,
        Func<string?>? dynamicPortIdSource = null)
        : this(host, nodeIdSource, new GraphNodeAuthoringService(dynamicPortIdSource), initialize: true)
    {
    }

    /// <summary>
    /// Deterministic constructor seam for tests and hosts which own an
    /// authoring service. The service remains non-mutating until AddNodeAt
    /// explicitly routes its candidate through Host.
    /// </summary>
    public CanonicalGraphEditorView(GraphEditorHostViewModel host,
        GraphNodeAuthoringService authoringService,
        Func<string?>? nodeIdSource = null)
        : this(host, nodeIdSource, authoringService, initialize: true)
    {
    }

    private CanonicalGraphEditorView(GraphEditorHostViewModel? host,
        Func<string?>? nodeIdSource,
        GraphNodeAuthoringService? authoringService,
        bool initialize)
    {
        _nodeIdSource = nodeIdSource ?? NextNodeId;
        _authoringService = authoringService ?? new GraphNodeAuthoringService();
        InitializeComponent();
        GraphCanvas.RenderTransform = new TransformGroup { Children = [_scale, _translate] };
        DataContextChanged += OnDataContextChanged;
        Loaded += View_OnLoaded;
        Unloaded += View_OnUnloaded;
        if (host is not null) Host = host;
    }

    public GraphEditorHostViewModel? Host
    {
        get => (GraphEditorHostViewModel?)GetValue(HostProperty);
        set => SetValue(HostProperty, value);
    }

    public GraphEditorHostViewModel? ViewModel => Host;
    public GraphViewportController ViewportController => _viewportController;
    public GraphEditorNodeViewModel? SelectedNode => _selectedNode;
    public GraphEditorConnectionViewModel? SelectedConnection => _selectedConnection;
    public event EventHandler<GraphSelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<GraphSelectionChangedEventArgs>? GraphSelectionChanged;
    public UIElement KeyboardCommandTarget => GraphCanvas;
    public FrameworkElement ViewportElement => CanvasViewport;
    public IReadOnlyCollection<CanonicalGraphNodeControl> NodeVisuals => _nodeVisuals.Values;
    public IReadOnlyCollection<FlowPortControl> PortVisuals => _ports.Values;
    public IReadOnlyCollection<Path> ConnectionHitTargets => _connectionHits.Keys;
    public IReadOnlyCollection<Path> ConnectionVisuals => GraphCanvas.Children.OfType<Path>().Where(path => !path.IsHitTestVisible).ToArray();

    public Point ScreenToGraph(Point viewportPoint) => _viewportController.ScreenToGraph(viewportPoint);

    public bool ContainsViewportPoint(Point viewportPoint)
        => IsFinite(viewportPoint.X) && IsFinite(viewportPoint.Y)
            && viewportPoint.X >= 0 && viewportPoint.Y >= 0
            && viewportPoint.X <= CanvasViewport.ActualWidth
            && viewportPoint.Y <= CanvasViewport.ActualHeight;

    /// <summary>Canonical scoped definitions in registry order, excluding compatibility nodes.</summary>
    public IReadOnlyList<GraphNodeDefinition> AuthoringDefinitions
        => Host is { } host ? GraphNodeDefinitionRegistry.ForAuthoringScope(host.Scope) : [];

    /// <summary>
    /// Canonical authoring groups. Group order is the first category appearance
    /// in the scoped registry, and definition order is never re-sorted.
    /// </summary>
    public IReadOnlyList<GraphNodeAuthoringCategory> AuthoringCategories
        => AuthoringDefinitions
            .GroupBy(definition => definition.Category, StringComparer.Ordinal)
            .Select(group => new GraphNodeAuthoringCategory(group.Key, group.ToArray()))
            .ToArray();

    /// <summary>Last non-mutating authoring diagnostics; retained for the Problems surface.</summary>
    public IReadOnlyList<ValidationIssue> LastAuthoringIssues => _lastAuthoringIssues;

    public IReadOnlyList<ValidationIssue> AuthoringIssues => LastAuthoringIssues;

    /// <summary>Returns whether a type is currently safe to author in Host's scope.</summary>
    public bool CanAuthorNodeType(string? nodeType)
    {
        if (Host is not { } host || string.IsNullOrWhiteSpace(nodeType) ||
            !GraphNodeDefinitionRegistry.TryGet(host.Scope, nodeType, out var definition) ||
            definition.CompatibilityOnly ||
            (definition.Unique && (host.Graph.Nodes ?? []).Any(node => node is not null &&
                string.Equals(node.Type, definition.Type, StringComparison.Ordinal))))
            return false;

        // These roles need semantic resource mappings that this generic view
        // intentionally cannot invent.
        return (host.Scope, definition.Type) is not
            (GraphScope.StoryFlow, "start") and not
            (GraphScope.StoryFlow, "session") and not
            (GraphScope.StoryFlow, "task") and not
            (GraphScope.Session, "choice") and not
            (GraphScope.Task, "settle");
    }

    /// <summary>
    /// Builds one detached candidate at a finite graph position. Position is
    /// intentionally host layout metadata and is not written by this method.
    /// </summary>
    public GraphNodeAuthoringResult CreateNodeAt(string? nodeType, double x, double y)
    {
        if (Host is not { } host)
            return RecordAuthoringFailure(new ValidationIssue(
                "graph.node.create.host.required", "A graph host is required.", "host"));
        if (!IsFinite(x) || !IsFinite(y))
            return RecordAuthoringFailure(new ValidationIssue(
                "graph.node.create.position.invalid", "Graph node position must be finite.", "position"));
        if (!CanAuthorNodeType(nodeType))
            return RecordAuthoringFailure(AuthoringAvailabilityIssue(host, nodeType));

        string? id;
        try
        {
            // Exactly one opaque node-ID request is made. A blank or duplicate
            // result is a single failed attempt; there is no retry loop.
            id = _nodeIdSource();
        }
        catch (Exception exception) when (exception is not (OutOfMemoryException or StackOverflowException))
        {
            return RecordAuthoringFailure(new ValidationIssue(
                "graph.node.create.id.unavailable", exception.Message, "id"));
        }

        var result = _authoringService.Create(host.Graph, host.Scope, nodeType, id);
        _lastAuthoringIssues = result.Issues.ToArray();
        return result;
    }

    /// <summary>Creates and explicitly commits one safe candidate through Host.</summary>
    public bool AddNodeAt(string? nodeType, double x, double y)
    {
        var result = CreateNodeAt(nodeType, x, y);
        if (!result.IsSuccess || result.Candidate is not { } candidate || Host is not { } host)
            return false;

        if (!host.AddNode(candidate))
        {
            _lastAuthoringIssues = host.LastValidationIssues.ToArray();
            return false;
        }
        host.SetNodePosition(candidate.Id, x, y);
        _lastAuthoringIssues = [];
        ClearConnectionSelection();
        return SelectNode(candidate.Id);
    }

    /// <summary>Builds the blank-canvas authoring menu without opening it.</summary>
    public ContextMenu CreateCanvasContextMenu(Point graphPoint)
    {
        var menu = FluentContextMenuFactory.Create(GraphCanvas);
        var add = FluentContextMenuFactory.CreateSubmenu("添加节点");
        foreach (var category in AuthoringCategories)
        {
            var group = FluentContextMenuFactory.CreateSubmenu(category.Name);
            foreach (var definition in category.Definitions)
            {
                var type = definition.Type;
                group.Items.Add(FluentContextMenuFactory.CreateItem(
                    definition.DisplayName,
                    () => AddNodeAt(type, graphPoint.X, graphPoint.Y),
                    CanAuthorNodeType(type)));
            }
            add.Items.Add(group);
        }
        menu.Items.Add(add);
        return menu;
    }

    /// <summary>Builds the menu at the last blank-canvas pointer location.</summary>
    public ContextMenu CreateCanvasContextMenu() => CreateCanvasContextMenu(_contextGraphPoint);

    private static void OnHostChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var view = (CanonicalGraphEditorView)sender;
        view._lastAuthoringIssues = [];
        if (!view._settingHostFromDataContext) view._hostFromDataContext = false;
        view.DetachHost(args.OldValue as GraphEditorHostViewModel);
        view.AttachHost(args.NewValue as GraphEditorHostViewModel);
        view.RebuildGraph();
    }

    private void View_OnLoaded(object sender, RoutedEventArgs args)
    {
        AttachHost(_host);
        RebuildGraph();
    }

    private void View_OnUnloaded(object sender, RoutedEventArgs args)
    {
        CancelPointerGesture();
        DetachHost(_host);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs args)
    {
        if (args.NewValue is GraphEditorHostViewModel host)
        {
            // An explicit Host binding is authoritative over inherited shell
            // context. DataContext-as-host is retained only when no explicit
            // host has been assigned (or the prior host was also implicit).
            if (Host is null || _hostFromDataContext)
            {
                _hostFromDataContext = true;
                if (!ReferenceEquals(Host, host))
                {
                    _settingHostFromDataContext = true;
                    try { Host = host; }
                    finally { _settingHostFromDataContext = false; }
                }
            }
            return;
        }

        // A recycled view may receive null or an unrelated shell DataContext.
        // Do not leave an implicitly adopted graph visible in that case.
        if (_hostFromDataContext)
        {
            _hostFromDataContext = false;
            if (Host is not null)
            {
                _settingHostFromDataContext = true;
                try { Host = null; }
                finally { _settingHostFromDataContext = false; }
            }
        }
    }

    private void AttachHost(GraphEditorHostViewModel? host)
    {
        _host = host;
        if (host is null || _hostEventsAttached) return;
        _hostEventsAttached = true;
        host.Nodes.CollectionChanged += HostCollectionChanged;
        host.Connections.CollectionChanged += HostCollectionChanged;
        foreach (var node in host.Nodes) node.PropertyChanged += NodePropertyChanged;
    }

    private void DetachHost(GraphEditorHostViewModel? host)
    {
        if (host is null || !_hostEventsAttached) return;
        _hostEventsAttached = false;
        host.Nodes.CollectionChanged -= HostCollectionChanged;
        host.Connections.CollectionChanged -= HostCollectionChanged;
        foreach (var node in host.Nodes) node.PropertyChanged -= NodePropertyChanged;
    }

    private void HostCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args) => RebuildGraph();

    private void NodePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (sender is GraphEditorNodeViewModel node && _nodeVisuals.TryGetValue(node, out var visual) &&
            args.PropertyName is nameof(GraphEditorNodeViewModel.X) or nameof(GraphEditorNodeViewModel.Y))
        {
            Canvas.SetLeft(visual, Safe(node.X));
            Canvas.SetTop(visual, Safe(node.Y));
            RedrawConnections();
        }
    }

    private void RebuildGraph()
    {
        var oldNode = _selectedNode;
        var oldConnection = _selectedConnection;
        if (_host is null)
        {
            ClearVisualState();
            NotifySelectionChanged(oldNode, oldConnection);
            return;
        }
        var selectedNodeId = CurrentUniqueSelectedNodeId() ?? _pendingSelectedNodeId;
        var selectedNodeIsUniqueInDocument = selectedNodeId is not null &&
            (_host.Graph.Nodes ?? []).Where(node => node is not null)
                .Count(node => string.Equals(node.Id, selectedNodeId, StringComparison.Ordinal)) == 1;
        if (!selectedNodeIsUniqueInDocument) selectedNodeId = null;
        CancelPointerGesture(false);
        foreach (var node in _nodeVisuals.Keys) node.PropertyChanged -= NodePropertyChanged;
        GraphCanvas.Children.Clear();
        _nodeVisuals.Clear();
        _ports.Clear();
        _connectionHits.Clear();
        foreach (var node in _host.Nodes)
        {
            if (node is null) continue;
            node.PropertyChanged -= NodePropertyChanged;
            node.PropertyChanged += NodePropertyChanged;
            var visual = new CanonicalGraphNodeControl(node);
            _nodeVisuals[node] = visual;
            GraphCanvas.Children.Add(visual);
            Canvas.SetLeft(visual, Safe(node.X));
            Canvas.SetTop(visual, Safe(node.Y));
            AutomationProperties.SetAutomationId(visual, $"CanonicalGraphNode_{node.NodeId}");
        }
        GraphEditorNodeViewModel[] matchingSelectedNodes = selectedNodeId is null
            ? []
            : _nodeVisuals.Keys.Where(node => string.Equals(node.NodeId, selectedNodeId, StringComparison.Ordinal)).ToArray();
        _selectedNode = matchingSelectedNodes.Length == 1 ? matchingSelectedNodes[0] : null;
        _pendingSelectedNodeId = _selectedNode is null ? selectedNodeId : null;
        ApplyNodeSelectionVisuals();
        if (_selectedConnection is not null && !_host.Connections.Contains(_selectedConnection))
            _selectedConnection = null;
        GraphCanvas.UpdateLayout();
        IndexPorts();
        RedrawConnections();
        ApplyViewport();
        NotifySelectionChanged(oldNode, oldConnection);
    }

    private void IndexPorts()
    {
        _ports.Clear();
        var malformed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var pair in _nodeVisuals)
        {
            foreach (var port in pair.Value.PortControls)
            {
                if (string.IsNullOrWhiteSpace(port.NodeId) || string.IsNullOrWhiteSpace(port.EffectivePortId)) continue;
                SetPortAutomation(port);
                var key = EndpointKey(port.NodeId, port.EffectivePortId);
                if (malformed.Contains(key)) continue;
                if (_ports.ContainsKey(key))
                {
                    _ports.Remove(key);
                    malformed.Add(key); // duplicate identity is not resolvable
                }
                else _ports[key] = port;
            }
        }
    }

    private void RedrawConnections()
    {
        foreach (var child in GraphCanvas.Children.OfType<Path>().ToArray()) GraphCanvas.Children.Remove(child);
        _connectionHits.Clear();
        if (_host is null) return;
        GraphCanvas.UpdateLayout();
        foreach (var connection in _host.Connections)
        {
            if (!TryGetPort(connection.FromNodeId, connection.FromPortId, out var from) ||
                !TryGetPort(connection.ToNodeId, connection.ToPortId, out var to)) continue;
            var start = from.GetAnchorPoint(GraphCanvas);
            var end = to.GetAnchorPoint(GraphCanvas);
            if (!IsFinite(start) || !IsFinite(end)) continue;
            var selected = ReferenceEquals(connection, _selectedConnection);
            var style = GraphConnectionVisualStyle.For(connection.InterfaceKind, selected);
            var geometry = WireGeometry(start, end);
            var hit = new Path { Data = geometry, Stroke = Brushes.Transparent, StrokeThickness = 14, Fill = null, Tag = connection, IsHitTestVisible = true };
            AutomationProperties.SetName(hit, $"{style.AutomationLabel} {connection.FromNodeId}:{connection.FromPortId} 到 {connection.ToNodeId}:{connection.ToPortId}");
            AutomationProperties.SetAutomationId(hit, $"CanonicalGraphConnection_{connection.FromNodeId}_{connection.FromPortId}_{connection.ToNodeId}_{connection.ToPortId}");
            hit.PreviewMouseLeftButtonDown += ConnectionHit_OnPreviewMouseLeftButtonDown;
            var line = new Path { Data = geometry, Stroke = new SolidColorBrush(style.StrokeColor), StrokeThickness = style.StrokeThickness, Fill = null, IsHitTestVisible = false, Tag = connection };
            GraphCanvas.Children.Insert(0, line);
            GraphCanvas.Children.Insert(1, hit);
            _connectionHits[hit] = connection;
        }
    }

    private bool TryGetPort(string nodeId, string portId, out FlowPortControl port) => _ports.TryGetValue(EndpointKey(nodeId, portId), out port!);

    private static string EndpointKey(string nodeId, string portId) => nodeId + "\u001f" + portId;

    private void CanvasViewport_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        GraphCanvas.Focus();
        Keyboard.Focus(GraphCanvas);
        var source = e.OriginalSource as DependencyObject;
        if (e.ChangedButton == MouseButton.Middle)
        {
            _pointerState.PreemptForPan();
            _pointerStart = e.GetPosition(CanvasViewport);
            CanvasViewport.CaptureMouse();
            e.Handled = true;
            return;
        }
        if (e.ChangedButton != MouseButton.Left) return;
        if (FindAncestor<FlowPortControl>(source) is { } port)
        {
            BeginWire(port, e.GetPosition(GraphCanvas));
            e.Handled = true;
            return;
        }
        if (FindAncestor<CanonicalGraphNodeControl>(source) is { Node: { } node })
        {
            SelectNode(node);
            if (!_pointerState.Begin(GraphPointerMode.NodeDrag)) return;
            _dragNode = node;
            _pointerStart = e.GetPosition(GraphCanvas);
            _dragOrigin = new Point(node.X, node.Y);
            CanvasViewport.CaptureMouse();
            e.Handled = true;
            return;
        }
        if (FindAncestor<Path>(source) is not { Tag: GraphEditorConnectionViewModel }) ClearSelection();
    }

    private void CanvasViewport_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        // A blank-canvas menu must never be inherited by a node, port, or wire
        // hit target. Clear the previous transient menu before this boundary
        // check so a second right-click cannot reopen stale authoring actions.
        GraphCanvas.ClearValue(ContextMenuProperty);
        if (FindAncestor<FlowPortControl>(source) is not null ||
            FindAncestor<CanonicalGraphNodeControl>(source) is not null ||
            FindAncestor<Path>(source) is { Tag: GraphEditorConnectionViewModel })
        {
            e.Handled = true;
            return;
        }

        _contextGraphPoint = e.GetPosition(GraphCanvas);
        OpenContextMenu(CreateCanvasContextMenu(_contextGraphPoint));
        e.Handled = true;
    }

    private void OpenContextMenu(ContextMenu menu)
    {
        menu.Closed += (_, _) =>
        {
            if (ReferenceEquals(GraphCanvas.ContextMenu, menu)) GraphCanvas.ClearValue(ContextMenuProperty);
        };
        GraphCanvas.ContextMenu = menu;
        menu.PlacementTarget = GraphCanvas;
        menu.Placement = System.Windows.Controls.Primitives.PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    private bool BeginWire(FlowPortControl port, Point point, GraphConnection? original = null)
    {
        if (_host is null || !TryEndpoint(port, out var endpoint) || !_pointerState.Begin(GraphPointerMode.WireDrag)) return false;
        var oldNode = _selectedNode;
        var oldConnection = _selectedConnection;
        ClearNodeSelection();
        if (original is null) ClearConnectionSelection();
        NotifySelectionChanged(oldNode, oldConnection);
        _wireStart = endpoint;
        _wireOriginal = original;
        _pointerStart = point;
        _draftWire = new Path { Stroke = Brushes.White, StrokeThickness = 3, StrokeDashArray = new DoubleCollection { 3, 3 }, IsHitTestVisible = false };
        GraphCanvas.Children.Add(_draftWire);
        CanvasViewport.CaptureMouse();
        UpdateWire(point);
        return true;
    }

    private void CanvasViewport_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        var point = e.GetPosition(GraphCanvas);
        if (_pointerState.Is(GraphPointerMode.Pan))
        {
            var current = e.GetPosition(CanvasViewport);
            _viewportController.PanBy(current.X - _pointerStart.X, current.Y - _pointerStart.Y);
            _pointerStart = current;
            ApplyViewport();
        }
        else if (_pointerState.Is(GraphPointerMode.NodeDrag) && _dragNode is not null)
        {
            var delta = point - _pointerStart;
            _host?.SetNodePosition(_dragNode.NodeId, Safe(_dragOrigin.X + delta.X), Safe(_dragOrigin.Y + delta.Y));
        }
        else if (_pointerState.Is(GraphPointerMode.WireDrag)) UpdateWire(point);
    }

    private void CanvasViewport_OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && _pointerState.Is(GraphPointerMode.WireDrag))
        {
            var target = FindAncestor<FlowPortControl>(CanvasViewport.InputHitTest(e.GetPosition(CanvasViewport)) as DependencyObject);
            _ = CompleteWire(target);
            e.Handled = true;
        }
        else if (e.ChangedButton == MouseButton.Left && _pointerState.Is(GraphPointerMode.NodeDrag)) EndPointerGesture();
        else if (e.ChangedButton == MouseButton.Middle && _pointerState.Is(GraphPointerMode.Pan)) EndPointerGesture();
    }

    private void UpdateWire(Point point)
    {
        if (_draftWire is null || _wireStart is null) return;
        var viewportPoint = GraphCanvas.TransformToAncestor(CanvasViewport).Transform(point);
        var target = FindAncestor<FlowPortControl>(CanvasViewport.InputHitTest(viewportPoint) as DependencyObject);
        foreach (var port in _ports.Values) port.IsConnecting = ReferenceEquals(port, target) || (TryEndpoint(port, out var ep) && ep == _wireStart.Value);
        if (target is not null && TryEndpoint(target, out var targetEndpoint) && _host is not null)
        {
            var source = _ports.TryGetValue(EndpointKey(_wireStart.Value.NodeId, _wireStart.Value.PortId), out var wirePort) ? wirePort : null;
            var valid = target.IsCompatibleEndpoint(source) && (_wireOriginal is null
                ? _host.CanConnect(_wireStart.Value, targetEndpoint)
                : _host.CanReconnect(_wireOriginal, _wireStart.Value, targetEndpoint));
            target.IsValidTarget = valid;
        }
        var startPort = _ports.TryGetValue(EndpointKey(_wireStart.Value.NodeId, _wireStart.Value.PortId), out var startWirePort) ? startWirePort : null;
        var start = startPort?.GetAnchorPoint(GraphCanvas) ?? _pointerStart;
        _draftWire.Data = WireGeometry(start, point);
    }

    private bool CompleteWire(FlowPortControl? target)
    {
        var result = false;
        if (_host is not null && _wireStart is { } start)
        {
            if (target is not null)
            {
                var fixedPort = _ports.TryGetValue(EndpointKey(start.NodeId, start.PortId), out var port) ? port : null;
                if (TryEndpoint(target, out var endpoint) &&
                    (_wireOriginal is null || fixedPort is not null && fixedPort.IsCompatibleEndpoint(target)))
                    result = _host.CompleteWireDrag(start, endpoint, _wireOriginal);
            }
            else
            {
                result = _host.CompleteWireDrag(start, null, _wireOriginal);
            }
        }
        CancelPointerGesture();
        return result;
    }

    private void ConnectionHit_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Path { Tag: GraphEditorConnectionViewModel connection }) return;
        SetSelectedConnection(connection);
        var point = e.GetPosition(GraphCanvas);
        if (TryGetConnectionFixedPort(connection, point, out var fixedPort))
            BeginWire(fixedPort, point, connection.Connection);
        e.Handled = true;
    }

    private void CanvasViewport_OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        var cursor = e.GetPosition(CanvasViewport);
        _viewportController.ZoomAt(cursor, e.Delta > 0 ? GraphViewportController.DefaultZoomFactor : 1 / GraphViewportController.DefaultZoomFactor);
        ApplyViewport();
        e.Handled = true;
    }

    private void GraphCanvas_OnKeyDown(object sender, KeyEventArgs e)
    {
        e.Handled = HandleKeyboardCommand(e.Key);
    }

    /// <summary>Shared by the routed GraphCanvas handler and deterministic STA tests.</summary>
    public bool HandleKeyboardCommand(Key key)
    {
        if (key == Key.Escape)
        {
            CancelPointerGesture();
            return true;
        }
        if (key == Key.Delete && _selectedConnection is not null)
        {
            var selected = _selectedConnection;
            var result = _host is not null && _host.Disconnect(selected);
            if (result) _selectedConnection = null;
            RebuildGraph();
            return true;
        }
        if (key == Key.Delete && _selectedNode is not null)
        {
            _ = RemoveSelectedNode();
            return true;
        }
        return false;
    }

    private void CanvasViewport_OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (!_canceling && !_pointerState.Is(GraphPointerMode.Idle)) CancelPointerGesture(false);
    }

    public bool DisconnectSelectedConnection()
    {
        if (_host is null || _selectedConnection is null) return false;
        var result = _host.Disconnect(_selectedConnection);
        if (result) _selectedConnection = null;
        RebuildGraph();
        return result;
    }

    /// <summary>Routes node deletion through the host, failing closed on references or protected nodes.</summary>
    public bool RemoveSelectedNode(bool confirmReferencedRemoval = false)
    {
        if (_host is null || _selectedNode is null) return false;
        var selected = _selectedNode;
        var result = _host.RemoveNode(selected.NodeId, confirmReferencedRemoval);
        if (result)
        {
            ClearNodeSelection();
            RebuildGraph();
        }
        else
        {
            // Host.LastValidationIssues is intentionally retained for the Problems surface.
            ApplyNodeSelectionVisuals();
        }
        return result;
    }

    /// <summary>Deterministically selects a materialized node for STA tests and hosts.</summary>
    public bool SelectNode(GraphEditorNodeViewModel? node)
    {
        var oldNode = _selectedNode;
        var oldConnection = _selectedConnection;
        if (node is null || !_nodeVisuals.ContainsKey(node))
        {
            ClearSelection();
            return false;
        }
        ClearConnectionSelection();
        _selectedNode = node;
        _pendingSelectedNodeId = null;
        ApplyNodeSelectionVisuals();
        NotifySelectionChanged(oldNode, oldConnection);
        return true;
    }

    /// <summary>Deterministically selects the sole materialized node with this stable ID.</summary>
    public bool SelectNode(string? nodeId)
    {
        GraphEditorNodeViewModel[] matches = string.IsNullOrWhiteSpace(nodeId)
            ? []
            : _nodeVisuals.Keys.Where(node => string.Equals(node.NodeId, nodeId, StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1)
        {
            ClearSelection();
            return false;
        }
        return SelectNode(matches[0]);
    }

    public void ClearSelection()
    {
        var oldNode = _selectedNode;
        var oldConnection = _selectedConnection;
        ClearNodeSelection();
        ClearConnectionSelection();
        NotifySelectionChanged(oldNode, oldConnection);
    }

    private void SetSelectedConnection(GraphEditorConnectionViewModel connection)
    {
        var oldNode = _selectedNode;
        var oldConnection = _selectedConnection;
        ClearNodeSelection();
        _selectedConnection = connection;
        RedrawConnections();
        NotifySelectionChanged(oldNode, oldConnection);
    }

    private void ClearNodeSelection()
    {
        _selectedNode = null;
        _pendingSelectedNodeId = null;
        ApplyNodeSelectionVisuals();
    }

    private void ClearConnectionSelection()
    {
        if (_selectedConnection is null) return;
        _selectedConnection = null;
        RedrawConnections();
    }

    private void ApplyNodeSelectionVisuals()
    {
        foreach (var pair in _nodeVisuals)
            pair.Value.IsSelected = ReferenceEquals(pair.Key, _selectedNode);
    }

    private static string? UniqueNodeId(string? nodeId) => string.IsNullOrWhiteSpace(nodeId) ? null : nodeId;

    private string? CurrentUniqueSelectedNodeId()
    {
        var nodeId = _selectedNode?.NodeId;
        if (UniqueNodeId(nodeId) is not { } id) return null;
        return _nodeVisuals.Keys.Count(node => string.Equals(node.NodeId, id, StringComparison.Ordinal)) == 1 ? id : null;
    }

    /// <summary>Small deterministic seam for tests and keyboard-accessible hosts.</summary>
    public bool BeginNewConnectionDrag(FlowPortControl port) => BeginWire(port, new Point(0, 0));

    /// <summary>Models an explicit reconnect that replaces the original input.</summary>
    public bool BeginExistingConnectionDrag(GraphEditorConnectionViewModel connection)
    {
        if (connection is null || !TryGetConnectionPorts(connection, out var output, out _)) return false;
        SetSelectedConnection(connection);
        return BeginWire(output, new Point(0, 0), connection.Connection);
    }

    /// <summary>
    /// Models an explicit connection-hit reconnect. The graph-space click
    /// chooses the nearer real endpoint; the opposite endpoint remains fixed
    /// when the host completes the reconnect transaction.
    /// </summary>
    public bool BeginExistingConnectionDrag(GraphEditorConnectionViewModel connection, Point graphPoint)
    {
        if (connection is null || !TryGetConnectionFixedPort(connection, graphPoint, out var fixedPort)) return false;
        SetSelectedConnection(connection);
        return BeginWire(fixedPort, graphPoint, connection.Connection);
    }

    /// <summary>Completes a started new-wire gesture using a canonical port target.</summary>
    public bool CompleteConnectionDrag(FlowPortControl? target)
    {
        if (!_pointerState.Is(GraphPointerMode.WireDrag)) return false;
        return CompleteWire(target);
    }

    private bool TryEndpoint(FlowPortControl port, out GraphEditorEndpoint endpoint)
    {
        endpoint = default;
        if (string.IsNullOrWhiteSpace(port.NodeId) || string.IsNullOrWhiteSpace(port.EffectivePortId)) return false;
        endpoint = new GraphEditorEndpoint(port.NodeId, port.EffectivePortId,
            port.IsInput ? GraphPortDirection.Input : GraphPortDirection.Output, port.InterfaceKind);
        return true;
    }

    private bool TryGetConnectionPorts(GraphEditorConnectionViewModel connection,
        out FlowPortControl output, out FlowPortControl input)
    {
        output = null!;
        input = null!;
        if (!TryGetPort(connection.FromNodeId, connection.FromPortId, out output) ||
            !TryGetPort(connection.ToNodeId, connection.ToPortId, out input) ||
            output.IsInput || !input.IsInput ||
            output.InterfaceKind != connection.InterfaceKind || input.InterfaceKind != connection.InterfaceKind)
            return false;
        return true;
    }

    private bool TryGetConnectionFixedPort(GraphEditorConnectionViewModel connection, Point graphPoint,
        out FlowPortControl fixedPort)
    {
        fixedPort = null!;
        if (!IsFinite(graphPoint) || !TryGetConnectionPorts(connection, out var output, out var input))
            return false;

        var outputAnchor = output.GetAnchorPoint(GraphCanvas);
        var inputAnchor = input.GetAnchorPoint(GraphCanvas);
        if (!IsFinite(outputAnchor) || !IsFinite(inputAnchor)) return false;

        // The nearer endpoint is the one being moved; the wire starts at the
        // opposite original endpoint and therefore remains fixed on-screen.
        fixedPort = DistanceSquared(graphPoint, outputAnchor) <= DistanceSquared(graphPoint, inputAnchor)
            ? input
            : output;
        return true;
    }

    private void CancelPointerGesture(bool releaseCapture = true)
    {
        _canceling = true;
        try
        {
            _pointerState.Cancel();
            _dragNode = null;
            _wireStart = null;
            _wireOriginal = null;
            if (_draftWire is not null) GraphCanvas.Children.Remove(_draftWire);
            _draftWire = null;
            foreach (var port in _ports.Values) { port.IsConnecting = false; port.IsValidTarget = false; }
            if (releaseCapture && Mouse.Captured == CanvasViewport) Mouse.Capture(null);
        }
        finally { _canceling = false; }
    }

    private void EndPointerGesture() => CancelPointerGesture();

    private void ClearVisualState()
    {
        CancelPointerGesture(false);
        foreach (var node in _nodeVisuals.Keys) node.PropertyChanged -= NodePropertyChanged;
        GraphCanvas.Children.Clear();
        _nodeVisuals.Clear();
        _ports.Clear();
        _connectionHits.Clear();
        _selectedConnection = null;
        _selectedNode = null;
        _pendingSelectedNodeId = null;
    }

    private static void SetPortAutomation(FlowPortControl port)
    {
        var id = port.EffectivePortId;
        var direction = port.IsInput ? "输入" : "输出";
        AutomationProperties.SetAutomationId(port, $"CanonicalGraphPort_{port.NodeId}_{id}");
        AutomationProperties.SetName(port, $"{port.NodeId} {direction}端口 {id} ({port.InterfaceKind})");
    }

    private void ApplyViewport()
    {
        _scale.ScaleX = _scale.ScaleY = Safe(_viewportController.Zoom);
        _translate.X = Safe(_viewportController.PanX);
        _translate.Y = Safe(_viewportController.PanY);
    }

    private void Zoom100_OnClick(object sender, RoutedEventArgs e) { _viewportController.SetZoomAt(1, ViewportCenter()); ApplyViewport(); }
    private void FitAll_OnClick(object sender, RoutedEventArgs e)
    {
        var points = _host?.Nodes.SelectMany(node => new[] { new Point(node.X, node.Y), new Point(node.X + NodeWidth, node.Y + NodeHeight) });
        _viewportController.FitToBounds(points, new Size(CanvasViewport.ActualWidth, CanvasViewport.ActualHeight));
        ApplyViewport();
    }
    private void ResetView_OnClick(object sender, RoutedEventArgs e) { _viewportController.ResetView(); ApplyViewport(); }
    private Point ViewportCenter() => new(Math.Max(1, CanvasViewport.ActualWidth) / 2, Math.Max(1, CanvasViewport.ActualHeight) / 2);

    private static PathGeometry WireGeometry(Point start, Point end)
    {
        var dx = Math.Max(35, Math.Abs(end.X - start.X) * .45);
        return new PathGeometry(new[] { new PathFigure(start, new[] { new BezierSegment(new Point(start.X + dx, start.Y), new Point(end.X - dx, end.Y), end, true) }, false) });
    }
    private static double Safe(double value) => double.IsFinite(value) ? value : 0;
    private static bool IsFinite(double value) => double.IsFinite(value);
    private static bool IsFinite(Point point) => double.IsFinite(point.X) && double.IsFinite(point.Y);
    private static string NextNodeId() => $"node_{Guid.NewGuid():N}";

    private GraphNodeAuthoringResult RecordAuthoringFailure(ValidationIssue issue)
    {
        _lastAuthoringIssues = [issue];
        return GraphNodeAuthoringResult.FromIssues(_lastAuthoringIssues);
    }

    private static ValidationIssue AuthoringAvailabilityIssue(GraphEditorHostViewModel host, string? nodeType)
    {
        if (string.IsNullOrWhiteSpace(nodeType))
            return new("graph.node.create.type.required", "Graph node type is required.", "type");
        if (!GraphNodeDefinitionRegistry.TryGet(host.Scope, nodeType, out var definition))
        {
            if (GraphNodeDefinitionRegistry.TryGet(nodeType, out var known))
                return new("graph.node.create.type.wrong_scope",
                    $"Node type '{nodeType}' belongs to scope '{known.Scope}', not '{host.Scope}'.", "type");
            return new("graph.node.create.type.unknown",
                $"Node type '{nodeType}' is not registered for scope '{host.Scope}'.", "type");
        }
        if (definition.CompatibilityOnly)
            return new("graph.node.create.type.compatibility_only",
                $"Node type '{nodeType}' is compatibility-only and cannot be authored normally.", "type");
        if (definition.Unique && (host.Graph.Nodes ?? []).Any(node => node is not null &&
            string.Equals(node.Type, definition.Type, StringComparison.Ordinal)))
            return new("graph.node.create.type.unique",
                $"Scope '{host.Scope}' allows only one '{nodeType}' node.", "type");
        return new("graph.node.create.semantic_initializer.required",
            $"Node type '{nodeType}' in scope '{host.Scope}' requires a semantic initializer before authoring.",
            "semantic_initializer");
    }
    private static double DistanceSquared(Point first, Point second)
    {
        var dx = first.X - second.X;
        var dy = first.Y - second.Y;
        return dx * dx + dy * dy;
    }

    private void NotifySelectionChanged(GraphEditorNodeViewModel? oldNode,
        GraphEditorConnectionViewModel? oldConnection)
    {
        if (ReferenceEquals(oldNode, _selectedNode) && ReferenceEquals(oldConnection, _selectedConnection)) return;
        var args = _selectedNode is { } node
            ? new GraphSelectionChangedEventArgs(GraphSelectionKind.Node, node: node)
            : _selectedConnection is { } connection
                ? new GraphSelectionChangedEventArgs(GraphSelectionKind.Connection, connection: connection)
                : new GraphSelectionChangedEventArgs(GraphSelectionKind.Clear);
        SelectionChanged?.Invoke(this, args);
        GraphSelectionChanged?.Invoke(this, args);
    }
    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    { while (source is not null) { if (source is T match) return match; source = VisualTreeHelper.GetParent(source); } return null; }
    private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
    { for (var i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++) { var child = VisualTreeHelper.GetChild(parent, i); if (child is T item) yield return item; foreach (var nested in FindVisualChildren<T>(child)) yield return nested; } }
}
