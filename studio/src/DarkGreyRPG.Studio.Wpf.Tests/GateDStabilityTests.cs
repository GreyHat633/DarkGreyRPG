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
        projectService.CreateStory("intro", "开场");
        var logSettingsPath = Path.Combine(project.Root, "settings", "settings.json");
        var logger = new CrashLogService(
            logSettingsPath,
            () => new DateTimeOffset(2026, 8, 27, 16, 0, 0, TimeSpan.FromHours(8)),
            "2.1.3-test");
        var shell = new ShellViewModel(
            projectService,
            new FixedFolderPicker(project.Root),
            resourceWorkspaceDialogs: new ThrowingResourceDialogs(),
            crashLogService: logger);

        shell.OpenProjectCommand.Execute(null);
        shell.ProjectHome.SelectedStory = shell.ProjectHome.Stories.Single();
        shell.OpenSelectedStoryCommand.Execute(null);
        shell.StoryWorkspace.SelectRoute(StoryWorkspaceRoutes.Dialogues);

        shell.NewStoryResourceCommand.Execute(null);

        Assert.IsTrue(shell.Output.Entries.Any(entry => entry.Kind == OutputKind.Error));
        Assert.IsTrue(shell.Problems.Problems.Any(problem => problem.Code == "operation.failure"));
        Assert.IsTrue(shell.Toast.IsVisible);
        Assert.AreEqual(ToastKind.Error, shell.Toast.Kind);
        var log = Directory.EnumerateFiles(logger.LogsDirectory, "crash-*.log").Single();
        var text = File.ReadAllText(log);
        StringAssert.Contains(text, nameof(InvalidOperationException));
        StringAssert.Contains(text, "Injected dialog failure");
        StringAssert.Contains(text, nameof(ShellViewModel));
        StringAssert.Contains(text, "NewStoryResource");
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

    [TestMethod]
    public void CreationChoiceBindingUsesExplicitOneWayMode()
    {
        var path = FindRepositoryFile("studio", "src", "DarkGreyRPG.Studio", "Views", "ResourceCreationChoiceDialog.xaml");
        var xaml = File.ReadAllText(path);
        StringAssert.Contains(xaml, "ChineseTypeLabel, Mode=OneWay");
    }

    private static string FindRepositoryFile(params string[] parts)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine([directory.FullName, .. parts]);
            if (File.Exists(candidate)) return candidate;
        }

        Assert.Fail("Could not locate repository source file.");
        return string.Empty;
    }

    private sealed class FixedFolderPicker(string folder) : IProjectFolderPicker
    {
        public string? PickProjectFolder() => folder;
    }

    private sealed class ThrowingResourceDialogs : IResourceWorkspaceDialogs
    {
        public ResourceCreationMode? RequestCreationMode(ProjectResourceType type, string storyDisplayName) =>
            throw new InvalidOperationException("Injected dialog failure");

        public ResourceIdentityRequest? RequestCreate(ProjectResourceType type, string suggestedId) =>
            throw new InvalidOperationException("Injected dialog failure");
        public ResourceIdentityRequest? RequestImportIdentity(ProjectResourceType type, ResourceDescriptor source, string suggestedId) => null;
        public ResourceDescriptor? PickResource(ProjectResourceType type, IReadOnlyList<ResourceDescriptor> candidates, ResourcePickerMode mode, string storyDisplayName) => null;
        public bool ConfirmDelete(ResourceDescriptor resource) => false;
        public bool ConfirmRemoveReference(ResourceDescriptor resource, string storyDisplayName) => false;
        public void ShowReferences(ResourceDescriptor resource, IReadOnlyList<ResourceDescriptor> references) { }
        public bool ConfirmSaveBeforeSwitch(ResourceDescriptor resource) => false;
        public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges(ResourceDescriptor resource) => UnsavedChangesChoice.Cancel;
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
