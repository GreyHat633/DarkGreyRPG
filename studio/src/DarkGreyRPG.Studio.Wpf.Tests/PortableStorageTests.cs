using System.IO;
using System.Text.Json;
using System.Security.AccessControl;
using System.Security.Principal;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.Settings;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class PortableStorageTests
{
    [TestMethod]
    public void OrganizedLayoutUsesRootApphostDirectoryForDataAndTools()
    {
        using var fixture = new Fixture();
        var root = fixture.Paths.Root;
        var resolved = StudioStoragePaths.ResolveRoot(Path.Combine(root, "Program"), Path.Combine(root, "DarkGreyRPGStudio.exe"));
        Assert.AreEqual(root, resolved);
        var paths = new StudioStoragePaths(resolved);
        Assert.AreEqual(Path.Combine(root, "Data", "Projects"), paths.Projects);
        Assert.AreEqual(Path.Combine(root, "Tools", "FFmpeg"), paths.MediaTools);
        var moved = Path.Combine(fixture.Root, "MovedStudio");
        Assert.AreEqual(moved, StudioStoragePaths.ResolveRoot(Path.Combine(moved, "Program"), Path.Combine(moved, "DarkGreyRPGStudio.exe")));
    }

    [TestMethod]
    public void DevelopmentAndUnrelatedHostsDoNotReadParentCopyData()
    {
        using var fixture = new Fixture();
        var root = fixture.Paths.Root;
        Assert.AreEqual(root, StudioStoragePaths.ResolveRoot(root, Path.Combine(root, "DarkGreyRPGStudio.exe")));
        var program = Path.Combine(root, "Program");
        Assert.AreEqual(program, StudioStoragePaths.ResolveRoot(program, null));
        Assert.AreEqual(program, StudioStoragePaths.ResolveRoot(program, Path.Combine(fixture.Root, "Other", "DarkGreyRPGStudio.exe")));
    }

    [TestMethod]
    public void MovingWholeCopyPreservesProjectAndDialogLocations()
    {
        using var fixture = new Fixture();
        var paths = fixture.Paths;
        paths.Initialize();
        var project = Path.Combine(paths.Projects, "中文项目");
        Directory.CreateDirectory(project);
        File.WriteAllText(Path.Combine(project, "project.json"), "{}");
        new SettingsService(storagePaths: paths).Save(new StudioSettings
        {
            LastProject = project, RecentProjects = [project], LastExportDirectory = paths.Exports,
            LastImportDirectory = fixture.External, GlobalNamespace = "GreyHat_", Theme = ThemePreference.Dark,
        });
        using (var raw = JsonDocument.Parse(File.ReadAllText(paths.Settings)))
        {
            Assert.IsFalse(Path.IsPathRooted(raw.RootElement.GetProperty("last_project").GetString()!));
            Assert.IsFalse(Path.IsPathRooted(raw.RootElement.GetProperty("last_export_directory").GetString()!));
        }
        var moved = Path.Combine(fixture.Root, "MovedStudio");
        Directory.Move(paths.Root, moved);
        var loaded = new SettingsService(storagePaths: new StudioStoragePaths(moved)).Load();
        Assert.AreEqual(Path.Combine(moved, "Data", "Projects", "中文项目"), loaded.LastProject);
        Assert.AreEqual(loaded.LastProject, loaded.RecentProjects.Single());
        Assert.AreEqual(Path.Combine(moved, "Data", "Exports"), loaded.LastExportDirectory);
        Assert.AreEqual(fixture.External, loaded.LastImportDirectory);
        Assert.AreEqual("GreyHat_", loaded.GlobalNamespace);
    }

    [TestMethod]
    public void SeparateCopiesHaveIndependentSettingsAndLogs()
    {
        using var fixture = new Fixture();
        var other = new StudioStoragePaths(Path.Combine(fixture.Root, "Other"));
        new SettingsService(storagePaths: fixture.Paths).Save(new StudioSettings { Theme = ThemePreference.Light });
        Assert.AreEqual(ThemePreference.Dark, new SettingsService(storagePaths: other).Load().Theme);
        var logger = new CrashLogService(logsDirectory: fixture.Paths.Logs);
        var log = logger.Log(new IOException("test"), "portable");
        Assert.IsNotNull(log);
        Assert.IsTrue(StudioStoragePaths.IsWithin(log, fixture.Paths.Logs));
        Assert.IsFalse(Directory.Exists(other.Data));
    }

    [TestMethod]
    public void ImportCopiesMediaReferencesAndEditorRecoveryWithoutChangingSource()
    {
        using var fixture = new Fixture();
        fixture.CreateProject();
        foreach (var relative in new[] { "resources/media/image.png", "resources/editor/recovery/draft.json", "references/pack.dgrs" })
        {
            var file = Path.Combine(fixture.External, relative);
            Directory.CreateDirectory(Path.GetDirectoryName(file)!);
            File.WriteAllBytes(file, [1, 2, 3, 4]);
        }
        var local = new PortableProjectStore(fixture.Paths).Import(fixture.External);
        Assert.IsTrue(fixture.Paths.ContainsProject(local));
        foreach (var file in Directory.GetFiles(fixture.External, "*", SearchOption.AllDirectories))
            CollectionAssert.AreEqual(File.ReadAllBytes(file), File.ReadAllBytes(Path.Combine(local, Path.GetRelativePath(fixture.External, file))));
        File.WriteAllText(Path.Combine(local, "project.json"), "local edit");
        StringAssert.Contains(File.ReadAllText(Path.Combine(fixture.External, "project.json")), "external");
    }

    [TestMethod]
    public void ImportSameNameKeepsBothProjectsAndLocalOpenDoesNotRecopy()
    {
        using var fixture = new Fixture(); fixture.CreateProject();
        var store = new PortableProjectStore(fixture.Paths);
        var first = store.Import(fixture.External);
        var second = store.Import(fixture.External);
        Assert.AreEqual(first + "_2", second);
        Assert.AreEqual(first, store.Import(first));
        Assert.AreEqual(2, Directory.GetDirectories(fixture.Paths.Projects).Length);
    }

    [TestMethod]
    public void LockedSourceFailureRemovesStagingAndDoesNotInstallPartialProject()
    {
        using var fixture = new Fixture(); fixture.CreateProject();
        var lockedPath = Path.Combine(fixture.External, "locked.bin"); File.WriteAllText(lockedPath, "locked");
        using var locked = new FileStream(lockedPath, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        Assert.Throws<IOException>(() => new PortableProjectStore(fixture.Paths).Import(fixture.External));
        Assert.AreEqual(0, Directory.GetDirectories(fixture.Paths.Projects).Length);
        Assert.AreEqual(0, Directory.GetDirectories(fixture.Paths.Temp).Length);
    }

    [TestMethod]
    public void ImportFailureLeavesCurrentShellProjectSelected()
    {
        using var fixture = new Fixture(); fixture.CreateProject();
        var picker = new Picker { Directory = fixture.External };
        var shell = new ShellViewModel(new ProjectService(), picker, portableProjects: new PortableProjectStore(fixture.Paths));
        shell.OpenProjectCommand.Execute(null);
        var current = shell.ProjectDirectory;
        picker.Directory = Path.Combine(fixture.Root, "Missing");
        shell.OpenProjectCommand.Execute(null);
        Assert.AreEqual(current, shell.ProjectDirectory);
        Assert.IsTrue(shell.HasProject);
    }

    [TestMethod]
    public void RestoreCannotAutomaticallyImportExternalProject()
    {
        using var fixture = new Fixture(); fixture.CreateProject();
        var shell = new ShellViewModel(new ProjectService(), new Picker(), portableProjects: new PortableProjectStore(fixture.Paths));
        Assert.IsFalse(shell.RestoreLastProject(fixture.External));
        Assert.IsFalse(shell.HasProject);
        Assert.IsFalse(Directory.Exists(fixture.Paths.Projects));
    }

    [TestMethod]
    public void CreationAllowsExplicitExternalLocationButKeepsProgramFoldersSeparate()
    {
        using var fixture = new Fixture();
        var model = ProjectCreationDialogViewModel.ForCreate(fixture.Paths.Projects);
        Assert.AreEqual(Path.Combine(fixture.Paths.Projects, "Project"), model.FullDestination);
        model.FullDestination = fixture.External;
        Assert.AreEqual(fixture.External, model.DestinationDirectory);
        var store = new PortableProjectStore(fixture.Paths);
        store.ValidateCreationDestination(model.DestinationDirectory);
        store.ValidateCreationDestination(Path.Combine(fixture.Paths.Projects, "Local"));
        foreach (var path in new[] { fixture.Paths.Root, fixture.Paths.Data, fixture.Paths.Projects, fixture.Paths.Logs, fixture.Root, Path.Combine(fixture.Paths.Root, "Program", "Project") })
            Assert.Throws<IOException>(() => store.ValidateCreationDestination(path));
    }

    [TestMethod]
    public void CreatedExternalProjectRestoresAndOpensInPlaceAfterRestart()
    {
        using var fixture = new Fixture();
        var store = new PortableProjectStore(fixture.Paths);
        var shell = new ShellViewModel(new ProjectService(), new Picker(),
            projectWorkspaceDialogs: new CreationDialogs(fixture.External), portableProjects: store);
        shell.NewProjectCommand.Execute(null);
        Assert.IsTrue(shell.HasProject);
        Assert.AreEqual(fixture.External, shell.ProjectDirectory);
        Assert.IsTrue(File.Exists(Path.Combine(fixture.External, "project.json")));
        Assert.IsFalse(Directory.Exists(fixture.Paths.Projects));

        var settingsService = new SettingsService(storagePaths: fixture.Paths);
        var created = settingsService.Load();
        CollectionAssert.AreEqual(new[] { fixture.External }, created.ExternalProjects.ToArray());
        // The in-place choice outlives the ten-entry recent history.
        settingsService.Save(created with { LastProject = fixture.External, RecentProjects = [] });
        var reopened = new ShellViewModel(new ProjectService(), new Picker { Directory = fixture.External },
            portableProjects: new PortableProjectStore(fixture.Paths));
        Assert.IsTrue(reopened.RestoreLastProject(settingsService.Load().LastProject));
        reopened.OpenProjectCommand.Execute(null);
        Assert.AreEqual(fixture.External, reopened.ProjectDirectory);
        Assert.IsFalse(Directory.Exists(fixture.Paths.Projects));

        var otherCopy = new PortableProjectStore(new StudioStoragePaths(Path.Combine(fixture.Root, "OtherStudio")));
        Assert.Throws<IOException>(() => otherCopy.PrepareOpen(fixture.External, isRestore: true));
    }

    [TestMethod]
    public void FailedCreationDoesNotRememberAnExternalLocation()
    {
        using var fixture = new Fixture();
        Directory.CreateDirectory(fixture.Root);
        File.WriteAllText(fixture.External, "blocked destination");
        var shell = new ShellViewModel(new ProjectService(), new Picker(),
            projectWorkspaceDialogs: new CreationDialogs(fixture.External),
            portableProjects: new PortableProjectStore(fixture.Paths));
        shell.NewProjectCommand.Execute(null);
        Assert.IsFalse(shell.HasProject);
        Assert.AreEqual(0, new SettingsService(storagePaths: fixture.Paths).Load().ExternalProjects.Count);
        Assert.AreEqual("blocked destination", File.ReadAllText(fixture.External));
    }

    [TestMethod]
    public void ExternalLocationsRemainAbsoluteAcrossStudioMovesAndAreNotCappedByRecentHistory()
    {
        using var fixture = new Fixture();
        var external = Enumerable.Range(0, 12).Select(i => Path.Combine(fixture.External, i.ToString())).ToArray();
        var service = new SettingsService(storagePaths: fixture.Paths);
        service.Save(new StudioSettings { ExternalProjects = external, RecentProjects = external });
        using (var raw = JsonDocument.Parse(File.ReadAllText(fixture.Paths.Settings)))
            Assert.IsTrue(Path.IsPathFullyQualified(raw.RootElement.GetProperty("external_projects")[0].GetString()!));
        var moved = Path.Combine(fixture.Root, "MovedStudio");
        Directory.Move(fixture.Paths.Root, moved);
        var loaded = new SettingsService(storagePaths: new StudioStoragePaths(moved)).Load();
        CollectionAssert.AreEqual(external, loaded.ExternalProjects.ToArray());
        Assert.AreEqual(10, loaded.RecentProjects.Count);
    }

    [TestMethod]
    public void UnwritableLocalLayoutFailsWithoutFallback()
    {
        using var fixture = new Fixture();
        Directory.CreateDirectory(fixture.Paths.Root);
        File.WriteAllText(fixture.Paths.Data, "a file blocks the data directory");
        Assert.Throws<IOException>(() => fixture.Paths.Initialize());
        Assert.IsFalse(File.Exists(fixture.Paths.Settings));
    }

    [TestMethod]
    public void ReadOnlyProgramDirectoryFailsWithoutCreatingDataElsewhere()
    {
        using var fixture = new Fixture();
        var directory = Directory.CreateDirectory(fixture.Paths.Root);
        var original = directory.GetAccessControl();
        var denied = directory.GetAccessControl();
        var identity = WindowsIdentity.GetCurrent().User!;
        denied.AddAccessRule(new FileSystemAccessRule(identity,
            FileSystemRights.CreateDirectories | FileSystemRights.CreateFiles,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.None, AccessControlType.Deny));
        try
        {
            directory.SetAccessControl(denied);
            Assert.Throws<UnauthorizedAccessException>(() => fixture.Paths.Initialize());
            Assert.IsFalse(Directory.Exists(fixture.Paths.Data));
        }
        finally { directory.SetAccessControl(original); }
    }

    [TestMethod]
    public void ProjectContainmentRejectsSiblingPrefixAndRoot()
    {
        using var fixture = new Fixture();
        Assert.IsFalse(fixture.Paths.ContainsProject(fixture.Paths.Projects + "Elsewhere/test"));
        Assert.IsFalse(fixture.Paths.ContainsProject(fixture.Paths.Projects));
        Assert.IsTrue(fixture.Paths.ContainsProject(Path.Combine(fixture.Paths.Projects, "Project")));
    }

    private sealed class Picker : IProjectFolderPicker
    {
        public string? Directory { get; set; }
        public string? PickProjectFolder() => Directory;
    }

    private sealed class CreationDialogs(string destination) : IProjectWorkspaceDialogs
    {
        public ProjectCreationRequest? RequestCreate(string? initialParentDirectory = null)
            => new(destination, "CustomProject", "自选项目");
        public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges() => UnsavedChangesChoice.Discard;
        public bool ConfirmDeleteStory(string storyId, string displayName, IReadOnlyList<string> resourcesToDelete) => false;
    }

    private sealed class Fixture : IDisposable
    {
        public string Root { get; } = Path.Combine(AppContext.BaseDirectory, ".portable-tests", Guid.NewGuid().ToString("N"));
        public StudioStoragePaths Paths => new(Path.Combine(Root, "Studio"));
        public string External => Path.Combine(Root, "External");
        public void CreateProject() { Directory.CreateDirectory(Path.Combine(External, "actors")); File.WriteAllText(Path.Combine(External, "project.json"), "{\"schema_version\":2,\"id\":\"external\",\"display_name\":\"External\"}"); }
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }
}
