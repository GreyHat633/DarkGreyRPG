using DarkGreyRPG.Studio.Core.IO;
using DarkGreyRPG.Studio.Core.Dialogues;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Quests;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class GateEFCoreTests
{
    [TestMethod]
    public void DraftCreationDoesNotWriteResourceOrMembershipAndDiscardUnregisters()
    {
        using var project = new TestProjectDirectory();
        var service = new ProjectService();
        service.OpenProject(project.Root);
        service.CreateStory("intro", "Intro");

        var draft = service.CreateDialogueDraftInStory("intro", "greeting", "Greeting");

        Assert.IsTrue(draft.IsNewDraft);
        Assert.AreEqual("intro", draft.DraftOwnerStoryId);
        Assert.IsFalse(draft.HasEverBeenSaved);
        Assert.IsFalse(File.Exists(Path.Combine(project.Root, "dialogues", "greeting.json")));
        Assert.IsEmpty(service.CurrentProject!.Stories.LoadStory("intro").OwnedResources.Dialogues);
        Assert.AreSame(draft, service.OpenDialogue("greeting"));
        Assert.IsTrue(service.DiscardDialogueDraft("greeting"));
        Assert.IsEmpty(service.OpenDialogueDocuments);
    }

    [TestMethod]
    public void DialogueDraftSaveWritesResourceAndOwnershipAndRestartLoadsBoth()
    {
        using var project = new TestProjectDirectory();
        var service = new ProjectService();
        service.OpenProject(project.Root);
        service.CreateStory("intro", "Intro");
        var draft = service.CreateDialogueDraftInStory("intro", "greeting", "Greeting");

        service.SaveDialogue(draft);

        Assert.IsFalse(draft.IsNewDraft);
        Assert.IsTrue(draft.HasEverBeenSaved);
        CollectionAssert.Contains(service.CurrentProject!.Stories.LoadStory("intro").OwnedResources.Dialogues, "greeting");
        var restarted = new ProjectService();
        restarted.OpenProject(project.Root);
        Assert.AreEqual("Greeting", restarted.OpenDialogue("greeting").DisplayName);
        CollectionAssert.Contains(restarted.CurrentProject!.Stories.LoadStory("intro").OwnedResources.Dialogues, "greeting");
    }

    [TestMethod]
    public void ResourceStageFailureLeavesDraftAndNoResource()
    {
        using var project = new TestProjectDirectory();
        var writer = new SelectiveFailingWriter { FailResourceWrites = true };
        var service = new ProjectService(writer);
        service.OpenProject(project.Root);
        service.CreateStory("intro", "Intro");
        var draft = service.CreateDialogueDraftInStory("intro", "greeting", "Greeting");

        Assert.ThrowsExactly<DarkGreyRPG.Studio.Core.Dialogues.DialogueRepositoryException>(() => service.SaveDialogue(draft));
        Assert.IsTrue(draft.IsNewDraft);
        Assert.IsFalse(draft.HasEverBeenSaved);
        Assert.IsFalse(File.Exists(Path.Combine(project.Root, "dialogues", "greeting.json")));
        Assert.IsEmpty(service.CurrentProject!.Stories.LoadStory("intro").OwnedResources.Dialogues);
        Assert.AreSame(draft, service.OpenDialogue("greeting"));
    }

    [TestMethod]
    public void StoryStageFailureRollsBackAndRetrySucceeds()
    {
        using var project = new TestProjectDirectory();
        var writer = new SelectiveFailingWriter();
        var service = new ProjectService(writer);
        service.OpenProject(project.Root);
        service.CreateStory("intro", "Intro");
        var storyPath = Path.Combine(project.Root, "stories", "intro.json");
        var original = File.ReadAllBytes(storyPath);
        var draft = service.CreateDialogueDraftInStory("intro", "greeting", "Greeting");
        writer.FailStoryWrites = true;

        Assert.ThrowsExactly<IOException>(() => service.SaveDialogue(draft));
        Assert.IsTrue(draft.IsNewDraft);
        Assert.IsFalse(File.Exists(Path.Combine(project.Root, "dialogues", "greeting.json")));
        CollectionAssert.AreEqual(original, File.ReadAllBytes(storyPath));
        Assert.IsEmpty(service.CurrentProject!.Stories.LoadStory("intro").OwnedResources.Dialogues);
        Assert.AreSame(draft, service.OpenDialogue("greeting"));

        writer.FailStoryWrites = false;
        service.SaveDialogue(draft);
        Assert.IsFalse(draft.IsNewDraft);
        CollectionAssert.Contains(service.CurrentProject.Stories.LoadStory("intro").OwnedResources.Dialogues, "greeting");
    }

    [TestMethod]
    public void QuestDraftHasEmptySafeDefaultAndNoFakeActor()
    {
        using var project = new TestProjectDirectory();
        var service = new ProjectService();
        service.OpenProject(project.Root);
        service.CreateStory("intro", "Intro");

        var draft = service.CreateQuestDraftInStory("intro", "quest", "Quest");

        Assert.AreEqual("Quest", draft.Title);
        Assert.AreEqual("Quest", draft.DisplayName);
        Assert.AreEqual(string.Empty, draft.Description);
        Assert.IsEmpty(draft.Objectives);
        Assert.IsEmpty(draft.ObjectiveGroups);
        Assert.IsFalse(draft.ToResource().ToString()!.Contains("actor", StringComparison.OrdinalIgnoreCase));
        Assert.IsTrue(service.DiscardQuestDraft("quest"));
    }

    [TestMethod]
    public void ResourceAppearingAfterDraftCreationCausesCollisionWithoutOverwriteOrUnregister()
    {
        using var project = new TestProjectDirectory();
        var service = new ProjectService();
        service.OpenProject(project.Root);
        service.CreateStory("intro", "Intro");
        var draft = service.CreateDialogueDraftInStory("intro", "greeting", "Greeting");
        var path = Path.Combine(project.Root, "dialogues", "greeting.json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var existing = DialogueSerializer.Serialize(DialogueDocument.CreateNew("greeting", "Existing").ToResource());
        File.WriteAllText(path, existing);

        Assert.ThrowsExactly<DialogueCollisionException>(() => service.SaveDialogue(draft));
        Assert.IsTrue(draft.IsNewDraft);
        Assert.AreSame(draft, service.OpenDialogue("greeting"));
        Assert.AreEqual(existing, File.ReadAllText(path));
        Assert.IsEmpty(service.CurrentProject!.Stories.LoadStory("intro").OwnedResources.Dialogues);
    }

    [TestMethod]
    public void QuestDraftSavePersistsValidObjectiveAndOwnershipWithoutFakeActor()
    {
        using var project = new TestProjectDirectory();
        var service = new ProjectService();
        service.OpenProject(project.Root);
        service.CreateStory("intro", "Intro");
        var draft = service.CreateQuestDraftInStory("intro", "quest", "Quest");
        draft.Description = "Collect the item";
        var objective = QuestObjectiveResource.Collect("collect", "Collect item", "minecraft:stone", 0, 1);
        draft.ReplaceObjectives([objective]);
        draft.ReplaceGroups([new ObjectiveGroupResource { Id = "all", Mode = "ALL", Objectives = [objective.Id] }]);

        service.SaveQuest(draft);

        var json = File.ReadAllText(Path.Combine(project.Root, "quests", "quest.json"));
        Assert.DoesNotContain("actor_id", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"actor\"", json, StringComparison.Ordinal);
        var restarted = new ProjectService();
        restarted.OpenProject(project.Root);
        var reloaded = restarted.OpenQuest("quest");
        Assert.AreEqual("Collect item", reloaded.Objectives.Single().Description);
        CollectionAssert.Contains(restarted.CurrentProject!.Stories.LoadStory("intro").OwnedResources.Quests, "quest");
    }

    [TestMethod]
    public void DraftSaveAnchorsMutableHomeStoryToDraftOwner()
    {
        using var project = new TestProjectDirectory();
        var service = new ProjectService();
        service.OpenProject(project.Root);
        service.CreateStory("intro", "Intro");
        service.CreateStory("other", "Other");
        var draft = service.CreateDialogueDraftInStory("intro", "greeting", "Greeting");
        draft.HomeStoryId = "other";

        service.SaveDialogue(draft);

        Assert.AreEqual("intro", draft.HomeStoryId);
        CollectionAssert.Contains(service.CurrentProject!.Stories.LoadStory("intro").OwnedResources.Dialogues, "greeting");
        Assert.IsEmpty(service.CurrentProject.Stories.LoadStory("other").OwnedResources.Dialogues);
    }

    [TestMethod]
    public void DialogueDuplicateDraftDeepCopiesContentAndDefersPersistence()
    {
        using var project = new TestProjectDirectory();
        var service = new ProjectService();
        service.OpenProject(project.Root);
        service.CreateStory("source_story", "Source");
        service.CreateStory("target_story", "Target");
        var source = service.CreateDialogueInStory("source_story", "source", "Source title");
        source.SetSpeakers(["hero", "merchant"]);
        source.Entry = "line";
        source.ReplaceNodes([
            DialogueNodeResource.Line("line", "merchant", "Welcome", "choice"),
            DialogueNodeResource.Choice("choice", "Choose", [new DialogueChoiceResource { Text = "Trade", Next = "end" }]),
            DialogueNodeResource.End("end", "traded")]);
        source.SetMetadata(new DialogueMetadata { Notes = "source notes", Tags = ["shop"] });
        service.SaveDialogue(source);
        var sourcePath = Path.Combine(project.Root, "dialogues", "source.json");
        var sourceBytes = File.ReadAllBytes(sourcePath);

        var draft = service.CreateDialogueDraftFromExistingInStory("target_story", "source", "copy", "Copy title");

        Assert.AreEqual("copy", draft.Id);
        Assert.AreEqual("Copy title", draft.Title);
        Assert.AreEqual("Copy title", draft.DisplayName);
        Assert.AreEqual("target_story", draft.HomeStoryId);
        Assert.AreEqual("target_story", draft.DraftOwnerStoryId);
        Assert.AreEqual("source", draft.SourceTemplateId);
        Assert.IsTrue(draft.IsNewDraft);
        Assert.IsFalse(draft.HasEverBeenSaved);
        Assert.IsFalse(File.Exists(Path.Combine(project.Root, "dialogues", "copy.json")));
        Assert.IsEmpty(service.CurrentProject!.Stories.LoadStory("target_story").OwnedResources.Dialogues);

        draft.Speakers.Add("guard");
        draft.Metadata.Tags.Add("copy-only");
        draft.ReplaceNodes([DialogueNodeResource.End("copy-end", "copy-result")]);
        draft.Entry = "copy-end";
        Assert.HasCount(2, source.Speakers);
        Assert.DoesNotContain("copy-only", source.Metadata.Tags);
        Assert.AreEqual("line", source.Entry);
        CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(sourcePath));

        service.SaveDialogue(draft);
        Assert.IsFalse(draft.IsNewDraft);
        var restarted = new ProjectService();
        restarted.OpenProject(project.Root);
        Assert.AreEqual("Welcome", restarted.OpenDialogue("source").Nodes[0].Text);
        Assert.AreEqual("copy-end", restarted.OpenDialogue("copy").Nodes[0].Id);
    }

    [TestMethod]
    public void QuestDuplicateDraftDeepCopiesContentAndDefersPersistence()
    {
        using var project = new TestProjectDirectory();
        var service = new ProjectService();
        service.OpenProject(project.Root);
        service.CreateStory("source_story", "Source");
        service.CreateStory("target_story", "Target");
        var source = service.CreateQuestInStory("source_story", "source", "Source quest");
        var objective = QuestObjectiveResource.Interact("talk", "Talk to merchant", "merchant_actor", 2);
        source.Description = "Source description";
        source.ReplaceObjectives([objective]);
        source.ReplaceGroups([new ObjectiveGroupResource { Id = "all", Mode = "ALL", Objectives = [objective.Id] }]);
        source.Metadata = new QuestMetadata { Notes = "source notes", Tags = ["main"] };
        service.SaveQuest(source);
        var sourceBytes = File.ReadAllBytes(Path.Combine(project.Root, "quests", "source.json"));

        var draft = service.CreateQuestDraftFromExistingInStory("target_story", "source", "copy", "Copy quest");

        Assert.AreEqual("copy", draft.Id);
        Assert.AreEqual("Copy quest", draft.Title);
        Assert.AreEqual("Copy quest", draft.DisplayName);
        Assert.AreEqual("Source description", draft.Description);
        Assert.AreEqual("target_story", draft.HomeStoryId);
        Assert.AreEqual("target_story", draft.DraftOwnerStoryId);
        Assert.AreEqual("source", draft.SourceTemplateId);
        Assert.IsTrue(draft.IsNewDraft);
        Assert.IsFalse(draft.HasEverBeenSaved);
        Assert.IsFalse(File.Exists(Path.Combine(project.Root, "quests", "copy.json")));
        Assert.IsEmpty(service.CurrentProject!.Stories.LoadStory("target_story").OwnedResources.Quests);

        draft.Metadata.Tags.Add("copy-only");
        Assert.HasCount(1, source.Objectives);
        Assert.AreNotSame(source.Objectives[0], draft.Objectives[0]);
        Assert.AreNotSame(source.ObjectiveGroups[0], draft.ObjectiveGroups[0]);
        Assert.AreNotSame(source.ObjectiveGroups[0].Objectives, draft.ObjectiveGroups[0].Objectives);
        Assert.DoesNotContain("copy-only", source.Metadata.Tags);
        CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(Path.Combine(project.Root, "quests", "source.json")));

        service.SaveQuest(draft);
        var restarted = new ProjectService();
        restarted.OpenProject(project.Root);
        Assert.HasCount(1, restarted.OpenQuest("source").Objectives);
        Assert.HasCount(1, restarted.OpenQuest("copy").Objectives);
    }

    [TestMethod]
    public void ReferencesDoNotChangeHomeStoryOrCreateResourceFiles()
    {
        using var project = new TestProjectDirectory();
        var service = new ProjectService();
        service.OpenProject(project.Root);
        service.CreateStory("home", "Home");
        service.CreateStory("other", "Other");
        service.CreateDialogueInStory("home", "dialogue", "Dialogue");
        service.CreateQuestInStory("home", "quest", "Quest");

        service.AddDialogueReference("other", "dialogue");
        service.AddQuestReference("other", "quest");

        var home = service.CurrentProject!.Stories.LoadStory("home");
        var other = service.CurrentProject.Stories.LoadStory("other");
        CollectionAssert.Contains(other.ReferencedResources.Dialogues, "dialogue");
        CollectionAssert.Contains(other.ReferencedResources.Quests, "quest");
        Assert.IsEmpty(other.OwnedResources.Dialogues);
        Assert.IsEmpty(other.OwnedResources.Quests);
        Assert.AreEqual("home", service.OpenDialogue("dialogue").HomeStoryId);
        Assert.AreEqual("home", service.OpenQuest("quest").HomeStoryId);
        Assert.IsFalse(File.Exists(Path.Combine(project.Root, "dialogues", "other.json")));
        Assert.IsFalse(File.Exists(Path.Combine(project.Root, "quests", "other.json")));
        Assert.AreEqual(home.Id, service.CurrentProject.Registry.GetHomeStory(ProjectResourceType.Dialogue, "dialogue")!.Id);
        Assert.AreEqual(home.Id, service.CurrentProject.Registry.GetHomeStory(ProjectResourceType.Quest, "quest")!.Id);
    }

    [TestMethod]
    public void DialogueDuplicateDraftCollisionNeverOverwritesSourceOrClaimsStory()
    {
        using var project = new TestProjectDirectory();
        var service = new ProjectService();
        service.OpenProject(project.Root);
        service.CreateStory("home", "Home");
        service.CreateStory("target", "Target");
        service.CreateDialogueInStory("home", "source", "Source");
        var sourcePath = Path.Combine(project.Root, "dialogues", "source.json");
        var sourceBytes = File.ReadAllBytes(sourcePath);
        var draft = service.CreateDialogueDraftFromExistingInStory("target", "source", "copy", "Copy");
        var copyPath = Path.Combine(project.Root, "dialogues", "copy.json");
        File.WriteAllText(copyPath, DialogueSerializer.Serialize(DialogueDocument.CreateNew("copy", "Racer").ToResource()));

        Assert.ThrowsExactly<DialogueCollisionException>(() => service.SaveDialogue(draft));
        Assert.IsTrue(draft.IsNewDraft);
        CollectionAssert.AreEqual(sourceBytes, File.ReadAllBytes(sourcePath));
        Assert.IsEmpty(service.CurrentProject!.Stories.LoadStory("target").OwnedResources.Dialogues);
    }

    private sealed class SelectiveFailingWriter : IAtomicFileWriter
    {
        private readonly AtomicFileWriter _inner = new();
        public bool FailResourceWrites { get; set; }
        public bool FailStoryWrites { get; set; }

        public void Write(string destinationPath, string contents, Action<string>? validateTemporaryFile = null)
        {
            var normalized = destinationPath.Replace('\\', '/');
            if ((FailResourceWrites && (normalized.Contains("/dialogues/", StringComparison.Ordinal) || normalized.Contains("/quests/", StringComparison.Ordinal)))
                || (FailStoryWrites && normalized.Contains("/stories/", StringComparison.Ordinal)))
                throw new IOException("injected write failure");
            _inner.Write(destinationPath, contents, validateTemporaryFile);
        }
    }
}
