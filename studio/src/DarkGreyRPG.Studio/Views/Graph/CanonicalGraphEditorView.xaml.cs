using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
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
    MultipleNodes,
    Connection,
}

public enum GraphWireGestureKind
{
    None,
    NewConnection,
    ReconnectSingleEndpoint,
    AddOnMultiPort,
    ReconnectMultiBundle,
}

public sealed class GraphSelectionChangedEventArgs : EventArgs
{
    public GraphSelectionChangedEventArgs(GraphSelectionKind kind,
        GraphEditorNodeViewModel? node = null,
        GraphEditorConnectionViewModel? connection = null,
        IReadOnlyList<GraphEditorNodeViewModel>? nodes = null)
    {
        Kind = kind;
        Node = node;
        Connection = connection;
        Nodes = nodes ?? (node is null ? [] : [node]);
    }

    public GraphSelectionKind Kind { get; }
    public GraphSelectionKind SelectionKind => Kind;
    public GraphEditorNodeViewModel? Node { get; }
    public GraphEditorNodeViewModel? SelectedNode => Node;
    public GraphEditorConnectionViewModel? Connection { get; }
    public GraphEditorConnectionViewModel? SelectedConnection => Connection;
    public IReadOnlyList<GraphEditorNodeViewModel> Nodes { get; }
    public IReadOnlyList<GraphEditorNodeViewModel> SelectedNodes => Nodes;
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
    private readonly Dictionary<GraphEditorConnectionViewModel, (Path Line, Path Hit)> _connectionVisuals = [];
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly TranslateTransform _translate = new();
    private readonly HashSet<GraphEditorNodeViewModel> _selectedNodes = [];
    private readonly HashSet<string> _pendingSelectedNodeIds = new(StringComparer.Ordinal);
    private GraphEditorHostViewModel? _host;
    private GraphEditorNodeViewModel? _dragNode;
    private Point _pointerStart;
    private Point _lastGraphPointer;
    private Dictionary<GraphEditorNodeViewModel, Point> _dragOrigins = [];
    private bool _nodeDragThresholdPassed;
    private bool _collapseSelectionOnNodeClick;
    private Rectangle? _selectionBox;
    private Rect _selectionBoxBounds = Rect.Empty;
    private HashSet<GraphEditorNodeViewModel> _selectionBeforeMarquee = [];
    private bool _additiveMarquee;
    private bool _marqueeThresholdPassed;
    private GraphEditorConnectionViewModel? _spliceCandidate;
    private GraphConnectionSplicePlan? _splicePlan;
    private readonly List<Path> _spliceGhostWires = [];
    private GraphEditorEndpoint? _wireStart;
    private GraphEditorEndpoint? _wireFixedEndpoint;
    private GraphConnection? _wireOriginal;
    private Path? _draftWire;
    private readonly List<Path> _draftWires = [];
    private readonly HashSet<Path> _transientWireVisuals = [];
    private IReadOnlyList<GraphConnection> _wireOriginals = [];
    private bool _incidentWireReconnect;
    private GraphWireGestureKind _wireGestureKind;
    private FlowPortControl? _pendingWirePort;
    private bool _pendingWireBundle;
    private bool _scissorsMode;
    private bool _altScissorsMode;
    private bool _scissorsModeBeforeAlt;
    private GraphEditorConnectionViewModel? _selectedConnection;
    private GraphEditorNodeViewModel? _selectedNode;
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

    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(
        nameof(IsReadOnly), typeof(bool), typeof(CanonicalGraphEditorView), new PropertyMetadata(false, (d, _) =>
        {
            var view = (CanonicalGraphEditorView)d;
            view.CancelPointerGesture();
            view.SetScissorsMode(false);
            foreach (var visual in view._nodeVisuals.Values) visual.IsEnabled = !view.IsReadOnly;
        }));
    public bool IsReadOnly { get => (bool)GetValue(IsReadOnlyProperty); set => SetValue(IsReadOnlyProperty, value); }

    public static readonly DependencyProperty ViewportStateProperty = DependencyProperty.Register(
        nameof(ViewportState), typeof(GraphViewportState), typeof(CanonicalGraphEditorView),
        new PropertyMetadata(null, OnViewportStateChanged));

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
    public GraphViewportState? ViewportState
    {
        get => (GraphViewportState?)GetValue(ViewportStateProperty);
        set => SetValue(ViewportStateProperty, value);
    }
    public GraphEditorNodeViewModel? SelectedNode => _selectedNode;
    public IReadOnlyCollection<GraphEditorNodeViewModel> SelectedNodes => _selectedNodes.ToArray();
    public GraphEditorConnectionViewModel? SelectedConnection => _selectedConnection;
    public event EventHandler<GraphSelectionChangedEventArgs>? SelectionChanged;
    public event EventHandler<GraphSelectionChangedEventArgs>? GraphSelectionChanged;
    public UIElement KeyboardCommandTarget => GraphCanvas;
    public FrameworkElement ViewportElement => CanvasViewport;
    public IReadOnlyCollection<CanonicalGraphNodeControl> NodeVisuals => _nodeVisuals.Values;
    public IReadOnlyCollection<FlowPortControl> PortVisuals => _ports.Values;
    public IReadOnlyCollection<Path> ConnectionHitTargets => _connectionHits.Keys;
    public IReadOnlyCollection<Path> ConnectionVisuals => GraphCanvas.Children.OfType<Path>().Where(path => !path.IsHitTestVisible).ToArray();
    public IReadOnlyCollection<Path> ActiveWireVisuals => _draftWires;
    public GraphEditorEndpoint? ActiveWireMovingEndpoint => _wireStart;
    public GraphEditorEndpoint? ActiveWireFixedEndpoint => _wireFixedEndpoint ?? _wireStart;
    public GraphWireGestureKind ActiveWireGestureKind => _wireGestureKind;
    public bool IsWirePressPending => _pointerState.Is(GraphPointerMode.PortPressed);
    public bool IsIncidentWireReconnect => _incidentWireReconnect;
    public bool IsScissorsMode => _scissorsMode;
    public bool IsTemporaryScissorsMode => _altScissorsMode;
    public bool IsBoxSelecting => _pointerState.Is(GraphPointerMode.BoxSelect);
    public Rect SelectionBoxBounds => _selectionBoxBounds;
    public GraphEditorConnectionViewModel? ActiveSpliceCandidate => _spliceCandidate;
    public GraphConnectionSplicePlan? ActiveSplicePlan => _splicePlan;
    public IReadOnlyCollection<Path> SpliceGhostVisuals => _spliceGhostWires;
    public Cursor ScissorsCursor => ScissorsCursorFactory.Cursor;

    public void SetScissorsMode(bool enabled)
    {
        enabled &= !IsReadOnly;
        _scissorsMode = enabled;
        CanvasViewport.Cursor = enabled ? ScissorsCursorFactory.Cursor : Cursors.Arrow;
    }
    public event Action<GraphEditorNodeViewModel>? NodeEditRequested;
    public Func<GraphEditorNodeViewModel, CanonicalNodeInspectorViewModel?>? InlineEditorFactory { get; set; }

    /// <summary>
    /// Recreates only the transient inline editors while preserving graph nodes,
    /// selection, layout, connections, and viewport state. Resource option lists
    /// are supplied by the host workspace and may change without a graph edit.
    /// </summary>
    public void RefreshInlineEditors()
    {
        foreach (var (node, visual) in _nodeVisuals)
        {
            visual.DisposeInlineEditor();
            visual.InlineEditor = InlineEditorFactory?.Invoke(node)
                ?? (_host is null ? null : new CanonicalNodeInspectorViewModel(
                    _host, node, subscribeToHostChanges: false));
        }
    }

    public Point ScreenToGraph(Point viewportPoint) => _viewportController.ScreenToGraph(viewportPoint);

    public bool ContainsViewportPoint(Point viewportPoint)
        => IsFinite(viewportPoint.X) && IsFinite(viewportPoint.Y)
            && viewportPoint.X >= 0 && viewportPoint.Y >= 0
            && viewportPoint.X <= CanvasViewport.ActualWidth
            && viewportPoint.Y <= CanvasViewport.ActualHeight;

    /// <summary>Returns the node visual under a viewport point without changing selection.</summary>
    public GraphEditorNodeViewModel? NodeAtViewportPoint(Point viewportPoint)
        => ContainsViewportPoint(viewportPoint)
            ? FindAncestor<CanonicalGraphNodeControl>(CanvasViewport.InputHitTest(viewportPoint) as DependencyObject)?.Node
            : null;

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
            .Select(group => new GraphNodeAuthoringCategory(
                group.Key,
                group.ToArray()))
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
            (GraphScope.Session, "start") and not
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
        if (IsReadOnly) return false;
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

    private static void OnViewportStateChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
    {
        var view = (CanonicalGraphEditorView)sender;
        if (args.OldValue is GraphViewportState oldState) view.CaptureViewport(oldState);
        view.RestoreViewport(args.NewValue as GraphViewportState);
    }

    private void View_OnLoaded(object sender, RoutedEventArgs args)
    {
        AttachHost(_host);
        RebuildGraph();
        RestoreViewport(ViewportState);
    }

    private void View_OnUnloaded(object sender, RoutedEventArgs args)
    {
        if (ViewportState is { } state) CaptureViewport(state);
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
        host.Nodes.CollectionChanged += HostNodesCollectionChanged;
        host.Connections.CollectionChanged += HostConnectionsCollectionChanged;
        host.GraphChanged += HostGraphChanged;
        host.PortsChanged += HostPortsChanged;
        host.NodesChanged += HostNodesChanged;
        foreach (var node in host.Nodes) node.PropertyChanged += NodePropertyChanged;
    }

    private void DetachHost(GraphEditorHostViewModel? host)
    {
        if (host is null || !_hostEventsAttached) return;
        _hostEventsAttached = false;
        host.Nodes.CollectionChanged -= HostNodesCollectionChanged;
        host.Connections.CollectionChanged -= HostConnectionsCollectionChanged;
        host.GraphChanged -= HostGraphChanged;
        host.PortsChanged -= HostPortsChanged;
        host.NodesChanged -= HostNodesChanged;
        foreach (var node in host.Nodes) node.PropertyChanged -= NodePropertyChanged;
    }

    private void HostNodesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (args.Action == NotifyCollectionChangedAction.Reset)
        {
            RebuildGraph();
            return;
        }

        var oldNode = _selectedNode;
        var oldConnection = _selectedConnection;
        if (args.OldItems is not null)
        {
            foreach (var item in args.OldItems.OfType<GraphEditorNodeViewModel>()) RemoveNodeVisual(item);
        }
        if (args.NewItems is not null)
        {
            foreach (var item in args.NewItems.OfType<GraphEditorNodeViewModel>()) AddNodeVisual(item);
        }

        GraphCanvas.UpdateLayout();
        IndexPorts();
        RedrawConnections();
        ApplyNodeSelectionVisuals();
        NotifySelectionChanged(oldNode, oldConnection);
    }

    private void HostConnectionsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs args)
    {
        if (args.Action == NotifyCollectionChangedAction.Reset)
        {
            RedrawConnections();
            return;
        }

        var oldNode = _selectedNode;
        var oldConnection = _selectedConnection;
        if (args.OldItems is not null)
        {
            foreach (var item in args.OldItems.OfType<GraphEditorConnectionViewModel>()) RemoveConnectionVisual(item);
        }
        if (args.NewItems is not null)
        {
            GraphCanvas.UpdateLayout();
            IndexPorts();
            foreach (var item in args.NewItems.OfType<GraphEditorConnectionViewModel>()) AddConnectionVisual(item);
        }
        if (_selectedConnection is not null && _host is not null && !_host.Connections.Contains(_selectedConnection))
            _selectedConnection = null;
        NotifySelectionChanged(oldNode, oldConnection);
    }

    private void HostGraphChanged(object? sender, EventArgs args)
    {
        // Port changes are handled by HostPortsChanged after the affected node
        // projection has been identified. Keep this notification lightweight.
        GraphCanvas.UpdateLayout();
        IndexPorts();
    }

    private void HostPortsChanged(object? sender, GraphPortsChangedEventArgs args)
    {
        if (_host is null) return;
        foreach (var nodeId in args.NodeIds)
        {
            var node = _host.Nodes.FirstOrDefault(candidate =>
                string.Equals(candidate.NodeId, nodeId, StringComparison.Ordinal));
            if (node is not null && _nodeVisuals.TryGetValue(node, out var visual))
                visual.RefreshPorts();
        }

        GraphCanvas.UpdateLayout();
        IndexPorts();
        foreach (var nodeId in args.NodeIds) RedrawIncidentConnections(nodeId);
    }

    private void HostNodesChanged(object? sender, GraphNodesChangedEventArgs args)
    {
        if (_host is null) return;
        foreach (var nodeId in args.NodeIds)
        {
            var node = _host.Nodes.FirstOrDefault(candidate =>
                string.Equals(candidate.NodeId, nodeId, StringComparison.Ordinal));
            if (node is not null && _nodeVisuals.TryGetValue(node, out var visual))
                visual.InlineEditor?.RefreshCanonicalProjection();
        }
    }

    private void NodePropertyChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (sender is GraphEditorNodeViewModel node && _nodeVisuals.TryGetValue(node, out var visual) &&
            args.PropertyName is nameof(GraphEditorNodeViewModel.X) or nameof(GraphEditorNodeViewModel.Y))
        {
            Canvas.SetLeft(visual, Safe(node.X));
            Canvas.SetTop(visual, Safe(node.Y));
            RedrawIncidentConnections(node.NodeId);
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
        var selectedNodeIds = _selectedNodes.Select(node => node.NodeId)
            .Concat(_pendingSelectedNodeIds)
            .Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal)
            .Where(id => (_host.Graph.Nodes ?? []).Where(node => node is not null)
                .Count(node => string.Equals(node.Id, id, StringComparison.Ordinal)) == 1)
            .ToHashSet(StringComparer.Ordinal);
        CancelPointerGesture(false);
        foreach (var node in _nodeVisuals.Keys) node.PropertyChanged -= NodePropertyChanged;
        foreach (var visual in _nodeVisuals.Values) visual.DisposeInlineEditor();
        GraphCanvas.Children.Clear();
        _nodeVisuals.Clear();
        _ports.Clear();
        _connectionHits.Clear();
        foreach (var node in _host.Nodes)
        {
            if (node is null) continue;
            node.PropertyChanged -= NodePropertyChanged;
            node.PropertyChanged += NodePropertyChanged;
            AddNodeVisual(node);
        }
        _selectedNodes.Clear();
        foreach (var node in _nodeVisuals.Keys.Where(node => selectedNodeIds.Contains(node.NodeId)))
            _selectedNodes.Add(node);
        _pendingSelectedNodeIds.Clear();
        foreach (var id in selectedNodeIds.Where(id => !_selectedNodes.Any(node =>
                     string.Equals(node.NodeId, id, StringComparison.Ordinal))))
            _pendingSelectedNodeIds.Add(id);
        SynchronizeUniqueSelectedNode();
        ApplyNodeSelectionVisuals();
        if (_selectedConnection is not null && !_host.Connections.Contains(_selectedConnection))
            _selectedConnection = null;
        GraphCanvas.UpdateLayout();
        IndexPorts();
        RedrawConnections();
        ApplyViewport();
        NotifySelectionChanged(oldNode, oldConnection);
    }

    private void AddNodeVisual(GraphEditorNodeViewModel node)
    {
        if (_nodeVisuals.ContainsKey(node)) return;
        node.PropertyChanged -= NodePropertyChanged;
        node.PropertyChanged += NodePropertyChanged;
        var inlineEditor = InlineEditorFactory?.Invoke(node)
            ?? (_host is null ? null : new CanonicalNodeInspectorViewModel(
                _host, node, subscribeToHostChanges: false));
        var visual = new CanonicalGraphNodeControl(node, inlineEditor) { IsEnabled = !IsReadOnly };
        _nodeVisuals[node] = visual;
        GraphCanvas.Children.Add(visual);
        Canvas.SetLeft(visual, Safe(node.X));
        Canvas.SetTop(visual, Safe(node.Y));
        AutomationProperties.SetAutomationId(visual, $"CanonicalGraphNode_{node.NodeId}");
    }

    private void RemoveNodeVisual(GraphEditorNodeViewModel node)
    {
        node.PropertyChanged -= NodePropertyChanged;
        if (_nodeVisuals.Remove(node, out var visual))
        {
            visual.DisposeInlineEditor();
            GraphCanvas.Children.Remove(visual);
        }
        _selectedNodes.Remove(node);
        SynchronizeUniqueSelectedNode();
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
        foreach (var connection in _connectionVisuals.Keys.ToArray()) RemoveConnectionVisual(connection);
        _connectionHits.Clear();
        if (_host is null) return;
        GraphCanvas.UpdateLayout();
        foreach (var connection in _host.Connections) AddConnectionVisual(connection);
    }

    private void AddConnectionVisual(GraphEditorConnectionViewModel connection)
    {
        if (_connectionVisuals.ContainsKey(connection)
            || !TryGetPort(connection.FromNodeId, connection.FromPortId, out var from)
            || !TryGetPort(connection.ToNodeId, connection.ToPortId, out var to)) return;
        var start = from.GetAnchorPoint(GraphCanvas);
        var end = to.GetAnchorPoint(GraphCanvas);
        if (!IsFinite(start) || !IsFinite(end)) return;
        var selected = ReferenceEquals(connection, _selectedConnection);
        var style = GraphConnectionVisualStyle.For(connection.InterfaceKind, selected);
        var geometry = WireGeometry(start, end);
        var hit = new Path { Data = geometry, Stroke = Brushes.Transparent, StrokeThickness = 14, Fill = null, Tag = connection, IsHitTestVisible = true };
        AutomationProperties.SetName(hit, $"{style.AutomationLabel} {connection.FromNodeId}:{connection.FromPortId} 到 {connection.ToNodeId}:{connection.ToPortId}");
        AutomationProperties.SetAutomationId(hit, $"CanonicalGraphConnection_{connection.FromNodeId}_{connection.FromPortId}_{connection.ToNodeId}_{connection.ToPortId}");
        hit.PreviewMouseLeftButtonDown += ConnectionHit_OnPreviewMouseLeftButtonDown;
        var line = new Path { Data = geometry, StrokeThickness = style.StrokeThickness, Fill = null, IsHitTestVisible = false, Tag = connection };
        ApplyWireBrush(line, connection.InterfaceKind, selected);
        GraphCanvas.Children.Insert(0, line);
        GraphCanvas.Children.Insert(1, hit);
        _connectionHits[hit] = connection;
        _connectionVisuals[connection] = (line, hit);
    }

    private void RemoveConnectionVisual(GraphEditorConnectionViewModel connection)
    {
        if (!_connectionVisuals.Remove(connection, out var visual)) return;
        visual.Hit.PreviewMouseLeftButtonDown -= ConnectionHit_OnPreviewMouseLeftButtonDown;
        _connectionHits.Remove(visual.Hit);
        GraphCanvas.Children.Remove(visual.Hit);
        GraphCanvas.Children.Remove(visual.Line);
    }

    private void RedrawIncidentConnections(string nodeId)
    {
        foreach (var pair in _connectionVisuals.Where(pair =>
                     string.Equals(pair.Key.FromNodeId, nodeId, StringComparison.Ordinal)
                     || string.Equals(pair.Key.ToNodeId, nodeId, StringComparison.Ordinal)).ToArray())
        {
            if (!TryGetPort(pair.Key.FromNodeId, pair.Key.FromPortId, out var from)
                || !TryGetPort(pair.Key.ToNodeId, pair.Key.ToPortId, out var to)) continue;
            var geometry = WireGeometry(from.GetAnchorPoint(GraphCanvas), to.GetAnchorPoint(GraphCanvas));
            pair.Value.Line.Data = geometry;
            pair.Value.Hit.Data = geometry;
        }
    }

    private bool TryGetPort(string nodeId, string portId, out FlowPortControl port) => _ports.TryGetValue(EndpointKey(nodeId, portId), out port!);

    private static string EndpointKey(string nodeId, string portId) => nodeId + "\u001f" + portId;

    private static bool IsMultiIncidentPort(GraphEditorEndpoint endpoint)
        => endpoint.Direction == GraphPortDirection.Input && endpoint.InterfaceKind == GraphInterfaceKind.Flow
            || endpoint.Direction == GraphPortDirection.Output && endpoint.InterfaceKind == GraphInterfaceKind.Logic;

    private static bool IsIncident(GraphConnection connection, GraphEditorEndpoint endpoint)
        => endpoint.IsInput
            ? string.Equals(connection.ToNodeId, endpoint.NodeId, StringComparison.Ordinal)
                && string.Equals(connection.ToPortId, endpoint.PortId, StringComparison.Ordinal)
            : string.Equals(connection.FromNodeId, endpoint.NodeId, StringComparison.Ordinal)
                && string.Equals(connection.FromPortId, endpoint.PortId, StringComparison.Ordinal);

    private void CanvasViewport_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        var source = e.OriginalSource as DependencyObject;
        // ComboBox popup items live in a separate visual tree, even when the
        // ComboBox itself is inside a node.  Their routed mouse event can still
        // reach this viewport through the logical placement target.  Never
        // reinterpret that click as blank-canvas selection/focus handling.
        if (FindAncestor<ComboBoxItem>(source) is not null) return;
        if (e.ChangedButton == MouseButton.Middle)
        {
            FocusGraphCanvas();
            if (!_pointerState.Is(GraphPointerMode.Idle)) CancelPointerGesture();
            _pointerState.PreemptForPan();
            _pointerStart = e.GetPosition(CanvasViewport);
            CanvasViewport.CaptureMouse();
            e.Handled = true;
            return;
        }
        if (e.ChangedButton != MouseButton.Left) return;
        if (IsReadOnly)
        {
            FocusGraphCanvas();
            if (e.ClickCount >= 2)
            {
                var point = e.GetPosition(GraphCanvas);
                var hit = _nodeVisuals.FirstOrDefault(pair => new Rect(
                    Canvas.GetLeft(pair.Value), Canvas.GetTop(pair.Value),
                    pair.Value.ActualWidth, pair.Value.ActualHeight).Contains(point));
                if (hit.Key is { } selected) RequestNodeEdit(selected);
            }
            e.Handled = true;
            return;
        }
        if (FindAncestor<FlowPortControl>(source) is { } port && port.IsAnchorHitTarget(source))
        {
            FocusGraphCanvas();
            BeginWirePress(port, e.GetPosition(GraphCanvas), reconnectIncidentBundle:
                (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control);
            e.Handled = true;
            return;
        }
        if (FindAncestor<CanonicalGraphNodeControl>(source) is { Node: { } node } nodeVisual)
        {
            // Parameter editors keep their native Ctrl/text-selection behavior;
            // merely entering an editor selects its node if needed.
            if (nodeVisual.IsParameterInteractionSource(source))
            {
                if (!_selectedNodes.Contains(node)) SelectNode(node);
                return;
            }
            var controlPressed = (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control;
            var wasSelectedWithPeers = !controlPressed && _selectedNodes.Contains(node) && _selectedNodes.Count > 1;
            if (controlPressed) ToggleNodeSelection(node);
            else if (!wasSelectedWithPeers) SelectNode(node);
            // Inline editors own keyboard focus and popup interaction.  Moving
            // focus to the graph during PreviewMouseDown can close or suppress
            // a ComboBox before its normal WPF mouse route completes.
            FocusGraphCanvas();
            if (e.ClickCount >= 2)
            {
                SelectNode(node);
                _ = RequestNodeEdit(node);
                e.Handled = true;
                return;
            }
            if (!nodeVisual.IsHeaderDragSource(source)
                || !BeginNodeDrag(node, e.GetPosition(GraphCanvas), wasSelectedWithPeers)) return;
            CanvasViewport.CaptureMouse();
            e.Handled = true;
            return;
        }
        FocusGraphCanvas();
        if (FindAncestor<Path>(source) is { Tag: GraphEditorConnectionViewModel }) return;
        if (!BeginMarqueeSelection(e.GetPosition(GraphCanvas),
                (Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)) return;
        CanvasViewport.CaptureMouse();
        e.Handled = true;
    }

    private void FocusGraphCanvas()
    {
        GraphCanvas.Focus();
        Keyboard.Focus(GraphCanvas);
    }

    private void CanvasViewport_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (IsReadOnly) { e.Handled = true; return; }
        var source = e.OriginalSource as DependencyObject;
        // A blank-canvas menu must never be inherited by a node, port, or wire
        // hit target. Clear the previous transient menu before this boundary
        // check so a second right-click cannot reopen stale authoring actions.
        GraphCanvas.ClearValue(ContextMenuProperty);
        if (FindAncestor<FlowPortControl>(source) is not null)
        {
            e.Handled = true;
            return;
        }

        if (FindAncestor<CanonicalGraphNodeControl>(source) is { Node: { } node })
        {
            OpenContextMenu(CreateNodeContextMenu(node));
            e.Handled = true;
            return;
        }

        if (FindAncestor<Path>(source) is { Tag: GraphEditorConnectionViewModel })
        {
            e.Handled = true;
            return;
        }

        _contextGraphPoint = e.GetPosition(GraphCanvas);
        OpenContextMenu(CreateCanvasContextMenu(_contextGraphPoint));
        e.Handled = true;
    }

    /// <summary>Builds the canonical node menu without opening it.</summary>
    public ContextMenu CreateNodeContextMenu(GraphEditorNodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (!_selectedNodes.Contains(node) && !SelectNode(node))
            return FluentContextMenuFactory.Create(GraphCanvas);
        var menu = FluentContextMenuFactory.Create(_nodeVisuals.TryGetValue(node, out var visual) ? visual : GraphCanvas);
        menu.Items.Add(FluentContextMenuFactory.CreateItem(
            "编辑",
            () =>
            {
                if (_selectedNodes.Count == 1 && _selectedNodes.Contains(node))
                    _ = RequestNodeEdit(node);
            }));

        var canDelete = Host is { } host && _selectedNodes.Any(selected =>
            !GraphNodeDefinitionRegistry.TryGet(host.Scope, selected.Type, out var definition)
                || !definition.NonDeletable && !definition.Required);
        if (canDelete)
        {
            menu.Items.Add(FluentContextMenuFactory.CreateItem("删除",
                () => DeleteCurrentSelection(confirmReferencedRemoval: true), critical: true));
        }
        return menu;
    }

    public bool RequestNodeEdit(GraphEditorNodeViewModel node)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (_selectedNodes.Count > 1) return false;
        if (!SelectNode(node)) return false;
        NodeEditRequested?.Invoke(node);
        return true;
    }

    private void Scissors_OnClick(object sender, RoutedEventArgs e)
    {
        SetScissorsMode(!_scissorsMode);
        e.Handled = true;
    }

    private static bool IsLeftAlt(KeyEventArgs e)
        => e.Key == Key.LeftAlt || e.Key == Key.System && e.SystemKey == Key.LeftAlt;

    private static bool IsSpliceModifierActive()
        => (Keyboard.Modifiers & (ModifierKeys.Shift | ModifierKeys.Control | ModifierKeys.Alt))
            == ModifierKeys.Shift;

    private void Root_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (IsReadOnly) { e.Handled = e.Key != Key.Escape; return; }
        if (e.Key is Key.LeftShift or Key.RightShift && _pointerState.Is(GraphPointerMode.NodeDrag))
        {
            UpdateSplicePreview(_lastGraphPointer, IsSpliceModifierActive());
            return;
        }
        if (!IsLeftAlt(e) || _altScissorsMode) return;
        _scissorsModeBeforeAlt = _scissorsMode;
        _altScissorsMode = true;
        _scissorsMode = true;
        CanvasViewport.Cursor = ScissorsCursorFactory.Cursor;
        e.Handled = true;
    }

    private void Root_OnPreviewKeyUp(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.LeftShift or Key.RightShift)
        {
            ClearSplicePreview();
            return;
        }
        if (!IsLeftAlt(e) || !_altScissorsMode) return;
        _altScissorsMode = false;
        _scissorsMode = _scissorsModeBeforeAlt;
        CanvasViewport.Cursor = _scissorsMode ? ScissorsCursorFactory.Cursor : Cursors.Arrow;
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

    private bool BeginWirePress(FlowPortControl port, Point point, bool reconnectIncidentBundle)
    {
        if (IsReadOnly) return false;
        if (_host is null || !TryEndpoint(port, out _) || !_pointerState.Begin(GraphPointerMode.PortPressed))
            return false;
        _pendingWirePort = port;
        _pendingWireBundle = reconnectIncidentBundle;
        _pointerStart = point;
        CanvasViewport.CaptureMouse();
        return true;
    }

    private bool AdvancePendingWire(Point point)
    {
        if (!_pointerState.Is(GraphPointerMode.PortPressed) || _pendingWirePort is null)
            return false;
        var delta = point - _pointerStart;
        if (Math.Abs(delta.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(delta.Y) < SystemParameters.MinimumVerticalDragDistance)
            return false;

        var port = _pendingWirePort;
        var bundle = _pendingWireBundle;
        _pendingWirePort = null;
        _pendingWireBundle = false;
        _pointerState.End(GraphPointerMode.PortPressed);
        return BeginWire(port, point, reconnectIncidentBundle: bundle);
    }

    private bool BeginWire(FlowPortControl port, Point point, GraphConnection? original = null,
        bool reconnectIncidentBundle = false)
    {
        if (_host is null || !TryEndpoint(port, out var endpoint) || !_pointerState.Begin(GraphPointerMode.WireDrag))
            return false;
        var oldNode = _selectedNode;
        var oldConnection = _selectedConnection;
        ClearNodeSelection();
        if (original is null) ClearConnectionSelection();
        NotifySelectionChanged(oldNode, oldConnection);
        _wireOriginal = original;
        _wireFixedEndpoint = null;
        _incidentWireReconnect = false;
        _wireOriginals = original is null ? [] : [original];
        _wireGestureKind = original is null
            ? GraphWireGestureKind.NewConnection
            : GraphWireGestureKind.ReconnectSingleEndpoint;
        if (original is not null)
        {
            if (!TryGetOppositeEndpoint(original, endpoint, out var fixedEndpoint))
            {
                CancelPointerGesture();
                return false;
            }
            _wireFixedEndpoint = fixedEndpoint;
        }
        else
        {
            var incident = (_host.Graph.Connections ?? [])
                .Where(connection => connection is not null && IsIncident(connection, endpoint))
                .ToArray();
            if (IsMultiIncidentPort(endpoint))
            {
                // Multi-capacity ports always author a fresh wire. Reconnecting
                // all incident wires is a separate, explicit Ctrl+drag gesture.
                if (reconnectIncidentBundle && incident.Length > 0)
                {
                    _incidentWireReconnect = true;
                    _wireOriginals = incident;
                    _wireGestureKind = GraphWireGestureKind.ReconnectMultiBundle;
                }
                else _wireGestureKind = GraphWireGestureKind.AddOnMultiPort;
            }
            else if (incident.Length == 1)
            {
                // Flow outputs and Logic inputs have cardinality one. Dragging
                // an occupied endpoint must carry its existing formal wire as
                // a reconnect transaction instead of drawing a second draft.
                _wireOriginal = incident[0];
                _wireOriginals = incident;
                _wireGestureKind = GraphWireGestureKind.ReconnectSingleEndpoint;
                if (!TryGetOppositeEndpoint(incident[0], endpoint, out var fixedEndpoint))
                {
                    CancelPointerGesture();
                    return false;
                }
                _wireFixedEndpoint = fixedEndpoint;
            }
            else if (incident.Length > 1)
            {
                CancelPointerGesture();
                return false;
            }
        }
        _wireStart = endpoint;
        _pointerStart = point;
        _draftWires.Clear();
        _transientWireVisuals.Clear();
        if (_wireOriginals.Count > 0)
        {
            foreach (var draggedConnection in _wireOriginals)
            {
                var visual = _connectionVisuals.FirstOrDefault(pair => pair.Key.Connection.Equals(draggedConnection)).Value;
                if (visual.Line is null)
                {
                    CancelPointerGesture();
                    return false;
                }
                // While a persisted connection is the formal drag visual, its
                // old transparent hit path must not remain at the pre-drag
                // geometry and steal pointer hits from a target port.
                visual.Hit.Visibility = Visibility.Hidden;
                _draftWires.Add(visual.Line);
            }
        }
        else
        {
            // A new uncommitted connection must use the same normal renderer
            // language as a persisted wire. In particular, FlowSelectedColor
            // is white and recreates the generic preview-line defect from
            // 0.3.1.1 when used for a brand-new drag.
            var style = GraphConnectionVisualStyle.For(endpoint.InterfaceKind);
            var wire = new Path
            {
                StrokeThickness = style.StrokeThickness,
                IsHitTestVisible = false,
                Tag = "UncommittedCanonicalConnection",
            };
            ApplyWireBrush(wire, endpoint.InterfaceKind, selected: false);
            _draftWires.Add(wire);
            _transientWireVisuals.Add(wire);
            GraphCanvas.Children.Insert(0, wire);
        }
        _draftWire = _draftWires.FirstOrDefault();
        CanvasViewport.CaptureMouse();
        UpdateWire(point);
        return true;
    }

    private static bool TryGetOppositeEndpoint(
        GraphConnection original,
        GraphEditorEndpoint movingEndpoint,
        out GraphEditorEndpoint fixedEndpoint)
    {
        fixedEndpoint = default;
        if (movingEndpoint.InterfaceKind != original.InterfaceKind) return false;
        if (movingEndpoint.IsOutput
            && string.Equals(movingEndpoint.NodeId, original.FromNodeId, StringComparison.Ordinal)
            && string.Equals(movingEndpoint.PortId, original.FromPortId, StringComparison.Ordinal))
        {
            fixedEndpoint = GraphEditorEndpoint.Input(
                original.ToNodeId, original.ToPortId, original.InterfaceKind);
            return true;
        }
        if (movingEndpoint.IsInput
            && string.Equals(movingEndpoint.NodeId, original.ToNodeId, StringComparison.Ordinal)
            && string.Equals(movingEndpoint.PortId, original.ToPortId, StringComparison.Ordinal))
        {
            fixedEndpoint = GraphEditorEndpoint.Output(
                original.FromNodeId, original.FromPortId, original.InterfaceKind);
            return true;
        }
        return false;
    }

    private static void ApplyWireBrush(Shape shape, GraphInterfaceKind interfaceKind, bool selected)
    {
        var resourceKey = selected
            ? "TextFillColorPrimaryBrush"
            : interfaceKind == GraphInterfaceKind.Flow
                ? "AccentFillColorDefaultBrush"
                : "SystemFillColorCautionBrush";
        shape.SetResourceReference(Shape.StrokeProperty, resourceKey);
    }

    private void CanvasViewport_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        var point = e.GetPosition(GraphCanvas);
        _lastGraphPointer = point;
        if (_pointerState.Is(GraphPointerMode.Pan))
        {
            var current = e.GetPosition(CanvasViewport);
            _viewportController.PanBy(current.X - _pointerStart.X, current.Y - _pointerStart.Y);
            _pointerStart = current;
            ApplyViewport();
        }
        else if (_pointerState.Is(GraphPointerMode.NodeDrag) && _dragNode is not null)
        {
            _ = UpdateSelectedNodeDrag(point - _pointerStart, IsSpliceModifierActive());
        }
        else if (_pointerState.Is(GraphPointerMode.BoxSelect)) UpdateMarquee(point);
        else if (_pointerState.Is(GraphPointerMode.PortPressed))
        {
            if (e.LeftButton != MouseButtonState.Pressed) CancelPointerGesture();
            else _ = AdvancePendingWire(point);
        }
        else if (_pointerState.Is(GraphPointerMode.WireDrag)) UpdateWire(point);
    }

    private void CanvasViewport_OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && _pointerState.Is(GraphPointerMode.WireDrag))
        {
            var target = FindWireTarget(e.GetPosition(GraphCanvas));
            _ = CompleteWire(target);
            e.Handled = true;
        }
        else if (e.ChangedButton == MouseButton.Left && _pointerState.Is(GraphPointerMode.PortPressed))
        {
            CancelPointerGesture();
            e.Handled = true;
        }
        else if (e.ChangedButton == MouseButton.Left && _pointerState.Is(GraphPointerMode.NodeDrag))
        {
            var collapseNode = !_nodeDragThresholdPassed && _collapseSelectionOnNodeClick ? _dragNode : null;
            if (_nodeDragThresholdPassed
                && IsSpliceModifierActive()
                && _splicePlan is { } splicePlan)
            {
                ClearSplicePreview();
                _ = _host?.SpliceConnection(splicePlan);
            }
            EndPointerGesture();
            if (collapseNode is not null) SelectNode(collapseNode);
            e.Handled = true;
        }
        else if (e.ChangedButton == MouseButton.Left && _pointerState.Is(GraphPointerMode.BoxSelect))
        {
            if (!_marqueeThresholdPassed) ClearSelection();
            EndPointerGesture();
            e.Handled = true;
        }
        else if (e.ChangedButton == MouseButton.Middle && _pointerState.Is(GraphPointerMode.Pan)) EndPointerGesture();
    }

    private void UpdateWire(Point point)
    {
        if (_draftWires.Count == 0 || _wireStart is null) return;
        _ = PreviewWireTarget(FindWireTarget(point));
        if (_incidentWireReconnect)
        {
            for (var index = 0; index < _wireOriginals.Count && index < _draftWires.Count; index++)
            {
                var original = _wireOriginals[index];
                var fixedEndpoint = _wireStart.Value.IsInput
                    ? new GraphEditorEndpoint(original.FromNodeId, original.FromPortId, GraphPortDirection.Output, original.InterfaceKind)
                    : new GraphEditorEndpoint(original.ToNodeId, original.ToPortId, GraphPortDirection.Input, original.InterfaceKind);
                var fixedPoint = _ports.TryGetValue(EndpointKey(fixedEndpoint.NodeId, fixedEndpoint.PortId), out var fixedPort)
                    ? fixedPort.GetAnchorPoint(GraphCanvas)
                    : _pointerStart;
                var geometry = WireGeometry(fixedPoint, point);
                _draftWires[index].Data = geometry;
                if (_connectionVisuals.FirstOrDefault(pair => pair.Key.Connection.Equals(original)).Value.Hit is { } hit)
                    hit.Data = geometry;
            }
        }
        else
        {
            var visualStart = _wireFixedEndpoint ?? _wireStart.Value;
            var startPort = _ports.TryGetValue(EndpointKey(visualStart.NodeId, visualStart.PortId), out var startWirePort)
                ? startWirePort : null;
            var start = startPort?.GetAnchorPoint(GraphCanvas) ?? _pointerStart;
            var geometry = WireGeometry(start, point);
            _draftWires[0].Data = geometry;
            if (_wireOriginal is not null
                && _connectionVisuals.FirstOrDefault(pair => pair.Key.Connection.Equals(_wireOriginal)).Value.Hit is { } hit)
                hit.Data = geometry;
        }
    }

    private FlowPortControl? FindWireTarget(Point graphPoint)
    {
        var viewportPoint = GraphCanvas.TransformToAncestor(CanvasViewport).Transform(graphPoint);
        var hit = CanvasViewport.InputHitTest(viewportPoint) as DependencyObject;
        var target = FindAncestor<FlowPortControl>(hit);
        return target is not null && target.IsAnchorHitTarget(hit) ? target : null;
    }

    private bool PreviewWireTarget(FlowPortControl? target)
    {
        foreach (var port in _ports.Values)
        {
            port.IsConnecting = false;
            port.IsValidTarget = false;
        }
        if (_wireStart is not { } moving || _host is null) return false;
        if (_ports.TryGetValue(EndpointKey(moving.NodeId, moving.PortId), out var movingPort))
            movingPort.IsConnecting = true;
        if (target is null || !TryEndpoint(target, out var targetEndpoint)) return false;

        var valid = _wireGestureKind switch
        {
            GraphWireGestureKind.NewConnection or GraphWireGestureKind.AddOnMultiPort
                => movingPort is not null
                    && target.IsCompatibleEndpoint(movingPort)
                    && _host.CanConnect(moving, targetEndpoint),
            GraphWireGestureKind.ReconnectSingleEndpoint
                => _wireOriginal is not null
                    && _wireFixedEndpoint is { } fixedEndpoint
                    && targetEndpoint.Direction == moving.Direction
                    && targetEndpoint.InterfaceKind == moving.InterfaceKind
                    && _host.CanReconnect(_wireOriginal, fixedEndpoint, targetEndpoint),
            GraphWireGestureKind.ReconnectMultiBundle
                => targetEndpoint.Direction == moving.Direction
                    && targetEndpoint.InterfaceKind == moving.InterfaceKind
                    && _host.CanReconnectIncidentConnections(_wireOriginals, moving, targetEndpoint),
            _ => false,
        };
        target.IsValidTarget = valid;
        return valid;
    }

    private bool CompleteWire(FlowPortControl? target)
    {
        var result = false;
        if (_host is not null && _wireStart is { } start)
        {
            if (target is not null)
            {
                if (PreviewWireTarget(target) && TryEndpoint(target, out var endpoint))
                {
                    result = _wireGestureKind switch
                    {
                        GraphWireGestureKind.ReconnectMultiBundle
                            => _host.CompleteIncidentWireDrag(_wireOriginals, start, endpoint),
                        GraphWireGestureKind.ReconnectSingleEndpoint when _wireFixedEndpoint is { } fixedEndpoint
                            => _host.CompleteWireDrag(fixedEndpoint, endpoint, _wireOriginal),
                        GraphWireGestureKind.NewConnection or GraphWireGestureKind.AddOnMultiPort
                            => _host.CompleteWireDrag(start, endpoint),
                        _ => false,
                    };
                }
            }
            else
            {
                result = _wireGestureKind switch
                {
                    GraphWireGestureKind.ReconnectMultiBundle
                        => _host.CompleteIncidentWireDrag(_wireOriginals, start, null),
                    GraphWireGestureKind.ReconnectSingleEndpoint
                        => _host.CompleteWireDrag(start, null, _wireOriginal),
                    _ => _host.CompleteWireDrag(start, null),
                };
            }
        }
        CancelPointerGesture();
        return result;
    }

    private void ConnectionHit_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Path { Tag: GraphEditorConnectionViewModel connection }) return;
        SetSelectedConnection(connection);
        if (_scissorsMode)
        {
            _ = _host?.Disconnect(connection);
            e.Handled = true;
            return;
        }
        var point = e.GetPosition(GraphCanvas);
        if (TryGetConnectionMovingPort(connection, point, out var movingPort))
            BeginWire(movingPort, point, connection.Connection);
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
        e.Handled = HandleKeyboardCommand(e.Key, e.OriginalSource as DependencyObject);
    }

    /// <summary>Shared by the routed GraphCanvas handler and deterministic STA tests.</summary>
    public bool HandleKeyboardCommand(Key key)
        => HandleKeyboardCommand(key, GraphCanvas);

    /// <summary>
    /// Editable controls retain their native Delete behavior even while graph
    /// nodes remain selected behind them.
    /// </summary>
    public bool HandleKeyboardCommand(Key key, DependencyObject? source)
    {
        if (IsReadOnly) return false;
        if (key == Key.Escape)
        {
            CancelPointerGesture();
            return true;
        }
        if (key == Key.Delete && IsEditableKeyboardSource(source)) return false;
        if (key == Key.Delete && _selectedConnection is not null)
        {
            var selected = _selectedConnection;
            var result = _host is not null && _host.Disconnect(selected);
            if (result) _selectedConnection = null;
            return true;
        }
        if (key == Key.Delete && _selectedNodes.Count != 0)
        {
            _ = DeleteCurrentSelection(confirmReferencedRemoval: true);
            return true;
        }
        return false;
    }

    private static bool IsEditableKeyboardSource(DependencyObject? source)
        => FindAncestor<TextBoxBase>(source) is not null
            || FindAncestor<PasswordBox>(source) is not null
            || FindAncestor<ComboBox>(source) is not null;

    private void CanvasViewport_OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (!_canceling && !_pointerState.Is(GraphPointerMode.Idle)) CancelPointerGesture(false);
    }

    public bool DisconnectSelectedConnection()
    {
        if (IsReadOnly) return false;
        if (_host is null || _selectedConnection is null) return false;
        var result = _host.Disconnect(_selectedConnection);
        if (result) _selectedConnection = null;
        return result;
    }

    /// <summary>
    /// Routes node deletion through the host. Interactive node deletion passes
    /// confirmation explicitly so incident wires are removed in the same undo
    /// transaction; Core still rejects required and non-deletable nodes.
    /// </summary>
    public bool RemoveSelectedNode(bool confirmReferencedRemoval = false)
    {
        if (_selectedNode is null) return false;
        return DeleteCurrentSelection(confirmReferencedRemoval);
    }

    /// <summary>
    /// Deletes every deletable selected node. Required/non-deletable nodes are
    /// preserved and remain selected; they never block deletable peers.
    /// </summary>
    public bool RemoveSelectedNodes(bool confirmReferencedRemoval = false)
        => DeleteCurrentSelection(confirmReferencedRemoval);

    /// <summary>
    /// Shared keyboard/context-menu selection delete. Core removes every
    /// deletable node and all incident wires in one graph/Undo transaction;
    /// protected peers survive and remain selected.
    /// </summary>
    public bool DeleteCurrentSelection(bool confirmReferencedRemoval = false)
    {
        if (IsReadOnly) return false;
        if (_host is null || _selectedNodes.Count == 0) return false;
        var selectedIds = _selectedNodes.Select(node => node.NodeId)
            .Where(id => !string.IsNullOrWhiteSpace(id)).Distinct(StringComparer.Ordinal).ToArray();
        var deletableIds = selectedIds.Where(id =>
        {
            var matches = _host.Nodes.Where(candidate =>
                string.Equals(candidate.NodeId, id, StringComparison.Ordinal)).ToArray();
            return matches.Length == 1 && (!GraphNodeDefinitionRegistry.TryGet(_host.Scope, matches[0].Type, out var definition)
                || !definition.NonDeletable && !definition.Required);
        }).ToArray();
        var protectedIds = selectedIds.Except(deletableIds, StringComparer.Ordinal).ToArray();
        _host.SetAuthoringIssue("graph.selection.delete", null);
        var removedAny = _host.RemoveNodes(selectedIds, confirmReferencedRemoval);
        if (removedAny && protectedIds.Length != 0)
        {
            _host.SetAuthoringIssue("graph.selection.delete", new ValidationIssue(
                "graph.node.not_deletable",
                $"{protectedIds.Length} selected required/non-deletable node(s) were retained.",
                "node_id",
                NodeId: protectedIds[0]));
        }

        var survivingIds = selectedIds.Where(id => _host.Nodes.Count(node =>
            string.Equals(node.NodeId, id, StringComparison.Ordinal)) == 1).ToArray();
        SetNodeSelection(_host.Nodes.Where(node => survivingIds.Contains(node.NodeId, StringComparer.Ordinal)));
        return removedAny;
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
        SetNodeSelection([node]);
        NotifySelectionChanged(oldNode, oldConnection);
        return true;
    }

    /// <summary>Selects unique materialized node IDs, optionally adding them to the current set.</summary>
    public bool SelectNodes(IEnumerable<string> nodeIds, bool additive = false)
    {
        ArgumentNullException.ThrowIfNull(nodeIds);
        var requested = nodeIds.Where(id => !string.IsNullOrWhiteSpace(id))
            .Distinct(StringComparer.Ordinal).ToArray();
        var matches = requested.Select(id => _nodeVisuals.Keys.Where(node =>
                string.Equals(node.NodeId, id, StringComparison.Ordinal)).ToArray())
            .ToArray();
        if (matches.Any(match => match.Length != 1)) return false;
        var oldNode = _selectedNode;
        var oldConnection = _selectedConnection;
        ClearConnectionSelection();
        var next = additive
            ? _selectedNodes.Concat(matches.Select(match => match[0])).Distinct().ToArray()
            : matches.Select(match => match[0]).ToArray();
        SetNodeSelection(next);
        NotifySelectionChanged(oldNode, oldConnection);
        return true;
    }

    /// <summary>Toggles one unique materialized node without affecting its peers.</summary>
    public bool ToggleNodeSelection(string nodeId)
    {
        var matches = _nodeVisuals.Keys.Where(node => string.Equals(node.NodeId, nodeId,
            StringComparison.Ordinal)).ToArray();
        if (matches.Length != 1) return false;
        ToggleNodeSelection(matches[0]);
        return true;
    }

    /// <summary>Starts a deterministic marquee gesture without requiring a real mouse device.</summary>
    public bool BeginMarqueeSelection(Point start, bool additive = false)
    {
        if (!IsFinite(start) || !_pointerState.Begin(GraphPointerMode.BoxSelect)) return false;
        _pointerStart = start;
        _lastGraphPointer = start;
        _selectionBeforeMarquee = [.. _selectedNodes];
        _additiveMarquee = additive;
        _marqueeThresholdPassed = false;
        _selectionBoxBounds = new Rect(start, start);
        CreateSelectionBox();
        ClearConnectionSelection();
        return true;
    }

    public bool UpdateMarqueeSelection(Point current)
    {
        if (!_pointerState.Is(GraphPointerMode.BoxSelect) || !IsFinite(current)) return false;
        UpdateMarquee(current);
        return _marqueeThresholdPassed;
    }

    public bool CompleteMarqueeSelection()
    {
        if (!_pointerState.Is(GraphPointerMode.BoxSelect)) return false;
        if (!_marqueeThresholdPassed) ClearSelection();
        EndPointerGesture();
        return true;
    }

    /// <summary>Applies marquee geometry directly for deterministic STA tests.</summary>
    public void ApplyMarqueeSelection(Rect bounds, bool additive = false)
    {
        var oldNode = _selectedNode;
        var oldConnection = _selectedConnection;
        var hits = HitTestNodes(bounds);
        var next = additive ? _selectedNodes.Concat(hits).Distinct().ToArray() : hits;
        SetNodeSelection(next);
        ClearConnectionSelection();
        NotifySelectionChanged(oldNode, oldConnection);
    }

    /// <summary>Moves the current selection by one finite graph-space delta.</summary>
    public bool MoveSelectedNodes(Vector delta)
    {
        if (_host is null || _selectedNodes.Count == 0
            || !double.IsFinite(delta.X) || !double.IsFinite(delta.Y)) return false;
        var selected = _selectedNodes.ToArray();
        var transactional = selected.Length > 1;
        if (transactional && !_host.BeginLayoutMove(selected.Select(node => node.NodeId))) return false;
        foreach (var node in selected)
            _host.SetNodePosition(node.NodeId, Safe(node.X + delta.X), Safe(node.Y + delta.Y));
        if (transactional) _ = _host.CommitLayoutMove();
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
        SetNodeSelection([]);
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
            pair.Value.IsSelected = _selectedNodes.Contains(pair.Key);
    }

    private void SetNodeSelection(IEnumerable<GraphEditorNodeViewModel> nodes)
    {
        _selectedNodes.Clear();
        foreach (var node in nodes.Where(_nodeVisuals.ContainsKey)) _selectedNodes.Add(node);
        _pendingSelectedNodeIds.Clear();
        SynchronizeUniqueSelectedNode();
        ApplyNodeSelectionVisuals();
    }

    private void ToggleNodeSelection(GraphEditorNodeViewModel node)
    {
        var oldNode = _selectedNode;
        var oldConnection = _selectedConnection;
        ClearConnectionSelection();
        if (!_selectedNodes.Add(node)) _selectedNodes.Remove(node);
        _pendingSelectedNodeIds.Clear();
        SynchronizeUniqueSelectedNode();
        ApplyNodeSelectionVisuals();
        NotifySelectionChanged(oldNode, oldConnection);
    }

    private void SynchronizeUniqueSelectedNode()
        => _selectedNode = _selectedNodes.Count == 1 ? _selectedNodes.Single() : null;

    /// <summary>Small deterministic seam for tests and keyboard-accessible hosts.</summary>
    public bool BeginNewConnectionDrag(FlowPortControl port) => BeginWire(port, new Point(0, 0));

    /// <summary>Begins the no-visual PortPressed phase used by the real pointer path.</summary>
    public bool BeginPendingConnectionPress(FlowPortControl port, Point point, bool reconnectIncidentBundle = false)
        => BeginWirePress(port, point, reconnectIncidentBundle);

    /// <summary>Advances a pending press and starts a wire only after the system drag threshold.</summary>
    public bool AdvancePendingConnectionPress(Point point) => AdvancePendingWire(point);

    /// <summary>Releases a light click without creating a wire or graph edit.</summary>
    public bool ReleasePendingConnectionPress()
    {
        if (!_pointerState.Is(GraphPointerMode.PortPressed)) return false;
        CancelPointerGesture();
        return true;
    }

    /// <summary>Deterministic candidate seam shared with hover and drop validation.</summary>
    public bool PreviewConnectionTarget(FlowPortControl? target)
        => _pointerState.Is(GraphPointerMode.WireDrag) && PreviewWireTarget(target);

    /// <summary>Deterministic seam for the explicit Ctrl+drag bundle gesture.</summary>
    public bool BeginIncidentConnectionBundleDrag(FlowPortControl port)
        => BeginWire(port, new Point(0, 0), reconnectIncidentBundle: true);

    /// <summary>Models an explicit reconnect that replaces the original input.</summary>
    public bool BeginExistingConnectionDrag(GraphEditorConnectionViewModel connection)
    {
        if (connection is null || !TryGetConnectionPorts(connection, out _, out var input)) return false;
        SetSelectedConnection(connection);
        return BeginWire(input, new Point(0, 0), connection.Connection);
    }

    /// <summary>
    /// Models an explicit connection-hit reconnect. The graph-space click
    /// chooses the nearer real endpoint; the opposite endpoint remains fixed
    /// when the host completes the reconnect transaction.
    /// </summary>
    public bool BeginExistingConnectionDrag(GraphEditorConnectionViewModel connection, Point graphPoint)
    {
        if (connection is null || !TryGetConnectionMovingPort(connection, graphPoint, out var movingPort)) return false;
        SetSelectedConnection(connection);
        return BeginWire(movingPort, graphPoint, connection.Connection);
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

    private bool TryGetConnectionMovingPort(GraphEditorConnectionViewModel connection, Point graphPoint,
        out FlowPortControl movingPort)
    {
        movingPort = null!;
        if (!IsFinite(graphPoint) || !TryGetConnectionPorts(connection, out var output, out var input))
            return false;

        var outputAnchor = output.GetAnchorPoint(GraphCanvas);
        var inputAnchor = input.GetAnchorPoint(GraphCanvas);
        if (!IsFinite(outputAnchor) || !IsFinite(inputAnchor)) return false;

        // The nearer endpoint is the one being moved. BeginWire derives the
        // opposite original endpoint and holds it fixed for formal geometry.
        movingPort = DistanceSquared(graphPoint, outputAnchor) <= DistanceSquared(graphPoint, inputAnchor)
            ? output
            : input;
        return true;
    }

    private static bool CrossedDragThreshold(Vector delta)
        => Math.Abs(delta.X) >= SystemParameters.MinimumHorizontalDragDistance
            || Math.Abs(delta.Y) >= SystemParameters.MinimumVerticalDragDistance;

    private bool BeginNodeDrag(GraphEditorNodeViewModel node, Point start, bool collapseSelectionOnClick)
    {
        if (IsReadOnly) return false;
        if (!_selectedNodes.Contains(node) || !_pointerState.Begin(GraphPointerMode.NodeDrag)) return false;
        if (_selectedNodes.Count > 1
            && (_host is null || !_host.BeginLayoutMove(_selectedNodes.Select(selected => selected.NodeId))))
        {
            _pointerState.End(GraphPointerMode.NodeDrag);
            return false;
        }
        _dragNode = node;
        _pointerStart = start;
        _lastGraphPointer = start;
        _dragOrigins = _selectedNodes.ToDictionary(selected => selected,
            selected => new Point(selected.X, selected.Y));
        _nodeDragThresholdPassed = false;
        _collapseSelectionOnNodeClick = collapseSelectionOnClick;
        return true;
    }

    /// <summary>Deterministic seam mirroring MouseDown on an already-selected node header.</summary>
    public bool BeginSelectedNodeDrag(string nodeId)
    {
        var matches = _selectedNodes.Where(node => string.Equals(node.NodeId, nodeId,
            StringComparison.Ordinal)).ToArray();
        return matches.Length == 1 && BeginNodeDrag(matches[0], new Point(0, 0),
            collapseSelectionOnClick: _selectedNodes.Count > 1);
    }

    /// <summary>Applies one graph-space pointer delta to the active selected-node drag.</summary>
    public bool UpdateSelectedNodeDrag(Vector delta, bool shiftPressed = false)
    {
        if (!_pointerState.Is(GraphPointerMode.NodeDrag) || _dragNode is null
            || !double.IsFinite(delta.X) || !double.IsFinite(delta.Y)) return false;
        if (!_nodeDragThresholdPassed && CrossedDragThreshold(delta))
        {
            _nodeDragThresholdPassed = true;
            _collapseSelectionOnNodeClick = false;
        }
        if (!_nodeDragThresholdPassed) return false;
        foreach (var (node, origin) in _dragOrigins)
            _host?.SetNodePosition(node.NodeId, Safe(origin.X + delta.X), Safe(origin.Y + delta.Y));
        _lastGraphPointer = _pointerStart + delta;
        UpdateSplicePreview(_lastGraphPointer, shiftPressed && _selectedNodes.Count == 1);
        return true;
    }

    public bool CompleteSelectedNodeDrag()
    {
        if (!_pointerState.Is(GraphPointerMode.NodeDrag)) return false;
        EndPointerGesture();
        return true;
    }

    private void CreateSelectionBox()
    {
        _selectionBox = new Rectangle
        {
            Stroke = Brushes.DodgerBlue,
            StrokeThickness = 1,
            Fill = new SolidColorBrush(Color.FromArgb(40, 30, 144, 255)),
            IsHitTestVisible = false,
        };
        AutomationProperties.SetAutomationId(_selectionBox, "CanonicalGraphMarqueeSelection");
        Panel.SetZIndex(_selectionBox, int.MaxValue);
        GraphCanvas.Children.Add(_selectionBox);
        Canvas.SetLeft(_selectionBox, _pointerStart.X);
        Canvas.SetTop(_selectionBox, _pointerStart.Y);
    }

    private void UpdateMarquee(Point point)
    {
        var delta = point - _pointerStart;
        if (!_marqueeThresholdPassed && !CrossedDragThreshold(delta)) return;
        _marqueeThresholdPassed = true;
        _selectionBoxBounds = new Rect(_pointerStart, point);
        if (_selectionBox is not null)
        {
            Canvas.SetLeft(_selectionBox, _selectionBoxBounds.Left);
            Canvas.SetTop(_selectionBox, _selectionBoxBounds.Top);
            _selectionBox.Width = _selectionBoxBounds.Width;
            _selectionBox.Height = _selectionBoxBounds.Height;
        }

        var oldNode = _selectedNode;
        var oldConnection = _selectedConnection;
        var hits = HitTestNodes(_selectionBoxBounds);
        SetNodeSelection(_additiveMarquee ? _selectionBeforeMarquee.Concat(hits) : hits);
        NotifySelectionChanged(oldNode, oldConnection);
    }

    private IReadOnlyList<GraphEditorNodeViewModel> HitTestNodes(Rect bounds)
        => _nodeVisuals.Where(pair => NodeBounds(pair.Key, pair.Value).IntersectsWith(bounds))
            .Select(pair => pair.Key).ToArray();

    private static Rect NodeBounds(GraphEditorNodeViewModel node, FrameworkElement visual)
    {
        var width = double.IsFinite(visual.ActualWidth) && visual.ActualWidth > 0 ? visual.ActualWidth : NodeWidth;
        var height = double.IsFinite(visual.ActualHeight) && visual.ActualHeight > 0 ? visual.ActualHeight : NodeHeight;
        return new Rect(Safe(node.X), Safe(node.Y), width, height);
    }

    private void UpdateSplicePreview(Point graphPoint, bool shiftPressed)
    {
        if (!shiftPressed || !_nodeDragThresholdPassed || _host is null || _dragNode is null
            || _selectedNodes.Count != 1 || !_selectedNodes.Contains(_dragNode))
        {
            ClearSplicePreview();
            return;
        }

        var pen = new Pen(Brushes.Black, 14);
        var candidates = _connectionVisuals.Where(pair => pair.Value.Hit.Data is { } geometry
                && geometry.StrokeContains(pen, graphPoint))
            .Select(pair => pair.Key)
            .Distinct()
            .Select(connection => _host.TryCreateSplicePlan(connection.Connection, _dragNode.NodeId, out var plan)
                ? (Connection: connection, Plan: plan)
                : (Connection: null, Plan: null))
            .Where(candidate => candidate.Connection is not null && candidate.Plan is not null)
            .ToArray();
        if (candidates.Length != 1)
        {
            ClearSplicePreview();
            return;
        }

        var candidate = candidates[0];
        var activeConnection = candidate.Connection!;
        var activePlan = candidate.Plan!;
        if (ReferenceEquals(_spliceCandidate, candidate.Connection))
        {
            _splicePlan = activePlan;
            UpdateSpliceGhostGeometry();
            return;
        }
        ActivateSplicePreview(activeConnection, activePlan);
    }

    private void ActivateSplicePreview(GraphEditorConnectionViewModel connection,
        GraphConnectionSplicePlan plan)
    {
        ClearSplicePreview();
        _spliceCandidate = connection;
        _splicePlan = plan;
        if (_connectionVisuals.TryGetValue(connection, out var visual))
            ApplyWireBrush(visual.Line, connection.InterfaceKind, selected: true);
        for (var index = 0; index < plan.Replacements.Count; index++)
        {
            var ghost = new Path
            {
                StrokeThickness = 2,
                StrokeDashArray = [4, 3],
                Opacity = .8,
                IsHitTestVisible = false,
                Tag = "CanonicalGraphSpliceGhost",
            };
            ApplyWireBrush(ghost, connection.InterfaceKind, selected: false);
            AutomationProperties.SetAutomationId(ghost, $"CanonicalGraphSpliceGhost_{index + 1}");
            Panel.SetZIndex(ghost, int.MaxValue - 1);
            _spliceGhostWires.Add(ghost);
            GraphCanvas.Children.Add(ghost);
        }
        UpdateSpliceGhostGeometry();
    }

    private void UpdateSpliceGhostGeometry()
    {
        if (_splicePlan is null || _spliceGhostWires.Count != _splicePlan.Replacements.Count) return;
        for (var index = 0; index < _splicePlan.Replacements.Count; index++)
            if (TryConnectionGeometry(_splicePlan.Replacements[index], out var geometry))
                _spliceGhostWires[index].Data = geometry;
    }

    private bool TryConnectionGeometry(GraphConnection connection, out PathGeometry geometry)
    {
        geometry = null!;
        if (!TryGetPort(connection.FromNodeId, connection.FromPortId, out var from)
            || !TryGetPort(connection.ToNodeId, connection.ToPortId, out var to)) return false;
        var start = from.GetAnchorPoint(GraphCanvas);
        var end = to.GetAnchorPoint(GraphCanvas);
        if (!IsFinite(start) || !IsFinite(end)) return false;
        geometry = WireGeometry(start, end);
        return true;
    }

    private void ClearSplicePreview()
    {
        if (_spliceCandidate is not null && _connectionVisuals.TryGetValue(_spliceCandidate, out var visual))
            ApplyWireBrush(visual.Line, _spliceCandidate.InterfaceKind,
                ReferenceEquals(_spliceCandidate, _selectedConnection));
        foreach (var ghost in _spliceGhostWires) GraphCanvas.Children.Remove(ghost);
        _spliceGhostWires.Clear();
        _spliceCandidate = null;
        _splicePlan = null;
    }

    /// <summary>Deterministic seam for splice validation/preview tests.</summary>
    public bool PreviewSpliceCandidate(GraphEditorNodeViewModel node,
        GraphEditorConnectionViewModel connection)
    {
        if (_host is null || node is null || connection is null || _selectedNodes.Count != 1
            || !_selectedNodes.Contains(node)
            || !_host.TryCreateSplicePlan(connection.Connection, node.NodeId, out var plan))
            return false;
        ActivateSplicePreview(connection, plan);
        return true;
    }

    /// <summary>Commits the currently preflighted splice as one Core edit.</summary>
    public bool CompleteActiveSplice()
    {
        if (_host is null || _splicePlan is not { } plan) return false;
        ClearSplicePreview();
        return _host.SpliceConnection(plan);
    }

    private void CancelPointerGesture(bool releaseCapture = true)
    {
        var canceledMode = _pointerState.Mode;
        var oldNode = _selectedNode;
        var oldConnection = _selectedConnection;
        var restoreMarqueeSelection = canceledMode == GraphPointerMode.BoxSelect
            ? _selectionBeforeMarquee.ToArray()
            : [];
        _canceling = true;
        try
        {
            if (canceledMode == GraphPointerMode.NodeDrag) _ = _host?.CancelLayoutMove();
            _pointerState.Cancel();
            ClearSplicePreview();
            _dragNode = null;
            _dragOrigins.Clear();
            _nodeDragThresholdPassed = false;
            _collapseSelectionOnNodeClick = false;
            if (_selectionBox is not null) GraphCanvas.Children.Remove(_selectionBox);
            _selectionBox = null;
            _selectionBoxBounds = Rect.Empty;
            _marqueeThresholdPassed = false;
            _additiveMarquee = false;
            if (canceledMode == GraphPointerMode.BoxSelect) SetNodeSelection(restoreMarqueeSelection);
            _selectionBeforeMarquee.Clear();
            _pendingWirePort = null;
            _pendingWireBundle = false;
            _wireStart = null;
            _wireFixedEndpoint = null;
            _wireOriginal = null;
            foreach (var draft in _transientWireVisuals) GraphCanvas.Children.Remove(draft);
            RestoreDraggedConnectionVisuals();
            _draftWires.Clear();
            _transientWireVisuals.Clear();
            _draftWire = null;
            _wireOriginals = [];
            _incidentWireReconnect = false;
            _wireGestureKind = GraphWireGestureKind.None;
            foreach (var port in _ports.Values) { port.IsConnecting = false; port.IsValidTarget = false; }
            if (releaseCapture && Mouse.Captured == CanvasViewport) Mouse.Capture(null);
            if (canceledMode == GraphPointerMode.BoxSelect) NotifySelectionChanged(oldNode, oldConnection);
        }
        finally { _canceling = false; }
    }

    private void EndPointerGesture()
    {
        if (_pointerState.Is(GraphPointerMode.NodeDrag)) _ = _host?.CommitLayoutMove();
        if (_pointerState.Is(GraphPointerMode.BoxSelect))
            _selectionBeforeMarquee = [.. _selectedNodes];
        CancelPointerGesture();
    }

    private void RestoreDraggedConnectionVisuals()
    {
        foreach (var original in _wireOriginals)
        {
            var pair = _connectionVisuals.FirstOrDefault(item => item.Key.Connection.Equals(original));
            if (pair.Key is null
                || !TryGetPort(pair.Key.FromNodeId, pair.Key.FromPortId, out var from)
                || !TryGetPort(pair.Key.ToNodeId, pair.Key.ToPortId, out var to))
                continue;
            var geometry = WireGeometry(from.GetAnchorPoint(GraphCanvas), to.GetAnchorPoint(GraphCanvas));
            pair.Value.Line.Data = geometry;
            pair.Value.Hit.Data = geometry;
            pair.Value.Hit.Visibility = Visibility.Visible;
        }
    }

    private void ClearVisualState()
    {
        CancelPointerGesture(false);
        foreach (var node in _nodeVisuals.Keys) node.PropertyChanged -= NodePropertyChanged;
        foreach (var visual in _nodeVisuals.Values) visual.DisposeInlineEditor();
        GraphCanvas.Children.Clear();
        _nodeVisuals.Clear();
        _ports.Clear();
        _connectionHits.Clear();
        _connectionVisuals.Clear();
        _selectedConnection = null;
        _selectedNode = null;
        _selectedNodes.Clear();
        _pendingSelectedNodeIds.Clear();
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

    private void CaptureViewport(GraphViewportState state)
    {
        state.PanX = Safe(_viewportController.PanX);
        state.PanY = Safe(_viewportController.PanY);
        state.Zoom = Math.Clamp(Safe(_viewportController.Zoom), GraphCoordinateTransform.MinZoom,
            GraphCoordinateTransform.MaxZoom);
    }

    private void RestoreViewport(GraphViewportState? state)
    {
        _viewportController.PanX = state is null ? 0d : Safe(state.PanX);
        _viewportController.PanY = state is null ? 0d : Safe(state.PanY);
        _viewportController.Zoom = state is null ? 1d : Math.Clamp(Safe(state.Zoom),
            GraphCoordinateTransform.MinZoom, GraphCoordinateTransform.MaxZoom);
        ApplyViewport();
    }

    private void Zoom100_OnClick(object sender, RoutedEventArgs e) { _viewportController.SetZoomAt(1, ViewportCenter()); ApplyViewport(); }
    public void FitAllNodes()
    {
        var points = _host?.Nodes.SelectMany(node =>
        {
            var width = NodeWidth;
            var height = NodeHeight;
            if (_nodeVisuals.TryGetValue(node, out var visual))
            {
                if (double.IsFinite(visual.ActualWidth) && visual.ActualWidth > 0) width = visual.ActualWidth;
                if (double.IsFinite(visual.ActualHeight) && visual.ActualHeight > 0) height = visual.ActualHeight;
            }
            return new[] { new Point(node.X, node.Y), new Point(node.X + width, node.Y + height) };
        });
        _viewportController.FitToBounds(points, new Size(CanvasViewport.ActualWidth, CanvasViewport.ActualHeight));
        ApplyViewport();
    }
    private void FitAll_OnClick(object sender, RoutedEventArgs e) => FitAllNodes();
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
        if (ReferenceEquals(oldNode, _selectedNode) && ReferenceEquals(oldConnection, _selectedConnection)
            && _selectedNodes.Count <= 1) return;
        var args = _selectedNodes.Count > 1
            ? new GraphSelectionChangedEventArgs(GraphSelectionKind.MultipleNodes,
                nodes: _selectedNodes.OrderBy(node => node.NodeId, StringComparer.Ordinal).ToArray())
            : _selectedNode is { } node
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
