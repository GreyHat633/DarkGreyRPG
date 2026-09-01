using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Threading;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.Settings;
using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.Views;

namespace DarkGreyRPG.Studio;

public partial class MainWindow : Window, INotifyPropertyChanged
{
    private readonly DispatcherTimer _toastTimer = new();
    private readonly ISettingsService _settingsService;
    private readonly ShellViewModel _shell;
    private GridLength _resourceBrowserWidth = new(260);
    private GridLength _storyResourceLibraryWidth = new(StudioSettings.DefaultStoryResourceLibraryWidth);

    public MainWindow(
        ThemeSettingsViewModel themeSettings,
        ISettingsService settingsService,
        ICrashLogService? crashLogService = null)
    {
        ThemeSettings = themeSettings ?? throw new ArgumentNullException(nameof(themeSettings));
        _settingsService = settingsService ?? throw new ArgumentNullException(nameof(settingsService));
        InitializeComponent();
        _shell = new ShellViewModel(
            new ProjectService(),
            new ProjectFolderPicker(),
            new ActorWorkspaceDialogs(() => this),
            new ProjectWorkspaceDialogs(() => this),
            new ResourceWorkspaceDialogs(() => this),
            new FlowWorkspaceDialogs(() => this),
            crashLogService ?? new CrashLogService(settingsService.SettingsPath),
            new CanonicalStoryResourceDialogs(() => this),
            itemWorkspaceDialogs: new ItemWorkspaceDialogs(() => this));
        DataContext = _shell;
        _shell.Toast.PropertyChanged += Toast_OnPropertyChanged;
        _shell.PropertyChanged += Shell_OnPropertyChanged;
        _toastTimer.Tick += ToastTimer_OnTick;
        Closing += MainWindow_OnClosing;
        ApplyPersistedSettings(themeSettings.CurrentSettings);
    }

    public ThemeSettingsViewModel ThemeSettings { get; }

    public ShellViewModel Shell => _shell;

    public IReadOnlyDictionary<string, string?> GetCrashLogDetails()
    {
        var details = new Dictionary<string, string?>(_shell.GetCrashLogDetails(), StringComparer.Ordinal)
        {
            ["Theme"] = ThemeSettings.SelectedTheme.ToString(),
        };
        try
        {
            var dpi = PresentationSource.FromVisual(this)?.CompositionTarget?.TransformToDevice.M11;
            details["DPI"] = dpi is > 0
                ? $"{dpi.Value:0.###}x"
                : "(unavailable)";
        }
        catch (Exception)
        {
            details["DPI"] = "(unavailable)";
        }

        return details;
    }

    public GridLength StoryResourceLibraryWidth
    {
        get => _storyResourceLibraryWidth;
        set
        {
            var width = value.IsAbsolute ? Math.Clamp(value.Value, StudioSettings.StoryResourceLibraryMinWidth, StudioSettings.StoryResourceLibraryMaxWidth) : StudioSettings.DefaultStoryResourceLibraryWidth;
            if (Math.Abs(_storyResourceLibraryWidth.Value - width) < 0.1)
            {
                return;
            }

            _storyResourceLibraryWidth = new GridLength(width);
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(StoryResourceLibraryWidth)));
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void MainWindow_OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.S || !Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;

        // LostFocus bindings otherwise run after the Ctrl+S command. Commit the
        // currently edited value first so the persistence snapshot cannot lag
        // one keystroke behind what the author can still see in the editor.
        FlushFocusedDraft(Keyboard.FocusedElement);
    }

    internal static void FlushFocusedDraft(IInputElement? focusedElement)
    {
        if (focusedElement is TextBox textBox)
            textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
    }

    private void HelpCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e) =>
        MessageBox.Show(
            this,
            "DarkGrey RPG Studio 0.3.1.5 RC\nStory packages, real entity/item identities, and server-authoritative RPG runtime",
            "关于",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

    private void CloseCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e) => Close();

    private void NewCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (DataContext is ShellViewModel shell && shell.NewProjectCommand.CanExecute(null))
        {
            shell.NewProjectCommand.Execute(null);
        }
    }

    private void FindCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e)
    {
        if (DataContext is not ShellViewModel shell)
        {
            return;
        }

        if (shell.HasCanonicalStoryWorkspace)
        {
            return;
        }

        shell.Navigation.SelectedItem = shell.Navigation.Items.Single(item => item.Page == "Story");
        if (!shell.IsResourceBrowserVisible)
        {
            shell.ToggleResourceBrowserCommand.Execute(null);
        }

        StorySearchBox.Focus();
        StorySearchBox.SelectAll();
    }

    private void StoryList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ShellViewModel shell &&
            ((sender as ListBox)?.SelectedItem ?? shell.ProjectHome.SelectedStory) is StoryListItemViewModel story)
        {
            shell.OpenStory(story);
            e.Handled = true;
        }
    }

    private void StoryList_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not ShellViewModel shell) return;

        var item = FindVisualAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        var menuTarget = item ?? sender as UIElement ?? StoryList;
        var menu = FluentContextMenuFactory.Create(menuTarget);
        menu.Items.Add(FluentContextMenuFactory.CreateItem(
            "新建故事",
            () => shell.CreateStoryCommand.Execute(null),
            shell.CreateStoryCommand.CanExecute(null)));

        if (item?.DataContext is not StoryListItemViewModel story)
        {
            StoryList.ContextMenu = menu;
            menu.IsOpen = true;
            e.Handled = true;
            return;
        }

        shell.ProjectHome.SelectedStory = story;
        item.IsSelected = true;
        item.Focus();

        var openItem = FluentContextMenuFactory.CreateItem("打开故事", () => shell.OpenStory(story));
        var deleteItem = FluentContextMenuFactory.CreateItem(
            "删除故事",
            action: null,
            enabled: shell.DeleteSelectedStoryCommand.CanExecute(null),
            critical: true);
        deleteItem.Command = shell.DeleteSelectedStoryCommand;

        menu.Items.Add(FluentContextMenuFactory.CreateSeparator());
        menu.Items.Add(openItem);
        menu.Items.Add(FluentContextMenuFactory.CreateSeparator());
        menu.Items.Add(deleteItem);
        item.ContextMenu = menu;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private void StoryResourceList_OnPreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is not ShellViewModel shell || sender is not ListBox list)
            return;

        var item = FindVisualAncestor<ListBoxItem>(e.OriginalSource as DependencyObject);
        if (item?.DataContext is not StoryResourceMembershipViewModel resource)
            return;

        list.SelectedItem = resource;
        shell.SelectedStoryResource = resource;
        item.IsSelected = true;
        item.Focus();

        var menu = FluentContextMenuFactory.Create(item);
        menu.Items.Add(FluentContextMenuFactory.CreateItem(
            "打开资源",
            () => shell.SelectedStoryResource = resource));
        menu.Items.Add(FluentContextMenuFactory.CreateSeparator());
        menu.Items.Add(FluentContextMenuFactory.CreateItem(
            shell.SelectedStoryResourceActionText,
            () => shell.DeleteStoryResourceCommand.Execute(null),
            shell.DeleteStoryResourceCommand.CanExecute(null),
            critical: true));
        item.ContextMenu = menu;
        menu.IsOpen = true;
        e.Handled = true;
    }

    private static T? FindVisualAncestor<T>(DependencyObject? source) where T : DependencyObject
    {
        while (source is not null)
        {
            if (source is T match) return match;
            source = VisualTreeHelper.GetParent(source);
        }
        return null;
    }

    private void Shell_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.EffectiveResourceBrowserVisible) && sender is ShellViewModel shell)
        {
            ApplyResourceBrowserVisibility(shell.EffectiveResourceBrowserVisible);
        }
    }

    private void ApplyResourceBrowserVisibility(bool isVisible)
    {
        if (!isVisible)
        {
            if (ResourceBrowserColumn.ActualWidth >= 200)
            {
                _resourceBrowserWidth = new GridLength(ResourceBrowserColumn.ActualWidth);
            }

            ResourceBrowserColumn.Width = new GridLength(0);
            ResourceBrowser.Visibility = Visibility.Collapsed;
            ResourceBrowserSplitter.Visibility = Visibility.Collapsed;
            return;
        }

        ResourceBrowserColumn.Width = _resourceBrowserWidth;
        ResourceBrowser.Visibility = Visibility.Visible;
        ResourceBrowserSplitter.Visibility = Visibility.Visible;
    }

    private void MainWindow_OnClosing(object? sender, CancelEventArgs e)
    {
        FlushFocusedDraft(Keyboard.FocusedElement);
        if (!_shell.TryClose())
        {
            e.Cancel = true;
            return;
        }

        try
        {
            var bounds = WindowState == WindowState.Normal
                ? new Rect(Left, Top, ActualWidth, ActualHeight)
                : RestoreBounds;
            var browserWidth = ResourceBrowserColumn.ActualWidth >= 200
                ? ResourceBrowserColumn.ActualWidth
                : _resourceBrowserWidth.Value;
            _settingsService.Save(ThemeSettings.CurrentSettings with
            {
                WindowWidth = bounds.Width,
                WindowHeight = bounds.Height,
                WindowMaximized = WindowState == WindowState.Maximized,
                ResourceBrowserWidth = browserWidth,
                StoryResourceLibraryWidth = StoryResourceLibraryWidth.Value,
                BottomPanelHeight = _shell.BottomPanel.ExpandedHeight,
                LastProject = _shell.HasProject ? _shell.ProjectDirectory : null,
                RecentProjects = _shell.RecentProjectDirectories,
            });
        }
        catch (SettingsPersistenceException exception)
        {
            _shell.Output.Append(exception.Message, OutputKind.Error, "Settings");
        }
    }

    private void ApplyPersistedSettings(StudioSettings settings)
    {
        var workArea = SystemParameters.WorkArea;
        var sizeIsSafe = settings.WindowWidth >= MinWidth && settings.WindowHeight >= MinHeight &&
                         settings.WindowWidth <= workArea.Width && settings.WindowHeight <= workArea.Height;
        Width = sizeIsSafe ? settings.WindowWidth : Math.Min(StudioSettings.DefaultWindowWidth, workArea.Width);
        Height = sizeIsSafe ? settings.WindowHeight : Math.Min(StudioSettings.DefaultWindowHeight, workArea.Height);
        _resourceBrowserWidth = new GridLength(Math.Clamp(settings.ResourceBrowserWidth, 180, 400));
        ResourceBrowserColumn.Width = _resourceBrowserWidth;
        StoryResourceLibraryWidth = new GridLength(settings.StoryResourceLibraryWidth);
        _shell.BottomPanel.ExpandedHeight = Math.Clamp(
            settings.BottomPanelHeight,
            BottomPanelViewModel.MinimumExpandedHeight,
            BottomPanelViewModel.MaximumExpandedHeight);
        if (settings.WindowMaximized)
        {
            WindowState = WindowState.Maximized;
        }

        _shell.SetRecentProjects(settings.RecentProjects);
        _shell.RestoreLastProject(settings.LastProject);
        ApplyResourceBrowserVisibility(_shell.EffectiveResourceBrowserVisible);
    }

    private void BottomPanelSplitter_OnDragCompleted(object sender, DragCompletedEventArgs e)
    {
        var expandedHeight = Math.Clamp(
            _shell.BottomPanel.ExpandedHeight - e.VerticalChange,
            BottomPanelViewModel.MinimumExpandedHeight,
            BottomPanelViewModel.MaximumExpandedHeight);

        Dispatcher.BeginInvoke(
            () =>
            {
                _shell.BottomPanel.ExpandedHeight = expandedHeight;
                ResetBottomPanelGridRows();
            },
            DispatcherPriority.Loaded);
    }

    private void ResetBottomPanelGridRows()
    {
        WorkspaceRow.Height = new GridLength(1, GridUnitType.Star);
        BottomPanelRow.Height = GridLength.Auto;
    }

    private void BottomPanelTabBar_OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.OriginalSource is not DependencyObject source ||
            ItemsControl.ContainerFromElement((ItemsControl)sender, source) is not ListBoxItem item ||
            item.DataContext is not BottomPanelTab tab)
        {
            return;
        }

        _shell.BottomPanel.SelectTab(tab);
    }

    private void BottomPanelTabBar_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        _shell.BottomPanel.Toggle();
        e.Handled = true;
    }

    private void ProblemList_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (DataContext is ShellViewModel shell && ProblemList.SelectedItem is ProblemItem problem)
        {
            shell.OpenProblem(problem);
        }
    }

    private void DismissToast_OnClick(object sender, RoutedEventArgs e)
    {
        _toastTimer.Stop();
        if (DataContext is ShellViewModel shell)
        {
            shell.Toast.Dismiss();
        }
    }

    private void Toast_OnPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ToastViewModel.IsVisible) || sender is not ToastViewModel toast)
        {
            return;
        }

        _toastTimer.Stop();
        if (toast.IsVisible)
        {
            _toastTimer.Interval = toast.Kind == ToastKind.Error
                ? TimeSpan.FromSeconds(8)
                : TimeSpan.FromSeconds(4);
            _toastTimer.Start();
        }
    }

    private void ToastTimer_OnTick(object? sender, EventArgs e)
    {
        _toastTimer.Stop();
        if (DataContext is ShellViewModel shell)
        {
            shell.Toast.Dismiss();
        }
    }
}
