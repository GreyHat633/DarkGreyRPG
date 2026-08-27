using DarkGreyRPG.Studio.Core.Dialogues;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class GateEWpfTests
{
    [TestMethod]
    public void DirectDialogueCreateIsInMemoryTopSelectedDraft()
    {
        using var project = CreateProject();
        var service = project.Service;
        service.CreateDialogueInStory("intro", "existing", "Existing");
        var dialogs = new GateEDialogs { CreateResult = new("draft", "Draft") };
        var shell = OpenDialogues(project, dialogs);

        shell.NewStoryResourceCommand.Execute(null);

        var item = shell.StoryWorkspace.Dialogues!.Items[0];
        Assert.AreEqual("draft", item.Id);
        Assert.IsTrue(item.IsDraft);
        Assert.AreEqual("未保存", item.DraftBadge);
        Assert.AreSame(item, shell.SelectedStoryResource);
        Assert.IsNotNull(shell.CurrentDialogue);
        Assert.IsFalse(File.Exists(Path.Combine(project.Root, "dialogues", "draft.json")));
        Assert.DoesNotContain("draft", project.Session.Stories.LoadStory("intro").OwnedResources.Dialogues);
        Assert.AreEqual(0, dialogs.CreationModeCalls);
    }

    [TestMethod]
    public void DialogueIdentityCancelNeverOpensCreationMode()
    {
        using var project = CreateProject();
        var dialogs = new GateEDialogs { CreateResult = null, CreationMode = ResourceCreationMode.ImportAsNew };
        var shell = OpenDialogues(project, dialogs);

        shell.NewStoryResourceCommand.Execute(null);

        Assert.IsNull(shell.CurrentDialogue);
        Assert.AreEqual(0, dialogs.CreationModeCalls);
        Assert.IsEmpty(shell.StoryWorkspace.Dialogues!.Items);
    }

    [TestMethod]
    public void FirstLineAndChoiceCreationAreExplicitAndNamedEndRequiresAction()
    {
        using var project = CreateProject();
        var shell = OpenDialogues(project, new GateEDialogs { CreateResult = new("draft", "Draft") });
        shell.NewStoryResourceCommand.Execute(null);
        var editor = shell.CurrentDialogue!;

        Assert.IsTrue(editor.IsStarterEmptyState);
        editor.AddFirstLineCommand.Execute(null);

        var line = editor.Document.Nodes.Single(node => node.Type == "line");
        Assert.AreEqual("line_1", line.Id);
        Assert.AreEqual(string.Empty, line.Next);
        Assert.HasCount(1, editor.Document.Nodes);
        Assert.AreEqual("line_1", editor.Document.Entry);

        using var namedEndProject = CreateProject();
        var namedEndShell = OpenDialogues(namedEndProject, new GateEDialogs { CreateResult = new("draft", "Draft") });
        namedEndShell.NewStoryResourceCommand.Execute(null);
        var namedEndEditor = namedEndShell.CurrentDialogue!;
        namedEndEditor.AddEndCommand.Execute(null);
        Assert.IsFalse(namedEndEditor.IsStarterEmptyState);
        Assert.IsTrue(namedEndEditor.SelectedNode!.IsEnd);
    }

    [TestMethod]
    public void ChoiceCreationDoesNotCreateImplicitEndsAndLastNodeCanBeDeleted()
    {
        using var project = CreateProject();
        var shell = OpenDialogues(project, new GateEDialogs { CreateResult = new("draft", "Draft") });
        shell.NewStoryResourceCommand.Execute(null);
        var editor = shell.CurrentDialogue!;

        editor.AddChoiceCommand.Execute(null);
        Assert.HasCount(1, editor.Nodes);
        Assert.IsTrue(editor.Nodes.Single().IsChoice);
        Assert.IsTrue(editor.Nodes.Single().Choices.All(choice => string.IsNullOrEmpty(choice.Next)));
        Assert.AreEqual(editor.Nodes.Single().Id, editor.Entry);

        editor.DeleteNodeCommand.Execute(null);
        Assert.IsEmpty(editor.Nodes);
        Assert.AreEqual(string.Empty, editor.Entry);
        Assert.IsNull(editor.SelectedNode);
        Assert.IsFalse(editor.DeleteNodeCommand.CanExecute(null));
    }

    [TestMethod]
    public void ExplicitSavePromotesDraftAndKeepsSelection()
    {
        using var project = CreateProject(withActor: true);
        var shell = OpenDialogues(project, new GateEDialogs { CreateResult = new("draft", "Draft") });
        shell.NewStoryResourceCommand.Execute(null);
        var editor = shell.CurrentDialogue!;
        editor.AddFirstLineCommand.Execute(null);
        editor.Nodes.Single(node => node.IsLine).Speaker = "hero";
        shell.SaveCurrentResourceCommand.Execute(null);

        var item = shell.SelectedStoryResource!;
        Assert.IsFalse(item.IsDraft);
        Assert.IsTrue(item.IsOwned);
        Assert.IsNotNull(item.Descriptor);
        Assert.AreEqual("draft", item.Id);
        Assert.IsFalse(shell.CurrentDialogue!.Document.IsNewDraft);
        Assert.IsTrue(File.Exists(Path.Combine(project.Root, "dialogues", "draft.json")));
        Assert.Contains("draft", project.Session.Stories.LoadStory("intro").OwnedResources.Dialogues);
    }

    [TestMethod]
    public void SwitchingWithSavePromotesWithoutStaleSelection()
    {
        using var project = CreateProject(withActor: true);
        project.Service.CreateDialogueInStory("intro", "other", "Other");
        var dialogs = new GateEDialogs { CreateResult = new("draft", "Draft"), CloseChoice = UnsavedChangesChoice.Save };
        var shell = OpenDialogues(project, dialogs);
        shell.NewStoryResourceCommand.Execute(null);
        shell.CurrentDialogue!.AddFirstLineCommand.Execute(null);
        shell.CurrentDialogue.Nodes.Single(node => node.IsLine).Speaker = "hero";
        var target = shell.StoryWorkspace.Dialogues!.Items.Single(item => item.Id == "other");

        shell.SelectedStoryResource = target;

        Assert.AreSame(target, shell.SelectedStoryResource);
        Assert.AreSame(target, shell.StoryWorkspace.Dialogues.SelectedItem);
        Assert.AreEqual("other", shell.CurrentDialogue?.Id);
        Assert.IsFalse(shell.StoryWorkspace.Dialogues.Items.Any(item => item.IsDraft));
    }

    [TestMethod]
    public void SwitchingWithDiscardRemovesDraftWithoutDiskState()
    {
        using var project = CreateProject();
        project.Service.CreateDialogueInStory("intro", "other", "Other");
        var dialogs = new GateEDialogs { CreateResult = new("draft", "Draft"), CloseChoice = UnsavedChangesChoice.Discard };
        var shell = OpenDialogues(project, dialogs);
        shell.NewStoryResourceCommand.Execute(null);
        var target = shell.StoryWorkspace.Dialogues!.Items.Single(item => item.Id == "other");
        shell.SelectedStoryResource = target;

        Assert.AreEqual("other", shell.SelectedStoryResource?.Id);
        Assert.IsFalse(File.Exists(Path.Combine(project.Root, "dialogues", "draft.json")));
        Assert.DoesNotContain("draft", project.Session.Stories.LoadStory("intro").OwnedResources.Dialogues);
        Assert.IsFalse(shell.StoryWorkspace.Dialogues.Items.Any(item => item.Id == "draft"));
    }

    [TestMethod]
    public void SwitchingWithCancelBlocksNavigation()
    {
        using var project = CreateProject();
        project.Service.CreateDialogueInStory("intro", "other", "Other");
        var dialogs = new GateEDialogs { CreateResult = new("draft", "Draft"), CloseChoice = UnsavedChangesChoice.Cancel };
        var shell = OpenDialogues(project, dialogs);
        shell.NewStoryResourceCommand.Execute(null);
        var draft = shell.SelectedStoryResource;
        shell.SelectedStoryResource = shell.StoryWorkspace.Dialogues!.Items.Single(item => item.Id == "other");

        Assert.AreSame(draft, shell.SelectedStoryResource);
        Assert.AreEqual("draft", shell.CurrentDialogue?.Id);
    }

    [TestMethod]
    public void TryCloseSupportsSaveDiscardAndCancel()
    {
        using var saveProject = CreateProject();
        var saveDialogs = new GateEDialogs { CreateResult = new("save", "Save"), CloseChoice = UnsavedChangesChoice.Save };
        var saveShell = OpenDialogues(saveProject, saveDialogs);
        saveShell.NewStoryResourceCommand.Execute(null);
        saveShell.CurrentDialogue!.AddEndCommand.Execute(null);
        Assert.IsTrue(saveShell.TryClose());
        Assert.IsTrue(File.Exists(Path.Combine(saveProject.Root, "dialogues", "save.json")));

        using var discardProject = CreateProject();
        var discardDialogs = new GateEDialogs { CreateResult = new("discard", "Discard"), CloseChoice = UnsavedChangesChoice.Discard };
        var discardShell = OpenDialogues(discardProject, discardDialogs);
        discardShell.NewStoryResourceCommand.Execute(null);
        Assert.IsTrue(discardShell.TryClose());
        Assert.IsFalse(File.Exists(Path.Combine(discardProject.Root, "dialogues", "discard.json")));

        using var cancelProject = CreateProject();
        var cancelDialogs = new GateEDialogs { CreateResult = new("cancel", "Cancel"), CloseChoice = UnsavedChangesChoice.Cancel };
        var cancelShell = OpenDialogues(cancelProject, cancelDialogs);
        cancelShell.NewStoryResourceCommand.Execute(null);
        Assert.IsFalse(cancelShell.TryClose());
        Assert.AreEqual("cancel", cancelShell.CurrentDialogue?.Id);
    }

    [TestMethod]
    public void SaveFailureRetainsSelectedEditableDraft()
    {
        using var project = CreateProject();
        var dialogs = new GateEDialogs { CreateResult = new("draft", "Draft"), CloseChoice = UnsavedChangesChoice.Save };
        var shell = OpenDialogues(project, dialogs);
        shell.NewStoryResourceCommand.Execute(null);
        var editor = shell.CurrentDialogue!;
        editor.Entry = "missing";
        var selected = shell.SelectedStoryResource;

        Assert.IsFalse(shell.TryClose());
        Assert.AreSame(selected, shell.SelectedStoryResource);
        Assert.AreSame(editor, shell.CurrentDialogue);
        Assert.IsTrue(shell.SelectedStoryResource!.IsDraft);
        Assert.IsTrue(editor.IsDirty);
        Assert.IsFalse(File.Exists(Path.Combine(project.Root, "dialogues", "draft.json")));
    }

    private static TestProject CreateProject(bool withActor = false)
    {
        var root = Path.Combine(AppContext.BaseDirectory, ".gate-e-test-data", Guid.NewGuid().ToString("N"));
        var service = new ProjectService();
        var session = service.CreateProject(root, "gate_e", "Gate E");
        session.Stories.CreateStory("intro", "Intro");
        if (withActor) service.CreateActorInStory("intro", "hero", "Hero");
        return new TestProject(root, service, session);
    }

    private static ShellViewModel OpenDialogues(TestProject project, GateEDialogs dialogs)
    {
        var shell = new ShellViewModel(project.Service, new FixedFolderPicker(project.Root), resourceWorkspaceDialogs: dialogs);
        shell.OpenProjectCommand.Execute(null);
        shell.ProjectHome.SelectedStory = shell.ProjectHome.Stories.Single();
        shell.OpenSelectedStoryCommand.Execute(null);
        shell.StoryWorkspace.SelectRoute(StoryWorkspaceRoutes.Dialogues);
        return shell;
    }

    private sealed record TestProject(string Root, ProjectService Service, ProjectSession Session) : IDisposable
    {
        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }

    private sealed class FixedFolderPicker(string root) : IProjectFolderPicker
    {
        public string? PickProjectFolder() => root;
    }

    private sealed class GateEDialogs : IResourceWorkspaceDialogs
    {
        public ResourceIdentityRequest? CreateResult { get; init; }
        public ResourceCreationMode? CreationMode { get; init; }
        public UnsavedChangesChoice CloseChoice { get; init; } = UnsavedChangesChoice.Cancel;
        public int CreationModeCalls { get; private set; }
        public ResourceCreationMode? RequestCreationMode(ProjectResourceType type, string storyDisplayName)
        {
            CreationModeCalls++;
            return CreationMode;
        }
        public ResourceIdentityRequest? RequestCreate(ProjectResourceType type, string suggestedId) => CreateResult;
        public ResourceIdentityRequest? RequestImportIdentity(ProjectResourceType type, ResourceDescriptor source, string suggestedId) => null;
        public ResourceDescriptor? PickResource(ProjectResourceType type, IReadOnlyList<ResourceDescriptor> candidates, ResourcePickerMode mode, string storyDisplayName) => null;
        public bool ConfirmDelete(ResourceDescriptor resource) => false;
        public bool ConfirmDiscardDraft(ResourceDescriptor resource) => false;
        public bool ConfirmRemoveReference(ResourceDescriptor resource, string storyDisplayName) => false;
        public void ShowReferences(ResourceDescriptor resource, IReadOnlyList<ResourceDescriptor> references) { }
        public bool ConfirmSaveBeforeSwitch(ResourceDescriptor resource) => false;
        public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges(ResourceDescriptor resource) => CloseChoice;
    }
}
