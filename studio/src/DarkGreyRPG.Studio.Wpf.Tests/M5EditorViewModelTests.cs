using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Dialogues;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Quests;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class M5EditorViewModelTests
{
    [TestMethod]
    public void DialogueAndQuestEditorsSupportUndoSaveAndRestart()
    {
        using var directory = new M5ProjectDirectory();
        var service = new ProjectService();
        var session = service.CreateProject(directory.Root, "m5_editors", "M5 Editors");
        session.Stories.CreateStory("uncategorized", "未分类");
        service.CreateActorInStory("uncategorized", "hero", "Hero");

        var dialogue = service.CreateDialogueInStory("uncategorized", "intro", "Intro");
        var dialogueEditor = new DialogueEditorViewModel(dialogue, ["hero"]);
        dialogueEditor.AddLineCommand.Execute(null);
        var line = dialogueEditor.Nodes.Single(node => node.IsLine);
        line.Text = "欢迎回来。";
        dialogueEditor.Entry = line.Id;
        dialogueEditor.DisplayName = "开场对话";
        Assert.IsTrue(dialogueEditor.IsDirty);
        dialogueEditor.UndoCommand.Execute(null);
        Assert.AreEqual("Intro", dialogueEditor.DisplayName);
        dialogueEditor.RedoCommand.Execute(null);
        Assert.AreEqual("开场对话", dialogueEditor.DisplayName);
        dialogueEditor.AddChoiceCommand.Execute(null);
        var choiceNode = dialogueEditor.Nodes.Single(node => node.IsChoice);
        choiceNode.AddChoiceOptionCommand.Execute(null);
        Assert.HasCount(3, choiceNode.Choices);
        choiceNode.Choices[^1].RemoveCommand.Execute(null);
        Assert.HasCount(2, choiceNode.Choices);
        service.SaveDialogue(dialogueEditor.Document);
        Assert.IsFalse(dialogueEditor.Document.IsDirty);

        var quest = service.CreateQuestInStory("uncategorized", "evidence", "Evidence");
        var questEditor = new QuestEditorViewModel(quest, ["hero"]);
        questEditor.AddCollectCommand.Execute(null);
        questEditor.SelectedObjective = questEditor.Objectives.Single(item => item.IsCollect);
        questEditor.SelectedObjective.Item = "minecraft:paper";
        questEditor.SelectedObjective.Description = "收集证据";
        questEditor.Description = "找到关键证据。";
        service.SaveQuest(questEditor.Document);

        var restarted = new ProjectService();
        restarted.OpenProject(directory.Root);
        var savedDialogue = restarted.OpenDialogue("intro");
        var savedQuest = restarted.OpenQuest("evidence");
        Assert.AreEqual("开场对话", savedDialogue.DisplayName);
        Assert.IsTrue(savedDialogue.Nodes.Any(node => node.Type == "line" && node.Text == "欢迎回来。"));
        Assert.AreEqual("找到关键证据。", savedQuest.Description);
        Assert.IsTrue(savedQuest.Objectives.Any(objective => objective.Type == "collect_item" && objective.Item == "minecraft:paper"));
    }

    [TestMethod]
    public void ShellCreatesEditsAndRestoresDialogueAndQuestFromStoryRoutes()
    {
        using var directory = new M5ProjectDirectory();
        var setup = new ProjectService();
        var session = setup.CreateProject(directory.Root, "m5_shell", "M5 Shell");
        session.Stories.CreateStory("beta", "Beta");
        setup.CreateActorInStory("beta", "hero", "Hero");

        var dialogs = new FakeResourceWorkspaceDialogs
        {
            CreationMode = ResourceCreationMode.Blank,
            CreateResult = new ResourceIdentityRequest("intro", "开场对话"),
        };
        var shell = CreateShell(directory.Root, dialogs);
        shell.OpenProjectCommand.Execute(null);
        OpenStoryRoute(shell, "beta", StoryWorkspaceRoutes.Dialogues);
        shell.NewStoryResourceCommand.Execute(null);
        Assert.AreEqual("intro", shell.SelectedStoryResource?.Id);
        Assert.IsNotNull(shell.CurrentDialogue);
        shell.CurrentDialogue.AddLineCommand.Execute(null);
        var line = shell.CurrentDialogue.Nodes.Single(node => node.IsLine);
        line.Speaker = "hero";
        line.Text = "案件开始。";
        shell.CurrentDialogue.Entry = line.Id;
        shell.SaveCurrentResourceCommand.Execute(null);

        dialogs.CreateResult = new ResourceIdentityRequest("case", "案件任务");
        OpenStoryRoute(shell, "beta", StoryWorkspaceRoutes.Quests);
        shell.NewStoryResourceCommand.Execute(null);
        shell.CurrentQuest!.AddFirstKillCommand.Execute(null);
        shell.CurrentQuest.Description = "调查现场。";
        shell.SaveCurrentResourceCommand.Execute(null);

        var restarted = CreateShell(directory.Root, new FakeResourceWorkspaceDialogs());
        restarted.OpenProjectCommand.Execute(null);
        OpenStoryRoute(restarted, "beta", StoryWorkspaceRoutes.Dialogues);
        restarted.SelectedStoryResource = restarted.StoryWorkspace.Dialogues!.Items.Single(item => item.Id == "intro");
        Assert.AreEqual("案件开始。", restarted.CurrentDialogue!.Nodes.Single(node => node.IsLine).Text);
        OpenStoryRoute(restarted, "beta", StoryWorkspaceRoutes.Quests);
        restarted.SelectedStoryResource = restarted.StoryWorkspace.Quests!.Items.Single(item => item.Id == "case");
        Assert.AreEqual("调查现场。", restarted.CurrentQuest!.Description);
    }

    [TestMethod]
    public void ShellDialogueImportIsIndependentAndReferenceRemovalPreservesFile()
    {
        using var directory = new M5ProjectDirectory();
        var setup = new ProjectService();
        var session = setup.CreateProject(directory.Root, "m5_lifecycle", "M5 Lifecycle");
        session.Stories.CreateStory("uncategorized", "未分类");
        session.Stories.CreateStory("beta", "Beta");
        var source = setup.CreateDialogueInStory("uncategorized", "source", "Source");
        source.Metadata = new DialogueMetadata { Notes = "template" };
        setup.SaveDialogue(source);
        var sourceDescriptor = new ResourceDescriptor(ProjectResourceType.Dialogue, "source", "Source", Path.Combine(directory.Root, "dialogues", "source.json"));

        var dialogs = new FakeResourceWorkspaceDialogs
        {
            ImportResult = new ResourceIdentityRequest("source_beta", "Beta Source"),
            PickResult = sourceDescriptor,
            Confirmed = true,
        };
        var shell = CreateShell(directory.Root, dialogs);
        shell.OpenProjectCommand.Execute(null);
        OpenStoryRoute(shell, "beta", StoryWorkspaceRoutes.Dialogues);
        shell.DuplicateStoryResourceCommand.Execute(null);
        shell.CurrentDialogue!.Notes = "independent";
        shell.SaveCurrentResourceCommand.Execute(null);
        Assert.AreEqual("template", new DialogueRepository(directory.Root).LoadDialogue("source").Metadata.Notes);

        dialogs.PickResult = sourceDescriptor;
        shell.ReferenceStoryResourceCommand.Execute(null);
        Assert.IsTrue(shell.StoryWorkspace.Dialogues!.Items.Single(item => item.Id == "source").IsReferenced);
        shell.RemoveStoryResourceReferenceCommand.Execute(null);
        Assert.IsTrue(File.Exists(sourceDescriptor.Path));
        Assert.DoesNotContain("source", session.Stories.LoadStory("beta").ReferencedResources.Dialogues);
    }

    private static ShellViewModel CreateShell(string projectDirectory, IResourceWorkspaceDialogs dialogs) =>
        new(new ProjectService(), new FixedProjectFolderPicker(projectDirectory), resourceWorkspaceDialogs: dialogs);

    private static void OpenStoryRoute(ShellViewModel shell, string storyId, string route)
    {
        shell.OpenStory(shell.ProjectHome.Stories.Single(story => story.Id == storyId));
        shell.StoryWorkspace.SelectRoute(route);
    }

    private sealed class FixedProjectFolderPicker(string projectDirectory) : IProjectFolderPicker
    {
        public string? PickProjectFolder() => projectDirectory;
    }

    private sealed class FakeResourceWorkspaceDialogs : IResourceWorkspaceDialogs
    {
        public ResourceCreationMode? CreationMode { get; set; }
        public ResourceIdentityRequest? CreateResult { get; set; }
        public ResourceIdentityRequest? ImportResult { get; set; }
        public ResourceDescriptor? PickResult { get; set; }
        public bool Confirmed { get; set; }
        public IReadOnlyList<ResourceDescriptor> LastReferences { get; private set; } = [];
        public ResourceCreationMode? RequestCreationMode(ProjectResourceType type, string storyDisplayName) => CreationMode;
        public ResourceIdentityRequest? RequestCreate(ProjectResourceType type, string suggestedId) => CreateResult;
        public ResourceIdentityRequest? RequestImportIdentity(ProjectResourceType type, ResourceDescriptor source, string suggestedId) => ImportResult;
        public ResourceDescriptor? PickResource(ProjectResourceType type, IReadOnlyList<ResourceDescriptor> candidates, ResourcePickerMode mode, string storyDisplayName) => PickResult;
        public bool ConfirmDelete(ResourceDescriptor resource) => Confirmed;
        public bool ConfirmDiscardDraft(ResourceDescriptor resource) => Confirmed;
        public bool ConfirmRemoveReference(ResourceDescriptor resource, string storyDisplayName) => Confirmed;
        public void ShowReferences(ResourceDescriptor resource, IReadOnlyList<ResourceDescriptor> references) => LastReferences = references;
        public bool ConfirmSaveBeforeSwitch(ResourceDescriptor resource) => Confirmed;
        public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges(ResourceDescriptor resource) => Confirmed ? UnsavedChangesChoice.Save : UnsavedChangesChoice.Cancel;
    }

    private sealed class M5ProjectDirectory : IDisposable
    {
        public M5ProjectDirectory()
        {
            Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".m5-test-data", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(Root);
        }

        public string Root { get; }

        public void Dispose()
        {
            var basePath = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".m5-test-data"));
            if (!Root.StartsWith(basePath + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Unsafe M5 test cleanup path.");
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }
}
