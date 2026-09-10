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
    public void ShellHasNoUserFacingMigrationCommandOrAlias()
    {
        Assert.IsNull(typeof(ShellViewModel).GetProperty("MigrateCanonicalProjectCommand"));
        Assert.IsNull(typeof(ShellViewModel).GetProperty("MigrateProjectCommand"));
    }

    [TestMethod]
    public void InternalCompatibilityTransactionStillUpgradesLegacyDefinitions()
    {
        using var project = TestProject.CreateApplicable();
        var preview = CanonicalProjectMigrationPreview.PreviewProject(project.Root);
        Assert.IsTrue(preview.CanApply, string.Join(";", preview.Issues.Select(issue => issue.Message)));
        new CanonicalProjectMigrationTransaction().Apply(preview);
        Assert.IsTrue(File.Exists(Path.Combine(project.Root, "resources/canonical/stories/intro.json")));
        Assert.IsTrue(File.Exists(Path.Combine(project.Root, "migration.log")));
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
