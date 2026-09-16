using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Stories.Definitions;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Views;

public partial class StoryFlowEditorView : UserControl
{
    private static readonly string[] NodeCategoryOrder = ["触发", "条件", "对话", "任务", "动作 / 奖励", "流程控制", "故事"];
    private readonly Dictionary<StoryFlowNodeEditorItem, StoryFlowNodeControl> _nodeVisuals = [];
    private readonly List<UIElement> _connectionVisuals = [];
    private readonly HashSet<string> _expandedNodes = new(StringComparer.Ordinal);
    private readonly HashSet<string> _initializedExpansionNodes = new(StringComparer.Ordinal);
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly TranslateTransform _translate = new();
    private readonly GraphViewportController _viewportController = new();
    private readonly GraphPointerState _pointerState = new();
    private StoryFlowEditorViewModel? _viewModel;
    private Point _pointerStart;
    private Rectangle? _selectionBox;
    private Dictionary<string, Point>? _dragOrigins;
    private WireDragSession? _wireSession;
    private bool _wireHasMoved;
    private StoryFlowConnectionEditorItem? _suppressedConnection;
    private Path? _connectionDraft;
    private FlowPortControl? _hoverWireTarget;
    private StoryFlowConnectionEditorItem? _selectedConnection;
    private bool _updatingViewport;
    private bool _cancelingPointerGesture;
    private bool _connectionRedrawQueued;
    private Point _contextGraphPoint;
    private Window? _hostWindow;
    private long _handledProblemFocusSequence;

    public StoryFlowEditorView()
    {
        InitializeComponent();
        GraphCanvas.RenderTransform = new TransformGroup { Children = [_scale, _translate] };
        DataContextChanged += OnDataContextChanged;
        Loaded += View_OnLoaded;
        Unloaded += View_OnUnloaded;
        CanvasViewport.LostMouseCapture += CanvasViewport_OnLostMouseCapture;
        CanvasViewport.AddHandler(Mouse.PreviewMouseUpEvent, new MouseButtonEventHandler(GraphCanvas_OnPreviewMouseUp), true);
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        CancelPointerGesture();
        if (_viewModel is not null)
        {
            _viewModel.Nodes.CollectionChanged -= GraphCollectionChanged;
            _viewModel.Connections.CollectionChanged -= GraphCollectionChanged;
            _viewModel.PropertyChanged -= ViewModelPropertyChanged;
        }
        _viewModel = e.NewValue as StoryFlowEditorViewModel;
        _handledProblemFocusSequence = 0;
        if (_viewModel is not null)
        {
            _viewModel.Nodes.CollectionChanged += GraphCollectionChanged;
            _viewModel.Connections.CollectionChanged += GraphCollectionChanged;
            _viewModel.PropertyChanged += ViewModelPropertyChanged;
        }
        RebuildGraph();
        if (_viewModel?.ProblemFocusRequest is { } request) HandleProblemFocus(request);
    }

    private void View_OnLoaded(object sender, RoutedEventArgs e)
    {
        _hostWindow = Window.GetWindow(this);
        if (_hostWindow is not null) _hostWindow.Deactivated += HostWindow_OnDeactivated;
        RebuildGraph();
    }

    private void View_OnUnloaded(object sender, RoutedEventArgs e)
    {
        CancelPointerGesture();
        if (_hostWindow is not null) _hostWindow.Deactivated -= HostWindow_OnDeactivated;
        _hostWindow = null;
    }

    private void HostWindow_OnDeactivated(object? sender, EventArgs e) => CancelPointerGesture();

    private void CanvasViewport_OnLostMouseCapture(object sender, MouseEventArgs e)
    {
        if (!_cancelingPointerGesture && _pointerState.Mode != GraphPointerMode.Idle) CancelPointerGesture(false);
    }

    private void GraphCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (!IsLoaded || _viewModel is null) return;
        if (ReferenceEquals(sender, _viewModel.Nodes))
        {
            SyncNodeVisuals();
            RedrawConnections();
            QueueConnectionRedraw();
            return;
        }

        if (ReferenceEquals(sender, _viewModel.Connections))
        {
            if (_selectedConnection is not null && !_viewModel.Connections.Contains(_selectedConnection))
                _selectedConnection = null;
            RefreshIncomingConnections();
            RedrawConnections();
            QueueConnectionRedraw();
        }
    }
    private void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_updatingViewport &&
            e.PropertyName is nameof(StoryFlowEditorViewModel.Zoom) or nameof(StoryFlowEditorViewModel.PanX) or nameof(StoryFlowEditorViewModel.PanY))
            ApplyViewport();
        if (e.PropertyName == nameof(StoryFlowEditorViewModel.SelectedNode)) RefreshSelectionVisuals();
        if (e.PropertyName == nameof(StoryFlowEditorViewModel.ProblemFocusRequest) && _viewModel?.ProblemFocusRequest is { } request)
            HandleProblemFocus(request);
    }

    private void HandleProblemFocus(FlowProblemFocusRequest request)
    {
        if (_viewModel is null || request.Sequence <= _handledProblemFocusSequence) return;
        _handledProblemFocusSequence = request.Sequence;
        _expandedNodes.Add(request.NodeId);
        RebuildGraph();
        Dispatcher.BeginInvoke(() =>
        {
            if (_viewModel is null) return;
            var node = _viewModel.Nodes.FirstOrDefault(candidate => candidate.Id == request.NodeId);
            if (node is null || !_nodeVisuals.TryGetValue(node, out var visual)) return;
            GraphCanvas.UpdateLayout();
            var zoom = Math.Max(1, _viewModel.Zoom);
            var center = ViewportCenter();
            var width = visual.ActualWidth > 0 ? visual.ActualWidth : StoryFlowNodeEditorItem.Width;
            var height = visual.ActualHeight > 0 ? visual.ActualHeight : StoryFlowNodeEditorItem.Height;
            _viewportController.Zoom = zoom;
            _viewportController.PanX = center.X - (node.X + width / 2) * zoom;
            _viewportController.PanY = center.Y - (node.Y + height / 2) * zoom;
            CommitViewport();
            visual.FocusField(request.Field);
        }, DispatcherPriority.ContextIdle);
    }

    private void ApplyViewport()
    {
        if (_viewModel is null) return;
        _viewportController.Zoom = _viewModel.Zoom;
        _viewportController.PanX = _viewModel.PanX;
        _viewportController.PanY = _viewModel.PanY;
        _scale.ScaleX = _scale.ScaleY = _viewportController.Zoom;
        _translate.X = _viewportController.PanX;
        _translate.Y = _viewportController.PanY;
    }

    private void CommitViewport()
    {
        if (_viewModel is null) return;
        _updatingViewport = true;
        try
        {
            _viewModel.Zoom = _viewportController.Zoom;
            _viewModel.PanX = _viewportController.PanX;
            _viewModel.PanY = _viewportController.PanY;
        }
        finally
        {
            _updatingViewport = false;
        }
        ApplyViewport();
    }

    private void RebuildGraph()
    {
        if (!IsLoaded || _viewModel is null) return;
        CancelPointerGesture(false);
        foreach (var pair in _nodeVisuals) pair.Key.PropertyChanged -= NodePropertyChanged;
        GraphCanvas.Children.Clear();
        _nodeVisuals.Clear();
        _connectionVisuals.Clear();
        foreach (var node in _viewModel.Nodes)
        {
            node.PropertyChanged += NodePropertyChanged;
            var visual = CreateNodeVisual(node);
            _nodeVisuals[node] = visual;
            GraphCanvas.Children.Add(visual);
            Canvas.SetLeft(visual, node.X);
            Canvas.SetTop(visual, node.Y);
        }
        RedrawConnections();
        ApplyViewport();
    }

    private StoryFlowNodeControl CreateNodeVisual(StoryFlowNodeEditorItem node)
    {
        if (_viewModel is null) throw new InvalidOperationException("Flow view model is not available.");
        if (_initializedExpansionNodes.Add(node.Id) && node.HasCoreParameters) _expandedNodes.Add(node.Id);
        var root = new StoryFlowNodeControl(node)
        {
            IsAdvancedExpanded = _expandedNodes.Contains(node.Id),
            ResourceCandidates = _viewModel.GetResourceCandidates(node),
            ResourceLabel = StoryFlowEditorViewModel.GetResourceLabel(node),
            IncomingConnections = _viewModel.GetIncomingConnections(node.Id),
        };
        root.PreviewMouseLeftButtonDown += Node_OnMouseLeftButtonDown;
        root.InputInvoked += InputPort_OnClick;
        root.InputDragStarted += InputPort_OnDragStarted;
        root.OutputInvoked += OutputPort_OnClick;
        root.OutputDragStarted += OutputPort_OnDragStarted;
        root.IncomingConnectionDragStarted += IncomingConnection_OnDragStarted;
        root.ExpandedChanged += Node_OnExpandedChanged;
        root.AddReferenceRequested += Node_OnAddReferenceRequested;
        root.DialogueExitsChangeRequested += Node_OnDialogueExitsChangeRequested;
        root.SequenceStepAddRequested += Node_OnSequenceStepAddRequested;
        root.SequenceStepRemoveRequested += Node_OnSequenceStepRemoveRequested;
        return root;
    }

    private void Node_OnDialogueExitsChangeRequested(object? sender, DialogueExitsChangeRequestedEventArgs e)
    {
        if (_viewModel is null || sender is not StoryFlowNodeControl control) return;
        var plan = _viewModel.AnalyzeDialogueExitNames(control.Node, e.RequestedNames);
        if (!plan.IsValid)
        {
            MessageBox.Show(Window.GetWindow(this), plan.ValidationError ?? "Dialogue Exit 出口名称无效。", "无法修改出口", MessageBoxButton.OK, MessageBoxImage.Warning);
            RebuildGraph();
            return;
        }
        if (plan.IsNoOp) return;

        var disposition = DialogueExitOutputDisposition.Delete;
        if (plan.RequiresConnectedOutputDecision)
        {
            var dialogs = new FlowWorkspaceDialogs(() => Window.GetWindow(this));
            if (plan.CanMigrate && plan.RenameCandidate is { } rename)
            {
                disposition = dialogs.ConfirmConnectedDialogueExitChange(control.Node.Id, rename.OldOutput, rename.NewOutput) switch
                {
                    ConnectedDynamicOutputChoice.Migrate => DialogueExitOutputDisposition.Migrate,
                    ConnectedDynamicOutputChoice.DeleteConnection => DialogueExitOutputDisposition.Delete,
                    _ => DialogueExitOutputDisposition.Cancel,
                };
            }
            else
            {
                foreach (var output in plan.RemovedConnectedOutputNames)
                    if (dialogs.ConfirmConnectedDialogueExitChange(control.Node.Id, output, null) == ConnectedDynamicOutputChoice.Cancel)
                    {
                        disposition = DialogueExitOutputDisposition.Cancel;
                        break;
                    }
            }
        }

        if (!_viewModel.ApplyDialogueExitNameChange(plan, disposition)) RebuildGraph();
    }

    private void Node_OnSequenceStepAddRequested(object? sender, EventArgs e)
    {
        if (_viewModel is not null && sender is StoryFlowNodeControl control)
            _viewModel.AddSequenceStep(control.Node);
    }

    private void Node_OnSequenceStepRemoveRequested(object? sender, EventArgs e)
    {
        if (_viewModel is null || sender is not StoryFlowNodeControl control) return;
        var plan = _viewModel.AnalyzeSequenceStepRemoval(control.Node);
        if (!plan.IsValid) return;
        var allowConnectedRemoval = !plan.IsConnected ||
            new FlowWorkspaceDialogs(() => Window.GetWindow(this)).ConfirmRemoveConnectedSequenceStep(control.Node.Id, plan.Output);
        if (allowConnectedRemoval) _viewModel.RemoveSequenceStep(plan, allowConnectedRemoval);
    }

    private void NodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StoryFlowNodeEditorItem.IsSelected)) RefreshSelectionVisuals();
        else if (sender is StoryFlowNodeEditorItem node && _nodeVisuals.TryGetValue(node, out var visual))
        {
            if (e.PropertyName is nameof(StoryFlowNodeEditorItem.X) or nameof(StoryFlowNodeEditorItem.Y))
            {
                Canvas.SetLeft(visual, node.X);
                Canvas.SetTop(visual, node.Y);
                GraphCanvas.UpdateLayout();
                RedrawConnections();
            }
            else if (e.PropertyName == nameof(StoryFlowNodeEditorItem.Outputs))
            {
                GraphCanvas.UpdateLayout();
                RedrawConnections();
                QueueConnectionRedraw();
            }
        }
    }

    private void QueueConnectionRedraw()
    {
        if (_connectionRedrawQueued || !IsLoaded) return;
        _connectionRedrawQueued = true;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            _connectionRedrawQueued = false;
            if (!IsLoaded) return;
            GraphCanvas.UpdateLayout();
            RedrawConnections();
        }));
    }

    private void SyncNodeVisuals()
    {
        if (!IsLoaded || _viewModel is null) return;
        var current = _viewModel.Nodes.ToHashSet();
        foreach (var pair in _nodeVisuals.Where(pair => !current.Contains(pair.Key)).ToArray())
        {
            pair.Key.PropertyChanged -= NodePropertyChanged;
            GraphCanvas.Children.Remove(pair.Value);
            _nodeVisuals.Remove(pair.Key);
        }

        foreach (var node in _viewModel.Nodes)
        {
            if (!_nodeVisuals.TryGetValue(node, out var visual))
            {
                node.PropertyChanged += NodePropertyChanged;
                visual = CreateNodeVisual(node);
                _nodeVisuals[node] = visual;
                GraphCanvas.Children.Add(visual);
            }
            Canvas.SetLeft(visual, node.X);
            Canvas.SetTop(visual, node.Y);
        }
    }

    private void RefreshIncomingConnections()
    {
        if (_viewModel is null) return;
        foreach (var node in _viewModel.Nodes)
            if (_nodeVisuals.TryGetValue(node, out var visual))
                visual.IncomingConnections = _viewModel.GetIncomingConnections(node.Id);
    }

    private void RefreshSelectionVisuals()
    {
        foreach (var visual in _nodeVisuals.Values) visual.InvalidateVisual();
    }

    private void RedrawConnections()
    {
        foreach (var path in _connectionVisuals) GraphCanvas.Children.Remove(path);
        _connectionVisuals.Clear();
        if (_viewModel is null) return;
        GraphCanvas.UpdateLayout();
        foreach (var connection in _viewModel.Connections)
        {
            if (Equals(connection, _suppressedConnection)) continue;
            var fromNode = _viewModel.Nodes.FirstOrDefault(node => node.Id == connection.From);
            var toNode = _viewModel.Nodes.FirstOrDefault(node => node.Id == connection.To);
            if (fromNode is null || toNode is null || !_nodeVisuals.TryGetValue(fromNode, out var fromVisual) || !_nodeVisuals.TryGetValue(toNode, out var toVisual)) continue;
            var outputPort = fromVisual.FindOutput(connection.Output);
            if (outputPort is null) continue;
            var start = outputPort.GetAnchorPoint(GraphCanvas);
            var end = toVisual.FindInput(connection).GetAnchorPoint(GraphCanvas);
            var deltaX = end.X - start.X;
            var bend = deltaX >= 0 ? Math.Max(24, deltaX * .42) : Math.Max(70, Math.Abs(deltaX) * .45);
            var geometry = new PathGeometry([new PathFigure(start, [new BezierSegment(new(start.X + bend, start.Y), new(end.X - bend, end.Y), end, true)], false)]);
            var selected = Equals(connection, _selectedConnection);
            // Persisted Story Flow connections are legacy flow edges; request
            // the shared style explicitly so the generic wire language remains
            // separate from the legacy resource schema.
            var style = GraphConnectionVisualStyle.For(GraphInterfaceKind.Flow, selected);
            var visualPath = new Path
            {
                Data = geometry,
                Stroke = new SolidColorBrush(style.StrokeColor),
                StrokeThickness = style.StrokeThickness,
                IsHitTestVisible = false,
            };
            var hitPath = new Path
            {
                Data = geometry,
                Stroke = Brushes.Transparent,
                StrokeThickness = 14,
                Tag = connection,
                Cursor = Cursors.Hand,
                ToolTip = $"{connection.From}.{connection.Output} → {connection.To}（单击选择，Delete 删除）",
            };
            AutomationProperties.SetName(hitPath, $"{style.AutomationLabel} {connection.From} {connection.Output} 到 {connection.To}");
            hitPath.MouseLeftButtonDown += Connection_OnMouseLeftButtonDown;
            Panel.SetZIndex(visualPath, -10);
            Panel.SetZIndex(hitPath, -9);
            GraphCanvas.Children.Add(visualPath);
            GraphCanvas.Children.Add(hitPath);
            _connectionVisuals.Add(visualPath);
            _connectionVisuals.Add(hitPath);
        }
    }

    private void Node_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel is null || sender is not StoryFlowNodeControl { Tag: StoryFlowNodeEditorItem node }) return;
        var source = e.OriginalSource as DependencyObject;
        if (FindAncestor<ButtonBase>(source) is not null || FindAncestor<ComboBox>(source) is not null || FindAncestor<TextBoxBase>(source) is not null) return;
        if (!_pointerState.Begin(GraphPointerMode.NodeDrag)) return;
        _selectedConnection = null;
        GraphCanvas.Focus();
        if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != 0) _viewModel.ToggleSelection(node);
        else if (!node.IsSelected) _viewModel.SelectOnly(node);
        _pointerStart = e.GetPosition(GraphCanvas);
        _dragOrigins = _viewModel.Nodes.Where(candidate => candidate.IsSelected).ToDictionary(candidate => candidate.Id, candidate => new Point(candidate.X, candidate.Y), StringComparer.Ordinal);
        CapturePointer();
        e.Handled = true;
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match) return match;
            source = VisualTreeHelper.GetParent(source);
        }
        return null;
    }

    private void OutputPort_OnClick(object? sender, FlowPortInvokedEventArgs e)
    {
        if (_viewModel?.Nodes.FirstOrDefault(node => node.Id == e.NodeId) is { } node) _viewModel.SelectOnly(node);
    }

    private void InputPort_OnClick(object? sender, FlowPortInvokedEventArgs e)
    {
        if (_viewModel?.Nodes.FirstOrDefault(node => node.Id == e.NodeId) is { } node) _viewModel.SelectOnly(node);
    }

    private void OutputPort_OnDragStarted(object? sender, FlowPortInvokedEventArgs e)
    {
        if (_viewModel is null) return;
        var original = _viewModel.GetConnection(e.NodeId, e.PortName);
        if (original is not null && TryGetConnectionPorts(original, out _, out var inputPort))
            BeginWireDrag(new WireDragSession(e.NodeId, e.PortName, false, e.Port,
                original.To, "input", true, inputPort, WireDragKind.ReconnectTarget, original));
        else
            BeginWireDrag(new WireDragSession(e.NodeId, e.PortName, false, e.Port,
                e.NodeId, e.PortName, false, e.Port, WireDragKind.New, original));
    }

    private void InputPort_OnDragStarted(object? sender, FlowPortInvokedEventArgs e)
    {
        if (_viewModel is null) return;
        var incoming = _viewModel.GetIncomingConnections(e.NodeId);
        var original = incoming.Count == 1 ? incoming[0] : null;
        if (original is not null && TryGetConnectionPorts(original, out var outputPort, out _))
            BeginWireDrag(new WireDragSession(e.NodeId, e.PortName, true, e.Port,
                original.From, original.Output, false, outputPort, WireDragKind.ReconnectSource, original));
        else
            BeginWireDrag(new WireDragSession(e.NodeId, e.PortName, true, e.Port,
                e.NodeId, e.PortName, true, e.Port, WireDragKind.New, original));
    }

    private void IncomingConnection_OnDragStarted(object? sender, IncomingConnectionDragEventArgs e)
    {
        if (TryGetConnectionPorts(e.Connection, out var outputPort, out _))
            BeginWireDrag(new WireDragSession(e.Connection.To, "input", true, e.InputPort,
                e.Connection.From, e.Connection.Output, false, outputPort,
                WireDragKind.ReconnectSource, e.Connection));
    }

    private bool TryGetConnectionPorts(StoryFlowConnectionEditorItem connection,
        out FlowPortControl outputPort, out FlowPortControl inputPort)
    {
        outputPort = null!;
        inputPort = null!;
        if (_viewModel is null) return false;
        var fromNode = _viewModel.Nodes.FirstOrDefault(node => node.Id == connection.From);
        var toNode = _viewModel.Nodes.FirstOrDefault(node => node.Id == connection.To);
        if (fromNode is null || toNode is null || !_nodeVisuals.TryGetValue(fromNode, out var fromVisual)
            || !_nodeVisuals.TryGetValue(toNode, out var toVisual)) return false;
        outputPort = fromVisual.FindOutput(connection.Output)!;
        inputPort = toVisual.FindInput(connection);
        return outputPort is not null;
    }

    private void BeginWireDrag(WireDragSession session)
    {
        if (_viewModel is null) return;
        CancelPointerGesture(false);
        if (!_pointerState.Begin(GraphPointerMode.WireDrag)) return;
        _wireSession = session;
        _wireHasMoved = false;
        session.DraggedPort.IsConnecting = true;
        if (_viewModel.Nodes.FirstOrDefault(node => node.Id == session.DraggedNodeId) is { } node) _viewModel.SelectOnly(node);
        GraphCanvas.UpdateLayout();
        var fixedAnchor = session.FixedPort.GetAnchorPoint(GraphCanvas);
        var draftGeometry = session.OriginalConnection is not null && TryGetConnectionPorts(session.OriginalConnection, out var originalOutput, out var originalInput)
            ? CreateConnectionGeometry(originalOutput.GetAnchorPoint(GraphCanvas), originalInput.GetAnchorPoint(GraphCanvas))
            : CreateConnectionGeometry(fixedAnchor, fixedAnchor);
        var draftStyle = GraphConnectionVisualStyle.For(session.FixedPort.InterfaceKind, selected: true);
        _connectionDraft = new Path
        {
            Data = draftGeometry,
            Stroke = new SolidColorBrush(draftStyle.StrokeColor),
            StrokeThickness = draftStyle.StrokeThickness,
            IsHitTestVisible = false,
        };
        Panel.SetZIndex(_connectionDraft, -5);
        GraphCanvas.Children.Add(_connectionDraft);
        // Install an equivalent draft before suppressing the persisted line. This
        // keeps the original connection visible through the mouse-down transition.
        _suppressedConnection = session.OriginalConnection;
        RedrawConnections();
        GraphCanvas.Focus();
        _pointerStart = Mouse.GetPosition(CanvasViewport);
        CapturePointer();
    }

    private void Node_OnExpandedChanged(object? sender, EventArgs e)
    {
        if (sender is not StoryFlowNodeControl control) return;
        if (control.IsAdvancedExpanded) _expandedNodes.Add(control.Node.Id);
        else _expandedNodes.Remove(control.Node.Id);
        GraphCanvas.UpdateLayout();
        RedrawConnections();
    }

    private void Node_OnAddReferenceRequested(object? sender, EventArgs e)
    {
        if (_viewModel is null || sender is not StoryFlowNodeControl control) return;
        if (_viewModel.AddResourceAsReference(control.Node)) RebuildGraph();
    }

    private void Connection_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel is null || sender is not FrameworkElement { Tag: StoryFlowConnectionEditorItem connection }) return;
        _selectedConnection = connection;
        _viewModel.SelectOnly(null);
        GraphCanvas.Focus();
        RedrawConnections();
        e.Handled = true;
    }

    private void GraphCanvas_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled || _viewModel is null) return;
        var source = e.OriginalSource as DependencyObject;
        if (FindAncestor<StoryFlowNodeControl>(source) is not null || FindAncestor<FlowPortControl>(source) is not null
            || FindAncestor<Path>(source)?.Tag is StoryFlowConnectionEditorItem) return;
        if (!_pointerState.Begin(GraphPointerMode.BoxSelect)) return;
        GraphCanvas.Focus();
        _selectedConnection = null;
        _viewModel.SelectOnly(null);
        _pointerStart = e.GetPosition(GraphCanvas);
        _selectionBox = new Rectangle { Stroke = Brushes.DodgerBlue, StrokeThickness = 1, Fill = new SolidColorBrush(Color.FromArgb(40, 30, 144, 255)) };
        GraphCanvas.Children.Add(_selectionBox);
        Canvas.SetLeft(_selectionBox, _pointerStart.X); Canvas.SetTop(_selectionBox, _pointerStart.Y);
        CapturePointer();
    }

    private void GraphCanvas_OnPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Middle || _viewModel is null) return;
        var displaced = _pointerState.PreemptForPan();
        CancelGesturePayload(displaced);
        _pointerStart = e.GetPosition(CanvasViewport);
        CanvasViewport.Cursor = Cursors.ScrollAll;
        CapturePointer();
        e.Handled = true;
    }

    private void GraphCanvas_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel is null) return;
        CancelPointerGesture();
        _contextGraphPoint = _viewportController.ScreenToGraph(e.GetPosition(CanvasViewport));
        var source = e.OriginalSource as DependencyObject;
        if (FindAncestor<Path>(source) is { Tag: StoryFlowConnectionEditorItem connection })
        {
            _selectedConnection = connection;
            _viewModel.SelectOnly(null);
            RedrawConnections();
            OpenContextMenu(CreateConnectionContextMenu(connection));
        }
        else if (FindAncestor<StoryFlowNodeControl>(source) is { Node: { } node } control)
        {
            _selectedConnection = null;
            if (!node.IsSelected) _viewModel.SelectOnly(node);
            OpenContextMenu(CreateNodeContextMenu(node, control));
        }
        else
        {
            _selectedConnection = null;
            OpenContextMenu(CreateCanvasContextMenu());
        }
        e.Handled = true;
    }

    private void GraphCanvas_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_viewModel is null) return;
        if (_pointerState.Is(GraphPointerMode.WireDrag) && _connectionDraft is not null && _wireSession is { } wireSession)
        {
            var current = e.GetPosition(GraphCanvas);
            var screenPoint = e.GetPosition(CanvasViewport);
            _wireHasMoved |= Math.Abs(screenPoint.X - _pointerStart.X) >= SystemParameters.MinimumHorizontalDragDistance
                             || Math.Abs(screenPoint.Y - _pointerStart.Y) >= SystemParameters.MinimumVerticalDragDistance;
            UpdateConnectionTarget(current);
            var fixedAnchor = wireSession.FixedPort.GetAnchorPoint(GraphCanvas);
            _connectionDraft.Data = wireSession.FixedIsInput
                ? CreateConnectionGeometry(current, fixedAnchor)
                : CreateConnectionGeometry(fixedAnchor, current);
            return;
        }
        if (_pointerState.Is(GraphPointerMode.Pan))
        {
            if (e.MiddleButton == MouseButtonState.Released)
            {
                EndPan();
                return;
            }
            var current = e.GetPosition(CanvasViewport);
            var delta = current - _pointerStart;
            _pointerStart = current;
            _viewportController.PanBy(delta);
            CommitViewport();
            return;
        }
        if (e.LeftButton == MouseButtonState.Released) return;
        if (_pointerState.Is(GraphPointerMode.NodeDrag) && _dragOrigins is not null)
        {
            var current = e.GetPosition(GraphCanvas); var delta = current - _pointerStart;
            foreach (var pair in _dragOrigins)
                if (_viewModel.Nodes.FirstOrDefault(node => node.Id == pair.Key) is { } node && _nodeVisuals.TryGetValue(node, out var visual))
                { Canvas.SetLeft(visual, pair.Value.X + delta.X); Canvas.SetTop(visual, pair.Value.Y + delta.Y); }
            RedrawConnections(); return;
        }
        if (_pointerState.Is(GraphPointerMode.BoxSelect) && _selectionBox is not null)
        {
            var current = e.GetPosition(GraphCanvas); var left = Math.Min(_pointerStart.X, current.X); var top = Math.Min(_pointerStart.Y, current.Y);
            Canvas.SetLeft(_selectionBox, left); Canvas.SetTop(_selectionBox, top); _selectionBox.Width = Math.Abs(current.X - _pointerStart.X); _selectionBox.Height = Math.Abs(current.Y - _pointerStart.Y);
        }
    }

    private void GraphCanvas_OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel is null) return;
        if (e.ChangedButton == MouseButton.Left && _pointerState.Is(GraphPointerMode.WireDrag) && _connectionDraft is not null)
        {
            var session = _wireSession;
            var moved = _wireHasMoved;
            var target = FindWireTarget(e.GetPosition(GraphCanvas));
            FinishConnectionDraft();
            if (session is not null)
            {
                if (target is not null)
                {
                    var from = session.FixedIsInput ? target.NodeId : session.FixedNodeId;
                    var output = session.FixedIsInput ? target.EffectivePortId : session.FixedPort.EffectivePortId;
                    var to = session.FixedIsInput ? session.FixedNodeId : target.NodeId;
                    if (session.OriginalConnection is null) _viewModel.Connect(from, output, to);
                    else _viewModel.ReconnectConnection(session.OriginalConnection, from, output, to);
                }
                else if (moved && session.OriginalConnection is not null)
                {
                    _viewModel.RemoveConnection(session.OriginalConnection);
                }
            }
            e.Handled = true;
            return;
        }
        if (e.ChangedButton == MouseButton.Left && _pointerState.Is(GraphPointerMode.NodeDrag) && _dragOrigins is not null)
        {
            var positions = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);
            foreach (var pair in _dragOrigins)
                if (_viewModel.Nodes.FirstOrDefault(node => node.Id == pair.Key) is { } node && _nodeVisuals.TryGetValue(node, out var visual))
                {
                    var x = Canvas.GetLeft(visual);
                    var y = Canvas.GetTop(visual);
                    positions[pair.Key] = (double.IsFinite(x) ? x : node.X, double.IsFinite(y) ? y : node.Y);
                }
            _dragOrigins = null;
            _pointerState.End(GraphPointerMode.NodeDrag);
            ReleasePointerCapture();
            _viewModel.MoveSelection(positions);
        }
        if (e.ChangedButton == MouseButton.Left && _pointerState.Is(GraphPointerMode.BoxSelect) && _selectionBox is not null)
        {
            var left = Canvas.GetLeft(_selectionBox); var top = Canvas.GetTop(_selectionBox);
            _viewModel.SelectInRectangle(left, top, left + _selectionBox.Width, top + _selectionBox.Height);
            GraphCanvas.Children.Remove(_selectionBox); _selectionBox = null;
            _pointerState.End(GraphPointerMode.BoxSelect);
            ReleasePointerCapture();
        }
        if (e.ChangedButton == MouseButton.Middle) EndPan();
    }

    private void GraphCanvas_OnMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (_viewModel is null) return;
        _viewportController.ZoomAtCursor(e.GetPosition(CanvasViewport), e.Delta > 0 ? GraphViewportController.DefaultZoomFactor : 1 / GraphViewportController.DefaultZoomFactor);
        CommitViewport();
        e.Handled = true;
    }

    private void ZoomIn_OnClick(object sender, RoutedEventArgs e) => ZoomAtViewportCenter(GraphViewportController.DefaultZoomFactor);
    private void ZoomOut_OnClick(object sender, RoutedEventArgs e) => ZoomAtViewportCenter(1 / GraphViewportController.DefaultZoomFactor);
    private void Zoom100_OnClick(object sender, RoutedEventArgs e)
    {
        _viewportController.SetZoomAt(1, ViewportCenter());
        CommitViewport();
    }

    private void ResetView_OnClick(object sender, RoutedEventArgs e)
    {
        _viewportController.ResetView();
        CommitViewport();
    }

    private void FitAll_OnClick(object sender, RoutedEventArgs e)
    {
        var bounds = Rect.Empty;
        foreach (var pair in _nodeVisuals)
        {
            var visual = pair.Value;
            var width = visual.ActualWidth > 0 ? visual.ActualWidth : visual.Width;
            var height = visual.ActualHeight > 0 ? visual.ActualHeight : visual.MinHeight;
            bounds.Union(new Rect(pair.Key.X, pair.Key.Y, width, height));
        }
        _viewportController.FitToBounds(bounds, new Size(CanvasViewport.ActualWidth, CanvasViewport.ActualHeight));
        CommitViewport();
    }

    private void ZoomAtViewportCenter(double factor)
    {
        _viewportController.ZoomAt(ViewportCenter(), factor);
        CommitViewport();
    }

    private Point ViewportCenter() => new(CanvasViewport.ActualWidth / 2, CanvasViewport.ActualHeight / 2);

    private void EndPan()
    {
        if (!_pointerState.End(GraphPointerMode.Pan)) return;
        CanvasViewport.ClearValue(CursorProperty);
        ReleasePointerCapture();
    }

    private void GraphCanvas_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel is null) return;
        if (e.Key == Key.Escape && _pointerState.Mode != GraphPointerMode.Idle)
        {
            CancelPointerGesture();
            e.Handled = true;
        }
        else if (e.Key == Key.Delete && _selectedConnection is not null)
        {
            var connection = _selectedConnection;
            _selectedConnection = null;
            _viewModel.RemoveConnection(connection);
            e.Handled = true;
        }
        else if (e.Key == Key.Delete) { _viewModel.DeleteSelectionCommand.Execute(null); e.Handled = true; }
        else if (e.Key == Key.C && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { _viewModel.CopyCommand.Execute(null); e.Handled = true; }
        else if (e.Key == Key.V && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { _viewModel.PasteCommand.Execute(null); e.Handled = true; }
        else if (e.Key == Key.Z && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { _viewModel.UndoCommand.Execute(null); e.Handled = true; }
        else if (e.Key == Key.Y && Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) { _viewModel.RedoCommand.Execute(null); e.Handled = true; }
    }

    private void UpdateConnectionTarget(Point graphPoint)
    {
        var target = FindWireTarget(graphPoint);
        if (ReferenceEquals(target, _hoverWireTarget)) return;
        if (_hoverWireTarget is not null) _hoverWireTarget.IsValidTarget = false;
        _hoverWireTarget = target;
        if (_hoverWireTarget is not null) _hoverWireTarget.IsValidTarget = true;
    }

    private FlowPortControl? FindWireTarget(Point graphPoint)
    {
        if (_viewModel is null || _wireSession is not { } session) return null;
        var hit = GraphCanvas.InputHitTest(graphPoint) as DependencyObject;
        var port = FindAncestor<FlowPortControl>(hit);
        if (port is null) return null;
        // Reject incompatible endpoints before invoking the legacy view-model
        // validator. The validator still owns Story cardinality and all other
        // document-level rules.
        if (!session.FixedPort.IsCompatibleEndpoint(port))
            return null;
        var from = session.FixedIsInput ? port.NodeId : session.FixedNodeId;
        var output = session.FixedIsInput ? port.EffectivePortId : session.FixedPort.EffectivePortId;
        var to = session.FixedIsInput ? session.FixedNodeId : port.NodeId;
        return _viewModel.ValidateConnection(from, output, to) ? port : null;
    }

    private void FinishConnectionDraft(bool releaseCapture = true)
    {
        ClearConnectionDraft();
        _pointerState.End(GraphPointerMode.WireDrag);
        if (releaseCapture) ReleasePointerCapture();
    }

    private void CancelPointerGesture(bool releaseCapture = true)
    {
        if (_cancelingPointerGesture) return;
        _cancelingPointerGesture = true;
        try
        {
            CancelGesturePayload(_pointerState.Cancel());
            if (releaseCapture) ReleasePointerCapture();
        }
        finally
        {
            _cancelingPointerGesture = false;
        }
    }

    private void CancelGesturePayload(GraphPointerMode mode)
    {
        switch (mode)
        {
            case GraphPointerMode.NodeDrag:
                if (_dragOrigins is not null)
                {
                    foreach (var pair in _dragOrigins)
                        if (_viewModel?.Nodes.FirstOrDefault(node => node.Id == pair.Key) is { } node
                            && _nodeVisuals.TryGetValue(node, out var visual))
                        {
                            Canvas.SetLeft(visual, pair.Value.X);
                            Canvas.SetTop(visual, pair.Value.Y);
                        }
                }
                _dragOrigins = null;
                RedrawConnections();
                break;
            case GraphPointerMode.BoxSelect:
                if (_selectionBox is not null) GraphCanvas.Children.Remove(_selectionBox);
                _selectionBox = null;
                break;
            case GraphPointerMode.WireDrag:
                ClearConnectionDraft();
                break;
            case GraphPointerMode.Pan:
                CanvasViewport.ClearValue(CursorProperty);
                break;
        }
    }

    private void ClearConnectionDraft()
    {
        if (_hoverWireTarget is not null) _hoverWireTarget.IsValidTarget = false;
        if (_wireSession is not null) _wireSession.DraggedPort.IsConnecting = false;
        if (_connectionDraft is not null) GraphCanvas.Children.Remove(_connectionDraft);
        _hoverWireTarget = null;
        _connectionDraft = null;
        _wireSession = null;
        _wireHasMoved = false;
        _suppressedConnection = null;
        RedrawConnections();
    }

    private void CapturePointer() => Mouse.Capture(CanvasViewport, CaptureMode.Element);

    private void ReleasePointerCapture()
    {
        if (ReferenceEquals(Mouse.Captured, CanvasViewport) || ReferenceEquals(Mouse.Captured, GraphCanvas)) Mouse.Capture(null);
    }

    private static PathGeometry CreateConnectionGeometry(Point start, Point end)
    {
        var deltaX = end.X - start.X;
        var bend = deltaX >= 0 ? Math.Max(24, deltaX * .42) : Math.Max(70, Math.Abs(deltaX) * .45);
        return new PathGeometry([new PathFigure(start, [new BezierSegment(new(start.X + bend, start.Y), new(end.X - bend, end.Y), end, true)], false)]);
    }

    private ContextMenu CreateCanvasContextMenu()
    {
        var menu = FluentContextMenuFactory.Create(GraphCanvas);
        var add = FluentContextMenuFactory.CreateSubmenu("添加");
        foreach (var category in NodeCategoryOrder)
        {
            var definitions = StoryNodeDefinitionRegistry.Definitions
                .Where(definition => string.Equals(definition.Category, category, StringComparison.Ordinal))
                .ToArray();
            if (definitions.Length > 0) add.Items.Add(CreateNodeGroup(category, definitions));
        }
        if (StoryNodeDefinitionRegistry.Get("End") is { } endDefinition)
            add.Items.Add(ActionMenuItem(endDefinition.DisplayName, () => _viewModel?.AddNodeAt(endDefinition.CanonicalType, _contextGraphPoint.X, _contextGraphPoint.Y)));
        menu.Items.Add(add);
        menu.Items.Add(FluentContextMenuFactory.CreateSeparator());
        menu.Items.Add(ActionMenuItem("粘贴", () => _viewModel?.PasteCommand.Execute(null), _viewModel?.PasteCommand.CanExecute(null) == true));
        menu.Items.Add(ActionMenuItem("全选", () => _viewModel?.SelectAll(), _viewModel?.Nodes.Count > 0));
        menu.Items.Add(FluentContextMenuFactory.CreateSeparator());
        menu.Items.Add(ActionMenuItem("适应全部节点", FitAll));
        menu.Items.Add(ActionMenuItem("重置视图", ResetView));
        return menu;
    }

    private MenuItem CreateNodeGroup(string header, IReadOnlyList<StoryNodeDefinition> definitions)
    {
        var group = FluentContextMenuFactory.CreateSubmenu(header);
        foreach (var definition in definitions)
        {
            var canonicalType = definition.CanonicalType;
            group.Items.Add(ActionMenuItem(definition.DisplayName, () => _viewModel?.AddNodeAt(canonicalType, _contextGraphPoint.X, _contextGraphPoint.Y)));
        }
        return group;
    }

    private ContextMenu CreateNodeContextMenu(StoryFlowNodeEditorItem node, StoryFlowNodeControl control)
    {
        var menu = FluentContextMenuFactory.Create(GraphCanvas);
        menu.Items.Add(ActionMenuItem(control.IsAdvancedExpanded ? "折叠参数" : "展开参数", () => control.IsAdvancedExpanded = !control.IsAdvancedExpanded));
        menu.Items.Add(FluentContextMenuFactory.CreateSeparator());
        menu.Items.Add(ActionMenuItem("复制", () => _viewModel?.CopyCommand.Execute(null), _viewModel?.CopyCommand.CanExecute(null) == true));
        menu.Items.Add(ActionMenuItem("创建副本", () => _viewModel?.DuplicateSelection()));
        menu.Items.Add(ActionMenuItem("断开所有连接", () => _viewModel?.DisconnectNode(node.Id), _viewModel?.Connections.Any(connection => connection.From == node.Id || connection.To == node.Id) == true));
        menu.Items.Add(FluentContextMenuFactory.CreateSeparator());
        menu.Items.Add(FluentContextMenuFactory.CreateItem("删除节点", () => _viewModel?.DeleteSelectionCommand.Execute(null), critical: true));
        return menu;
    }

    private ContextMenu CreateConnectionContextMenu(StoryFlowConnectionEditorItem connection)
    {
        var menu = FluentContextMenuFactory.Create(GraphCanvas);
        menu.Items.Add(FluentContextMenuFactory.CreateItem("删除连接", () => _viewModel?.RemoveConnection(connection), critical: true));
        return menu;
    }

    private static MenuItem ActionMenuItem(string header, Action action, bool enabled = true)
    {
        return FluentContextMenuFactory.CreateItem(header, action, enabled, critical: header.StartsWith("删除", StringComparison.Ordinal));
    }

    private void OpenContextMenu(ContextMenu menu)
    {
        GraphCanvas.ContextMenu = menu;
        menu.PlacementTarget = GraphCanvas;
        menu.Placement = PlacementMode.MousePoint;
        menu.IsOpen = true;
    }

    private void FitAll()
    {
        var bounds = Rect.Empty;
        foreach (var pair in _nodeVisuals)
        {
            var visual = pair.Value;
            var width = visual.ActualWidth > 0 ? visual.ActualWidth : visual.Width;
            var height = visual.ActualHeight > 0 ? visual.ActualHeight : StoryFlowNodeEditorItem.Height;
            bounds.Union(new Rect(pair.Key.X, pair.Key.Y, width, height));
        }
        _viewportController.FitToBounds(bounds, new Size(CanvasViewport.ActualWidth, CanvasViewport.ActualHeight));
        CommitViewport();
    }

    private void ResetView()
    {
        _viewportController.ResetView();
        CommitViewport();
    }

    private void Problems_OnClick(object sender, RoutedEventArgs e)
    {
        if (Window.GetWindow(this)?.DataContext is ShellViewModel shell) shell.FocusCurrentFlowProblems();
    }

    private enum WireDragKind
    {
        New,
        ReconnectSource,
        ReconnectTarget,
    }

    private sealed record WireDragSession(
        string DraggedNodeId,
        string DraggedPortName,
        bool DraggedIsInput,
        FlowPortControl DraggedPort,
        string FixedNodeId,
        string FixedPortName,
        bool FixedIsInput,
        FlowPortControl FixedPort,
        WireDragKind Kind,
        StoryFlowConnectionEditorItem? OriginalConnection);

}

internal static class StoryFlowListExtensions
{
    public static int IndexOf(this IReadOnlyList<string> values, string value)
    { for (var index = 0; index < values.Count; index++) if (values[index] == value) return index; return -1; }
}

internal sealed class AccessibleBorder : Border
{
    protected override AutomationPeer OnCreateAutomationPeer() => new StoryFlowElementAutomationPeer(this);
}

internal sealed class StoryFlowElementAutomationPeer(FrameworkElement owner) : FrameworkElementAutomationPeer(owner)
{
    protected override string GetClassNameCore() => Owner.GetType().Name;
    protected override string GetAutomationIdCore()
    {
        var configuredId = AutomationProperties.GetAutomationId(Owner);
        return string.IsNullOrWhiteSpace(configuredId) ? base.GetAutomationIdCore() : configuredId;
    }
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;
    protected override bool IsControlElementCore() => true;
    protected override bool IsContentElementCore() => true;
}
