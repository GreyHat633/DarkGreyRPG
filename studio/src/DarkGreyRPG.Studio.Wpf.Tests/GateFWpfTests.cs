using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Quests;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.Services;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class GateFWpfTests
{
    [TestMethod]
    public void QuestEditorStartsEmptyAndFirstObjectiveCreatesMainGroup()
    {
        var document = QuestDocument.CreateNew("quest");
        var editor = new QuestEditorViewModel(document, ["hero"]);

        Assert.IsTrue(editor.IsEmptyState);
        Assert.IsEmpty(editor.Objectives);
        Assert.IsEmpty(document.ObjectiveGroups);

        editor.AddFirstCollectCommand.Execute(null);
        Assert.HasCount(1, editor.Objectives);
        Assert.AreEqual("main", document.ObjectiveGroups.Single().Id);
        Assert.AreEqual("collect", document.ObjectiveGroups.Single().Objectives.Single());
        Assert.AreEqual("ALL", editor.CompletionMode);

        editor.CompletionMode = "SEQUENCE";
        Assert.AreEqual("SEQUENCE", document.ObjectiveGroups.Single().Mode);
        editor.DeleteObjectiveCommand.Execute(null);
        Assert.IsTrue(editor.IsEmptyState);
        Assert.IsEmpty(document.ObjectiveGroups);
    }

    [TestMethod]
    public void InteractWithoutActorsRemainsBlankAndInvalid()
    {
        var document = QuestDocument.CreateNew("quest");
        var editor = new QuestEditorViewModel(document);

        editor.AddFirstInteractCommand.Execute(null);

        Assert.AreEqual(string.Empty, editor.SelectedObjective!.ActorId);
        Assert.IsTrue(document.ValidationErrors.Any(issue => issue.Code == "quest.objective.actor.required"));
        Assert.DoesNotContain("actor", document.ToResource().Objectives.Select(item => item.ActorId));
    }

    [TestMethod]
    public void QuestIdentityCancelDoesNotOpenCreationMode()
    {
        using var project = CreateProject();
        var dialogs = new TestDialogs { CreateResult = null };
        var shell = OpenQuests(project, dialogs);

        shell.NewStoryResourceCommand.Execute(null);

        Assert.AreEqual(0, dialogs.CreationModeCalls);
        Assert.IsEmpty(shell.StoryWorkspace.Quests!.Items);
        Assert.IsNull(shell.CurrentQuest);
    }

    [TestMethod]
    public void FirstObjectiveDefaultsAreValidAndNeverUseFakeActor()
    {
        foreach (var item in new[] { "kill", "collect", "interact" })
        {
            var document = QuestDocument.CreateNew($"quest_{item}");
            var editor = new QuestEditorViewModel(document, item == "interact" ? ["hero"] : []);
            if (item == "kill") editor.AddFirstKillCommand.Execute(null);
            if (item == "collect") editor.AddFirstCollectCommand.Execute(null);
            if (item == "interact") editor.AddFirstInteractCommand.Execute(null);

            var objective = editor.SelectedObjective!;
            Assert.AreEqual(item switch { "kill" => "kill_entity", "collect" => "collect_item", _ => "interact_actor" }, objective.Type);
            Assert.AreEqual("main", document.ObjectiveGroups.Single().Id);
            Assert.AreEqual(objective.Id, document.ObjectiveGroups.Single().Objectives.Single());
            Assert.IsGreaterThan(0, objective.Required);
            Assert.AreNotEqual("actor", objective.ActorId);
        }

        var noActor = QuestDocument.CreateNew("quest_no_actor");
        var noActorEditor = new QuestEditorViewModel(noActor);
        noActorEditor.AddFirstInteractCommand.Execute(null);
        Assert.AreEqual(string.Empty, noActorEditor.SelectedObjective!.ActorId);
        Assert.IsTrue(noActor.ValidationErrors.Any(issue => issue.Code == "quest.objective.actor.required"));
    }

    [TestMethod]
    public void CompletionModeEmptyStateAndDeleteUndoRedoPreserveState()
    {
        var document = QuestDocument.CreateNew("quest_modes");
        var editor = new QuestEditorViewModel(document);
        editor.CompletionMode = "SEQUENCE";
        Assert.IsEmpty(document.ObjectiveGroups);
        Assert.AreEqual("SEQUENCE", editor.CompletionMode);
        editor.AddFirstKillCommand.Execute(null);
        foreach (var mode in new[] { "ALL", "ANY", "SEQUENCE" })
        {
            editor.CompletionMode = mode;
            Assert.AreEqual(mode, document.ObjectiveGroups.Single().Mode);
        }
        editor.AddFirstCollectCommand.Execute(null);
        _ = editor.SelectedObjective!.Id;
        editor.DeleteObjectiveCommand.Execute(null);
        editor.DeleteObjectiveCommand.Execute(null);
        Assert.IsEmpty(editor.Objectives);
        Assert.IsEmpty(document.ObjectiveGroups);
        editor.UndoCommand.Execute(null);
        Assert.AreEqual("kill", editor.SelectedObjective!.Id);
        Assert.AreEqual("SEQUENCE", document.ObjectiveGroups.Single().Mode);
        editor.RedoCommand.Execute(null);
        Assert.IsEmpty(editor.Objectives);
        Assert.IsEmpty(document.ObjectiveGroups);
    }

    [TestMethod]
    public void QuestDraftCardHasQuestTooltipAndSaveReloadsWithoutFakeActor()
    {
        using var project = CreateProject();
        var dialogs = new TestDialogs { CreateResult = new("draft", "Draft") };
        var shell = OpenQuests(project, dialogs);
        shell.NewStoryResourceCommand.Execute(null);
        var draft = shell.SelectedStoryResource!;
        Assert.AreEqual(ProjectResourceType.Quest, draft.ResourceType);
        StringAssert.Contains(draft.MembershipTooltip, "任务草稿");
        shell.CurrentQuest!.AddFirstKillCommand.Execute(null);
        shell.CurrentQuest.Description = "完成任务";
        shell.SaveCurrentResourceCommand.Execute(null);

        var json = File.ReadAllText(Path.Combine(project.Root, "quests", "draft.json"));
        Assert.IsFalse(json.Contains("\"actor_id\":\"actor\"", StringComparison.Ordinal));
        var restarted = new ProjectService();
        restarted.OpenProject(project.Root);
        Assert.IsNotNull(restarted.OpenQuest("draft"));
    }

    [TestMethod]
    public void QuestSwitchSaveDiscardCancelAndCloseChoicesAreHandled()
    {
        using var saveProject = CreateProject(withOtherQuest: true);
        var saveDialogs = new TestDialogs { CreateResult = new("save_draft", "Save"), CloseChoice = UnsavedChangesChoice.Save };
        var saveShell = OpenQuests(saveProject, saveDialogs);
        saveShell.NewStoryResourceCommand.Execute(null);
        saveShell.CurrentQuest!.AddFirstKillCommand.Execute(null);
        saveShell.CurrentQuest.Description = "保存";
        saveShell.SelectedStoryResource = saveShell.StoryWorkspace.Quests!.Items.Single(item => item.Id == "other");
        Assert.AreEqual("other", saveShell.SelectedStoryResource!.Id);
        Assert.IsFalse(saveShell.StoryWorkspace.Quests.Items.Any(item => item.Id == "save_draft" && item.IsDraft));

        using var discardProject = CreateProject(withOtherQuest: true);
        var discardDialogs = new TestDialogs { CreateResult = new("discard_draft", "Discard"), CloseChoice = UnsavedChangesChoice.Discard };
        var discardShell = OpenQuests(discardProject, discardDialogs);
        discardShell.NewStoryResourceCommand.Execute(null);
        discardShell.SelectedStoryResource = discardShell.StoryWorkspace.Quests!.Items.Single(item => item.Id == "other");
        Assert.IsFalse(File.Exists(Path.Combine(discardProject.Root, "quests", "discard_draft.json")));
        Assert.IsFalse(discardShell.StoryWorkspace.Quests.Items.Any(item => item.Id == "discard_draft"));

        using var cancelProject = CreateProject(withOtherQuest: true);
        var cancelDialogs = new TestDialogs { CreateResult = new("cancel_draft", "Cancel"), CloseChoice = UnsavedChangesChoice.Cancel };
        var cancelShell = OpenQuests(cancelProject, cancelDialogs);
        cancelShell.NewStoryResourceCommand.Execute(null);
        var selected = cancelShell.SelectedStoryResource;
        cancelShell.SelectedStoryResource = cancelShell.StoryWorkspace.Quests!.Items.Single(item => item.Id == "other");
        Assert.AreSame(selected, cancelShell.SelectedStoryResource);
        Assert.IsFalse(cancelShell.TryClose());
        cancelDialogs.CloseChoice = UnsavedChangesChoice.Discard;
        Assert.IsTrue(cancelShell.TryClose());

        using var closeSaveProject = CreateProject();
        var closeSaveDialogs = new TestDialogs { CreateResult = new("close_save", "Close Save"), CloseChoice = UnsavedChangesChoice.Save };
        var closeSaveShell = OpenQuests(closeSaveProject, closeSaveDialogs);
        closeSaveShell.NewStoryResourceCommand.Execute(null);
        closeSaveShell.CurrentQuest!.AddFirstKillCommand.Execute(null);
        closeSaveShell.CurrentQuest.Description = "关闭时保存";
        Assert.IsTrue(closeSaveShell.TryClose());
        Assert.IsTrue(File.Exists(Path.Combine(closeSaveProject.Root, "quests", "close_save.json")));
    }

    [TestMethod]
    public void QuestSaveFailureRetainsExactSelectedEditableDraft()
    {
        using var project = CreateProject();
        var dialogs = new TestDialogs { CreateResult = new("invalid", "Invalid"), CloseChoice = UnsavedChangesChoice.Save };
        var shell = OpenQuests(project, dialogs);
        shell.NewStoryResourceCommand.Execute(null);
        var selected = shell.SelectedStoryResource;
        var editor = shell.CurrentQuest;

        Assert.IsFalse(shell.TryClose());
        Assert.AreSame(selected, shell.SelectedStoryResource);
        Assert.AreSame(editor, shell.CurrentQuest);
        Assert.IsTrue(shell.SelectedStoryResource!.IsDraft);
        Assert.IsFalse(File.Exists(Path.Combine(project.Root, "quests", "invalid.json")));
    }

    [TestMethod]
    public void QuestDraftCreateDiscardAndSavePromoteCard()
    {
        using var project = CreateProject();
        var dialogs = new TestDialogs { CreateResult = new("draft", "Draft") };
        var shell = new ShellViewModel(project.Service, new FixedFolderPicker(project.Root),
            projectWorkspaceDialogs: dialogs, resourceWorkspaceDialogs: dialogs);
        shell.OpenProjectCommand.Execute(null);
        shell.ProjectHome.SelectedStory = shell.ProjectHome.Stories.Single();
        shell.OpenSelectedStoryCommand.Execute(null);
        shell.StoryWorkspace.SelectRoute(StoryWorkspaceRoutes.Quests);
        shell.NewStoryResourceCommand.Execute(null);

        var draft = shell.SelectedStoryResource;
        Assert.IsTrue(draft!.IsDraft);
        Assert.AreSame(draft, shell.StoryWorkspace.Quests!.SelectedItem);
        Assert.IsEmpty(project.Service.OpenQuestDocuments.Single().Objectives);
        Assert.IsFalse(File.Exists(Path.Combine(project.Root, "quests", "draft.json")));

        shell.CurrentQuest!.AddFirstKillCommand.Execute(null);
        shell.CurrentQuest.Description = "完成任务";
        Assert.IsTrue(shell.CurrentQuest.CanSave, shell.CurrentQuest.ValidationText);
        shell.SaveCurrentResourceCommand.Execute(null);

        Assert.IsFalse(shell.CurrentQuest.Document.IsNewDraft, shell.StatusMessage);
        Assert.IsFalse(shell.SelectedStoryResource!.IsDraft);
        Assert.IsTrue(File.Exists(Path.Combine(project.Root, "quests", "draft.json")));
        Assert.Contains("draft", project.Session.Stories.LoadStory("intro").OwnedResources.Quests);

        dialogs.CreateResult = new("discard", "Discard");
        shell.NewStoryResourceCommand.Execute(null);
        dialogs.CloseChoice = UnsavedChangesChoice.Discard;
        shell.SelectedStoryResource = shell.StoryWorkspace.Quests.Items.Single(item => item.Id == "draft");
        Assert.IsFalse(shell.StoryWorkspace.Quests.Items.Any(item => item.Id == "discard"));
    }

    [TestMethod]
    public void DirtyQuestDraftCanBeDiscardedFromResourceCommand()
    {
        using var project = CreateProject();
        var dialogs = new TestDialogs { CreateResult = new("discard_command", "Discard command") };
        var shell = OpenQuests(project, dialogs);
        shell.NewStoryResourceCommand.Execute(null);

        Assert.AreEqual("放弃草稿", shell.SelectedStoryResourceActionText);
        shell.CurrentQuest!.AddFirstKillCommand.Execute(null);
        Assert.IsTrue(shell.CurrentQuest.IsDirty);
        Assert.IsTrue(shell.DeleteStoryResourceCommand.CanExecute(null));

        shell.DeleteStoryResourceCommand.Execute(null);

        Assert.IsFalse(shell.StoryWorkspace.Quests!.Items.Any(item => item.Id == "discard_command"));
        Assert.IsFalse(File.Exists(Path.Combine(project.Root, "quests", "discard_command.json")));
        Assert.AreEqual("删除资源", shell.SelectedStoryResourceActionText);
    }

    private static TestProject CreateProject(bool withOtherQuest = false)
    {
        var root = Path.Combine(AppContext.BaseDirectory, ".gate-f-test-data", Guid.NewGuid().ToString("N"));
        var service = new ProjectService();
        var session = service.CreateProject(root, "gate_f", "Gate F");
        session.Stories.CreateStory("intro", "Intro");
        if (withOtherQuest) service.CreateQuestInStory("intro", "other", "Other");
        return new TestProject(root, service, session);
    }

    private static ShellViewModel OpenQuests(TestProject project, TestDialogs dialogs)
    {
        var shell = new ShellViewModel(project.Service, new FixedFolderPicker(project.Root),
            projectWorkspaceDialogs: dialogs, resourceWorkspaceDialogs: dialogs);
        shell.OpenProjectCommand.Execute(null);
        shell.ProjectHome.SelectedStory = shell.ProjectHome.Stories.Single();
        shell.OpenSelectedStoryCommand.Execute(null);
        shell.StoryWorkspace.SelectRoute(StoryWorkspaceRoutes.Quests);
        return shell;
    }

    private sealed record TestProject(string Root, ProjectService Service, ProjectSession Session) : IDisposable
    {
        public void Dispose() { if (Directory.Exists(Root)) Directory.Delete(Root, true); }
    }

    private sealed class FixedFolderPicker(string root) : IProjectFolderPicker
    {
        public string? PickProjectFolder() => root;
    }

    private sealed class TestDialogs : IResourceWorkspaceDialogs, IProjectWorkspaceDialogs
    {
        public ResourceIdentityRequest? CreateResult { get; set; }
        public UnsavedChangesChoice CloseChoice { get; set; } = UnsavedChangesChoice.Cancel;
        public int CreationModeCalls { get; private set; }
        public ResourceCreationMode? RequestCreationMode(ProjectResourceType type, string storyDisplayName)
        {
            CreationModeCalls++;
            return ResourceCreationMode.Blank;
        }
        public ResourceIdentityRequest? RequestCreate(ProjectResourceType type, string suggestedId) => CreateResult;
        public ResourceIdentityRequest? RequestImportIdentity(ProjectResourceType type, ResourceDescriptor source, string suggestedId) => null;
        public ResourceDescriptor? PickResource(ProjectResourceType type, IReadOnlyList<ResourceDescriptor> candidates, ResourcePickerMode mode, string storyDisplayName) => null;
        public bool ConfirmDelete(ResourceDescriptor resource) => false;
        public bool ConfirmDiscardDraft(ResourceDescriptor resource) => true;
        public bool ConfirmRemoveReference(ResourceDescriptor resource, string storyDisplayName) => false;
        public void ShowReferences(ResourceDescriptor resource, IReadOnlyList<ResourceDescriptor> references) { }
        public bool ConfirmSaveBeforeSwitch(ResourceDescriptor resource) => false;
        public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges(ResourceDescriptor resource) => CloseChoice;
        public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges() => CloseChoice;
        public ProjectCreationRequest? RequestCreate(string? initialParentDirectory = null) => null;
        public bool ConfirmDeleteStory(string storyId, string displayName, IReadOnlyList<string> resourcesToDelete) => false;
    }
}
