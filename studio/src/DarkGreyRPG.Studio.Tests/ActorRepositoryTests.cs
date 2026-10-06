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
        var document = repository.CreateActor("ST-2345-6789-ABCD-EFGH~actor~teacher", "老师");

        document.SetTags(["school", "quest"]);

        repository.SaveActor(document);

        Assert.IsFalse(document.IsNew);
        Assert.IsFalse(document.IsDirty);
        Assert.AreEqual(repository.GetActorPath("ST-2345-6789-ABCD-EFGH~actor~teacher"), document.SourcePath);
        var loaded = repository.LoadActor("ST-2345-6789-ABCD-EFGH~actor~teacher");
        Assert.AreEqual("老师", loaded.DisplayName);
        CollectionAssert.AreEqual(new[] { "school", "quest" }, loaded.Tags.ToArray());
    }

    [TestMethod]
    public void DuplicateIdCannotOverwriteExistingActor()
    {
        using var project = new TestProjectDirectory();
        var repository = new ActorRepository(project.Root);
        repository.SaveActor(repository.CreateActor("ST-2345-6789-ABCD-EFGH~actor~teacher", "Teacher"));
        var duplicate = ActorDocument.CreateNew("ST-2345-6789-ABCD-EFGH~actor~teacher", "Other Teacher");

        Assert.ThrowsExactly<ActorCollisionException>(() => repository.SaveActor(duplicate));
        Assert.AreEqual("Teacher", repository.LoadActor("ST-2345-6789-ABCD-EFGH~actor~teacher").DisplayName);
        Assert.IsTrue(duplicate.IsDirty);
    }

    [TestMethod]
    public void InvalidSavePreservesExistingFileAndDirtyDocument()
    {
        using var project = new TestProjectDirectory();
        var repository = new ActorRepository(project.Root);
        var document = repository.SaveActor(repository.CreateActor("ST-2345-6789-ABCD-EFGH~actor~teacher", "Teacher"));
        var before = File.ReadAllText(repository.GetActorPath("ST-2345-6789-ABCD-EFGH~actor~teacher"));
        document.DisplayName = " ";

        Assert.ThrowsExactly<ActorValidationException>(() => repository.SaveActor(document));

        Assert.AreEqual(before, File.ReadAllText(repository.GetActorPath("ST-2345-6789-ABCD-EFGH~actor~teacher")));
        Assert.IsTrue(document.IsDirty);
    }

    [TestMethod]
    public void ChangingSavedIdIsRejected()
    {
        using var project = new TestProjectDirectory();
        var repository = new ActorRepository(project.Root);
        var document = repository.SaveActor(repository.CreateActor("ST-2345-6789-ABCD-EFGH~actor~teacher", "Teacher"));
        document.Id = "ST-2345-6789-ABCD-EFGH~actor~school_teacher";

        var exception = Assert.ThrowsExactly<ActorRepositoryException>(() => repository.SaveActor(document));

        StringAssert.Contains(exception.Message, "immutable");
        Assert.IsTrue(File.Exists(repository.GetActorPath("ST-2345-6789-ABCD-EFGH~actor~teacher")));
        Assert.IsFalse(File.Exists(repository.GetActorPath("ST-2345-6789-ABCD-EFGH~actor~school_teacher")));
    }

    [TestMethod]
    public void DuplicateCopiesFieldsAndAllocatesUniqueIds()
    {
        using var project = new TestProjectDirectory();
        var repository = new ActorRepository(project.Root);
        var source = repository.CreateActor("ST-2345-6789-ABCD-EFGH~actor~teacher", "Teacher");

        source.SetTags(["school"]);
        repository.SaveActor(source);

        var first = repository.DuplicateActor("ST-2345-6789-ABCD-EFGH~actor~teacher");
        var second = repository.DuplicateActor("ST-2345-6789-ABCD-EFGH~actor~teacher");

        Assert.AreNotEqual(source.Id, first.Id);
        Assert.AreNotEqual(first.Id, second.Id);
        Assert.AreEqual(source.HomeStoryId, first.HomeStoryId);
        Assert.AreEqual(source.DisplayName, first.DisplayName);
        CollectionAssert.AreEqual(source.Tags.ToArray(), first.Tags.ToArray());
        Assert.IsTrue(File.Exists(repository.GetActorPath(first.Id)));
        Assert.IsTrue(File.Exists(repository.GetActorPath(second.Id)));
    }

    [TestMethod]
    public void DeleteRemovesActorFromDiskAndList()
    {
        using var project = new TestProjectDirectory();
        var repository = new ActorRepository(project.Root);
        repository.SaveActor(repository.CreateActor("ST-2345-6789-ABCD-EFGH~actor~teacher", "Teacher"));

        repository.DeleteActor("ST-2345-6789-ABCD-EFGH~actor~teacher");

        Assert.IsFalse(File.Exists(repository.GetActorPath("ST-2345-6789-ABCD-EFGH~actor~teacher")));
        Assert.AreEqual(0, repository.ListActors().Count);
        Assert.ThrowsExactly<ActorNotFoundException>(() => repository.DeleteActor("ST-2345-6789-ABCD-EFGH~actor~teacher"));
    }

    [TestMethod]
    public void PersistedIdentityRenameIsRejectedWithoutChangingTheActor()
    {
        using var project = new TestProjectDirectory();
        var repository = new ActorRepository(project.Root);
        const string source = "ST-2345-6789-ABCD-EFGH~actor~teacher";
        const string target = "ST-2345-6789-ABCD-EFGH~actor~school_teacher";
        repository.SaveActor(repository.CreateActor(source, "Teacher"));
        var before = File.ReadAllBytes(repository.GetActorPath(source));
        Assert.ThrowsExactly<ActorRepositoryException>(() => repository.RenameActor(source, target));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(repository.GetActorPath(source)));
        Assert.IsFalse(File.Exists(repository.GetActorPath(target)));
    }

    [TestMethod]
    public void RenameCollisionPreservesBothExistingActors()
    {
        using var project = new TestProjectDirectory();
        var repository = new ActorRepository(project.Root);
        repository.SaveActor(repository.CreateActor("ST-2345-6789-ABCD-EFGH~actor~teacher", "Teacher"));
        repository.SaveActor(repository.CreateActor("ST-2345-6789-ABCD-EFGH~actor~guard", "Guard"));

        Assert.ThrowsExactly<ActorRepositoryException>(() => repository.RenameActor("ST-2345-6789-ABCD-EFGH~actor~teacher", "ST-2345-6789-ABCD-EFGH~actor~guard"));

        Assert.AreEqual("Teacher", repository.LoadActor("ST-2345-6789-ABCD-EFGH~actor~teacher").DisplayName);
        Assert.AreEqual("Guard", repository.LoadActor("ST-2345-6789-ABCD-EFGH~actor~guard").DisplayName);
    }

    [TestMethod]
    public void ListActorsIsSortedAndContainsPersistedMetadata()
    {
        using var project = new TestProjectDirectory();
        var repository = new ActorRepository(project.Root);
        repository.SaveActor(repository.CreateActor("ST-2345-6789-ABCD-EFGH~actor~zeta", "Zeta"));
        repository.SaveActor(repository.CreateActor("ST-2345-6789-ABCD-EFGH~actor~alpha", "Alpha"));

        var actors = repository.ListActors();

        CollectionAssert.AreEqual(new[] { "ST-2345-6789-ABCD-EFGH~actor~alpha", "ST-2345-6789-ABCD-EFGH~actor~zeta" }, actors.Select(actor => actor.Id).ToArray());
        Assert.AreEqual("Alpha", actors[0].DisplayName);
    }
}
