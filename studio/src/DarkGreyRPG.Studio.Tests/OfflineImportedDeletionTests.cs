using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.Items;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class OfflineImportedDeletionTests
{
    [TestMethod]
    public void ImportedStoryDeletesOnlyOwnedItemsAndGroups()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var items = new ItemRepository(project.Root);
        var service = new CanonicalStoryLifecycleService(store, items: items);
        service.Create("ST-2345-6789-ABCD-EFGH", "Imported Story");
        service.Create("ST-JKLM-NPQR-STUV-WXYZ", "Other Story");
        items.SaveItem(new IndividualItemResource { ItemId = "ST-2345-6789-ABCD-EFGH~item~owned_item", DisplayName = "Owned Item" });
        items.SaveGroup(new CollectiveItemResource { GroupId = "ST-2345-6789-ABCD-EFGH~item_group~owned_group", DisplayName = "Owned Group" });
        items.SaveItem(new IndividualItemResource { ItemId = "ST-JKLM-NPQR-STUV-WXYZ~item~referenced_item", DisplayName = "Referenced Item" });
        items.SaveGroup(new CollectiveItemResource { GroupId = "ST-JKLM-NPQR-STUV-WXYZ~item_group~referenced_group", DisplayName = "Referenced Group" });
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "ST-2345-6789-ABCD-EFGH",
            new CanonicalStoryMembershipSet { Items = ["ST-2345-6789-ABCD-EFGH~item~owned_item"], ItemGroups = ["ST-2345-6789-ABCD-EFGH~item_group~owned_group"] },
            new CanonicalStoryMembershipSet { Items = ["ST-JKLM-NPQR-STUV-WXYZ~item~referenced_item"], ItemGroups = ["ST-JKLM-NPQR-STUV-WXYZ~item_group~referenced_group"] }));

        var policyPath = Path.Combine(project.Root, "unrelated-settings.json");
        File.WriteAllText(policyPath, "{\"keep\":true}");

        var plan = service.GetDeletionPlan("ST-2345-6789-ABCD-EFGH");

        CollectionAssert.AreEqual(new[] { "ST-2345-6789-ABCD-EFGH~item~owned_item" }, plan.ItemIds.ToArray());
        CollectionAssert.AreEqual(new[] { "ST-2345-6789-ABCD-EFGH~item_group~owned_group" }, plan.ItemGroupIds.ToArray());
        Assert.IsTrue(plan.CanDelete);
        service.Delete("ST-2345-6789-ABCD-EFGH");

        Assert.IsFalse(File.Exists(store.Stories.GetPath("ST-2345-6789-ABCD-EFGH")));
        Assert.IsFalse(File.Exists(store.Memberships.GetPath("ST-2345-6789-ABCD-EFGH")));
        Assert.IsFalse(File.Exists(items.GetItemPath("ST-2345-6789-ABCD-EFGH~item~owned_item")));
        Assert.IsFalse(File.Exists(items.GetGroupPath("ST-2345-6789-ABCD-EFGH~item_group~owned_group")));
        Assert.IsTrue(File.Exists(items.GetItemPath("ST-JKLM-NPQR-STUV-WXYZ~item~referenced_item")));
        Assert.IsTrue(File.Exists(items.GetGroupPath("ST-JKLM-NPQR-STUV-WXYZ~item_group~referenced_group")));
        Assert.AreEqual("{\"keep\":true}", File.ReadAllText(policyPath));
    }

    [TestMethod]
    public void ConsumerMembershipBlocksOwnedItemAndGroupDeletion()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var items = new ItemRepository(project.Root);
        var service = new CanonicalStoryLifecycleService(store, items: items);
        service.Create("ST-2345-6789-ABCD-EFGH", "Imported Story");
        service.Create("ST-JKLM-NPQR-STUV-WXYZ", "Consumer Story");
        items.SaveItem(new IndividualItemResource { ItemId = "ST-2345-6789-ABCD-EFGH~item~owned_item", DisplayName = "Owned Item" });
        items.SaveGroup(new CollectiveItemResource { GroupId = "ST-2345-6789-ABCD-EFGH~item_group~owned_group", DisplayName = "Owned Group" });
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "ST-2345-6789-ABCD-EFGH",
            new CanonicalStoryMembershipSet { Items = ["ST-2345-6789-ABCD-EFGH~item~owned_item"], ItemGroups = ["ST-2345-6789-ABCD-EFGH~item_group~owned_group"] }));
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "ST-JKLM-NPQR-STUV-WXYZ",
            referencedResources: new CanonicalStoryMembershipSet
            {
                Items = ["ST-2345-6789-ABCD-EFGH~item~owned_item"],
                ItemGroups = ["ST-2345-6789-ABCD-EFGH~item_group~owned_group"],
            }));

        var plan = service.GetDeletionPlan("ST-2345-6789-ABCD-EFGH");

        Assert.IsFalse(plan.CanDelete);
        Assert.IsTrue(plan.Blockers.Any(item => item.ResourceKind == "item" && item.ResourceId == "ST-2345-6789-ABCD-EFGH~item~owned_item"));
        Assert.IsTrue(plan.Blockers.Any(item => item.ResourceKind == "item_group" && item.ResourceId == "ST-2345-6789-ABCD-EFGH~item_group~owned_group"));
        Assert.ThrowsExactly<CanonicalStoryLifecycleException>(() => service.Delete("ST-2345-6789-ABCD-EFGH"));
        Assert.IsTrue(File.Exists(items.GetItemPath("ST-2345-6789-ABCD-EFGH~item~owned_item")));
        Assert.IsTrue(File.Exists(items.GetGroupPath("ST-2345-6789-ABCD-EFGH~item_group~owned_group")));
    }

    [TestMethod]
    public void InjectedDeleteFailureRestoresImportedResourcesAndUnrelatedMetadataByteForByte()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var items = new ItemRepository(project.Root);
        var service = new CanonicalStoryLifecycleService(store, items: items);
        service.Create("ST-2345-6789-ABCD-EFGH", "Imported Story");
        items.SaveItem(new IndividualItemResource { ItemId = "ST-2345-6789-ABCD-EFGH~item~owned_item", DisplayName = "Owned Item" });
        items.SaveGroup(new CollectiveItemResource { GroupId = "ST-2345-6789-ABCD-EFGH~item_group~owned_group", DisplayName = "Owned Group" });
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "ST-2345-6789-ABCD-EFGH",
            new CanonicalStoryMembershipSet { Items = ["ST-2345-6789-ABCD-EFGH~item~owned_item"], ItemGroups = ["ST-2345-6789-ABCD-EFGH~item_group~owned_group"] }));
        var policyPath = Path.Combine(project.Root, "unrelated-settings.json");
        File.WriteAllText(policyPath, "{\"keep\":true}");

        var paths = new[]
        {
            store.Stories.GetPath("ST-2345-6789-ABCD-EFGH"),
            store.Memberships.GetPath("ST-2345-6789-ABCD-EFGH"),
            items.GetItemPath("ST-2345-6789-ABCD-EFGH~item~owned_item"),
            items.GetGroupPath("ST-2345-6789-ABCD-EFGH~item_group~owned_group"),
            policyPath,
        };
        var before = paths.ToDictionary(path => path, File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);
        var failing = new CanonicalStoryLifecycleService(store, items: items, deleteFile: path =>
        {
            if (string.Equals(path, items.GetGroupPath("ST-2345-6789-ABCD-EFGH~item_group~owned_group"), StringComparison.OrdinalIgnoreCase))
                throw new IOException("simulated delete failure");
            File.Delete(path);
        });

        var exception = Assert.ThrowsExactly<CanonicalStoryLifecycleException>(() => failing.Delete("ST-2345-6789-ABCD-EFGH"));

        Assert.AreEqual("story.lifecycle.delete_failed", exception.Code);
        foreach (var pair in before)
            CollectionAssert.AreEqual(pair.Value, File.ReadAllBytes(pair.Key));
        CollectionAssert.AreEqual(before[policyPath], File.ReadAllBytes(policyPath));
    }
}
