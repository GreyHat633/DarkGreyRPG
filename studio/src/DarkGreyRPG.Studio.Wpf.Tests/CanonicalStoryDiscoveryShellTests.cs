using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Validation;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.ViewModels;
using System.Text.Json;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class CanonicalStoryDiscoveryShellTests
{
    [TestMethod]
    public void ProjectHomeListsAndOpensCanonicalOnlyStoryWithoutLegacyAlias()
    {
        using var project = new DiscoveryProjectFixture();
        project.CreateLegacyStory("legacy", "Legacy");
        project.CreateCanonicalStory("canonical_only", "Canonical Only");

        var shell = project.OpenShell();

        CollectionAssert.AreEquivalent(
            new[] { "legacy", "canonical_only" },
            shell.ProjectHome.Stories.Select(story => story.Id).ToArray());
        var canonical = shell.ProjectHome.Stories.Single(story => story.Id == "canonical_only");
        Assert.IsTrue(canonical.IsCanonicalOnly);
        Assert.IsFalse(canonical.CanDeleteLegacyStory);
        shell.OpenStory(canonical);
        Assert.IsTrue(shell.HasCanonicalStoryWorkspace);
        Assert.AreEqual("canonical_only", shell.CanonicalStoryWorkspace!.StoryEditor.Id);
        Assert.IsTrue(shell.DeleteSelectedStoryCommand.CanExecute(null));
    }

    [TestMethod]
    public void SameIdCanonicalStoryWinsAndIncompleteRootRemainsVisibleButFailsClosed()
    {
        using var project = new DiscoveryProjectFixture();
        project.CreateLegacyStory("shared", "Old Shared");
        project.CreateCanonicalStory("shared", "Canonical Shared");
        project.Store.Memberships.Create(new CanonicalStoryMembershipManifest("membership_only"));

        var shell = project.OpenShell();

        Assert.AreEqual(1, shell.ProjectHome.Stories.Count(story => story.Id == "shared"));
        var shared = shell.ProjectHome.Stories.Single(story => story.Id == "shared");
        Assert.AreEqual("Canonical Shared", shared.DisplayName);
        Assert.IsTrue(shared.HasLegacyStory);
        Assert.IsTrue(shared.HasCanonicalStory);
        shell.ProjectHome.SelectedStory = shared;
        Assert.IsTrue(shell.DeleteSelectedStoryCommand.CanExecute(null));
        shell.OpenStory(shared);
        Assert.IsTrue(shell.HasCanonicalStoryWorkspace);

        var incomplete = shell.ProjectHome.Stories.Single(story => story.Id == "membership_only");
        Assert.IsTrue(incomplete.IsCanonicalOnly);
        Assert.AreEqual("新格式 · 数据不完整", incomplete.TagsText);
        Assert.IsTrue(shell.Problems.Problems.Any(problem =>
            problem.Source == "canonical-discovery/membership_only"
            && problem.Code == "story.discovery.root.missing"));
        shell.OpenStory(incomplete);
        Assert.IsFalse(shell.HasCanonicalStoryWorkspace);
        StringAssert.Contains(shell.StatusMessage, "打开故事");
    }

    [TestMethod]
    public void ProjectGraphCannotReintroduceRetiredStandaloneTransitions()
    {
        using var project = new DiscoveryProjectFixture();
        project.CreateCanonicalStory("source", "Source");
        project.CreateCanonicalStory("target", "Target");
        Assert.ThrowsExactly<GraphResourceRepositoryException>(() => project.Store.Stories.Replace(
            new GraphResourceEnvelope(GraphResourceKind.Story, "source", "Source",
                new GraphDocument([Enter("old", "target")]))));
        var shell = project.OpenShell();
        CollectionAssert.AreEquivalent(new[] { "source", "target" }, shell.ProjectHome.Graph.Nodes.Select(node => node.Id).ToArray());
        Assert.IsEmpty(shell.ProjectHome.Graph.Edges);
        shell.OpenStory(shell.ProjectHome.Stories.Single(story => story.Id == "source"));
        Assert.IsTrue(shell.HasCanonicalStoryWorkspace);
    }

    private static GraphResourceEnvelope Envelope(
        string id,
        string displayName,
        IEnumerable<GraphNode>? nodes = null)
        => new(
            GraphResourceKind.Story,
            id,
            displayName,
            new GraphDocument(nodes ?? [GraphNodeFactory.Create(GraphScope.StoryFlow, "start", "start")]));

    private static GraphNode Enter(string id, string target)
        => new(id, "enter_story", id, properties: new Dictionary<string, JsonElement>
        {
            ["target_story_id"] = JsonSerializer.SerializeToElement(target),
        });

    private sealed class DiscoveryProjectFixture : IDisposable
    {
        public DiscoveryProjectFixture()
        {
            Root = Path.Combine(
                AppContext.BaseDirectory,
                "temp",
                "canonical-discovery-shell-" + Guid.NewGuid().ToString("N"));
            Setup = new ProjectService();
            Setup.CreateProject(Root, "test_project", "Test Project");
            Store = new CanonicalProjectGraphStore(Root);
        }

        public string Root { get; }
        public ProjectService Setup { get; }
        public CanonicalProjectGraphStore Store { get; }

        public void CreateLegacyStory(string id, string displayName)
            => Setup.CreateStory(id, displayName);

        public void CreateCanonicalStory(
            string id,
            string displayName,
            IEnumerable<GraphNode>? nodes = null)
        {
            Store.Stories.Create(Envelope(id, displayName, nodes));
            Store.Memberships.Create(new CanonicalStoryMembershipManifest(id));
        }

        public ShellViewModel OpenShell()
        {
            var shell = new ShellViewModel(
                new ProjectService(),
                new FixedProjectFolderPicker(Root));
            shell.OpenProjectCommand.Execute(null);
            return shell;
        }

        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class FixedProjectFolderPicker(string projectDirectory) : IProjectFolderPicker
    {
        public string? PickProjectFolder() => projectDirectory;
    }
}
