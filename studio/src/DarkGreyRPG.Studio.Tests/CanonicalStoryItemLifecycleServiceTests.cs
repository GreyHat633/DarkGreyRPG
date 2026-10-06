using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.IO;
using DarkGreyRPG.Studio.Core.Items;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CanonicalStoryItemLifecycleServiceTests
{
    [TestMethod]
    public void CreatesIndividualAndGroupWithoutChangingMembershipSchema()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = NewStore(project.Root, "ST-2345-6789-ABCD-EFGH");
        var original = store.Memberships.Load("ST-2345-6789-ABCD-EFGH");
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            original.StoryId,
            original.OwnedResources,
            original.ReferencedResources)
        {
            SchemaVersion = CanonicalStoryMembershipManifest.CurrentSchemaVersion,
        });
        var service = new CanonicalStoryItemLifecycleService(store);

        var item = service.CreateOwned("ST-2345-6789-ABCD-EFGH", CanonicalStoryItemKind.Individual, "ST-2345-6789-ABCD-EFGH~item~key", "钥匙", ["quest", "key"]);
        var group = service.CreateOwned("ST-2345-6789-ABCD-EFGH", CanonicalStoryItemKind.Collective, "ST-2345-6789-ABCD-EFGH~item_group~weapon", "武器组", ["combat"]);

        Assert.IsInstanceOfType<IndividualItemResource>(item);
        Assert.IsInstanceOfType<CollectiveItemResource>(group);
        Assert.AreEqual(2, item.SchemaVersion);
        CollectionAssert.AreEqual(new[] { "quest", "key" }, item.Tags.ToArray());
        var membership = store.Memberships.Load("ST-2345-6789-ABCD-EFGH");
        Assert.AreEqual(CanonicalStoryMembershipManifest.CurrentSchemaVersion, membership.SchemaVersion);
        CollectionAssert.AreEqual(new[] { "ST-2345-6789-ABCD-EFGH~item~key" }, membership.OwnedResources.Items);
        CollectionAssert.AreEqual(new[] { "ST-2345-6789-ABCD-EFGH~item_group~weapon" }, membership.OwnedResources.ItemGroups);
        Assert.AreEqual("ST-2345-6789-ABCD-EFGH~item_group~weapon", new ItemRepository(project.Root).LoadGroup("ST-2345-6789-ABCD-EFGH~item_group~weapon").GroupId);
    }

    [TestMethod]
    public void DeleteAndCreatePreservePersistedDisplayOrderAndAppendMembership()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = NewStore(project.Root, "ST-2345-6789-ABCD-EFGH");
        var service = new CanonicalStoryItemLifecycleService(store);
        service.CreateOwnedItem("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~item~item_a", "物品 A");
        service.CreateOwnedItem("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~item~item_b", "物品 B");
        service.CreateOwnedItem("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~item~item_c", "物品 C");

        var membership = store.Memberships.Load("ST-2345-6789-ABCD-EFGH");
        membership.DisplayOrder = new CanonicalStoryDisplayOrder
        {
            Items = ["item:ST-2345-6789-ABCD-EFGH~item~item_c", "item:ST-2345-6789-ABCD-EFGH~item~item_b", "item:ST-2345-6789-ABCD-EFGH~item~item_a"],
        };
        store.Memberships.Replace(membership);

        service.DeleteOwnedItem("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~item~item_b");
        service.CreateOwnedItem("ST-2345-6789-ABCD-EFGH", "ST-2345-6789-ABCD-EFGH~item~item_d", "物品 D");

        var reloaded = store.Memberships.Load("ST-2345-6789-ABCD-EFGH");
        Assert.AreEqual(CanonicalStoryMembershipManifest.CurrentSchemaVersion, reloaded.SchemaVersion);
        CollectionAssert.AreEqual(
            new[] { "item:ST-2345-6789-ABCD-EFGH~item~item_c", "item:ST-2345-6789-ABCD-EFGH~item~item_b", "item:ST-2345-6789-ABCD-EFGH~item~item_a" },
            reloaded.DisplayOrder.Items);
        CollectionAssert.AreEqual(new[] { "ST-2345-6789-ABCD-EFGH~item~item_a", "ST-2345-6789-ABCD-EFGH~item~item_c", "ST-2345-6789-ABCD-EFGH~item~item_d" }, reloaded.OwnedResources.Items);

    }

    [TestMethod]
    public void ReferenceDetachKeepsItemFileAndDeleteBlocksCrossStoryUse()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = NewStore(project.Root, "ST-2345-6789-ABCD-EFGH", "ST-JKLM-NPQR-STUV-WXYZ");
        var service = new CanonicalStoryItemLifecycleService(store);
        service.CreateOwned("ST-2345-6789-ABCD-EFGH", CanonicalStoryItemKind.Individual, "ST-2345-6789-ABCD-EFGH~item~coin", "铜币");
        service.AddReference("ST-JKLM-NPQR-STUV-WXYZ", CanonicalStoryItemKind.Individual, "ST-2345-6789-ABCD-EFGH~item~coin");

        var plan = service.GetDeletionPlan("ST-2345-6789-ABCD-EFGH", CanonicalStoryItemKind.Individual, "ST-2345-6789-ABCD-EFGH~item~coin");
        Assert.IsFalse(plan.CanDelete);
        CollectionAssert.AreEqual(new[] { "ST-JKLM-NPQR-STUV-WXYZ" }, plan.ReferencingStoryIds.ToArray());
        var blocked = Assert.ThrowsExactly<CanonicalStoryItemLifecycleException>(
            () => service.DeleteOwned("ST-2345-6789-ABCD-EFGH", CanonicalStoryItemKind.Individual, "ST-2345-6789-ABCD-EFGH~item~coin"));
        Assert.AreEqual("story.item.delete.blocked", blocked.Code);

        service.RemoveReference("ST-JKLM-NPQR-STUV-WXYZ", CanonicalStoryItemKind.Individual, "ST-2345-6789-ABCD-EFGH~item~coin");
        service.DeleteOwned("ST-2345-6789-ABCD-EFGH", CanonicalStoryItemKind.Individual, "ST-2345-6789-ABCD-EFGH~item~coin");
        Assert.IsFalse(File.Exists(new ItemRepository(project.Root).GetItemPath("ST-2345-6789-ABCD-EFGH~item~coin")));
    }

    [TestMethod]
    public void DeleteRestoresFileWhenMembershipWriteFails()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var normal = NewStore(project.Root, "ST-2345-6789-ABCD-EFGH");
        var normalService = new CanonicalStoryItemLifecycleService(normal);
        normalService.CreateOwned("ST-2345-6789-ABCD-EFGH", CanonicalStoryItemKind.Collective, "ST-2345-6789-ABCD-EFGH~item_group~keys", "钥匙组");
        var repository = new ItemRepository(project.Root);
        var itemPath = repository.GetGroupPath("ST-2345-6789-ABCD-EFGH~item_group~keys");
        var original = File.ReadAllBytes(itemPath);
        var failing = NewStore(project.Root, "ST-2345-6789-ABCD-EFGH", new AlwaysFailingWriter());
        var exception = Assert.ThrowsExactly<CanonicalStoryItemLifecycleException>(
            () => new CanonicalStoryItemLifecycleService(failing).DeleteOwned("ST-2345-6789-ABCD-EFGH", CanonicalStoryItemKind.Collective, "ST-2345-6789-ABCD-EFGH~item_group~keys"));

        Assert.AreEqual("story.item.membership_replace_failed", exception.Code);
        CollectionAssert.AreEqual(original, File.ReadAllBytes(itemPath));
        CollectionAssert.AreEqual(new[] { "ST-2345-6789-ABCD-EFGH~item_group~keys" }, failing.Memberships.Load("ST-2345-6789-ABCD-EFGH").OwnedResources.ItemGroups);
    }

    [TestMethod]
    public void CreateRemovesResourceWhenSaveFailsAfterWriting()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = NewStore(project.Root, "ST-2345-6789-ABCD-EFGH");
        var items = new ItemRepository(project.Root, new FailAfterWriteWriter());
        var service = new CanonicalStoryItemLifecycleService(store, items);

        var itemException = Assert.ThrowsExactly<CanonicalStoryItemLifecycleException>(
            () => service.CreateOwned("ST-2345-6789-ABCD-EFGH", CanonicalStoryItemKind.Individual, "ST-2345-6789-ABCD-EFGH~item~coin", "铜币", ["loot"]));

        var groupException = Assert.ThrowsExactly<CanonicalStoryItemLifecycleException>(
            () => service.CreateOwned("ST-2345-6789-ABCD-EFGH", CanonicalStoryItemKind.Collective, "ST-2345-6789-ABCD-EFGH~item_group~weapons", "武器组", ["combat"]));

        Assert.AreEqual("story.item.create_failed", itemException.Code);
        Assert.AreEqual("story.item.create_failed", groupException.Code);
        Assert.IsFalse(File.Exists(items.GetItemPath("ST-2345-6789-ABCD-EFGH~item~coin")));
        Assert.IsFalse(File.Exists(items.GetGroupPath("ST-2345-6789-ABCD-EFGH~item_group~weapons")));
        var membership = store.Memberships.Load("ST-2345-6789-ABCD-EFGH");
        CollectionAssert.AreEqual(Array.Empty<string>(), membership.OwnedResources.Items);
        CollectionAssert.AreEqual(Array.Empty<string>(), membership.ReferencedResources.Items);
    }

    [TestMethod]
    public void CreateRestoresMembershipWhenMembershipWriteFailsAfterWriting()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var normal = NewStore(project.Root, "ST-2345-6789-ABCD-EFGH");
        var membershipPath = normal.Memberships.GetPath("ST-2345-6789-ABCD-EFGH");
        var originalMembership = File.ReadAllBytes(membershipPath);
        var failing = NewStore(project.Root, "ST-2345-6789-ABCD-EFGH", new FailAfterWriteWriter());
        var items = new ItemRepository(project.Root);
        var service = new CanonicalStoryItemLifecycleService(failing, items);

        var exception = Assert.ThrowsExactly<CanonicalStoryItemLifecycleException>(
            () => service.CreateOwned("ST-2345-6789-ABCD-EFGH", CanonicalStoryItemKind.Collective, "ST-2345-6789-ABCD-EFGH~item_group~weapons", "武器组"));

        Assert.AreEqual("story.item.membership_replace_failed", exception.Code);
        Assert.IsFalse(File.Exists(items.GetGroupPath("ST-2345-6789-ABCD-EFGH~item_group~weapons")));
        CollectionAssert.AreEqual(originalMembership, File.ReadAllBytes(membershipPath));
    }

    private static CanonicalProjectGraphStore NewStore(string root, params string[] stories)
    {
        var store = new CanonicalProjectGraphStore(root);
        foreach (var story in stories)
        {
            store.Stories.Create(new GraphResourceEnvelope(
                GraphResourceKind.Story, story, story,
                new GraphDocument([new GraphNode("start", "start", "Start")])));
            store.Memberships.Create(new CanonicalStoryMembershipManifest(story));
        }
        return store;
    }

    private static CanonicalProjectGraphStore NewStore(string root, string story, IAtomicFileWriter writer)
    {
        var store = new CanonicalProjectGraphStore(root, writer);
        // The canonical files already exist from the normal setup; this
        // overload intentionally only changes the writer used by mutations.
        return store;
    }

    private sealed class AlwaysFailingWriter : IAtomicFileWriter
    {
        public void Write(string destinationPath, string contents, Action<string>? validateTemporaryFile = null)
            => throw new IOException("simulated membership failure");
    }

    private sealed class FailAfterWriteWriter : IAtomicFileWriter
    {
        public void Write(string destinationPath, string contents, Action<string>? validateTemporaryFile = null)
        {
            new AtomicFileWriter().Write(destinationPath, contents, validateTemporaryFile);
            throw new IOException("simulated save failure after commit");
        }
    }
}
