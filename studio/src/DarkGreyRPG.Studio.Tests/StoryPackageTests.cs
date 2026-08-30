using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Packaging;
using DarkGreyRPG.Studio.Core.Stories;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class StoryPackageTests
{
    [TestMethod]
    public void BuildRejectsLegacyEnterStoryBeforeChangingOutput()
    {
        using var project = new TestProjectDirectory();
        var stories = new StoryRepository(project.Root);
        var story = stories.CreateStory("legacy_flow", "Legacy Flow");
        story.Nodes.Add(new StoryNodeResource
        {
            Id = "next_story",
            Type = "EnterStory",
            Properties = new Dictionary<string, JsonElement>
            {
                ["target_story_id"] = JsonSerializer.SerializeToElement("other_story"),
            },
        });
        stories.SaveStory(story);

        var output = Path.Combine(project.Root, "existing-output");
        Directory.CreateDirectory(output);
        var existingPath = Path.Combine(output, "sentinel.bin");
        var existingBytes = new byte[] { 0, 17, 34, 255 };
        File.WriteAllBytes(existingPath, existingBytes);

        var exception = Assert.ThrowsExactly<StoryPackageException>(
            () => new StoryPackageExporter(project.Root).Build("legacy_flow", output));

        StringAssert.Contains(exception.Message, "Selected Story 'legacy_flow'");
        StringAssert.Contains(exception.Message, "legacy EnterStory");
        Assert.IsTrue(exception.Message.Contains("project-level cross-Story migration is required", StringComparison.OrdinalIgnoreCase));
        CollectionAssert.AreEqual(existingBytes, File.ReadAllBytes(existingPath));
        CollectionAssert.AreEqual(new[] { "sentinel.bin" },
            Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(output, path))
                .ToArray());
    }

    [TestMethod]
    public void BuildRejectsCanonicalEnterStoryBeforeChangingOutput()
    {
        using var project = new TestProjectDirectory();
        new StoryRepository(project.Root).CreateStory("canonical_flow", "Canonical Flow");

        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(new GraphResourceEnvelope(
            GraphResourceKind.Story,
            "canonical_flow",
            "Canonical Flow",
            new GraphDocument([
                new GraphNode("start", "start", "Start"),
                new GraphNode("next_story", "enter_story", "Enter Story"),
            ])));

        var output = Path.Combine(project.Root, "existing-output");
        Directory.CreateDirectory(output);
        var existingPath = Path.Combine(output, "sentinel.bin");
        var existingBytes = new byte[] { 9, 8, 7, 6 };
        File.WriteAllBytes(existingPath, existingBytes);

        var exception = Assert.ThrowsExactly<StoryPackageException>(
            () => new StoryPackageExporter(project.Root).Build("canonical_flow", output));

        StringAssert.Contains(exception.Message, "Selected Story 'canonical_flow'");
        StringAssert.Contains(exception.Message, "canonical enter_story");
        Assert.IsTrue(exception.Message.Contains("project-level cross-Story migration is required", StringComparison.OrdinalIgnoreCase));
        CollectionAssert.AreEqual(existingBytes, File.ReadAllBytes(existingPath));
        CollectionAssert.AreEqual(new[] { "sentinel.bin" },
            Directory.EnumerateFiles(output, "*", SearchOption.AllDirectories)
                .Select(path => Path.GetRelativePath(output, path))
                .ToArray());
    }

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

    [TestMethod]
    public void BuildCanonicalStoryWithEmptySessionsAndTasksCreatesAllCanonicalRoots()
    {
        using var project = new TestProjectDirectory();
        new StoryRepository(project.Root).CreateStory("canonical_empty", "Canonical Empty");
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(new GraphResourceEnvelope(
            GraphResourceKind.Story,
            "canonical_empty",
            "Canonical Empty",
            new GraphDocument([new GraphNode("start", "start", "Start")])));
        store.Memberships.Create(new CanonicalStoryMembershipManifest("canonical_empty"));

        var output = Path.Combine(project.Root, "canonical-package");
        var result = new StoryPackageExporter(project.Root).Build("canonical_empty", output);

        foreach (var directory in new[] { "stories", "memberships", "sessions", "tasks" })
            Assert.IsTrue(Directory.Exists(Path.Combine(output, "resources", "canonical", directory)));
        Assert.IsTrue(File.Exists(Path.Combine(output, "resources", "canonical", "stories", "canonical_empty.json")));
        Assert.IsTrue(File.Exists(Path.Combine(output, "resources", "canonical", "memberships", "canonical_empty.json")));
        CollectionAssert.AreEqual(new[] { "resources/canonical/stories/canonical_empty.json" }, result.Manifest.RequiredResources.CanonicalStories);
        CollectionAssert.AreEqual(new[] { "resources/canonical/memberships/canonical_empty.json" }, result.Manifest.RequiredResources.CanonicalMemberships);
        CollectionAssert.AreEqual(Array.Empty<string>(), result.Manifest.RequiredResources.Sessions);
        CollectionAssert.AreEqual(Array.Empty<string>(), result.Manifest.RequiredResources.Tasks);

        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "manifest.json")));
        Assert.AreEqual(JsonValueKind.Array, manifest.RootElement.GetProperty("required_resources").GetProperty("sessions").ValueKind);
        Assert.AreEqual(0, manifest.RootElement.GetProperty("required_resources").GetProperty("sessions").GetArrayLength());
        Assert.AreEqual(JsonValueKind.Array, manifest.RootElement.GetProperty("required_resources").GetProperty("tasks").ValueKind);
        Assert.AreEqual(0, manifest.RootElement.GetProperty("required_resources").GetProperty("tasks").GetArrayLength());

        File.WriteAllText(Path.Combine(output, "resources", "canonical", "sessions", "stale.json"), "stale");
        new StoryPackageExporter(project.Root).Build("canonical_empty", output);
        Assert.IsFalse(File.Exists(Path.Combine(output, "resources", "canonical", "sessions", "stale.json")));
    }

    [TestMethod]
    public void BuildDoesNotCreateCanonicalRootsForUnrelatedCanonicalData()
    {
        using var project = new TestProjectDirectory();
        new StoryRepository(project.Root).CreateStory("legacy_only", "Legacy Only");
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(new GraphResourceEnvelope(
            GraphResourceKind.Story,
            "other_story",
            "Other Story",
            new GraphDocument([new GraphNode("start", "start", "Start")])));
        store.Memberships.Create(new CanonicalStoryMembershipManifest("other_story"));

        var output = Path.Combine(project.Root, "legacy-package");
        new StoryPackageExporter(project.Root).Build("legacy_only", output);

        Assert.IsFalse(Directory.Exists(Path.Combine(output, "resources")));
    }

    [TestMethod]
    public void BuildCarriesOnlyRootStoryOwnedPublicLogicEdges()
    {
        using var project = new TestProjectDirectory();
        var legacy = new StoryRepository(project.Root);
        legacy.CreateStory("source", "Source");
        legacy.CreateStory("target", "Target");
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(CanonicalBoundaryStory("source", "logic_output", "output", "signal"));
        store.Stories.Create(CanonicalBoundaryStory("target", "logic_input", "input", "gate"));
        store.Memberships.Create(new CanonicalStoryMembershipManifest("source"));
        store.Memberships.Create(new CanonicalStoryMembershipManifest("target"));
        store.StoryLogicGraph.Save([new("source", "signal", "target", "gate")]);

        var output = Path.Combine(project.Root, "source-package");
        var result = new StoryPackageExporter(project.Root).Build("source", output);

        Assert.AreEqual("resources/story_logic_graph.json", result.Manifest.RequiredResources.StoryLogicGraph);
        var packaged = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "resources", "story_logic_graph.json")));
        var edge = packaged.RootElement.GetProperty("connections")[0];
        Assert.AreEqual("source", edge.GetProperty("source_story_id").GetString());
        Assert.AreEqual("target", edge.GetProperty("target_story_id").GetString());
        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "manifest.json")));
        Assert.AreEqual("resources/story_logic_graph.json",
            manifest.RootElement.GetProperty("required_resources").GetProperty("story_logic_graph").GetString());
    }

    private static GraphResourceEnvelope CanonicalBoundaryStory(
        string storyId,
        string type,
        string nodeId,
        string portId)
    {
        var node = DarkGreyRPG.Studio.Core.Graphs.Definitions.GraphNodeFactory.Create(
            DarkGreyRPG.Studio.Core.Graphs.Definitions.GraphScope.StoryFlow,
            type,
            nodeId);
        node.Properties["port_id"] = JsonSerializer.SerializeToElement(portId);
        node.Properties["display_name"] = JsonSerializer.SerializeToElement(portId);
        return new(GraphResourceKind.Story, storyId, storyId, new GraphDocument([node]));
    }
}
