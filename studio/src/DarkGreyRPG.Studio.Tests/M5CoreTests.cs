using DarkGreyRPG.Studio.Core.Projects;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class M5CoreTests
{

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
