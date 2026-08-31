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
        var store = NewStore(project.Root, "story");
        var original = store.Memberships.Load("story");
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            original.StoryId,
            original.OwnedResources,
            original.ReferencedResources)
        {
            SchemaVersion = CanonicalStoryMembershipManifest.ItemMembershipSchemaVersion,
        });
        var service = new CanonicalStoryItemLifecycleService(store);

        var item = service.CreateOwned("story", CanonicalStoryItemKind.Individual, "key", "钥匙", ["quest", "key"]);
        var group = service.CreateOwned("story", CanonicalStoryItemKind.Collective, "weapon", "武器组", ["combat"]);

        Assert.IsInstanceOfType<IndividualItemResource>(item);
        Assert.IsInstanceOfType<CollectiveItemResource>(group);
        Assert.AreEqual(1, item.SchemaVersion);
        CollectionAssert.AreEqual(new[] { "quest", "key" }, item.Tags.ToArray());
        var membership = store.Memberships.Load("story");
        Assert.AreEqual(CanonicalStoryMembershipManifest.ItemMembershipSchemaVersion, membership.SchemaVersion);
        CollectionAssert.AreEqual(new[] { "key" }, membership.OwnedResources.Items);
        CollectionAssert.AreEqual(new[] { "weapon" }, membership.OwnedResources.ItemGroups);
        StringAssert.Contains(File.ReadAllText(new ItemRepository(project.Root).GetGroupPath("weapon")), "\"group_id\": \"weapon\"");
    }

    [TestMethod]
    public void DeleteAndCreatePreservePersistedDisplayOrderAndAppendMembership()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = NewStore(project.Root, "story");
        var service = new CanonicalStoryItemLifecycleService(store);
        service.CreateOwnedItem("story", "item_a", "物品 A");
        service.CreateOwnedItem("story", "item_b", "物品 B");
        service.CreateOwnedItem("story", "item_c", "物品 C");

        var membership = store.Memberships.Load("story");
        membership.DisplayOrder = new CanonicalStoryDisplayOrder
        {
            Items = ["item:item_c", "item:item_b", "item:item_a"],
        };
        store.Memberships.Replace(membership);

        service.DeleteOwnedItem("story", "item_b");
        service.CreateOwnedItem("story", "item_d", "物品 D");

        var reloaded = store.Memberships.Load("story");
        Assert.AreEqual(CanonicalStoryMembershipManifest.CurrentSchemaVersion, reloaded.SchemaVersion);
        CollectionAssert.AreEqual(
            new[] { "item:item_c", "item:item_b", "item:item_a" },
            reloaded.DisplayOrder.Items);
        CollectionAssert.AreEqual(new[] { "item_a", "item_c", "item_d" }, reloaded.OwnedResources.Items);

    }

    [TestMethod]
    public void ReferenceDetachKeepsItemFileAndDeleteBlocksCrossStoryUse()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = NewStore(project.Root, "owner", "other");
        var service = new CanonicalStoryItemLifecycleService(store);
        service.CreateOwned("owner", CanonicalStoryItemKind.Individual, "coin", "铜币");
        service.AddReference("other", CanonicalStoryItemKind.Individual, "coin");

        var plan = service.GetDeletionPlan("owner", CanonicalStoryItemKind.Individual, "coin");
        Assert.IsFalse(plan.CanDelete);
        CollectionAssert.AreEqual(new[] { "other" }, plan.ReferencingStoryIds.ToArray());
        var blocked = Assert.ThrowsExactly<CanonicalStoryItemLifecycleException>(
            () => service.DeleteOwned("owner", CanonicalStoryItemKind.Individual, "coin"));
        Assert.AreEqual("story.item.delete.blocked", blocked.Code);

        service.RemoveReference("other", CanonicalStoryItemKind.Individual, "coin");
        service.DeleteOwned("owner", CanonicalStoryItemKind.Individual, "coin");
        Assert.IsFalse(File.Exists(new ItemRepository(project.Root).GetItemPath("coin")));
    }

    [TestMethod]
    public void DeleteRestoresFileWhenMembershipWriteFails()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var normal = NewStore(project.Root, "story");
        var normalService = new CanonicalStoryItemLifecycleService(normal);
        normalService.CreateOwned("story", CanonicalStoryItemKind.Collective, "keys", "钥匙组");
        var repository = new ItemRepository(project.Root);
        var itemPath = repository.GetGroupPath("keys");
        var original = File.ReadAllBytes(itemPath);
        var failing = NewStore(project.Root, "story", new AlwaysFailingWriter());
        var exception = Assert.ThrowsExactly<CanonicalStoryItemLifecycleException>(
            () => new CanonicalStoryItemLifecycleService(failing).DeleteOwned("story", CanonicalStoryItemKind.Collective, "keys"));

        Assert.AreEqual("story.item.membership_replace_failed", exception.Code);
        CollectionAssert.AreEqual(original, File.ReadAllBytes(itemPath));
        CollectionAssert.AreEqual(new[] { "keys" }, failing.Memberships.Load("story").OwnedResources.ItemGroups);
    }

    [TestMethod]
    public void CreateRemovesResourceWhenSaveFailsAfterWriting()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = NewStore(project.Root, "story");
        var items = new ItemRepository(project.Root, new FailAfterWriteWriter());
        var service = new CanonicalStoryItemLifecycleService(store, items);

        var itemException = Assert.ThrowsExactly<CanonicalStoryItemLifecycleException>(
            () => service.CreateOwned("story", CanonicalStoryItemKind.Individual, "coin", "铜币", ["loot"]));

        var groupException = Assert.ThrowsExactly<CanonicalStoryItemLifecycleException>(
            () => service.CreateOwned("story", CanonicalStoryItemKind.Collective, "weapons", "武器组", ["combat"]));

        Assert.AreEqual("story.item.create_failed", itemException.Code);
        Assert.AreEqual("story.item.create_failed", groupException.Code);
        Assert.IsFalse(File.Exists(items.GetItemPath("coin")));
        Assert.IsFalse(File.Exists(items.GetGroupPath("weapons")));
        var membership = store.Memberships.Load("story");
        CollectionAssert.AreEqual(Array.Empty<string>(), membership.OwnedResources.Items);
        CollectionAssert.AreEqual(Array.Empty<string>(), membership.ReferencedResources.Items);
    }

    [TestMethod]
    public void CreateRestoresMembershipWhenMembershipWriteFailsAfterWriting()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var normal = NewStore(project.Root, "story");
        var membershipPath = normal.Memberships.GetPath("story");
        var originalMembership = File.ReadAllBytes(membershipPath);
        var failing = NewStore(project.Root, "story", new FailAfterWriteWriter());
        var items = new ItemRepository(project.Root);
        var service = new CanonicalStoryItemLifecycleService(failing, items);

        var exception = Assert.ThrowsExactly<CanonicalStoryItemLifecycleException>(
            () => service.CreateOwned("story", CanonicalStoryItemKind.Collective, "weapons", "武器组"));

        Assert.AreEqual("story.item.membership_replace_failed", exception.Code);
        Assert.IsFalse(File.Exists(items.GetGroupPath("weapons")));
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
