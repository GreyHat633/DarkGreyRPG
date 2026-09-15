using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DarkGreyRPG.Studio.Core.Media;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Views.Graph;

/// <summary>Resize directions exposed for deterministic geometry tests.</summary>
public enum ScreenResizeHandle
{
    TopLeft, Top, TopRight, Right, BottomRight, Bottom, BottomLeft, Left
}

/// <summary>Normalized screen-layer geometry used by the eight resize handles.</summary>
public readonly record struct ScreenLayerGeometry(
    double X, double Y, double Width, double Height, double AnchorX, double AnchorY)
{
    public Rect CanvasRect(double canvasWidth = 320, double canvasHeight = 180)
    {
        var width = Width * canvasWidth;
        var height = Height * canvasHeight;
        return new Rect(X * canvasWidth - AnchorX * width, Y * canvasHeight - AnchorY * height, width, height);
    }
}

/// <summary>Transient normalized composition editor. Each drag commits one history unit.</summary>
public sealed class SessionScreenEditor : UserControl
{
    private const double CanvasWidth = 320;
    private const double CanvasHeight = 180;
    private const double MinimumLayerSize = 0.001;
    private const double MaximumLayerSize = 4;
    private const int MaximumLayers = 32;

    public static readonly DependencyProperty ProjectDirectoryProperty =
        DependencyProperty.Register(nameof(ProjectDirectory), typeof(string), typeof(SessionScreenEditor),
            new PropertyMetadata(null, (owner, _) => ((SessionScreenEditor)owner).Refresh()));

    public string? ProjectDirectory { get => (string?)GetValue(ProjectDirectoryProperty); set => SetValue(ProjectDirectoryProperty, value); }

    private CanonicalNodeInspectorViewModel? _inspector;
    private ScreenLayerEditorState? _state;
    private List<string> _names = [];
    private readonly TextBox _name = new() { Margin = new Thickness(2), Tag = "name" };
    private readonly TextBlock _cardTitle = new() { FontWeight = FontWeights.SemiBold, TextTrimming = TextTrimming.CharacterEllipsis, Margin = new Thickness(0, 0, 0, 8) };
    private readonly Button _cardHeader = new() { Tag = "screen-layer-properties-header", HorizontalContentAlignment = HorizontalAlignment.Stretch, Padding = new Thickness(0), Background = Brushes.Transparent, BorderThickness = new Thickness(0) };
    private readonly StackPanel _cardContent = new();
    private readonly Dictionary<TextBox, string> _projectedText = new();
    private readonly List<System.Windows.Shapes.Line> _guides = [];
    private JsonArray _layers = [];
    private readonly ListBox _list = new() { Height = 142, MinHeight = 40, MaxHeight = 142 };
    private readonly Canvas _canvas = new()
    {
        Width = CanvasWidth, Height = CanvasHeight,
        Background = new SolidColorBrush(Color.FromRgb(28, 30, 35)), ClipToBounds = true, Focusable = true
    };
    private readonly Dictionary<string, TextBox> _fields = new();
    private readonly Dictionary<string, BitmapSource> _images = new();
    private readonly HashSet<string> _loading = new();
    private readonly CheckBox _dialogue = new() { Content = "对话框", IsChecked = true, Margin = new Thickness(4, 0, 4, 0) };
    private readonly CheckBox _choice = new() { Content = "选项框", Margin = new Thickness(4, 0, 4, 0) };
    private readonly TextBlock _error = new() { Foreground = Brushes.Tomato, TextWrapping = TextWrapping.Wrap };
    private bool _projecting;
    private bool _dragging;
    private bool _cardExpanded;
    private long _generation;
    private JsonArray? _gestureBefore;
    private bool _listPointerDown;
    private Point _listPointerOrigin;
    private int _listPointerIndex = -1;
    private int _listReorderIndex = -1;

    public SessionScreenEditor()
    {
        SetResourceReference(ForegroundProperty, "TextFillColorPrimaryBrush");
        _error.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCriticalBrush");
        Focusable = true;
        System.Windows.Automation.AutomationProperties.SetName(_list, "画面图片列表");
        System.Windows.Automation.AutomationProperties.SetName(_name, "图片名称");
        System.Windows.Automation.AutomationProperties.SetName(_cardHeader, "图片属性");
        PreviewKeyDown += OnPreviewKeyDown;
        var panel = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        panel.Children.Add(new TextBlock { Text = "完整画面（空列表清除画面）" });
        var buttons = new UniformGrid { Columns = 2, Margin = new Thickness(0, 2, 0, 4) };
        var add = new Button { Content = "添加图片", HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0, 0, 3, 0) };
        var remove = new Button { Content = "移除图片", HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(3, 0, 0, 0) };
        add.Click += Import;
        remove.Click += (_, _) => RemoveSelected();
        buttons.Children.Add(add);
        buttons.Children.Add(remove);
        panel.Children.Add(buttons);

        _list.ItemTemplate = CreateLayerTemplate();
        _list.HorizontalContentAlignment = HorizontalAlignment.Stretch;
        _list.SetValue(ScrollViewer.HorizontalScrollBarVisibilityProperty, ScrollBarVisibility.Disabled);
        _list.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        _list.SelectionChanged += (_, _) =>
        {
            if (_projecting) return;
            _state?.Select(_list.SelectedIndex);
            ProjectSelection();
        };
        _list.PreviewMouseLeftButtonDown += ListMouseDown;
        _list.PreviewMouseMove += ListMouseMove;
        _list.PreviewMouseLeftButtonUp += ListMouseUp;
        _list.LostMouseCapture += (_, _) =>
        {
            if (_listReorderIndex >= 0) CancelGesture();
            _listPointerDown = false;
            _listReorderIndex = -1;
        };
        panel.Children.Add(_list);
        var ordering = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 4, 0, 0) };
        void AddOrderButton(string glyph, string label, int delta)
        {
            var button = new Button { Content = glyph, ToolTip = label, MinWidth = 32, Padding = new Thickness(8, 3, 8, 3), Margin = new Thickness(4, 0, 0, 0) };
            System.Windows.Automation.AutomationProperties.SetName(button, label);
            button.Click += (_, _) => Move(delta);
            ordering.Children.Add(button);
        }
        AddOrderButton("▲", "上移", -1);
        AddOrderButton("▼", "下移", 1);
        panel.Children.Add(ordering);


        panel.Children.Add(new Viewbox
        {
            Stretch = Stretch.Uniform, Child = _canvas, MaxHeight = 190,
            Margin = new Thickness(0, 6, 0, 6)
        });
        var overlays = new WrapPanel { HorizontalAlignment = HorizontalAlignment.Left };
        overlays.Children.Add(_dialogue);
        overlays.Children.Add(_choice);
        panel.Children.Add(overlays);
        _dialogue.Checked += (_, _) => Draw(); _dialogue.Unchecked += (_, _) => Draw();
        _choice.Checked += (_, _) => Draw(); _choice.Unchecked += (_, _) => Draw();

        var card = new Border { BorderThickness = new Thickness(1), BorderBrush = new SolidColorBrush(Color.FromRgb(114, 114, 114)),
            CornerRadius = new CornerRadius(4), Padding = new Thickness(8), Margin = new Thickness(0, 8, 0, 0) };
        card.SetResourceReference(BackgroundProperty, "CardBackgroundFillColorDefaultBrush");
        var properties = new StackPanel(); card.Child = properties;
        _cardHeader.Content = _cardTitle;
        _cardHeader.Click += (_, _) => SetCardExpanded(!_cardExpanded);
        properties.Children.Add(_cardHeader);
        var grid = new UniformGrid { Columns = 2 };
        grid.Children.Add(new TextBlock { Text = "名称", VerticalAlignment = VerticalAlignment.Center });
        grid.Children.Add(_name);
        _name.LostKeyboardFocus += (_, _) => RenameSelected();
        _name.PreviewKeyDown += (_, args) => { if (args.Key == Key.Enter) RenameSelected(); };
        foreach (var pair in new[]
        {
            ("z", "图层"),
            ("x", "位置 X"), ("y", "位置 Y"), ("width", "宽度"), ("height", "高度")
        })
        {
            grid.Children.Add(new TextBlock { Text = pair.Item2, VerticalAlignment = VerticalAlignment.Center });
            var box = new TextBox { Margin = new Thickness(2), MinWidth = 55, Tag = pair.Item1 };
            System.Windows.Automation.AutomationProperties.SetName(box, pair.Item2);
            box.GotKeyboardFocus += (_, _) =>
            {
                if (Selected is { } selected) box.Text = EditorNumber(selected, pair.Item1).ToString("R", CultureInfo.InvariantCulture);
                _projectedText[box] = box.Text;
            };
            NumericDrag.SetStep(box, pair.Item1 == "z" ? 1 : 0.01);
            if (pair.Item1 is "width" or "height")
            { NumericDrag.SetMinimum(box, MinimumLayerSize); NumericDrag.SetMaximum(box, MaximumLayerSize); }
            box.LostKeyboardFocus += FieldChanged;
            box.PreviewKeyDown += (sender, args) =>
            {
                if (args.Key == Key.Enter || args.Key == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                    FieldChanged(sender, null!);
            };
            NumericDrag.AddPreviewedHandler(box, NumericPreviewed);
            NumericDrag.AddCommittedHandler(box, NumericCommitted);
            NumericDrag.AddCanceledHandler(box, NumericCanceled);
            _fields.Add(pair.Item1, box);
            grid.Children.Add(box);
        }
        _cardContent.Children.Add(grid);
        var help = new TextBlock { Text = "位置 X/Y 表示图片左上角；位置和尺寸以画面宽高为单位，1 表示整个画面。图层越大越靠前。拖动时自动对齐边缘和中心，按 Alt 暂停吸附。", Margin = new Thickness(0, 6, 0, 0) };
        AuthoringText.SetIsHelp(help, true); _cardContent.Children.Add(help);
        properties.Children.Add(_cardContent);
        SetCardExpanded(false);
        panel.Children.Add(card); panel.Children.Add(_error); Content = panel;

        DataContextChanged += (_, _) =>
        {
            var nextInspector = DataContext as CanonicalNodeInspectorViewModel;
            var differentNode = _inspector is null || nextInspector is null
                || !ReferenceEquals(_inspector.Host, nextInspector.Host) || _inspector.NodeId != nextInspector.NodeId;
            if (_inspector is not null) _inspector.PropertyChanged -= Changed;
            if (_state is not null) _state.Changed -= SharedStateChanged;
            _inspector = nextInspector;
            if (_inspector is not null) _inspector.PropertyChanged += Changed;
            _state = _inspector is null ? null : ScreenLayerEditorState.For(_inspector, ProjectDirectory);
            if (_state is not null) _state.Changed += SharedStateChanged;
            if (differentNode) SetCardExpanded(false);
            _generation++; _images.Clear(); _loading.Clear(); CancelGesture(); Refresh();
        };
    }

    private void UpdateCardTitle() => _cardTitle.Text = (_cardExpanded ? "▾ " : "▸ ")
        + (Selected is null ? "图片属性（未选择图片）" : $"图片属性 · {_names.ElementAtOrDefault(_list.SelectedIndex)}");

    private void SetCardExpanded(bool expanded)
    {
        _cardExpanded = expanded;
        UpdateCardTitle();
        _cardContent.Visibility = expanded ? Visibility.Visible : Visibility.Collapsed;
        _cardHeader.SetValue(System.Windows.Automation.AutomationProperties.HelpTextProperty, expanded ? "收起图片属性" : "展开图片属性");
    }

    /// <summary>Calculates a resize while keeping the opposite edge or corner fixed.</summary>
    public static ScreenLayerGeometry ResizeGeometry(
        ScreenLayerGeometry original, ScreenResizeHandle handle, double deltaX, double deltaY,
        double canvasWidth = CanvasWidth, double canvasHeight = CanvasHeight)
    {
        if (!double.IsFinite(deltaX) || !double.IsFinite(deltaY)) return original;
        var rect = original.CanvasRect(canvasWidth, canvasHeight);
        var left = rect.Left; var top = rect.Top; var right = rect.Right; var bottom = rect.Bottom;
        var minWidth = MinimumLayerSize * canvasWidth; var minHeight = MinimumLayerSize * canvasHeight;
        var maxWidth = MaximumLayerSize * canvasWidth; var maxHeight = MaximumLayerSize * canvasHeight;
        var affectsLeft = handle is ScreenResizeHandle.TopLeft or ScreenResizeHandle.Left or ScreenResizeHandle.BottomLeft;
        var affectsRight = handle is ScreenResizeHandle.TopRight or ScreenResizeHandle.Right or ScreenResizeHandle.BottomRight;
        var affectsTop = handle is ScreenResizeHandle.TopLeft or ScreenResizeHandle.Top or ScreenResizeHandle.TopRight;
        var affectsBottom = handle is ScreenResizeHandle.BottomLeft or ScreenResizeHandle.Bottom or ScreenResizeHandle.BottomRight;
        if (affectsLeft) left = Math.Clamp(left + deltaX, right - maxWidth, right - minWidth);
        if (affectsRight) right = Math.Clamp(right + deltaX, left + minWidth, left + maxWidth);
        if (affectsTop) top = Math.Clamp(top + deltaY, bottom - maxHeight, bottom - minHeight);
        if (affectsBottom) bottom = Math.Clamp(bottom + deltaY, top + minHeight, top + maxHeight);
        var width = Math.Max(minWidth, right - left); var height = Math.Max(minHeight, bottom - top);
        return original with
        {
            X = Math.Clamp((left + original.AnchorX * width) / canvasWidth, -2, 3),
            Y = Math.Clamp((top + original.AnchorY * height) / canvasHeight, -2, 3),
            Width = Math.Clamp(width / canvasWidth, MinimumLayerSize, MaximumLayerSize),
            Height = Math.Clamp(height / canvasHeight, MinimumLayerSize, MaximumLayerSize)
        };
    }

    /// <summary>Reference rectangles matching the canonical 320x180 game UI projection.</summary>
    public static Rect DialogueReferenceRect => new(16, 128, 288, 48);
    public static Rect ChoiceReferenceRect => new(16, 14, 288, 68);
    /// <summary>Individual choice rows matching the game's wide button geometry.</summary>
    public static IReadOnlyList<Rect> ChoiceReferenceOptionRects =>
    [new(16, 14, 288, 20), new(16, 38, 288, 20), new(16, 62, 288, 20)];

    private static DataTemplate CreateLayerTemplate()
    {
        var root = new FrameworkElementFactory(typeof(DockPanel));
        var image = new FrameworkElementFactory(typeof(Image));
        image.SetValue(FrameworkElement.WidthProperty, 34d); image.SetValue(FrameworkElement.HeightProperty, 24d);
        image.SetValue(Image.StretchProperty, Stretch.UniformToFill); image.SetValue(FrameworkElement.MarginProperty, new Thickness(2, 1, 5, 1));
        image.SetBinding(Image.SourceProperty, new Binding(nameof(ScreenLayerItem.Thumbnail))); root.AppendChild(image);
        var label = new FrameworkElementFactory(typeof(TextBlock));
        label.SetValue(TextBlock.TextTrimmingProperty, TextTrimming.CharacterEllipsis);
        label.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
        label.SetBinding(TextBlock.TextProperty, new Binding(nameof(ScreenLayerItem.Label))); root.AppendChild(label);
        return new DataTemplate { VisualTree = root };
    }

    private void Changed(object? sender, PropertyChangedEventArgs args)
    {
        if (args.PropertyName == nameof(CanonicalNodeInspectorViewModel.ScreenLayers) && !_dragging) Refresh();
    }

    private void Refresh(bool readModel = true)
    {
        if (_dragging && readModel) return;
        if (_inspector is not null) ScreenLayerEditorState.For(_inspector, ProjectDirectory);
        if (readModel) _layers = _inspector?.IsScreen == true
            ? JsonNode.Parse(_inspector.ScreenLayers.GetRawText())!.AsArray() : [];
        if (readModel) _names = _state?.Names(_layers, ProjectDirectory).ToList() ?? [];
        RenderList(); ProjectSelection();
    }

    private void SharedStateChanged(object? sender, EventArgs args)
    {
        if (!_dragging) Refresh();
    }

    private void RenderList()
    {
        var selected = _state?.Selection ?? _list.SelectedIndex;
        _projecting = true;
        _list.ItemsSource = _layers.Select((layer, index) => new ScreenLayerItem(
            index, $"{(_names.Count > index ? _names[index] : $"图片 {index + 1}")} · 图层 {layer?["z"]}",
            layer is JsonObject item && item["media_ref"] is JsonValue mediaValue && mediaValue.TryGetValue<string>(out var media)
                && _images.TryGetValue(media, out var bitmap) ? bitmap : null)).ToArray();
        _list.SelectedIndex = _layers.Count == 0 ? -1 : Math.Clamp(selected, 0, _layers.Count - 1);
        _projecting = false;
    }

    private JsonObject? Selected => _list.SelectedIndex >= 0 && _list.SelectedIndex < _layers.Count
        ? _layers[_list.SelectedIndex]?.AsObject() : null;

    private void ProjectSelection(bool redraw = true)
    {
        UpdateCardTitle();
        _name.IsEnabled = Selected is not null;
        _name.Text = _names.ElementAtOrDefault(_list.SelectedIndex) ?? "";
        foreach (var pair in _fields)
        {
            pair.Value.IsEnabled = Selected is not null;
            if (pair.Value.IsKeyboardFocused) continue;
            pair.Value.Text = Selected is { } layer ? EditorNumber(layer, pair.Key).ToString("0.##", CultureInfo.InvariantCulture) : "";
            if (Selected is { } positionLayer && pair.Key is "x" or "y")
            {
                var offset = pair.Key == "x" ? Number(positionLayer, "anchor_x") * Number(positionLayer, "width")
                    : Number(positionLayer, "anchor_y") * Number(positionLayer, "height");
                NumericDrag.SetMinimum(pair.Value, -2 - offset); NumericDrag.SetMaximum(pair.Value, 3 - offset);
            }
            _projectedText[pair.Value] = pair.Value.Text;
        }
        if (redraw && !_dragging) Draw();
    }

    private void RenameSelected()
    {
        var index = _list.SelectedIndex;
        if (_projecting || Selected is null || _state is null || _inspector is null || index >= _names.Count) return;
        var text = _name.Text.Trim();
        if (text.Length == 0) { _error.Text = "请输入图片名称。"; return; }
        if (_names[index] == text) return;
        var state = _state; var layers = CloneLayers(_layers);
        var before = _names.ToArray(); var after = _names.ToArray(); after[index] = text;
        _inspector.Host.EditMetadata(() => state.Store(layers, before, true), () => state.Store(layers, after, true));
        _error.Text = "";
    }

    private void FieldChanged(object sender, KeyboardFocusChangedEventArgs args)
    {
        if (_projecting || Selected is not { } layer || sender is not TextBox { Tag: string key } box) return;
        if (_projectedText.TryGetValue(box, out var projected) && box.Text == projected) { ProjectSelection(); return; }
        if (!double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
          { _error.Text = "请输入有限数值。"; return; }
        if (EditorNumber(layer, key) == value) return;
        SetEditorNumber(layer, key, value); Commit();
    }

    private void NumericPreviewed(object sender, RoutedEventArgs args)
    {
        if (_projecting || Selected is not { } layer || sender is not TextBox { Tag: string key }
            || args is not NumericDrag.PreviewedEventArgs preview
            || !double.TryParse(preview.ValueText, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || !double.IsFinite(value)) return;
        // Keep the preview in the editor's transient copy. The model receives one
        // command when NumericDrag raises Committed, so the whole drag is one undo unit.
        BeginGesture(focus: false);
        SetEditorNumber(layer, key, value);
        Draw();
    }

    private void NumericCommitted(object sender, RoutedEventArgs args)
    {
        if (args is NumericDrag.CommittedEventArgs && _gestureBefore is not null) CompleteGesture();
    }

    private void NumericCanceled(object sender, RoutedEventArgs args)
    {
        if (_gestureBefore is not null) CancelGesture();
    }

    private void Commit()
    {
        if (_inspector is not null && _state is not null)
        {
            var before = JsonNode.Parse(_inspector.ScreenLayers.GetRawText())!.AsArray();
            _state.Store(before, _state.Names(before, ProjectDirectory));
            _state.Store(_layers, _names.ToArray());
        }
        if (_inspector?.SetScreenLayers(JsonSerializer.SerializeToElement(_layers)) != true)
        { _error.Text = "画面参数无效：请检查位置、尺寸和整数图层。"; Refresh(); }
        else { _error.Text = ""; Refresh(); }
    }

    private void RemoveSelected()
    {
        var index = _list.SelectedIndex; if (index < 0) return;
        _layers.RemoveAt(index); _names.RemoveAt(index); NormalizeLayerOrder(); Commit();
    }

    private void Move(int delta)
    {
        var index = _list.SelectedIndex; var target = index + delta;
        if (index < 0 || target < 0 || target >= _layers.Count) return;
        MoveLayer(index, target);
    }

    private void MoveLayer(int index, int target, bool commit = true)
    {
        if (index == target || index < 0 || target < 0 || index >= _layers.Count || target >= _layers.Count) return;
        var node = _layers[index]; _layers.RemoveAt(index); _layers.Insert(target, node); NormalizeLayerOrder();
        var name = _names[index]; _names.RemoveAt(index); _names.Insert(target, name);
        _dragging = true; _state?.Select(target);
        _projecting = true; _list.SelectedIndex = target; _projecting = false;
        Refresh(false);
        if (!commit) Draw();
        if (commit) { _dragging = false; Commit(); }
    }

    private void NormalizeLayerOrder()
    {
        for (var index = 0; index < _layers.Count; index++) _layers[index]!["z"] = index;
    }

    private void ListMouseDown(object sender, MouseButtonEventArgs args)
    {
        var index = ItemIndexAt(args.GetPosition(_list)); if (index < 0) return;
        Keyboard.ClearFocus();
        // Select before taking capture: captured ListBox input never reaches ListBoxItem.
        _list.SelectedIndex = index;
        _listPointerDown = true; _listPointerOrigin = args.GetPosition(_list); _listPointerIndex = index; _list.CaptureMouse();
        args.Handled = true;
    }

    private void ListMouseMove(object sender, MouseEventArgs args)
    {
        if (!_listPointerDown || args.LeftButton != MouseButtonState.Pressed) return;
        var point = args.GetPosition(_list);
        if (_listReorderIndex < 0 && Math.Abs(point.X - _listPointerOrigin.X) < SystemParameters.MinimumHorizontalDragDistance
            && Math.Abs(point.Y - _listPointerOrigin.Y) < SystemParameters.MinimumVerticalDragDistance) return;
        if (_listReorderIndex < 0) { _listReorderIndex = _listPointerIndex; BeginGesture(); }
        var insertion = InsertionIndexAt(point);
        var target = insertion > _listReorderIndex ? insertion - 1 : insertion;
        if (target >= 0 && target < _layers.Count && target != _listReorderIndex)
        { MoveLayer(_listReorderIndex, target, false); _listReorderIndex = target; }
    }

    private void ListMouseUp(object sender, MouseButtonEventArgs args)
    {
        if (!_listPointerDown) return;
        _listPointerDown = false;
        var reordering = _listReorderIndex >= 0;
        _listReorderIndex = -1;
        if (_list.IsMouseCaptured) _list.ReleaseMouseCapture();
        if (reordering) CompleteGesture();
        _listPointerIndex = -1;
    }

    private int ItemIndexAt(Point point)
    {
        var element = _list.InputHitTest(point) as DependencyObject;
        var container = ItemsControl.ContainerFromElement(_list, element) as ListBoxItem;
        return container is null ? -1 : _list.ItemContainerGenerator.IndexFromContainer(container);
    }

    private int InsertionIndexAt(Point point)
    {
        for (var index = 0; index < _list.Items.Count; index++)
        {
            if (_list.ItemContainerGenerator.ContainerFromIndex(index) is not ListBoxItem item) continue;
            var top = item.TranslatePoint(new Point(0, 0), _list).Y;
            if (point.Y < top + item.ActualHeight / 2) return index;
        }
        return _list.Items.Count;
    }

    private void BeginGesture(bool focus = true)
    {
        // Focus transfer may commit the previous text field. Keep its projection
        // from rebuilding the canvas and destroying the Thumb holding capture.
        _dragging = true;
        if (focus) Focus();
        if (_gestureBefore is null) _gestureBefore = CloneLayers(_layers);
    }

    private void CompleteGesture()
    {
        var before = _gestureBefore; _gestureBefore = null; _dragging = false;
        if (before is not null && !JsonNode.DeepEquals(before, _layers)) Commit(); else Refresh(false);
    }

    private void CancelGesture()
    {
        if (_gestureBefore is null) return;
        _layers = CloneLayers(_gestureBefore); _gestureBefore = null; _dragging = false;
        _names = _state?.Names(_layers, ProjectDirectory).ToList() ?? _names;
        _listPointerDown = false; _listReorderIndex = -1;
        if (_list.IsMouseCaptured) _list.ReleaseMouseCapture();
        Refresh(false);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs args)
    {
        // NumericDrag owns Escape while its mouse gesture is captured; letting
        // the event reach the TextBox raises NumericDrag.Canceled for rollback.
        if (args.Key == Key.Escape && args.OriginalSource is TextBox box && NumericDrag.HasActiveGesture(box)) return;
        if (args.Key == Key.Escape && _gestureBefore is not null) { CancelGesture(); args.Handled = true; }
    }

    private static JsonArray CloneLayers(JsonArray source) => JsonNode.Parse(source.ToJsonString())!.AsArray();

    private async void Import(object sender, RoutedEventArgs args)
    {
        if (_inspector?.IsScreen != true || ProjectDirectory is not { } root || _layers.Count >= MaximumLayers) return;
        var owner = _inspector;
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "图片|*.png;*.jpg;*.jpeg", CheckFileExists = true };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        SetCurrentValue(IsEnabledProperty, false);
        try
        {
            var tools = Path.Combine(AppContext.BaseDirectory, "media-tools", "ffmpeg");
            var media = await new ProjectMediaStore(root, Path.Combine(tools, "ffmpeg.exe"), Path.Combine(tools, "ffprobe.exe"))
                .ImportImageAsync(dialog.FileName);
            if (_inspector != owner) return;
            _layers.Add(new JsonObject
            {
                ["media_ref"] = media.MediaRef, ["x"] = 0.25, ["y"] = 0.125, ["width"] = 0.5,
                ["height"] = 0.75, ["anchor_x"] = 0, ["anchor_y"] = 0, ["z"] = _layers.Count
            });
            _names.Add(Path.GetFileNameWithoutExtension(dialog.FileName));
            Commit(); _list.SelectedIndex = _layers.Count - 1;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Win32Exception)
        { _error.Text = exception.Message; }
        finally { SetCurrentValue(IsEnabledProperty, true); }
    }

    private void Draw()
    {
        _canvas.Children.Clear();
        _guides.Clear();
        foreach (var entry in _layers.Select((node, index) => (node: node!.AsObject(), index)).OrderBy(item => Number(item.node, "z")))
        {
            var layer = entry.node;
            if (layer["media_ref"] is not JsonValue mediaValue || !mediaValue.TryGetValue<string>(out var media)) continue;
            var geometry = ReadGeometry(layer); var rect = geometry.CanvasRect();
            var border = new Border
            {
                Width = rect.Width, Height = rect.Height,
                BorderBrush = entry.index == _list.SelectedIndex ? Brushes.DeepSkyBlue : Brushes.Gray,
                BorderThickness = new Thickness(1), Background = Brushes.DimGray
            };
            if (_images.TryGetValue(media, out var bitmap)) border.Child = new Image { Source = bitmap, Stretch = Stretch.Fill }; else LoadImage(media);
            Place(border, rect.Left, rect.Top);

            var thumb = new Thumb
            {
                Width = rect.Width, Height = rect.Height, Opacity = 0.01, Cursor = Cursors.SizeAll,
                Tag = "screen-layer-move"
            };
            var handles = new Dictionary<ScreenResizeHandle, Thumb>();
            if (entry.index == _list.SelectedIndex)
                foreach (var handle in Enum.GetValues<ScreenResizeHandle>())
                {
                    var resize = new Thumb { Width = 12, Height = 12, Background = Brushes.DeepSkyBlue, Cursor = CursorFor(handle) };
                    handles.Add(handle, resize);
                    // Thumb releases capture before raising DragCompleted on a
                    // normal mouse-up. Its Canceled flag owns cancellation.
                    var total = new Vector();
                    resize.DragStarted += (_, _) => { BeginGesture(); _list.SelectedIndex = entry.index; total = new Vector(); };
                    resize.DragDelta += (_, delta) =>
                    {
                        if (!_dragging) return;
                        total += new Vector(delta.HorizontalChange, delta.VerticalChange);
                        var next = ResizeGeometry(geometry, handle, total.X, total.Y); WriteGeometry(layer, next);
                        UpdateGeometry(border, thumb, handles, next);
                        ProjectSelection(false);
                    };
                    resize.DragCompleted += (_, completed) => { if (completed.Canceled) CancelGesture(); else CompleteGesture(); };
                }

            var moveX = 0d; var moveY = 0d;
            thumb.DragStarted += (_, _) => { BeginGesture(); _list.SelectedIndex = entry.index; moveX = moveY = 0; };
            thumb.DragDelta += (_, delta) =>
            {
                if (!_dragging) return;
                moveX += delta.HorizontalChange; moveY += delta.VerticalChange;
                var moved = geometry with { X = Math.Clamp(geometry.X + moveX / CanvasWidth, -2, 3), Y = Math.Clamp(geometry.Y + moveY / CanvasHeight, -2, 3) };
                var snapped = SnapGeometry(moved, _layers.Where((_, index) => index != entry.index)
                    .Select(other => ReadGeometry(other!.AsObject())), !Keyboard.Modifiers.HasFlag(ModifierKeys.Alt));
                WriteGeometry(layer, snapped.Geometry);
                DrawGuides(snapped.GuideX, snapped.GuideY);
                UpdateGeometry(border, thumb, handles, ReadGeometry(layer));
                ProjectSelection(false);
            };
            thumb.DragCompleted += (_, completed) => { if (completed.Canceled) CancelGesture(); else CompleteGesture(); };
            Place(thumb, rect.Left, rect.Top); UpdateGeometry(border, thumb, handles, geometry);
            foreach (var handle in handles) Place(handle.Value, HandleLeft(geometry, handle.Key), HandleTop(geometry, handle.Key));
        }
        if (_dialogue.IsChecked == true) DrawDialogueReference();
        if (_choice.IsChecked == true) DrawChoiceReference();
    }

    public readonly record struct SnapResult(ScreenLayerGeometry Geometry, double? GuideX, double? GuideY);

    /// <summary>Snap edges/centres within four preview pixels; preserve exact aligned geometry.</summary>
    public static SnapResult SnapGeometry(ScreenLayerGeometry geometry, IEnumerable<ScreenLayerGeometry> others, bool enabled = true)
    {
        if (!enabled) return new(geometry, null, null);
        var xTargets = new List<double> { 0, CanvasWidth / 2, CanvasWidth };
        var yTargets = new List<double> { 0, CanvasHeight / 2, CanvasHeight };
        foreach (var other in others)
        {
            var rect = other.CanvasRect();
            xTargets.AddRange([rect.Left, rect.Left + rect.Width / 2, rect.Right]);
            yTargets.AddRange([rect.Top, rect.Top + rect.Height / 2, rect.Bottom]);
        }
        static (double delta, double? guide) Closest(double[] sources, List<double> targets)
        {
            var best = 4.01; var delta = 0d; double? guide = null;
            foreach (var target in targets)
                foreach (var source in sources)
                    if (Math.Abs(target - source) < best) { best = Math.Abs(target - source); delta = target - source; guide = target; }
            return (delta, guide);
        }
        var current = geometry.CanvasRect();
        var x = Closest([current.Left, current.Left + current.Width / 2, current.Right], xTargets);
        var y = Closest([current.Top, current.Top + current.Height / 2, current.Bottom], yTargets);
        return new(geometry with { X = Math.Clamp(geometry.X + x.delta / CanvasWidth, -2, 3),
            Y = Math.Clamp(geometry.Y + y.delta / CanvasHeight, -2, 3) }, x.guide, y.guide);
    }

    private void DrawGuides(double? x, double? y)
    {
        foreach (var line in _guides) _canvas.Children.Remove(line);
        _guides.Clear();
        void Guide(double x1, double y1, double x2, double y2)
        {
            var line = new System.Windows.Shapes.Line { X1 = x1, Y1 = y1, X2 = x2, Y2 = y2,
                Stroke = Brushes.DeepSkyBlue, StrokeThickness = 1, StrokeDashArray = [3, 2], IsHitTestVisible = false };
            Panel.SetZIndex(line, int.MaxValue); _canvas.Children.Add(line); _guides.Add(line);
        }
        if (x is { } vertical) Guide(vertical, 0, vertical, CanvasHeight);
        if (y is { } horizontal) Guide(0, horizontal, CanvasWidth, horizontal);
    }

    private void DrawDialogueReference()
    {
        var dialogue = DialogueReferenceRect;
        var content = new Grid();
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(32) });
        content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });

        var portrait = new Border
        {
            Width = 32, Height = 32, VerticalAlignment = VerticalAlignment.Center,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)), BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.FromRgb(0x30, 0x30, 0x30)),
            Child = new TextBlock { Text = "头像", Foreground = Brushes.Gainsboro, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center }
        };
        Grid.SetColumn(portrait, 0); content.Children.Add(portrait);

        var text = new StackPanel { Margin = new Thickness(8, 5, 4, 4) };
        var speaker = new TextBlock { Text = "角色" };
        speaker.Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
        var line = new TextBlock { Text = "「这里显示对话文本。」", TextWrapping = TextWrapping.Wrap };
        line.Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
        text.Children.Add(speaker); text.Children.Add(line);
        Grid.SetColumn(text, 1); content.Children.Add(text);

        Place(new Border
        {
            Width = dialogue.Width, Height = dialogue.Height, IsHitTestVisible = false,
            BorderBrush = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)), BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.FromArgb(0xCC, 0x16, 0x16, 0x16)), Child = content
        }, dialogue.Left, dialogue.Top);
    }

    private void DrawChoiceReference()
    {
        var labels = new[] { "选项一", "选项二", "选项三" };
        var rows = ChoiceReferenceOptionRects;
        for (var index = 0; index < rows.Count; index++)
        {
            var row = rows[index];
            var label = new TextBlock
            {
                Text = labels[index], Margin = new Thickness(8, 2, 8, 2),
                HorizontalAlignment = HorizontalAlignment.Center, TextAlignment = TextAlignment.Center
            };
            label.Foreground = new SolidColorBrush(Color.FromRgb(0xE8, 0xE8, 0xE8));
            Place(new Border
            {
                Width = row.Width, Height = row.Height, IsHitTestVisible = false,
                BorderBrush = new SolidColorBrush(Color.FromRgb(0x88, 0x88, 0x88)), BorderThickness = new Thickness(1),
                Background = new SolidColorBrush(Color.FromRgb(0x20, 0x20, 0x20)), Child = label
            }, row.Left, row.Top);
        }
    }

    private void UpdateGeometry(Border border, Thumb move, Dictionary<ScreenResizeHandle, Thumb> handles, ScreenLayerGeometry geometry)
    {
        var rect = geometry.CanvasRect(); border.Width = rect.Width; border.Height = rect.Height;
        move.Width = rect.Width; move.Height = rect.Height; Canvas.SetLeft(border, rect.Left); Canvas.SetTop(border, rect.Top);
        Canvas.SetLeft(move, rect.Left); Canvas.SetTop(move, rect.Top);
        foreach (var pair in handles) { Canvas.SetLeft(pair.Value, HandleLeft(geometry, pair.Key)); Canvas.SetTop(pair.Value, HandleTop(geometry, pair.Key)); }
    }

    private static ScreenLayerGeometry ReadGeometry(JsonObject layer) => new(
        Number(layer, "x"), Number(layer, "y"), Number(layer, "width"), Number(layer, "height"), Number(layer, "anchor_x"), Number(layer, "anchor_y"));

    private static void WriteGeometry(JsonObject layer, ScreenLayerGeometry geometry)
    {
        layer["x"] = geometry.X; layer["y"] = geometry.Y; layer["width"] = geometry.Width; layer["height"] = geometry.Height;
    }

    private static double HandleLeft(ScreenLayerGeometry geometry, ScreenResizeHandle handle)
    {
        var rect = geometry.CanvasRect();
        return handle switch
        {
            ScreenResizeHandle.TopLeft or ScreenResizeHandle.BottomLeft or ScreenResizeHandle.Left => rect.Left - 6,
            ScreenResizeHandle.TopRight or ScreenResizeHandle.BottomRight or ScreenResizeHandle.Right => rect.Right - 6,
            _ => rect.Left + rect.Width / 2 - 6
        };
    }

    private static double HandleTop(ScreenLayerGeometry geometry, ScreenResizeHandle handle)
    {
        var rect = geometry.CanvasRect();
        return handle switch
        {
            ScreenResizeHandle.TopLeft or ScreenResizeHandle.TopRight or ScreenResizeHandle.Top => rect.Top - 6,
            ScreenResizeHandle.BottomLeft or ScreenResizeHandle.BottomRight or ScreenResizeHandle.Bottom => rect.Bottom - 6,
            _ => rect.Top + rect.Height / 2 - 6
        };
    }

    private static Cursor CursorFor(ScreenResizeHandle handle) => handle switch
    {
        ScreenResizeHandle.TopLeft or ScreenResizeHandle.BottomRight => Cursors.SizeNWSE,
        ScreenResizeHandle.TopRight or ScreenResizeHandle.BottomLeft => Cursors.SizeNESW,
        ScreenResizeHandle.Top or ScreenResizeHandle.Bottom => Cursors.SizeNS,
        _ => Cursors.SizeWE
    };

    private void Place(UIElement element, double x, double y)
    {
        Panel.SetZIndex(element, _canvas.Children.Count); Canvas.SetLeft(element, x); Canvas.SetTop(element, y); _canvas.Children.Add(element);
    }

    private async void LoadImage(string media)
    {
        if (ProjectDirectory is not { } root || !_loading.Add(media)) return;
        var generation = _generation;
        try
        {
            var bitmap = await Task.Run(() =>
            {
                using var stream = File.OpenRead(Path.Combine(root, "resources", media.Replace('/', Path.DirectorySeparatorChar)));
                var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad;
                image.DecodePixelWidth = (int)CanvasWidth; image.StreamSource = stream; image.EndInit(); image.Freeze(); return image;
            });
            if (generation == _generation)
            {
                _images[media] = bitmap;
                if (!_dragging) { Refresh(false); Draw(); }
            }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException) { }
    }

    private static double Number(JsonObject layer, string key)
    {
        if (layer[key] is JsonValue value && value.TryGetValue<double>(out var number)) return number;
        if (layer[key] is JsonValue integer && integer.TryGetValue<int>(out var whole)) return whole;
        return 0;
    }

    private static double EditorNumber(JsonObject layer, string key) => key switch
    {
        "x" => Number(layer, "x") - Number(layer, "anchor_x") * Number(layer, "width"),
        "y" => Number(layer, "y") - Number(layer, "anchor_y") * Number(layer, "height"),
        _ => Number(layer, key)
    };

    // Keep legacy Runtime anchors intact. Authoring coordinates always address
    // the top-left corner, including when changing width and height.
    private static void SetEditorNumber(JsonObject layer, string key, double value)
    {
        var left = EditorNumber(layer, "x"); var top = EditorNumber(layer, "y");
        layer[key] = value;
        if (key == "x") layer["x"] = value + Number(layer, "anchor_x") * Number(layer, "width");
        if (key == "y") layer["y"] = value + Number(layer, "anchor_y") * Number(layer, "height");
        if (key == "width") layer["x"] = left + Number(layer, "anchor_x") * value;
        if (key == "height") layer["y"] = top + Number(layer, "anchor_y") * value;
    }

    private sealed record ScreenLayerItem(int Index, string Label, BitmapSource? Thumbnail);
}
