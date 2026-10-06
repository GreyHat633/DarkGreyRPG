using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.Core.Validation;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class ShellViewModelTests
{
    private const string Owner = "ST-2345-6789-ABCD-EFGH";
    private const string Opening = "ST-JKLM-NPQR-STUV-WXYZ";
    private static string ActorId(string local) => Owner + "~actor~" + local;

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
        shell.OpenStory(shell.ProjectHome.Stories.Single());
        var canonicalWorkspace = shell.CanonicalStoryWorkspace!;
        var canonicalFolders = canonicalWorkspace.Folders.ToArray();
        Assert.IsTrue(canonicalWorkspace.RequestCreate(CanonicalStoryFolderKind.Actors));
        Assert.AreSame(canonicalWorkspace, shell.CanonicalStoryWorkspace);
        CollectionAssert.AreEqual(canonicalFolders, shell.CanonicalStoryWorkspace!.Folders.ToArray());
        var createdActorId = shell.Actors.Single().Id;
        shell.SelectedActor = shell.Actors.Single();
        shell.CurrentActor!.DisplayName = "学校中的任务 NPC";
        shell.CurrentActor.TagsText = "school, quest";
        shell.SaveActorCommand.Execute(null);

        var restartedShell = new ShellViewModel(
            new ProjectService(),
            new FixedProjectFolderPicker("unused"));
        Assert.IsTrue(restartedShell.RestoreLastProject(destination));
        restartedShell.SelectedActor = restartedShell.Actors.Single(actor => actor.Id == createdActorId);

                Assert.AreEqual("学校中的任务 NPC", restartedShell.CurrentActor?.DisplayName);
        Assert.AreEqual("school, quest", restartedShell.CurrentActor?.TagsText);
    }

    [TestMethod]
    public void OpenSelectEditSaveAndRestartReloadUsesRealProjectData()
    {
        using var directory = new TestProjectDirectory();
        var repository = new ActorRepository(directory.Root);
        CreateFixtureActor(directory.Root, "teacher", "Teacher");

        var shell = CreateShell(directory.Root);
        shell.OpenProjectCommand.Execute(null);

        Assert.IsTrue(shell.HasProject);
        Assert.AreEqual(directory.Root, shell.ProjectDirectory);
        Assert.HasCount(1, shell.Actors);

        shell.SelectedActor = shell.Actors.Single();
        Assert.IsNotNull(shell.CurrentActor);

        shell.CurrentActor.DisplayName = "老师";
        shell.CurrentActor.DisplayName = "学校中的任务 NPC";
        shell.CurrentActor.TagsText = "school, quest";

        Assert.IsTrue(shell.SaveActorCommand.CanExecute(null));
        shell.SaveActorCommand.Execute(null);
        Assert.IsFalse(shell.SaveActorCommand.CanExecute(null));

        var restartedShell = CreateShell(directory.Root);
        restartedShell.OpenProjectCommand.Execute(null);
        restartedShell.SelectedActor = restartedShell.Actors.Single();

        Assert.IsNotNull(restartedShell.CurrentActor);
                Assert.AreEqual("学校中的任务 NPC", restartedShell.CurrentActor.DisplayName);
        Assert.AreEqual("school, quest", restartedShell.CurrentActor.TagsText);
    }

    [TestMethod]
    public void SearchFiltersActorsByIdDisplayNameAndTag()
    {
        using var directory = new TestProjectDirectory();
        var repository = new ActorRepository(directory.Root);
        var teacher = CreateFixtureActor(directory.Root, "teacher", "老师");
        teacher.SetTags(["school"]);
        repository.SaveActor(teacher);
        CreateFixtureActor(directory.Root, "merchant", "商人");
        var shell = CreateShell(directory.Root);
        shell.OpenProjectCommand.Execute(null);

        shell.SearchText = "school";
        Assert.AreEqual(ActorId("teacher"), shell.FilteredActors.Single().Id);
        shell.SearchText = "商人";
        Assert.AreEqual(ActorId("merchant"), shell.FilteredActors.Single().Id);
        shell.SearchText = "tea";
        Assert.AreEqual(ActorId("teacher"), shell.FilteredActors.Single().Id);
        shell.SearchText = string.Empty;
        Assert.HasCount(2, shell.FilteredActors);
    }

    [TestMethod]
    public void CreateDuplicateRenameAndDeleteOperateOnRealFiles()
    {
        using var directory = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(directory.Root);
        new CanonicalStoryLifecycleService(store).Create(Owner, "Owner");
        var shell = CreateShell(directory.Root, new FakeActorWorkspaceDialogs
        {
            CreateResult = new ActorIdentityRequest("ignored_user_id", "学生"),
            RenameResult = "改名后的学生",
            DeleteConfirmed = true,
        });
        shell.OpenProjectCommand.Execute(null);
        OpenStoryActors(shell, Owner);
        Assert.IsTrue(shell.CanonicalStoryWorkspace!.RequestCreate(CanonicalStoryFolderKind.Actors));
        var original = shell.CanonicalStoryWorkspace.ActorItems.Single();
        var repository = new ActorRepository(directory.Root);
        var originalBytes = File.ReadAllBytes(repository.GetActorPath(original.Id));
        shell.SelectedActor = shell.Actors.Single();
        Assert.IsTrue(shell.DuplicateActorCommand.CanExecute(null));
        shell.DuplicateActorCommand.Execute(null);
        var copy = repository.ListActors().Single(actor => actor.Id != original.Id);
        Assert.AreEqual(Owner, repository.LoadActor(copy.Id).HomeStoryId);
        shell.ShowProjectHomeCommand.Execute(null);
        OpenStoryActors(shell, Owner);
        var copiedItem = shell.CanonicalStoryWorkspace!.ActorItems.Single(actor => actor.Id == copy.Id);
        Assert.IsTrue(shell.CanonicalStoryWorkspace.RequestRename(copiedItem));
        Assert.AreEqual("改名后的学生", repository.LoadActor(copy.Id).DisplayName);
        CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(repository.GetActorPath(original.Id)));
        Assert.IsTrue(shell.CanonicalStoryWorkspace.RequestDelete(shell.CanonicalStoryWorkspace.ActorItems.Single(actor => actor.Id == copy.Id)));
        Assert.IsFalse(File.Exists(repository.GetActorPath(copy.Id)));
        Assert.IsTrue(File.Exists(repository.GetActorPath(original.Id)));
    }

    [TestMethod]
    public void DeleteSelectedStoryRequiresConfirmationAndRefreshesProjectHome()
    {
        using var directory = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(directory.Root);
        new CanonicalStoryLifecycleService(store).Create(Owner, "Deletable Story");
        var actor = CreateFixtureActor(directory.Root, "cascade", "Cascade Actor");
        var resources = new CanonicalStoryResourceLifecycleService(store);
        var session = resources.CreateOwned(Owner, GraphResourceKind.Session, Owner + "~session~cascade", "Session");
        var task = resources.CreateOwned(Owner, GraphResourceKind.Task, Owner + "~task~cascade", "Task");
        var paths = new[] { store.Stories.GetPath(Owner), store.Memberships.GetPath(Owner),
            new ActorRepository(directory.Root).GetActorPath(actor.Id), store.Sessions.GetPath(session.Id), store.Tasks.GetPath(task.Id) };
        var dialogs = new FakeProjectWorkspaceDialogs(null);
        var shell = new ShellViewModel(new ProjectService(), new FixedProjectFolderPicker(directory.Root), projectWorkspaceDialogs: dialogs);
        shell.OpenProjectCommand.Execute(null);
        shell.ProjectHome.SelectedStory = shell.ProjectHome.Stories.Single();
        shell.DeleteSelectedStoryCommand.Execute(null);
        Assert.AreEqual(1, dialogs.DeleteStoryConfirmationCount);
        Assert.AreEqual(Owner, dialogs.LastDeleteStoryId);
        Assert.IsTrue(paths.All(File.Exists));
        Assert.HasCount(3, dialogs.LastDeleteStoryResources);
        dialogs.DeleteStoryConfirmed = true;
        shell.DeleteSelectedStoryCommand.Execute(null);
        Assert.AreEqual(2, dialogs.DeleteStoryConfirmationCount);
        Assert.IsTrue(paths.All(path => !File.Exists(path)));
        Assert.IsEmpty(shell.ProjectHome.Stories);
        Assert.IsNull(shell.ProjectHome.SelectedStory);
        var restarted = CreateShell(directory.Root);
        restarted.OpenProjectCommand.Execute(null);
        Assert.IsEmpty(restarted.ProjectHome.Stories);
    }

    [TestMethod]
    public void M4BlankAndImportCreationPersistSelectedStoryAndIndependentData()
    {
        using var directory = new TestProjectDirectory();
        var source = CreateFixtureActor(directory.Root, "source", "Source");
        source.SetTags(["template"]);
        var repository = new ActorRepository(directory.Root);
        repository.SaveActor(source);
        var sourceBytes = File.ReadAllBytes(repository.GetActorPath(source.Id));
        var store = new CanonicalProjectGraphStore(directory.Root);
        new CanonicalStoryLifecycleService(store).Create(Opening, "Target");
        var shell = CreateShell(directory.Root, new FakeActorWorkspaceDialogs { CreateResult = new("ignored", "Blank") });
        shell.OpenProjectCommand.Execute(null);
        OpenStoryActors(shell, Opening);
        Assert.IsTrue(shell.CanonicalStoryWorkspace!.RequestCreate(CanonicalStoryFolderKind.Actors));
        var blank = shell.CanonicalStoryWorkspace.ActorItems.Single();
        Assert.AreEqual(Opening, repository.LoadActor(blank.Id).HomeStoryId);
        CollectionAssert.Contains(store.Memberships.Load(Opening).OwnedResources.Actors, blank.Id);
        // Current import-as-new service preserves ownership and is then edited through the live shell.
        var service = new ProjectService(); service.OpenProject(directory.Root);
        var imported = service.ImportActorAsNew(source.Id, Opening + "~actor~imported", Opening);
        var importedShell = CreateShell(directory.Root);
        importedShell.OpenProjectCommand.Execute(null);
        importedShell.SelectedActor = importedShell.Actors.Single(actor => actor.Id == imported.Id);
        importedShell.CurrentActor!.DisplayName = "Independent edit";
        importedShell.SaveActorCommand.Execute(null);
        Assert.AreEqual("Independent edit", repository.LoadActor(imported.Id).DisplayName);
        CollectionAssert.AreEqual(new[] { "template" }, repository.LoadActor(imported.Id).Tags.ToArray());
        CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(repository.GetActorPath(source.Id)));
    }

    [TestMethod]
    public void M4ReferenceSharesDataRemovePreservesFileAndDeleteIsProtected()
    {
        using var directory = new TestProjectDirectory();
        var source = CreateFixtureActor(directory.Root, "shared", "Shared Actor");
        var store = new CanonicalProjectGraphStore(directory.Root);
        new CanonicalStoryLifecycleService(store).Create(Opening, "Consumer");
        var repository = new ActorRepository(directory.Root);
        var dialogs = new FakeActorWorkspaceDialogs { PickResult = repository.ListActors().Single(), DeleteConfirmed = true };
        var shell = CreateShell(directory.Root, dialogs);
        shell.OpenProjectCommand.Execute(null);
        OpenStoryActors(shell, Opening);
        Assert.IsTrue(shell.CanonicalStoryWorkspace!.RequestReference(CanonicalStoryFolderKind.Actors));
        CollectionAssert.Contains(store.Memberships.Load(Opening).ReferencedResources.Actors, source.Id);
        shell.SelectedActor = shell.Actors.Single();
        shell.CurrentActor!.DisplayName = "Edited through reference";
        shell.SaveActorCommand.Execute(null);
        Assert.AreEqual("Edited through reference", repository.LoadActor(source.Id).DisplayName);
        OpenStoryActors(shell, Owner);
        shell.CanonicalStoryWorkspace!.RequestDelete(shell.CanonicalStoryWorkspace.ActorItems.Single());
        Assert.IsTrue(File.Exists(repository.GetActorPath(source.Id)));
        Assert.HasCount(1, dialogs.LastShownReferences);
        Assert.AreEqual(Opening, dialogs.LastShownReferences.Single().Id);
        OpenStoryActors(shell, Opening);
        shell.CanonicalStoryWorkspace!.RequestDelete(shell.CanonicalStoryWorkspace.ActorItems.Single());
        Assert.IsTrue(File.Exists(repository.GetActorPath(source.Id)));
        CollectionAssert.DoesNotContain(store.Memberships.Load(Opening).ReferencedResources.Actors, source.Id);
        OpenStoryActors(shell, Owner);
        shell.CanonicalStoryWorkspace!.RequestDelete(shell.CanonicalStoryWorkspace.ActorItems.Single());
        Assert.IsFalse(File.Exists(repository.GetActorPath(source.Id)));
    }

    [TestMethod]
    public void DirtyActorDisablesDestructiveWorkspaceCommands()
    {
        using var directory = new TestProjectDirectory();
        var repository = new ActorRepository(directory.Root);
        CreateFixtureActor(directory.Root, "teacher", "Teacher");
        var shell = CreateShell(directory.Root, new FakeActorWorkspaceDialogs());
        shell.OpenProjectCommand.Execute(null);
        shell.SelectedActor = shell.Actors.Single();

        shell.CurrentActor!.DisplayName = "Changed";

        Assert.IsFalse(shell.DuplicateActorCommand.CanExecute(null));
        Assert.IsFalse(shell.RenameActorCommand.CanExecute(null));
        Assert.IsFalse(shell.DeleteActorCommand.CanExecute(null));
    }

    [TestMethod]
    public void DirtyActorUsesResourcePromptForSwitchAndSingleWorkspacePromptForClose()
    {
        using var directory = new TestProjectDirectory();
        var repository = new ActorRepository(directory.Root);
        CreateFixtureActor(directory.Root, "a", "A");
        CreateFixtureActor(directory.Root, "b", "B");
        var dialogs = new FakeActorWorkspaceDialogs
        {
            SaveBeforeSwitch = false,
            CloseChoice = UnsavedChangesChoice.Cancel,
        };
        var projectDialogs = new FakeProjectWorkspaceDialogs(null)
        {
            CloseChoice = UnsavedChangesChoice.Cancel,
        };
        var shell = CreateShell(directory.Root, dialogs, projectDialogs);
        shell.OpenProjectCommand.Execute(null);
        shell.SelectedActor = shell.Actors.Single(actor => actor.Id == ActorId("a"));
        shell.CurrentActor!.DisplayName = "dirty";

        shell.SelectedActor = shell.Actors.Single(actor => actor.Id == ActorId("b"));

        Assert.AreEqual(ActorId("a"), shell.SelectedActor?.Id);
        Assert.IsFalse(shell.TryClose());
        Assert.AreEqual(1, projectDialogs.CloseConfirmationCount);
    }

    [TestMethod]
    public void WorkspaceCloseSavePersistsAllDirtyDocumentsThroughOnePrompt()
    {
        using var directory = new TestProjectDirectory();
        var repository = new ActorRepository(directory.Root);
        CreateFixtureActor(directory.Root, "teacher", "Teacher");
        var projectDialogs = new FakeProjectWorkspaceDialogs(null)
        {
            CloseChoice = UnsavedChangesChoice.Save,
        };
        var shell = CreateShell(directory.Root, new FakeActorWorkspaceDialogs(), projectDialogs);
        shell.OpenProjectCommand.Execute(null);
        shell.SelectedActor = shell.Actors.Single();
        shell.CurrentActor!.DisplayName = "saved while closing";

        Assert.IsTrue(shell.TryClose());
        Assert.AreEqual(1, projectDialogs.CloseConfirmationCount);
        Assert.AreEqual("saved while closing", repository.LoadActor(ActorId("teacher")).DisplayName);
        Assert.IsFalse(shell.CurrentActor.Document.IsDirty);
    }

    [TestMethod]
    public void WorkspaceCloseSavePersistsTwoOpenDirtyDocumentsThroughOnePrompt()
    {
        using var directory = new TestProjectDirectory();
        var repository = new ActorRepository(directory.Root);
        CreateFixtureActor(directory.Root, "first", "First");
        CreateFixtureActor(directory.Root, "second", "Second");
        var projectService = new ProjectService();
        var projectDialogs = new FakeProjectWorkspaceDialogs(null)
        {
            CloseChoice = UnsavedChangesChoice.Save,
        };
        var shell = new ShellViewModel(
            projectService,
            new FixedProjectFolderPicker(directory.Root),
            new FakeActorWorkspaceDialogs(),
            projectDialogs);
        shell.OpenProjectCommand.Execute(null);
        projectService.OpenActor(ActorId("first")).DisplayName = "first saved while closing";
        projectService.OpenActor(ActorId("second")).DisplayName = "second saved while closing";

        Assert.IsTrue(shell.TryClose());
        Assert.AreEqual(1, projectDialogs.CloseConfirmationCount);
        Assert.AreEqual("first saved while closing", repository.LoadActor(ActorId("first")).DisplayName);
        Assert.AreEqual("second saved while closing", repository.LoadActor(ActorId("second")).DisplayName);
        Assert.IsFalse(projectService.OpenActorDocuments.Any(document => document.IsDirty));
    }

    [TestMethod]
    public void WorkspaceCloseDiscardClosesWithoutSavingAndPromptsOnlyOnce()
    {
        using var directory = new TestProjectDirectory();
        var repository = new ActorRepository(directory.Root);
        CreateFixtureActor(directory.Root, "teacher", "Teacher");
        var projectDialogs = new FakeProjectWorkspaceDialogs(null)
        {
            CloseChoice = UnsavedChangesChoice.Discard,
        };
        var shell = CreateShell(directory.Root, new FakeActorWorkspaceDialogs(), projectDialogs);
        shell.OpenProjectCommand.Execute(null);
        shell.SelectedActor = shell.Actors.Single();
        shell.CurrentActor!.DisplayName = "discarded change";

        Assert.IsTrue(shell.TryClose());
        Assert.AreEqual(1, projectDialogs.CloseConfirmationCount);
        Assert.AreNotEqual("discarded change", repository.LoadActor(ActorId("teacher")).DisplayName);
        Assert.IsTrue(shell.CurrentActor.Document.IsDirty);
    }

    [TestMethod]
    public void FeedbackSurfacesTrackOperationsAndValidationProblems()
    {
        using var directory = new TestProjectDirectory();
        var repository = new ActorRepository(directory.Root);
        CreateFixtureActor(directory.Root, "teacher", "Teacher");
        var shell = CreateShell(directory.Root, new FakeActorWorkspaceDialogs());

        shell.OpenProjectCommand.Execute(null);

        Assert.IsTrue(shell.Toast.IsVisible);
        Assert.IsTrue(shell.Output.Entries.Any(entry => entry.Kind == OutputKind.Success));
        shell.SelectedActor = shell.Actors.Single();
        shell.CurrentActor!.DisplayName = string.Empty;

        Assert.IsTrue(shell.Problems.HasErrors);
        Assert.AreEqual("actor/" + ActorId("teacher"), shell.Problems.Problems[0].Source);

        shell.OpenProblem(shell.Problems.Problems[0]);
        Assert.AreEqual("Story", shell.Navigation.SelectedItem.Page);
        Assert.AreEqual(ActorId("teacher"), shell.SelectedActor?.Id);
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
        CreateFixtureActor(directory.Root, "teacher", "Teacher");
        var shell = CreateShell(directory.Root, new FakeActorWorkspaceDialogs());
        shell.OpenProjectCommand.Execute(null);
        shell.SelectedActor = shell.Actors.Single();
        shell.CurrentActor!.DisplayName = "saved by Save All";

        Assert.IsTrue(shell.SaveAllCommand.CanExecute(null));
        shell.SaveAllCommand.Execute(null);
        Assert.IsFalse(shell.CurrentActor.Document.IsDirty);
        Assert.AreEqual("saved by Save All", repository.LoadActor(ActorId("teacher")).DisplayName);

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
        Assert.IsTrue(shell.StatusMessage.Contains("新建故事", StringComparison.Ordinal));
    }

    [TestMethod]
    public void CreateStoryCommandPersistsStoryAndSelectsItsOverview()
    {
        using var directory = new TestProjectDirectory();
        var destination = Path.Combine(directory.Root, "created_story_project");
        var resourceDialogs = new FakeResourceWorkspaceDialogs
        {
            CreateResult = new ResourceIdentityRequest(Opening, "开场剧情"),
        };
        var shell = new ShellViewModel(
            new ProjectService(),
            new FixedProjectFolderPicker(directory.Root),
            projectWorkspaceDialogs: new FakeProjectWorkspaceDialogs(
                new ProjectCreationRequest(destination, "story_project", "Story Project")),
            resourceWorkspaceDialogs: resourceDialogs);

        shell.NewProjectCommand.Execute(null);
        shell.CreateStoryCommand.Execute(null);

        var createdUid = shell.ProjectHome.SelectedStory!.Id;
        Assert.IsTrue(File.Exists(Path.Combine(destination, "resources", "canonical", "stories", createdUid + ".json")));
        Assert.IsTrue(File.Exists(Path.Combine(destination, "resources", "canonical", "memberships", createdUid + ".json")));
        Assert.IsFalse(File.Exists(Path.Combine(destination, "stories", createdUid + ".json")));
        Assert.AreEqual(createdUid, DarkGreyRPG.Studio.Core.Identity.StoryUid.Parse(createdUid).Value);
        Assert.AreEqual("开场剧情", shell.ProjectHome.SelectedStory?.DisplayName);
        Assert.IsTrue(shell.ProjectHome.SelectedStory?.IsCanonicalOnly);
        Assert.AreEqual(1, shell.ProjectHome.SelectedStory?.Overview.FlowNodeCount);
        Assert.IsTrue(shell.StatusMessage.Contains(createdUid, StringComparison.Ordinal));
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
    [DataRow(GraphResourceKind.Story)]
    [DataRow(GraphResourceKind.Session)]
    [DataRow(GraphResourceKind.Task)]
    public void RestoreRejectsUnsupportedPublicOutputsBeforeOpeningOrWriting(GraphResourceKind kind)
    {
        using var directory = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(directory.Root);
        var story = new CanonicalStoryLifecycleService(store).Create(Owner, "Old contract");
        var resource = kind == GraphResourceKind.Story ? story
            : new CanonicalStoryResourceLifecycleService(store).CreateOwned(Owner, kind,
                Owner + (kind == GraphResourceKind.Session ? "~session~old" : "~task~old"), "Old contract");
        var scope = kind switch { GraphResourceKind.Story => GraphScope.StoryFlow, GraphResourceKind.Session => GraphScope.Session, _ => GraphScope.Task };
        var type = kind switch { GraphResourceKind.Story => "terminate", GraphResourceKind.Session => "end", _ => "settle" };
        var graph = resource.Graph!;
        graph.Nodes.Add(new GraphNodeAuthoringService().Create(graph, scope, type, "old_boundary").Candidate!);
        resource.Graph = graph;
        var repository = kind switch { GraphResourceKind.Story => store.Stories, GraphResourceKind.Session => store.Sessions, _ => store.Tasks };
        repository.Replace(resource);
        var path = repository.GetPath(resource.Id);
        var invalid = System.Text.RegularExpressions.Regex.Replace(File.ReadAllText(path), "\\\"display_order\\\"\\s*:\\s*\\d+\\s*,", "");
        File.WriteAllText(path, invalid);
        var files = Directory.GetFiles(directory.Root, "*", SearchOption.AllDirectories).ToDictionary(file => file, File.ReadAllBytes);
        var shell = CreateShell(directory.Root);
        Assert.IsFalse(shell.RestoreLastProject(directory.Root));
        Assert.IsFalse(shell.HasProject);
        StringAssert.Contains(shell.StatusMessage, "graph.output.order.required");
        foreach (var file in files) CollectionAssert.AreEqual(file.Value, File.ReadAllBytes(file.Key));
        using var valid = new TestProjectDirectory();
        Assert.IsTrue(shell.RestoreLastProject(valid.Root));
        Assert.IsFalse(shell.RestoreLastProject(directory.Root));
        Assert.AreEqual(valid.Root, shell.ProjectDirectory);
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
        CreateFixtureActor(directory.Root, "teacher", "Teacher");
        var shell = CreateShell(directory.Root);
        shell.OpenProjectCommand.Execute(null);
        shell.SelectedActor = shell.Actors.Single();
        shell.CurrentActor!.DisplayName = "saved before runtime reload";

        shell.PrepareRuntimeReloadCommand.Execute(null);

        Assert.AreEqual("saved before runtime reload", repository.LoadActor(ActorId("teacher")).DisplayName);
        Assert.AreEqual("Output", shell.BottomPanel.SelectedTab.Page);
        Assert.IsTrue(shell.Output.Entries.Last().Message.Contains("/dgrpg reload", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ExistingCanonicalRootsMountLiveWorkspaceAndRetainDirtyGraphAcrossProjectHome()
    {
        using var directory = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(directory.Root);
        store.Stories.Create(Envelope(
            GraphResourceKind.Story,
            Opening,
            "Canonical Opening",
            GraphNodeFactory.Create(GraphScope.StoryFlow, "start", "start")));
        store.Sessions.Create(Envelope(GraphResourceKind.Session, Opening + "~session~session", "Session"));
        store.Tasks.Create(Envelope(GraphResourceKind.Task, Opening + "~task~task", "Task"));
        store.Memberships.Create(new CanonicalStoryMembershipManifest(
            Opening,
            new CanonicalStoryMembershipSet
            {
                Actors = [Opening + "~actor~missing_actor"],
                Sessions = [Opening + "~session~session"],
                Tasks = [Opening + "~task~task"],
            }));
        var shell = CreateShell(directory.Root);
        shell.OpenProjectCommand.Execute(null);

        shell.OpenStory(shell.ProjectHome.Stories.Single(story => story.Id == Opening));

        Assert.IsTrue(shell.HasCanonicalStoryWorkspace);
        Assert.IsFalse(shell.StoryWorkspace.HasStory);
        Assert.IsFalse(shell.EffectiveResourceBrowserVisible);
        Assert.AreEqual("Canonical Opening", shell.CanonicalStoryWorkspace!.StoryEditor.DisplayName);
        Assert.HasCount(1, shell.CanonicalStoryWorkspace.MissingItems);
        Assert.IsTrue(shell.Problems.Problems.Any(problem =>
            problem.Code == "story.workspace.member.missing"
            && problem.Source == "canonical/story/" + Opening));
        Assert.IsTrue(shell.CanonicalStoryWorkspace.StoryEditor.Host.AddNode(
            GraphNodeFactory.Create(GraphScope.StoryFlow, "action", "action")));
        Assert.IsTrue(shell.CanonicalStoryWorkspace.HasDirtyEditors);
        var canonical = shell.CanonicalStoryWorkspace;
        Assert.IsTrue(canonical.OpenGraphResource(canonical.SessionItems.Single()));
        Assert.IsTrue(canonical.OpenGraphResource(canonical.TaskItems.Single()));
        Assert.IsTrue(canonical.ReturnToStory());
        Assert.IsTrue(canonical.StoryEditor.IsDirty, "Graph navigation must retain the unsaved in-memory editor.");
        Assert.IsTrue(shell.SaveCurrentResourceCommand.CanExecute(null));

        shell.ShowProjectHomeCommand.Execute(null);

        Assert.IsTrue(shell.HasCanonicalStoryWorkspace);
        Assert.IsFalse(shell.IsCanonicalStoryWorkspaceVisible);
        Assert.IsTrue(canonical.StoryEditor.IsDirty);
        Assert.IsTrue(shell.EffectiveResourceBrowserVisible);
        shell.OpenStory(shell.ProjectHome.Stories.Single(story => story.Id == Opening));
        Assert.IsTrue(shell.IsCanonicalStoryWorkspaceVisible);
        Assert.AreSame(canonical, shell.CanonicalStoryWorkspace);
        shell.SaveCurrentResourceCommand.Execute(null);
        Assert.IsFalse(shell.CanonicalStoryWorkspace.HasDirtyEditors);
        Assert.HasCount(2, store.Stories.Load(Opening).Graph!.Nodes);

        shell.ShowProjectHomeCommand.Execute(null);

        Assert.IsTrue(shell.HasCanonicalStoryWorkspace);
        Assert.IsFalse(shell.IsCanonicalStoryWorkspaceVisible);
        Assert.IsTrue(shell.EffectiveResourceBrowserVisible);
        Assert.AreEqual(Opening, shell.ProjectHome.SelectedStory?.Id);
    }

    [TestMethod]
    public void PartialCanonicalRootsFailClosedWithoutOpeningLegacyStory()
    {
        using var directory = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(directory.Root);
        store.Stories.Create(Envelope(
            GraphResourceKind.Story,
            Opening,
            "Canonical Opening",
            GraphNodeFactory.Create(GraphScope.StoryFlow, "start", "start")));
        var shell = CreateShell(directory.Root);
        shell.OpenProjectCommand.Execute(null);

        shell.OpenStory(shell.ProjectHome.Stories.Single(story => story.Id == Opening));

        Assert.IsFalse(shell.HasCanonicalStoryWorkspace);
        Assert.IsFalse(shell.StoryWorkspace.HasStory);
        Assert.AreEqual(OutputKind.Error, shell.Output.Entries.Last().Kind);
        Assert.IsTrue(shell.Output.Entries.Last().Message.Contains("membership", StringComparison.OrdinalIgnoreCase));
    }

    [TestMethod]
    public void CanonicalNodeAuthoringIssueAppearsAndClearsInProblemsImmediately()
    {
        using var directory = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(directory.Root);
        store.Stories.Create(new GraphResourceEnvelope(
            GraphResourceKind.Story,
            Opening,
            "Canonical Opening",
            new GraphDocument([GraphNodeFactory.CreateStoryStart("start", triggerPortId: "region")])));
        store.Memberships.Create(new CanonicalStoryMembershipManifest(Opening));
        var shell = CreateShell(directory.Root);
        shell.OpenProjectCommand.Execute(null);
        shell.OpenStory(shell.ProjectHome.Stories.Single(story => story.Id == Opening));
        var workspace = shell.CanonicalStoryWorkspace!;
        Assert.IsTrue(workspace.SelectGraphNode(workspace.ActiveGraphHost.Nodes.Single()));
        var trigger = workspace.NodeInspector!.StoryStartTriggers.Single();

        trigger.RadiusText = "bad";

        Assert.IsTrue(shell.Problems.Problems.Any(problem =>
            problem.Code == "graph.story.start.trigger.authoring_invalid"
            && problem.Field == StoryStartSchema.RadiusProperty
            && problem.Source == "canonical/story/" + Opening));

        trigger.RadiusText = "6";

        Assert.IsFalse(shell.Problems.Problems.Any(problem =>
            problem.Code == "graph.story.start.trigger.authoring_invalid"));
    }

    [TestMethod]
    public void ExportSelectedStoryPackageCommandBuildsServerReadyPackage()
    {
        using var directory = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(directory.Root);
        store.Stories.Create(new GraphResourceEnvelope(
            GraphResourceKind.Story,
            Opening,
            "Opening",
            new GraphDocument([GraphNodeFactory.CreateStoryStart("start", triggerPortId: "entry")])));
        store.Memberships.Create(new CanonicalStoryMembershipManifest(Opening));
        var package = Path.Combine(directory.Root, "chosen", "opening.dgrs");
        var picker = new FixedDgrsExportPathPicker(package);
        var shell = CreateShell(directory.Root, picker);
        shell.OpenProjectCommand.Execute(null);
        shell.ProjectHome.SelectedStory = shell.ProjectHome.Stories.Single(story => story.Id == Opening);

        Assert.IsTrue(shell.ExportSelectedStoryPackageCommand.CanExecute(null));
        shell.ExportSelectedStoryPackageCommand.Execute(null);

        Assert.IsTrue(File.Exists(package));
        Assert.AreEqual("Opening", picker.DisplayName);
        Assert.AreEqual(StudioBuildInfo.Version,
            DarkGreyRPG.Studio.Core.Packaging.OfflineDgrsPackageReader.ReadContainer(package).Single().Manifest.ProducerVersion);
        Assert.AreEqual(
            Path.Combine(directory.Root, "build", "story_packages"),
            picker.SuggestedDirectory);
        Assert.IsFalse(File.Exists(Path.Combine(
            directory.Root,
            "build",
            "story_packages",
            "opening.dgrs")));
        Assert.IsFalse(Directory.Exists(Path.Combine(directory.Root, "build", "story_packages", Opening)));
        Assert.AreEqual(OutputKind.Success, shell.Output.Entries.Last().Kind);
        StringAssert.Contains(shell.Output.Entries.Last().Message, "已导出并验证");
    }

    [TestMethod]
    public void ProjectHomeExportRemainsEnabledAndSavesRetainedCanonicalEdits()
    {
        using var directory = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(directory.Root);
        store.Stories.Create(new GraphResourceEnvelope(
            GraphResourceKind.Story,
            Opening,
            "Opening",
            new GraphDocument([GraphNodeFactory.CreateStoryStart("start", triggerPortId: "entry")])));
        store.Memberships.Create(new CanonicalStoryMembershipManifest(Opening));
        var package = Path.Combine(directory.Root, "exports", "opening.dgrs");
        var shell = CreateShell(directory.Root, new FixedDgrsExportPathPicker(package));
        shell.OpenProjectCommand.Execute(null);
        shell.OpenStory(shell.ProjectHome.Stories.Single(story => story.Id == Opening));
        shell.CanonicalStoryWorkspace!.StoryEditor.Host.SetNodePosition("start", 480, 270);
        Assert.IsTrue(shell.CanonicalStoryWorkspace.HasDirtyEditors);

        shell.ShowProjectHomeCommand.Execute(null);

        Assert.AreEqual(Opening, shell.ProjectHome.SelectedStory?.Id);
        Assert.IsTrue(shell.ExportSelectedStoryPackageCommand.CanExecute(null));
        shell.ExportSelectedStoryPackageCommand.Execute(null);

        Assert.IsFalse(shell.CanonicalStoryWorkspace.HasDirtyEditors);
        Assert.IsTrue(File.Exists(package));
        Assert.AreEqual(OutputKind.Success, shell.Output.Entries.Last().Kind);
        StringAssert.Contains(shell.Output.Entries.Last().Message, "已导出并验证");
    }

    [TestMethod]
    public void CancellingExportPathSelectionDoesNotSaveOrCreatePackage()
    {
        using var directory = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(directory.Root);
        store.Stories.Create(new GraphResourceEnvelope(
            GraphResourceKind.Story,
            Opening,
            "Opening",
            new GraphDocument([GraphNodeFactory.CreateStoryStart("start", triggerPortId: "entry")])));
        store.Memberships.Create(new CanonicalStoryMembershipManifest(Opening));
        var shell = CreateShell(directory.Root, new FixedDgrsExportPathPicker(null));
        shell.OpenProjectCommand.Execute(null);
        shell.OpenStory(shell.ProjectHome.Stories.Single(story => story.Id == Opening));
        shell.CanonicalStoryWorkspace!.StoryEditor.Host.SetNodePosition("start", 480, 270);
        Assert.IsTrue(shell.CanonicalStoryWorkspace.HasDirtyEditors);
        shell.ShowProjectHomeCommand.Execute(null);

        shell.ExportSelectedStoryPackageCommand.Execute(null);

        Assert.IsTrue(shell.CanonicalStoryWorkspace.HasDirtyEditors);
        Assert.IsFalse(Directory.Exists(Path.Combine(directory.Root, "build")));
        Assert.AreNotEqual("ExportSelectedStoryPackage", shell.LastUiCommand);
    }

    [TestMethod]
    public void GroupExportPassesGroupDisplayNameInsteadOfSelectedMemberIdentity()
    {
        using var directory = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(directory.Root);
        var lifecycle = new CanonicalStoryLifecycleService(store);
        var a = lifecycle.Create(Owner, "甲");
        var b = lifecycle.Create(Opening, "乙");
        var output = GraphNodeFactory.Create(GraphScope.StoryFlow, "logic_output", "out");
        output.Properties["port_id"] = System.Text.Json.JsonSerializer.SerializeToElement("group_out");
        output.Properties["display_name"] = System.Text.Json.JsonSerializer.SerializeToElement("出口");
        var input = GraphNodeFactory.Create(GraphScope.StoryFlow, "logic_input", "in");
        input.Properties["port_id"] = System.Text.Json.JsonSerializer.SerializeToElement("group_in");
        input.Properties["display_name"] = System.Text.Json.JsonSerializer.SerializeToElement("入口");
        var graphA = a.Graph!; graphA.Nodes.Add(output); a.Graph = graphA; store.Stories.Replace(a);
        var graphB = b.Graph!; graphB.Nodes.Add(input); b.Graph = graphB; store.Stories.Replace(b);
        store.StoryLogicGraph.Save([new(Owner, "group_out", Opening, "group_in")]);
        var catalog = StoryGroupCatalog.Derive([Owner, Opening], store.StoryLogicGraph.Load());
        new StoryGroupNameStore(directory.Root).Save(catalog.Rename(catalog.Groups.Single().Key, "旅人故事组"));
        var picker = new FixedDgrsExportPathPicker(null);
        var shell = CreateShell(directory.Root, picker);
        shell.OpenProjectCommand.Execute(null);
        shell.ProjectHome.SelectedStory = shell.ProjectHome.Stories.Single(story => story.Id == Opening);
        shell.ExportSelectedStoryPackageCommand.Execute(null);
        Assert.AreEqual("旅人故事组", picker.GroupDisplayName);
        Assert.IsNull(picker.DisplayName);
        Assert.IsFalse(Directory.Exists(Path.Combine(directory.Root, "build")));
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void InvalidExportListsEveryTaskWithFriendlyContextAndPreservesExistingArchive(bool groupExport)
    {
        using var directory = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(directory.Root);
        var lifecycle = new CanonicalStoryLifecycleService(store);
        var owner = lifecycle.Create(Owner, "来源故事");
        if (groupExport)
        {
            var opening = lifecycle.Create(Opening, "后续故事");
            var output = GraphNodeFactory.Create(GraphScope.StoryFlow, "logic_output", "out");
            var input = GraphNodeFactory.Create(GraphScope.StoryFlow, "logic_input", "in");
            output.Properties["port_id"] = System.Text.Json.JsonSerializer.SerializeToElement("group_out");
            input.Properties["port_id"] = System.Text.Json.JsonSerializer.SerializeToElement("group_in");
            output.Properties["display_name"] = System.Text.Json.JsonSerializer.SerializeToElement("出口");
            input.Properties["display_name"] = System.Text.Json.JsonSerializer.SerializeToElement("入口");
            var graphA = owner.Graph!; graphA.Nodes.Add(output); owner.Graph = graphA; store.Stories.Replace(owner);
            var graphB = opening.Graph!; graphB.Nodes.Add(input); opening.Graph = graphB; store.Stories.Replace(opening);
            store.StoryLogicGraph.Save([new(Owner, "group_out", Opening, "group_in")]);
        }
        var tasks = new[] { Owner + "~task~broken_one", Owner + "~task~broken_two" };
        foreach (var id in tasks)
        {
            var objective = GraphNodeFactory.Create(GraphScope.Task, "objective", "objective");
            var settle = new GraphNodeAuthoringService().Create(new GraphDocument(), GraphScope.Task, "settle", "settle").Candidate!;
            store.Tasks.Create(new GraphResourceEnvelope(GraphResourceKind.Task, id,
                id == tasks[0] ? "收集苹果" : "消灭史莱姆", new GraphDocument([objective, settle],
                    [new("objective", "logic_status", "settle", "logic_in", GraphInterfaceKind.Logic)])));
        }
        var membership = store.Memberships.Load(Owner);
        var owned = membership.OwnedResources; owned.Tasks = [.. tasks]; membership.OwnedResources = owned;
        store.Memberships.Replace(membership);
        var package = Path.Combine(directory.Root, groupExport ? "unchanged.dgrs.g" : "unchanged.dgrs");
        File.WriteAllText(package, "previous good package");
        var shell = CreateShell(directory.Root, new FixedDgrsExportPathPicker(package));
        shell.OpenProjectCommand.Execute(null);
        shell.ProjectHome.SelectedStory = shell.ProjectHome.Stories.Single(story => story.Id == (groupExport ? Opening : Owner));

        shell.ExportSelectedStoryPackageCommand.Execute(null);

        Assert.AreEqual("previous good package", File.ReadAllText(package));
        var problems = shell.Problems.Problems.Where(problem => problem.Source == "export/story/" + Owner).ToArray();
        Assert.HasCount(2, problems);
        CollectionAssert.AreEquivalent(tasks, problems.Select(problem => problem.GraphResourceId).ToArray());
        Assert.IsTrue(problems.All(problem => problem.NodeId == "objective" && problem.Field == "properties.entity"));
        Assert.IsTrue(problems.All(problem => problem.DisplayMessage.Contains("来源故事", StringComparison.Ordinal)));
        Assert.IsTrue(problems.All(problem => !problem.DisplayMessage.Contains(Owner, StringComparison.Ordinal)));
        Assert.AreEqual("Problems", shell.BottomPanel.SelectedTab.Page);
        Assert.AreEqual(OutputKind.Error, shell.Output.Entries.Last().Kind);
        shell.OpenProblem(problems[0]);
        Assert.AreEqual(Owner, shell.CanonicalStoryWorkspace!.StoryEditor.Id);
        Assert.AreEqual("objective", shell.CanonicalStoryWorkspace.StoryNodeFocusRequest!.NodeId);
        Assert.AreEqual(problems[0].GraphResourceId, shell.CanonicalStoryWorkspace.StoryNodeFocusRequest.ResourceId);
    }

    private static ActorDocument CreateFixtureActor(string root, string local, string name)
    {
        var store = new CanonicalProjectGraphStore(root);
        if (!File.Exists(store.Stories.GetPath(Owner)))
            new CanonicalStoryLifecycleService(store).Create(Owner, "Owner");
        var service = new ProjectService();
        service.OpenProject(root);
        return service.CreateActorInStory(Owner, ActorId(local), name);
    }

    private static GraphResourceEnvelope Envelope(
        GraphResourceKind kind,
        string id,
        string displayName,
        params GraphNode[] nodes)
        => new(kind, id, displayName, new GraphDocument(nodes));

    private static ShellViewModel CreateShell(string projectDirectory) =>
        new(new ProjectService(), new FixedProjectFolderPicker(projectDirectory));

    private static ShellViewModel CreateShell(
        string projectDirectory,
        IDgrsExportPathPicker exportPathPicker) =>
        new(
            new ProjectService(),
            new FixedProjectFolderPicker(projectDirectory),
            dgrsExportPathPicker: exportPathPicker);

    private static ShellViewModel CreateShell(string projectDirectory, IActorWorkspaceDialogs dialogs) =>
        new(new ProjectService(), new FixedProjectFolderPicker(projectDirectory), dialogs);

    private static ShellViewModel CreateShell(
        string projectDirectory,
        IActorWorkspaceDialogs actorDialogs,
        IProjectWorkspaceDialogs projectDialogs) =>
        new(new ProjectService(), new FixedProjectFolderPicker(projectDirectory), actorDialogs, projectDialogs);

    private static void OpenStoryActors(ShellViewModel shell, string storyId)
    {
        shell.OpenStory(shell.ProjectHome.Stories.Single(story => story.Id == storyId));
        Assert.IsTrue(shell.CanonicalStoryWorkspace!.SelectFolder(CanonicalStoryFolderKind.Actors));
    }

    private sealed class FixedProjectFolderPicker(string projectDirectory) : IProjectFolderPicker
    {
        public string? PickProjectFolder() => projectDirectory;
    }

    private sealed class FixedDgrsExportPathPicker(string? path) : IDgrsExportPathPicker
    {
        public string? DisplayName { get; private set; }
        public string? GroupDisplayName { get; private set; }

        public string? SuggestedDirectory { get; private set; }

        public string? PickExportPath(string displayName, string suggestedDirectory)
        {
            DisplayName = displayName;
            SuggestedDirectory = suggestedDirectory;
            return path;
        }
        public string? PickGroupExportPath(string displayName, string suggestedDirectory)
        {
            GroupDisplayName = displayName;
            SuggestedDirectory = suggestedDirectory;
            return path;
        }
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
        public string? RequestDisplayName(string resourceLabel, string id, string currentDisplayName) => RenameResult;

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
        public UnsavedChangesChoice CloseChoice { get; set; } = UnsavedChangesChoice.Cancel;
        public int CloseConfirmationCount { get; private set; }
        public bool DeleteStoryConfirmed { get; set; }
        public int DeleteStoryConfirmationCount { get; private set; }
        public string? LastDeleteStoryId { get; private set; }
        public IReadOnlyList<string> LastDeleteStoryResources { get; private set; } = [];
        public ProjectCreationRequest? RequestCreate(string? initialParentDirectory = null) => result;
        public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges()
        {
            CloseConfirmationCount++;
            return CloseChoice;
        }
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
                  "schema_version": 3,
                  "identity_format": "story-uid-v1",
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
