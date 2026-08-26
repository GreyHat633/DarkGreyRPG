using DarkGreyRPG.Studio.Core.Actors;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class StoryNavigationViewModelTests
{
    [TestMethod]
    public void ProjectHomeSearchesStoriesByIdDisplayNameAndTagsAndBuildsGraph()
    {
        var stories = new[]
        {
            new StoryResource
            {
                Id = "castle_mystery",
                DisplayName = "Castle Mystery",
                Tags = ["main", "mystery"],
                Nodes = [new StoryNodeResource
                {
                    Id = "to_kingdom",
                    Type = "EnterStory",
                    Properties = new Dictionary<string, JsonElement>
                    {
                        ["target_story_id"] = JsonSerializer.SerializeToElement("kingdom"),
                    },
                }],
            },
            new StoryResource { Id = "kingdom", DisplayName = "Kingdom Route", Tags = ["branch"] },
        };
        var home = new ProjectHomeViewModel();
        home.ReplaceStories(stories);

        home.SearchText = "mystery";
        CollectionAssert.AreEqual(new[] { "castle_mystery" }, home.FilteredStories.Select(item => item.Id).ToArray());
        home.SearchText = "kingdom";
        CollectionAssert.AreEqual(new[] { "kingdom" }, home.FilteredStories.Select(item => item.Id).ToArray());
        home.SearchText = "branch";
        CollectionAssert.AreEqual(new[] { "kingdom" }, home.FilteredStories.Select(item => item.Id).ToArray());

        home.ShowGraph();
        Assert.IsTrue(home.IsGraphVisible);
        Assert.HasCount(2, home.Graph.Nodes);
        Assert.AreEqual("2 个剧情 · 1 条转场 · 0 个诊断", home.Graph.Summary);
        Assert.AreEqual("kingdom", home.Graph.Edges.Single().TargetStoryId);
    }

    [TestMethod]
    public void StoryWorkspaceDefaultsOverviewAndAllFiveRoutesExposeDistinctModels()
    {
        var story = new StoryResource
        {
            Id = "castle_mystery",
            DisplayName = "Castle Mystery",
            OwnedResources = new StoryMembership { Actors = ["hero"], Dialogues = ["opening"], Quests = ["investigate"] },
            ReferencedResources = new StoryMembership { Actors = ["merchant", "missing"], Dialogues = ["shared"], Quests = ["shared_quest"] },
            Nodes = [new StoryNodeResource { Id = "start", Type = "START" }],
        };
        var actors = new[]
        {
            new ActorResourceInfo("hero", "Hero", "hero.json", ["main"]),
            new ActorResourceInfo("merchant", "Merchant", "merchant.json", ["shop"]),
        };
        var workspace = new StoryWorkspaceViewModel();

        workspace.OpenStory(story, actors);

        Assert.AreEqual(StoryWorkspaceRoutes.Overview, workspace.CurrentRoute);
        Assert.AreSame(workspace.Overview, workspace.CurrentPage);
        Assert.HasCount(3, workspace.Actors!.Memberships);
        Assert.AreEqual(1, workspace.Actors.OwnedCount);
        Assert.AreEqual(2, workspace.Actors.ReferencedCount);
        Assert.IsTrue(workspace.Actors.Memberships.Single(item => item.Id == "merchant").IsResolved);
        Assert.IsTrue(workspace.Actors.HasMissingActors);

        var changed = new List<string>();
        workspace.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);
        workspace.SelectRoute(StoryWorkspaceRoutes.Actors);
        Assert.AreSame(workspace.Actors, workspace.CurrentPage);
        CollectionAssert.Contains(changed, nameof(StoryWorkspaceViewModel.SelectedRoute));
        workspace.SelectRoute(StoryWorkspaceRoutes.Dialogues);
        Assert.AreSame(workspace.Dialogues, workspace.CurrentPage);
        workspace.SelectRoute(StoryWorkspaceRoutes.Quests);
        Assert.AreSame(workspace.Quests, workspace.CurrentPage);
        workspace.SelectRoute(StoryWorkspaceRoutes.Flow);
        Assert.AreSame(workspace.Flow, workspace.CurrentPage);
        Assert.AreNotEqual(workspace.Overview, workspace.CurrentPage);
    }

    [TestMethod]
    public void ShellOpensProjectHomeThenStoryWithoutSelectingGlobalActor()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var service = new ProjectService();
            service.CreateProject(directory, "demo", "Demo");
            var actor = service.CurrentProject!.Actors.CreateActor("hero", "Hero");
            actor.HomeStoryId = "uncategorized";
            service.CurrentProject.Actors.SaveActor(actor);
            service.CurrentProject.Stories.SaveStory(new StoryResource
            {
                Id = "castle_mystery",
                DisplayName = "Castle Mystery",
                Tags = ["main"],
                OwnedResources = new StoryMembership { Actors = ["hero"] },
                Nodes = [new StoryNodeResource { Id = "end", Type = "END" }],
            });
            service.CloseProject(discardUnsavedChanges: true);

            var shell = new ShellViewModel(service, new FixedProjectFolderPicker(directory));
            shell.OpenProjectCommand.Execute(null);

            Assert.HasCount(2, shell.ProjectHome.Stories);
            Assert.IsNull(shell.SelectedActor);
            Assert.IsNull(shell.CurrentActor);
            Assert.IsTrue(shell.ProjectHome.IsHomeVisible);

            shell.ProjectHome.SelectedStory = shell.ProjectHome.Stories.Single(item => item.Id == "castle_mystery");
            shell.OpenSelectedStoryCommand.Execute(null);

            Assert.AreEqual("castle_mystery", shell.StoryWorkspace.StoryId);
            Assert.AreEqual(StoryWorkspaceRoutes.Overview, shell.StoryWorkspace.CurrentRoute);
            Assert.IsNull(shell.SelectedActor);

            shell.StoryWorkspace.SelectRoute(StoryWorkspaceRoutes.Actors);
            shell.StoryWorkspace.Actors!.SelectedMembership = shell.StoryWorkspace.Actors.Memberships.Single();
            Assert.AreEqual("hero", shell.SelectedActor!.Id);
            Assert.AreEqual("hero", shell.CurrentActor!.Id);
        }
        finally
        {
            TryDelete(directory);
        }
    }

    private static string CreateProjectDirectory() =>
        Path.Combine(AppContext.BaseDirectory, ".test-data", "darkgrey-story-vm-" + Guid.NewGuid().ToString("N"));

    private static void TryDelete(string path)
    {
        if (Directory.Exists(path)) Directory.Delete(path, recursive: true);
    }

    private sealed class FixedProjectFolderPicker(string directory) : IProjectFolderPicker
    {
        public string? PickProjectFolder() => directory;
    }
}
