using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.IO;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CanonicalStoryMembershipRepositoryTests
{
    [TestMethod]
    public void CreateListLoadAndReplaceUseStableStoryFilename()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var repository = Repository(project);
        repository.Create(Manifest("ST-2345-6789-ABCD-EFG2", ["ST-2345-6789-ABCD-EFG2~actor~actor"]));

        var info = repository.List().Single();
        var loaded = repository.Load("ST-2345-6789-ABCD-EFG2");
        Assert.AreEqual("ST-2345-6789-ABCD-EFG2", info.StoryId);
        Assert.AreEqual(Path.GetFullPath(repository.GetPath("ST-2345-6789-ABCD-EFG2")), info.SourcePath);
        CollectionAssert.AreEqual(new[] { "ST-2345-6789-ABCD-EFG2~actor~actor" }, loaded.OwnedResources.Actors);

        repository.Replace(Manifest("ST-2345-6789-ABCD-EFG2", ["ST-2345-6789-ABCD-EFG2~actor~actor", "ST-2345-6789-ABCD-EFG2~actor~actor_two"]));
        CollectionAssert.AreEqual(
            new[] { "ST-2345-6789-ABCD-EFG2~actor~actor", "ST-2345-6789-ABCD-EFG2~actor~actor_two" },
            repository.Load("ST-2345-6789-ABCD-EFG2").OwnedResources.Actors);
    }

    [TestMethod]
    public void CollisionMissingInvalidAndFilenameMismatchFailClosed()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var repository = Repository(project);
        var manifest = Manifest("ST-2345-6789-ABCD-EFG2", []);
        repository.Create(manifest);

        AssertCode(() => repository.Create(manifest), "story.membership.repository.collision");
        AssertCode(() => repository.Replace(Manifest("ST-2345-6789-ABCD-EFG3", [])), "story.membership.repository.not_found");
        AssertCode(() => repository.Load("../escape"), "story.membership.repository.story_id.invalid");

        File.WriteAllText(repository.GetPath("ST-2345-6789-ABCD-EFG4"), Manifest("ST-2345-6789-ABCD-EFG5", []).ToJson());
        AssertCode(() => repository.Load("ST-2345-6789-ABCD-EFG4"), "story.membership.repository.path.occupied");
        AssertCode(() => repository.Create(Manifest("ST-2345-6789-ABCD-EFG4", [])), "story.membership.repository.path.occupied");
        Assert.AreEqual("ST-2345-6789-ABCD-EFG5", repository.Load("ST-2345-6789-ABCD-EFG5").StoryId);
    }

    [TestMethod]
    public void InvalidOrLegacyManifestNeverBecomesRepositoryData()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var repository = Repository(project);
        var invalid = Manifest("ST-2345-6789-ABCD-EFG2", ["ST-2345-6789-ABCD-EFG2~actor~same", "ST-2345-6789-ABCD-EFG2~actor~same"]);
        AssertCode(() => repository.Create(invalid), "story.membership.repository.data.invalid");

        Directory.CreateDirectory(repository.MembershipDirectory);
        File.WriteAllText(repository.GetPath("ST-2345-6789-ABCD-EFG6"),
            "{\"schema_version\":2,\"id\":\"legacy\",\"owned_resources\":{}}");
        AssertCode(() => repository.Load("ST-2345-6789-ABCD-EFG6"), "story.membership.repository.data.invalid");
    }

    [TestMethod]
    public void FailedAtomicReplacePreservesExistingManifest()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var repository = Repository(project);
        repository.Create(Manifest("ST-2345-6789-ABCD-EFG2", ["ST-2345-6789-ABCD-EFG2~actor~actor"]));
        var path = repository.GetPath("ST-2345-6789-ABCD-EFG2");
        var before = File.ReadAllText(path);
        var failing = new CanonicalStoryMembershipRepository(
            repository.MembershipDirectory,
            new ThrowingWriter());

        AssertCode(
            () => failing.Replace(Manifest("ST-2345-6789-ABCD-EFG2", ["ST-2345-6789-ABCD-EFG2~actor~changed"])),
            "story.membership.repository.write.failed");
        Assert.AreEqual(before, File.ReadAllText(path));
    }

    private static CanonicalStoryMembershipRepository Repository(TestProjectDirectory project)
        => new(Path.Combine(project.Root, "canonical", "memberships"));

    private static CanonicalStoryMembershipManifest Manifest(string storyId, string[] actors)
        => new(storyId, new CanonicalStoryMembershipSet { Actors = [.. actors] });

    private static void AssertCode(Action action, string code)
        => Assert.AreEqual(code,
            Assert.ThrowsExactly<CanonicalStoryMembershipRepositoryException>(action).Code);

    private sealed class ThrowingWriter : IAtomicFileWriter
    {
        public void Write(string destinationPath, string contents, Action<string>? validateTemporaryFile = null)
            => throw new IOException("simulated");
    }
}
