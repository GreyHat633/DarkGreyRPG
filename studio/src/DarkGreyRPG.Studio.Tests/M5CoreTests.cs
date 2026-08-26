using DarkGreyRPG.Studio.Core.Dialogues;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Quests;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class M5CoreTests
{
    [TestMethod]
    public void DialogueV1RoundTripsAsV2AndRejectsUnknownFields()
    {
        const string json = "{\"schema_version\":1,\"id\":\"intro\",\"title\":\"Intro\",\"speakers\":[\"hero\"],\"entry\":\"line\",\"nodes\":[{\"id\":\"line\",\"type\":\"line\",\"speaker\":\"hero\",\"text\":\"Hi\",\"next\":\"end\"},{\"id\":\"end\",\"type\":\"end\",\"result\":\"done\"}],\"metadata\":{\"notes\":\"n\",\"tags\":[]}}";
        var resource = DialogueSerializer.Deserialize(json);
        Assert.AreEqual("uncategorized", resource.HomeStoryId);
        var v2 = DialogueSerializer.Serialize(resource);
        StringAssert.Contains(v2, "\"display_name\": \"Intro\"");
        Assert.ThrowsExactly<DialogueDataException>(() => DialogueSerializer.Deserialize(json.Replace("\"metadata\"", "\"unknown\" : 1, \"metadata\"", StringComparison.Ordinal)));
    }

    [TestMethod]
    public void QuestValidationRequiresExactGroupMembership()
    {
        var resource = new QuestResource { Id = "quest", Title = "Quest", DisplayName = "Quest", Description = "Do it", HomeStoryId = "uncategorized", Objectives = [QuestObjectiveResource.Kill("kill", "Kill", "slime", 1)], ObjectiveGroups = [] };
        var issues = QuestValidator.Validate(resource);
        Assert.IsTrue(issues.Any(i => i.Code == "quest.groups.required"));
        Assert.ThrowsExactly<QuestValidationException>(() => QuestSerializer.Serialize(resource));
    }

    [TestMethod]
    public void ProjectServiceCreatesSavesAndRestoresDialogueAndQuest()
    {
        using var directory = new TestProjectDirectory();
        var service = new ProjectService();
        service.OpenProject(directory.Root);
        var dialogue = service.CreateDialogueInStory("uncategorized", "intro", "Intro");
        dialogue.Speakers.Add("hero"); dialogue.Nodes.Add(DialogueNodeResource.Line("line", "hero", "Hello", "end")); dialogue.Entry = "line"; service.SaveDialogue(dialogue);
        var quest = service.CreateQuestInStory("uncategorized", "find", "Find"); quest.Description = "Find it"; service.SaveQuest(quest);
        var restarted = new ProjectService(); restarted.OpenProject(directory.Root);
        Assert.AreEqual("line", restarted.OpenDialogue("intro").Entry);
        Assert.AreEqual("Find it", restarted.OpenQuest("find").Description);
        Assert.IsTrue(restarted.CurrentProject!.Registry.Exists(ProjectResourceType.Dialogue, "intro"));
        Assert.IsTrue(restarted.CurrentProject.Registry.Exists(ProjectResourceType.Quest, "find"));
    }
}
