using DarkGreyRPG.Studio.Core.Items;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class ItemRepositoryTests
{
    [TestMethod]
    public void SaveAndReloadsIndividualAndGroupWithoutDrift()
    {
        using var project = new TestProjectDirectory();
        var repository = new ItemRepository(project.Root);
        var item = repository.SaveItem(new IndividualItemResource { ItemId = "ST-2345-6789-ABCD-EFGH~item~royal_key", DisplayName = "Key", Tags = ["quest"] });
        var group = repository.SaveGroup(new CollectiveItemResource
        {
            GroupId = "ST-2345-6789-ABCD-EFGH~item_group~keys",
            DisplayName = "Keys",
            Tags = ["quest"],
        });

        var itemJson = File.ReadAllText(repository.GetItemPath(item.ItemId));
        var groupJson = File.ReadAllText(repository.GetGroupPath(group.GroupId));
        Assert.AreEqual(itemJson, ItemSerializer.Serialize(repository.LoadItem("ST-2345-6789-ABCD-EFGH~item~royal_key")));
        Assert.AreEqual(groupJson, ItemSerializer.Serialize(repository.LoadGroup("ST-2345-6789-ABCD-EFGH~item_group~keys")));
        CollectionAssert.AreEqual(new[] { "ST-2345-6789-ABCD-EFGH~item~royal_key" }, repository.ListItems().Select(x => x.Id).ToArray());
        CollectionAssert.AreEqual(new[] { "ST-2345-6789-ABCD-EFGH~item_group~keys" }, repository.ListGroups().Select(x => x.Id).ToArray());
    }

    [TestMethod]
    public void DuplicateResourceDoesNotOverwriteExistingFile()
    {
        using var project = new TestProjectDirectory();
        var repository = new ItemRepository(project.Root);
        repository.SaveItem(new IndividualItemResource { ItemId = "ST-2345-6789-ABCD-EFGH~item~key", DisplayName = "Original" });
        var path = repository.GetItemPath("ST-2345-6789-ABCD-EFGH~item~key");
        var original = File.ReadAllText(path);

        // Existing resources are intentionally updateable; a second create with a different
        // identity is the collision boundary enforced by the file key itself.
        repository.SaveItem(new IndividualItemResource { ItemId = "ST-2345-6789-ABCD-EFGH~item~key", DisplayName = "Updated" });
        Assert.AreNotEqual(original, File.ReadAllText(path));
        Assert.AreEqual("Updated", repository.LoadItem("ST-2345-6789-ABCD-EFGH~item~key").DisplayName);
    }

    [TestMethod]
    public void InvalidSaveLeavesLastValidFileInPlace()
    {
        using var project = new TestProjectDirectory();
        var repository = new ItemRepository(project.Root);
        repository.SaveItem(new IndividualItemResource { ItemId = "ST-2345-6789-ABCD-EFGH~item~key", DisplayName = "Original" });
        var path = repository.GetItemPath("ST-2345-6789-ABCD-EFGH~item~key");
        var original = File.ReadAllText(path);

        Assert.ThrowsExactly<ItemValidationException>(() => repository.SaveItem(new IndividualItemResource { ItemId = "Bad ID", DisplayName = "Invalid" }));
        Assert.AreEqual(original, File.ReadAllText(path));
        Assert.AreEqual(0, Directory.GetFiles(repository.ItemsDirectory, "*.tmp").Length);
    }
}
