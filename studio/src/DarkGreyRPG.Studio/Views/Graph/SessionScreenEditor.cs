using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DarkGreyRPG.Studio.Core.Media;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Views.Graph;

/// <summary>Transient normalized composition editor. Dragging commits exactly once on release.</summary>
public sealed class SessionScreenEditor : UserControl
{
    public static readonly DependencyProperty ProjectDirectoryProperty = DependencyProperty.Register(nameof(ProjectDirectory), typeof(string), typeof(SessionScreenEditor));
    public string? ProjectDirectory { get => (string?)GetValue(ProjectDirectoryProperty); set => SetValue(ProjectDirectoryProperty, value); }
    private CanonicalNodeInspectorViewModel? _inspector;
    private JsonArray _layers = [];
    private readonly ListBox _list = new() { MaxHeight = 130 };
    private readonly Canvas _canvas = new() { Width = 320, Height = 180, Background = new SolidColorBrush(Color.FromRgb(28, 30, 35)), ClipToBounds = true };
    private readonly Dictionary<string, TextBox> _fields = new();
    private readonly Dictionary<string, BitmapSource> _images = new();
    private readonly HashSet<string> _loading = new();
    private readonly CheckBox _dialogue = new() { Content = "显示对话框参考", IsChecked = true };
    private readonly CheckBox _choice = new() { Content = "显示选项框参考" };
    private readonly TextBlock _error = new() { Foreground = Brushes.Tomato, TextWrapping = TextWrapping.Wrap };
    private bool _projecting;
    private bool _dragging;
    private long _generation;
    public SessionScreenEditor()
    {
        var panel = new StackPanel { Margin = new Thickness(0, 10, 0, 0) };
        panel.Children.Add(new TextBlock { Text = "完整画面（空列表清除画面）" });
        var buttons = new WrapPanel();
        void Button(string title, RoutedEventHandler action) { var b = new Button { Content = title, Margin = new Thickness(0, 2, 4, 2) }; b.Click += action; buttons.Children.Add(b); }
        Button("添加图片…", Import);
        Button("移除", (_, _) => { int i = _list.SelectedIndex; if (i >= 0) { _layers.RemoveAt(i); Commit(); } });
        Button("上移", (_, _) => Move(-1)); Button("下移", (_, _) => Move(1));
        panel.Children.Add(buttons); panel.Children.Add(_list);
        _list.SelectionChanged += (_, _) => { if (!_projecting) ProjectSelection(); };
        var view = new Viewbox { Stretch = Stretch.Uniform, Child = _canvas, MaxHeight = 220, Margin = new Thickness(0, 6, 0, 6) };
        panel.Children.Add(view);
        var overlays = new WrapPanel(); overlays.Children.Add(_dialogue); overlays.Children.Add(_choice); panel.Children.Add(overlays);
        _dialogue.Checked += (_, _) => Draw(); _dialogue.Unchecked += (_, _) => Draw();
        _choice.Checked += (_, _) => Draw(); _choice.Unchecked += (_, _) => Draw();
        var grid = new UniformGrid { Columns = 2 };
        foreach (var pair in new[] { ("x", "位置 X"), ("y", "位置 Y"), ("width", "宽度"), ("height", "高度"), ("anchor_x", "锚点 X"), ("anchor_y", "锚点 Y"), ("z", "层级 Z") })
        {
            grid.Children.Add(new TextBlock { Text = pair.Item2, VerticalAlignment = VerticalAlignment.Center });
            var box = new TextBox { Margin = new Thickness(2), MinWidth = 55, Tag = pair.Item1 };
            box.LostKeyboardFocus += FieldChanged; _fields.Add(pair.Item1, box); grid.Children.Add(box);
        }
        panel.Children.Add(grid); panel.Children.Add(_error); Content = panel;
        DataContextChanged += (_, _) =>
        {
            if (_inspector is not null) _inspector.PropertyChanged -= Changed;
            _inspector = DataContext as CanonicalNodeInspectorViewModel;
            if (_inspector is not null) _inspector.PropertyChanged += Changed;
            _generation++; _images.Clear(); _loading.Clear(); Refresh();
        };
    }
    private void Changed(object? sender, PropertyChangedEventArgs args) { if (args.PropertyName == nameof(CanonicalNodeInspectorViewModel.ScreenLayers)) Refresh(); }
    private void Refresh()
    {
        if (_dragging) return;
        _layers = _inspector?.IsScreen == true ? JsonNode.Parse(_inspector.ScreenLayers.GetRawText())!.AsArray() : [];
        int selected = _list.SelectedIndex;
        _projecting = true;
        _list.ItemsSource = _layers.Select((layer, i) => $"图片 {i + 1} · 层级 {layer?["z"]}").ToArray();
        _list.SelectedIndex = _layers.Count == 0 ? -1 : Math.Clamp(selected, 0, _layers.Count - 1);
        _projecting = false;
        ProjectSelection();
    }
    private JsonObject? Selected => _list.SelectedIndex >= 0 && _list.SelectedIndex < _layers.Count ? _layers[_list.SelectedIndex]?.AsObject() : null;
    private void ProjectSelection()
    {
        foreach (var pair in _fields) { pair.Value.IsEnabled = Selected is not null; pair.Value.Text = Selected?[pair.Key]?.ToJsonString() ?? ""; }
        if (!_dragging) Draw();
    }
    private void FieldChanged(object sender, System.Windows.Input.KeyboardFocusChangedEventArgs args)
    {
        if (_projecting || Selected is not { } layer || sender is not TextBox { Tag: string key } box) return;
        if (!double.TryParse(box.Text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value)) { _error.Text = "请输入有限数值。"; return; }
        layer[key] = JsonValue.Create(value);
        Commit();
    }
    private void Commit()
    {
        if (_inspector?.SetScreenLayers(JsonSerializer.SerializeToElement(_layers)) != true) { _error.Text = "画面参数无效：请检查坐标、尺寸、锚点和整数层级。"; Refresh(); }
        else { _error.Text = ""; Refresh(); }
    }
    private void Move(int delta)
    {
        int i = _list.SelectedIndex, target = i + delta;
        if (i < 0 || target < 0 || target >= _layers.Count) return;
        var node = _layers[i]; _layers.RemoveAt(i); _layers.Insert(target, node);
        for (int index = 0; index < _layers.Count; index++) _layers[index]!["z"] = index;
        Commit(); _list.SelectedIndex = target;
    }
    private async void Import(object sender, RoutedEventArgs args)
    {
        if (_inspector?.IsScreen != true || ProjectDirectory is not { } root || _layers.Count >= 32) return;
        var owner = _inspector;
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "图片|*.png;*.jpg;*.jpeg", CheckFileExists = true };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        SetCurrentValue(IsEnabledProperty, false);
        try
        {
            string tools = Path.Combine(AppContext.BaseDirectory, "media-tools", "ffmpeg");
            var media = await new ProjectMediaStore(root, Path.Combine(tools, "ffmpeg.exe"), Path.Combine(tools, "ffprobe.exe")).ImportImageAsync(dialog.FileName);
            if (_inspector != owner) return;
            _layers.Add(new JsonObject { ["media_ref"] = media.MediaRef, ["x"] = 0.5, ["y"] = 0.5, ["width"] = 0.5,
                ["height"] = 0.75, ["anchor_x"] = 0.5, ["anchor_y"] = 0.5, ["z"] = _layers.Count });
            Commit(); _list.SelectedIndex = _layers.Count - 1;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { _error.Text = exception.Message; }
        finally { SetCurrentValue(IsEnabledProperty, true); }
    }
    private void Draw()
    {
        _canvas.Children.Clear();
        foreach (var entry in _layers.Select((node, index) => (node: node!.AsObject(), index)).OrderBy(item => Number(item.node, "z")))
        {
            var layer = entry.node; string media = layer["media_ref"]!.GetValue<string>();
            double w = Number(layer, "width") * 320, h = Number(layer, "height") * 180;
            double x = Number(layer, "x") * 320 - Number(layer, "anchor_x") * w, y = Number(layer, "y") * 180 - Number(layer, "anchor_y") * h;
            var border = new Border { Width = w, Height = h, BorderBrush = entry.index == _list.SelectedIndex ? Brushes.DeepSkyBlue : Brushes.Gray, BorderThickness = new Thickness(1), Background = Brushes.DimGray };
            if (_images.TryGetValue(media, out var bitmap)) border.Child = new Image { Source = bitmap, Stretch = Stretch.Fill };
            else LoadImage(media);
            Place(border, x, y);
            var thumb = new Thumb { Width = w, Height = h, Opacity = 0.01, Cursor = System.Windows.Input.Cursors.SizeAll };
            int index = entry.index;
            var resize = new Thumb { Width = 9, Height = 9, Cursor = System.Windows.Input.Cursors.SizeNWSE, Background = Brushes.DeepSkyBlue };
            Point origin = default;
            double originX = 0, originY = 0, originWidth = 0, originHeight = 0;
            void BeginDrag()
            {
                _dragging = true; _list.SelectedIndex = index;
                origin = System.Windows.Input.Mouse.GetPosition(_canvas);
                originX = Number(layer, "x"); originY = Number(layer, "y");
                originWidth = Number(layer, "width"); originHeight = Number(layer, "height");
            }
            void UpdateGeometry()
            {
                double width = Number(layer, "width") * 320, height = Number(layer, "height") * 180;
                double left = Number(layer, "x") * 320 - Number(layer, "anchor_x") * width;
                double top = Number(layer, "y") * 180 - Number(layer, "anchor_y") * height;
                border.Width = thumb.Width = width; border.Height = thumb.Height = height;
                Canvas.SetLeft(border, left); Canvas.SetTop(border, top);
                Canvas.SetLeft(thumb, left); Canvas.SetTop(thumb, top);
                Canvas.SetLeft(resize, left + width - 9); Canvas.SetTop(resize, top + height - 9);
            }
            thumb.DragStarted += (_, _) => BeginDrag();
            thumb.DragDelta += (_, _) =>
            {
                Vector delta = System.Windows.Input.Mouse.GetPosition(_canvas) - origin;
                layer["x"] = Math.Clamp(originX + delta.X / 320, -2, 3);
                layer["y"] = Math.Clamp(originY + delta.Y / 180, -2, 3);
                UpdateGeometry();
            };
            thumb.DragCompleted += (_, _) => { _dragging = false; Commit(); }; Place(thumb, x, y);
            resize.DragStarted += (_, _) => BeginDrag();
            resize.DragDelta += (_, _) =>
            {
                Vector delta = System.Windows.Input.Mouse.GetPosition(_canvas) - origin;
                double width = Math.Clamp(originWidth + delta.X / 320, 0.001, 4);
                double height = Math.Clamp(originHeight + delta.Y / 180, 0.001, 4);
                layer["width"] = width; layer["height"] = height;
                layer["x"] = Math.Clamp(originX + Number(layer, "anchor_x") * (width - originWidth), -2, 3);
                layer["y"] = Math.Clamp(originY + Number(layer, "anchor_y") * (height - originHeight), -2, 3);
                UpdateGeometry();
            };
            resize.DragCompleted += (_, _) => { _dragging = false; Commit(); }; Place(resize, x + w - 9, y + h - 9);
        }
        if (_dialogue.IsChecked == true) Overlay("对话框参考", 8, 132, 304, 42);
        if (_choice.IsChecked == true) Overlay("选项框参考", 160, 88, 150, 38);
    }
    private void Place(UIElement element, double x, double y) { Canvas.SetLeft(element, x); Canvas.SetTop(element, y); _canvas.Children.Add(element); }
    private void Overlay(string text, double x, double y, double w, double h) => Place(new Border { Width = w, Height = h, IsHitTestVisible = false, BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(1), Background = new SolidColorBrush(Color.FromArgb(110, 0, 0, 0)), Child = new TextBlock { Text = text, Foreground = Brushes.White, Margin = new Thickness(4) } }, x, y);
    private async void LoadImage(string media)
    {
        if (ProjectDirectory is not { } root || !_loading.Add(media)) return;
        long generation = _generation;
        try
        {
            var bitmap = await Task.Run(() =>
            {
                using var stream = File.OpenRead(Path.Combine(root, "resources", media.Replace('/', Path.DirectorySeparatorChar)));
                var image = new BitmapImage(); image.BeginInit(); image.CacheOption = BitmapCacheOption.OnLoad; image.DecodePixelWidth = 320;
                image.StreamSource = stream; image.EndInit(); image.Freeze(); return image;
            });
            if (generation == _generation) { _images[media] = bitmap; if (!_dragging) Draw(); }
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException) { }
    }
    private static double Number(JsonObject layer, string key) => layer[key]!.GetValue<double>();
}
