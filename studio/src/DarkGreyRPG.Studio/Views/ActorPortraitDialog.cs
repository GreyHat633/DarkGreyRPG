using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Media;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Views;

public sealed class ActorPortraitDialog : Window
{
    private readonly ActorEditorViewModel _editor;
    private readonly string _projectDirectory;
    private readonly Image _preview = new() { Height = 140, Stretch = Stretch.Uniform, Margin = new Thickness(0, 8, 0, 8) };
    private readonly TextBlock _error = new() { Foreground = Brushes.Tomato, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 6, 0, 6) };
    private readonly StackPanel _body = new();
    private bool _busy;
    private string? _previewReference;

    public ActorPortraitDialog(ActorDocument document, string projectDirectory)
    {
        _editor = new(document); _projectDirectory = projectDirectory;
        Title = $"头像与表情 · {document.DisplayName}";
        Width = 600; Height = 700; MinWidth = 470; MinHeight = 580;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        DataContext = _editor;
        var dock = new DockPanel { Margin = new Thickness(24) }; Content = dock;
        var footer = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Right, Margin = new Thickness(0, 14, 0, 0) };
        var cancel = Button("取消", (_, _) => DialogResult = false); cancel.IsCancel = true; footer.Children.Add(cancel);
        var save = Button("保存头像", (_, _) => { if (!_busy && document.ValidationErrors.Count == 0) DialogResult = true; else _error.Text = _editor.ValidationText; }); footer.Children.Add(save);
        DockPanel.SetDock(footer, Dock.Bottom); dock.Children.Add(footer);
        dock.Children.Add(new ScrollViewer { Content = _body, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled });
        _body.Children.Add(new TextBlock { Text = "默认头像", FontSize = 18, FontWeight = FontWeights.SemiBold });
        _body.Children.Add(_preview);
        var defaultButtons = new WrapPanel(); defaultButtons.Children.Add(Button("导入默认头像…", async (_, _) => await Import(false))); defaultButtons.Children.Add(Button("移除默认头像", (_, _) => _editor.DefaultPortraitRef = null)); _body.Children.Add(defaultButtons);
        var status = new TextBlock { Margin = new Thickness(0, 6, 0, 12) }; status.SetBinding(TextBlock.TextProperty, "DefaultPortraitStatus"); _body.Children.Add(status);
        _body.Children.Add(new TextBlock { Text = "表情变体", FontWeight = FontWeights.SemiBold });
        var variants = new ListBox { Height = 90, DisplayMemberPath = "Name", Margin = new Thickness(0, 6, 0, 10) };
        variants.SetBinding(ItemsControl.ItemsSourceProperty, "PortraitVariants"); variants.SetBinding(ListBox.SelectedItemProperty, new Binding("SelectedPortraitVariant") { Mode = BindingMode.TwoWay }); _body.Children.Add(variants);
        _body.Children.Add(new TextBlock { Text = "变体名称" });
        var name = new TextBox { Margin = new Thickness(0, 5, 0, 6) }; name.SetBinding(TextBox.TextProperty, new Binding("PortraitVariantName") { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged }); _body.Children.Add(name);
        var actions = new WrapPanel(); actions.Children.Add(Button("导入为新变体…", async (_, _) => await Import(true)));
        actions.Children.Add(Button("重命名选中变体", (_, _) => { if (_editor.SelectedPortraitVariant is { } selected && ValidName(_editor.PortraitVariantName, selected)) _editor.SetPortraitVariants(_editor.PortraitVariants.Select(v => v == selected ? v with { Name = _editor.PortraitVariantName.Trim() } : v)); }));
        actions.Children.Add(Button("移除选中变体", (_, _) => { if (_editor.SelectedPortraitVariant is { } selected) _editor.SetPortraitVariants(_editor.PortraitVariants.Where(v => v != selected)); })); _body.Children.Add(actions);
        var history = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) }; history.Children.Add(new Button { Content = "撤销", Command = _editor.UndoCommand, Margin = new Thickness(0, 0, 6, 0) }); history.Children.Add(new Button { Content = "重做", Command = _editor.RedoCommand }); _body.Children.Add(history); _body.Children.Add(_error);
        _editor.PropertyChanged += (_, _) => RefreshPreview(); RefreshPreview();
        Closing += (_, e) => { if (_busy) e.Cancel = true; };
    }
    private static Button Button(string text, RoutedEventHandler handler) { var b = new Button { Content = text, Margin = new Thickness(0, 2, 6, 2), Padding = new Thickness(10, 5, 10, 5) }; b.Click += handler; return b; }
    private bool ValidName(string name, ActorPortraitVariant? except = null)
    {
        if (!string.IsNullOrWhiteSpace(name) && _editor.PortraitVariants.All(v => v == except || v.Name != name.Trim())) { _error.Text = ""; return true; }
        _error.Text = "请输入非空且不重复的变体名称。"; return false;
    }
    private async Task Import(bool variant)
    {
        var name = _editor.PortraitVariantName.Trim(); if (variant && !ValidName(name)) return;
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "图片|*.png;*.jpg;*.jpeg", CheckFileExists = true };
        if (dialog.ShowDialog(this) != true) return;
        _busy = true; _body.IsEnabled = false;
        try
        {
            var tools = Path.Combine(AppContext.BaseDirectory, "media-tools", "ffmpeg");
            var media = await new ProjectMediaStore(_projectDirectory, Path.Combine(tools, "ffmpeg.exe"), Path.Combine(tools, "ffprobe.exe")).ImportImageAsync(dialog.FileName);
            if (variant) _editor.SetPortraitVariants([.. _editor.PortraitVariants, new(name, media.MediaRef)]); else _editor.DefaultPortraitRef = media.MediaRef;
            _error.Text = "";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or System.ComponentModel.Win32Exception) { _error.Text = e.Message; }
        finally { _busy = false; _body.IsEnabled = true; }
    }
    private void RefreshPreview()
    {
        var reference = _editor.SelectedPortraitVariant?.MediaRef ?? _editor.DefaultPortraitRef;
        if (reference == _previewReference) return;
        _previewReference = reference;
        try
        {
            if (reference is null) { _preview.Source = null; return; }
            using var stream = File.OpenRead(Path.Combine(_projectDirectory, "resources", reference.Replace('/', Path.DirectorySeparatorChar)));
            var bitmap = new BitmapImage(); bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.DecodePixelWidth = 320; bitmap.StreamSource = stream; bitmap.EndInit(); bitmap.Freeze(); _preview.Source = bitmap;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException or NotSupportedException or FileFormatException) { _preview.Source = null; }
    }
}
