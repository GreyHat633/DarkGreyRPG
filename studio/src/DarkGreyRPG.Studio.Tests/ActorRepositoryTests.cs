using DarkGreyRPG.Studio.Core.Actors;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class ActorRepositoryTests
{
    [TestMethod]
    public void CreateAndSavePersistsActorAndClearsDirtyState()
    {
        using var project = new TestProjectDirectory();
        var repository = new ActorRepository(project.Root);
        var document = repository.CreateActor("teacher", "老师");
        document.Notes = "学校中的任务 NPC";
        document.SetTags(["school", "quest"]);

        repository.SaveActor(document);

        Assert.IsFalse(document.IsNew);
        Assert.IsFalse(document.IsDirty);
        Assert.AreEqual(project.ActorPath("teacher"), document.SourcePath);
        var loaded = repository.LoadActor("teacher");
        Assert.AreEqual("老师", loaded.DisplayName);
        Assert.AreEqual("学校中的任务 NPC", loaded.Notes);
        CollectionAssert.AreEqual(new[] { "school", "quest" }, loaded.Tags.ToArray());
    }

    [TestMethod]
    public void DuplicateIdCannotOverwriteExistingActor()
    {
        using var project = new TestProjectDirectory();
        var repository = new ActorRepository(project.Root);
        repository.SaveActor(repository.CreateActor("teacher", "Teacher"));
        var duplicate = ActorDocument.CreateNew("teacher", "Other Teacher");

        Assert.ThrowsExactly<ActorCollisionException>(() => repository.SaveActor(duplicate));
        Assert.AreEqual("Teacher", repository.LoadActor("teacher").DisplayName);
        Assert.IsTrue(duplicate.IsDirty);
    }

    [TestMethod]
    public void InvalidSavePreservesExistingFileAndDirtyDocument()
    {
        using var project = new TestProjectDirectory();
        var repository = new ActorRepository(project.Root);
        var document = repository.SaveActor(repository.CreateActor("teacher", "Teacher"));
        var before = File.ReadAllText(project.ActorPath("teacher"));
        document.DisplayName = " ";

        Assert.ThrowsExactly<ActorValidationException>(() => repository.SaveActor(document));

        Assert.AreEqual(before, File.ReadAllText(project.ActorPath("teacher")));
        Assert.IsTrue(document.IsDirty);
    }

    [TestMethod]
    public void ChangingSavedIdRequiresExplicitRename()
    {
        using var project = new TestProjectDirectory();
        var repository = new ActorRepository(project.Root);
        var document = repository.SaveActor(repository.CreateActor("teacher", "Teacher"));
        document.Id = "school_teacher";

        var exception = Assert.ThrowsExactly<ActorRepositoryException>(() => repository.SaveActor(document));

        StringAssert.Contains(exception.Message, "RenameActor");
        Assert.IsTrue(File.Exists(project.ActorPath("teacher")));
        Assert.IsFalse(File.Exists(project.ActorPath("school_teacher")));
    }

    [TestMethod]
    public void DuplicateCopiesFieldsAndAllocatesUniqueIds()
    {
        using var project = new TestProjectDirectory();
        var repository = new ActorRepository(project.Root);
        var source = repository.CreateActor("teacher", "Teacher");
        source.Notes = "Notes";
        source.SetTags(["school"]);
        repository.SaveActor(source);

        var first = repository.DuplicateActor("teacher");
        var second = repository.DuplicateActor("teacher");

        Assert.AreEqual("teacher_copy", first.Id);
        Assert.AreEqual("teacher_copy_2", second.Id);
        Assert.AreEqual(source.DisplayName, first.DisplayName);
        Assert.AreEqual(source.Notes, first.Notes);
        CollectionAssert.AreEqual(source.Tags.ToArray(), first.Tags.ToArray());
        Assert.IsTrue(File.Exists(project.ActorPath("teacher_copy")));
        Assert.IsTrue(File.Exists(project.ActorPath("teacher_copy_2")));
    }

    [TestMethod]
    public void DeleteRemovesActorFromDiskAndList()
    {
        using var project = new TestProjectDirectory();
        var repository = new ActorRepository(project.Root);
        repository.SaveActor(repository.CreateActor("teacher", "Teacher"));

        repository.DeleteActor("teacher");

        Assert.IsFalse(File.Exists(project.ActorPath("teacher")));
        Assert.AreEqual(0, repository.ListActors().Count);
        Assert.ThrowsExactly<ActorNotFoundException>(() => repository.DeleteActor("teacher"));
    }

    [TestMethod]
    public void RenameUpdatesIdAndFileWithoutLeavingOldFile()
    {
        using var project = new TestProjectDirectory();
        var repository = new ActorRepository(project.Root);
        repository.SaveActor(repository.CreateActor("teacher", "Teacher"));

        var renamed = repository.RenameActor("teacher", "school_teacher");

        Assert.AreEqual("school_teacher", renamed.Id);
        Assert.IsFalse(renamed.IsDirty);
        Assert.IsFalse(File.Exists(project.ActorPath("teacher")));
        Assert.IsTrue(File.Exists(project.ActorPath("school_teacher")));
        Assert.AreEqual("school_teacher", repository.LoadActor("school_teacher").Id);
        Assert.AreEqual(0, Directory.GetFiles(project.Actors, "*.rename.*").Length);
    }

    [TestMethod]
    public void RenameCollisionPreservesBothExistingActors()
    {
        using var project = new TestProjectDirectory();
        var repository = new ActorRepository(project.Root);
        repository.SaveActor(repository.CreateActor("teacher", "Teacher"));
        repository.SaveActor(repository.CreateActor("guard", "Guard"));

        Assert.ThrowsExactly<ActorCollisionException>(() => repository.RenameActor("teacher", "guard"));

        Assert.AreEqual("Teacher", repository.LoadActor("teacher").DisplayName);
        Assert.AreEqual("Guard", repository.LoadActor("guard").DisplayName);
    }

    [TestMethod]
    public void ListActorsIsSortedAndContainsPersistedMetadata()
    {
        using var project = new TestProjectDirectory();
        var repository = new ActorRepository(project.Root);
        repository.SaveActor(repository.CreateActor("zeta", "Zeta"));
        repository.SaveActor(repository.CreateActor("alpha", "Alpha"));

        var actors = repository.ListActors();

        CollectionAssert.AreEqual(new[] { "alpha", "zeta" }, actors.Select(actor => actor.Id).ToArray());
        Assert.AreEqual("Alpha", actors[0].DisplayName);
    }
}
