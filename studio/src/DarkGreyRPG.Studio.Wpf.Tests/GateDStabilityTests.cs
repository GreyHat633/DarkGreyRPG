using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class GateDStabilityTests
{
    [TestMethod]
    public void ResourceDialogFailureIsReportedAndDoesNotEscapeCommand()
    {
        using var project = new GateDProjectDirectory();
        var projectService = new ProjectService();
        projectService.CreateProject(project.Root, "gate_d", "Gate D");
        new CanonicalStoryLifecycleService(new CanonicalProjectGraphStore(project.Root)).Create("ST-2345-6789-ABCD-EFGH", "开场");
        var logSettingsPath = Path.Combine(project.Root, "settings", "settings.json");
        var logger = new CrashLogService(
            logSettingsPath,
            () => new DateTimeOffset(2026, 8, 27, 16, 0, 0, TimeSpan.FromHours(8)),
            "2.1.3-test");
        var shell = new ShellViewModel(
            projectService,
            new FixedFolderPicker(project.Root),
            canonicalStoryResourceDialogs: new ThrowingResourceDialogs(),
            crashLogService: logger);

        shell.OpenProjectCommand.Execute(null);
        shell.ProjectHome.SelectedStory = shell.ProjectHome.Stories.Single();
        shell.OpenSelectedStoryCommand.Execute(null);
        shell.CanonicalStoryWorkspace!.RequestCreate(CanonicalStoryFolderKind.Sessions);

        Assert.IsTrue(shell.Output.Entries.Any(entry => entry.Kind == OutputKind.Error));
        Assert.IsTrue(shell.Problems.Problems.Any(problem => problem.Code == "operation.failure"));
        Assert.IsTrue(shell.Toast.IsVisible);
        Assert.AreEqual(ToastKind.Error, shell.Toast.Kind);
        var log = Directory.EnumerateFiles(logger.LogsDirectory, "crash-*.log").Single();
        var text = File.ReadAllText(log);
        StringAssert.Contains(text, nameof(InvalidOperationException));
        StringAssert.Contains(text, "Injected dialog failure");
        StringAssert.Contains(text, nameof(ShellViewModel));
        StringAssert.Contains(text, "CreateCanonicalStoryResource");
    }

    [TestMethod]
    public void CrashLogServiceCreatesDeterministicUtf8FullStackReport()
    {
        using var directory = new GateDProjectDirectory();
        var settingsPath = Path.Combine(directory.Root, "nested", "settings.json");
        var logger = new CrashLogService(
            settingsPath,
            () => new DateTimeOffset(2026, 8, 27, 16, 1, 2, 123, TimeSpan.FromHours(8)),
            "2.1.3-test");
        var exception = new InvalidOperationException("full stack evidence");

        var path = logger.Log(exception, "injected-context", new Dictionary<string, string?>
        {
            ["Theme"] = "Dark",
            ["Current Story"] = "intro",
        });

        Assert.IsNotNull(path);
        Assert.IsTrue(File.Exists(path));
        StringAssert.Contains(File.ReadAllText(path!), "InvalidOperationException");
        StringAssert.Contains(File.ReadAllText(path!), "full stack evidence");
        StringAssert.Contains(File.ReadAllText(path!), "injected-context");
        StringAssert.Contains(File.ReadAllText(path!), "Current Story: intro");
        StringAssert.Contains(File.ReadAllText(path!), "Studio Version: 2.1.3-test");
    }

    private sealed class FixedFolderPicker(string folder) : IProjectFolderPicker
    {
        public string? PickProjectFolder() => folder;
    }

    private sealed class ThrowingResourceDialogs : ICanonicalStoryResourceDialogs
    {
        public CanonicalGraphResourceIdentityRequest? RequestCreate(GraphResourceKind kind, string suggestedId)
            => throw new InvalidOperationException("Injected dialog failure");
        public CanonicalGraphResourceChoice? PickReference(GraphResourceKind kind, IReadOnlyList<GraphResourceInfo> candidates, string storyDisplayName) => null;
        public bool ConfirmRemoveReference(CanonicalGraphResourceChoice resource, string storyDisplayName) => false;
        public bool ConfirmDeleteOwned(CanonicalGraphResourceChoice resource) => false;
        public bool ConfirmAggregateInterfaceRemoval(CanonicalGraphResourceChoice resource, IReadOnlyList<GraphConnection> affectedConnections) => false;
        public void ShowDeleteBlocked(CanonicalGraphResourceChoice resource, IReadOnlyList<string> storyIds) { }
    }

    private sealed class GateDProjectDirectory : IDisposable
    {
        public GateDProjectDirectory()
        {
            Root = Path.Combine(AppContext.BaseDirectory, ".gate-d-test-data", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
