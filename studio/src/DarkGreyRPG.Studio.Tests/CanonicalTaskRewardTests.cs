using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CanonicalTaskRewardTests
{
    [TestMethod]
    public void RewardIsLogicSinkWithStrictSignedPackage()
    {
        var node = GraphNodeFactory.Create(GraphScope.Task, "reward", "reward");
        Assert.AreEqual(1, node.Ports.Count);
        Assert.IsTrue(node.Ports.Single().IsInput);
        Assert.AreEqual(GraphInterfaceKind.Logic, node.Ports.Single().InterfaceKind);
        Assert.AreEqual(1, node.Properties["entries"].GetArrayLength());
        Assert.AreEqual("", node.Properties["entries"][0].GetProperty("item").GetString());
        Assert.AreEqual(1, node.Properties["entries"][0].GetProperty("amount").GetInt32());
        Assert.AreEqual(1, CanonicalTaskRewardSchema.Validate(node).Count);
        foreach (var invalid in new[] { "null", "[{}]", "[{\"type\":\"currency\",\"amount\":1}]", "[{\"type\":\"xp\",\"amount\":1.5}]", "[{\"type\":\"xp\",\"amount\":2147483648}]", "[{\"type\":\"xp\",\"amount\":0,\"item\":\"a\"}]", "[{\"type\":\"item\",\"amount\":1}]" })
        {
            node.Properties["entries"] = JsonDocument.Parse(invalid).RootElement.Clone();
            Assert.IsTrue(CanonicalTaskRewardSchema.Validate(node).Count > 0, invalid);
        }
        node.Properties["entries"] = JsonSerializer.SerializeToElement(new object[] {
            new { type = "item", item = "ST-2345-6789-ABCD-EFGH~item~apple", amount = int.MinValue }, new { type = "xp", amount = 0 }, new { type = "xp", amount = int.MaxValue } });
        Assert.AreEqual(0, CanonicalTaskRewardSchema.Validate(node).Count);
        var restored = GraphSerializer.Deserialize(GraphSerializer.Serialize(new GraphDocument([node])));
        Assert.AreEqual(3, restored.Nodes.Single().Properties["entries"].GetArrayLength());
    }

    [TestMethod]
    public void CopyPreservesTaskDescriptionAndChangesOnlyRewardItemReferences()
    {
        var reward = GraphNodeFactory.Create(GraphScope.Task, "reward", "reward");
        reward.Properties["entries"] = JsonSerializer.SerializeToElement(new object[] { new { type = "item", item = "ST-2345-6789-ABCD-EFGH~item~apple", amount = -2 }, new { type = "xp", amount = 250 } });
        var original = new GraphResourceEnvelope(GraphResourceKind.Task, "ST-2345-6789-ABCD-EFGH~task~task", "Task", new GraphDocument([reward]))
        { TaskMetadata = new CanonicalTaskMetadata("ST-2345-6789-ABCD-EFGH~item~apple 是背景文字") };
        var map = new ResourceCopyRemapper(); map.Add(DgrResourceKind.Item, "ST-2345-6789-ABCD-EFGH~item~apple", "ST-JKLM-NPQR-STUV-WXYZ~item~apple");
        var changed = map.Rewrite(original);
        Assert.AreEqual(original.TaskMetadata, changed.TaskMetadata);
        Assert.AreEqual("ST-JKLM-NPQR-STUV-WXYZ~item~apple", changed.Graph!.Nodes.Single().Properties["entries"][0].GetProperty("item").GetString());
        Assert.AreEqual("ST-2345-6789-ABCD-EFGH~item~apple", original.Graph!.Nodes.Single().Properties["entries"][0].GetProperty("item").GetString());
        Assert.ThrowsExactly<InvalidDataException>(() => CanonicalTaskRewardReferences.Validate(reward, GraphResourceKind.Task, new HashSet<DgrResourceKey>()));
        CanonicalTaskRewardReferences.Validate(reward, GraphResourceKind.Task, new HashSet<DgrResourceKey> { new(DgrResourceKind.Item, "ST-2345-6789-ABCD-EFGH~item~apple") });
    }
}
