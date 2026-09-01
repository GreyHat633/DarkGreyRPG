using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Views;

public partial class ProjectGraphView : UserControl
{
    private const double NodeWidth = 210;
    private const double NodeHeight = 82;
    private readonly Dictionary<ProjectGraphNodeViewModel, Border> _nodeVisuals = [];
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly TranslateTransform _translate = new();
    private readonly GraphViewportController _viewportController = new();
    private readonly GraphPointerState _pointerState = new();
    private ProjectGraphViewModel? _viewModel;
    private ProjectGraphNodeViewModel? _dragNode;
    private Point _pointerStart;
    private Point _nodeStart;
    private bool _updatingViewport;
    private bool _cancelingPointerGesture;
    private Window? _hostWindow;
    private long _handledProblemFocusSequence;

    public ProjectGraphView()
    {
        InitializeComponent();
        GraphCanvas.RenderTransform = new TransformGroup { Children = [_scale, _translate] };
        DataContextChanged += OnDataContextChanged;
        Loaded += View_OnLoaded;
        Unloaded += View_OnUnloaded;
        CanvasViewport.LostMouseCapture += CanvasViewport_OnLostMouseCapture;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        CancelPointerGesture();
        Detach();
        _viewModel = e.NewValue as ProjectGraphViewModel;
        _handledProblemFocusSequence = 0;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += ViewModelPropertyChanged;
            foreach (var node in _viewModel.Nodes) node.PropertyChanged += NodePropertyChanged;
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

    private void Detach()
    {
        if (_viewModel is null) return;
        _viewModel.PropertyChanged -= ViewModelPropertyChanged;
        foreach (var node in _viewModel.Nodes) node.PropertyChanged -= NodePropertyChanged;
    }

    private void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!_updatingViewport &&
            e.PropertyName is nameof(ProjectGraphViewModel.Zoom) or nameof(ProjectGraphViewModel.PanX) or nameof(ProjectGraphViewModel.PanY))
            ApplyViewport();
        if (e.PropertyName == nameof(ProjectGraphViewModel.ProblemFocusRequest) && _viewModel?.ProblemFocusRequest is { } request)
            HandleProblemFocus(request);
    }

    private void NodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProjectGraphNodeViewModel.X) or nameof(ProjectGraphNodeViewModel.Y) or nameof(ProjectGraphNodeViewModel.IsVisible)) RebuildGraph();
    }

    private void RebuildGraph()
    {
        if (!IsLoaded || _viewModel is null) return;
        CancelPointerGesture(false);
        GraphCanvas.Children.Clear();
        _nodeVisuals.Clear();
        foreach (var node in _viewModel.Nodes.Where(node => node.IsVisible))
        {
            var visual = CreateNodeVisual(node);
            _nodeVisuals[node] = visual;
            GraphCanvas.Children.Add(visual);
            Canvas.SetLeft(visual, node.X);
            Canvas.SetTop(visual, node.Y);
        }
        DrawEdges();
        ApplyViewport();
    }

    private Border CreateNodeVisual(ProjectGraphNodeViewModel node)
    {
        var root = new AccessibleBorder
        {
            Width = NodeWidth, Height = NodeHeight, Tag = node, Cursor = Cursors.SizeAll,
            BorderThickness = node.HasWarning ? new Thickness(2) : new Thickness(1), CornerRadius = new CornerRadius(6),
            ToolTip = node.WarningText.Length == 0 ? "双击打开 Story Flow" : node.WarningText,
        };
        root.SetResourceReference(BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
        root.SetResourceReference(BorderBrushProperty,
            node.HasWarning ? "SystemFillColorCautionBrush" : "CardStrokeColorDefaultBrush");
        AutomationProperties.SetName(root, $"故事图谱节点 {node.DisplayName} {node.Id}");
        root.PreviewMouseLeftButtonDown += Node_OnPreviewMouseLeftButtonDown;

        var panel = new StackPanel();
        var header = new Border { Padding = new Thickness(10, 7, 10, 7), CornerRadius = new CornerRadius(5, 5, 0, 0) };
        header.SetResourceReference(BackgroundProperty,
            node.IsHomeStory ? "SystemFillColorSuccessBrush" : "AccentFillColorDefaultBrush");
        var headerText = new TextBlock { Text = node.IsHomeStory ? $"★ {node.DisplayName}" : node.DisplayName, FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis };
        headerText.SetResourceReference(TextBlock.ForegroundProperty, "TextOnAccentFillColorPrimaryBrush");
        header.Child = headerText;
        panel.Children.Add(header);
        var idText = new TextBlock { Text = node.Id, Margin = new Thickness(10, 7, 10, 3), TextTrimming = TextTrimming.CharacterEllipsis };
        idText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
        panel.Children.Add(idText);
        root.Child = panel;
        return root;
    }

    private void DrawEdges()
    {
        if (_viewModel is null) return;
        foreach (var edge in _viewModel.Edges)
        {
            var source = _viewModel.Nodes.FirstOrDefault(node => node.Id == edge.SourceStoryId && node.IsVisible);
            var target = _viewModel.Nodes.FirstOrDefault(node => node.Id == edge.TargetStoryId && node.IsVisible);
            if (source is null || target is null) continue;
            var shape = ProjectGraphEdgeGeometry.Create(
                new Rect(source.X, source.Y, NodeWidth, NodeHeight),
                new Rect(target.X, target.Y, NodeWidth, NodeHeight),
                edge.IsSelfLoop);
            var hitPath = new ProjectGraphAccessiblePath
            {
                Data = shape.Geometry,
                Stroke = Brushes.Transparent,
                StrokeThickness = 16,
                Tag = edge,
                ToolTip = edge.Tooltip,
                Cursor = Cursors.Hand,
                InvokeAction = () =>
                {
                    CancelPointerGesture();
                    OpenContextMenu(CreateEdgeContextMenu(edge));
                },
            };
            AutomationProperties.SetName(hitPath, $"故事图谱边 {edge.SourceStoryId} 到 {edge.TargetStoryId} 转场 {edge.Count}");
            hitPath.MouseLeftButtonDown += Edge_OnMouseLeftButtonDown;
            Panel.SetZIndex(hitPath, -8);
            GraphCanvas.Children.Add(hitPath);
            var visiblePath = new Path
            {
                Data = shape.Geometry,
                StrokeThickness = 3,
                IsHitTestVisible = false,
            };
            visiblePath.SetResourceReference(Shape.StrokeProperty, "AccentFillColorDefaultBrush");
            Panel.SetZIndex(visiblePath, -7);
            GraphCanvas.Children.Add(visiblePath);
            var arrow = new Polygon
            {
                Points = ProjectGraphEdgeGeometry.CreateArrow(shape),
                IsHitTestVisible = false,
            };
            arrow.SetResourceReference(Shape.FillProperty, "AccentFillColorDefaultBrush");
            Panel.SetZIndex(arrow, -6);
            GraphCanvas.Children.Add(arrow);
            if (edge.Count > 1)
            {
                var label = new Border
                {
                    Padding = new Thickness(5, 1, 5, 1),
                    CornerRadius = new CornerRadius(8),
                    IsHitTestVisible = false,
                };
                label.SetResourceReference(BackgroundProperty, "AccentFillColorSecondaryBrush");
                var countText = new TextBlock { Text = $"×{edge.Count}", FontSize = 11 };
                countText.SetResourceReference(TextBlock.ForegroundProperty, "TextFillColorPrimaryBrush");
                label.Child = countText;
                Canvas.SetLeft(label, shape.LabelPoint.X - 12);
                Canvas.SetTop(label, shape.LabelPoint.Y - 8);
                Panel.SetZIndex(label, -5);
                GraphCanvas.Children.Add(label);
            }
        }
    }

    private void Edge_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (sender is not Path { Tag: ProjectGraphEdgeViewModel edge }) return;
        CancelPointerGesture();
        OpenContextMenu(CreateEdgeContextMenu(edge));
        e.Handled = true;
    }

    private void Node_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel is null || sender is not Border { Tag: ProjectGraphNodeViewModel node }) return;
        if (FindAncestor<Button>(e.OriginalSource as DependencyObject) is not null) return;
        if (e.ClickCount == 2) { _viewModel.OpenStoryFlow(node.Id); e.Handled = true; return; }
        if (!_pointerState.Begin(GraphPointerMode.NodeDrag)) return;
        _dragNode = node;
        _pointerStart = e.GetPosition(GraphCanvas);
        _nodeStart = new Point(node.X, node.Y);
        CapturePointer();
        e.Handled = true;
    }

    private void GraphCanvas_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    { if (!e.Handled) GraphCanvas.Focus(); }

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
        if (FindAncestor<Path>(e.OriginalSource as DependencyObject) is { Tag: ProjectGraphEdgeViewModel edge })
            OpenContextMenu(CreateEdgeContextMenu(edge));
        else if (FindAncestor<AccessibleBorder>(e.OriginalSource as DependencyObject) is { Tag: ProjectGraphNodeViewModel node })
            OpenContextMenu(CreateNodeContextMenu(node));
        else
            OpenContextMenu(CreateCanvasContextMenu());
        e.Handled = true;
    }

    private void GraphCanvas_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_viewModel is null) return;
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
        if (_pointerState.Is(GraphPointerMode.NodeDrag) && _dragNode is not null && e.LeftButton == MouseButtonState.Pressed && _nodeVisuals.TryGetValue(_dragNode, out var visual))
        {
            var delta = e.GetPosition(GraphCanvas) - _pointerStart;
            Canvas.SetLeft(visual, _nodeStart.X + delta.X); Canvas.SetTop(visual, _nodeStart.Y + delta.Y);
            return;
        }
    }

    private void GraphCanvas_OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton == MouseButton.Left && _pointerState.Is(GraphPointerMode.NodeDrag)
            && _viewModel is not null && _dragNode is not null && _nodeVisuals.TryGetValue(_dragNode, out var visual))
        {
            var nodeId = _dragNode.Id;
            var x = Canvas.GetLeft(visual);
            var y = Canvas.GetTop(visual);
            _dragNode = null;
            _pointerState.End(GraphPointerMode.NodeDrag);
            ReleasePointerCapture();
            _viewModel.MoveNode(nodeId, x, y);
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
        foreach (var node in _nodeVisuals.Keys)
            bounds.Union(new Rect(node.X, node.Y, NodeWidth, NodeHeight));
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
        if (mode == GraphPointerMode.NodeDrag)
        {
            if (_dragNode is not null && _nodeVisuals.TryGetValue(_dragNode, out var visual))
            {
                Canvas.SetLeft(visual, _nodeStart.X);
                Canvas.SetTop(visual, _nodeStart.Y);
            }
            _dragNode = null;
        }
        else if (mode == GraphPointerMode.Pan)
        {
            CanvasViewport.ClearValue(CursorProperty);
        }
    }

    private void CapturePointer() => Mouse.Capture(CanvasViewport, CaptureMode.Element);

    private void ReleasePointerCapture()
    {
        if (ReferenceEquals(Mouse.Captured, CanvasViewport) || ReferenceEquals(Mouse.Captured, GraphCanvas)) Mouse.Capture(null);
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

    private ContextMenu CreateNodeContextMenu(ProjectGraphNodeViewModel node)
    {
        var menu = FluentContextMenuFactory.Create(GraphCanvas);
        menu.Items.Add(ActionMenuItem("打开 Story", () => _viewModel?.OpenStoryOverview(node.Id)));
        menu.Items.Add(ActionMenuItem("打开 Flow", () => _viewModel?.OpenStoryFlow(node.Id)));
        menu.Items.Add(FluentContextMenuFactory.CreateSeparator());
        menu.Items.Add(ActionMenuItem("聚焦此节点", () => FocusNode(node)));
        menu.Items.Add(ActionMenuItem("复制 Story ID", () => Clipboard.SetText(node.Id)));
        menu.Items.Add(ActionMenuItem("查看该 Story 诊断", FocusProblems, node.HasWarning));
        return menu;
    }

    private ContextMenu CreateCanvasContextMenu()
    {
        var menu = FluentContextMenuFactory.Create(GraphCanvas);
        menu.Items.Add(ActionMenuItem("自动布局", () => _viewModel?.AutoLayout()));
        menu.Items.Add(ActionMenuItem("适应全部节点", FitAll));
        menu.Items.Add(ActionMenuItem("实际大小", ActualSize));
        menu.Items.Add(ActionMenuItem("重置视图", ResetView));
        return menu;
    }

    private ContextMenu CreateEdgeContextMenu(ProjectGraphEdgeViewModel edge)
    {
        var menu = FluentContextMenuFactory.Create(GraphCanvas);
        menu.Items.Add(ActionMenuItem("查看来源 Story", () => _viewModel?.OpenStoryOverview(edge.SourceStoryId)));
        menu.Items.Add(ActionMenuItem("查看目标 Story", () => _viewModel?.OpenStoryOverview(edge.TargetStoryId)));
        menu.Items.Add(FluentContextMenuFactory.CreateSeparator());
        if (edge.Transitions.Count == 1)
        {
            var transition = edge.Transitions[0];
            menu.Items.Add(ActionMenuItem($"定位来源 EnterStory：{transition.NodeId}", () => OpenSourceTransition(edge, transition)));
        }
        else
        {
            var sources = FluentContextMenuFactory.CreateSubmenu($"查看来源 EnterStory（{edge.Count}）");
            foreach (var transition in edge.Transitions)
            {
                var captured = transition;
                sources.Items.Add(ActionMenuItem(captured.NodeId, () => OpenSourceTransition(edge, captured)));
            }
            menu.Items.Add(sources);
        }
        return menu;
    }

    private void OpenSourceTransition(ProjectGraphEdgeViewModel edge, ProjectGraphTransitionViewModel transition)
    {
        if (Window.GetWindow(this)?.DataContext is ShellViewModel shell)
            shell.OpenStoryFlowNode(edge.SourceStoryId, transition.NodeId, "target_story_id");
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

    private void FocusNode(ProjectGraphNodeViewModel node)
    {
        var center = ViewportCenter();
        _viewportController.PanX = center.X - (node.X + NodeWidth / 2) * _viewportController.Zoom;
        _viewportController.PanY = center.Y - (node.Y + NodeHeight / 2) * _viewportController.Zoom;
        CommitViewport();
        if (_nodeVisuals.TryGetValue(node, out var visual)) visual.Focus();
    }

    private void HandleProblemFocus(ProjectGraphFocusRequest request)
    {
        if (_viewModel is null || request.Sequence <= _handledProblemFocusSequence) return;
        _handledProblemFocusSequence = request.Sequence;
        RebuildGraph();
        var node = _viewModel.Nodes.FirstOrDefault(candidate => candidate.Id == request.StoryId);
        if (node is not null) FocusNode(node);
    }

    private void FitAll()
    {
        var bounds = Rect.Empty;
        foreach (var node in _nodeVisuals.Keys) bounds.Union(new Rect(node.X, node.Y, NodeWidth, NodeHeight));
        _viewportController.FitToBounds(bounds, new Size(CanvasViewport.ActualWidth, CanvasViewport.ActualHeight));
        CommitViewport();
    }

    private void ResetView()
    {
        _viewportController.ResetView();
        CommitViewport();
    }

    private void ActualSize()
    {
        _viewportController.SetZoomAt(1, ViewportCenter());
        CommitViewport();
    }

    private void Problems_OnClick(object sender, RoutedEventArgs e) => FocusProblems();

    private void FocusProblems()
    {
        if (Window.GetWindow(this)?.DataContext is ShellViewModel shell) shell.FocusProjectGraphProblems();
    }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    { while (source is not null) { if (source is T match) return match; source = VisualTreeHelper.GetParent(source); } return null; }
}
