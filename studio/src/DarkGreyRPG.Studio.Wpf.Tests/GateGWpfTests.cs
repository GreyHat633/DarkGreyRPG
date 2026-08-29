using DarkGreyRPG.Studio.Core.Dialogues;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Quests;
using DarkGreyRPG.Studio.Services;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class GateGWpfTests
{
    [TestMethod]
    public void DialogueDuplicateCreatesUnsavedIndependentDraftAndPromotesOnSave()
    {
        using var project = CreateProject();
        var source = project.Service.CreateDialogueInStory("home", "source", "Source");
        source.Metadata = new DialogueMetadata { Notes = "source notes" };
        project.Service.SaveDialogue(source);
        var dialogs = new GateDialogs
        {
            PickResult = Descriptor(project, ProjectResourceType.Dialogue, "source", "Source"),
            ImportResult = new("copy", "Copy"),
        };
        var shell = OpenStory(project, dialogs, StoryWorkspaceRoutes.Dialogues);

        shell.DuplicateStoryResourceCommand.Execute(null);

        Assert.AreEqual(ResourcePickerMode.ImportAsNew, dialogs.LastMode);
        Assert.IsTrue(shell.SelectedStoryResource!.IsDraft);
        Assert.AreEqual("source", shell.CurrentDialogue!.Document.SourceTemplateId);
        Assert.AreEqual("source notes", shell.CurrentDialogue.Notes);
        Assert.IsFalse(File.Exists(Path.Combine(project.Root, "dialogues", "copy.json")));
        shell.CurrentDialogue.Notes = "copy notes";
        shell.SaveCurrentResourceCommand.Execute(null);
        Assert.IsFalse(shell.SelectedStoryResource!.IsDraft);
        Assert.AreEqual("source notes", new DialogueRepository(project.Root).LoadDialogue("source").Metadata.Notes);
        Assert.AreEqual("copy notes", new DialogueRepository(project.Root).LoadDialogue("copy").Metadata.Notes);
    }

    [TestMethod]
    public void QuestDuplicateCancelAtPickerOrIdentityDoesNotCreateDraft()
    {
        foreach (var cancelAtPicker in new[] { true, false })
        {
            using var project = CreateProject();
            var source = project.Service.CreateQuestInStory("home", "source", "Source");
            source.Description = "source description";
            project.Service.SaveQuest(source);
            var dialogs = new GateDialogs
            {
                PickResult = cancelAtPicker
                    ? null
                    : Descriptor(project, ProjectResourceType.Quest, "source", "Source"),
                ImportResult = cancelAtPicker ? new("copy", "Copy") : null,
            };
            var shell = OpenStory(project, dialogs, StoryWorkspaceRoutes.Quests);
            shell.DuplicateStoryResourceCommand.Execute(null);

            Assert.AreEqual(ResourcePickerMode.ImportAsNew, dialogs.LastMode);
            Assert.IsFalse(File.Exists(Path.Combine(project.Root, "quests", "copy.json")));
            Assert.IsFalse(shell.StoryWorkspace.Quests!.Items.Any(item => item.Id == "copy"));
            Assert.IsNull(shell.SelectedStoryResource);
            Assert.IsNull(shell.CurrentQuest);
        }
    }

    [TestMethod]
    public void DialogueDuplicateDiscardOnSwitchLeavesSourceAndTargetUntouched()
    {
        using var project = CreateProject();
        var source = project.Service.CreateDialogueInStory("home", "source", "Source");
        source.Metadata = new DialogueMetadata { Notes = "source notes" };
        project.Service.SaveDialogue(source);
        project.Service.CreateDialogueInStory("target", "other", "Other");
        var sourcePath = Path.Combine(project.Root, "dialogues", "source.json");
        var sourceBytes = File.ReadAllBytes(sourcePath);
        var dialogs = new GateDialogs
        {
            PickResult = Descriptor(project, ProjectResourceType.Dialogue, "source", "Source"),
            ImportResult = new("copy", "Copy"),
            CloseChoice = UnsavedChangesChoice.Discard,
        };
        var shell = OpenStory(project, dialogs, StoryWorkspaceRoutes.Dialogues);
        shell.DuplicateStoryResourceCommand.Execute(null);
        shell.CurrentDialogue!.Notes = "discarded copy";

        shell.SelectedStoryResource = shell.StoryWorkspace.Dialogues!.Items.Single(item => item.Id == "other");

        Assert.IsFalse(File.Exists(Path.Combine(project.Root, "dialogues", "copy.json")));
        Assert.DoesNotContain("copy", project.Session.Stories.LoadStory("target").OwnedResources.Dialogues);
        CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(sourcePath));
        Assert.AreEqual("source notes", new DialogueRepository(project.Root).LoadDialogue("source").Metadata.Notes);
    }

    [TestMethod]
    public void QuestDuplicateCopiesContentWithoutChangingSource()
    {
        using var project = CreateProject();
        var source = project.Service.CreateQuestInStory("home", "source", "Source");
        source.Description = "source description";
        project.Service.SaveQuest(source);
        var dialogs = new GateDialogs
        {
            PickResult = Descriptor(project, ProjectResourceType.Quest, "source", "Source"),
            ImportResult = new("copy", "Copy"),
        };
        var shell = OpenStory(project, dialogs, StoryWorkspaceRoutes.Quests);

        shell.DuplicateStoryResourceCommand.Execute(null);

        Assert.IsTrue(shell.SelectedStoryResource!.IsDraft);
        Assert.AreEqual("source", shell.CurrentQuest!.Document.SourceTemplateId);
        Assert.AreEqual("source description", shell.CurrentQuest.Description);
        Assert.IsFalse(File.Exists(Path.Combine(project.Root, "quests", "copy.json")));
        shell.CurrentQuest.Description = "copy description";
        shell.SaveCurrentResourceCommand.Execute(null);
        Assert.AreEqual("source description", new QuestRepository(project.Root).LoadQuest("source").Description);
        Assert.AreEqual("copy description", new QuestRepository(project.Root).LoadQuest("copy").Description);
    }

    [TestMethod]
    public void ReferenceFiltersExistingMembershipAndPreservesHomeStoryAndFile()
    {
        using var project = CreateProject();
        var source = project.Service.CreateDialogueInStory("home", "source", "Source");
        project.Service.SaveDialogue(source);
        var alreadyReferenced = project.Service.CreateDialogueInStory("home", "already", "Already");
        project.Service.SaveDialogue(alreadyReferenced);
        project.Service.AddDialogueReference("target", "already");
        var sourcePath = Path.Combine(project.Root, "dialogues", "source.json");
        var originalBytes = File.ReadAllBytes(sourcePath);
        var dialogs = new GateDialogs
        {
            PickResult = Descriptor(project, ProjectResourceType.Dialogue, "source", "Source"),
        };
        var shell = OpenStory(project, dialogs, StoryWorkspaceRoutes.Dialogues);

        shell.ReferenceStoryResourceCommand.Execute(null);

        Assert.IsFalse(dialogs.LastCandidates.Any(item => item.Id == "already"));
        Assert.Contains("source", project.Session.Stories.LoadStory("target").ReferencedResources.Dialogues);
        Assert.AreEqual("home", new DialogueRepository(project.Root).LoadDialogue("source").HomeStoryId);
        CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(sourcePath));
        var card = shell.StoryWorkspace.Dialogues!.Items.Single(item => item.Id == "source");
        Assert.IsTrue(card.IsReferenced);
        StringAssert.Contains(card.MembershipTooltip, "来源故事");
    }

    [TestMethod]
    public void QuestReferenceUsesTypedCandidatesAndPreservesHomeStory()
    {
        using var project = CreateProject();
        var source = project.Service.CreateQuestInStory("home", "source", "Source");
        project.Service.SaveQuest(source);
        var existing = project.Service.CreateQuestInStory("home", "existing", "Existing");
        project.Service.SaveQuest(existing);
        project.Service.AddQuestReference("target", "existing");
        var sourcePath = Path.Combine(project.Root, "quests", "source.json");
        var originalBytes = File.ReadAllBytes(sourcePath);
        var dialogs = new GateDialogs { PickResult = Descriptor(project, ProjectResourceType.Quest, "source", "Source") };
        var shell = OpenStory(project, dialogs, StoryWorkspaceRoutes.Quests);

        shell.ReferenceStoryResourceCommand.Execute(null);

        var candidate = dialogs.LastCandidates.Single(item => item.Id == "source");
        Assert.AreEqual(ProjectResourceType.Quest, candidate.Type);
        Assert.AreEqual("Home", candidate.HomeStoryDisplayName);
        Assert.IsFalse(dialogs.LastCandidates.Any(item => item.Id == "existing"));
        Assert.Contains("source", project.Session.Stories.LoadStory("target").ReferencedResources.Quests);
        Assert.AreEqual("home", new QuestRepository(project.Root).LoadQuest("source").HomeStoryId);
        CollectionAssert.AreEqual(originalBytes, File.ReadAllBytes(sourcePath));
        var card = shell.StoryWorkspace.Quests!.Items.Single(item => item.Id == "source");
        Assert.IsTrue(card.IsReferenced);
        StringAssert.Contains(card.MembershipTooltip, "来源故事");
    }

    [TestMethod]
    public void ReferencedDialogueEditIsVisibleFromHomeStory()
    {
        using var project = CreateProject();
        var source = project.Service.CreateDialogueInStory("home", "shared", "Shared");
        project.Service.SaveDialogue(source);
        var sourcePath = Path.Combine(project.Root, "dialogues", "shared.json");
        var dialogs = new GateDialogs { PickResult = Descriptor(project, ProjectResourceType.Dialogue, "shared", "Shared") };
        var shell = OpenStory(project, dialogs, StoryWorkspaceRoutes.Dialogues);
        shell.ReferenceStoryResourceCommand.Execute(null);
        shell.CurrentDialogue!.Notes = "edited through target";
        shell.SaveCurrentResourceCommand.Execute(null);
        var fileCount = Directory.GetFiles(Path.Combine(project.Root, "dialogues"), "*.json").Length;

        shell.OpenStory(shell.ProjectHome.Stories.Single(item => item.Id == "home"));
        shell.StoryWorkspace.SelectRoute(StoryWorkspaceRoutes.Dialogues);
        shell.SelectedStoryResource = shell.StoryWorkspace.Dialogues!.Items.Single(item => item.Id == "shared");

        Assert.AreEqual("edited through target", shell.CurrentDialogue!.Notes);
        Assert.AreEqual("home", new DialogueRepository(project.Root).LoadDialogue("shared").HomeStoryId);
        Assert.HasCount(fileCount, Directory.GetFiles(Path.Combine(project.Root, "dialogues"), "*.json"));
        Assert.IsTrue(File.Exists(sourcePath));
    }

    [TestMethod]
    public void GateGUiContractContainsTypedCopyAndHomeStoryPickerBindings()
    {
        var mainWindow = ReadRepoFile("studio/src/DarkGreyRPG.Studio/MainWindow.xaml");
        var picker = ReadRepoFile("studio/src/DarkGreyRPG.Studio/Views/ResourcePickerDialog.xaml");
        StringAssert.Contains(mainWindow, "DuplicateStoryResourceCommand");
        StringAssert.Contains(mainWindow, "从现有复制 Dialogue");
        StringAssert.Contains(mainWindow, "从现有复制 Quest");
        StringAssert.Contains(picker, "{Binding Type");
        StringAssert.Contains(picker, "HomeStoryDisplayName");
        StringAssert.Contains(mainWindow, "MembershipTooltip");
    }

    private static TestProject CreateProject()
    {
        var root = Path.Combine(AppContext.BaseDirectory, ".gate-g-test-data", Guid.NewGuid().ToString("N"));
        var service = new ProjectService();
        var session = service.CreateProject(root, "gate_g", "Gate G");
        session.Stories.CreateStory("home", "Home");
        session.Stories.CreateStory("target", "Target");
        return new(root, service, session);
    }

    private static ShellViewModel OpenStory(TestProject project, GateDialogs dialogs, string route)
    {
        var shell = new ShellViewModel(project.Service, new FixedFolderPicker(project.Root), resourceWorkspaceDialogs: dialogs);
        shell.OpenProjectCommand.Execute(null);
        shell.OpenStory(shell.ProjectHome.Stories.Single(item => item.Id == "target"));
        shell.StoryWorkspace.SelectRoute(route);
        return shell;
    }

    private static ResourceDescriptor Descriptor(TestProject project, ProjectResourceType type, string id, string name) =>
        new(type, id, name, Path.Combine(project.Root, type == ProjectResourceType.Dialogue ? "dialogues" : "quests", id + ".json"));

    private static string ReadRepoFile(string relativePath)
    {
        for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
        {
            var candidate = Path.Combine(directory.FullName, relativePath.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(candidate)) return File.ReadAllText(candidate);
        }
        throw new FileNotFoundException(relativePath);
    }

    private sealed record TestProject(string Root, ProjectService Service, ProjectSession Session) : IDisposable
    {
        public void Dispose()
        {
            if (Directory.Exists(Root)) Directory.Delete(Root, recursive: true);
        }
    }

    private sealed class FixedFolderPicker(string root) : IProjectFolderPicker
    {
        public string? PickProjectFolder() => root;
    }

    private sealed class GateDialogs : IResourceWorkspaceDialogs
    {
        public ResourceDescriptor? PickResult { get; set; }
        public ResourceIdentityRequest? ImportResult { get; set; }
        public UnsavedChangesChoice CloseChoice { get; set; } = UnsavedChangesChoice.Cancel;
        public ResourcePickerMode? LastMode { get; private set; }
        public IReadOnlyList<ResourceDescriptor> LastCandidates { get; private set; } = [];
        public ResourceCreationMode? RequestCreationMode(ProjectResourceType type, string storyDisplayName) => null;
        public ResourceIdentityRequest? RequestCreate(ProjectResourceType type, string suggestedId) => null;
        public ResourceIdentityRequest? RequestImportIdentity(ProjectResourceType type, ResourceDescriptor source, string suggestedId) => ImportResult;
        public ResourceDescriptor? PickResource(ProjectResourceType type, IReadOnlyList<ResourceDescriptor> candidates, ResourcePickerMode mode, string storyDisplayName)
        {
            LastMode = mode;
            LastCandidates = candidates;
            return PickResult;
        }
        public bool ConfirmDelete(ResourceDescriptor resource) => false;
        public bool ConfirmDiscardDraft(ResourceDescriptor resource) => false;
        public bool ConfirmRemoveReference(ResourceDescriptor resource, string storyDisplayName) => false;
        public void ShowReferences(ResourceDescriptor resource, IReadOnlyList<ResourceDescriptor> references) { }
        public bool ConfirmSaveBeforeSwitch(ResourceDescriptor resource) => false;
        public UnsavedChangesChoice ConfirmCloseWithUnsavedChanges(ResourceDescriptor resource) => CloseChoice;
    }
}
