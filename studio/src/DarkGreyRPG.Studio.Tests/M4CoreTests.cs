using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class M4CoreTests
{
    [TestMethod]
    public void CreateInSelectedStoryPersistsOwnershipAcrossRestart()
    {
        using var directory = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(directory.Root);
        new CanonicalStoryLifecycleService(store).Create("ST-2345-6789-ABCD-EFGH", "Owner");
        var service = new ProjectService();
        var session = service.OpenProject(directory.Root);
        new CanonicalStoryLifecycleService(store).Create("ST-JKLM-NPQR-STUV-WXYZ", "王国线");

        var created = service.CreateActorInStory("ST-JKLM-NPQR-STUV-WXYZ", "ST-JKLM-NPQR-STUV-WXYZ~actor~captain", "Captain");

        Assert.AreEqual("ST-JKLM-NPQR-STUV-WXYZ", created.HomeStoryId);
        Assert.AreEqual(5, new ActorRepository(directory.Root).LoadActor("ST-JKLM-NPQR-STUV-WXYZ~actor~captain").ToResource().SchemaVersion);
        CollectionAssert.Contains(store.Memberships.Load("ST-JKLM-NPQR-STUV-WXYZ").OwnedResources.Actors, "ST-JKLM-NPQR-STUV-WXYZ~actor~captain");

        var restarted = new ProjectService();
        restarted.OpenProject(directory.Root);
        Assert.AreEqual("ST-JKLM-NPQR-STUV-WXYZ", restarted.OpenActor("ST-JKLM-NPQR-STUV-WXYZ~actor~captain").HomeStoryId);
        CollectionAssert.Contains(new CanonicalProjectGraphStore(directory.Root).Memberships.Load("ST-JKLM-NPQR-STUV-WXYZ").OwnedResources.Actors, "ST-JKLM-NPQR-STUV-WXYZ~actor~captain");
    }

    [TestMethod]
    public void ImportAsNewCreatesIndependentActorAndSelectedOwnership()
    {
        using var directory = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(directory.Root);
        new CanonicalStoryLifecycleService(store).Create("ST-2345-6789-ABCD-EFGH", "Owner");
        var service = new ProjectService();
        service.OpenProject(directory.Root);
        var source = service.CreateActor("ST-2345-6789-ABCD-EFGH~actor~source", "Source");
        source.SetTags(["guard", "elite"]);
        service.SaveActor(source);
        new CanonicalStoryLifecycleService(store).Create("ST-JKLM-NPQR-STUV-WXYZ", "Capital");

        var imported = service.ImportActorAsNew("ST-2345-6789-ABCD-EFGH~actor~source", "ST-JKLM-NPQR-STUV-WXYZ~actor~capital_guard", "ST-JKLM-NPQR-STUV-WXYZ");
        imported.DisplayName = "Capital Guard";
        service.SaveActor(imported);

        var sourceReloaded = new ActorRepository(directory.Root).LoadActor("ST-2345-6789-ABCD-EFGH~actor~source");
        var importedReloaded = new ActorRepository(directory.Root).LoadActor("ST-JKLM-NPQR-STUV-WXYZ~actor~capital_guard");
        Assert.AreEqual("Source", sourceReloaded.DisplayName);
        Assert.AreEqual("Capital Guard", importedReloaded.DisplayName);
        CollectionAssert.AreEqual(sourceReloaded.Tags.ToArray(), importedReloaded.Tags.ToArray());
        Assert.AreEqual("ST-JKLM-NPQR-STUV-WXYZ", importedReloaded.HomeStoryId);
        CollectionAssert.Contains(store.Memberships.Load("ST-JKLM-NPQR-STUV-WXYZ").OwnedResources.Actors, "ST-JKLM-NPQR-STUV-WXYZ~actor~capital_guard");
        CollectionAssert.DoesNotContain(store.Memberships.Load("ST-JKLM-NPQR-STUV-WXYZ").OwnedResources.Actors, "ST-2345-6789-ABCD-EFGH~actor~source");
    }

    [TestMethod]
    public void ReferenceIsSharedIdempotentAndRemovalNeverDeletesActor()
    {
        using var directory = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(directory.Root);
        new CanonicalStoryLifecycleService(store).Create("ST-2345-6789-ABCD-EFGH", "Owner");
        var service = new ProjectService();
        var session = service.OpenProject(directory.Root);
        var actor = service.CreateActorInStory("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~actor~detective", "Detective");
        new CanonicalStoryLifecycleService(store).Create("ST-JKLM-NPQR-STUV-WXYZ", "Kingdom");

        service.AddActorReference("ST-JKLM-NPQR-STUV-WXYZ", actor.Id);
        Assert.ThrowsExactly<CanonicalStoryActorLifecycleException>(() => service.AddActorReference("ST-JKLM-NPQR-STUV-WXYZ", actor.Id));
        Assert.ThrowsExactly<CanonicalStoryActorLifecycleException>(() =>
            service.AddActorReference("ST-2345-6789-ABCD-EFGH", actor.Id));

        service.RemoveActorReference("ST-JKLM-NPQR-STUV-WXYZ", actor.Id);
        Assert.ThrowsExactly<CanonicalStoryActorLifecycleException>(() => service.RemoveActorReference("ST-JKLM-NPQR-STUV-WXYZ", actor.Id));
        Assert.IsTrue(File.Exists(new ActorRepository(directory.Root).GetActorPath(actor.Id)));
        CollectionAssert.DoesNotContain(store.Memberships.Load("ST-JKLM-NPQR-STUV-WXYZ").ReferencedResources.Actors, actor.Id);

        var restarted = new ProjectService();
        restarted.OpenProject(directory.Root);
        CollectionAssert.DoesNotContain(new CanonicalProjectGraphStore(directory.Root).Memberships.Load("ST-JKLM-NPQR-STUV-WXYZ").ReferencedResources.Actors, actor.Id);
        Assert.IsTrue(File.Exists(new ActorRepository(directory.Root).GetActorPath(actor.Id)));
    }

    [TestMethod]
    public void RemovingReferenceCannotDetachOwnedActor()
    {
        using var directory = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(directory.Root);
        new CanonicalStoryLifecycleService(store).Create("ST-2345-6789-ABCD-EFGH", "Owner");
        var service = new ProjectService();
        var session = service.OpenProject(directory.Root);
        var actor = service.CreateActorInStory("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~actor~owner", "Owner");

        Assert.ThrowsExactly<CanonicalStoryActorLifecycleException>(() => service.RemoveActorReference("ST-2345-6789-ABCD-EFGH", actor.Id));

        CollectionAssert.Contains(
            store.Memberships.Load("ST-2345-6789-ABCD-EFGH").OwnedResources.Actors,
            actor.Id);
        Assert.IsTrue(File.Exists(new ActorRepository(directory.Root).GetActorPath(actor.Id)));
    }

    [TestMethod]
    public void ExternalReferencesExposeDescriptorsAndBlockDelete()
    {
        using var directory = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(directory.Root);
        new CanonicalStoryLifecycleService(store).Create("ST-2345-6789-ABCD-EFGH", "Owner");
        var service = new ProjectService();
        var session = service.OpenProject(directory.Root);
        var actor = service.CreateActorInStory("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~actor~detective", "Detective");
        new CanonicalStoryLifecycleService(store).Create("ST-JKLM-NPQR-STUV-WXYZ", "王国线");
        service.AddActorReference("ST-JKLM-NPQR-STUV-WXYZ", actor.Id);

        var references = service.GetActorReferences(actor.Id);
        Assert.AreEqual(1, references.Count);
        Assert.AreEqual("ST-JKLM-NPQR-STUV-WXYZ", references[0].Id);
        Assert.AreEqual("王国线", references[0].DisplayName);
        Assert.IsTrue(references[0].Path.EndsWith(Path.Combine("stories", "ST-JKLM-NPQR-STUV-WXYZ.json"), StringComparison.OrdinalIgnoreCase));
        Assert.IsFalse(service.CanDeleteActor(actor.Id));
        Assert.ThrowsExactly<CanonicalStoryActorLifecycleException>(() => service.DeleteActor(actor.Id));
        Assert.IsTrue(File.Exists(new ActorRepository(directory.Root).GetActorPath(actor.Id)));

        service.RemoveActorReference("ST-JKLM-NPQR-STUV-WXYZ", actor.Id);
        Assert.IsTrue(service.CanDeleteActor(actor.Id));
        service.DeleteActor(actor.Id);
        Assert.IsFalse(File.Exists(new ActorRepository(directory.Root).GetActorPath(actor.Id)));
    }

    [TestMethod]
    public void MissingSelectedStoryIsRejectedBeforeActorMutation()
    {
        using var directory = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(directory.Root);
        new CanonicalStoryLifecycleService(store).Create("ST-2345-6789-ABCD-EFGH", "Owner");
        var service = new ProjectService();
        service.OpenProject(directory.Root);

        Assert.ThrowsExactly<GraphResourceRepositoryException>(() => service.CreateActorInStory("ST-3456-789A-BCDE-FGHJ", "ST-JKLM-NPQR-STUV-WXYZ~actor~captain", "Captain"));
        Assert.IsFalse(File.Exists(new ActorRepository(directory.Root).GetActorPath("ST-JKLM-NPQR-STUV-WXYZ~actor~captain")));
    }
}
