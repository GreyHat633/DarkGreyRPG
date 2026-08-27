using System.Security.Cryptography;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.Core.Validation;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class StoryFlowRecoveryStoreTests
{
    [TestMethod]
    public void RecoveryDirectoryIsEditorOnlyAndAbsentEnumerationIsSafe()
    {
        using var project = new TestProjectDirectory();
        var store = new StoryFlowRecoveryStore(project.Root);

        Assert.AreEqual(Path.Combine(project.Root, "resources", "editor", "recovery"), store.RecoveryDirectory);
        Assert.AreEqual(0, store.Enumerate().Count);
        Assert.IsNull(store.Load("missing_story"));
        Assert.IsFalse(Directory.Exists(Path.Combine(project.Root, "stories")));
    }

    [TestMethod]
    public void InvalidDraftRoundTripsAsADeepClonedRecoverySnapshot()
    {
        using var project = new TestProjectDirectory();
        var store = new StoryFlowRecoveryStore(project.Root);
        var draft = new StoryResource
        {
            Id = "broken_flow",
            DisplayName = "",
            Entry = "missing",
            Nodes = [],
            Connections = [new StoryConnectionResource { From = "missing", Output = "next", To = "nowhere" }],
        };

        var saved = store.Save(draft, @"C:\projects\stories\broken_flow.json");
        draft.Tags.Add("mutated-after-save");
        var loaded = store.Load("broken_flow");

        Assert.IsNotNull(loaded);
        Assert.AreEqual(saved.StoryId, loaded!.StoryId);
        Assert.AreEqual(saved.SourcePath, loaded.SourcePath);
        Assert.AreEqual("broken_flow", loaded.Resource!.Id);
        Assert.AreEqual(0, loaded.Resource.Tags.Count);
        Assert.AreEqual("missing", loaded.Resource.Entry);
        Assert.IsTrue(StoryValidator.Validate(loaded.Resource).Any(issue => issue.Severity == ValidationSeverity.Error));
        Assert.AreEqual(1, store.Enumerate().Count);
    }

    [TestMethod]
    public void MalformedSnapshotDoesNotHideValidSnapshots()
    {
        using var project = new TestProjectDirectory();
        var store = new StoryFlowRecoveryStore(project.Root);
        store.Save(new StoryResource { Id = "valid_story", Title = "Valid" });
        Directory.CreateDirectory(store.RecoveryDirectory);
        File.WriteAllText(Path.Combine(store.RecoveryDirectory, "malformed.json"), "{ not json }");
        File.WriteAllText(Path.Combine(store.RecoveryDirectory, "mismatched.json"),
            "{\"schema_version\":1,\"story_id\":\"mismatched\",\"captured_utc\":\"2026-08-26T00:00:00Z\",\"resource\":{\"schema_version\":2,\"id\":\"other\"}}");

        var snapshots = store.Enumerate();

        Assert.AreEqual(1, snapshots.Count);
        Assert.AreEqual("valid_story", snapshots[0].StoryId);
        Assert.IsNull(store.Load("malformed"));
        Assert.IsNull(store.Load("mismatched"));
    }

    [TestMethod]
    public void DeleteRemovesOnlyTheValidatedOneStorySnapshot()
    {
        using var project = new TestProjectDirectory();
        var store = new StoryFlowRecoveryStore(project.Root);
        store.Save(new StoryResource { Id = "first_story" });
        store.Save(new StoryResource { Id = "second_story" });

        Assert.IsTrue(store.Delete("first_story"));
        Assert.IsFalse(store.Delete("first_story"));
        Assert.IsNotNull(store.Load("second_story"));
        Assert.ThrowsExactly<ArgumentException>(() => store.Delete("../second_story"));
        Assert.ThrowsExactly<ArgumentException>(() => store.Delete(""));
        Assert.ThrowsExactly<ArgumentException>(() => store.Delete("UPPER"));
        Assert.IsTrue(File.Exists(Path.Combine(store.RecoveryDirectory, "second_story.json")));
    }

    [TestMethod]
    public void RecoveryDoesNotChangeOfficialStoryJson()
    {
        using var project = new TestProjectDirectory();
        var official = new StoryRepository(project.Root);
        var resource = new StoryResource { Id = "official_story", DisplayName = "Official" };
        official.SaveStory(resource);
        var officialPath = Path.Combine(official.StoriesDirectory, "official_story.json");
        var before = SHA256.HashData(File.ReadAllBytes(officialPath));

        new StoryFlowRecoveryStore(project.Root).Save(new StoryResource
        {
            Id = resource.Id,
            DisplayName = "Invalid draft",
            Entry = "not-there",
            Nodes = [],
        });

        CollectionAssert.AreEqual(before, SHA256.HashData(File.ReadAllBytes(officialPath)));
        Assert.IsFalse(File.Exists(Path.Combine(project.Root, "stories", "invalid draft.json")));
    }

    [TestMethod]
    public void RuntimeStoryRepositoryDoesNotEnumerateRecoveryOnlyDrafts()
    {
        using var project = new TestProjectDirectory();
        new StoryFlowRecoveryStore(project.Root).Save(new StoryResource { Id = "recovery_only", Entry = "missing" });

        Assert.IsEmpty(new StoryRepository(project.Root).ListStories());
    }
}
