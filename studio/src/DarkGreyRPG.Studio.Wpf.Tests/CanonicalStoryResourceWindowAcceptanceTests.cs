using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Settings;
using DarkGreyRPG.Studio.ViewModels;

#pragma warning disable WPF0001

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
[DoNotParallelize]
public sealed class CanonicalStoryResourceWindowAcceptanceTests
{
    [STATestMethod]
    public void MetadataWindowsAndHomeNavigationRenderWhenRequested()
    {
        var root = Environment.GetEnvironmentVariable("DGRPG_METADATA_UI_ROOT");
        if (string.IsNullOrEmpty(root)) return;
        Environment.SetEnvironmentVariable("DARKGREYRPG_STUDIO_SETTINGS_PATH", Path.Combine(root, "settings.json"));
        File.AppendAllText(Path.Combine(root, "ui-progress.txt"), "start\n");
        var app = System.Windows.Application.Current;
        if (app is null) { var created = new App(); created.InitializeComponent(); app = created; }
        var settings = new SettingsService(Path.Combine(root, "settings.json"));
        var theme = new ThemeSettingsViewModel(settings, _ => app.ThemeMode = System.Windows.ThemeMode.Dark);
        app.ThemeMode = System.Windows.ThemeMode.Dark;
        var window = new MainWindow(theme, settings) { WindowState = System.Windows.WindowState.Normal, Width = 1536, Height = 824 };
        try
        {
            File.AppendAllText(Path.Combine(root, "ui-progress.txt"), "show\n");
            window.Show();
            File.AppendAllText(Path.Combine(root, "ui-progress.txt"), "idle\n");
            Idle(window);
            File.AppendAllText(Path.Combine(root, "ui-progress.txt"), "ready\n");
            var shell = window.Shell;
            var folders = Descendants<System.Windows.Controls.Expander>(window)
                .Where(folder => folder.DataContext is HomeStoryResourceFolder).ToArray();
            Assert.HasCount(4, folders);
            Assert.IsTrue(folders.All(folder => !folder.IsExpanded));
            SaveScreenshot(window, Path.Combine(root, "home-collapsed.png"));
            foreach (var folder in folders) folder.IsExpanded = folder.IsEnabled;
            Idle(window);
            SaveScreenshot(window, Path.Combine(root, "home-expanded.png"));
            var sessionRow = Descendants<System.Windows.Controls.ListBoxItem>(window).Single(item =>
                item.DataContext is HomeStoryResourceRow { Id: "A:session" });
            sessionRow.RaiseEvent(new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Left)
                { RoutedEvent = System.Windows.Controls.Control.MouseDoubleClickEvent });
            Idle(window);
            Assert.AreEqual("A:session", shell.CanonicalStoryWorkspace!.ActiveEditor.Id);
            Assert.AreEqual("测试、可编辑", shell.CanonicalStoryWorkspace.InspectorTagsText);
            StringAssert.Contains(shell.CanonicalStoryWorkspace.InspectorIdentityText, "Session_ID");
            SaveScreenshot(window, Path.Combine(root, "session-inspector.png"));
            foreach (var resource in shell.CanonicalStoryWorkspace.SessionItems.Concat(shell.CanonicalStoryWorkspace.TaskItems))
            {
                var target = Descendants<System.Windows.Controls.Button>(window).Single(button => ReferenceEquals(button.Tag, resource));
                var view = Descendants<DarkGreyRPG.Studio.Views.Graph.CanonicalStoryWorkspaceView>(window).Single();
                typeof(DarkGreyRPG.Studio.Views.Graph.CanonicalStoryWorkspaceView).GetMethod("ResourceItem_OnPreviewMouseRightButtonDown", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!
                    .Invoke(view, [target, new System.Windows.Input.MouseButtonEventArgs(System.Windows.Input.Mouse.PrimaryDevice, 0, System.Windows.Input.MouseButton.Right) { RoutedEvent = System.Windows.UIElement.PreviewMouseRightButtonDownEvent }]);
                Idle(window);
                var menu = target.ContextMenu;
                Assert.IsNotNull(menu);
                var entries = menu.Items.OfType<System.Windows.Controls.MenuItem>().ToArray();
                Assert.AreEqual(resource.ResourceKind == GraphResourceKind.Session ? "会话图" : "任务图", entries[0].Header);
                Assert.AreEqual("编辑", entries[1].Header);
                Assert.AreEqual(!resource.IsReadOnly, entries[1].IsEnabled);
                Assert.IsTrue(entries.All(entry => double.IsNaN(entry.Height)));
                SaveVisual(menu, Path.Combine(root, $"menu-{resource.ResourceKind}-{resource.IsReadOnly}.png"));
                menu.IsOpen = false;
            }
            foreach (var label in new[] { "角色", "角色组", "物品", "物品组", "会话", "任务" })
            {
                var vm = new DisplayNameDialogViewModel(label, "Author:LongResourceIdentifier_CaseSensitive", "较长的资源显示名称用于检查编辑窗口布局", true, ["现有标签", "Tag"]);
                var dialog = new DarkGreyRPG.Studio.Views.DisplayNameDialog(vm) { Owner = window };
                dialog.Show(); Idle(dialog);
                Assert.AreEqual("编辑" + label, vm.Title);
                Assert.HasCount(3, Descendants<System.Windows.Controls.TextBox>(dialog).ToArray());
                CollectionAssert.AreEqual(new[] { "现有标签", "Tag" }, vm.Tags.ToArray());
                SaveScreenshot(dialog, Path.Combine(root, "edit-" + label + ".png"));
                vm.TagsText = string.Empty;
                Assert.AreEqual(0, vm.Tags.Count);
                dialog.Close();
            }
            foreach (var kind in new[] { GraphResourceKind.Session, GraphResourceKind.Task })
            {
                var vm = CanonicalResourceIdentityDialogViewModel.ForCreate(kind, "A:NewResource");
                vm.TagsText = "新标签，Tag; Other";
                var dialog = new DarkGreyRPG.Studio.Views.CanonicalResourceIdentityDialog(vm) { Owner = window };
                dialog.Show(); Idle(dialog);
                CollectionAssert.AreEqual(new[] { "新标签", "Tag", "Other" }, vm.Tags.ToArray());
                SaveScreenshot(dialog, Path.Combine(root, "create-" + kind + ".png"));
                dialog.Close();
            }
        }
        finally { window.Close(); }
    }

    private static void Idle(System.Windows.Window window)
    {
        var frame = new System.Windows.Threading.DispatcherFrame();
        var timer = new System.Windows.Threading.DispatcherTimer(System.Windows.Threading.DispatcherPriority.Send) { Interval = TimeSpan.FromMilliseconds(200) };
        timer.Tick += (_, _) => { timer.Stop(); frame.Continue = false; };
        timer.Start(); System.Windows.Threading.Dispatcher.PushFrame(frame);
        window.UpdateLayout();
    }

    private static IEnumerable<T> Descendants<T>(System.Windows.DependencyObject root) where T : System.Windows.DependencyObject
    {
        if (root is T match) yield return match;
        for (var index = 0; index < VisualTreeHelper.GetChildrenCount(root); index++)
            foreach (var child in Descendants<T>(VisualTreeHelper.GetChild(root, index))) yield return child;
    }

    private static void SaveVisual(System.Windows.FrameworkElement visual, string path)
    {
        var bitmap = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(visual.ActualWidth)), Math.Max(1, (int)Math.Ceiling(visual.ActualHeight)), 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(visual);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(path); encoder.Save(stream);
    }

    [STATestMethod]
    public void MainWindowRendersCanonicalResourceCommandSurfaceWhenCaptureIsRequested()
    {
        var screenshotPath = Environment.GetEnvironmentVariable("DGRPG_CANONICAL_SCREENSHOT_PATH");
        var projectGraphScreenshotPath = Environment.GetEnvironmentVariable("DGRPG_CANONICAL_PROJECT_GRAPH_SCREENSHOT_PATH");
        if (string.IsNullOrWhiteSpace(screenshotPath) && string.IsNullOrWhiteSpace(projectGraphScreenshotPath)) return;

        using var fixture = new Fixture();
        var settingsPath = Path.Combine(fixture.Root, "studio-settings.json");
        var settings = new SettingsService(settingsPath);
        settings.Save(new StudioSettings
        {
            Theme = ThemePreference.Dark,
            WindowWidth = 1360,
            WindowHeight = 820,
            LastProject = fixture.Root,
            RecentProjects = [fixture.Root],
        });
        var application = System.Windows.Application.Current;
        if (application is null)
        {
            var studioApplication = new DarkGreyRPG.Studio.App();
            studioApplication.InitializeComponent();
            application = studioApplication;
        }
        var theme = new ThemeSettingsViewModel(settings, preference =>
            application.ThemeMode = preference switch
            {
                ThemePreference.Light => System.Windows.ThemeMode.Light,
                ThemePreference.Dark => System.Windows.ThemeMode.Dark,
                _ => System.Windows.ThemeMode.System,
            });
        var window = new DarkGreyRPG.Studio.MainWindow(theme, settings)
        {
            Width = 1360,
            Height = 820,
            WindowStartupLocation = System.Windows.WindowStartupLocation.Manual,
            Left = 40,
            Top = 40,
        };
        try
        {
            window.Show();
            if (!string.IsNullOrWhiteSpace(projectGraphScreenshotPath))
            {
                window.Shell.ShowProjectGraphCommand.Execute(null);
                window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
                window.Shell.Toast.Dismiss();
                window.UpdateLayout();
                Assert.IsTrue(window.Shell.ProjectHome.IsGraphVisible);
                var edge = window.Shell.ProjectHome.Graph.Edges.Single(item =>
                    item.SourceStoryId == "graph_source" && item.TargetStoryId == "graph_target");
                Assert.AreEqual("to_target", edge.Transitions.Single().NodeId);
                Assert.IsTrue(window.Shell.ProjectHome.Graph.Nodes.Any(node => node.Id == "graph_source"));
                SaveScreenshot(window, projectGraphScreenshotPath);
                return;
            }

            window.Shell.OpenStory(
                window.Shell.ProjectHome.Stories.Single(story => story.Id == "opening"));
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            window.Shell.Toast.Dismiss();
            window.UpdateLayout();

            Assert.IsTrue(window.Shell.HasCanonicalStoryWorkspace);
            var workspace = window.Shell.CanonicalStoryWorkspace!;
            Assert.AreEqual("Opening Session", workspace.SessionItems.Single().DisplayName);
            Assert.AreEqual("Opening Task", workspace.TaskItems.Single().DisplayName);
            Assert.IsTrue(workspace.PlaceAggregate(workspace.SessionItems.Single(), "opening-session-placement", 300, 130));
            Assert.IsTrue(workspace.PlaceAggregate(workspace.TaskItems.Single(), "opening-task-placement", 300, 360));
            Assert.HasCount(3, workspace.StoryEditor.Host.Graph.Nodes);
            window.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            window.UpdateLayout();

            SaveScreenshot(window, screenshotPath!);
        }
        finally
        {
            window.Close();
        }
    }

    private static void SaveScreenshot(System.Windows.Window window, string screenshotPath)
    {
        var width = Math.Max(1, (int)Math.Ceiling(window.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(window.ActualHeight));
        var bitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(window);
        var directory = Path.GetDirectoryName(Path.GetFullPath(screenshotPath));
        Assert.IsFalse(string.IsNullOrWhiteSpace(directory));
        Directory.CreateDirectory(directory!);
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var stream = File.Create(screenshotPath);
        encoder.Save(stream);
    }

    private sealed class Fixture : IDisposable
    {
        public Fixture()
        {
            Root = Path.Combine(
                AppContext.BaseDirectory,
                "temp",
                "canonical-window-" + Guid.NewGuid().ToString("N"));
            var project = new ProjectService();
            project.CreateProject(Root, "acceptance_project", "0.3.0.0 Acceptance");
            var store = new CanonicalProjectGraphStore(Root);
            var storyStart = GraphNodeFactory.Create(GraphScope.StoryFlow, "start", "start");
            storyStart.Ports.Add(new GraphPort("interact", "Interact", false, GraphInterfaceKind.Flow, 0));
            store.Stories.Create(new GraphResourceEnvelope(
                GraphResourceKind.Story,
                "opening",
                "Canonical Opening",
                new GraphDocument([storyStart])));

            var enterStory = GraphNodeFactory.Create(GraphScope.StoryFlow, "enter_story", "to_target");
            enterStory.Properties["target_story_id"] = JsonSerializer.SerializeToElement("graph_target");
            store.Stories.Create(new GraphResourceEnvelope(
                GraphResourceKind.Story,
                "graph_source",
                "Canonical Graph Source",
                new GraphDocument([enterStory])));
            store.Stories.Create(new GraphResourceEnvelope(
                GraphResourceKind.Story,
                "graph_target",
                "Canonical Graph Target",
                new GraphDocument([GraphNodeFactory.Create(GraphScope.StoryFlow, "start", "target_start")])));

            var sessionEnd = GraphNodeFactory.Create(GraphScope.Session, "end", "session-end");
            sessionEnd.Properties["port_id"] = JsonSerializer.SerializeToElement("continue");
            sessionEnd.Properties["display_name"] = JsonSerializer.SerializeToElement("Continue");
            store.Sessions.Create(new GraphResourceEnvelope(
                GraphResourceKind.Session,
                "opening_session",
                "Opening Session",
                new GraphDocument([sessionEnd])));

            var taskSettle = GraphNodeFactory.Create(GraphScope.Task, "settle", "task-settle");
            taskSettle.Ports.Add(new GraphPort("completed", "Completed", true, GraphInterfaceKind.Logic, 0));
            store.Tasks.Create(new GraphResourceEnvelope(
                GraphResourceKind.Task,
                "opening_task",
                "Opening Task",
                new GraphDocument([taskSettle])));
            store.Memberships.Create(new CanonicalStoryMembershipManifest(
                "opening",
                new CanonicalStoryMembershipSet
                {
                    Sessions = ["opening_session"],
                    Tasks = ["opening_task"],
                }));
            store.Memberships.Create(new CanonicalStoryMembershipManifest("graph_source"));
            store.Memberships.Create(new CanonicalStoryMembershipManifest("graph_target"));
        }

        public string Root { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}

#pragma warning restore WPF0001
