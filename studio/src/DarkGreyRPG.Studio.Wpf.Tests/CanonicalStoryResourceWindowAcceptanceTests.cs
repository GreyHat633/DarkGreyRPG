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
