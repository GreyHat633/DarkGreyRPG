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
        Assert.AreEqual("Canonical · 数据不完整", incomplete.TagsText);
        Assert.IsTrue(shell.Problems.Problems.Any(problem =>
            problem.Source == "canonical-discovery/membership_only"
            && problem.Code == "story.discovery.root.missing"));
        shell.OpenStory(incomplete);
        Assert.IsFalse(shell.HasCanonicalStoryWorkspace);
        StringAssert.Contains(shell.StatusMessage, "打开 Canonical Story");
    }

    [TestMethod]
    public void ProjectGraphUsesCanonicalOnlyStoryFlowsAndPublishesPreciseProblems()
    {
        using var project = new DiscoveryProjectFixture();
        project.CreateCanonicalStory("source", "Source",
        [
            Enter("to_target", "target"),
            Enter("to_missing", "missing"),
        ]);
        project.CreateCanonicalStory("target", "Target");

        var shell = project.OpenShell();

        CollectionAssert.AreEquivalent(
            new[] { "source", "target" },
            shell.ProjectHome.Graph.Nodes.Select(node => node.Id).ToArray());
        var edge = shell.ProjectHome.Graph.Edges.Single();
        Assert.AreEqual("source", edge.SourceStoryId);
        Assert.AreEqual("target", edge.TargetStoryId);
        Assert.AreEqual("to_target", edge.Transitions.Single().NodeId);
        var problem = shell.Problems.Problems.Single(item =>
            item.Code == "project_graph.target.missing"
            && item.Source == "project-graph/source/to_missing");
        Assert.AreEqual(ValidationSeverity.Error, problem.Severity);
        shell.OpenProblem(problem);
        Assert.AreEqual("source", shell.CanonicalStoryWorkspace?.StoryEditor.Id);
        Assert.AreEqual("to_missing", shell.CanonicalStoryWorkspace?.StoryNodeFocusRequest?.NodeId);
        Assert.AreEqual("target_story_id", shell.CanonicalStoryWorkspace?.StoryNodeFocusRequest?.Field);
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
