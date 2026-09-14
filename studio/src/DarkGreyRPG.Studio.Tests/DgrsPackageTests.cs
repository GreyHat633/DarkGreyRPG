using System.IO.Compression;
using System.Text;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Editing;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Packaging;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class DgrsPackageTests
{
    [TestMethod]
    public void BuildWritesOneValidatedDgrsWithVersionedManifestAndEquivalentCanonicalPayload()
    {
        using var project = new TestProjectDirectory();
        CreateCanonicalStory(project.Root, "canonical_only", "中文故事");
        var outputDirectory = Path.Combine(project.Root, "exports");
        var output = Path.Combine(outputDirectory, "canonical_only.dgrs");

        var result = new DgrsStoryPackageExporter(project.Root)
            .Build("canonical_only", output, "0.3.2.0");

        Assert.AreEqual(output, result.PackagePath);
        Assert.AreEqual(StoryPackageManifest.CurrentFormat, result.Manifest.Format);
        Assert.AreEqual(StoryPackageManifest.CurrentFormatVersion, result.Manifest.FormatVersion);
        Assert.AreEqual("DarkGreyRPGStudio", result.Manifest.Producer);
        Assert.AreEqual("0.3.2.0", result.Manifest.ProducerVersion);
        CollectionAssert.AreEqual(new[] { output }, Directory.GetFiles(outputDirectory, "*.dgrs"));
        Assert.IsFalse(Directory.EnumerateDirectories(outputDirectory).Any());
        CollectionAssert.Contains(result.Validation.Entries.ToArray(), "manifest.json");
        CollectionAssert.Contains(result.Validation.Entries.ToArray(),
            "resources/canonical/stories/canonical_only.json");

        using var archive = ZipFile.OpenRead(output);
        var packaged = Read(archive.GetEntry("resources/canonical/stories/canonical_only.json")!);
        var source = File.ReadAllText(Path.Combine(
            project.Root, "resources", "canonical", "stories", "canonical_only.json"));
        Assert.AreEqual(source, packaged);
        _ = DgrsPackageValidator.Validate(output);
    }

    [TestMethod]
    public void ValidateRejectsCorruptMissingManifestUnsupportedVersionTraversalAndNormalizedDuplicate()
    {
        using var project = new TestProjectDirectory();
        var corrupt = Path.Combine(project.Root, "corrupt.dgrs");
        File.WriteAllText(corrupt, "not a zip");
        Assert.ThrowsExactly<StoryPackageException>(() => DgrsPackageValidator.Validate(corrupt));

        var missing = Path.Combine(project.Root, "missing-manifest.dgrs");
        WriteArchive(missing, [("project.json", "{}")]);
        StringAssert.Contains(
            Assert.ThrowsExactly<StoryPackageException>(() => DgrsPackageValidator.Validate(missing)).Message,
            "manifest.json");

        var unsupported = Path.Combine(project.Root, "unsupported.dgrs");
        WriteArchive(unsupported,
        [
            ("manifest.json", Manifest(formatVersion: 999)),
            ("project.json", "{}"),
        ]);
        StringAssert.Contains(
            Assert.ThrowsExactly<StoryPackageException>(() => DgrsPackageValidator.Validate(unsupported)).Message,
            "format_version");

        var traversal = Path.Combine(project.Root, "traversal.dgrs");
        WriteArchive(traversal, [("../escape.json", "{}")]);
        StringAssert.Contains(
            Assert.ThrowsExactly<StoryPackageException>(() => DgrsPackageValidator.Validate(traversal)).Message,
            "unsafe entry path");

        var duplicate = Path.Combine(project.Root, "duplicate.dgrs");
        WriteArchive(duplicate, [("Data.json", "{}"), ("data.json", "{}")]);
        StringAssert.Contains(
            Assert.ThrowsExactly<StoryPackageException>(() => DgrsPackageValidator.Validate(duplicate)).Message,
            "duplicate normalized entry");
    }

    [TestMethod]
    public void FailedArchiveWritePreservesPreviousPackageAndCleansTransactionArtifacts()
    {
        using var project = new TestProjectDirectory();
        CreateCanonicalStory(project.Root, "safe", "Safe");
        var outputDirectory = Path.Combine(project.Root, "exports");
        Directory.CreateDirectory(outputDirectory);
        var output = Path.Combine(outputDirectory, "safe.dgrs");
        var previous = new byte[] { 0, 17, 34, 51, 255 };
        File.WriteAllBytes(output, previous);
        var exporter = new DgrsStoryPackageExporter(
            project.Root,
            (_, temporary) =>
            {
                File.WriteAllText(temporary, "partial");
                throw new IOException("injected writer failure");
            });

        Assert.ThrowsExactly<StoryPackageException>(() => exporter.Build("safe", output, "0.3.2.0"));

        CollectionAssert.AreEqual(previous, File.ReadAllBytes(output));
        CollectionAssert.AreEqual(
            new[] { "safe.dgrs" },
            Directory.EnumerateFileSystemEntries(outputDirectory)
                .Select(Path.GetFileName)
                .Order(StringComparer.Ordinal)
                .ToArray());
    }

    [TestMethod]
    public void RoundTripIncludesSessionStoryJudgmentAndObjectivePrerequisiteVariants()
    {
        using var project = new TestProjectDirectory();
        var store = new CanonicalProjectGraphStore(project.Root);
        var storyStart = GraphNodeFactory.CreateStoryStart("start", triggerPortId: "entry");
        var storyJudgment = GraphNodeFactory.Create(GraphScope.StoryFlow,
            FlowJudgmentSchema.NodeType, "story_judgment");
        store.Stories.Create(new GraphResourceEnvelope(GraphResourceKind.Story, "complete_story", "完整故事",
            new GraphDocument([storyStart, storyJudgment],
            [new GraphConnection("start", "entry", "story_judgment", FlowJudgmentSchema.FlowInputPortId,
                GraphInterfaceKind.Flow)])));

        var sessionStart = GraphNodeFactory.Create(GraphScope.Session, "start", "start");
        var sessionJudgment = GraphNodeFactory.Create(GraphScope.Session,
            FlowJudgmentSchema.NodeType, "session_judgment");
        var sessionEnd = GraphNodeFactory.Create(GraphScope.Session, "end", "end");
        store.Sessions.Create(new GraphResourceEnvelope(GraphResourceKind.Session, "dialogue", "会话",
            new GraphDocument([sessionStart, sessionJudgment, sessionEnd],
            [
                new GraphConnection("start", "flow_out", "session_judgment",
                    FlowJudgmentSchema.FlowInputPortId, GraphInterfaceKind.Flow),
                new GraphConnection("session_judgment", FlowJudgmentSchema.FlowOutputPortId,
                    "end", "flow_in", GraphInterfaceKind.Flow),
            ])));

        store.Tasks.Create(Task("task_off", prerequisite: false));
        store.Tasks.Create(Task("task_on", prerequisite: true));
        store.Memberships.Create(new CanonicalStoryMembershipManifest(
            "complete_story",
            new CanonicalStoryMembershipSet
            {
                Sessions = ["dialogue"],
                Tasks = ["task_off", "task_on"],
            }));

        var output = Path.Combine(project.Root, "exports", "complete_story.dgrs");
        var result = new DgrsStoryPackageExporter(project.Root).Build("complete_story", output, "0.3.2.0");

        CollectionAssert.Contains(result.Validation.Entries.ToArray(),
            "resources/canonical/sessions/dialogue.json");
        CollectionAssert.Contains(result.Validation.Entries.ToArray(),
            "resources/canonical/tasks/task_off.json");
        CollectionAssert.Contains(result.Validation.Entries.ToArray(),
            "resources/canonical/tasks/task_on.json");
        using var archive = ZipFile.OpenRead(output);
        StringAssert.Contains(Read(archive.GetEntry("resources/canonical/stories/complete_story.json")!),
            FlowJudgmentSchema.NodeType);
        StringAssert.Contains(Read(archive.GetEntry("resources/canonical/tasks/task_on.json")!),
            CanonicalTaskObjectiveSchema.PrerequisitePortId);
        _ = DgrsPackageValidator.Validate(output);
    }

    [TestMethod]
    public void ExactAuditShapePreservesDetachedUnselectedObjectiveAsDormantPayload()
    {
        using var project = new TestProjectDirectory();
        CreateCanonicalStory(project.Root, "kill_slimes", "消灭史莱姆");
        var store = new CanonicalProjectGraphStore(project.Root);
        var configured = GraphNodeFactory.Create(GraphScope.Task, "objective", "configured");
        configured.Properties["description"] = JsonSerializer.SerializeToElement("Test objective");
        configured.Properties[CanonicalTaskObjectiveSchema.EntityProperty] =
            System.Text.Json.JsonSerializer.SerializeToElement("slimes");
        configured.Properties[CanonicalTaskObjectiveSchema.RequiredProperty] =
            System.Text.Json.JsonSerializer.SerializeToElement(3);
        var dormant = GraphNodeFactory.Create(GraphScope.Task, "objective", "dormant");
        dormant.Properties["description"] = JsonSerializer.SerializeToElement("Dormant objective");
        dormant.Properties[CanonicalTaskObjectiveSchema.RequiredProperty] =
            System.Text.Json.JsonSerializer.SerializeToElement(10);
        var settle = GraphNodeFactory.Create(GraphScope.Task, "settle", "settle");
        settle.Ports.Add(new GraphPort("complete", "任务完成", true, GraphInterfaceKind.Logic));
        store.Tasks.Create(new GraphResourceEnvelope(GraphResourceKind.Task, "kill_slimes", "消灭史莱姆",
            new GraphDocument([settle, configured, dormant],
            [
                new GraphConnection("configured", CanonicalTaskObjectiveSchema.CompletionPortId,
                    "settle", "complete", GraphInterfaceKind.Logic),
            ])));
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "kill_slimes",
            new CanonicalStoryMembershipSet { Tasks = ["kill_slimes"] }));

        var output = Path.Combine(project.Root, "exports", "kill_slimes.dgrs");
        var result = new DgrsStoryPackageExporter(project.Root).Build("kill_slimes", output, "0.3.2.0");

        Assert.IsTrue(File.Exists(result.PackagePath));
        _ = DgrsPackageValidator.Validate(output);
        using var archive = ZipFile.OpenRead(output);
        var payload = Read(archive.GetEntry("resources/canonical/tasks/kill_slimes.json")!);
        StringAssert.Contains(payload, "\"id\": \"dormant\"");
        StringAssert.Contains(payload, "\"entity\": \"\"");
    }

    [TestMethod]
    public void ConnectedUnselectedObjectiveStillFailsClosed()
    {
        using var project = new TestProjectDirectory();
        CreateCanonicalStory(project.Root, "invalid", "Invalid");
        var store = new CanonicalProjectGraphStore(project.Root);
        var objective = GraphNodeFactory.Create(GraphScope.Task, "objective", "objective");
        objective.Properties["description"] = JsonSerializer.SerializeToElement("Test objective");
        var settle = GraphNodeFactory.Create(GraphScope.Task, "settle", "settle");
        settle.Ports.Add(new GraphPort("complete", "完成", true, GraphInterfaceKind.Logic));
        store.Tasks.Create(new GraphResourceEnvelope(GraphResourceKind.Task, "invalid", "Invalid",
            new GraphDocument([objective, settle],
            [
                new GraphConnection("objective", CanonicalTaskObjectiveSchema.CompletionPortId,
                    "settle", "complete", GraphInterfaceKind.Logic),
            ])));
        store.Memberships.Replace(new CanonicalStoryMembershipManifest(
            "invalid",
            new CanonicalStoryMembershipSet { Tasks = ["invalid"] }));

        var output = Path.Combine(project.Root, "exports", "invalid.dgrs");
        var exception = Assert.ThrowsExactly<StoryPackageException>(() =>
            new DgrsStoryPackageExporter(project.Root).Build("invalid", output, "0.3.2.0"));

        StringAssert.Contains(exception.Message, "graph.objective.target.invalid");
        Assert.IsFalse(File.Exists(output));
    }

    private static void CreateCanonicalStory(string root, string id, string displayName)
    {
        var store = new CanonicalProjectGraphStore(root);
        store.Stories.Create(new GraphResourceEnvelope(
            GraphResourceKind.Story,
            id,
            displayName,
            new GraphDocument([GraphNodeFactory.CreateStoryStart("start", triggerPortId: "entry")])));
        store.Memberships.Create(new CanonicalStoryMembershipManifest(id));
    }

    private static GraphResourceEnvelope Task(string id, bool prerequisite)
    {
        var objective = GraphNodeFactory.Create(GraphScope.Task, "objective", "objective");
        objective.Properties["description"] = JsonSerializer.SerializeToElement("Test objective");
        objective.Properties[CanonicalTaskObjectiveSchema.EntityProperty] =
            System.Text.Json.JsonSerializer.SerializeToElement("boss");
        var settle = GraphNodeFactory.Create(GraphScope.Task, "settle", "settle");
        settle.Ports.Add(new GraphPort("complete", "完成", true, GraphInterfaceKind.Logic));
        var nodes = new List<GraphNode> { objective, settle };
        var graph = new GraphDocument(nodes,
        [
            new GraphConnection("objective", CanonicalTaskObjectiveSchema.CompletionPortId,
                "settle", "complete", GraphInterfaceKind.Logic),
        ]);
        if (prerequisite)
        {
            var source = GraphNodeFactory.Create(GraphScope.Task, "logic_input", "source");
            source.Properties["port_id"] = System.Text.Json.JsonSerializer.SerializeToElement("source_gate");
            source.Properties["display_name"] = System.Text.Json.JsonSerializer.SerializeToElement("前置来源");
            graph.Nodes.Insert(0, source);
            var session = new GraphEditSession(graph, GraphScope.Task);
            Assert.IsTrue(session.SetObjectivePrerequisiteEnabled("objective", true));
            graph.Connections.Add(new GraphConnection("source", "logic_out", "objective",
                CanonicalTaskObjectiveSchema.PrerequisitePortId, GraphInterfaceKind.Logic));
        }
        return new GraphResourceEnvelope(GraphResourceKind.Task, id, id, graph);
    }

    private static string Read(ZipArchiveEntry entry)
    {
        using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
        return reader.ReadToEnd();
    }

    private static void WriteArchive(string path, IEnumerable<(string Path, string Content)> entries)
    {
        using var archive = ZipFile.Open(path, ZipArchiveMode.Create);
        foreach (var (entryPath, content) in entries)
        {
            var entry = archive.CreateEntry(entryPath);
            using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(false));
            writer.Write(content);
        }
    }

    private static string Manifest(int formatVersion) => $$"""
        {
          "format": "dgrs",
          "format_version": {{formatVersion}},
          "producer": "DarkGreyRPGStudio",
          "producer_version": "0.3.2.0",
          "schema_version": 1,
          "package_id": "test",
          "package_version": "0.3.2.0",
          "story_id": "story",
          "story_schema_version": 1,
          "required_resources": {
            "story": "stories/story.json",
            "actors": [],
            "items": [],
            "item_groups": [],
            "dialogues": [],
            "quests": [],
            "canonical_stories": [],
            "canonical_memberships": [],
            "sessions": [],
            "tasks": []
          }
        }
        """;
}
