using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class StoryGroupCatalogTests
{
    private static CanonicalStoryLogicConnection Edge(string from, string to, string kind = "Flow") => new(from, "out", to, "in", kind);
    private static CanonicalStoryLogicGraph Graph(params CanonicalStoryLogicConnection[] edges) => new(2, edges);

    [TestMethod]
    public void PersistedNamesDoNotCreateMembershipAndRejectDuplicateFields()
    {
        var before = StoryGroupCatalog.Derive(["a", "b"], Graph(Edge("a", "b")));
        before = before.Rename(before.Groups[0].Key, "保留名称");
        var restored = StoryGroupNameStore.Parse(StoryGroupNameStore.Serialize(before));
        Assert.AreEqual("保留名称", StoryGroupCatalog.Derive(["a", "b"], Graph(Edge("a", "b")), restored).Groups.Single().DisplayName);
        Assert.IsEmpty(StoryGroupCatalog.Derive(["a", "b"], Graph(), restored).Groups);
        Assert.ThrowsExactly<InvalidDataException>(() => StoryGroupNameStore.Parse("{\"schema_version\":1,\"schema_version\":1,\"next_ordinal\":1,\"names\":[]}"));
        Assert.ThrowsExactly<InvalidDataException>(() => StoryGroupNameStore.Parse("{\"schema_version\":1,\"next_ordinal\":1,\"names\":[{\"previous_members\":[\"a\",\"a\"],\"display_name\":\"bad\"}]}"));
    }

    [TestMethod]
    public void FlowAndLogicUseUndirectedComponentsAndSelfLoopStaysSingle()
    {
        var snapshot = StoryGroupCatalog.Derive(["a", "b", "c", "d"], Graph(Edge("a", "b"), Edge("c", "b", "Logic"), Edge("d", "d")));
        Assert.HasCount(1, snapshot.Groups);
        CollectionAssert.AreEqual(new[] { "a", "b", "c" }, snapshot.Groups[0].Members.ToArray());
        CollectionAssert.AreEqual(new[] { "d" }, snapshot.Singles.ToArray());
        var reversed = StoryGroupCatalog.Derive(["d", "c", "b", "a"], Graph(Edge("b", "a"), Edge("b", "c", "Logic")));
        Assert.AreEqual(snapshot.Groups[0].Key, reversed.Groups[0].Key);
    }

    [TestMethod]
    public void ExpansionRetainsNameButMergeAndSplitGetFreshNames()
    {
        var start = StoryGroupCatalog.Derive(["a", "b", "c", "d", "e"], Graph(Edge("a", "b")));
        start = start.Rename(start.Groups[0].Key, "主线");
        var expanded = StoryGroupCatalog.Derive(["a", "b", "c", "d", "e"], Graph(Edge("a", "b"), Edge("b", "c")), start);
        Assert.AreEqual("主线", expanded.Groups[0].DisplayName);
        var two = StoryGroupCatalog.Derive(["a", "b", "c", "d", "e"], Graph(Edge("a", "b"), Edge("b", "c"), Edge("d", "e")), expanded);
        var merged = StoryGroupCatalog.Derive(["a", "b", "c", "d", "e"], Graph(Edge("a", "b"), Edge("b", "c"), Edge("c", "d"), Edge("d", "e")), two);
        Assert.AreNotEqual("主线", merged.Groups[0].DisplayName);
        Assert.IsFalse(two.Groups.Any(group => group.DisplayName == merged.Groups[0].DisplayName));
        var split = StoryGroupCatalog.Derive(["a", "b", "c", "d", "e"], Graph(Edge("a", "b"), Edge("c", "d")), merged);
        Assert.HasCount(2, split.Groups);
        Assert.IsTrue(split.Groups.All(group => group.DisplayName != merged.Groups[0].DisplayName));
        CollectionAssert.AreEqual(new[] { "e" }, split.Singles.ToArray());
        Assert.AreEqual("主线", start.Groups[0].DisplayName, "Undo snapshot must remain untouched.");
    }

    [TestMethod]
    public void RemovingNonBridgeKeepsNameAndMissingEndpointIsNotSilentlyIgnored()
    {
        var connected = StoryGroupCatalog.Derive(["a", "b", "c"], Graph(Edge("a", "b"), Edge("b", "c"), Edge("c", "a", "Logic")));
        connected = connected.Rename(connected.Groups[0].Key, "环");
        var tree = StoryGroupCatalog.Derive(["a", "b", "c"], Graph(Edge("a", "b"), Edge("b", "c")), connected);
        Assert.AreEqual("环", tree.Groups[0].DisplayName);
        Assert.ThrowsExactly<ArgumentException>(() => StoryGroupCatalog.Derive(["a"], Graph(Edge("a", "b"))));
        Assert.ThrowsExactly<ArgumentException>(() => StoryGroupCatalog.Derive(["a", "a"], Graph()));
        Assert.ThrowsExactly<ArgumentException>(() => StoryGroupCatalog.Derive(["a", "b"], Graph(Edge("a", "b", "Other"))));
    }
}
