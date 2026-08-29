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
        store.Stories.Create(Envelope(GraphResourceKind.Story, "story", "Canonical Name"));
        store.Memberships.Create(new CanonicalStoryMembershipManifest("story"));

        var snapshot = new CanonicalStoryDiscoveryService(store).Discover();
        var item = snapshot.Items.Single();

        Assert.AreEqual("story", item.Id);
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
        store.Stories.Create(Envelope(GraphResourceKind.Story, "story_only", "Story Only"));
        store.Memberships.Create(new CanonicalStoryMembershipManifest("membership_only"));

        var items = new CanonicalStoryDiscoveryService(store).Discover().Items;

        CollectionAssert.AreEqual(new[] { "membership_only", "story_only" }, items.Select(item => item.Id).ToArray());
        var membershipOnly = items.Single(item => item.Id == "membership_only");
        Assert.IsFalse(membershipOnly.HasStoryRoot);
        Assert.IsTrue(membershipOnly.HasMembershipRoot);
        Assert.IsFalse(membershipOnly.IsComplete);
        Assert.IsFalse(membershipOnly.IsValid);
        Assert.AreEqual("membership_only", membershipOnly.DisplayName);
        AssertIssue(membershipOnly, "story.discovery.root.missing", "story");

        var storyOnly = items.Single(item => item.Id == "story_only");
        Assert.IsTrue(storyOnly.HasStoryRoot);
        Assert.IsFalse(storyOnly.HasMembershipRoot);
        Assert.IsFalse(storyOnly.IsComplete);
        Assert.IsFalse(storyOnly.IsValid);
        AssertIssue(storyOnly, "story.discovery.root.missing", "membership");
    }

    [TestMethod]
    public void InvalidRootsKeepFilenameIdentityAndDoNotHideValidEntries()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(Envelope(GraphResourceKind.Story, "valid", "Valid"));
        store.Memberships.Create(new CanonicalStoryMembershipManifest("valid"));
        Directory.CreateDirectory(store.StoriesDirectory);
        Directory.CreateDirectory(store.MembershipsDirectory);
        File.WriteAllText(Path.Combine(store.StoriesDirectory, "malformed.json"), "not json");
        store.Memberships.Create(new CanonicalStoryMembershipManifest("malformed"));
        store.Stories.Create(Envelope(GraphResourceKind.Story, "mismatched", "Mismatched Story"));
        File.WriteAllText(
            Path.Combine(store.MembershipsDirectory, "mismatched.json"),
            CanonicalStoryMembershipSerializer.Serialize(new CanonicalStoryMembershipManifest("other")));

        var snapshot = new CanonicalStoryDiscoveryService(store).Discover();

        CollectionAssert.AreEqual(new[] { "malformed", "mismatched", "valid" }, snapshot.Items.Select(item => item.Id).ToArray());
        var malformed = snapshot.Items.Single(item => item.Id == "malformed");
        Assert.AreEqual("malformed", malformed.DisplayName);
        Assert.IsNull(malformed.Story);
        Assert.IsNotNull(malformed.Membership);
        Assert.IsTrue(malformed.IsComplete);
        Assert.IsFalse(malformed.IsValid);
        AssertIssue(malformed, "story.discovery.root.invalid", "story");

        var mismatched = snapshot.Items.Single(item => item.Id == "mismatched");
        Assert.AreEqual("Mismatched Story", mismatched.DisplayName);
        Assert.IsNotNull(mismatched.Story);
        Assert.IsNull(mismatched.Membership);
        Assert.IsTrue(mismatched.IsComplete);
        Assert.IsFalse(mismatched.IsValid);
        AssertIssue(mismatched, "story.discovery.root.invalid", "membership");

        Assert.IsTrue(snapshot.Items.Single(item => item.Id == "valid").IsValid);
        CollectionAssert.AreEqual(
            new[] { "malformed", "mismatched" },
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
