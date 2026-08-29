using System.Text.Json;
using DarkGreyRPG.Studio.Core.Packaging;
using DarkGreyRPG.Studio.Core.Stories;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class StoryPackageTests
{
    [TestMethod]
    public void BuildWritesManifestAndDeterministicCompleteRoots()
    {
        using var project = new TestProjectDirectory();
        var stories = new StoryRepository(project.Root);
        stories.CreateStory("intro", "Intro");
        var first = Path.Combine(project.Root, "out-one");
        var second = Path.Combine(project.Root, "out-two");

        var result = new StoryPackageExporter(project.Root).Build("intro", first, "2.0.0");
        new StoryPackageExporter(project.Root).Build("intro", second, "2.0.0");

        Assert.AreEqual("intro", result.Manifest.StoryId);
        Assert.AreEqual("2.0.0", result.Manifest.PackageVersion);
        Assert.IsTrue(File.Exists(Path.Combine(first, "manifest.json")));
        foreach (var directory in new[] { "actors", "dialogues", "quests", "stories" })
            Assert.IsTrue(Directory.Exists(Path.Combine(first, directory)));
        var filesOne = Directory.EnumerateFiles(first, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(first, path))
            .Where(path => !path.Equals("manifest.json", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        var filesTwo = Directory.EnumerateFiles(second, "*", SearchOption.AllDirectories)
            .Select(path => Path.GetRelativePath(second, path))
            .Where(path => !path.Equals("manifest.json", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToArray();
        CollectionAssert.AreEqual(filesOne, filesTwo);
        foreach (var relative in filesOne)
            CollectionAssert.AreEqual(File.ReadAllBytes(Path.Combine(first, relative)), File.ReadAllBytes(Path.Combine(second, relative)));

        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(first, "manifest.json")));
        Assert.AreEqual("intro", manifest.RootElement.GetProperty("story_id").GetString());
        Assert.AreEqual("stories/intro.json", manifest.RootElement.GetProperty("required_resources").GetProperty("story").GetString());
    }
}
