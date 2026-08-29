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
        repository.Create(Manifest("story", ["actor"]));

        var info = repository.List().Single();
        var loaded = repository.Load("story");
        Assert.AreEqual("story", info.StoryId);
        Assert.AreEqual(Path.GetFullPath(repository.GetPath("story")), info.SourcePath);
        CollectionAssert.AreEqual(new[] { "actor" }, loaded.OwnedResources.Actors);

        repository.Replace(Manifest("story", ["actor", "actor_two"]));
        CollectionAssert.AreEqual(
            new[] { "actor", "actor_two" },
            repository.Load("story").OwnedResources.Actors);
    }

    [TestMethod]
    public void CollisionMissingInvalidAndFilenameMismatchFailClosed()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var repository = Repository(project);
        var manifest = Manifest("story", []);
        repository.Create(manifest);

        AssertCode(() => repository.Create(manifest), "story.membership.repository.collision");
        AssertCode(() => repository.Replace(Manifest("missing", [])), "story.membership.repository.not_found");
        AssertCode(() => repository.Load("../escape"), "story.membership.repository.story_id.invalid");

        File.WriteAllText(repository.GetPath("file_id"), Manifest("other_id", []).ToJson());
        AssertCode(() => repository.Load("file_id"), "story.membership.repository.filename.mismatch");
    }

    [TestMethod]
    public void InvalidOrLegacyManifestNeverBecomesRepositoryData()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var repository = Repository(project);
        var invalid = Manifest("story", ["same", "same"]);
        AssertCode(() => repository.Create(invalid), "story.membership.repository.data.invalid");

        Directory.CreateDirectory(repository.MembershipDirectory);
        File.WriteAllText(repository.GetPath("legacy"),
            "{\"schema_version\":2,\"id\":\"legacy\",\"owned_resources\":{}}");
        AssertCode(() => repository.Load("legacy"), "story.membership.repository.data.invalid");
    }

    [TestMethod]
    public void FailedAtomicReplacePreservesExistingManifest()
    {
        using var project = new TestProjectDirectory(createProjectFile: false);
        var repository = Repository(project);
        repository.Create(Manifest("story", ["actor"]));
        var path = repository.GetPath("story");
        var before = File.ReadAllText(path);
        var failing = new CanonicalStoryMembershipRepository(
            repository.MembershipDirectory,
            new ThrowingWriter());

        AssertCode(
            () => failing.Replace(Manifest("story", ["changed"])),
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
