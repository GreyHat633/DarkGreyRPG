using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Validation;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class ShellViewModelTests
{
    [TestMethod]
    public void PhaseOneAcceptanceCreatesTeacherAndRestoresItAfterRestart()
    {
        using var directory = new TestProjectDirectory();
        var destination = Path.Combine(directory.Root, "acceptance_project");
        var shell = new ShellViewModel(
            new ProjectService(),
            new FixedProjectFolderPicker(destination),
            new FakeActorWorkspaceDialogs
            {
                CreationMode = ActorCreationMode.Blank,
                CreateResult = new ActorIdentityRequest("teacher", "老师"),
            },
            new FakeProjectWorkspaceDialogs(
                new ProjectCreationRequest(destination, "school_rpg", "学校 RPG")),
            new FakeResourceWorkspaceDialogs
            {
                CreateResult = new ResourceIdentityRequest("school_story", "校园剧情"),
            });

        shell.NewProjectCommand.Execute(null);
        Assert.IsEmpty(shell.ProjectHome.Stories);
        shell.CreateStoryCommand.Execute(null);
        OpenStoryActors(shell, "school_story");
        shell.NewActorCommand.Execute(null);
        shell.CurrentActor!.Notes = "学校中的任务 NPC";
        shell.CurrentActor.TagsText = "school, quest";
        shell.SaveActorCommand.Execute(null);

        var restartedShell = new ShellViewModel(
            new ProjectService(),
            new FixedProjectFolderPicker("unused"));
        Assert.IsTrue(restartedShell.RestoreLastProject(destination));
        restartedShell.SelectedActor = restartedShell.Actors.Single(actor => actor.Id == "teacher");

        Assert.AreEqual("老师", restartedShell.CurrentActor?.DisplayName);
        Assert.AreEqual("学校中的任务 NPC", restartedShell.CurrentActor?.Notes);
        Assert.AreEqual("school, quest", restartedShell.CurrentActor?.TagsText);
    }

    [TestMethod]
    public void OpenSelectEditSaveAndRestartReloadUsesRealProjectData()
    {
        using var directory = new TestProjectDirectory();
        var repository = new ActorRepository(directory.Root);
        repository.SaveActor(repository.CreateActor("teacher", "Teacher"));

        var shell = CreateShell(directory.Root);
        shell.OpenProjectCommand.Execute(null);

        Assert.IsTrue(shell.HasProject);
        Assert.AreEqual(directory.Root, shell.ProjectDirectory);
        Assert.HasCount(1, shell.Actors);

        shell.SelectedActor = shell.Actors.Single();
        Assert.IsNotNull(shell.CurrentActor);

        shell.CurrentActor.DisplayName = "老师";
        shell.CurrentActor.Notes = "学校中的任务 NPC";
        shell.CurrentActor.TagsText = "school, quest";

        Assert.IsTrue(shell.SaveActorCommand.CanExecute(null));
        shell.SaveActorCommand.Execute(null);
        Assert.IsFalse(shell.SaveActorCommand.CanExecute(null));

        var restartedShell = CreateShell(directory.Root);
        restartedShell.OpenProjectCommand.Execute(null);
        restartedShell.SelectedActor = restartedShell.Actors.Single();

        Assert.IsNotNull(restartedShell.CurrentActor);
        Assert.AreEqual("老师", restartedShell.CurrentActor.DisplayName);
        Assert.AreEqual("学校中的任务 NPC", restartedShell.CurrentActor.Notes);
        Assert.AreEqual("school, quest", restartedShell.CurrentActor.TagsText);
    }

    [TestMethod]
    public void SearchFiltersActorsByIdDisplayNameAndTag()
    {
        using var directory = new TestProjectDirectory();
        var repository = new ActorRepository(directory.Root);
        var teacher = repository.CreateActor("teacher", "老师");
        teacher.SetTags(["school"]);
        repository.SaveActor(teacher);
        repository.SaveActor(repository.CreateActor("merchant", "商人"));
        var shell = CreateShell(directory.Root);
        shell.OpenProjectCommand.Execute(null);

        shell.SearchText = "school";
        Assert.AreEqual("teacher", shell.FilteredActors.Single().Id);
        shell.SearchText = "商人";
        Assert.AreEqual("merchant", shell.FilteredActors.Single().Id);
        shell.SearchText = "tea";
        Assert.AreEqual("teacher", shell.FilteredActors.Single().Id);
        shell.SearchText = string.Empty;
        Assert.HasCount(2, shell.FilteredActors);
    }

    [TestMethod]
    public void CreateDuplicateRenameAndDeleteOperateOnRealFiles()
    {
        using var directory = new TestProjectDirectory();
        var dialogs = new FakeActorWorkspaceDialogs
        {
            CreationMode = ActorCreationMode.Blank,
            CreateResult = new ActorIdentityRequest("student", "学生"),
            RenameResult = "student_copy_renamed",
            DeleteConfirmed = true,
        };
        var shell = CreateShell(directory.Root, dialogs);
        shell.OpenProjectCommand.Execute(null);
        OpenStoryActors(shell, "uncategorized");

        shell.NewActorCommand.Execute(null);
        Assert.IsTrue(File.Exists(Path.Combine(directory.Root, "actors", "student.json")));
        Assert.AreEqual("student", shell.SelectedActor?.Id);

        shell.DuplicateActorCommand.Execute(null);
        Assert.IsTrue(File.Exists(Path.Combine(directory.Root, "actors", "student_copy.json")));
        Assert.AreEqual("student_copy", shell.SelectedActor?.Id);

        shell.RenameActorCommand.Execute(null);
        Assert.IsFalse(File.Exists(Path.Combine(directory.Root, "actors", "student_copy.json")));
        Assert.IsTrue(File.Exists(Path.Combine(directory.Root, "actors", "student_copy_renamed.json")));
        Assert.AreEqual("student_copy_renamed", shell.SelectedActor?.Id);

        shell.DeleteActorCommand.Execute(null);
        Assert.IsFalse(File.Exists(Path.Combine(directory.Root, "actors", "student_copy_renamed.json")));
        Assert.IsNull(shell.CurrentActor);
    }

    [TestMethod]
    public void DeleteSelectedStoryRequiresConfirmationAndRefreshesProjectHome()
    {
        using var directory = new TestProjectDirectory();
        var projectDirectory = Path.Combine(directory.Root, "story_delete_project");
        var setup = new ProjectService();
        var session = setup.CreateProject(projectDirectory, "story_delete", "Story Delete");
        session.Stories.CreateStory("deletable", "Deletable Story");
        setup.CreateActorInStory("deletable", "cascade_actor", "Cascade Actor");
        setup.CreateDialogueInStory("deletable", "cascade_dialogue", "Cascade Dialogue");
        setup.CreateQuestInStory("deletable", "cascade_quest", "Cascade Quest");
        setup.CloseProject(discardUnsavedChanges: true);

        var dialogs = new FakeProjectWorkspaceDialogs(null);
        var shell = new ShellViewModel(
            new ProjectService(),
            new FixedProjectFolderPicker(projectDirectory),
            projectWorkspaceDialogs: dialogs);
        shell.OpenProjectCommand.Execute(null);
        shell.ProjectHome.SelectedStory = shell.ProjectHome.Stories.Single(story => story.Id == "deletable");

        Assert.IsTrue(shell.DeleteSelectedStoryCommand.CanExecute(null));
        shell.DeleteSelectedStoryCommand.Execute(null);

        Assert.AreEqual(1, dialogs.DeleteStoryConfirmationCount);
        Assert.AreEqual("deletable", dialogs.LastDeleteStoryId);
        CollectionAssert.AreEquivalent(
            new[] { "角色：cascade_actor", "对话：cascade_dialogue", "任务：cascade_quest" },
            dialogs.LastDeleteStoryResources.ToArray());
        Assert.IsTrue(File.Exists(Path.Combine(projectDirectory, "stories", "deletable.json")));
        Assert.IsTrue(File.Exists(Path.Combine(projectDirectory, "actors", "cascade_actor.json")));
        Assert.IsTrue(File.Exists(Path.Combine(projectDirectory, "dialogues", "cascade_dialogue.json")));
        Assert.IsTrue(File.Exists(Path.Combine(projectDirectory, "quests", "cascade_quest.json")));

        dialogs.DeleteStoryConfirmed = true;
        shell.DeleteSelectedStoryCommand.Execute(null);

        Assert.AreEqual(2, dialogs.DeleteStoryConfirmationCount);
        Assert.IsFalse(File.Exists(Path.Combine(projectDirectory, "stories", "deletable.json")));
        Assert.IsFalse(File.Exists(Path.Combine(projectDirectory, "actors", "cascade_actor.json")));
        Assert.IsFalse(File.Exists(Path.Combine(projectDirectory, "dialogues", "cascade_dialogue.json")));
        Assert.IsFalse(File.Exists(Path.Combine(projectDirectory, "quests", "cascade_quest.json")));
        Assert.IsFalse(shell.ProjectHome.Stories.Any(story => story.Id == "deletable"));
        Assert.IsNull(shell.ProjectHome.SelectedStory);
        Assert.IsEmpty(shell.ProjectHome.Stories);
        Assert.IsFalse(File.Exists(Path.Combine(projectDirectory, "stories", "uncategorized.json")));
    }

    [TestMethod]
    public void M4BlankAndImportCreationPersistSelectedStoryAndIndependentData()
    {
        using var directory = new TestProjectDirectory();
        var projectDirectory = Path.Combine(directory.Root, "m4_create_project");
        var setup = new ProjectService();
        var session = setup.CreateProject(projectDirectory, "m4_create", "M4 Create");
        session.Stories.CreateStory("uncategorized", "未分类");
        session.Stories.CreateStory("beta", "Beta Story");
        var source = setup.CreateActorInStory("uncategorized", "source", "Source");
        source.Notes = "template notes";
        source.SetTags(["template"]);
        setup.SaveActor(source);

        var blankShell = CreateShell(projectDirectory, new FakeActorWorkspaceDialogs
        {
            CreationMode = ActorCreationMode.Blank,
            CreateResult = new ActorIdentityRequest("blank_actor", "Blank Actor"),
        });
        blankShell.OpenProjectCommand.Execute(null);
        OpenStoryActors(blankShell, "beta");
        blankShell.NewActorCommand.Execute(null);

        var repository = new ActorRepository(projectDirectory);
        Assert.AreEqual("beta", repository.LoadActor("blank_actor").HomeStoryId);
        CollectionAssert.Contains(session.Stories.LoadStory("beta").OwnedResources.Actors, "blank_actor");

        var sourceInfo = repository.ListActors().Single(actor => actor.Id == "source");
        var importShell = CreateShell(projectDirectory, new FakeActorWorkspaceDialogs
        {
            CreationMode = ActorCreationMode.ImportAsNew,
            PickResult = sourceInfo,
            ImportResult = new ActorIdentityRequest("source_beta", "Beta Source"),
        });
        importShell.OpenProjectCommand.Execute(null);
        OpenStoryActors(importShell, "beta");
        importShell.NewActorCommand.Execute(null);

        Assert.AreEqual("source_beta", importShell.SelectedActor?.Id);
        Assert.AreEqual("beta", repository.LoadActor("source_beta").HomeStoryId);
        Assert.AreEqual("template notes", repository.LoadActor("source_beta").Notes);
        importShell.CurrentActor!.Notes = "independent edit";
        importShell.SaveActorCommand.Execute(null);
        Assert.AreEqual("template notes", repository.LoadActor("source").Notes);
        Assert.AreEqual("independent edit", repository.LoadActor("source_beta").Notes);
    }

    [TestMethod]
    public void M4ReferenceSharesDataRemovePreservesFileAndDeleteIsProtected()
    {
        using var directory = new TestProjectDirectory();
        var projectDirectory = Path.Combine(directory.Root, "m4_reference_project");
        var setup = new ProjectService();
        var session = setup.CreateProject(projectDirectory, "m4_reference", "M4 Reference");
        session.Stories.CreateStory("uncategorized", "未分类");
        session.Stories.CreateStory("beta", "Beta Story");
        setup.CreateActorInStory("uncategorized", "shared", "Shared Actor");
        var repository = new ActorRepository(projectDirectory);
        var sharedInfo = repository.ListActors().Single(actor => actor.Id == "shared");
        var dialogs = new FakeActorWorkspaceDialogs
        {
            PickResult = sharedInfo,
            DeleteConfirmed = true,
        };
        var shell = CreateShell(projectDirectory, dialogs);
        shell.OpenProjectCommand.Execute(null);
        OpenStoryActors(shell, "beta");

        shell.ReferenceActorCommand.Execute(null);
        var referenced = shell.StoryWorkspace.Actors!.Memberships.Single(item => item.Id == "shared");
        Assert.IsTrue(referenced.IsReferenced);
        Assert.IsTrue(referenced.SourceLabel.StartsWith("来自：", StringComparison.Ordinal));
        shell.CurrentActor!.Notes = "edited through reference";
        shell.SaveActorCommand.Execute(null);
        Assert.AreEqual("edited through reference", repository.LoadActor("shared").Notes);

        OpenStoryActors(shell, "uncategorized");
        shell.SelectedStoryActor = shell.StoryWorkspace.Actors!.Memberships.Single(item => item.Id == "shared");
        shell.DeleteActorCommand.Execute(null);
        Assert.IsTrue(File.Exists(Path.Combine(projectDirectory, "actors", "shared.json")));
        Assert.HasCount(1, dialogs.LastShownReferences);
        Assert.AreEqual("beta", dialogs.LastShownReferences[0].Id);

        OpenStoryActors(shell, "beta");
        shell.SelectedStoryActor = shell.StoryWorkspace.Actors!.Memberships.Single(item => item.Id == "shared");
        shell.RemoveActorReferenceCommand.Execute(null);
        Assert.IsTrue(File.Exists(Path.Combine(projectDirectory, "actors", "shared.json")));
        CollectionAssert.DoesNotContain(session.Stories.LoadStory("beta").ReferencedResources.Actors, "shared");

        OpenStoryActors(shell, "uncategorized");
        shell.SelectedStoryActor = shell.StoryWorkspace.Actors!.Memberships.Single(item => item.Id == "shared");
        shell.DeleteActorCommand.Execute(null);
        Assert.IsFalse(File.Exists(Path.Combine(projectDirectory, "actors", "shared.json")));
    }

    [TestMethod]
    public void DirtyActorDisablesDestructiveWorkspaceCommands()
    {
        using var directory = new TestProjectDirectory();
        var repository = new ActorRepository(directory.Root);
        repository.SaveActor(repository.CreateActor("teacher", "Teacher"));
        var shell = CreateShell(directory.Root, new FakeActorWorkspaceDialogs());
        shell.OpenProjectCommand.Execute(null);
        shell.SelectedActor = shell.Actors.Single();

        shell.CurrentActor!.DisplayName = "Changed";

        Assert.IsFalse(shell.DuplicateActorCommand.CanExecute(null));
        Assert.IsFalse(shell.RenameActorCommand.CanExecute(null));
        Assert.IsFalse(shell.DeleteActorCommand.CanExecute(null));
    }

    [TestMethod]
    public void DirtyActorPromptsBeforeSwitchAndClose()
    {
        using var directory = new TestProjectDirectory();
        var repository = new ActorRepository(directory.Root);
        repository.SaveActor(repository.CreateActor("a", "A"));
        repository.SaveActor(repository.CreateActor("b", "B"));
        var dialogs = new FakeActorWorkspaceDialogs
        {
            SaveBeforeSwitch = false,
            CloseChoice = UnsavedChangesChoice.Cancel,
        };
        var shell = CreateShell(directory.Root, dialogs);
        shell.OpenProjectCommand.Execute(null);
        shell.SelectedActor = shell.Actors.Single(actor => actor.Id == "a");
        shell.CurrentActor!.Notes = "dirty";

        shell.SelectedActor = shell.Actors.Single(actor => actor.Id == "b");

        Assert.AreEqual("a", shell.SelectedActor?.Id);
        Assert.IsFalse(shell.TryClose());
    }

    [TestMethod]
    public void FeedbackSurfacesTrackOperationsAndValidationProblems()
    {
        using var directory = new TestProjectDirectory();
        var repository = new ActorRepository(directory.Root);
        repository.SaveActor(repository.CreateActor("teacher", "Teacher"));
        var shell = CreateShell(directory.Root, new FakeActorWorkspaceDialogs());

        shell.OpenProjectCommand.Execute(null);

        Assert.IsTrue(shell.Toast.IsVisible);
        Assert.IsTrue(shell.Output.Entries.Any(entry => entry.Kind == OutputKind.Success));
        shell.SelectedActor = shell.Actors.Single();
        shell.CurrentActor!.DisplayName = string.Empty;

        Assert.IsTrue(shell.Problems.HasErrors);
        Assert.AreEqual("actor/teacher", shell.Problems.Problems[0].Source);

        shell.OpenProblem(shell.Problems.Problems[0]);
        Assert.AreEqual("Story", shell.Navigation.SelectedItem.Page);
        Assert.AreEqual("teacher", shell.SelectedActor?.Id);
    }

    [TestMethod]
    public void OpenFailureUsesExplicitErrorOutputProblemAndToast()
    {
        var missing = Path.Combine(AppContext.BaseDirectory, ".test-data", Guid.NewGuid().ToString("N"));
        var shell = CreateShell(missing, new FakeActorWorkspaceDialogs());

        shell.OpenProjectCommand.Execute(null);

        Assert.AreEqual(ToastKind.Error, shell.Toast.Kind);
        Assert.IsTrue(shell.Toast.Message.StartsWith("打开项目失败：", StringComparison.Ordinal));
        Assert.AreEqual(OutputKind.Error, shell.Output.Entries.Last().Kind);
        Assert.IsTrue(shell.Problems.HasErrors);
        Assert.AreEqual("operation.failure", shell.Problems.Problems.Single().Code);
    }

    [TestMethod]
    public void MenuCommandsSaveAllValidateNavigateAndTogglePanels()
    {
        using var directory = new TestProjectDirectory();
        var repository = new ActorRepository(directory.Root);
        repository.SaveActor(repository.CreateActor("teacher", "Teacher"));
        var shell = CreateShell(directory.Root, new FakeActorWorkspaceDialogs());
        shell.OpenProjectCommand.Execute(null);
        shell.SelectedActor = shell.Actors.Single();
        shell.CurrentActor!.Notes = "saved by Save All";

        Assert.IsTrue(shell.SaveAllCommand.CanExecute(null));
        shell.SaveAllCommand.Execute(null);
        Assert.IsFalse(shell.CurrentActor.Document.IsDirty);
        Assert.AreEqual("saved by Save All", repository.LoadActor("teacher").Notes);

        shell.ValidateProjectCommand.Execute(null);
        Assert.AreEqual("Problems", shell.BottomPanel.SelectedTab.Page);
        Assert.IsTrue(shell.Problems.HasProblems);

        shell.ShowProjectSettingsCommand.Execute(null);
        Assert.AreEqual("Settings", shell.Navigation.SelectedItem.Page);

        shell.ToggleResourceBrowserCommand.Execute(null);
        shell.ToggleBottomPanelCommand.Execute(null);
        Assert.IsFalse(shell.IsResourceBrowserVisible);
        Assert.IsTrue(shell.BottomPanel.IsExpanded);
    }

    [TestMethod]
    public void ProblemsDoNotForceBottomPanelExpansion()
    {
        using var directory = new TestProjectDirectory();
        var shell = CreateShell(directory.Root);

        shell.OpenProjectCommand.Execute(null);
        Assert.IsFalse(shell.BottomPanel.IsExpanded);

        shell.Problems.Replace(
        [
            new ProblemItem(ValidationSeverity.Error, "test.error", "A test error.")
        ]);

        Assert.IsTrue(shell.Problems.HasErrors);
        Assert.IsFalse(shell.BottomPanel.IsExpanded);
    }

    [TestMethod]
    public void NewProjectCommandCreatesRuntimeCompatibleLayoutAndOpensIt()
    {
        using var directory = new TestProjectDirectory();
        var destination = Path.Combine(directory.Root, "created_project");
        var shell = new ShellViewModel(
            new ProjectService(),
            new FixedProjectFolderPicker(directory.Root),
            new FakeActorWorkspaceDialogs(),
            new FakeProjectWorkspaceDialogs(
                new ProjectCreationRequest(destination, "school_rpg", "学校 RPG")));

        shell.NewProjectCommand.Execute(null);

        Assert.IsTrue(shell.HasProject);
        Assert.AreEqual(destination, shell.ProjectDirectory);
        Assert.IsTrue(File.Exists(Path.Combine(destination, "project.json")));
        foreach (var name in new[] { "actors", "dialogues", "quests", "stories", "resources" })
        {
            Assert.IsTrue(Directory.Exists(Path.Combine(destination, name)), name);
        }
        Assert.IsEmpty(Directory.EnumerateFiles(Path.Combine(destination, "stories"), "*.json"));
        Assert.IsEmpty(shell.ProjectHome.Stories);
        Assert.IsTrue(shell.StatusMessage.Contains("新建剧情", StringComparison.Ordinal));
    }

    [TestMethod]
    public void CreateStoryCommandPersistsStoryAndSelectsItsOverview()
    {
        using var directory = new TestProjectDirectory();
        var destination = Path.Combine(directory.Root, "created_story_project");
        var resourceDialogs = new FakeResourceWorkspaceDialogs
        {
            CreateResult = new ResourceIdentityRequest("opening", "开场剧情"),
        };
        var shell = new ShellViewModel(
            new ProjectService(),
            new FixedProjectFolderPicker(directory.Root),
            projectWorkspaceDialogs: new FakeProjectWorkspaceDialogs(
                new ProjectCreationRequest(destination, "story_project", "Story Project")),
            resourceWorkspaceDialogs: resourceDialogs);

        shell.NewProjectCommand.Execute(null);
        shell.CreateStoryCommand.Execute(null);

        Assert.IsTrue(File.Exists(Path.Combine(destination, "stories", "opening.json")));
        Assert.AreEqual("opening", shell.ProjectHome.SelectedStory?.Id);
        Assert.AreEqual("开场剧情", shell.ProjectHome.SelectedStory?.DisplayName);
        Assert.AreEqual(1, shell.ProjectHome.SelectedStory?.Overview.FlowNodeCount);
        Assert.IsTrue(shell.StatusMessage.Contains("opening", StringComparison.Ordinal));
    }

    [TestMethod]
    public void RecentProjectsTrackExistingOpenedProjectsAndSwitchWithoutDiskScan()
    {
        using var first = new TestProjectDirectory();
        using var second = new TestProjectDirectory();
        var missing = Path.Combine(AppContext.BaseDirectory, ".test-data", Guid.NewGuid().ToString("N"));
        var shell = CreateShell(first.Root);

        shell.SetRecentProjects([missing, second.Root, first.Root, second.Root]);

        CollectionAssert.AreEqual(
            new[] { Path.GetFullPath(second.Root), Path.GetFullPath(first.Root) },
            shell.RecentProjectDirectories.ToArray());
        Assert.IsTrue(shell.OpenRecentProject(first.Root));
        Assert.AreEqual(first.Root, shell.ProjectDirectory);
        Assert.AreEqual(Path.GetFullPath(first.Root), shell.RecentProjectDirectories[0]);
        Assert.IsFalse(shell.OpenRecentProject(missing));
        Assert.IsFalse(shell.RecentProjectDirectories.Any(path =>
            string.Equals(path, missing, StringComparison.OrdinalIgnoreCase)));
    }

    [TestMethod]
    public void RestoreLastProjectOpensRealProjectWithoutFolderPicker()
    {
        using var directory = new TestProjectDirectory();
        var shell = CreateShell(Path.Combine(directory.Root, "picker-must-not-be-used"));

        var restored = shell.RestoreLastProject(directory.Root);

        Assert.IsTrue(restored);
        Assert.IsTrue(shell.HasProject);
        Assert.AreEqual(directory.Root, shell.ProjectDirectory);
        Assert.IsTrue(shell.Output.Entries.Last().Message.StartsWith("已恢复上次项目", StringComparison.Ordinal));
    }

    [TestMethod]
    public void PrepareRuntimeReloadSavesValidatesAndShowsMinecraftInstructions()
    {
        using var directory = new TestProjectDirectory();
        var repository = new ActorRepository(directory.Root);
        repository.SaveActor(repository.CreateActor("teacher", "Teacher"));
        var shell = CreateShell(directory.Root);
        shell.OpenProjectCommand.Execute(null);
        shell.SelectedActor = shell.Actors.Single();
        shell.CurrentActor!.Notes = "saved before runtime reload";

        shell.PrepareRuntimeReloadCommand.Execute(null);

        Assert.AreEqual("saved before runtime reload", repository.LoadActor("teacher").Notes);
        Assert.AreEqual("Minecraft", shell.BottomPanel.SelectedTab.Page);
        Assert.IsTrue(shell.Output.Entries.Last().Message.Contains("/dgrpg reload", StringComparison.Ordinal));
    }

    private static ShellViewModel CreateShell(string projectDirectory) =>
        new(new ProjectService(), new FixedProjectFolderPicker(projectDirectory));

    private static ShellViewModel CreateShell(string projectDirectory, IActorWorkspaceDialogs dialogs) =>
        new(new ProjectService(), new FixedProjectFolderPicker(projectDirectory), dialogs);

    private static void OpenStoryActors(ShellViewModel shell, string storyId)
    {
        shell.OpenStory(shell.ProjectHome.Stories.Single(story => story.Id == storyId));
        shell.StoryWorkspace.SelectRoute(StoryWorkspaceRoutes.Actors);
    }

    private sealed class FixedProjectFolderPicker(string projectDirectory) : IProjectFolderPicker
    {
        public string? PickProjectFolder() => projectDirectory;
    }

    private sealed class FakeActorWorkspaceDialogs : IActorWorkspaceDialogs
    {
        public ActorCreationMode? CreationMode { get; init; }

        public ActorIdentityRequest? CreateResult { get; init; }

        public ActorIdentityRequest? ImportResult { get; init; }

        public ActorResourceInfo? PickResult { get; init; }

        public string? RenameResult { get; init; }

        public bool DeleteConfirmed { get; init; }

        public bool SaveBeforeSwitch { get; init; }

        public IReadOnlyList<ResourceDescriptor> LastShownReferences { get; private set; } = [];

        public UnsavedChangesChoice CloseChoice { get; init; } = UnsavedChangesChoice.Cancel;

        public ActorCreationMode? RequestCreationMode(string storyDisplayName) => CreationMode;

        public ActorIdentityRequest? RequestCreate(string suggestedId) => CreateResult;

        public ActorIdentityRequest? RequestImportIdentity(ActorResourceInfo source, string suggestedId) => ImportResult;

        public ActorResourceInfo? PickActor(
            IReadOnlyList<ActorResourceInfo> candidates,
            ActorPickerMode mode,
            string storyDisplayName) => PickResult;

        public string? RequestRename(ActorResourceInfo actor, string suggestedId) => RenameResult;

        public bool ConfirmDelete(ActorResourceInfo actor) => DeleteConfirmed;

        public bool ConfirmRemoveReference(ActorResourceInfo actor, string storyDisplayName) => DeleteConfirmed;

        public void ShowReferences(ActorResourceInfo actor, IReadOnlyList<ResourceDescriptor> references) =>
            LastShownReferences = references;

        public bool ConfirmSaveBeforeSwitch(ActorResourceInfo actor) => SaveBeforeSwitch;

        public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges(ActorResourceInfo actor) => CloseChoice;
    }

    private sealed class FakeResourceWorkspaceDialogs : IResourceWorkspaceDialogs
    {
        public ResourceIdentityRequest? CreateResult { get; init; }
        public ResourceCreationMode? RequestCreationMode(ProjectResourceType type, string storyDisplayName) => ResourceCreationMode.Blank;
        public ResourceIdentityRequest? RequestCreate(ProjectResourceType type, string suggestedId) => CreateResult;
        public ResourceIdentityRequest? RequestImportIdentity(ProjectResourceType type, ResourceDescriptor source, string suggestedId) => null;
        public ResourceDescriptor? PickResource(ProjectResourceType type, IReadOnlyList<ResourceDescriptor> candidates, ResourcePickerMode mode, string storyDisplayName) => null;
        public bool ConfirmDelete(ResourceDescriptor resource) => false;
        public bool ConfirmDiscardDraft(ResourceDescriptor resource) => false;
        public bool ConfirmRemoveReference(ResourceDescriptor resource, string storyDisplayName) => false;
        public void ShowReferences(ResourceDescriptor resource, IReadOnlyList<ResourceDescriptor> references) { }
        public bool ConfirmSaveBeforeSwitch(ResourceDescriptor resource) => false;
        public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges(ResourceDescriptor resource) => UnsavedChangesChoice.Cancel;
    }

    private sealed class FakeProjectWorkspaceDialogs(ProjectCreationRequest? result) : IProjectWorkspaceDialogs
    {
        public bool DeleteStoryConfirmed { get; set; }
        public int DeleteStoryConfirmationCount { get; private set; }
        public string? LastDeleteStoryId { get; private set; }
        public IReadOnlyList<string> LastDeleteStoryResources { get; private set; } = [];
        public ProjectCreationRequest? RequestCreate(string? initialParentDirectory = null) => result;
        public bool ConfirmDeleteStory(string storyId, string displayName, IReadOnlyList<string> resourcesToDelete)
        {
            DeleteStoryConfirmationCount++;
            LastDeleteStoryId = storyId;
            LastDeleteStoryResources = resourcesToDelete;
            return DeleteStoryConfirmed;
        }
    }

    private sealed class TestProjectDirectory : IDisposable
    {
        public TestProjectDirectory()
        {
            Root = Path.Combine(AppContext.BaseDirectory, ".test-data", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(Path.Combine(Root, "actors"));
            File.WriteAllText(
                Path.Combine(Root, "project.json"),
                """
                {
                  "schema_version": 1,
                  "id": "test_project",
                  "display_name": "Test Project"
                }
                """);
        }

        public string Root { get; }

        public void Dispose()
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
    }
}
