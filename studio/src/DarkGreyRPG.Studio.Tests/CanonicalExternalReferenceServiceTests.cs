using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CanonicalExternalReferenceServiceTests
{
    [TestMethod]
    public void RejectsUnresolvedExternalReferenceWithoutChangingMembership()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        CreateStory(store, "ST-2345-6789-ABCD-EFGH");

        AssertCode(() => new CanonicalExternalReferenceService(store)
            .AddReference("ST-2345-6789-ABCD-EFGH", DgrResourceKind.Actor, "ST-JKLM-NPQR-STUV-WXYZ~actor~boss"), "story.external_reference.source.invalid");

        var membership = store.Memberships.Load("ST-2345-6789-ABCD-EFGH");
        Assert.IsEmpty(membership.ReferencedResources.Actors);
        Assert.IsEmpty(membership.ReferencedResources.Items);
        Assert.IsFalse(File.Exists(Path.Combine(project.Root, "resources", "canonical", "actors", "boss.json")));
    }

    [TestMethod]
    public void RejectsStoryInvalidOwnedAndDuplicateReferences()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        CreateStory(store, "ST-2345-6789-ABCD-EFGH");
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "ST-2345-6789-ABCD-EFGH",
            ownedResources: new CanonicalStoryMembershipSet { Items = ["ST-2345-6789-ABCD-EFGH~item~owned"] },
            referencedResources: new CanonicalStoryMembershipSet { Actors = ["ST-JKLM-NPQR-STUV-WXYZ~actor~boss"] }));
        var service = new CanonicalExternalReferenceService(store);

        AssertCode(() => service.AddReference("ST-2345-6789-ABCD-EFGH", DgrResourceKind.Story, "ST-JKLM-NPQR-STUV-WXYZ"),
            "story.external_reference.kind.unsupported");
        AssertCode(() => service.AddReference("ST-2345-6789-ABCD-EFGH", DgrResourceKind.Item, "bare_id"),
            "story.external_reference.id.invalid");
        AssertCode(() => service.AddReference("ST-2345-6789-ABCD-EFGH", DgrResourceKind.Item, "ST-2345-6789-ABCD-EFGH~item~owned"),
            "story.external_reference.owned");
        AssertCode(() => service.AddReference("ST-2345-6789-ABCD-EFGH", DgrResourceKind.Actor, "ST-JKLM-NPQR-STUV-WXYZ~actor~boss"),
            "story.external_reference.duplicate");
    }

    [TestMethod]
    public void RejectedProviderPreservesUnrelatedMembershipFields()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        CreateStory(store, "ST-2345-6789-ABCD-EFGH");
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "ST-2345-6789-ABCD-EFGH",
            ownedResources: new CanonicalStoryMembershipSet { Tasks = ["ST-2345-6789-ABCD-EFGH~task~task"] },
            referencedResources: new CanonicalStoryMembershipSet { ItemGroups = ["ST-JKLM-NPQR-STUV-WXYZ~item_group~loot"] })
        {
            DisplayOrder = new CanonicalStoryDisplayOrder { Tasks = ["ST-2345-6789-ABCD-EFGH~task~task"] },
        });

        AssertCode(() => new CanonicalExternalReferenceService(store)
            .AddReference("ST-2345-6789-ABCD-EFGH", DgrResourceKind.Session, "ST-JKLM-NPQR-STUV-WXYZ~session~dialogue_session"), "story.external_reference.source.invalid");

        var membership = store.Memberships.Load("ST-2345-6789-ABCD-EFGH");
        CollectionAssert.AreEqual(new[] { "ST-2345-6789-ABCD-EFGH~task~task" }, membership.OwnedResources.Tasks);
        CollectionAssert.AreEqual(new[] { "ST-JKLM-NPQR-STUV-WXYZ~item_group~loot" }, membership.ReferencedResources.ItemGroups);
        Assert.IsEmpty(membership.ReferencedResources.Sessions);
        CollectionAssert.AreEqual(new[] { "ST-2345-6789-ABCD-EFGH~task~task" }, membership.DisplayOrder.Tasks);
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
