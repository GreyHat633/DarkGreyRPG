using DarkGreyRPG.Studio.Core.Identity;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class StoryResourceCopyMapTests
{
    private static readonly StoryUid A = StoryUid.Parse("ST-2345-6789-ABCD-EFGH");
    private static readonly StoryUid B = StoryUid.Parse("ST-JKLM-NPQR-STUV-WXYZ");
    private static readonly StoryUid External = StoryUid.Parse("ST-ZZZZ-ZZZZ-ZZZZ-ZZZZ");

    [TestMethod]
    public void NonemptyTargetKeepsExistingAddressesAndOnlyCollidingCopiesChangeLocalId()
    {
        ResourceAddress[] source = [new(A, ResourceKind.Actor, "r17"), new(A, ResourceKind.Item, "r17"), new(A, ResourceKind.Task, "r1")];
        ResourceAddress[] target = [new(B, ResourceKind.Actor, "r17")];
        var map = StoryResourceCopyMap.Create(A, B, source, target);
        Assert.HasCount(3, map.Copies);
        Assert.AreNotEqual("r17", map.Resolve(source[0]).LocalId);
        Assert.AreEqual("r17", map.Resolve(source[1]).LocalId);
        Assert.AreEqual("r1", map.Resolve(source[2]).LocalId);
        Assert.IsTrue(map.Copies.Values.All(value => value.StoryUid == B));
        Assert.AreEqual(new ResourceAddress(B, ResourceKind.Actor, "r17"), target[0]);
        Assert.IsTrue(source.All(value => value.StoryUid == A));
        Assert.AreEqual(map.Resolve(source[0]), map.Resolve(new(A, ResourceKind.Actor, "r17")));
        var reference = new ResourceAddress(External, ResourceKind.Actor, "r17");
        Assert.AreSame(reference, map.Resolve(reference));
    }

    [TestMethod]
    public void RepeatedCopyAllocatesASeparateBatchAndDoesNotChangeThePreviousMap()
    {
        ResourceAddress[] source = [new(A, ResourceKind.Actor, "r17"), new(A, ResourceKind.Session, "r17")];
        var first = StoryResourceCopyMap.Create(A, B, source, []);
        var firstSnapshot = first.Copies.Values.ToHashSet();
        var second = StoryResourceCopyMap.Create(A, B, source, firstSnapshot);
        Assert.IsFalse(second.Copies.Values.Any(firstSnapshot.Contains));
        Assert.IsTrue(firstSnapshot.SetEquals(first.Copies.Values));
        Assert.HasCount(2, second.Copies);
    }

    [TestMethod]
    public void InvalidOwnershipAndSelfCopyFailBeforeProducingAMap()
    {
        var source = new ResourceAddress(A, ResourceKind.Actor, "r17");
        Assert.ThrowsExactly<ArgumentException>(() => StoryResourceCopyMap.Create(A, A, [source], []));
        Assert.ThrowsExactly<ArgumentException>(() => StoryResourceCopyMap.Create(A, B, [source, source], []));
        Assert.ThrowsExactly<ArgumentException>(() => StoryResourceCopyMap.Create(A, B, [new(External, ResourceKind.Actor, "r17")], []));
        Assert.ThrowsExactly<ArgumentException>(() => StoryResourceCopyMap.Create(A, B, [source], [source]));
    }
}
