using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class GraphGroup0333Tests
{
    private static GraphCommentFrame Group(string id, params string[] nodes) => new(id, id, 0, 0, 100, 100, nodes);
    [TestMethod]
    public void CombinationTablePreservesExplicitObjectTypes()
    {
        var original = new[] { Group("A", "a", "b"), Group("B", "c", "d") };
        var added = GraphGroupOperations.Combine(original, ["a", "x"], [], "new");
        CollectionAssert.AreEquivalent(new[] { "a", "b", "x" }, added.Single(f => f.Id == "A").Members);
        Assert.AreEqual(2, added.Length);
        var transfer = GraphGroupOperations.Combine(original, ["c"], ["A"], "new");
        CollectionAssert.AreEqual(new[] { "d" }, transfer.Single(f => f.Id == "B").Members);
        var fused = GraphGroupOperations.Combine(original, ["c", "d"], ["A"], "new");
        Assert.AreEqual(1, fused.Length);
        var nested = GraphGroupOperations.Combine(original, ["x"], ["A", "B"], "C");
        CollectionAssert.AreEquivalent(new[] { "A", "B" }, nested.Single(f => f.Id == "C").Groups);
        GraphGroupOperations.Validate(nested, ["a", "b", "c", "d", "x"]);
        var split = GraphGroupOperations.Ungroup(nested, "C");
        Assert.AreEqual(2, split.Length);
        CollectionAssert.AreEqual(original[0].Members, split[0].Members);
        var multiSource = GraphGroupOperations.Combine(original, ["a", "c"], [], "N");
        Assert.AreEqual("b", multiSource.Single(f => f.Id == "A").Members.Single());
        Assert.AreEqual("d", multiSource.Single(f => f.Id == "B").Members.Single());
        CollectionAssert.AreEquivalent(new[] { "a", "c" }, multiSource.Single(f => f.Id == "N").Members);
    }
    [TestMethod]
    public void AncestorNormalizationNoopAndCycleValidation()
    {
        var original = new[] { Group("A", "a", "b") };
        Assert.AreEqual(1, GraphGroupOperations.Combine(original, ["a", "b"], [], "N").Length);
        Assert.AreEqual(0, GraphGroupOperations.Combine([], ["a"], [], "N").Length);
        var nested = new[] { Group("A", "a"), Group("B") with { Groups = ["A"] } };
        Assert.AreEqual(2, GraphGroupOperations.Combine(nested, [], ["A", "B"], "N").Length);
        Assert.ThrowsExactly<ArgumentException>(() => GraphGroupOperations.Validate(
            [Group("A") with { Groups = ["B"] }, Group("B") with { Groups = ["A"] }], []));
        Assert.ThrowsExactly<ArgumentException>(() => GraphGroupOperations.Validate([Group("A", "a"), Group("B", "a")], ["a"]));
        var removed = GraphGroupOperations.MoveNodes(nested, ["a"], null);
        Assert.AreEqual(0, removed.Length);
    }
    [TestMethod]
    public void UngroupPromotesOnlyOneLevel()
    {
        var frames = new[] { Group("A", "a"), Group("B", "b") with { Groups = ["A"] }, Group("C", "c") with { Groups = ["B"] } };
        var result = GraphGroupOperations.Ungroup(frames, "B");
        GraphGroupOperations.Validate(result, ["a", "b", "c"]);
        Assert.AreEqual("A", result.Single(f => f.Id == "C").Groups.Single());
        CollectionAssert.AreEquivalent(new[] { "b", "c" }, result.Single(f => f.Id == "C").Members);
        Assert.AreEqual("a", result.Single(f => f.Id == "A").Members.Single());
    }
}
