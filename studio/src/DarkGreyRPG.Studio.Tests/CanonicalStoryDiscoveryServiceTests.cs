using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CanonicalStoryDiscoveryServiceTests
{
    [TestMethod]
    public void CompleteStoryReturnsDetachedRootsAndCanonicalDisplayName()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(Envelope(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", "Canonical Name"));
        store.Memberships.Create(new CanonicalStoryMembershipManifest("ST-2345-6789-ABCD-EFGH"));

        var snapshot = new CanonicalStoryDiscoveryService(store).Discover();
        var item = snapshot.Items.Single();

        Assert.AreEqual("ST-2345-6789-ABCD-EFGH", item.Id);
        Assert.AreEqual("Canonical Name", item.DisplayName);
        Assert.IsNotNull(item.Story);
        Assert.IsNotNull(item.Membership);
        Assert.IsTrue(item.HasStoryRoot);
        Assert.IsTrue(item.HasMembershipRoot);
        Assert.IsTrue(item.IsComplete);
        Assert.IsTrue(item.IsValid);
        Assert.IsEmpty(item.Issues);
        Assert.IsEmpty(snapshot.Issues);

        item.Story!.Graph!.Nodes.Add(new GraphNode("local", "unknown", "Local"));
        Assert.HasCount(1, new CanonicalStoryDiscoveryService(store).Discover().Items.Single().Story!.Graph!.Nodes);
    }

    [TestMethod]
    public void StoryOnlyAndMembershipOnlyIdentitiesRemainIncomplete()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(Envelope(GraphResourceKind.Story, "ST-JKLM-NPQR-STUV-WXYZ", "Story Only"));
        store.Memberships.Create(new CanonicalStoryMembershipManifest("ST-2345-6789-ABCD-EFGH"));

        var items = new CanonicalStoryDiscoveryService(store).Discover().Items;

        CollectionAssert.AreEqual(new[] { "ST-2345-6789-ABCD-EFGH", "ST-JKLM-NPQR-STUV-WXYZ" }, items.Select(item => item.Id).ToArray());
        var membershipOnly = items.Single(item => item.Id == "ST-2345-6789-ABCD-EFGH");
        Assert.IsFalse(membershipOnly.HasStoryRoot);
        Assert.IsTrue(membershipOnly.HasMembershipRoot);
        Assert.IsFalse(membershipOnly.IsComplete);
        Assert.IsFalse(membershipOnly.IsValid);
        Assert.AreEqual("ST-2345-6789-ABCD-EFGH", membershipOnly.DisplayName);
        AssertIssue(membershipOnly, "story.discovery.root.missing", "story");

        var storyOnly = items.Single(item => item.Id == "ST-JKLM-NPQR-STUV-WXYZ");
        Assert.IsTrue(storyOnly.HasStoryRoot);
        Assert.IsFalse(storyOnly.HasMembershipRoot);
        Assert.IsFalse(storyOnly.IsComplete);
        Assert.IsFalse(storyOnly.IsValid);
        AssertIssue(storyOnly, "story.discovery.root.missing", "membership");
    }

    [TestMethod]
    public void MalformedRootsRemainVisibleAndValidMembershipUsesContentIdentity()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(Envelope(GraphResourceKind.Story, "ST-5678-9ABC-DEFG-HJKL", "Valid"));
        store.Memberships.Create(new CanonicalStoryMembershipManifest("ST-5678-9ABC-DEFG-HJKL"));
        Directory.CreateDirectory(store.StoriesDirectory);
        Directory.CreateDirectory(store.MembershipsDirectory);
        File.WriteAllText(Path.Combine(store.StoriesDirectory, "ST-2345-6789-ABCD-EFGH.json"), "not json");
        store.Memberships.Create(new CanonicalStoryMembershipManifest("ST-2345-6789-ABCD-EFGH"));
        store.Stories.Create(Envelope(GraphResourceKind.Story, "ST-3456-789A-BCDE-FGHJ", "Mismatched Story"));
        File.WriteAllText(
            Path.Combine(store.MembershipsDirectory, "ST-3456-789A-BCDE-FGHJ.json"),
            CanonicalStoryMembershipSerializer.Serialize(new CanonicalStoryMembershipManifest("ST-4567-89AB-CDEF-GHJK")));

        var snapshot = new CanonicalStoryDiscoveryService(store).Discover();

        CollectionAssert.AreEqual(new[] { "ST-2345-6789-ABCD-EFGH", "ST-3456-789A-BCDE-FGHJ", "ST-4567-89AB-CDEF-GHJK", "ST-5678-9ABC-DEFG-HJKL" }, snapshot.Items.Select(item => item.Id).ToArray());
        var malformed = snapshot.Items.Single(item => item.Id == "ST-2345-6789-ABCD-EFGH");
        Assert.AreEqual("ST-2345-6789-ABCD-EFGH", malformed.DisplayName);
        Assert.IsNull(malformed.Story);
        Assert.IsNotNull(malformed.Membership);
        Assert.IsTrue(malformed.IsComplete);
        Assert.IsFalse(malformed.IsValid);
        AssertIssue(malformed, "story.discovery.root.invalid", "story");

        var mismatched = snapshot.Items.Single(item => item.Id == "ST-3456-789A-BCDE-FGHJ");
        Assert.AreEqual("Mismatched Story", mismatched.DisplayName);
        Assert.IsNotNull(mismatched.Story);
        Assert.IsNull(mismatched.Membership);
        Assert.IsFalse(mismatched.IsComplete);
        Assert.IsFalse(mismatched.IsValid);
        AssertIssue(mismatched, "story.discovery.root.missing", "membership");
        var contentOwner = snapshot.Items.Single(item => item.Id == "ST-4567-89AB-CDEF-GHJK");
        Assert.IsNotNull(contentOwner.Membership);
        AssertIssue(contentOwner, "story.discovery.root.missing", "story");

        Assert.IsTrue(snapshot.Items.Single(item => item.Id == "ST-5678-9ABC-DEFG-HJKL").IsValid);
        CollectionAssert.AreEqual(
            new[] { "ST-2345-6789-ABCD-EFGH", "ST-3456-789A-BCDE-FGHJ", "ST-4567-89AB-CDEF-GHJK" },
            snapshot.Issues.Select(issue => issue.StoryId).ToArray());
    }

    [TestMethod]
    public void AbsentCanonicalRootReturnsEmptySnapshotWithoutCreatingDirectories()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);

        var snapshot = new CanonicalStoryDiscoveryService(store).Discover();

        Assert.IsEmpty(snapshot.Items);
        Assert.IsEmpty(snapshot.Issues);
        Assert.IsFalse(Directory.Exists(store.CanonicalDirectory));
    }

    private static void AssertIssue(
        CanonicalStoryDiscoveryItem item,
        string code,
        string root)
    {
        var issue = item.Issues.Single();
        Assert.AreEqual(item.Id, issue.StoryId);
        Assert.AreEqual(code, issue.Code);
        StringAssert.Contains(issue.Message, $"root '{root}'");
    }

    private static GraphResourceEnvelope Envelope(GraphResourceKind kind, string id, string displayName)
        => new(kind, id, displayName, new GraphDocument([new GraphNode("node", "unknown", "Node")]));
}
