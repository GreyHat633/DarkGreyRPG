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
    [ThreadStatic] private static HashSet<LinePagesEditor>? _liveEditors;
    private static HashSet<LinePagesEditor> LiveEditors => _liveEditors ??= new();
    [ThreadStatic] private static WeakReference<DynamicContentEditor>? _activeText;
    private DynamicContentEditor? ActiveText => _activeText is not null && _activeText.TryGetTarget(out var text)
        && text.IsLoaded && !text.IsReadOnly && text.DataContext is CanonicalLinePageViewModel page
        && DataContext is CanonicalNodeInspectorViewModel owner && ReferenceEquals(page.Owner.Host, owner.Host)
        && page.Owner.NodeId == owner.NodeId && owner.LinePages.Any(p => p.PageId == page.PageId) ? text : null;
    private static void SetActiveText(DynamicContentEditor? text)
    {
        _activeText = text is null ? null : new(text);
        foreach (var editor in LiveEditors) editor.DynamicInsert.IsEnabled = editor.ActiveText is not null;
    }
    internal static void ForgetDynamicTarget(DynamicContentEditor text)
    {
        if (_activeText is not null && _activeText.TryGetTarget(out var active) && ReferenceEquals(active, text)) SetActiveText(null);
    }
    private void DynamicInsert_OnClick(object sender, RoutedEventArgs e)
    {
        ActiveText?.ShowPicker(DynamicInsert);
    }
    public LinePagesEditor()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            DetachWindow();
            _inputWindow = Window.GetWindow(this);
            _inputWindow?.AddHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(Window_OnMouseDown), true);
            _inputWindow?.AddHandler(Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(Window_OnFocus), true);
            LiveEditors.Add(this);
            DynamicInsert.IsEnabled = ActiveText is not null;
        };
        AddHandler(Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler((_, e) =>
        {
            var text = Ancestor<DynamicContentEditor>(e.NewFocus as DependencyObject);
            if (text is not null && text.DataContext is CanonicalLinePageViewModel && text.Body.IsKeyboardFocusWithin) SetActiveText(text);
        }), true);
        Unloaded += (_, _) =>
        {
            // IsLoaded is already false here; clear the shared target before removing its owner.
            if (_activeText is not null && _activeText.TryGetTarget(out var text)
                && ReferenceEquals(Ancestor<LinePagesEditor>(text), this)) SetActiveText(null);
            LiveEditors.Remove(this);
            DetachWindow();
        };
        DataContextChanged += (_, e) =>
        {
            if (e.OldValue is CanonicalNodeInspectorViewModel old && !ReferenceEquals(e.OldValue, e.NewValue))
            {
                old.ClearLinePageSelection();
                if (_activeText is not null && _activeText.TryGetTarget(out var text) && text.DataContext is CanonicalLinePageViewModel page && ReferenceEquals(page.Owner, old)) SetActiveText(null);
            }
        };
    }
    private void FocusPage(CanonicalNodeInspectorViewModel owner, string id, bool atEnd)
    {
        var page = owner.LinePages.FirstOrDefault(p => p.PageId == id);
        if (page is null) return;
        page.IsExpanded = true;
        owner.SelectLinePage(id);
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            if (!ReferenceEquals(DataContext, owner)) return;
            var editor = Children<DynamicContentEditor>(Pages).FirstOrDefault(b =>
                System.Windows.Automation.AutomationProperties.GetAutomationId(b) == "LinePageText"
                && b.DataContext is CanonicalLinePageViewModel p && p.PageId == id);
            if (editor is null) return;
            editor.BringIntoView(); editor.Body.Focus(); editor.Body.CaretPosition = atEnd
                ? editor.Body.Document.ContentEnd : editor.Body.Document.ContentStart;
        }));
    }
    private void DynamicText_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not DynamicContentEditor { DataContext: CanonicalLinePageViewModel page } editor || editor.IsReadOnly) return;
        if (Keyboard.Modifiers == ModifierKeys.Control && e.Key is Key.Z or Key.Y)
        {
            var before = page.Owner.LinePages.Select(p => p.PageId).ToArray();
            var index = Array.IndexOf(before, page.PageId);
            var changed = e.Key == Key.Z ? page.Owner.Host.Undo() : page.Owner.Host.Redo();
            if (changed)
            {
                var target = page.Owner.LinePages.FirstOrDefault(p => !before.Contains(p.PageId))
                    ?? page.Owner.LinePages.FirstOrDefault(p => p.PageId == page.PageId)
                    ?? page.Owner.LinePages.ElementAtOrDefault(Math.Max(0, index - 1));
                if (target is not null) FocusPage(page.Owner, target.PageId, true);
            }
            e.Handled = true; return;
        }
        if (Keyboard.Modifiers != ModifierKeys.None) return;
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            if (!e.IsRepeat && page.Owner.InsertLinePageAfter(page.PageId) is { } id) FocusPage(page.Owner, id, false);
        }
        else if (e.Key == Key.Back && editor.Text.Length == 0)
        {
            e.Handled = true;
            if (e.IsRepeat) return;
            editor.Commit();
            var index = page.Owner.LinePages.IndexOf(page);
            var target = index > 0 ? page.Owner.LinePages[index - 1] : page.Owner.LinePages.Skip(1).FirstOrDefault();
            if (target is not null && page.Owner.RemoveEmptyLinePage(page.PageId)) FocusPage(page.Owner, target.PageId, index > 0);
        }
    }
    private void DetachWindow()
    {
        _inputWindow?.RemoveHandler(Mouse.PreviewMouseDownEvent, new MouseButtonEventHandler(Window_OnMouseDown));
        _inputWindow?.RemoveHandler(Keyboard.GotKeyboardFocusEvent, new KeyboardFocusChangedEventHandler(Window_OnFocus));
        _inputWindow = null;
    }
    private void Window_OnFocus(object sender, KeyboardFocusChangedEventArgs e)
    {
        if (ActiveText is not { } active || active.IsPickerOpen) return;
        var source = e.NewFocus as DependencyObject;
        if (Ancestor<DynamicContentEditor>(source) is { DataContext: CanonicalLinePageViewModel } text && text.Body.IsKeyboardFocusWithin)
        { SetActiveText(text); return; }
        if (Ancestor<Button>(source) != DynamicInsert) SetActiveText(null);
    }
    private void Window_OnMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ChangedButton != MouseButton.Left || DataContext is not CanonicalNodeInspectorViewModel owner) return;
        var source = e.OriginalSource as DependencyObject;
        if (ActiveText is { } active && active.IsPickerOpen && DynamicContentEditor.IsResourceSource(source)) return;
        var editor = Ancestor<LinePagesEditor>(source);
        if (editor?.DataContext is not CanonicalNodeInspectorViewModel target ||
            !ReferenceEquals(owner.Host, target.Host) || owner.NodeId != target.NodeId)
        {
            owner.ClearLinePageSelection();
            if (ActiveText is not null) SetActiveText(null);
            return;
        }
        if (!ReferenceEquals(editor, this)) return;
        if (ActiveText is not null && Ancestor<DynamicContentEditor>(source) is null && Ancestor<Button>(source) != DynamicInsert)
            SetActiveText(null);
        for (var current = source; current is not null && !ReferenceEquals(current, this); current = InputParent(current))
            if (current is Border { Name: "LinePageCard", DataContext: CanonicalLinePageViewModel page })
            {
                SelectPageAtPointer(page, source, e);
                return;
            }
        var action = Ancestor<Button>(source);
        if (action is null || System.Windows.Automation.AutomationProperties.GetAutomationId(action)
            is not ("AddLinePage" or "RemoveSelectedLinePages" or "InsertLineDynamicContent")) owner.ClearLinePageSelection();
    }
    private void Add_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CanonicalNodeInspectorViewModel vm || vm.AddLinePage() is not { } id) return;
        FocusPage(vm, id, false);
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
            var tools = Services.StudioStoragePaths.Default.MediaTools;
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
    internal static DependencyObject? InputParent(DependencyObject value) => value is FrameworkContentElement content
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
