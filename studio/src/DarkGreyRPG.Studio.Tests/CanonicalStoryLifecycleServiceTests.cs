using System.Text.Json;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.IO;
using DarkGreyRPG.Studio.Core.Stories;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CanonicalStoryLifecycleServiceTests
{
    [TestMethod]
    public void CreateWritesValidStoryAndEmptyMembership()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var story = new CanonicalStoryLifecycleService(store).Create("story_1", "Story 1");

        Assert.AreEqual(GraphResourceKind.Story, story.ResourceKind);
        Assert.AreEqual("story_1", story.Id);
        Assert.AreEqual("Story 1", story.DisplayName);
        Assert.AreEqual("start", story.Graph!.Nodes.Single().Type);
        Assert.IsEmpty(store.Memberships.Load("story_1").OwnedResources.Actors);
    }

    [TestMethod]
    public void CreateRejectsEitherExistingRoot()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        Directory.CreateDirectory(store.MembershipsDirectory);
        File.WriteAllText(store.Memberships.GetPath("story"), "occupied");

        var exception = Assert.ThrowsExactly<CanonicalStoryLifecycleException>(
            () => new CanonicalStoryLifecycleService(store).Create("story", "Story"));

        Assert.AreEqual("story.lifecycle.collision", exception.Code);
        Assert.IsFalse(File.Exists(store.Stories.GetPath("story")));
    }

    [TestMethod]
    public void RetiredTransitionCannotBeSavedOrBlockUnrelatedDeletion()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var service = new CanonicalStoryLifecycleService(store);
        service.Create("target", "Target");
        service.Create("source", "Source");
        var before = File.ReadAllBytes(store.Stories.GetPath("source"));
        Assert.ThrowsExactly<GraphResourceRepositoryException>(() => store.Stories.Replace(new GraphResourceEnvelope(
            GraphResourceKind.Story, "source", "Source",
            new GraphDocument([new GraphNode("old", "enter_story", "Old")]))));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(store.Stories.GetPath("source")));
        Assert.IsTrue(service.GetDeletionPlan("target").CanDelete);
    }

    [TestMethod]
    public void DeleteRemovesOwnedFilesAndPreservesReferencedResourcesAndLegacyStory()
    {
        using var project = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(project.Root);
        var service = new CanonicalStoryLifecycleService(store);
        service.Create("story", "Story");
        var actorRepository = new ActorRepository(project.Root);
        var actor = actorRepository.CreateActor("guard", "Guard");
        actor.HomeStoryId = "story";
        actorRepository.SaveActor(actor);
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "story",
            ownedResources: new CanonicalStoryMembershipSet { Actors = ["guard"] }));
        new StoryRepository(project.Root).CreateStory("story", "Legacy Story");

        var plan = service.GetDeletionPlan("story");
        Assert.IsTrue(plan.CanDelete);
        service.Delete("story");

        Assert.IsFalse(File.Exists(store.Stories.GetPath("story")));
        Assert.IsFalse(File.Exists(store.Memberships.GetPath("story")));
        Assert.IsFalse(File.Exists(project.ActorPath("guard")));
        Assert.IsTrue(File.Exists(Path.Combine(project.Root, "stories", "story.json")));
    }

    [TestMethod]
    public void MembershipCreateFailureCompensatesStoryRoot()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var normal = new CanonicalProjectGraphStore(project.Root);
        var failing = new CanonicalProjectGraphStore(project.Root, new FailOnWrite(2));

        var exception = Assert.ThrowsExactly<CanonicalStoryLifecycleException>(
            () => new CanonicalStoryLifecycleService(failing).Create("story", "Story"));

        Assert.AreEqual("story.lifecycle.membership_create_failed", exception.Code);
        Assert.IsFalse(File.Exists(normal.Stories.GetPath("story")));
        Assert.IsFalse(File.Exists(normal.Memberships.GetPath("story")));
    }

    [TestMethod]
    public void MissingOwnedFileBlocksDeletionWithoutChangingRoots()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var service = new CanonicalStoryLifecycleService(store);
        service.Create("story", "Story");
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "story", ownedResources: new CanonicalStoryMembershipSet { Sessions = ["missing"] }));
        var storyBytes = File.ReadAllBytes(store.Stories.GetPath("story"));

        var plan = service.GetDeletionPlan("story");

        Assert.IsFalse(plan.CanDelete);
        Assert.IsTrue(plan.Blockers.Any(item => item.ResourceId == "missing"));
        Assert.ThrowsExactly<CanonicalStoryLifecycleException>(() => service.Delete("story"));
        CollectionAssert.AreEqual(storyBytes, File.ReadAllBytes(store.Stories.GetPath("story")));
    }

    [TestMethod]
    public void OrdinaryMidDeleteFailureRestoresEverySnapshotByteForByte()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var service = new CanonicalStoryLifecycleService(store);
        service.Create("story", "Story");
        var storyPath = store.Stories.GetPath("story");
        var membershipPath = store.Memberships.GetPath("story");
        var storyBytes = File.ReadAllBytes(storyPath);
        var membershipBytes = File.ReadAllBytes(membershipPath);
        var failing = new CanonicalStoryLifecycleService(store, deleteFile: path =>
        {
            if (string.Equals(path, membershipPath, StringComparison.Ordinal))
                throw new IOException("simulated delete failure");
            File.Delete(path);
        });

        var exception = Assert.ThrowsExactly<CanonicalStoryLifecycleException>(() => failing.Delete("story"));

        Assert.AreEqual("story.lifecycle.delete_failed", exception.Code);
        CollectionAssert.AreEqual(storyBytes, File.ReadAllBytes(storyPath));
        CollectionAssert.AreEqual(membershipBytes, File.ReadAllBytes(membershipPath));
    }

    private sealed class FailOnWrite(int failureNumber) : IAtomicFileWriter
    {
        private int _writes;

        public void Write(string destinationPath, string contents, Action<string>? validateTemporaryFile = null)
        {
            if (++_writes == failureNumber) throw new IOException("simulated membership write failure");
            new AtomicFileWriter().Write(destinationPath, contents, validateTemporaryFile);
        }
    }
}
