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
    public void CurrentSessionAndTaskSaveAndRestoreInProject()
    {
        using var directory = new TestProjectDirectory();
        const string uid = "ST-2345-6789-ABCD-EFGH";
        var store = new Core.Graphs.Resources.CanonicalProjectGraphStore(directory.Root);
        new Core.Graphs.Resources.CanonicalStoryLifecycleService(store).Create(uid, "Story");
        var lifecycle = new Core.Graphs.Resources.CanonicalStoryResourceLifecycleService(store);
        lifecycle.CreateOwnedSession(uid, uid + "~session~intro", "Intro");
        lifecycle.CreateOwnedTask(uid, uid + "~task~find", "Find");
        var session = store.Sessions.Load(uid + "~session~intro"); session.DisplayName = "Saved Session"; store.Sessions.Replace(session);
        var task = store.Tasks.Load(uid + "~task~find"); task.DisplayName = "Saved Task"; store.Tasks.Replace(task);
        new ProjectService().OpenProject(directory.Root);
        var reopened = new Core.Graphs.Resources.CanonicalProjectGraphStore(directory.Root);
        Assert.AreEqual("Saved Session", reopened.Sessions.Load(session.Id).DisplayName);
        Assert.AreEqual("Saved Task", reopened.Tasks.Load(task.Id).DisplayName);
        CollectionAssert.Contains(reopened.Memberships.Load(uid).OwnedResources.Sessions, session.Id);
        CollectionAssert.Contains(reopened.Memberships.Load(uid).OwnedResources.Tasks, task.Id);
    }
}
