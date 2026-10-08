using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Packaging;
using DarkGreyRPG.Studio.Core.Stories;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class StoryPackageTests
{
    [TestMethod]
    [DataRow("")]
    [DataRow("  \t")]
    public void BlankLineDraftCanSaveButExportPreservesPreviousOutput(string text)
    {
        using var project = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(new GraphResourceEnvelope(GraphResourceKind.Story, "ST-2345-6789-ABCD-EFGH", "Intro",
            new GraphDocument([GraphNodeFactory.CreateStoryStart("start", triggerPortId: "entry")])));
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "empty_line");
        var page = CanonicalSessionLineSchema.CreatePage("draft");
        page["text"] = JsonSerializer.SerializeToElement(text);
        line.Properties["pages"] = JsonSerializer.SerializeToElement(new[] { page });
        store.Sessions.Create(new GraphResourceEnvelope(GraphResourceKind.Session, "ST-2345-6789-ABCD-EFGH~session~talk", "Talk",
            new GraphDocument([GraphNodeFactory.Create(GraphScope.Session, "start", "start"), line])));
        store.Memberships.Create(new CanonicalStoryMembershipManifest("ST-2345-6789-ABCD-EFGH",
            new CanonicalStoryMembershipSet { Sessions = ["ST-2345-6789-ABCD-EFGH~session~talk"] }));
        var output = Path.Combine(project.Root, "output");
        Directory.CreateDirectory(Path.Combine(output, "resources"));
        var previous = Path.Combine(output, "resources", "previous.json");
        File.WriteAllText(previous, "previous package");

        var error = Assert.ThrowsExactly<StoryPackageException>(() =>
            new StoryPackageExporter(project.Root).Build("ST-2345-6789-ABCD-EFGH", output));

        StringAssert.Contains(error.Message, "empty_line");
        StringAssert.Contains(error.Message, "第 1 句");
        Assert.AreEqual("previous package", File.ReadAllText(previous));
        Assert.IsTrue(File.Exists(store.Sessions.GetPath("ST-2345-6789-ABCD-EFGH~session~talk")));
    }

    [TestMethod]
    public void BuildRejectsLegacyEnterStoryBeforeChangingOutput()
    {
        using var project = new TestProjectDirectory();
        var legacyPath = Path.Combine(project.Root, "stories", "ST-2345-6789-ABCD-EFGH.json");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        const string legacyJson = """{"schema_version":2,"id":"ST-2345-6789-ABCD-EFGH","nodes":[{"id":"next_story","type":"EnterStory","properties":{"target_story_id":"ST-JKLM-NPQR-STUV-WXYZ"}}]}""";
        File.WriteAllText(legacyPath, legacyJson);

        var output = Path.Combine(project.Root, "existing-output");
        Directory.CreateDirectory(output);
        var existingPath = Path.Combine(output, "sentinel.bin");
        var existingBytes = new byte[] { 0, 17, 34, 255 };
        File.WriteAllBytes(existingPath, existingBytes);

        var exception = Assert.ThrowsExactly<GraphResourceRepositoryException>(
            () => new StoryPackageExporter(project.Root).Build("ST-2345-6789-ABCD-EFGH", output));

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

        var store = new CanonicalProjectGraphStore(project.Root);
        Directory.CreateDirectory(Path.GetDirectoryName(store.Stories.GetPath("ST-2345-6789-ABCD-EFGH"))!);
        File.WriteAllText(Path.Combine(store.StoriesDirectory, "ST-2345-6789-ABCD-EFGH.json"), """
            {"schema_version":3,"identity_format":"story-uid-v1","resource_kind":"story","id":"ST-2345-6789-ABCD-EFGH","display_name":"Old",
             "graph":{"nodes":[{"id":"old","type":"enter_story","display_name":"Old","ports":[],"properties":{}}],"connections":[]}}
            """);

        var output = Path.Combine(project.Root, "existing-output");
        Directory.CreateDirectory(output);
        var existingPath = Path.Combine(output, "sentinel.bin");
        var existingBytes = new byte[] { 9, 8, 7, 6 };
        File.WriteAllBytes(existingPath, existingBytes);

        var exception = Assert.ThrowsExactly<GraphResourceRepositoryException>(
            () => new StoryPackageExporter(project.Root).Build("ST-2345-6789-ABCD-EFGH", output));
        Assert.IsInstanceOfType<GraphResourceEnvelopeException>(exception.InnerException);
        Assert.AreEqual("graph.resource.story.standalone_node.removed", ((GraphResourceEnvelopeException)exception.InnerException!).Code);
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
        new CanonicalStoryLifecycleService(new CanonicalProjectGraphStore(project.Root)).Create("ST-2345-6789-ABCD-EFGH", "Intro");
        var first = Path.Combine(project.Root, "out-one");
        var second = Path.Combine(project.Root, "out-two");

        var result = new StoryPackageExporter(project.Root).Build("ST-2345-6789-ABCD-EFGH", first, "2.0.0");
        new StoryPackageExporter(project.Root).Build("ST-2345-6789-ABCD-EFGH", second, "2.0.0");

        Assert.AreEqual("ST-2345-6789-ABCD-EFGH", result.Manifest.StoryId);
        Assert.AreEqual("2.0.0", result.Manifest.PackageVersion);
        Assert.IsTrue(File.Exists(Path.Combine(first, "manifest.json")));
        foreach (var directory in new[] { "actors", "resources" })
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
        Assert.AreEqual("ST-2345-6789-ABCD-EFGH", manifest.RootElement.GetProperty("story_id").GetString());
        Assert.AreEqual("resources/canonical/stories/ST-2345-6789-ABCD-EFGH.json", manifest.RootElement.GetProperty("required_resources").GetProperty("story").GetString());
    }

    [TestMethod]
    public void BuildCanonicalStoryWithEmptySessionsAndTasksCreatesAllCanonicalRoots()
    {
        using var project = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(new GraphResourceEnvelope(
            GraphResourceKind.Story,
            "ST-2345-6789-ABCD-EFGH",
            "Canonical Empty",
            new GraphDocument([GraphNodeFactory.CreateStoryStart("start")])));
        store.Memberships.Create(new CanonicalStoryMembershipManifest("ST-2345-6789-ABCD-EFGH"));
        new CanonicalGraphLayoutStore(project.Root).Save(
            GraphResourceKind.Story,
            "ST-2345-6789-ABCD-EFGH",
            new Dictionary<string, ProjectGraphNodeLayout>(StringComparer.Ordinal)
            {
                ["start"] = new() { X = 123, Y = 456 },
            });

        var output = Path.Combine(project.Root, "canonical-package");
        var result = new StoryPackageExporter(project.Root).Build("ST-2345-6789-ABCD-EFGH", output);

        foreach (var directory in new[] { "stories", "memberships", "sessions", "tasks" })
            Assert.IsTrue(Directory.Exists(Path.Combine(output, "resources", "canonical", directory)));
        Assert.IsTrue(File.Exists(Path.Combine(output, "resources", "canonical", "stories", "ST-2345-6789-ABCD-EFGH.json")));
        Assert.IsTrue(File.Exists(Path.Combine(output, "resources", "canonical", "memberships", "ST-2345-6789-ABCD-EFGH.json")));
        Assert.IsFalse(File.Exists(Path.Combine(output, "resources", "editor", "studio_layout.json")));
        Assert.IsFalse(Directory.GetFiles(output, "*layout*", SearchOption.AllDirectories).Any());
        CollectionAssert.AreEqual(new[] { "resources/canonical/stories/ST-2345-6789-ABCD-EFGH.json" }, result.Manifest.RequiredResources.CanonicalStories);
        CollectionAssert.AreEqual(new[] { "resources/canonical/memberships/ST-2345-6789-ABCD-EFGH.json" }, result.Manifest.RequiredResources.CanonicalMemberships);
        CollectionAssert.AreEqual(Array.Empty<string>(), result.Manifest.RequiredResources.Sessions);
        CollectionAssert.AreEqual(Array.Empty<string>(), result.Manifest.RequiredResources.Tasks);

        using var manifest = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "manifest.json")));
        Assert.AreEqual(JsonValueKind.Array, manifest.RootElement.GetProperty("required_resources").GetProperty("sessions").ValueKind);
        Assert.AreEqual(0, manifest.RootElement.GetProperty("required_resources").GetProperty("sessions").GetArrayLength());
        Assert.AreEqual(JsonValueKind.Array, manifest.RootElement.GetProperty("required_resources").GetProperty("tasks").ValueKind);
        Assert.AreEqual(0, manifest.RootElement.GetProperty("required_resources").GetProperty("tasks").GetArrayLength());

        File.WriteAllText(Path.Combine(output, "resources", "canonical", "sessions", "stale.json"), "stale");
        new StoryPackageExporter(project.Root).Build("ST-2345-6789-ABCD-EFGH", output);
        Assert.IsFalse(File.Exists(Path.Combine(output, "resources", "canonical", "sessions", "stale.json")));
    }

    [TestMethod]
    public void BuildCanonicalOnlyStoryDoesNotRequireLegacyStoryRepositoryEntry()
    {
        using var project = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(new GraphResourceEnvelope(
            GraphResourceKind.Story,
            "ST-2345-6789-ABCD-EFGH",
            "Canonical Only",
            new GraphDocument([GraphNodeFactory.CreateStoryStart("start", triggerPortId: "entry")])));
        store.Memberships.Create(new CanonicalStoryMembershipManifest("ST-2345-6789-ABCD-EFGH"));

        var output = Path.Combine(project.Root, "canonical-only-package");
        var result = new StoryPackageExporter(project.Root).Build("ST-2345-6789-ABCD-EFGH", output, "0.3.2.0");

        Assert.AreEqual(
            "resources/canonical/stories/ST-2345-6789-ABCD-EFGH.json",
            result.Manifest.RequiredResources.Story);
        Assert.IsFalse(File.Exists(Path.Combine(output, "stories", "ST-2345-6789-ABCD-EFGH.json")));
        Assert.IsTrue(File.Exists(Path.Combine(
            output, "resources", "canonical", "stories", "ST-2345-6789-ABCD-EFGH.json")));
    }

    [TestMethod]
    public void BuildDoesNotCreateCanonicalRootsForUnrelatedCanonicalData()
    {
        using var project = new TestProjectDirectory();
        var legacyPath = Path.Combine(project.Root, "stories", "ST-2345-6789-ABCD-EFGH.json");
        Directory.CreateDirectory(Path.GetDirectoryName(legacyPath)!);
        File.WriteAllText(legacyPath, """{"schema_version":2,"id":"ST-2345-6789-ABCD-EFGH"}""");
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(new GraphResourceEnvelope(
            GraphResourceKind.Story,
            "ST-JKLM-NPQR-STUV-WXYZ",
            "Other Story",
            new GraphDocument([GraphNodeFactory.CreateStoryStart("start")])));
        store.Memberships.Create(new CanonicalStoryMembershipManifest("ST-JKLM-NPQR-STUV-WXYZ"));

        var output = Path.Combine(project.Root, "legacy-package");
        Assert.ThrowsExactly<GraphResourceRepositoryException>(() =>
            new StoryPackageExporter(project.Root).Build("ST-2345-6789-ABCD-EFGH", output));
        Assert.IsFalse(Directory.Exists(output));
    }

    [TestMethod]
    public void BuildCarriesOnlyRootStoryOwnedPublicLogicEdges()
    {
        using var project = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(CanonicalBoundaryStory("ST-2345-6789-ABCD-EFGH", "logic_output", "output", "signal"));
        store.Stories.Create(CanonicalBoundaryStory("ST-JKLM-NPQR-STUV-WXYZ", "logic_input", "input", "gate"));
        store.Memberships.Create(new CanonicalStoryMembershipManifest("ST-2345-6789-ABCD-EFGH"));
        store.Memberships.Create(new CanonicalStoryMembershipManifest("ST-JKLM-NPQR-STUV-WXYZ"));
        store.StoryLogicGraph.Save([new("ST-2345-6789-ABCD-EFGH", "signal", "ST-JKLM-NPQR-STUV-WXYZ", "gate")]);

        var output = Path.Combine(project.Root, "ST-2345-6789-ABCD-EFGH-package");
        var result = new StoryPackageExporter(project.Root).Build("ST-2345-6789-ABCD-EFGH", output);

        Assert.AreEqual("resources/story_logic_graph.json", result.Manifest.RequiredResources.StoryLogicGraph);
        var packaged = JsonDocument.Parse(File.ReadAllText(Path.Combine(output, "resources", "story_logic_graph.json")));
        var edge = packaged.RootElement.GetProperty("connections")[0];
        Assert.AreEqual("ST-2345-6789-ABCD-EFGH", edge.GetProperty("source_story_id").GetString());
        Assert.AreEqual("ST-JKLM-NPQR-STUV-WXYZ", edge.GetProperty("target_story_id").GetString());
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
        return new(GraphResourceKind.Story, storyId, storyId, new GraphDocument([GraphNodeFactory.CreateStoryStart("start"), node]));
    }
}
