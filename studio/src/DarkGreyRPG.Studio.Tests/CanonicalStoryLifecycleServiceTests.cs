using System.Text.Json;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.IO;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CanonicalStoryLifecycleServiceTests
{
    [TestMethod]
    public void CreateWritesValidStoryAndEmptyMembership()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var story = new CanonicalStoryLifecycleService(store).Create("ST-2345-6789-ABCD-EFGH", "Story 1");

        Assert.AreEqual(GraphResourceKind.Story, story.ResourceKind);
        Assert.AreEqual("ST-2345-6789-ABCD-EFGH", story.Id);
        Assert.AreEqual("Story 1", story.DisplayName);
        Assert.AreEqual("start", story.Graph!.Nodes.Single().Type);
        Assert.IsEmpty(store.Memberships.Load("ST-2345-6789-ABCD-EFGH").OwnedResources.Actors);
    }

    [TestMethod]
    public void CreateRejectsEitherExistingRoot()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        Directory.CreateDirectory(store.MembershipsDirectory);
        File.WriteAllText(store.Memberships.GetPath("ST-2345-6789-ABCD-EFGH"), "occupied");

        var exception = Assert.ThrowsExactly<CanonicalStoryLifecycleException>(
            () => new CanonicalStoryLifecycleService(store).Create("ST-2345-6789-ABCD-EFGH", "Story"));

        Assert.AreEqual("story.lifecycle.collision", exception.Code);
        Assert.IsFalse(File.Exists(store.Stories.GetPath("ST-2345-6789-ABCD-EFGH")));
    }

    [TestMethod]
    public void RetiredTransitionCannotBeSavedOrBlockUnrelatedDeletion()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var service = new CanonicalStoryLifecycleService(store);
        service.Create("ST-2345-6789-ABCD-EFGH", "Target");
        service.Create("ST-JKLM-NPQR-STUV-WXYZ", "Source");
        var before = File.ReadAllBytes(store.Stories.GetPath("ST-JKLM-NPQR-STUV-WXYZ"));
        Assert.ThrowsExactly<GraphResourceRepositoryException>(() => store.Stories.Replace(new GraphResourceEnvelope(
            GraphResourceKind.Story, "ST-JKLM-NPQR-STUV-WXYZ", "Source",
            new GraphDocument([new GraphNode("old", "enter_story", "Old")]))));
        CollectionAssert.AreEqual(before, File.ReadAllBytes(store.Stories.GetPath("ST-JKLM-NPQR-STUV-WXYZ")));
        Assert.IsTrue(service.GetDeletionPlan("ST-2345-6789-ABCD-EFGH").CanDelete);
    }

    [TestMethod]
    public void DeleteRemovesOwnedFilesAndPreservesCurrentReferencesAndExistingEmptyDirectories()
    {
        using var project = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(project.Root);
        var service = new CanonicalStoryLifecycleService(store);
        service.Create("ST-2345-6789-ABCD-EFGH", "Story");
        var actorRepository = new ActorRepository(project.Root);
        var actor = actorRepository.CreateActor("ST-2345-6789-ABCD-EFGH~actor~guard", "Guard");
        actor.HomeStoryId = "ST-2345-6789-ABCD-EFGH";
        actorRepository.SaveActor(actor);
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "ST-2345-6789-ABCD-EFGH",
            ownedResources: new CanonicalStoryMembershipSet { Actors = ["ST-2345-6789-ABCD-EFGH~actor~guard"] }));
        service.Create("ST-JKLM-NPQR-STUV-WXYZ", "Provider");
        var actors = new CanonicalStoryActorLifecycleService(store, actorRepository);
        actors.CreateOwned("ST-JKLM-NPQR-STUV-WXYZ", "ST-JKLM-NPQR-STUV-WXYZ~actor~shared", "Shared");
        actors.AddReference("ST-2345-6789-ABCD-EFGH", "ST-JKLM-NPQR-STUV-WXYZ~actor~shared");
        Directory.CreateDirectory(Path.Combine(project.Root, "stories"));

        var plan = service.GetDeletionPlan("ST-2345-6789-ABCD-EFGH");
        Assert.IsTrue(plan.CanDelete);
        service.Delete("ST-2345-6789-ABCD-EFGH");

        Assert.IsFalse(File.Exists(store.Stories.GetPath("ST-2345-6789-ABCD-EFGH")));
        Assert.IsFalse(File.Exists(store.Memberships.GetPath("ST-2345-6789-ABCD-EFGH")));
        Assert.IsFalse(File.Exists(actorRepository.GetActorPath("ST-2345-6789-ABCD-EFGH~actor~guard")));
        Assert.IsTrue(Directory.Exists(Path.Combine(project.Root, "stories")));
        Assert.IsTrue(File.Exists(actorRepository.GetActorPath("ST-JKLM-NPQR-STUV-WXYZ~actor~shared")));
        Assert.IsTrue(File.Exists(store.Stories.GetPath("ST-JKLM-NPQR-STUV-WXYZ")));
    }

    [TestMethod]
    public void MembershipCreateFailureCompensatesStoryRoot()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var normal = new CanonicalProjectGraphStore(project.Root);
        var failing = new CanonicalProjectGraphStore(project.Root, new FailOnWrite(2));

        var exception = Assert.ThrowsExactly<CanonicalStoryLifecycleException>(
            () => new CanonicalStoryLifecycleService(failing).Create("ST-2345-6789-ABCD-EFGH", "Story"));

        Assert.AreEqual("story.lifecycle.membership_create_failed", exception.Code);
        Assert.IsFalse(File.Exists(normal.Stories.GetPath("ST-2345-6789-ABCD-EFGH")));
        Assert.IsFalse(File.Exists(normal.Memberships.GetPath("ST-2345-6789-ABCD-EFGH")));
    }

    [TestMethod]
    public void MissingOwnedFileBlocksDeletionWithoutChangingRoots()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var service = new CanonicalStoryLifecycleService(store);
        service.Create("ST-2345-6789-ABCD-EFGH", "Story");
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "ST-2345-6789-ABCD-EFGH", ownedResources: new CanonicalStoryMembershipSet { Sessions = ["ST-2345-6789-ABCD-EFGH~session~missing"] }));
        var storyBytes = File.ReadAllBytes(store.Stories.GetPath("ST-2345-6789-ABCD-EFGH"));

        var plan = service.GetDeletionPlan("ST-2345-6789-ABCD-EFGH");

        Assert.IsFalse(plan.CanDelete);
        Assert.IsTrue(plan.Blockers.Any(item => item.ResourceId == "ST-2345-6789-ABCD-EFGH~session~missing"));
        Assert.ThrowsExactly<CanonicalStoryLifecycleException>(() => service.Delete("ST-2345-6789-ABCD-EFGH"));
        CollectionAssert.AreEqual(storyBytes, File.ReadAllBytes(store.Stories.GetPath("ST-2345-6789-ABCD-EFGH")));
    }

    [TestMethod]
    public void OrdinaryMidDeleteFailureRestoresEverySnapshotByteForByte()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var store = new CanonicalProjectGraphStore(project.Root);
        var service = new CanonicalStoryLifecycleService(store);
        service.Create("ST-2345-6789-ABCD-EFGH", "Story");
        var storyPath = store.Stories.GetPath("ST-2345-6789-ABCD-EFGH");
        var membershipPath = store.Memberships.GetPath("ST-2345-6789-ABCD-EFGH");
        var storyBytes = File.ReadAllBytes(storyPath);
        var membershipBytes = File.ReadAllBytes(membershipPath);
        var failing = new CanonicalStoryLifecycleService(store, deleteFile: path =>
        {
            if (string.Equals(path, membershipPath, StringComparison.Ordinal))
                throw new IOException("simulated delete failure");
            File.Delete(path);
        });

        var exception = Assert.ThrowsExactly<CanonicalStoryLifecycleException>(() => failing.Delete("ST-2345-6789-ABCD-EFGH"));

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
