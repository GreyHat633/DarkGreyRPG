using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Stories;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class M4CoreTests
{
    [TestMethod]
    public void CreateInSelectedStoryPersistsOwnershipAcrossRestart()
    {
        using var directory = new TestProjectDirectory();
        var service = new ProjectService();
        var session = service.OpenProject(directory.Root);
        session.Stories.CreateStory("kingdom", "王国线");

        var created = service.CreateActorInStory("kingdom", "captain", "Captain");

        Assert.AreEqual("kingdom", created.HomeStoryId);
        Assert.AreEqual(2, new ActorRepository(directory.Root).LoadActor("captain").ToResource().SchemaVersion);
        CollectionAssert.Contains(session.Stories.LoadStory("kingdom").OwnedResources.Actors, "captain");

        var restarted = new ProjectService();
        restarted.OpenProject(directory.Root);
        Assert.AreEqual("kingdom", restarted.OpenActor("captain").HomeStoryId);
        CollectionAssert.Contains(restarted.CurrentProject!.Stories.LoadStory("kingdom").OwnedResources.Actors, "captain");
    }

    [TestMethod]
    public void ImportAsNewCreatesIndependentActorAndSelectedOwnership()
    {
        using var directory = new TestProjectDirectory();
        var service = new ProjectService();
        service.OpenProject(directory.Root);
        var source = service.CreateActor("source", "Source");
        source.Notes = "template notes";
        source.SetTags(["guard", "elite"]);
        service.SaveActor(source);
        service.CurrentProject!.Stories.CreateStory("capital", "Capital");

        var imported = service.ImportActorAsNew("source", "capital_guard", "capital");
        imported.DisplayName = "Capital Guard";
        service.SaveActor(imported);

        var sourceReloaded = new ActorRepository(directory.Root).LoadActor("source");
        var importedReloaded = new ActorRepository(directory.Root).LoadActor("capital_guard");
        Assert.AreEqual("Source", sourceReloaded.DisplayName);
        Assert.AreEqual("Capital Guard", importedReloaded.DisplayName);
        Assert.AreEqual("template notes", importedReloaded.Notes);
        CollectionAssert.AreEqual(sourceReloaded.Tags.ToArray(), importedReloaded.Tags.ToArray());
        Assert.AreEqual("capital", importedReloaded.HomeStoryId);
        CollectionAssert.Contains(service.CurrentProject.Stories.LoadStory("capital").OwnedResources.Actors, "capital_guard");
        CollectionAssert.DoesNotContain(service.CurrentProject.Stories.LoadStory("capital").OwnedResources.Actors, "source");
    }

    [TestMethod]
    public void ReferenceIsSharedIdempotentAndRemovalNeverDeletesActor()
    {
        using var directory = new TestProjectDirectory();
        var service = new ProjectService();
        var session = service.OpenProject(directory.Root);
        var actor = service.CreateActorInStory("uncategorized", "detective", "Detective");
        session.Stories.CreateStory("kingdom", "Kingdom");

        session.Registry.AddReference("kingdom", ProjectResourceType.Actor, actor.Id);
        session.Registry.AddReference("kingdom", ProjectResourceType.Actor, actor.Id);
        Assert.ThrowsExactly<ProjectException>(() =>
            session.Registry.AddReference("uncategorized", ProjectResourceType.Actor, actor.Id));

        session.Registry.RemoveReference("kingdom", ProjectResourceType.Actor, actor.Id);
        session.Registry.RemoveReference("kingdom", ProjectResourceType.Actor, actor.Id);
        Assert.IsTrue(File.Exists(directory.ActorPath(actor.Id)));
        CollectionAssert.DoesNotContain(session.Stories.LoadStory("kingdom").ReferencedResources.Actors, actor.Id);

        var restarted = new ProjectService();
        restarted.OpenProject(directory.Root);
        CollectionAssert.DoesNotContain(restarted.CurrentProject!.Stories.LoadStory("kingdom").ReferencedResources.Actors, actor.Id);
        Assert.IsTrue(File.Exists(directory.ActorPath(actor.Id)));
    }

    [TestMethod]
    public void RemovingReferenceCannotDetachOwnedActor()
    {
        using var directory = new TestProjectDirectory();
        var service = new ProjectService();
        var session = service.OpenProject(directory.Root);
        var actor = service.CreateActorInStory("uncategorized", "owner", "Owner");

        service.RemoveActorReference("uncategorized", actor.Id);

        CollectionAssert.Contains(
            session.Stories.LoadStory("uncategorized").OwnedResources.Actors,
            actor.Id);
        Assert.IsTrue(File.Exists(directory.ActorPath(actor.Id)));
    }

    [TestMethod]
    public void ExternalReferencesExposeDescriptorsAndBlockDelete()
    {
        using var directory = new TestProjectDirectory();
        var service = new ProjectService();
        var session = service.OpenProject(directory.Root);
        var actor = service.CreateActorInStory("uncategorized", "detective", "Detective");
        session.Stories.CreateStory("kingdom", "王国线");
        session.Registry.AddReference("kingdom", ProjectResourceType.Actor, actor.Id);

        var references = service.GetActorReferences(actor.Id);
        Assert.AreEqual(1, references.Count);
        Assert.AreEqual("kingdom", references[0].Id);
        Assert.AreEqual("王国线", references[0].DisplayName);
        Assert.IsTrue(references[0].Path.EndsWith(Path.Combine("stories", "kingdom.json"), StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(service.CanDeleteActor(actor.Id));
        Assert.ThrowsExactly<ProjectException>(() => service.DeleteActor(actor.Id));
        Assert.IsTrue(File.Exists(directory.ActorPath(actor.Id)));

        session.Registry.RemoveReference("kingdom", ProjectResourceType.Actor, actor.Id);
        Assert.IsTrue(service.CanDeleteActor(actor.Id));
        service.DeleteActor(actor.Id);
        Assert.IsFalse(File.Exists(directory.ActorPath(actor.Id)));
    }

    [TestMethod]
    public void MissingSelectedStoryIsRejectedBeforeActorMutation()
    {
        using var directory = new TestProjectDirectory();
        var service = new ProjectService();
        service.OpenProject(directory.Root);

        Assert.ThrowsExactly<StoryNotFoundException>(() => service.CreateActorInStory("missing", "captain", "Captain"));
        Assert.IsFalse(File.Exists(directory.ActorPath("captain")));
    }
}
