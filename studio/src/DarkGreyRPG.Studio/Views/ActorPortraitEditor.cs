using System.IO;
using System.Windows;
using System.Windows.Automation;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Media;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Views;

/// <summary>
/// Inline Actor portrait editor. It deliberately has no Window/Dialog base
/// class; the host can place it in any Inspector and still use the OS file
/// picker for importing a project-owned image.
/// </summary>
public sealed class ActorPortraitEditor : UserControl
{
    private ActorEditorViewModel? _editor;
    private readonly Border _defaultPreview = new() { Width = 96, Height = 96, ClipToBounds = true, Margin = new Thickness(0, 4, 10, 4) };
    private readonly TextBlock _defaultStatus = new() { VerticalAlignment = VerticalAlignment.Center };
    private readonly ListBox _variants = new() { Height = 146, MinHeight = 96, HorizontalContentAlignment = HorizontalAlignment.Stretch };
    private readonly TextBox _variantName = new() { MinWidth = 180 };
    private readonly StackPanel _renameRow = new() { Orientation = Orientation.Horizontal, Visibility = Visibility.Collapsed };
    private readonly TextBlock _error = new() { Foreground = Brushes.OrangeRed, TextWrapping = TextWrapping.Wrap };
    private readonly StackPanel _root = new();
    private bool _refreshing;
    private Func<ActorPortraitVariant, bool>? _isReferenced;

    public ActorPortraitEditor()
    {
        SetResourceReference(ForegroundProperty, "TextFillColorPrimaryBrush");
        _error.SetResourceReference(TextBlock.ForegroundProperty, "SystemFillColorCriticalBrush");
        BuildVisualTree();
        DataContextChanged += OnDataContextChanged;
        _variants.SelectionChanged += OnVariantSelectionChanged;
        _variants.PreviewKeyDown += (_, args) => { if (args.Key == Key.F2) { BeginRename(); args.Handled = true; } };
    }

    public static readonly DependencyProperty ProjectDirectoryProperty = DependencyProperty.Register(
        nameof(ProjectDirectory), typeof(string), typeof(ActorPortraitEditor), new PropertyMetadata(string.Empty));

    public string ProjectDirectory
    {
        get => (string)GetValue(ProjectDirectoryProperty);
        set => SetValue(ProjectDirectoryProperty, value);
    }

    public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register(
        nameof(IsReadOnly), typeof(bool), typeof(ActorPortraitEditor), new PropertyMetadata(false, (d, _) => ((ActorPortraitEditor)d).Refresh()));

    public bool IsReadOnly
    {
        get => (bool)GetValue(IsReadOnlyProperty);
        set => SetValue(IsReadOnlyProperty, value);
    }

    /// <summary>Optional reference check supplied by the host's story index.</summary>
    public Func<ActorPortraitVariant, bool>? IsPortraitVariantReferenced
    {
        get => _isReferenced;
        set
        {
            _isReferenced = value;
            if (_editor is not null) _editor.IsPortraitVariantReferenced = value;
        }
    }

    public string LastError => _error.Text;

    public ActorEditorViewModel? Editor => _editor;

    public void SetEditor(ActorEditorViewModel? editor)
    {
        if (ReferenceEquals(_editor, editor)) return;
        if (_editor is not null) _editor.PropertyChanged -= OnEditorPropertyChanged;
        _editor = editor;
        if (_editor is not null)
        {
            if (_isReferenced is not null) _editor.IsPortraitVariantReferenced = _isReferenced;
            _editor.PropertyChanged += OnEditorPropertyChanged;
        }
        Refresh();
    }

    public async Task<bool> ImportDefaultAsync(string source, CancellationToken cancellationToken = default)
    {
        if (!CanEdit(out var editor)) return false;
        try
        {
            var media = await CreateStore().ImportImageAsync(source, cancellationToken);
            editor.DefaultPortraitRef = media.MediaRef;
            SetError(string.Empty);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            SetError(exception.Message);
            return false;
        }
    }

    public async Task<bool> ImportVariantAsync(string source, string? name = null, CancellationToken cancellationToken = default)
    {
        if (!CanEdit(out var editor)) return false;
        var variantName = (name ?? Path.GetFileNameWithoutExtension(source)).Trim();
        if (string.IsNullOrWhiteSpace(variantName)) variantName = "表情";
        var baseName = variantName;
        for (var suffix = 1; editor.PortraitVariants.Any(value => value.Name == variantName); suffix++)
            variantName = $"{baseName}({suffix})";

        try
        {
            var media = await CreateStore().ImportImageAsync(source, cancellationToken);
            editor.SetPortraitVariants([.. editor.PortraitVariants, new ActorPortraitVariant(variantName, media.MediaRef)]);
            editor.PortraitVariantName = variantName;
            editor.SelectedPortraitVariant = editor.PortraitVariants.Last();
            SetError(string.Empty);
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            SetError(exception.Message);
            return false;
        }
    }

    public bool RenameSelected(string? name)
    {
        if (_editor?.SelectedPortraitVariant is not { } selected) return SetErrorAndFalse("请先选择一个表情差分。");
        var success = _editor.TryRenamePortraitVariant(selected, name);
        SetError(success ? string.Empty : _editor.PortraitEditError);
        return success;
    }

    public bool RemoveSelected()
    {
        if (_editor?.SelectedPortraitVariant is not { } selected) return SetErrorAndFalse("请先选择一个表情差分。");
        var success = _editor.TryRemovePortraitVariant(selected);
        SetError(success ? string.Empty : _editor.PortraitEditError);
        return success;
    }

    public bool ClearDefault()
    {
        if (!CanEdit(out var editor)) return false;
        editor.DefaultPortraitRef = null;
        SetError(string.Empty);
        return true;
    }

    private void BuildVisualTree()
    {
        _root.Margin = new Thickness(0, 8, 0, 0);
        _root.Children.Add(new TextBlock { Text = "默认头像", FontWeight = FontWeights.SemiBold });
        var defaultRow = new StackPanel { Orientation = Orientation.Horizontal };
        defaultRow.Children.Add(_defaultPreview);
        var defaultActions = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
        var importDefault = NamedButton("导入/更换…", "导入或更换默认头像", new Thickness(0, 0, 6, 4));
        importDefault.Click += async (_, _) => await PickAndImportDefaultAsync(importDefault);
        var clearDefault = NamedButton("清除", "清除默认头像", new Thickness(0, 0, 6, 4));
        clearDefault.Click += (_, _) => ClearDefault();
        defaultActions.Children.Add(new StackPanel { Orientation = Orientation.Horizontal, Children = { importDefault, clearDefault } });
        defaultActions.Children.Add(_defaultStatus);
        defaultRow.Children.Add(defaultActions);
        _root.Children.Add(defaultRow);

        _root.Children.Add(new TextBlock { Text = "表情差分", FontWeight = FontWeights.SemiBold, Margin = new Thickness(0, 8, 0, 3) });
        _root.Children.Add(_variants);
        _renameRow.Margin = new Thickness(0, 5, 0, 0);
        _renameRow.Children.Add(_variantName);
        var confirmRename = NamedButton("确定", "确定重命名");
        confirmRename.Click += (_, _) => CommitRename();
        _renameRow.Children.Add(confirmRename);
        _variantName.KeyDown += (_, args) =>
        {
            if (args.Key == Key.Enter) { CommitRename(); args.Handled = true; }
            else if (args.Key == Key.Escape) { _renameRow.Visibility = Visibility.Collapsed; _variants.Focus(); args.Handled = true; }
        };
        _root.Children.Add(_renameRow);
        var variantActions = new StackPanel { Orientation = Orientation.Horizontal, Margin = new Thickness(0, 5, 0, 0) };
        var importVariant = NamedButton("导入", "导入表情差分", new Thickness(0, 0, 6, 0));
        importVariant.Click += async (_, _) => await PickAndImportVariantAsync(importVariant);
        var rename = NamedButton("重命名", "重命名表情差分", new Thickness(0, 0, 6, 0));
        rename.Click += (_, _) => BeginRename();
        var remove = NamedButton("删除", "删除表情差分");
        remove.Click += (_, _) => RemoveSelected();
        variantActions.Children.Add(importVariant); variantActions.Children.Add(rename); variantActions.Children.Add(remove);
        _root.Children.Add(variantActions);
        _root.Children.Add(_error);
        Content = _root;
    }

    private static Button NamedButton(string content, string automationName, Thickness? margin = null)
    {
        var button = new Button { Content = content };
        if (margin is { } value) button.Margin = value;
        AutomationProperties.SetName(button, automationName);
        return button;
    }

    private async Task PickAndImportDefaultAsync(Button button)
    {
        if (!CanEdit(out _)) return;
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "图片|*.png;*.jpg;*.jpeg", CheckFileExists = true, Multiselect = false };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        button.IsEnabled = false;
        try { await ImportDefaultAsync(dialog.FileName); }
        finally { button.IsEnabled = true; }
    }

    private async Task PickAndImportVariantAsync(Button button)
    {
        if (!CanEdit(out _)) return;
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "图片|*.png;*.jpg;*.jpeg", CheckFileExists = true, Multiselect = false };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        button.IsEnabled = false;
        try { await ImportVariantAsync(dialog.FileName); }
        finally { button.IsEnabled = true; }
    }

    private ProjectMediaStore CreateStore()
    {
        if (string.IsNullOrWhiteSpace(ProjectDirectory)) throw new InvalidOperationException("未设置项目目录。");
        var tools = Path.Combine(AppContext.BaseDirectory, "media-tools", "ffmpeg");
        return new ProjectMediaStore(ProjectDirectory, Path.Combine(tools, "ffmpeg.exe"), Path.Combine(tools, "ffprobe.exe"));
    }

    private void BeginRename()
    {
        if (!CanEdit(out var editor) || editor.SelectedPortraitVariant is not { } selected) return;
        _variantName.Text = selected.Name;
        _renameRow.Visibility = Visibility.Visible;
        _variantName.Focus();
        _variantName.SelectAll();
    }

    private void CommitRename()
    {
        if (RenameSelected(_variantName.Text)) { _renameRow.Visibility = Visibility.Collapsed; _variants.Focus(); }
    }

    private bool CanEdit(out ActorEditorViewModel editor)
    {
        editor = _editor!;
        if (editor is null) { SetError("当前没有角色编辑器。"); return false; }
        if (IsReadOnly || editor.IsReadOnly || !editor.SupportsPortraits)
        {
            SetError(!editor.SupportsPortraits ? "旧版角色不支持头像编辑。" : "当前角色只读，无法修改头像。");
            return false;
        }
        return true;
    }

    private bool SetErrorAndFalse(string message) { SetError(message); return false; }

    private void SetError(string value)
    {
        _error.Text = value;
        _error.Visibility = string.IsNullOrEmpty(value) ? Visibility.Collapsed : Visibility.Visible;
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs args) =>
        SetEditor(args.NewValue as ActorEditorViewModel);

    private void OnEditorPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs args) =>
        Dispatcher.InvokeAsync(Refresh, System.Windows.Threading.DispatcherPriority.DataBind);

    private void OnVariantSelectionChanged(object sender, SelectionChangedEventArgs args)
    {
        if (_refreshing || _editor is null) return;
        _editor.SelectedPortraitVariant = (_variants.SelectedItem as ListBoxItem)?.Tag as ActorPortraitVariant;
        _variantName.Text = _editor.SelectedPortraitVariant?.Name ?? string.Empty;
    }

    private void Refresh()
    {
        if (!Dispatcher.CheckAccess()) { Dispatcher.Invoke(Refresh); return; }
        _refreshing = true;
        try
        {
            var editor = _editor;
            var canEdit = editor is not null && !IsReadOnly && !editor.IsReadOnly && editor.SupportsPortraits;
            foreach (var element in FindButtons(_root)) element.IsEnabled = canEdit;
            _variants.IsEnabled = canEdit;
            _variantName.IsEnabled = canEdit;
            _defaultStatus.Text = editor?.DefaultPortraitStatus ?? "未选择角色";
            _defaultPreview.Child = editor?.DefaultPortraitRef is { } reference ? TryImage(reference) : new TextBlock { Text = "无头像", HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
            _variants.Items.Clear();
            if (editor is not null)
            {
                foreach (var variant in editor.PortraitVariants)
                {
                    var row = new DockPanel { LastChildFill = true, Margin = new Thickness(0, 1, 0, 1) };
                    row.Children.Add(new Border { Width = 42, Height = 42, ClipToBounds = true, Margin = new Thickness(0, 0, 8, 0), Child = TryImage(variant.MediaRef) });
                    row.Children.Add(new TextBlock { Text = variant.Name, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis });
                    _variants.Items.Add(new ListBoxItem { Content = row, Tag = variant, ToolTip = variant.MediaRef });
                }
                var selected = editor.SelectedPortraitVariant;
                _variants.SelectedItem = _variants.Items.OfType<ListBoxItem>().FirstOrDefault(item => Equals(item.Tag, selected));
                _variantName.Text = selected?.Name ?? string.Empty;
                if (string.IsNullOrEmpty(_error.Text) && editor.PortraitEditError.Length > 0) SetError(editor.PortraitEditError);
            }
        }
        finally { _refreshing = false; }
    }

    private UIElement TryImage(string mediaRef)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(ProjectDirectory) || !MediaReference.IsImage(mediaRef)) throw new InvalidDataException();
            if (_editor?.PortraitPreviewData?.Invoke(mediaRef) is { } data)
            {
                using var stream = new MemoryStream(data, writable: false);
                var referenced = new BitmapImage();
                referenced.BeginInit(); referenced.CacheOption = BitmapCacheOption.OnLoad; referenced.StreamSource = stream; referenced.EndInit(); referenced.Freeze();
                return new Image { Source = referenced, Stretch = Stretch.UniformToFill };
            }
            var path = Path.Combine(Path.GetFullPath(ProjectDirectory), "resources", mediaRef.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(path)) throw new FileNotFoundException();
            var bitmap = new BitmapImage();
            bitmap.BeginInit(); bitmap.CacheOption = BitmapCacheOption.OnLoad; bitmap.UriSource = new Uri(path); bitmap.EndInit(); bitmap.Freeze();
            return new Image { Source = bitmap, Stretch = Stretch.UniformToFill };
        }
        catch { return new TextBlock { Text = "预览不可用", FontSize = 10, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, TextWrapping = TextWrapping.Wrap }; }
    }

    private static IEnumerable<Button> FindButtons(DependencyObject parent)
    {
        if (parent is Button button) yield return button;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(parent); index++)
            foreach (var child in FindButtons(VisualTreeHelper.GetChild(parent, index))) yield return child;
    }
}
