using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class ProjectServiceTests
{
    [TestMethod]
    public void ActorSaveRollsBackBothFilesAndKeepsDirtyDocumentOnMembershipWriteFailure()
    {
        using var directory = new TestProjectDirectory();
        const string owner = "ST-2345-6789-ABCD-EFGH";
        var store = new CanonicalProjectGraphStore(directory.Root);
        new CanonicalStoryLifecycleService(store).Create(owner, "Owner");
        var setup = new ProjectService();
        setup.OpenProject(directory.Root);
        setup.SaveActor(setup.CreateActor(owner + "~actor~hero", "Original"));
        var actorPath = new ActorRepository(directory.Root).GetActorPath(owner + "~actor~hero");
        var memberPath = store.Memberships.GetPath(owner);
        var actorBefore = File.ReadAllBytes(actorPath);
        var memberBefore = File.ReadAllBytes(memberPath);
        var writer = new FailSecondWrite();
        var service = new ProjectService(writer);
        service.OpenProject(directory.Root);
        var actor = service.OpenActor(owner + "~actor~hero");
        actor.DisplayName = "Unsaved edit";
        Assert.ThrowsExactly<IOException>(() => service.SaveActor(actor));
        Assert.IsTrue(actor.IsDirty);
        CollectionAssert.AreEqual(actorBefore, File.ReadAllBytes(actorPath));
        CollectionAssert.AreEqual(memberBefore, File.ReadAllBytes(memberPath));
    }

    private sealed class FailSecondWrite : Core.IO.IAtomicFileWriter
    {
        private int _writes;
        public void Write(string path, string contents, Action<string>? validateTemporaryFile = null)
        {
            if (++_writes == 2) throw new IOException("Injected membership write failure");
            new Core.IO.AtomicFileWriter().Write(path, contents, validateTemporaryFile);
        }
    }

    [TestMethod]
    public void CreateProjectBuildsRuntimeCompatibleLayout()
    {
        using var directory = new TestProjectDirectory(createProjectFile: false);
        Directory.Delete(Path.Combine(directory.Root, "actors"));
        var service = new ProjectService();

        var session = service.CreateProject(directory.Root, "school_rpg", "学校 RPG");

        Assert.AreEqual("school_rpg", session.Project.Id);
        foreach (var name in new[] { "actors", "resources" })
        {
            Assert.IsTrue(Directory.Exists(Path.Combine(directory.Root, name)), name);
        }

        StringAssert.Contains(File.ReadAllText(Path.Combine(directory.Root, "project.json")), "\"display_name\": \"学校 RPG\"");
        foreach (var retired in new[] { "stories", "dialogues", "quests" })
            Assert.IsFalse(Directory.Exists(Path.Combine(directory.Root, retired)));

        service.CloseProject();
        var reopened = new ProjectService().OpenProject(directory.Root);
        Assert.IsEmpty(new CanonicalProjectGraphStore(reopened.ProjectDirectory).Stories.List());
    }

    [TestMethod]
    public void CreateStoryIsExplicitAndAllocatesStableIds()
    {
        using var directory = new TestProjectDirectory(createProjectFile: false);
        var service = new ProjectService();
        var session = service.CreateProject(directory.Root, "school_rpg", "学校 RPG");

        var lifecycle = new CanonicalStoryLifecycleService(new CanonicalProjectGraphStore(directory.Root));
        var created = lifecycle.CreateNew("开场");
        Assert.IsTrue(StoryUid.IsValid(created.Id));
        var second = lifecycle.CreateNew("第二章");
        Assert.AreNotEqual(created.Id, second.Id);
        Assert.AreEqual(created.Id, new CanonicalProjectGraphStore(directory.Root).Stories.Load(created.Id).Id);
        Assert.ThrowsExactly<CanonicalStoryLifecycleException>(() => lifecycle.Create(created.Id, "重复"));
    }

    [TestMethod]
    public void OpenProjectLoadsExistingActorAndSaveAllPersistsChanges()
    {
        using var directory = new TestProjectDirectory();
        new CanonicalStoryLifecycleService(new CanonicalProjectGraphStore(directory.Root)).Create("ST-2345-6789-ABCD-EFGH", "Owner");
        var repository = new Core.Actors.ActorRepository(directory.Root);
        repository.SaveActor(repository.CreateActor("ST-2345-6789-ABCD-EFGH~actor~teacher", "Teacher"));
        var service = new ProjectService();
        service.OpenProject(directory.Root);
        var document = service.OpenActor("ST-2345-6789-ABCD-EFGH~actor~teacher");
        document.DisplayName = "老师";

        service.SaveAll();

        Assert.IsFalse(document.IsDirty);
        Assert.AreEqual("老师", repository.LoadActor("ST-2345-6789-ABCD-EFGH~actor~teacher").DisplayName);
    }

    [TestMethod]
    public void SaveActorPersistsOnlySelectedDocumentAcrossServiceRestart()
    {
        using var directory = new TestProjectDirectory();
        new CanonicalStoryLifecycleService(new CanonicalProjectGraphStore(directory.Root)).Create("ST-2345-6789-ABCD-EFGH", "Owner");
        var repository = new Core.Actors.ActorRepository(directory.Root);
        repository.SaveActor(repository.CreateActor("ST-2345-6789-ABCD-EFGH~actor~teacher", "Teacher"));
        repository.SaveActor(repository.CreateActor("ST-2345-6789-ABCD-EFGH~actor~blacksmith", "Blacksmith"));

        var service = new ProjectService();
        service.OpenProject(directory.Root);
        var teacher = service.OpenActor("ST-2345-6789-ABCD-EFGH~actor~teacher");
        var blacksmith = service.OpenActor("ST-2345-6789-ABCD-EFGH~actor~blacksmith");
        teacher.DisplayName = "老师";
        teacher.SetTags(["school", "quest"]);
        blacksmith.DisplayName = "铁匠（未保存）";

        service.SaveActor(teacher);

        Assert.IsFalse(teacher.IsDirty);
        Assert.IsTrue(blacksmith.IsDirty);

        var restartedService = new ProjectService();
        restartedService.OpenProject(directory.Root);
        var reloadedTeacher = restartedService.OpenActor("ST-2345-6789-ABCD-EFGH~actor~teacher");
        var reloadedBlacksmith = restartedService.OpenActor("ST-2345-6789-ABCD-EFGH~actor~blacksmith");

        Assert.AreEqual("老师", reloadedTeacher.DisplayName);
        CollectionAssert.AreEqual(new[] { "school", "quest" }, reloadedTeacher.Tags.ToArray());
        Assert.AreEqual("Blacksmith", reloadedBlacksmith.DisplayName);
    }

    [TestMethod]
    public void CloseAndReloadRefuseToDiscardDirtyDocumentsByDefault()
    {
        using var directory = new TestProjectDirectory();
        new CanonicalStoryLifecycleService(new CanonicalProjectGraphStore(directory.Root)).Create("ST-2345-6789-ABCD-EFGH", "Owner");
        var repository = new Core.Actors.ActorRepository(directory.Root);
        repository.SaveActor(repository.CreateActor("ST-2345-6789-ABCD-EFGH~actor~teacher", "Teacher"));
        var service = new ProjectService();
        service.OpenProject(directory.Root);
        service.OpenActor("ST-2345-6789-ABCD-EFGH~actor~teacher").DisplayName = "Unsaved";

        Assert.ThrowsExactly<ProjectException>(() => service.CloseProject());
        Assert.ThrowsExactly<ProjectException>(() => service.ReloadProject());

        service.ReloadProject(discardUnsavedChanges: true);
        Assert.IsNotNull(service.CurrentProject);
    }

    [TestMethod]
    public void ValidateProjectReportsMissingFutureDirectoriesAsWarnings()
    {
        using var directory = new TestProjectDirectory();
        var service = new ProjectService();
        service.OpenProject(directory.Root);

        var issues = service.ValidateProject();

        Assert.IsTrue(issues.Any(issue => issue.Code == "project.directory.resources.missing"));
        Assert.IsFalse(issues.Any(issue => issue.Code == "project.directory.actors.missing"));
    }

    [TestMethod]
    public void ActorCrudKeepsOpenDocumentsAndDiskInSync()
    {
        using var directory = new TestProjectDirectory();
        new CanonicalStoryLifecycleService(new CanonicalProjectGraphStore(directory.Root)).Create("ST-2345-6789-ABCD-EFGH", "Owner");
        var service = new ProjectService();
        service.OpenProject(directory.Root);

        var created = service.CreateActor("ST-2345-6789-ABCD-EFGH~actor~teacher", "Teacher");
        Assert.IsTrue(created.IsDirty);
        service.SaveActor(created);

        var duplicate = service.DuplicateActor("ST-2345-6789-ABCD-EFGH~actor~teacher");
        Assert.AreNotEqual(created.Id, duplicate.Id);
        Assert.AreEqual(ResourceAddress.FromKey(created.Id).StoryUid, ResourceAddress.FromKey(duplicate.Id).StoryUid);
        Assert.IsFalse(duplicate.IsDirty);
        Assert.AreSame(duplicate, service.OpenActor(duplicate.Id));
        var duplicatePath = new ActorRepository(directory.Root).GetActorPath(duplicate.Id);
        Assert.IsTrue(File.Exists(duplicatePath));
        Assert.IsTrue(File.Exists(duplicatePath));
        Assert.AreSame(duplicate, service.OpenActor(duplicate.Id));
        service.DeleteActor(duplicate.Id);
        Assert.IsFalse(File.Exists(duplicatePath));
        Assert.ThrowsExactly<ActorNotFoundException>(() => service.OpenActor(duplicate.Id));
        Assert.AreEqual(1, service.OpenActorDocuments.Count);
    }

    [TestMethod]
    public void ReleaseOpenActorDropsCleanCacheAndRejectsDirtyDocuments()
    {
        using var directory = new TestProjectDirectory();
        new CanonicalStoryLifecycleService(new CanonicalProjectGraphStore(directory.Root)).Create("ST-2345-6789-ABCD-EFGH", "Owner");
        var service = new ProjectService();
        service.OpenProject(directory.Root);
        var created = service.CreateActor("ST-2345-6789-ABCD-EFGH~actor~teacher", "Teacher");
        service.SaveActor(created);

        service.ReleaseOpenActor("ST-2345-6789-ABCD-EFGH~actor~teacher");

        Assert.IsEmpty(service.OpenActorDocuments);
        Assert.IsTrue(File.Exists(new ActorRepository(directory.Root).GetActorPath("ST-2345-6789-ABCD-EFGH~actor~teacher")));
        var reopened = service.OpenActor("ST-2345-6789-ABCD-EFGH~actor~teacher");
        reopened.DisplayName = "Unsaved";
        Assert.ThrowsExactly<ProjectException>(() => service.ReleaseOpenActor("ST-2345-6789-ABCD-EFGH~actor~teacher"));
        Assert.HasCount(1, service.OpenActorDocuments);
    }

    [TestMethod]
    public void DeleteRefusesDirtyDocumentsAndPreservesOtherActors()
    {
        using var directory = new TestProjectDirectory();
        new CanonicalStoryLifecycleService(new CanonicalProjectGraphStore(directory.Root)).Create("ST-2345-6789-ABCD-EFGH", "Owner");
        var repository = new ActorRepository(directory.Root);
        repository.SaveActor(repository.CreateActor("ST-2345-6789-ABCD-EFGH~actor~teacher", "Teacher"));
        repository.SaveActor(repository.CreateActor("ST-2345-6789-ABCD-EFGH~actor~guard", "Guard"));

        var service = new ProjectService();
        service.OpenProject(directory.Root);
        var teacher = service.OpenActor("ST-2345-6789-ABCD-EFGH~actor~teacher");
        teacher.DisplayName = "Unsaved";

        var deleteException = Assert.ThrowsExactly<ProjectException>(
            () => service.DeleteActor("ST-2345-6789-ABCD-EFGH~actor~teacher"));
        StringAssert.Contains(deleteException.Message, "unsaved");
        Assert.IsTrue(File.Exists(new ActorRepository(directory.Root).GetActorPath("ST-2345-6789-ABCD-EFGH~actor~teacher")));
        Assert.AreSame(teacher, service.OpenActor("ST-2345-6789-ABCD-EFGH~actor~teacher"));

        service.SaveActor(teacher);
        Assert.IsTrue(File.Exists(new ActorRepository(directory.Root).GetActorPath("ST-2345-6789-ABCD-EFGH~actor~teacher")));
        Assert.IsTrue(File.Exists(new ActorRepository(directory.Root).GetActorPath("ST-2345-6789-ABCD-EFGH~actor~guard")));
        Assert.AreSame(teacher, service.OpenActor("ST-2345-6789-ABCD-EFGH~actor~teacher"));
    }
}
