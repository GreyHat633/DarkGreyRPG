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
        Assert.AreEqual("2 个剧情 · 1 条转场 / 1 组关系 · 0 个诊断", home.Graph.Summary);
        Assert.AreEqual("kingdom", home.Graph.Edges.Single().TargetStoryId);
    }

    [TestMethod]
    public void SelectedStoryExposesFullOverviewAndStoryWorkspaceDefaultsActorsWithFourRoutes()
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
        var home = new ProjectHomeViewModel();
        home.ReplaceStories([story]);
        home.SelectedStory = home.Stories.Single();

        Assert.AreEqual("Castle Mystery", home.SelectedStory.Overview.DisplayName);
        Assert.AreEqual("castle_mystery", home.SelectedStory.Overview.Id);
        Assert.AreEqual(story.Description, home.SelectedStory.Overview.Description);
        Assert.AreEqual("1 个本剧情角色 · 2 个引用角色 · 2 个对话 · 2 个任务", home.SelectedStory.Overview.MembershipSummary);
        Assert.AreEqual(1, home.SelectedStory.Overview.FlowNodeCount);
        Assert.AreEqual(home.SelectedStory.Overview.MembershipSummary, home.SelectedStory.MembershipSummary);
        Assert.AreEqual(home.SelectedStory.Overview.FlowNodeCount, home.SelectedStory.FlowNodeCount);

        var workspace = new StoryWorkspaceViewModel();

        workspace.OpenStory(story, actors);

        Assert.AreEqual(StoryWorkspaceRoutes.Actors, workspace.CurrentRoute);
        CollectionAssert.AreEqual(
            new[] { StoryWorkspaceRoutes.Actors, StoryWorkspaceRoutes.Dialogues, StoryWorkspaceRoutes.Quests, StoryWorkspaceRoutes.Flow },
            workspace.Routes.Select(route => route.Page).ToArray());
        Assert.AreSame(workspace.Actors, workspace.CurrentPage);
        Assert.HasCount(3, workspace.Actors!.Memberships);
        Assert.AreEqual(1, workspace.Actors.OwnedCount);
        Assert.AreEqual(2, workspace.Actors.ReferencedCount);
        Assert.IsTrue(workspace.Actors.Memberships.Single(item => item.Id == "merchant").IsResolved);
        Assert.IsTrue(workspace.Actors.HasMissingActors);

        var changed = new List<string>();
        workspace.PropertyChanged += (_, args) => changed.Add(args.PropertyName ?? string.Empty);
        workspace.SelectRoute(StoryWorkspaceRoutes.Dialogues);
        Assert.AreSame(workspace.Dialogues, workspace.CurrentPage);
        CollectionAssert.Contains(changed, nameof(StoryWorkspaceViewModel.SelectedRoute));
        workspace.SelectRoute(StoryWorkspaceRoutes.Actors);
        Assert.AreSame(workspace.Actors, workspace.CurrentPage);
        workspace.SelectRoute(StoryWorkspaceRoutes.Quests);
        Assert.AreSame(workspace.Quests, workspace.CurrentPage);
        workspace.SelectRoute(StoryWorkspaceRoutes.Flow);
        Assert.AreSame(workspace.Flow, workspace.CurrentPage);
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
            actor.HomeStoryId = "castle_mystery";
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

            Assert.HasCount(1, shell.ProjectHome.Stories);
            Assert.IsNull(shell.SelectedActor);
            Assert.IsNull(shell.CurrentActor);
            Assert.IsTrue(shell.ProjectHome.IsHomeVisible);

            shell.ProjectHome.SelectedStory = shell.ProjectHome.Stories.Single(item => item.Id == "castle_mystery");
            shell.OpenSelectedStoryCommand.Execute(null);

            Assert.AreEqual("castle_mystery", shell.StoryWorkspace.StoryId);
            Assert.AreEqual(StoryWorkspaceRoutes.Actors, shell.StoryWorkspace.CurrentRoute);
            Assert.IsNull(shell.SelectedActor);

            shell.StoryWorkspace.SelectRoute(StoryWorkspaceRoutes.Flow);
            var draft = shell.CurrentFlow!;
            draft.AddNodeAt("PlayDialogue", 240, 160);
            Assert.IsNotEmpty(draft.ValidationErrors);
            shell.StoryWorkspace.SelectRoute(StoryWorkspaceRoutes.Actors);
            Assert.AreEqual(StoryWorkspaceRoutes.Actors, shell.StoryWorkspace.CurrentRoute);
            Assert.AreSame(draft, shell.CurrentFlow);
            Assert.IsTrue(draft.IsDirty);

            shell.StoryWorkspace.Actors!.SelectedMembership = shell.StoryWorkspace.Actors.Memberships.Single();
            Assert.AreEqual("hero", shell.SelectedActor!.Id);
            Assert.AreEqual("hero", shell.CurrentActor!.Id);
        }
        finally
        {
            TryDelete(directory);
        }
    }

    [TestMethod]
    public void LeavingStoryPromptsForInvalidFlowAndSupportsCancelSaveFailureAndDiscard()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var service = new ProjectService();
            service.CreateProject(directory, "flow_leave", "Flow Leave");
            service.CurrentProject!.Stories.SaveStory(new StoryResource
            {
                Id = "draft_story",
                DisplayName = "Draft Story",
                Nodes = [new StoryNodeResource { Id = "end", Type = "END" }],
            });
            service.CloseProject(discardUnsavedChanges: true);
            var dialogs = new FakeFlowWorkspaceDialogs { Choice = UnsavedChangesChoice.Cancel };
            var shell = new ShellViewModel(service, new FixedProjectFolderPicker(directory), flowWorkspaceDialogs: dialogs);
            shell.OpenProjectCommand.Execute(null);
            shell.OpenStory(shell.ProjectHome.Stories.Single(story => story.Id == "draft_story"));
            shell.StoryWorkspace.SelectRoute(StoryWorkspaceRoutes.Flow);
            shell.CurrentFlow!.AddNodeAt("PlayDialogue", 200, 120);

            shell.ShowProjectHomeCommand.Execute(null);
            Assert.IsTrue(shell.StoryWorkspace.HasStory);

            dialogs.Choice = UnsavedChangesChoice.Save;
            shell.ShowProjectHomeCommand.Execute(null);
            Assert.IsTrue(shell.StoryWorkspace.HasStory);
            Assert.IsTrue(shell.BottomPanel.IsExpanded);
            Assert.AreEqual("Problems", shell.BottomPanel.SelectedTab.Page);

            dialogs.Choice = UnsavedChangesChoice.Discard;
            shell.ShowProjectHomeCommand.Execute(null);
            Assert.IsFalse(shell.StoryWorkspace.HasStory);
            Assert.IsTrue(shell.ProjectHome.IsHomeVisible);
            Assert.IsFalse(service.CurrentProject!.Stories.LoadStory("draft_story").Nodes.Any(node => node.Type.Equals("play_dialogue", StringComparison.OrdinalIgnoreCase)));
        }
        finally
        {
            TryDelete(directory);
        }
    }

    [TestMethod]
    public void ShellUndoCommandTracksFlowHistoryAvailability()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var service = new ProjectService();
            service.CreateProject(directory, "flow_history", "Flow History");
            service.CurrentProject!.Stories.SaveStory(new StoryResource
            {
                Id = "history_story",
                DisplayName = "History Story",
                Nodes = [new StoryNodeResource { Id = "end", Type = "END" }],
            });
            service.CloseProject(discardUnsavedChanges: true);
            var shell = new ShellViewModel(service, new FixedProjectFolderPicker(directory));
            shell.OpenProjectCommand.Execute(null);
            shell.OpenStory(shell.ProjectHome.Stories.Single(story => story.Id == "history_story"));
            shell.StoryWorkspace.SelectRoute(StoryWorkspaceRoutes.Flow);
            var before = shell.CurrentFlow!.Nodes.Count;

            Assert.IsFalse(shell.UndoCurrentCommand.CanExecute(null));
            shell.CurrentFlow.AddNodeAt("End", 300, 120);
            Assert.IsTrue(shell.UndoCurrentCommand.CanExecute(null));
            shell.UndoCurrentCommand.Execute(null);
            Assert.HasCount(before, shell.CurrentFlow.Nodes);
        }
        finally
        {
            TryDelete(directory);
        }
    }

    [TestMethod]
    public void OpeningFlowProblemNavigatesSelectsNodeAndCarriesField()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var service = new ProjectService();
            service.CreateProject(directory, "flow_problem", "Flow Problem");
            service.CurrentProject!.Stories.SaveStory(new StoryResource
            {
                Id = "problem_story", DisplayName = "Problem Story", Entry = "dialogue",
                Nodes = [new StoryNodeResource
                {
                    Id = "dialogue", Type = "play_dialogue",
                    Properties = new Dictionary<string, JsonElement>
                    {
                        ["dialogue_id"] = JsonSerializer.SerializeToElement("missing_dialogue"),
                    },
                }],
            });
            service.CloseProject(discardUnsavedChanges: true);
            var dialogs = new FakeFlowWorkspaceDialogs { Choice = UnsavedChangesChoice.Cancel };
            var shell = new ShellViewModel(service, new FixedProjectFolderPicker(directory), flowWorkspaceDialogs: dialogs);
            shell.OpenProjectCommand.Execute(null);
            shell.OpenStory(shell.ProjectHome.Stories.Single(story => story.Id == "problem_story"));
            shell.StoryWorkspace.SelectRoute(StoryWorkspaceRoutes.Flow);
            shell.CurrentFlow!.Nodes.Single(node => node.Id == "dialogue").ResourceValue = "still_missing";
            shell.FocusCurrentFlowProblems();
            var problem = shell.Problems.Problems.Single(item => item.Code == "story.flow.dialogue.missing");

            Assert.AreEqual("story/problem_story/flow/dialogue", problem.Source);
            Assert.AreEqual("dialogue_id", problem.Field);
            shell.OpenProblem(problem);

            Assert.AreEqual(StoryWorkspaceRoutes.Flow, shell.StoryWorkspace.CurrentRoute);
            Assert.AreEqual("dialogue", shell.CurrentFlow!.SelectedNode?.Id);
            Assert.AreEqual("dialogue_id", shell.CurrentFlow.ProblemFocusRequest?.Field);
            Assert.AreEqual(0, dialogs.UnsavedPromptCount);
        }
        finally
        {
            TryDelete(directory);
        }
    }

    [TestMethod]
    public void InvalidFlowDraftIsRecoveredWithoutChangingOfficialJsonAndValidSaveClearsRecovery()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var setup = new ProjectService();
            setup.CreateProject(directory, "flow_recovery", "Flow Recovery");
            setup.CurrentProject!.Stories.SaveStory(new StoryResource
            {
                Id = "recovery_story", DisplayName = "Recovery Story", Entry = "end",
                Nodes = [new StoryNodeResource { Id = "end", Type = "end" }],
            });
            setup.CloseProject(discardUnsavedChanges: true);

            var firstShell = new ShellViewModel(new ProjectService(), new FixedProjectFolderPicker(directory));
            firstShell.OpenProjectCommand.Execute(null);
            firstShell.OpenStory(firstShell.ProjectHome.Stories.Single(story => story.Id == "recovery_story"));
            firstShell.StoryWorkspace.SelectRoute(StoryWorkspaceRoutes.Flow);
            firstShell.CurrentFlow!.AddNodeAt("PlayDialogue", 260, 180);
            var recoveryPath = Path.Combine(directory, "resources", "editor", "recovery", "recovery_story.json");
            Assert.IsTrue(File.Exists(recoveryPath));
            Assert.IsFalse(new StoryRepository(directory).LoadStory("recovery_story").Nodes.Any(node => StoryValidator.CanonicalizeType(node.Type) == "PlayDialogue"));

            var dialogs = new FakeFlowWorkspaceDialogs { RecoveryChoice = StoryFlowRecoveryChoice.Recover };
            var restarted = new ShellViewModel(new ProjectService(), new FixedProjectFolderPicker(directory), flowWorkspaceDialogs: dialogs);
            restarted.OpenProjectCommand.Execute(null);
            restarted.OpenStory(restarted.ProjectHome.Stories.Single(story => story.Id == "recovery_story"));
            restarted.StoryWorkspace.SelectRoute(StoryWorkspaceRoutes.Flow);
            var recoveredNode = restarted.CurrentFlow!.Nodes.Single(node => node.CanonicalType == "PlayDialogue");

            Assert.IsTrue(restarted.CurrentFlow.IsDirty);
            Assert.AreEqual(StoryFlowRecoveryChoice.Recover, dialogs.LastRecoveryChoice);
            Assert.IsFalse(new StoryRepository(directory).LoadStory("recovery_story").Nodes.Any(node => StoryValidator.CanonicalizeType(node.Type) == "PlayDialogue"));

            restarted.CurrentFlow.SelectOnly(recoveredNode);
            restarted.CurrentFlow.DeleteSelectionCommand.Execute(null);
            restarted.SaveCurrentResourceCommand.Execute(null);

            Assert.IsFalse(File.Exists(recoveryPath));
            Assert.IsFalse(restarted.CurrentFlow.IsDirty);
        }
        finally
        {
            TryDelete(directory);
        }
    }

    [TestMethod]
    public void RecoveryCanBeIgnoredForSessionOrDeletedWhenStoryOpens()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var setup = new ProjectService();
            setup.CreateProject(directory, "recovery_choices", "Recovery Choices");
            foreach (var id in new[] { "ignore_story", "delete_story" })
                setup.CurrentProject!.Stories.SaveStory(new StoryResource
                {
                    Id = id, DisplayName = id, Entry = "end",
                    Nodes = [new StoryNodeResource { Id = "end", Type = "end" }],
                });
            setup.CloseProject(discardUnsavedChanges: true);
            var store = new StoryFlowRecoveryStore(directory);
            foreach (var id in new[] { "ignore_story", "delete_story" })
                store.Save(new StoryResource
                {
                    Id = id, DisplayName = id, Entry = "end",
                    Nodes =
                    [
                        new StoryNodeResource { Id = "end", Type = "end" },
                        new StoryNodeResource { Id = "draft", Type = "play_dialogue" },
                    ],
                });

            var dialogs = new FakeFlowWorkspaceDialogs { RecoveryChoice = StoryFlowRecoveryChoice.Ignore };
            var shell = new ShellViewModel(new ProjectService(), new FixedProjectFolderPicker(directory), flowWorkspaceDialogs: dialogs);
            shell.OpenProjectCommand.Execute(null);
            Assert.IsTrue(shell.Output.Entries.Any(entry => entry.Message.Contains("2 个 Story Flow 恢复草稿", StringComparison.Ordinal)));
            shell.OpenStory(shell.ProjectHome.Stories.Single(story => story.Id == "ignore_story"));
            Assert.IsFalse(shell.CurrentFlow!.Nodes.Any(node => node.Id == "draft"));
            Assert.IsNotNull(store.Load("ignore_story"));

            dialogs.RecoveryChoice = StoryFlowRecoveryChoice.Delete;
            shell.OpenStory(shell.ProjectHome.Stories.Single(story => story.Id == "delete_story"));
            Assert.IsFalse(shell.CurrentFlow!.Nodes.Any(node => node.Id == "draft"));
            Assert.IsNull(store.Load("delete_story"));
        }
        finally
        {
            TryDelete(directory);
        }
    }

    [TestMethod]
    public void ProjectGraphProblemsFocusStoryOrOpenMissingTargetEnterStory()
    {
        var directory = CreateProjectDirectory();
        try
        {
            var setup = new ProjectService();
            setup.CreateProject(directory, "graph_problems", "Graph Problems");
            setup.CurrentProject!.Stories.SaveStory(new StoryResource
            {
                Id = "source", DisplayName = "Source", Entry = "enter_missing",
                Nodes = [new StoryNodeResource
                {
                    Id = "enter_missing", Type = "enter_story",
                    Properties = new Dictionary<string, JsonElement>
                    {
                        ["target_story_id"] = JsonSerializer.SerializeToElement("missing"),
                    },
                }],
            });
            setup.CurrentProject.Stories.SaveStory(new StoryResource { Id = "isolated", DisplayName = "Isolated", Entry = "end", Nodes = [new StoryNodeResource { Id = "end", Type = "end" }] });
            setup.CloseProject(discardUnsavedChanges: true);
            var shell = new ShellViewModel(new ProjectService(), new FixedProjectFolderPicker(directory));
            shell.OpenProjectCommand.Execute(null);
            shell.FocusProjectGraphProblems();

            var missing = shell.Problems.Problems.Single(problem => problem.Code == "project_graph.target.missing");
            Assert.AreEqual("project-graph/source/enter_missing", missing.Source);
            shell.OpenProblem(missing);
            Assert.AreEqual("source", shell.CurrentFlow?.Id);
            Assert.AreEqual("enter_missing", shell.CurrentFlow?.SelectedNode?.Id);
            Assert.AreEqual("target_story_id", shell.CurrentFlow?.ProblemFocusRequest?.Field);

            shell.ShowProjectHomeCommand.Execute(null);
            shell.ShowProjectGraphCommand.Execute(null);
            shell.FocusProjectGraphProblems();
            var isolated = shell.Problems.Problems.First(problem => problem.Code == "project_graph.story.isolated");
            shell.OpenProblem(isolated);
            Assert.IsTrue(shell.ProjectHome.IsGraphVisible);
            Assert.AreEqual(isolated.Source?.Split('/')[1], shell.ProjectHome.Graph.ProblemFocusRequest?.StoryId);
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

    private sealed class FakeFlowWorkspaceDialogs : IFlowWorkspaceDialogs
    {
        public UnsavedChangesChoice Choice { get; set; }
        public StoryFlowRecoveryChoice RecoveryChoice { get; set; } = StoryFlowRecoveryChoice.Ignore;
        public StoryFlowRecoveryChoice? LastRecoveryChoice { get; private set; }
        public int UnsavedPromptCount { get; private set; }
        public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges(StoryFlowEditorViewModel flow)
        {
            UnsavedPromptCount++;
            return Choice;
        }
        public StoryFlowRecoveryChoice ChooseRecovery(StoryFlowRecoverySnapshot snapshot)
        {
            LastRecoveryChoice = RecoveryChoice;
            return RecoveryChoice;
        }
    }
}
