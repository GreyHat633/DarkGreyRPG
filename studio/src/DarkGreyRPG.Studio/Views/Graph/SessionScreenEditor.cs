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
        DependencyProperty.Register(nameof(ProjectDirectory), typeof(string), typeof(SessionScreenEditor));

    public string? ProjectDirectory { get => (string?)GetValue(ProjectDirectoryProperty); set => SetValue(ProjectDirectoryProperty, value); }

    private CanonicalNodeInspectorViewModel? _inspector;
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
    private long _generation;
    private JsonArray? _gestureBefore;
    private bool _listPointerDown;
    private Point _listPointerOrigin;
    private int _listPointerIndex = -1;
    private int _listReorderIndex = -1;

    public SessionScreenEditor()
    {
        Focusable = true;
        PreviewKeyDown += OnPreviewKeyDown;
        var panel = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        panel.Children.Add(new TextBlock { Text = "完整画面（空列表清除画面）" });
        var buttons = new WrapPanel();
        void Button(string title, RoutedEventHandler action)
        {
            var button = new Button { Content = title, Margin = new Thickness(0, 2, 4, 2) };
            button.Click += action;
            buttons.Children.Add(button);
        }
        Button("添加图片…", Import);
        Button("移除", (_, _) => RemoveSelected());
        Button("上移", (_, _) => Move(-1));
        Button("下移", (_, _) => Move(1));
        panel.Children.Add(buttons);

        _list.ItemTemplate = CreateLayerTemplate();
        _list.SetValue(ScrollViewer.VerticalScrollBarVisibilityProperty, ScrollBarVisibility.Auto);
        _list.SelectionChanged += (_, _) => { if (!_projecting) ProjectSelection(); };
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

        panel.Children.Add(new Viewbox
        {
            Stretch = Stretch.Uniform, Child = _canvas, Height = 190, MaxHeight = 190,
            Margin = new Thickness(0, 6, 0, 6)
        });
        var overlays = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Center };
        overlays.Children.Add(_dialogue);
        overlays.Children.Add(_choice);
        panel.Children.Add(overlays);
        _dialogue.Checked += (_, _) => Draw(); _dialogue.Unchecked += (_, _) => Draw();
        _choice.Checked += (_, _) => Draw(); _choice.Unchecked += (_, _) => Draw();

        var grid = new UniformGrid { Columns = 2 };
        foreach (var pair in new[]
        {
            ("x", "位置 X"), ("y", "位置 Y"), ("width", "宽度"), ("height", "高度"),
            ("anchor_x", "锚点 X"), ("anchor_y", "锚点 Y"), ("z", "层级 Z")
        })
        {
            grid.Children.Add(new TextBlock { Text = pair.Item2, VerticalAlignment = VerticalAlignment.Center });
            var box = new TextBox { Margin = new Thickness(2), MinWidth = 55, Tag = pair.Item1 };
            NumericDrag.SetStep(box, pair.Item1 == "z" ? 1 : 0.01);
            if (pair.Item1 is "width" or "height")
            { NumericDrag.SetMinimum(box, MinimumLayerSize); NumericDrag.SetMaximum(box, MaximumLayerSize); }
            if (pair.Item1 is "x" or "y")
            { NumericDrag.SetMinimum(box, -2); NumericDrag.SetMaximum(box, 3); }
            if (pair.Item1 is "anchor_x" or "anchor_y")
            { NumericDrag.SetMinimum(box, 0); NumericDrag.SetMaximum(box, 1); }
            box.LostKeyboardFocus += FieldChanged;
            box.PreviewKeyDown += (sender, args) =>
            {
                if (args.Key == Key.Enter || args.Key == Key.S && Keyboard.Modifiers.HasFlag(ModifierKeys.Control))
                    FieldChanged(sender, null!);
            };
            NumericDrag.AddCommittedHandler(box, (sender, _) => FieldChanged(sender, null!));
            _fields.Add(pair.Item1, box);
            grid.Children.Add(box);
        }
        panel.Children.Add(grid); panel.Children.Add(_error); Content = panel;

        DataContextChanged += (_, _) =>
        {
            if (_inspector is not null) _inspector.PropertyChanged -= Changed;
            _inspector = DataContext as CanonicalNodeInspectorViewModel;
            if (_inspector is not null) _inspector.PropertyChanged += Changed;
            _generation++; _images.Clear(); _loading.Clear(); CancelGesture(); Refresh();
        };
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

    private static DataTemplate CreateLayerTemplate()
    {
        var root = new FrameworkElementFactory(typeof(StackPanel));
        root.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
        var image = new FrameworkElementFactory(typeof(Image));
        image.SetValue(FrameworkElement.WidthProperty, 34d); image.SetValue(FrameworkElement.HeightProperty, 24d);
        image.SetValue(Image.StretchProperty, Stretch.UniformToFill); image.SetValue(FrameworkElement.MarginProperty, new Thickness(2, 1, 5, 1));
        image.SetBinding(Image.SourceProperty, new Binding(nameof(ScreenLayerItem.Thumbnail))); root.AppendChild(image);
        var label = new FrameworkElementFactory(typeof(TextBlock));
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
        if (readModel) _layers = _inspector?.IsScreen == true
            ? JsonNode.Parse(_inspector.ScreenLayers.GetRawText())!.AsArray() : [];
        RenderList(); ProjectSelection();
    }

    private void RenderList()
    {
        var selected = _list.SelectedIndex;
        _projecting = true;
        _list.ItemsSource = _layers.Select((layer, index) => new ScreenLayerItem(
            index, $"图片 {index + 1} · 层级 {layer?["z"]}",
            layer is JsonObject item && item["media_ref"] is JsonValue mediaValue && mediaValue.TryGetValue<string>(out var media)
                && _images.TryGetValue(media, out var bitmap) ? bitmap : null)).ToArray();
        _list.SelectedIndex = _layers.Count == 0 ? -1 : Math.Clamp(selected, 0, _layers.Count - 1);
        _projecting = false;
    }

    private JsonObject? Selected => _list.SelectedIndex >= 0 && _list.SelectedIndex < _layers.Count
        ? _layers[_list.SelectedIndex]?.AsObject() : null;

    private void ProjectSelection()
    {
        foreach (var pair in _fields)
        {
            pair.Value.IsEnabled = Selected is not null;
            pair.Value.Text = Selected?[pair.Key]?.ToJsonString() ?? "";
        }
        if (!_dragging) Draw();
    }

    private void FieldChanged(object sender, KeyboardFocusChangedEventArgs args)
    {
        if (_projecting || Selected is not { } layer || sender is not TextBox { Tag: string key } box) return;
        if (!double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
          { _error.Text = "请输入有限数值。"; return; }
        if (Number(layer, key) == value) return;
          layer[key] = JsonValue.Create(value); Commit();
    }

    private void Commit()
    {
        if (_inspector?.SetScreenLayers(JsonSerializer.SerializeToElement(_layers)) != true)
        { _error.Text = "画面参数无效：请检查坐标、尺寸、锚点和整数层级。"; Refresh(); }
        else { _error.Text = ""; Refresh(); }
    }

    private void RemoveSelected()
    {
        var index = _list.SelectedIndex; if (index < 0) return;
        _layers.RemoveAt(index); NormalizeLayerOrder(); Commit();
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
        _projecting = true; _list.SelectedIndex = target; _projecting = false;
        Refresh(false);
        if (!commit) Draw();
        if (commit) Commit();
    }

    private void NormalizeLayerOrder()
    {
        for (var index = 0; index < _layers.Count; index++) _layers[index]!["z"] = index;
    }

    private void ListMouseDown(object sender, MouseButtonEventArgs args)
    {
        var index = ItemIndexAt(args.GetPosition(_list)); if (index < 0) return;
        _listPointerDown = true; _listPointerOrigin = args.GetPosition(_list); _listPointerIndex = index; _list.CaptureMouse();
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

    private void BeginGesture()
    {
        if (_gestureBefore is null) _gestureBefore = CloneLayers(_layers);
        _dragging = true; Focus();
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
        _listPointerDown = false; _listReorderIndex = -1;
        if (_list.IsMouseCaptured) _list.ReleaseMouseCapture();
        Refresh(false);
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs args)
    {
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
                ["media_ref"] = media.MediaRef, ["x"] = 0.5, ["y"] = 0.5, ["width"] = 0.5,
                ["height"] = 0.75, ["anchor_x"] = 0.5, ["anchor_y"] = 0.5, ["z"] = _layers.Count
            });
            Commit(); _list.SelectedIndex = _layers.Count - 1;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or Win32Exception)
        { _error.Text = exception.Message; }
        finally { SetCurrentValue(IsEnabledProperty, true); }
    }

    private void Draw()
    {
        _canvas.Children.Clear();
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

            var thumb = new Thumb { Width = rect.Width, Height = rect.Height, Opacity = 0.01, Cursor = Cursors.SizeAll };
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
                    };
                    resize.DragCompleted += (_, completed) => { if (completed.Canceled) CancelGesture(); else CompleteGesture(); };
                }

            var moveX = 0d; var moveY = 0d;
            thumb.DragStarted += (_, _) => { BeginGesture(); _list.SelectedIndex = entry.index; moveX = moveY = 0; };
            thumb.DragDelta += (_, delta) =>
            {
                if (!_dragging) return;
                moveX += delta.HorizontalChange; moveY += delta.VerticalChange;
                layer["x"] = Math.Clamp(geometry.X + moveX / CanvasWidth, -2, 3);
                layer["y"] = Math.Clamp(geometry.Y + moveY / CanvasHeight, -2, 3);
                UpdateGeometry(border, thumb, handles, ReadGeometry(layer));
            };
            thumb.DragCompleted += (_, completed) => { if (completed.Canceled) CancelGesture(); else CompleteGesture(); };
            Place(thumb, rect.Left, rect.Top); UpdateGeometry(border, thumb, handles, geometry);
            foreach (var handle in handles) Place(handle.Value, HandleLeft(geometry, handle.Key), HandleTop(geometry, handle.Key));
        }
        if (_dialogue.IsChecked == true) DrawDialogueReference();
        if (_choice.IsChecked == true) DrawChoiceReference();
    }

    private void DrawDialogueReference() => PlaceOverlay("对话框", 16, 128, 288, 48);

    private void DrawChoiceReference() => PlaceOverlay("选项框", 16, 14, 288, 68);

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

    private void PlaceOverlay(string text, double x, double y, double width, double height)
    {
        Place(new Border
        {
            Width = width, Height = height, IsHitTestVisible = false,
            BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(1),
            Background = new SolidColorBrush(Color.FromArgb(110, 0, 0, 0)),
            Child = new TextBlock { Text = text, Foreground = Brushes.White, Margin = new Thickness(4) }
        }, x, y);
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

    private sealed record ScreenLayerItem(int Index, string Label, BitmapSource? Thumbnail);
}
