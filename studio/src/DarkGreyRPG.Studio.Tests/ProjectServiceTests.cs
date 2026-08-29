using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Stories;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class ProjectServiceTests
{
    [TestMethod]
    public void CreateProjectBuildsRuntimeCompatibleLayout()
    {
        using var directory = new TestProjectDirectory(createProjectFile: false);
        Directory.Delete(Path.Combine(directory.Root, "actors"));
        var service = new ProjectService();

        var session = service.CreateProject(directory.Root, "school_rpg", "学校 RPG");

        Assert.AreEqual("school_rpg", session.Project.Id);
        foreach (var name in new[] { "actors", "dialogues", "quests", "stories", "resources" })
        {
            Assert.IsTrue(Directory.Exists(Path.Combine(directory.Root, name)), name);
        }

        StringAssert.Contains(File.ReadAllText(Path.Combine(directory.Root, "project.json")), "\"display_name\": \"学校 RPG\"");
        Assert.IsEmpty(Directory.EnumerateFiles(Path.Combine(directory.Root, "stories"), "*.json"));

        service.CloseProject();
        var reopened = new ProjectService().OpenProject(directory.Root);
        Assert.IsEmpty(reopened.Stories.ListStories());
    }

    [TestMethod]
    public void CreateStoryIsExplicitAndAllocatesStableIds()
    {
        using var directory = new TestProjectDirectory(createProjectFile: false);
        var service = new ProjectService();
        var session = service.CreateProject(directory.Root, "school_rpg", "学校 RPG");

        var created = service.CreateStory("intro", "开场");

        Assert.AreEqual("intro", created.Id);
        Assert.AreEqual("end", created.Entry);
        Assert.AreEqual("END", created.Nodes.Single().Type);
        Assert.IsEmpty(StoryValidator.Validate(created));
        Assert.AreEqual("intro_2", session.Stories.GetAvailableId("intro"));
        session.Stories.CreateStory("intro_2", "第二章");
        Assert.AreEqual("intro_3", session.Stories.GetAvailableId("intro"));
        Assert.ThrowsExactly<StoryRepositoryException>(() => service.CreateStory("intro", "重复"));
    }

    [TestMethod]
    public void OpenProjectLoadsExistingActorAndSaveAllPersistsChanges()
    {
        using var directory = new TestProjectDirectory();
        var repository = new Core.Actors.ActorRepository(directory.Root);
        repository.SaveActor(repository.CreateActor("teacher", "Teacher"));
        var service = new ProjectService();
        service.OpenProject(directory.Root);
        var document = service.OpenActor("teacher");
        document.DisplayName = "老师";

        service.SaveAll();

        Assert.IsFalse(document.IsDirty);
        Assert.AreEqual("老师", repository.LoadActor("teacher").DisplayName);
    }

    [TestMethod]
    public void SaveActorPersistsOnlySelectedDocumentAcrossServiceRestart()
    {
        using var directory = new TestProjectDirectory();
        var repository = new Core.Actors.ActorRepository(directory.Root);
        repository.SaveActor(repository.CreateActor("teacher", "Teacher"));
        repository.SaveActor(repository.CreateActor("blacksmith", "Blacksmith"));

        var service = new ProjectService();
        service.OpenProject(directory.Root);
        var teacher = service.OpenActor("teacher");
        var blacksmith = service.OpenActor("blacksmith");
        teacher.DisplayName = "老师";
        teacher.Notes = "学校中的任务 NPC";
        teacher.SetTags(["school", "quest"]);
        blacksmith.DisplayName = "铁匠（未保存）";

        service.SaveActor(teacher);

        Assert.IsFalse(teacher.IsDirty);
        Assert.IsTrue(blacksmith.IsDirty);

        var restartedService = new ProjectService();
        restartedService.OpenProject(directory.Root);
        var reloadedTeacher = restartedService.OpenActor("teacher");
        var reloadedBlacksmith = restartedService.OpenActor("blacksmith");

        Assert.AreEqual("老师", reloadedTeacher.DisplayName);
        Assert.AreEqual("学校中的任务 NPC", reloadedTeacher.Notes);
        CollectionAssert.AreEqual(new[] { "school", "quest" }, reloadedTeacher.Tags.ToArray());
        Assert.AreEqual("Blacksmith", reloadedBlacksmith.DisplayName);
    }

    [TestMethod]
    public void CloseAndReloadRefuseToDiscardDirtyDocumentsByDefault()
    {
        using var directory = new TestProjectDirectory();
        var repository = new Core.Actors.ActorRepository(directory.Root);
        repository.SaveActor(repository.CreateActor("teacher", "Teacher"));
        var service = new ProjectService();
        service.OpenProject(directory.Root);
        service.OpenActor("teacher").Notes = "Unsaved";

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

        Assert.IsTrue(issues.Any(issue => issue.Code == "project.directory.dialogues.missing"));
        Assert.IsFalse(issues.Any(issue => issue.Code == "project.directory.actors.missing"));
    }

    [TestMethod]
    public void ActorCrudKeepsOpenDocumentsAndDiskInSync()
    {
        using var directory = new TestProjectDirectory();
        var service = new ProjectService();
        service.OpenProject(directory.Root);

        var created = service.CreateActor("teacher", "Teacher");
        Assert.IsTrue(created.IsDirty);
        service.SaveActor(created);

        var duplicate = service.DuplicateActor("teacher");
        Assert.AreEqual("teacher_copy", duplicate.Id);
        Assert.IsFalse(duplicate.IsDirty);
        Assert.AreSame(duplicate, service.OpenActor("teacher_copy"));
        Assert.IsTrue(File.Exists(directory.ActorPath("teacher_copy")));

        var renamed = service.RenameActor("teacher_copy", "mentor");
        Assert.AreEqual("mentor", renamed.Id);
        Assert.IsFalse(File.Exists(directory.ActorPath("teacher_copy")));
        Assert.IsTrue(File.Exists(directory.ActorPath("mentor")));
        Assert.AreSame(renamed, service.OpenActor("mentor"));

        service.DeleteActor("mentor");
        Assert.IsFalse(File.Exists(directory.ActorPath("mentor")));
        Assert.ThrowsExactly<ActorNotFoundException>(() => service.OpenActor("mentor"));
        Assert.AreEqual(1, service.OpenActorDocuments.Count);
    }

    [TestMethod]
    public void ReleaseOpenActorDropsCleanCacheAndRejectsDirtyDocuments()
    {
        using var directory = new TestProjectDirectory();
        var service = new ProjectService();
        service.OpenProject(directory.Root);
        var created = service.CreateActor("teacher", "Teacher");
        service.SaveActor(created);

        service.ReleaseOpenActor("teacher");

        Assert.IsEmpty(service.OpenActorDocuments);
        Assert.IsTrue(File.Exists(directory.ActorPath("teacher")));
        var reopened = service.OpenActor("teacher");
        reopened.Notes = "Unsaved";
        Assert.ThrowsExactly<ProjectException>(() => service.ReleaseOpenActor("teacher"));
        Assert.HasCount(1, service.OpenActorDocuments);
    }

    [TestMethod]
    public void RenameAndDeleteRefuseDirtyDocumentsAndCollisions()
    {
        using var directory = new TestProjectDirectory();
        var repository = new ActorRepository(directory.Root);
        repository.SaveActor(repository.CreateActor("teacher", "Teacher"));
        repository.SaveActor(repository.CreateActor("guard", "Guard"));

        var service = new ProjectService();
        service.OpenProject(directory.Root);
        var teacher = service.OpenActor("teacher");
        teacher.Notes = "Unsaved";

        var renameException = Assert.ThrowsExactly<ProjectException>(
            () => service.RenameActor("teacher", "mentor"));
        StringAssert.Contains(renameException.Message, "unsaved");
        var deleteException = Assert.ThrowsExactly<ProjectException>(
            () => service.DeleteActor("teacher"));
        StringAssert.Contains(deleteException.Message, "unsaved");
        Assert.IsTrue(File.Exists(directory.ActorPath("teacher")));
        Assert.AreSame(teacher, service.OpenActor("teacher"));

        service.SaveActor(teacher);
        var collisionException = Assert.ThrowsExactly<ActorCollisionException>(
            () => service.RenameActor("teacher", "guard"));
        StringAssert.Contains(collisionException.Message, "already exists");
        Assert.IsTrue(File.Exists(directory.ActorPath("teacher")));
        Assert.IsTrue(File.Exists(directory.ActorPath("guard")));
        Assert.AreSame(teacher, service.OpenActor("teacher"));
    }
}
