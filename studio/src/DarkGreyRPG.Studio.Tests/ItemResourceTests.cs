using DarkGreyRPG.Studio.Core.Items;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class ItemResourceTests
{
    [TestMethod]
    public void IndividualResourceUsesStableSnakeCaseSchema()
    {
        var resource = new IndividualItemResource
        {
            ItemId = "ST-2345-6789-ABCD-EFGH~item~royal_key",
            DisplayName = "王室钥匙",
            Tags = ["quest", "ST-2345-6789-ABCD-EFGH~item~key"],
        };

        var json = ItemSerializer.Serialize(resource);
        var loaded = Assert.IsInstanceOfType<IndividualItemResource>(ItemSerializer.Deserialize(json, "royal_key.json"));

        StringAssert.Contains(json, "\"schema_version\": 2");
        Assert.AreEqual("ST-2345-6789-ABCD-EFGH", System.Text.Json.Nodes.JsonNode.Parse(json)!["item_id"]["story_uid"].GetValue<string>());
        Assert.IsFalse(json.Contains("ItemId", StringComparison.Ordinal));
        Assert.AreEqual(resource.ItemId, loaded.ItemId);
        CollectionAssert.AreEqual(resource.Tags, loaded.Tags);
        Assert.AreEqual(json, ItemSerializer.Serialize(loaded));
    }

    [TestMethod]
    public void CollectiveResourceRoundTripsWithOnlyItsStudioFields()
    {
        var resource = new CollectiveItemResource
        {
            GroupId = "ST-2345-6789-ABCD-EFGH~item_group~sword",
            DisplayName = "剑",
            Tags = ["weapon"],
        };

        var json = ItemSerializer.Serialize(resource);
        var loaded = ItemSerializer.DeserializeCollective(json, "sword.json");

        Assert.AreEqual("ST-2345-6789-ABCD-EFGH", System.Text.Json.Nodes.JsonNode.Parse(json)!["group_id"]["story_uid"].GetValue<string>());
        Assert.IsFalse(json.Contains("members", StringComparison.Ordinal));
        Assert.AreEqual(resource.GroupId, loaded.GroupId);
        CollectionAssert.AreEqual(resource.Tags, loaded.Tags);
        Assert.AreEqual(json, ItemSerializer.Serialize(loaded));
    }

    [TestMethod]
    public void UnknownFieldsAreRejected()
    {
        const string unknownField = """
            { "schema_version": 1, "type": "individual", "item_id": "ST-2345-6789-ABCD-EFGH~item~key", "display_name": "Key", "tags": [], "extra": true }
            """;

        Assert.ThrowsExactly<ItemDataException>(() => ItemSerializer.Deserialize(unknownField));
    }
}
