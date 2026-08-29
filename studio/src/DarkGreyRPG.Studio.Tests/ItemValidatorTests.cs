using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class ItemValidatorTests
{
    [TestMethod]
    public void InvalidSchemaAndIdsFailClosed()
    {
        var resource = new IndividualItemResource { SchemaVersion = 99, ItemId = "Bad ID", DisplayName = "Key" };

        var issues = ItemValidator.Validate(resource);

        Assert.IsTrue(issues.Any(issue => issue.Code == "item.schema.unsupported" && issue.Severity == ValidationSeverity.Error));
        Assert.IsTrue(issues.Any(issue => issue.Code == "item.item_id.invalid" && issue.Severity == ValidationSeverity.Error));
        Assert.ThrowsExactly<ItemValidationException>(() => ItemSerializer.Serialize(resource));
    }

    [TestMethod]
    public void DuplicateTagsAreRejected()
    {
        var resource = new IndividualItemResource
        {
            ItemId = "iron_sword",
            DisplayName = "Iron Sword",
            Tags = ["weapon", "weapon"],
        };

        var issues = ItemValidator.Validate(resource);

        Assert.IsTrue(issues.Any(issue => issue.Code == "item.tags.duplicate"));
    }

    [TestMethod]
    public void StackCountIsNotRepresentedByItemIdentity()
    {
        var one = new IndividualItemResource { ItemId = "apple", DisplayName = "Apple" };
        var same = new IndividualItemResource { ItemId = "apple", DisplayName = "Apple" };

        Assert.AreEqual(ItemSerializer.Serialize(one), ItemSerializer.Serialize(same));
    }
}
