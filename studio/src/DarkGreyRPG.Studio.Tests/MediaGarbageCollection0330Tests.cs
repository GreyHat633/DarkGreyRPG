using System.Security.Cryptography;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Media;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class MediaGarbageCollection0330Tests
{
    [TestMethod]
    public void SavedResourcesProtectMediaAndStartupReclaimsOnlyOrphans()
    {
        using var project = new TestProjectDirectory();
        var bytes = new byte[] { 1, 2, 3 };
        var reference = "media/" + Convert.ToHexStringLower(SHA256.HashData(bytes)) + ".png";
        var file = Path.Combine(project.Root, "resources", reference);
        Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllBytes(file, bytes);
        var repository = new ActorRepository(project.Root); var actor = repository.CreateIndividual("hero", "Hero");
        actor.HomeStoryId = "story"; actor.DefaultPortraitRef = reference; repository.SaveActor(actor);
        var orphan = Path.Combine(project.Root, "resources", "media", new string('b', 64) + ".ogg"); File.WriteAllBytes(orphan, [4, 5]);
        var work = Path.Combine(project.Root, "resources", "media_work", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(work); File.WriteAllText(Path.Combine(work, "source.wav"), "unfinished");
        // Merely changing the document does not remove bytes needed by Undo.
        actor.DefaultPortraitRef = null;
        Assert.IsTrue(File.Exists(file));
        var result = ProjectMediaGarbageCollector.CollectAtStartup(project.Root);
        Assert.IsTrue(File.Exists(file)); Assert.IsFalse(File.Exists(orphan)); Assert.IsFalse(Directory.Exists(work));
        Assert.AreEqual(1, result.RuntimeFiles);
        repository.SaveActor(actor);
        ProjectMediaGarbageCollector.CollectAtStartup(project.Root);
        Assert.IsFalse(File.Exists(file));
    }

    [TestMethod]
    public void InvalidSavedRootsAbortBeforeAnyDeletion()
    {
        using var project = new TestProjectDirectory();
        var file = Path.Combine(project.Root, "resources", "media", new string('a', 64) + ".png");
        Directory.CreateDirectory(Path.GetDirectoryName(file)!); File.WriteAllBytes(file, [1]);
        File.WriteAllText(Path.Combine(project.Root, "actors", "broken.json"), "{");
        Assert.Throws<System.Text.Json.JsonException>(() => ProjectMediaGarbageCollector.CollectAtStartup(project.Root));
        Assert.IsTrue(File.Exists(file));
    }
}
