using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CanonicalExternalReferenceServiceTests
{
    [TestMethod]
    public void AddsUnresolvedExternalReferenceToOnlySelectedKind()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        CreateStory(store, "consumer");

        new CanonicalExternalReferenceService(store)
            .AddReference("consumer", DgrResourceKind.Actor, "other_author:boss");

        var membership = store.Memberships.Load("consumer");
        CollectionAssert.AreEqual(new[] { "other_author:boss" }, membership.ReferencedResources.Actors);
        Assert.IsEmpty(membership.ReferencedResources.Items);
        Assert.IsFalse(File.Exists(Path.Combine(project.Root, "resources", "canonical", "actors", "boss.json")));
    }

    [TestMethod]
    public void RejectsStoryInvalidOwnedAndDuplicateReferences()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        CreateStory(store, "consumer");
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "consumer",
            ownedResources: new CanonicalStoryMembershipSet { Items = ["other_author:owned"] },
            referencedResources: new CanonicalStoryMembershipSet { Actors = ["other_author:boss"] }));
        var service = new CanonicalExternalReferenceService(store);

        AssertCode(() => service.AddReference("consumer", DgrResourceKind.Story, "other_author:story"),
            "story.external_reference.kind.unsupported");
        AssertCode(() => service.AddReference("consumer", DgrResourceKind.Item, "bare_id"),
            "story.external_reference.id.invalid");
        AssertCode(() => service.AddReference("consumer", DgrResourceKind.Item, "other_author:owned"),
            "story.external_reference.owned");
        AssertCode(() => service.AddReference("consumer", DgrResourceKind.Actor, "other_author:boss"),
            "story.external_reference.duplicate");
    }

    [TestMethod]
    public void PreservesUnrelatedMembershipFieldsAndDoesNotNormalizeFullId()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        CreateStory(store, "consumer");
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "consumer",
            ownedResources: new CanonicalStoryMembershipSet { Tasks = ["consumer:task"] },
            referencedResources: new CanonicalStoryMembershipSet { ItemGroups = ["other_author:loot"] })
        {
            DisplayOrder = new CanonicalStoryDisplayOrder { Tasks = ["consumer:task"] },
        });

        new CanonicalExternalReferenceService(store)
            .AddReference("consumer", DgrResourceKind.Session, "other_author:dialogue_session");

        var membership = store.Memberships.Load("consumer");
        CollectionAssert.AreEqual(new[] { "consumer:task" }, membership.OwnedResources.Tasks);
        CollectionAssert.AreEqual(new[] { "other_author:loot" }, membership.ReferencedResources.ItemGroups);
        CollectionAssert.AreEqual(new[] { "other_author:dialogue_session" }, membership.ReferencedResources.Sessions);
        CollectionAssert.AreEqual(new[] { "consumer:task" }, membership.DisplayOrder.Tasks);
    }

    private static void CreateStory(CanonicalProjectGraphStore store, string id)
    {
        store.Stories.Create(new GraphResourceEnvelope(
            GraphResourceKind.Story, id, id, new GraphDocument([new GraphNode("start", "start", "Start")])));
        store.Memberships.Create(new CanonicalStoryMembershipManifest(id));
    }

    private static void AssertCode(Action action, string expectedCode)
        => Assert.AreEqual(expectedCode,
            Assert.ThrowsExactly<CanonicalExternalReferenceException>(action).Code);
}
