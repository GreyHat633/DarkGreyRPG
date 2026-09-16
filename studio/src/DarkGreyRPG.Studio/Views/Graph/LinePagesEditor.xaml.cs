using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using System.Windows.Input;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Views.Graph;

public partial class LinePagesEditor : UserControl
{
    public LinePagesEditor() => InitializeComponent();
    private void Add_OnClick(object sender, RoutedEventArgs e)
    {
        if (DataContext is not CanonicalNodeInspectorViewModel vm || vm.AddLinePage() is not { } id) return;
        Dispatcher.BeginInvoke(DispatcherPriority.Loaded, new Action(() =>
        {
            var box = Children<TextBox>(Pages).FirstOrDefault(b => b.DataContext is CanonicalLinePageViewModel page && page.PageId == id);
            box?.BringIntoView(); box?.Focus();
        }));
    }
    private void Remove_OnClick(object sender, RoutedEventArgs e) { if ((sender as FrameworkElement)?.DataContext is CanonicalLinePageViewModel page) page.Remove(); }
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
        while (value is not null) { if (value is T result) return result; value = VisualTreeHelper.GetParent(value); }
        return null;
    }
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
