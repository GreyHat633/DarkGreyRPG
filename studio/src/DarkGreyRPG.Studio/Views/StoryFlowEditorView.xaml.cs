using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Automation.Peers;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Views;

public partial class StoryFlowEditorView : UserControl
{
    private readonly Dictionary<StoryFlowNodeEditorItem, Border> _nodeVisuals = [];
    private readonly List<Path> _connectionVisuals = [];
    private readonly ScaleTransform _scale = new(1, 1);
    private readonly TranslateTransform _translate = new();
    private StoryFlowEditorViewModel? _viewModel;
    private Point _pointerStart;
    private bool _panning;
    private bool _boxSelecting;
    private Rectangle? _selectionBox;
    private Dictionary<string, Point>? _dragOrigins;
    private string? _pendingFrom;
    private string? _pendingOutput;
    private StoryFlowConnectionEditorItem? _selectedConnection;

    public StoryFlowEditorView()
    {
        InitializeComponent();
        GraphCanvas.RenderTransform = new TransformGroup { Children = [_scale, _translate] };
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => RebuildGraph();
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (_viewModel is not null)
        {
            _viewModel.Nodes.CollectionChanged -= GraphCollectionChanged;
            _viewModel.Connections.CollectionChanged -= GraphCollectionChanged;
            _viewModel.PropertyChanged -= ViewModelPropertyChanged;
        }
        _viewModel = e.NewValue as StoryFlowEditorViewModel;
        if (_viewModel is not null)
        {
            _viewModel.Nodes.CollectionChanged += GraphCollectionChanged;
            _viewModel.Connections.CollectionChanged += GraphCollectionChanged;
            _viewModel.PropertyChanged += ViewModelPropertyChanged;
        }
        RebuildGraph();
    }

    private void GraphCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        _selectedConnection = null;
        RebuildGraph();
    }
    private void ViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StoryFlowEditorViewModel.Zoom) or nameof(StoryFlowEditorViewModel.PanX) or nameof(StoryFlowEditorViewModel.PanY)) ApplyViewport();
        if (e.PropertyName == nameof(StoryFlowEditorViewModel.SelectedNode)) RefreshSelectionVisuals();
    }

    private void ApplyViewport()
    {
        if (_viewModel is null) return;
        _scale.ScaleX = _scale.ScaleY = _viewModel.Zoom;
        _translate.X = _viewModel.PanX;
        _translate.Y = _viewModel.PanY;
    }

    private void RebuildGraph()
    {
        if (!IsLoaded || _viewModel is null) return;
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

    private Border CreateNodeVisual(StoryFlowNodeEditorItem node)
    {
        var root = new AccessibleBorder
        {
            Width = StoryFlowNodeEditorItem.Width,
            MinHeight = StoryFlowNodeEditorItem.Height,
            Background = new SolidColorBrush(Color.FromRgb(37, 41, 47)),
            BorderBrush = node.IsSelected ? Brushes.White : new SolidColorBrush(Color.FromRgb(82, 88, 98)),
            BorderThickness = node.IsSelected ? new Thickness(2) : new Thickness(1),
            CornerRadius = new CornerRadius(6),
            Tag = node,
            Cursor = Cursors.SizeAll,
        };
        AutomationProperties.SetName(root, $"Flow 节点 {node.TypeLabel} {node.Id}");
        root.PreviewMouseLeftButtonDown += Node_OnMouseLeftButtonDown;

        var panel = new StackPanel();
        var header = new Border { Background = (Brush)new BrushConverter().ConvertFromString(node.Accent)!, Padding = new Thickness(10, 7, 10, 7), CornerRadius = new CornerRadius(5, 5, 0, 0) };
        header.Child = new TextBlock { Text = node.TypeLabel, FontWeight = FontWeights.SemiBold, Foreground = Brushes.White };
        panel.Children.Add(header);
        var id = new TextBlock { Text = node.Id, Margin = new Thickness(10, 7, 10, 4), Foreground = Brushes.White, TextTrimming = TextTrimming.CharacterEllipsis };
        panel.Children.Add(id);
        var ports = new Grid { Margin = new Thickness(6, 2, 6, 8) };
        ports.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        ports.ColumnDefinitions.Add(new ColumnDefinition());
        ports.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var input = new Button { Content = "●", Padding = new Thickness(2), Width = 24, Height = 24, Tag = new InputPort(node.Id), ToolTip = "输入端口" };
        AutomationProperties.SetName(input, $"{EscapeAccessKey(node.Id)} 输入端口");
        input.Click += InputPort_OnClick;
        ports.Children.Add(input);
        var outputs = new StackPanel { HorizontalAlignment = HorizontalAlignment.Right };
        Grid.SetColumn(outputs, 2);
        foreach (var output in node.Outputs)
        {
            var button = new Button { Content = $"{EscapeAccessKey(output)}  ●", Padding = new Thickness(5, 2, 3, 2), Margin = new Thickness(0, 1, 0, 1), Tag = new OutputPort(node.Id, output), ToolTip = "输出端口" };
            AutomationProperties.SetName(button, $"{EscapeAccessKey(node.Id)} 输出端口 {EscapeAccessKey(output)}");
            button.Click += OutputPort_OnClick;
            outputs.Children.Add(button);
        }
        ports.Children.Add(outputs);
        panel.Children.Add(ports);
        root.Child = panel;
        return root;
    }

    private static string EscapeAccessKey(string value) => value.Replace("_", "__", StringComparison.Ordinal);

    private void NodePropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(StoryFlowNodeEditorItem.IsSelected)) RefreshSelectionVisuals();
        else RebuildGraph();
    }

    private void RefreshSelectionVisuals()
    {
        foreach (var pair in _nodeVisuals)
        {
            pair.Value.BorderBrush = pair.Key.IsSelected ? Brushes.White : new SolidColorBrush(Color.FromRgb(82, 88, 98));
            pair.Value.BorderThickness = pair.Key.IsSelected ? new Thickness(2) : new Thickness(1);
        }
    }

    private void RedrawConnections()
    {
        foreach (var path in _connectionVisuals) GraphCanvas.Children.Remove(path);
        _connectionVisuals.Clear();
        if (_viewModel is null) return;
        foreach (var connection in _viewModel.Connections)
        {
            var fromNode = _viewModel.Nodes.FirstOrDefault(node => node.Id == connection.From);
            var toNode = _viewModel.Nodes.FirstOrDefault(node => node.Id == connection.To);
            if (fromNode is null || toNode is null || !_nodeVisuals.TryGetValue(fromNode, out var fromVisual) || !_nodeVisuals.TryGetValue(toNode, out var toVisual)) continue;
            var outputs = fromNode.Outputs;
            var outputIndex = Math.Max(0, outputs.IndexOf(connection.Output));
            var start = new Point(Canvas.GetLeft(fromVisual) + fromVisual.Width, Canvas.GetTop(fromVisual) + 82 + outputIndex * 26);
            var end = new Point(Canvas.GetLeft(toVisual), Canvas.GetTop(toVisual) + 82);
            var deltaX = end.X - start.X;
            var bend = deltaX >= 0 ? Math.Max(24, deltaX * .42) : Math.Max(70, Math.Abs(deltaX) * .45);
            var geometry = new PathGeometry([new PathFigure(start, [new BezierSegment(new(start.X + bend, start.Y), new(end.X - bend, end.Y), end, true)], false)]);
            var selected = Equals(connection, _selectedConnection);
            var path = new Path { Data = geometry, Stroke = selected ? Brushes.White : new SolidColorBrush(Color.FromRgb(108, 177, 255)), StrokeThickness = selected ? 4 : 3, Tag = connection, Cursor = Cursors.Hand, ToolTip = $"{connection.From}.{connection.Output} → {connection.To}（单击选择，Delete 删除）" };
            AutomationProperties.SetName(path, $"Flow 连接 {connection.From} {connection.Output} 到 {connection.To}");
            path.MouseLeftButtonDown += Connection_OnMouseLeftButtonDown;
            Panel.SetZIndex(path, -10);
            GraphCanvas.Children.Add(path);
            _connectionVisuals.Add(path);
        }
    }

    private void Node_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel is null || sender is not Border { Tag: StoryFlowNodeEditorItem node }) return;
        if (FindAncestor<Button>(e.OriginalSource as DependencyObject) is not null) return;
        _selectedConnection = null;
        GraphCanvas.Focus();
        if ((Keyboard.Modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != 0) _viewModel.ToggleSelection(node);
        else if (!node.IsSelected) _viewModel.SelectOnly(node);
        _pointerStart = e.GetPosition(GraphCanvas);
        _dragOrigins = _viewModel.Nodes.Where(candidate => candidate.IsSelected).ToDictionary(candidate => candidate.Id, candidate => new Point(candidate.X, candidate.Y), StringComparer.Ordinal);
        GraphCanvas.CaptureMouse();
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

    private void OutputPort_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: OutputPort port }) return;
        _pendingFrom = port.NodeId; _pendingOutput = port.Output;
        if (_viewModel?.Nodes.FirstOrDefault(node => node.Id == port.NodeId) is { } node) _viewModel.SelectOnly(node);
        e.Handled = true;
    }

    private void InputPort_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel is null || sender is not Button { Tag: InputPort port } || _pendingFrom is null || _pendingOutput is null) return;
        _viewModel.Connect(_pendingFrom, _pendingOutput, port.NodeId);
        _pendingFrom = null; _pendingOutput = null;
        e.Handled = true;
    }

    private void Connection_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel is null || sender is not Path { Tag: StoryFlowConnectionEditorItem connection }) return;
        _selectedConnection = connection;
        _viewModel.SelectOnly(null);
        GraphCanvas.Focus();
        RedrawConnections();
        e.Handled = true;
    }

    private void GraphCanvas_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.Handled || _viewModel is null) return;
        GraphCanvas.Focus();
        _selectedConnection = null;
        _viewModel.SelectOnly(null);
        _pointerStart = e.GetPosition(GraphCanvas);
        _boxSelecting = true;
        _selectionBox = new Rectangle { Stroke = Brushes.DodgerBlue, StrokeThickness = 1, Fill = new SolidColorBrush(Color.FromArgb(40, 30, 144, 255)) };
        GraphCanvas.Children.Add(_selectionBox);
        Canvas.SetLeft(_selectionBox, _pointerStart.X); Canvas.SetTop(_selectionBox, _pointerStart.Y);
        GraphCanvas.CaptureMouse();
    }

    private void GraphCanvas_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    { _pointerStart = e.GetPosition(CanvasViewport); _panning = true; GraphCanvas.CaptureMouse(); e.Handled = true; }

    private void GraphCanvas_OnPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_viewModel is null || e.LeftButton == MouseButtonState.Released && e.RightButton == MouseButtonState.Released) return;
        if (_dragOrigins is not null)
        {
            var current = e.GetPosition(GraphCanvas); var delta = current - _pointerStart;
            foreach (var pair in _dragOrigins)
                if (_viewModel.Nodes.FirstOrDefault(node => node.Id == pair.Key) is { } node && _nodeVisuals.TryGetValue(node, out var visual))
                { Canvas.SetLeft(visual, pair.Value.X + delta.X); Canvas.SetTop(visual, pair.Value.Y + delta.Y); }
            RedrawConnections(); return;
        }
        if (_panning)
        {
            var current = e.GetPosition(CanvasViewport); var delta = current - _pointerStart; _pointerStart = current;
            _viewModel.PanX += delta.X; _viewModel.PanY += delta.Y; return;
        }
        if (_boxSelecting && _selectionBox is not null)
        {
            var current = e.GetPosition(GraphCanvas); var left = Math.Min(_pointerStart.X, current.X); var top = Math.Min(_pointerStart.Y, current.Y);
            Canvas.SetLeft(_selectionBox, left); Canvas.SetTop(_selectionBox, top); _selectionBox.Width = Math.Abs(current.X - _pointerStart.X); _selectionBox.Height = Math.Abs(current.Y - _pointerStart.Y);
        }
    }

    private void GraphCanvas_OnPreviewMouseUp(object sender, MouseButtonEventArgs e)
    {
        if (_viewModel is null) return;
        if (_dragOrigins is not null)
        {
            var positions = new Dictionary<string, (double X, double Y)>(StringComparer.Ordinal);
            foreach (var pair in _dragOrigins)
                if (_viewModel.Nodes.FirstOrDefault(node => node.Id == pair.Key) is { } node && _nodeVisuals.TryGetValue(node, out var visual)) positions[pair.Key] = (Canvas.GetLeft(visual), Canvas.GetTop(visual));
            _dragOrigins = null; _viewModel.MoveSelection(positions);
        }
        if (_boxSelecting && _selectionBox is not null)
        {
            var left = Canvas.GetLeft(_selectionBox); var top = Canvas.GetTop(_selectionBox);
            _viewModel.SelectInRectangle(left, top, left + _selectionBox.Width, top + _selectionBox.Height);
            GraphCanvas.Children.Remove(_selectionBox); _selectionBox = null;
        }
        _boxSelecting = false; _panning = false; GraphCanvas.ReleaseMouseCapture();
    }

    private void GraphCanvas_OnMouseWheel(object sender, MouseWheelEventArgs e)
    { if (_viewModel is not null) _viewModel.Zoom *= e.Delta > 0 ? 1.12 : 1 / 1.12; e.Handled = true; }
    private void ZoomIn_OnClick(object sender, RoutedEventArgs e) { if (_viewModel is not null) _viewModel.Zoom *= 1.12; }
    private void ZoomOut_OnClick(object sender, RoutedEventArgs e) { if (_viewModel is not null) _viewModel.Zoom /= 1.12; }

    private void GraphCanvas_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (_viewModel is null) return;
        if (e.Key == Key.Delete && _selectedConnection is not null)
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

    private sealed record OutputPort(string NodeId, string Output);
    private sealed record InputPort(string NodeId);
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
    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Custom;
}
