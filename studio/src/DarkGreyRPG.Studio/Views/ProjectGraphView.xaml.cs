using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Views;

public partial class ProjectGraphView : UserControl
{
    private const double NodeWidth = 210;
    private const double NodeHeight = 112;
    private readonly Dictionary<ProjectGraphNodeViewModel, Border> _nodeVisuals = [];
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly TranslateTransform _translate = new();
    private ProjectGraphViewModel? _viewModel;
    private ProjectGraphNodeViewModel? _dragNode;
    private Point _pointerStart;
    private Point _nodeStart;
    private bool _panning;

    public ProjectGraphView()
    {
        InitializeComponent();
        GraphCanvas.RenderTransform = new TransformGroup { Children = [_scale, _translate] };
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => RebuildGraph();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        Detach();
        _viewModel = e.NewValue as ProjectGraphViewModel;
        if (_viewModel is not null)
        {
            _viewModel.PropertyChanged += ViewModelPropertyChanged;
            foreach (var node in _viewModel.Nodes) node.PropertyChanged += NodePropertyChanged;
        }
        RebuildGraph();
    }

    private void Detach()
    {
        if (_viewModel is null) return;
        _viewModel.PropertyChanged -= ViewModelPropertyChanged;
        foreach (var node in _viewModel.Nodes) node.PropertyChanged -= NodePropertyChanged;
    }

    private void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProjectGraphViewModel.Zoom) or nameof(ProjectGraphViewModel.PanX) or nameof(ProjectGraphViewModel.PanY)) ApplyViewport();
    }

    private void NodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(ProjectGraphNodeViewModel.X) or nameof(ProjectGraphNodeViewModel.Y) or nameof(ProjectGraphNodeViewModel.IsVisible)) RebuildGraph();
    }

    private void RebuildGraph()
    {
        if (!IsLoaded || _viewModel is null) return;
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
            Background = new SolidColorBrush(Color.FromRgb(37, 41, 47)),
            BorderBrush = node.HasWarning ? Brushes.Orange : new SolidColorBrush(Color.FromRgb(82, 88, 98)),
            BorderThickness = node.HasWarning ? new Thickness(2) : new Thickness(1), CornerRadius = new CornerRadius(6),
            ToolTip = node.WarningText.Length == 0 ? "双击打开 Story Flow" : node.WarningText,
        };
        AutomationProperties.SetName(root, $"剧情图谱节点 {node.DisplayName} {node.Id}");
        root.PreviewMouseLeftButtonDown += Node_OnPreviewMouseLeftButtonDown;

        var panel = new StackPanel();
        var header = new Border { Background = new SolidColorBrush(node.IsHomeStory ? Color.FromRgb(67, 160, 71) : Color.FromRgb(45, 125, 170)), Padding = new Thickness(10, 7, 10, 7), CornerRadius = new CornerRadius(5, 5, 0, 0) };
        header.Child = new TextBlock { Text = node.IsHomeStory ? $"★ {node.DisplayName}" : node.DisplayName, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis };
        panel.Children.Add(header);
        panel.Children.Add(new TextBlock { Text = node.Id, Margin = new Thickness(10, 7, 10, 3), Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis });
        var open = new Button { Content = "打开 Flow", Margin = new Thickness(10, 1, 10, 7), Padding = new Thickness(5, 2, 5, 2), Tag = node.Id };
        AutomationProperties.SetName(open, $"{EscapeAccessKey(node.Id)} 打开 Flow");
        open.Click += OpenFlow_OnClick;
        panel.Children.Add(open);
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
            var start = new Point(source.X + NodeWidth, source.Y + NodeHeight / 2);
            var end = new Point(target.X, target.Y + NodeHeight / 2);
            var delta = end.X - start.X;
            var bend = delta >= 0 ? Math.Max(45, delta * .42) : Math.Max(100, Math.Abs(delta) * .45);
            var geometry = new PathGeometry([new PathFigure(start, [new BezierSegment(new(start.X + bend, start.Y), new(end.X - bend, end.Y), end, true)], false)]);
            var path = new Path { Data = geometry, Stroke = new SolidColorBrush(Color.FromRgb(108, 177, 255)), StrokeThickness = 3, IsHitTestVisible = false, ToolTip = $"{edge.SourceStoryId} → {edge.TargetStoryId}" };
            Panel.SetZIndex(path, -10);
            GraphCanvas.Children.Add(path);
        }
    }

    private void Node_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel is null || sender is not Border { Tag: ProjectGraphNodeViewModel node }) return;
        if (FindAncestor<Button>(e.OriginalSource as DependencyObject) is not null) return;
        if (e.ClickCount == 2) { _viewModel.OpenStoryFlow(node.Id); e.Handled = true; return; }
        _dragNode = node;
        _pointerStart = e.GetPosition(GraphCanvas);
        _nodeStart = new Point(node.X, node.Y);
        GraphCanvas.CaptureMouse();
        e.Handled = true;
    }

    private void OpenFlow_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is not null && sender is Button { Tag: string storyId }) _viewModel.OpenStoryFlow(storyId);
        e.Handled = true;
    }

    private void GraphCanvas_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    { if (!e.Handled) GraphCanvas.Focus(); }

    private void GraphCanvas_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    { _pointerStart = e.GetPosition(CanvasViewport); _panning = true; GraphCanvas.CaptureMouse(); e.Handled = true; }

    private void GraphCanvas_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_viewModel is null) return;
        if (_dragNode is not null && e.LeftButton == MouseButtonState.Pressed && _nodeVisuals.TryGetValue(_dragNode, out var visual))
        {
            var delta = e.GetPosition(GraphCanvas) - _pointerStart;
            Canvas.SetLeft(visual, _nodeStart.X + delta.X); Canvas.SetTop(visual, _nodeStart.Y + delta.Y);
            return;
        }
        if (_panning && e.RightButton == MouseButtonState.Pressed)
        {
            var current = e.GetPosition(CanvasViewport); var delta = current - _pointerStart; _pointerStart = current;
            _viewModel.PanX += delta.X; _viewModel.PanY += delta.Y;
        }
    }

    private void GraphCanvas_OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel is not null && _dragNode is not null && _nodeVisuals.TryGetValue(_dragNode, out var visual))
            _viewModel.MoveNode(_dragNode.Id, Canvas.GetLeft(visual), Canvas.GetTop(visual));
        _dragNode = null; _panning = false; GraphCanvas.ReleaseMouseCapture();
    }

    private void GraphCanvas_OnMouseWheel(object sender, MouseWheelEventArgs e)
    { if (_viewModel is not null) _viewModel.Zoom *= e.Delta > 0 ? 1.12 : 1 / 1.12; e.Handled = true; }
    private void ZoomIn_OnClick(object sender, RoutedEventArgs e) { if (_viewModel is not null) _viewModel.Zoom *= 1.12; }
    private void ZoomOut_OnClick(object sender, RoutedEventArgs e) { if (_viewModel is not null) _viewModel.Zoom /= 1.12; }
    private void ApplyViewport()
    { if (_viewModel is not null) { _scale.ScaleX = _scale.ScaleY = _viewModel.Zoom; _translate.X = _viewModel.PanX; _translate.Y = _viewModel.PanY; } }

    private static T? FindAncestor<T>(DependencyObject? source) where T : DependencyObject
    { while (source is not null) { if (source is T match) return match; source = VisualTreeHelper.GetParent(source); } return null; }
    private static string EscapeAccessKey(string value) => value.Replace("_", "__", StringComparison.Ordinal);
}
