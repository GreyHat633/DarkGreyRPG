using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Dialogues;
using DarkGreyRPG.Studio.Core.Graphs.Migration;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class CanonicalProjectMigrationUiTests
{
    [TestMethod]
    public void InvalidPreviewListsIssueAndDisablesConfirmation()
    {
        var preview = CanonicalProjectMigrationPreview.PreviewProject(
            Path.Combine(Path.GetTempPath(), "missing-dgrpg-" + Guid.NewGuid().ToString("N")));
        var viewModel = new CanonicalProjectMigrationDialogViewModel(preview);

        Assert.IsFalse(viewModel.CanApply);
        Assert.IsFalse(viewModel.ConfirmCommand.CanExecute(null));
        Assert.IsTrue(viewModel.Issues.Any(issue => issue.Code == "migration.project.path.invalid"));
        Assert.IsEmpty(viewModel.ProposedDestinations);
    }

    [TestMethod]
    public void ShellNeverAppliesInvalidPreviewEvenIfDialogConfirms()
    {
        using var project = TestProject.Create();
        var dialogs = new FakeProjectWorkspaceDialogs { Confirmed = true };
        var applyCount = 0;
        var shell = new ShellViewModel(
            new ProjectService(),
            new FixedProjectFolderPicker(project.Root),
            projectWorkspaceDialogs: dialogs,
            migrationPreview: _ => CanonicalProjectMigrationPreview.PreviewProject(
                Path.Combine(project.Root, "does-not-exist")),
            migrationApply: _ =>
            {
                applyCount++;
                throw new InvalidOperationException("must not be called");
            });

        shell.OpenProjectCommand.Execute(null);
        shell.MigrateCanonicalProjectCommand.Execute(null);

        Assert.AreEqual(1, dialogs.ConfirmationCount);
        Assert.AreEqual(0, applyCount);
    }

    [TestMethod]
    public void CancelledApplicablePreviewDoesNotCreateMigrationArtifacts()
    {
        using var project = TestProject.Create();
        var dialogs = new FakeProjectWorkspaceDialogs { Confirmed = false };
        var shell = new ShellViewModel(
            new ProjectService(),
            new FixedProjectFolderPicker(project.Root),
            projectWorkspaceDialogs: dialogs);

        shell.OpenProjectCommand.Execute(null);
        shell.MigrateCanonicalProjectCommand.Execute(null);

        Assert.AreEqual(1, dialogs.ConfirmationCount);
        Assert.IsFalse(Directory.Exists(Path.Combine(project.Root, ".migration-backups")));
        Assert.IsFalse(File.Exists(Path.Combine(project.Root, "migration.log")));
        Assert.IsFalse(Directory.Exists(Path.Combine(project.Root, "resources")));
    }

    [TestMethod]
    public void ApplicablePreviewShowsExactDestinationsAndEnablesConfirmation()
    {
        using var project = TestProject.CreateApplicable();
        var preview = CanonicalProjectMigrationPreview.PreviewProject(project.Root);
        var viewModel = new CanonicalProjectMigrationDialogViewModel(preview);

        Assert.IsTrue(viewModel.CanApply, string.Join("; ", viewModel.Issues.Select(issue => issue.Code)));
        Assert.IsTrue(viewModel.ConfirmCommand.CanExecute(null));
        CollectionAssert.AreEqual(new[]
        {
            "resources/canonical/memberships/intro.json",
            "resources/canonical/sessions/hello.json",
            "resources/canonical/stories/intro.json",
        }, viewModel.ProposedDestinations.ToArray());
        Assert.AreEqual("无", viewModel.IssuesText);
    }

    [TestMethod]
    public void ConfirmedApplicablePreviewAppliesOnceAndRefreshesCanonicalDiscovery()
    {
        using var project = TestProject.CreateApplicable();
        var dialogs = new FakeProjectWorkspaceDialogs { Confirmed = true };
        var applyCount = 0;
        var shell = new ShellViewModel(
            new ProjectService(),
            new FixedProjectFolderPicker(project.Root),
            projectWorkspaceDialogs: dialogs,
            migrationApply: preview =>
            {
                applyCount++;
                return new CanonicalProjectMigrationTransaction(
                    utcNow: () => new DateTimeOffset(2026, 8, 29, 2, 3, 4, TimeSpan.Zero)).Apply(preview);
            });

        shell.OpenProjectCommand.Execute(null);
        shell.MigrateCanonicalProjectCommand.Execute(null);

        Assert.AreEqual(1, dialogs.ConfirmationCount);
        Assert.AreEqual(1, applyCount);
        Assert.IsTrue(File.Exists(Path.Combine(project.Root, "resources", "canonical", "stories", "intro.json")));
        Assert.IsTrue(File.Exists(Path.Combine(project.Root, "migration.log")));
        Assert.IsTrue(Directory.Exists(Path.Combine(project.Root, ".migration-backups", "20260829T020304000Z-0.3.0.0")));
        var refreshedStory = shell.ProjectHome.Stories.Single(story => story.Id == "intro");
        Assert.IsTrue(refreshedStory.HasLegacyStory);
        Assert.IsTrue(refreshedStory.HasCanonicalStory);
        Assert.IsTrue(shell.Output.Entries.Any(entry =>
            entry.Kind == OutputKind.Success && entry.Source == "migration" && entry.Message.Contains("Canonical 迁移成功", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void ApplyFailureIsReportedWithoutSuccessOrCanonicalArtifacts()
    {
        using var project = TestProject.CreateApplicable();
        var dialogs = new FakeProjectWorkspaceDialogs { Confirmed = true };
        var shell = new ShellViewModel(
            new ProjectService(),
            new FixedProjectFolderPicker(project.Root),
            projectWorkspaceDialogs: dialogs,
            migrationApply: _ => throw new CanonicalProjectMigrationException("migration.test.failure", "injected"));

        shell.OpenProjectCommand.Execute(null);
        shell.MigrateCanonicalProjectCommand.Execute(null);

        Assert.IsFalse(Directory.Exists(Path.Combine(project.Root, "resources", "canonical")));
        Assert.IsTrue(shell.Output.Entries.Any(entry =>
            entry.Kind == OutputKind.Error && entry.Source == "migration" && entry.Message.Contains("migration.test.failure", StringComparison.Ordinal)));
        Assert.IsFalse(shell.Output.Entries.Any(entry =>
            entry.Kind == OutputKind.Success && entry.Source == "migration"));
    }

    [TestMethod]
    public void TrackedAcceptanceProjectPreviewsAllCanonicalWritesAndEnablesConfirmation()
    {
        var project = Path.Combine(FindRepositoryRoot(), ".tooling", "2.1-acceptance", "DarkGrey-2.1-Acceptance");
        var viewModel = new CanonicalProjectMigrationDialogViewModel(
            CanonicalProjectMigrationPreview.PreviewProject(project));

        Assert.IsTrue(viewModel.CanApply, string.Join("; ", viewModel.Issues.Select(issue => issue.Code)));
        Assert.IsTrue(viewModel.ConfirmCommand.CanExecute(null));
        Assert.IsEmpty(viewModel.Issues);
        Assert.HasCount(10, viewModel.ProposedDestinations);
        CollectionAssert.Contains(viewModel.ProposedDestinations.ToArray(),
            "resources/canonical/stories/royal_mystery.json");
    }

    private sealed class FakeProjectWorkspaceDialogs : IProjectWorkspaceDialogs
    {
        public bool Confirmed { get; init; }
        public int ConfirmationCount { get; private set; }
        public ProjectCreationRequest? RequestCreate(string? initialParentDirectory = null) => null;
        public bool ConfirmDeleteStory(string storyId, string displayName, IReadOnlyList<string> resourcesToDelete) => false;
        public bool ConfirmCanonicalProjectMigration(CanonicalProjectMigrationPreviewResult preview)
        {
            ConfirmationCount++;
            return Confirmed;
        }
    }

    private sealed class FixedProjectFolderPicker(string directory) : IProjectFolderPicker
    {
        public string? PickProjectFolder() => directory;
    }

    private sealed class TestProject : IDisposable
    {
        private TestProject(string root) => Root = root;
        public string Root { get; }

        public static TestProject Create()
        {
            var root = Path.Combine(Path.GetTempPath(), "dgrpg-migration-ui-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(root, "actors"));
            Directory.CreateDirectory(Path.Combine(root, "dialogues"));
            Directory.CreateDirectory(Path.Combine(root, "quests"));
            Directory.CreateDirectory(Path.Combine(root, "stories"));
            File.WriteAllText(Path.Combine(root, "project.json"),
                "{\"schema_version\":2,\"id\":\"test_project\",\"display_name\":\"Test Project\"}");
            return new TestProject(root);
        }

        public static TestProject CreateApplicable()
        {
            var project = Create();
            var actor = new ActorResource
            {
                SchemaVersion = 2,
                Id = "guard",
                DisplayName = "Guard",
                HomeStoryId = "intro",
            };
            File.WriteAllText(
                Path.Combine(project.Root, "actors", "guard.json"),
                ActorSerializer.Serialize(actor, ActorIdPolicy.ExistingResource));
            var dialogue = new DialogueResource
            {
                SchemaVersion = 2,
                Id = "hello",
                Title = "Hello",
                DisplayName = "Hello",
                HomeStoryId = "intro",
                Speakers = ["guard"],
                Entry = "line",
                Nodes = [DialogueNodeResource.Line("line", "guard", "Hello", null)],
            };
            File.WriteAllText(
                Path.Combine(project.Root, "dialogues", "hello.json"),
                DialogueSerializer.Serialize(dialogue, ActorIdPolicy.ExistingResource));
            var story = new StoryResource
            {
                SchemaVersion = 2,
                Id = "intro",
                DisplayName = "Intro",
                Title = "Intro",
                Entry = "end",
                OwnedResources = new() { Actors = ["guard"], Dialogues = ["hello"] },
                Nodes = [new StoryNodeResource { Id = "end", Type = "end" }],
            };
            File.WriteAllText(
                Path.Combine(project.Root, "stories", "intro.json"),
                StorySerializer.Serialize(story));
            return project;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".tooling", "2.1-acceptance")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
