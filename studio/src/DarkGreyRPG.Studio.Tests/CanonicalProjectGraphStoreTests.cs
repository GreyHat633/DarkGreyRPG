using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CanonicalProjectGraphStoreTests
{
    [TestMethod]
    public void ExactLayoutIsIsolatedUnderResourcesCanonical()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var root = Path.Combine(project.Root, "resources", "canonical");

        Assert.AreEqual(Path.GetFullPath(root), store.CanonicalDirectory);
        Assert.AreEqual(Path.Combine(root, "stories"), store.StoriesDirectory);
        Assert.AreEqual(Path.Combine(root, "sessions"), store.SessionsDirectory);
        Assert.AreEqual(Path.Combine(root, "tasks"), store.TasksDirectory);
        Assert.AreEqual(Path.Combine(root, "memberships"), store.MembershipsDirectory);
        Assert.AreEqual(GraphResourceKind.Story, store.Stories.ExpectedKind);
        Assert.AreEqual(GraphResourceKind.Session, store.Sessions.ExpectedKind);
        Assert.AreEqual(GraphResourceKind.Task, store.Tasks.ExpectedKind);
    }

    [TestMethod]
    public void ConstructionAndInspectionDoNotCreateDirectories()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);

        Assert.IsFalse(store.IsInitialized);
        Assert.IsFalse(store.HasCanonicalData);
        Assert.IsEmpty(store.Stories.List());
        Assert.IsEmpty(store.Memberships.List());
        Assert.IsFalse(Directory.Exists(store.CanonicalDirectory));
    }

    [TestMethod]
    public void ExplicitInitializationCreatesOnlyCanonicalSubdirectories()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        store.EnsureDirectories();

        Assert.IsTrue(store.IsInitialized);
        Assert.IsFalse(Directory.Exists(Path.Combine(project.Root, "dialogues")));
        Assert.IsFalse(Directory.Exists(Path.Combine(project.Root, "quests")));
        CollectionAssert.AreEquivalent(
            new[] { "memberships", "sessions", "stories", "tasks" },
            Directory.EnumerateDirectories(store.CanonicalDirectory)
                .Select(Path.GetFileName).ToArray());
    }

    [TestMethod]
    public void KindRepositoriesAndMembershipFilesRemainSeparated()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(Envelope(GraphResourceKind.Story, "story"));
        store.Sessions.Create(Envelope(GraphResourceKind.Session, "session"));
        store.Tasks.Create(Envelope(GraphResourceKind.Task, "task"));
        store.Memberships.Create(new CanonicalStoryMembershipManifest("story"));

        Assert.IsTrue(store.HasCanonicalData);
        Assert.AreEqual("story", store.Stories.List().Single().Id);
        Assert.AreEqual("session", store.Sessions.List().Single().Id);
        Assert.AreEqual("task", store.Tasks.List().Single().Id);
        Assert.AreEqual("story", store.Memberships.List().Single().StoryId);
        Assert.HasCount(1, Directory.EnumerateFiles(store.StoriesDirectory).ToArray());
        Assert.HasCount(1, Directory.EnumerateFiles(store.MembershipsDirectory).ToArray());
    }

    private static GraphResourceEnvelope Envelope(GraphResourceKind kind, string id)
        => new(kind, id, id, new GraphDocument());
}
