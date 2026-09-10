using System.Text.Json;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.Settings;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class NamespaceShellWorkflowTests
{
    [TestMethod]
    public void FirstUseCancellationAtNamespaceOrProjectLeavesSettingsAndFilesUntouched()
    {
        using var directory = new TemporaryDirectory();
        var namespaceCancelTarget = Path.Combine(directory.Root, "namespace-cancel");
        var namespaceCancelSettings = new RecordingSettingsService(new StudioSettings());
        var namespaceCancelDialogs = new FakeNamespaceDialogs(null);
        var namespaceCancelProjectDialogs = new FakeProjectDialogs(
            new ProjectCreationRequest(namespaceCancelTarget, "cancelled", "Cancelled"));
        var namespaceCancelShell = CreateShell(
            namespaceCancelSettings,
            namespaceCancelDialogs,
            namespaceCancelProjectDialogs);

        namespaceCancelShell.NewProjectCommand.Execute(null);

        Assert.IsFalse(Directory.Exists(namespaceCancelTarget));
        Assert.AreEqual(0, namespaceCancelSettings.SaveCount);
        Assert.AreEqual(0, namespaceCancelProjectDialogs.RequestCount);
        Assert.IsTrue(namespaceCancelDialogs.LastFirstUse);

        var projectCancelTarget = Path.Combine(directory.Root, "project-cancel");
        var projectCancelSettings = new RecordingSettingsService(new StudioSettings());
        var projectCancelDialogs = new FakeNamespaceDialogs("author");
        var projectCancelProjectDialogs = new FakeProjectDialogs(null);
        var projectCancelShell = CreateShell(
            projectCancelSettings,
            projectCancelDialogs,
            projectCancelProjectDialogs);

        projectCancelShell.NewProjectCommand.Execute(null);

        Assert.IsFalse(Directory.Exists(projectCancelTarget));
        Assert.AreEqual(0, projectCancelSettings.SaveCount);
        Assert.AreEqual(1, projectCancelProjectDialogs.RequestCount);
        Assert.AreEqual("author", projectCancelDialogs.LastRequestedNamespace);
    }

    [TestMethod]
    public void FirstUseNamespaceAndNewProjectWritesPolicyAndSettings()
    {
        using var directory = new TemporaryDirectory();
        var projectDirectory = Path.Combine(directory.Root, "created-project");
        var settings = new RecordingSettingsService(new StudioSettings { Theme = ThemePreference.Dark, GlobalNamespace = "PreviousServer" });
        var namespaceDialogs = new FakeNamespaceDialogs("author");
        var projectDialogs = new FakeProjectDialogs(
            new ProjectCreationRequest(projectDirectory, "TestProject2", "Created Project"));
        var shell = CreateShell(settings, namespaceDialogs, projectDialogs);

        shell.NewProjectCommand.Execute(null);

        Assert.IsTrue(shell.HasProject);
        Assert.AreEqual(projectDirectory, shell.ProjectDirectory);
        Assert.AreEqual(1, settings.SaveCount);
        Assert.AreEqual("author", settings.Current.GlobalNamespace);
        Assert.AreEqual("author", NamespacePolicyStore.Load(projectDirectory)?.GlobalNamespace);
        Assert.AreEqual("author", namespaceDialogs.LastRequestedNamespace);
        Assert.AreEqual("PreviousServer", namespaceDialogs.LastInitialNamespace);
        StringAssert.Contains(File.ReadAllText(Path.Combine(projectDirectory, "project.json")), "TestProject2");
        Assert.IsTrue(File.Exists(Path.Combine(projectDirectory, "project.json")));
    }

    [TestMethod]
    public void ProjectNamespaceChangeAndUndoDoNotWriteApplicationDefaults()
    {
        using var project = CreateNamespaceProject("base");
        var before = Files(project.Root);
        var settings = new RecordingSettingsService(new StudioSettings { GlobalNamespace = "base" })
        {
            SaveException = PersistenceFailure(),
        };
        var namespaceDialogs = new FakeNamespaceDialogs("changed") { ConfirmResult = true };
        var shell = CreateShell(settings, namespaceDialogs, new FakeProjectDialogs(null), project.Root);

        shell.OpenProjectCommand.Execute(null);
        shell.ChangeGlobalNamespaceCommand.Execute(null);

        Assert.AreEqual("changed", NamespacePolicyStore.Load(project.Root)?.GlobalNamespace);
        Assert.AreEqual("base", settings.Current.GlobalNamespace);
        Assert.AreEqual(0, settings.SaveCount);
        Assert.IsTrue(shell.UndoCurrentCommand.CanExecute(null));
        shell.UndoCurrentCommand.Execute(null);
        AssertFilesEqual(before, Files(project.Root));
        Assert.AreEqual("base", NamespacePolicyStore.Load(project.Root)?.GlobalNamespace);
    }

    [TestMethod]
    public void OpeningProjectsPreservesEachNamespaceDespiteDifferentApplicationDefault()
    {
        using var first = CreateNamespaceProject("ServerOne");
        using var second = CreateNamespaceProject("ServerTwo");
        var settings = new RecordingSettingsService(new StudioSettings { GlobalNamespace = "OtherServer" });
        var dialogs = new FakeNamespaceDialogs(null);
        var shell = CreateShell(settings, dialogs, new FakeProjectDialogs(null), first.Root);
        foreach (var project in new[] { first, second, first })
        {
            var before = Files(project.Root);
            shell.RestoreLastProject(project.Root);
            Assert.AreEqual(project.Root, shell.ProjectDirectory);
            AssertFilesEqual(before, Files(project.Root));
        }
        Assert.IsNull(dialogs.LastInitialNamespace);
        Assert.AreEqual(0, settings.SaveCount);
    }

    [TestMethod]
    public void CustomNamespaceAndReturnRewriteTypedReferencesThroughGlobalLifecycle()
    {
        using var project = CreateNamespaceProject("base");
        var settings = new RecordingSettingsService(new StudioSettings { GlobalNamespace = "base" });
        var namespaceDialogs = new FakeNamespaceDialogs("custom") { ConfirmResult = true };
        var shell = CreateShell(settings, namespaceDialogs, new FakeProjectDialogs(null), project.Root);

        shell.OpenProjectCommand.Execute(null);
        shell.ChangeStoryNamespace("base:story", returnToGlobal: false);

        var custom = NamespaceProjectMigrationService.ReadProject(project.Root);
        Assert.AreEqual("custom:story", custom.Graphs.Single().Id);
        Assert.AreEqual("custom:actor", custom.Actors.Single().Id);
        Assert.AreEqual("custom:story", custom.Memberships.Single().StoryId);
        Assert.AreEqual("custom:actor", custom.Memberships.Single().OwnedResources.Actors.Single());
        Assert.AreEqual("custom:story", custom.Actors.Single().HomeStoryId);
        Assert.AreEqual("custom:actor", ReadStartActorReference(project.Root, "custom:story"));
        Assert.AreEqual("custom", custom.Policy!.StoryOverrides["custom:story"]);

        shell.ChangeStoryNamespace("custom:story", returnToGlobal: true);

        var returned = NamespaceProjectMigrationService.ReadProject(project.Root);
        Assert.AreEqual("base:story", returned.Graphs.Single().Id);
        Assert.AreEqual("base:actor", returned.Actors.Single().Id);
        Assert.AreEqual("base:story", returned.Memberships.Single().StoryId);
        Assert.AreEqual("base:actor", returned.Memberships.Single().OwnedResources.Actors.Single());
        Assert.AreEqual("base:story", returned.Actors.Single().HomeStoryId);
        Assert.AreEqual("base:actor", ReadStartActorReference(project.Root, "base:story"));
        Assert.IsEmpty(returned.Policy!.StoryOverrides);
    }

    [TestMethod]
    public void SavingThemePreservesChangedGlobalNamespace()
    {
        var settings = new RecordingSettingsService(new StudioSettings
        {
            Theme = ThemePreference.System,
            GlobalNamespace = "changed",
            WindowWidth = 1440,
        });
        var viewModel = new ThemeSettingsViewModel(settings, _ => { });
        settings.Save(settings.Load() with { GlobalNamespace = "ChangedAfterViewModelCreation" });

        viewModel.SelectedTheme = ThemePreference.Dark;

        Assert.AreEqual("ChangedAfterViewModelCreation", settings.LastSaved?.GlobalNamespace);
        Assert.AreEqual(1440, settings.LastSaved?.WindowWidth);
        Assert.AreEqual(ThemePreference.Dark, settings.LastSaved?.Theme);
    }

    private static ShellViewModel CreateShell(
        ISettingsService settings,
        INamespaceDialogs namespaceDialogs,
        IProjectWorkspaceDialogs projectDialogs,
        string? projectDirectory = null)
        => new(
            new ProjectService(),
            new FixedProjectFolderPicker(projectDirectory),
            projectWorkspaceDialogs: projectDialogs,
            namespaceSettings: settings,
            namespaceDialogs: namespaceDialogs);

    private static NamespaceProject CreateNamespaceProject(string globalNamespace)
    {
        var directory = new TemporaryDirectory();
        var service = new ProjectService();
        service.CreateProject(directory.Root, "namespace_project", "Namespace Project");
        var store = new CanonicalProjectGraphStore(directory.Root);
        var start = GraphNodeFactory.CreateStoryStart("start");
        start.Ports.Clear();
        StoryStartSchema.InitializeDefault(start, "actor_port", StoryStartSchema.ActorInteraction, "actor");
        store.Stories.Create(new(GraphResourceKind.Story, "story", "Story", new GraphDocument([start])));
        store.Memberships.Create(new("story", new() { Actors = ["actor"] }));
        var actor = new IndividualActorResource
        {
            NpcId = "actor",
            DisplayName = "Actor",
            HomeStoryId = "story",
        };
        File.WriteAllText(
            Path.Combine(directory.Root, "actors", "actor.json"),
            ActorSerializer.Serialize(actor, ActorIdPolicy.ExistingResource));
        var migration = new NamespaceProjectMigrationService();
        migration.Apply(migration.PreviewGlobal(directory.Root, globalNamespace));
        return new NamespaceProject(directory);
    }

    private static string ReadStartActorReference(string root, string storyId)
    {
        var path = Path.Combine(
            root,
            "resources/canonical/stories",
            DgrResourceId.RelativeJsonPath(storyId));
        var graph = GraphResourceEnvelope.FromJson(File.ReadAllText(path));
        var triggers = graph.Graph!.Nodes.Single(node => node.Type == "start")
            .Properties[StoryStartSchema.TriggersProperty];
        return triggers.EnumerateArray().Single()
            .GetProperty("trigger_properties")
            .GetProperty(StoryStartSchema.ActorIdProperty)
            .GetString()!;
    }

    private static Dictionary<string, byte[]> Files(string root) =>
        Directory.GetFiles(root, "*", SearchOption.AllDirectories)
            .ToDictionary(
                path => Path.GetRelativePath(root, path),
                File.ReadAllBytes,
                StringComparer.OrdinalIgnoreCase);

    private static void AssertFilesEqual(
        Dictionary<string, byte[]> expected,
        Dictionary<string, byte[]> actual)
    {
        CollectionAssert.AreEquivalent(expected.Keys.ToArray(), actual.Keys.ToArray());
        foreach (var pair in expected)
            CollectionAssert.AreEqual(pair.Value, actual[pair.Key], pair.Key);
    }

    private static SettingsPersistenceException PersistenceFailure() =>
        new("Unable to save namespace settings.", new IOException("Injected settings failure."));

    private sealed record NamespaceProject(TemporaryDirectory Directory) : IDisposable
    {
        public string Root => Directory.Root;

        public void Dispose() => Directory.Dispose();
    }

    private sealed class FixedProjectFolderPicker(string? projectDirectory) : IProjectFolderPicker
    {
        public string? PickProjectFolder() => projectDirectory;
    }

    private sealed class FakeNamespaceDialogs(string? namespaceResult) : INamespaceDialogs
    {
        public bool ConfirmResult { get; init; }

        public string? LastRequestedNamespace { get; private set; }

        public bool LastFirstUse { get; private set; }
        public string? LastInitialNamespace { get; private set; }

        public string? RequestNamespace(string? currentNamespace, bool firstUse)
        {
            LastInitialNamespace = currentNamespace;
            LastRequestedNamespace = namespaceResult;
            LastFirstUse = firstUse;
            return namespaceResult;
        }

        public bool ConfirmMigration(NamespaceMigrationPreview preview) => ConfirmResult;
    }

    private sealed class FakeProjectDialogs(ProjectCreationRequest? result) : IProjectWorkspaceDialogs
    {
        public int RequestCount { get; private set; }

        public ProjectCreationRequest? RequestCreate(string? initialParentDirectory = null)
        {
            RequestCount++;
            return result;
        }

        public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges() => UnsavedChangesChoice.Cancel;

        public bool ConfirmDeleteStory(string storyId, string displayName, IReadOnlyList<string> resourcesToDelete) => false;
    }

    private sealed class RecordingSettingsService(StudioSettings initial) : ISettingsService
    {
        public StudioSettings Current { get; private set; } = initial;

        public SettingsPersistenceException? SaveException { get; init; }

        public int SaveCount { get; private set; }

        public StudioSettings? LastSaved { get; private set; }

        public string SettingsPath => "test-settings.json";

        public StudioSettings Load() => Current;

        public void Save(StudioSettings settings)
        {
            SaveCount++;
            if (SaveException is not null)
                throw SaveException;
            Current = settings;
            LastSaved = settings;
        }
    }

    private sealed class TemporaryDirectory : IDisposable
    {
        public TemporaryDirectory()
        {
            Root = Path.Combine(AppContext.BaseDirectory, ".test-data", "namespace-shell", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
                Directory.Delete(Root, recursive: true);
        }
    }
}
