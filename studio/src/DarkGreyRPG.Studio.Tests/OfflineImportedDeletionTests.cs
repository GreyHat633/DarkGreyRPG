using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;
using DarkGreyRPG.Studio.Core.Items;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class OfflineImportedDeletionTests
{
    [TestMethod]
    public void ImportedStoryDeletesOwnedItemsAndGroupsAndOnlyItsNamespaceOverride()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var items = new ItemRepository(project.Root);
        var service = new CanonicalStoryLifecycleService(store, items: items);
        service.Create("Pack:story", "Imported Story");
        service.Create("Other:story", "Other Story");
        items.SaveItem(new IndividualItemResource { ItemId = "Pack:owned_item", DisplayName = "Owned Item" });
        items.SaveGroup(new CollectiveItemResource { GroupId = "Pack:owned_group", DisplayName = "Owned Group" });
        items.SaveItem(new IndividualItemResource { ItemId = "Pack:referenced_item", DisplayName = "Referenced Item" });
        items.SaveGroup(new CollectiveItemResource { GroupId = "Pack:referenced_group", DisplayName = "Referenced Group" });
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "Pack:story",
            new CanonicalStoryMembershipSet { Items = ["Pack:owned_item"], ItemGroups = ["Pack:owned_group"] },
            new CanonicalStoryMembershipSet { Items = ["Pack:referenced_item"], ItemGroups = ["Pack:referenced_group"] }));

        var policyPath = Path.Combine(project.Root, NamespacePolicyStore.RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(policyPath)!);
        File.WriteAllBytes(policyPath, NamespacePolicyStore.Encode(new NamespacePolicy(
            "global",
            new Dictionary<string, string>
            {
                ["Pack:story"] = "Pack",
                ["Other:story"] = "Other",
            })));

        var plan = service.GetDeletionPlan("Pack:story");

        CollectionAssert.AreEqual(new[] { "Pack:owned_item" }, plan.ItemIds.ToArray());
        CollectionAssert.AreEqual(new[] { "Pack:owned_group" }, plan.ItemGroupIds.ToArray());
        Assert.IsTrue(plan.CanDelete);
        service.Delete("Pack:story");

        Assert.IsFalse(File.Exists(store.Stories.GetPath("Pack:story")));
        Assert.IsFalse(File.Exists(store.Memberships.GetPath("Pack:story")));
        Assert.IsFalse(File.Exists(items.GetItemPath("Pack:owned_item")));
        Assert.IsFalse(File.Exists(items.GetGroupPath("Pack:owned_group")));
        Assert.IsTrue(File.Exists(items.GetItemPath("Pack:referenced_item")));
        Assert.IsTrue(File.Exists(items.GetGroupPath("Pack:referenced_group")));
        var policy = NamespacePolicyStore.Load(project.Root)!;
        Assert.IsFalse(policy.StoryOverrides.ContainsKey("Pack:story"));
        Assert.AreEqual("Other", policy.StoryOverrides["Other:story"]);
    }

    [TestMethod]
    public void ConsumerMembershipBlocksOwnedItemAndGroupDeletion()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var items = new ItemRepository(project.Root);
        var service = new CanonicalStoryLifecycleService(store, items: items);
        service.Create("Pack:story", "Imported Story");
        service.Create("Consumer:story", "Consumer Story");
        items.SaveItem(new IndividualItemResource { ItemId = "Pack:owned_item", DisplayName = "Owned Item" });
        items.SaveGroup(new CollectiveItemResource { GroupId = "Pack:owned_group", DisplayName = "Owned Group" });
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "Pack:story",
            new CanonicalStoryMembershipSet { Items = ["Pack:owned_item"], ItemGroups = ["Pack:owned_group"] }));
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "Consumer:story",
            referencedResources: new CanonicalStoryMembershipSet
            {
                Items = ["Pack:owned_item"],
                ItemGroups = ["Pack:owned_group"],
            }));

        var plan = service.GetDeletionPlan("Pack:story");

        Assert.IsFalse(plan.CanDelete);
        Assert.IsTrue(plan.Blockers.Any(item => item.ResourceKind == "item" && item.ResourceId == "Pack:owned_item"));
        Assert.IsTrue(plan.Blockers.Any(item => item.ResourceKind == "item_group" && item.ResourceId == "Pack:owned_group"));
        Assert.ThrowsExactly<CanonicalStoryLifecycleException>(() => service.Delete("Pack:story"));
        Assert.IsTrue(File.Exists(items.GetItemPath("Pack:owned_item")));
        Assert.IsTrue(File.Exists(items.GetGroupPath("Pack:owned_group")));
    }

    [TestMethod]
    public void InjectedDeleteFailureRestoresImportedResourcesAndPolicyByteForByte()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var items = new ItemRepository(project.Root);
        var service = new CanonicalStoryLifecycleService(store, items: items);
        service.Create("Pack:story", "Imported Story");
        items.SaveItem(new IndividualItemResource { ItemId = "Pack:owned_item", DisplayName = "Owned Item" });
        items.SaveGroup(new CollectiveItemResource { GroupId = "Pack:owned_group", DisplayName = "Owned Group" });
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "Pack:story",
            new CanonicalStoryMembershipSet { Items = ["Pack:owned_item"], ItemGroups = ["Pack:owned_group"] }));
        var policyPath = Path.Combine(project.Root, NamespacePolicyStore.RelativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(policyPath)!);
        File.WriteAllBytes(policyPath, NamespacePolicyStore.Encode(new NamespacePolicy(
            "global", new Dictionary<string, string> { ["Pack:story"] = "Pack", ["Other:story"] = "Other" })));

        var paths = new[]
        {
            store.Stories.GetPath("Pack:story"),
            store.Memberships.GetPath("Pack:story"),
            items.GetItemPath("Pack:owned_item"),
            items.GetGroupPath("Pack:owned_group"),
            policyPath,
        };
        var before = paths.ToDictionary(path => path, File.ReadAllBytes, StringComparer.OrdinalIgnoreCase);
        var failing = new CanonicalStoryLifecycleService(store, items: items, deleteFile: path =>
        {
            if (string.Equals(path, items.GetGroupPath("Pack:owned_group"), StringComparison.OrdinalIgnoreCase))
                throw new IOException("simulated delete failure");
            File.Delete(path);
        });

        var exception = Assert.ThrowsExactly<CanonicalStoryLifecycleException>(() => failing.Delete("Pack:story"));

        Assert.AreEqual("story.lifecycle.delete_failed", exception.Code);
        foreach (var pair in before)
            CollectionAssert.AreEqual(pair.Value, File.ReadAllBytes(pair.Key));
        CollectionAssert.AreEqual(before[policyPath], File.ReadAllBytes(policyPath));
    }
}
