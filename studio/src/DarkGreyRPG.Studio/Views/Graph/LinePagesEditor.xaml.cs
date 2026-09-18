using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Input;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Views.Graph;

public partial class LinePagesEditor : UserControl
{
    private Window? _inputWindow;
    public LinePagesEditor()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            DetachWindow();
            _inputWindow = Window.GetWindow(this);
            _inputWindow?.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(Window_OnMouseDown), true);
        };
        Unloaded += (_, _) => DetachWindow();
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is CanonicalNodeInspectorViewModel old && !ReferenceEquals(e.OldValue, e.NewValue))
                old.ClearLinePageSelection();
        };
    }
    private void DetachWindow()
    {
        _inputWindow?.RemoveHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(Window_OnMouseDown));
        _inputWindow = null;
    }
    private void Window_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || DataContext is not CanonicalNodeInspectorViewModel owner) return;
        var source = e.OriginalSource as DependencyObject;
        var editor = Ancestor<LinePagesEditor>(source);
        if (editor?.DataContext is not CanonicalNodeInspectorViewModel target ||
            !ReferenceEquals(owner.Host, target.Host) || owner.NodeId != target.NodeId)
        {
            owner.ClearLinePageSelection();
            return;
        }
        if (!ReferenceEquals(editor, this)) return;
        for (var current = source; current is not null && !ReferenceEquals(current, this); current = InputParent(current))
            if (current is Border { Name: "LinePageCard", DataContext: CanonicalLinePageViewModel page })
            {
                SelectPageAtPointer(page, source, e);
                return;
            }
        var action = Ancestor<Button>(source);
        if (action is null || System.Windows.Automation.AutomationProperties.GetAutomationId(action)
            is not ("AddLinePage" or "RemoveSelectedLinePages")) owner.ClearLinePageSelection();
    }
    private void Add_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CanonicalNodeInspectorViewModel vm || vm.AddLinePage() is not { } id) return;
        vm.SelectLinePage(id);
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            var box = Children<TextBox>(Pages).FirstOrDefault(b => b.DataContext is CanonicalLinePageViewModel page && page.PageId == id);
            box?.BringIntoView(); box?.Focus();
        }));
    }
    private void RemoveSelected_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is CanonicalNodeInspectorViewModel vm) vm.RemoveSelectedLinePages();
        e.Handled = true;
    }
    private void SelectPageAtPointer(CanonicalLinePageViewModel page, DependencyObject? source, MouseButtonEventArgs e)
    {
        var button = source is Button direct ? direct : source is null ? null : Ancestor<Button>(source);
        if (button is not null && EntryReorder.GetIsHandle(button)) return;
        var modifiers = Keyboard.Modifiers;
        page.Owner.SelectLinePage(page.PageId, modifiers.HasFlag(ModifierKeys.Control), modifiers.HasFlag(ModifierKeys.Shift));
        // Keep modifier selection from activating fields or collapsing the selected rows.
        if ((modifiers & (ModifierKeys.Control | ModifierKeys.Shift)) != 0) e.Handled = true;
        // Card padding and border are selection-only surfaces. Claim them before the canvas starts dragging.
        if (Ancestor<Control>(source) is ItemsControl || source is Border { Name: "LinePageCard" }) e.Handled = true;
    }
    private void Toggle_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is not CanonicalLinePageViewModel page) return;
        if (!page.IsSelected) page.Owner.SelectLinePage(page.PageId);
        page.IsExpanded = !page.IsExpanded;
        e.Handled = true;
    }
    private void RemoveVoice_OnClick(object sender, RoutedEventArgs e) { if ((sender as FrameworkElement)?.DataContext is CanonicalLinePageViewModel page) page.SetVoice(null); }
    private void Volume_OnMouseUp(object sender, MouseButtonEventArgs e) => ((sender as FrameworkElement)?.DataContext as CanonicalLinePageViewModel)?.CommitVolume();
    private void Volume_OnLostFocus(object sender, KeyboardFocusChangedEventArgs e) => ((sender as FrameworkElement)?.DataContext as CanonicalLinePageViewModel)?.CommitVolume();
    private void Volume_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Enter or Key.Escape)) return;
        ((sender as FrameworkElement)?.DataContext as CanonicalLinePageViewModel)?.CommitVolume(e.Key == Key.Enter); e.Handled = true;
    }
    private async void Import_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: CanonicalLinePageViewModel page } button) return;
        var workspace = Ancestor<CanonicalStoryWorkspaceView>(this)?.Workspace;
        if (workspace?.MediaProjectDirectory is not { } root || !workspace.IsWritableEditor(workspace.ActiveEditor)) return;
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "音频文件|*.mp3;*.wav;*.ogg", CheckFileExists = true };
        if (dialog.ShowDialog(Window.GetWindow(this)) != true) return;
        button.IsEnabled = false;
        try
        {
            var tools = System.IO.Path.Combine(AppContext.BaseDirectory, "media-tools", "ffmpeg");
            var store = new Core.Media.ProjectMediaStore(root, System.IO.Path.Combine(tools, "ffmpeg.exe"), System.IO.Path.Combine(tools, "ffprobe.exe"));
            var result = await store.ImportAudioAsync(dialog.FileName);
            if (workspace.MediaProjectDirectory == root && page.Owner.LinePages.Contains(page)) page.SetVoice(result.MediaRef);
        }
        catch (Exception error) { MessageBox.Show(Window.GetWindow(this), error.Message, "音频导入失败", MessageBoxButton.OK, MessageBoxImage.Error); }
        finally { button.IsEnabled = true; }
    }
    internal static T? Ancestor<T>(DependencyObject? value) where T : DependencyObject
    {
        while (value is not null) { if (value is T result) return result; value = InputParent(value); }
        return null;
    }
    private static DependencyObject? InputParent(DependencyObject value) => value is FrameworkContentElement content
        ? content.Parent : value is Visual || value is System.Windows.Media.Media3D.Visual3D
            ? VisualTreeHelper.GetParent(value) : LogicalTreeHelper.GetParent(value);
    internal static IEnumerable<T> Children<T>(DependencyObject value) where T : DependencyObject
    {
        for (var i = 0; i < VisualTreeHelper.GetChildrenCount(value); i++)
        {
            var child = VisualTreeHelper.GetChild(value, i);
            if (child is T result) yield return result;
            foreach (var descendant in Children<T>(child)) yield return descendant;
        }
    }
}
