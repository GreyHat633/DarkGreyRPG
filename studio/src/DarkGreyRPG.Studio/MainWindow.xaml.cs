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

public partial class MainWindow : Window
{
    private readonly DispatcherTimer _toastTimer = new();
    private readonly ISettingsService _settingsService;
    private readonly ShellViewModel _shell;
    private GridLength _resourceBrowserWidth = new(260);

    public MainWindow(ThemeSettingsViewModel themeSettings, ISettingsService settingsService)
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
            new FlowWorkspaceDialogs(() => this));
        DataContext = _shell;
        _shell.Toast.PropertyChanged += Toast_OnPropertyChanged;
        _shell.PropertyChanged += Shell_OnPropertyChanged;
        _toastTimer.Tick += ToastTimer_OnTick;
        Closing += MainWindow_OnClosing;
        ApplyPersistedSettings(themeSettings.CurrentSettings);
    }

    public ThemeSettingsViewModel ThemeSettings { get; }

    private void HelpCommand_OnExecuted(object sender, ExecutedRoutedEventArgs e) =>
        MessageBox.Show(
            this,
            "DarkGrey RPG Studio 2.1.2\nStory-first authoring with unified Flow and read-only Story Graph interactions",
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
            "新建剧情",
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

        var openItem = FluentContextMenuFactory.CreateItem("打开剧情", () => shell.OpenStory(story));
        var deleteItem = FluentContextMenuFactory.CreateItem(
            "删除剧情",
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
        if (e.PropertyName == nameof(ShellViewModel.IsResourceBrowserVisible) && sender is ShellViewModel shell)
        {
            ApplyResourceBrowserVisibility(shell.IsResourceBrowserVisible);
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
