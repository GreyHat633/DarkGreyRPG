using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Packaging;
using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.ViewModels.Graph;
using DarkGreyRPG.Studio.Views.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
[DoNotParallelize]
public sealed class LegacyStoryBoundary0331Tests
{
    [TestMethod]
    public void RawLegacyBoundariesSupportUnsavedStartInputsRenameReopenAndFlowPersistence()
    {
        using var directory = new ProjectGraph0331Directory();
        var store = new CanonicalProjectGraphStore(directory.Root);
        var sourcePath = WriteRawStory(directory.Root, "source", RawSourceStory);
        var targetPath = WriteRawStory(directory.Root, "target", RawTargetStory);
        var sourceBefore = File.ReadAllText(sourcePath);
        var targetBefore = File.ReadAllText(targetPath);

        var source = store.Stories.Load("source");
        var sourcePorts = CanonicalStoryBoundaryProjection.Ports(source);
        Assert.HasCount(3, sourcePorts);
        Assert.AreEqual("source_entry", sourcePorts.Single(port => port.IsInput).Id);
        Assert.AreEqual("终止", sourcePorts.Single(port => !port.IsInput && port.DisplayName == "终止").DisplayName);
        Assert.AreEqual("custom_exit", sourcePorts.Single(port => port.DisplayName == "自定义出口").Id);
        Assert.AreEqual(StableLegacyExitId("exit_missing"),
            sourcePorts.Single(port => port.DisplayName == "终止").Id);
        Assert.AreEqual(sourceBefore, File.ReadAllText(sourcePath));

        var target = store.Stories.Load("target");
        using var targetEditor = new CanonicalGraphResourceEditorViewModel(target);
        Assert.IsTrue(targetEditor.Host.AddStoryStartTrigger("start", "入口 A", StoryStartSchema.FlowDriven));
        Assert.IsTrue(targetEditor.Host.AddStoryStartTrigger("start", "入口 B", StoryStartSchema.FlowDriven));
        var unsavedInputs = CanonicalStoryBoundaryProjection.Ports(targetEditor.CreatePersistenceSnapshot())
            .Where(port => port.IsInput && port.InterfaceKind == GraphInterfaceKind.Flow)
            .ToArray();
        Assert.HasCount(2, unsavedInputs);
        Assert.AreNotEqual(unsavedInputs[0].Id, unsavedInputs[1].Id);
        CollectionAssert.AreEqual(new[] { "入口 A", "入口 B" },
            unsavedInputs.Select(port => port.DisplayName).ToArray());
        Assert.AreEqual(targetBefore, File.ReadAllText(targetPath));
        Assert.IsTrue(targetEditor.Host.RenameStoryStartTrigger("start", unsavedInputs[0].Id, "改名入口"));

        var projectGraph = new ProjectGraphViewModel([], projectDirectory: directory.Root);
        var refreshedPortNodes = new List<string>();
        projectGraph.CanonicalHost!.PortsChanged += (_, args) => refreshedPortNodes.AddRange(args.NodeIds);
        projectGraph.RefreshStoryBoundary(targetEditor.CreatePersistenceSnapshot(), () =>
        {
            new CanonicalGraphResourceSaveCoordinator(store).Replace(targetEditor);
            return true;
        });
        CollectionAssert.Contains(refreshedPortNodes, "target");
        var projectTarget = projectGraph.CanonicalHost!.Nodes.Single(node => node.NodeId == "target");
        CollectionAssert.AreEquivalent(unsavedInputs.Select(port => port.Id).ToArray(),
            projectTarget.Inputs.Where(port => port.InterfaceKind == GraphInterfaceKind.Flow)
                .Select(port => port.Id).ToArray());

        var persistedEdge = new GraphConnection("source", sourcePorts.Single(port => !port.IsInput && port.DisplayName == "终止").Id,
            "target", unsavedInputs[0].Id, GraphInterfaceKind.Flow);
        Assert.IsTrue(projectGraph.CanonicalHost.Connect(
            GraphEditorEndpoint.Output(persistedEdge.FromNodeId, persistedEdge.FromPortId, persistedEdge.InterfaceKind),
            GraphEditorEndpoint.Input(persistedEdge.ToNodeId, persistedEdge.ToPortId, persistedEdge.InterfaceKind)));
        Assert.AreEqual("改名入口", CanonicalStoryBoundaryProjection.Ports(store.Stories.Load("target"))
            .Single(port => port.Id == persistedEdge.ToPortId).DisplayName);
        Assert.AreEqual(new CanonicalStoryLogicConnection(
            "source", persistedEdge.FromPortId, "target", persistedEdge.ToPortId, "Flow"),
            store.StoryLogicGraph.Load().Connections.Single());

        Assert.IsTrue(projectGraph.CanonicalHost.Connect(
            GraphEditorEndpoint.Output("source", "custom_exit", GraphInterfaceKind.Flow),
            GraphEditorEndpoint.Input("target", unsavedInputs[1].Id, GraphInterfaceKind.Flow)));
        Assert.HasCount(2, store.StoryLogicGraph.Load().Connections);

        Assert.IsTrue(targetEditor.Host.SetStoryStartTriggerType("start", unsavedInputs[1].Id,
            StoryStartSchema.ActorInteraction, "hero"));
        new CanonicalGraphResourceSaveCoordinator(store).Replace(targetEditor);
        var reopenedTarget = store.Stories.Load("target");
        var reopenedInputs = CanonicalStoryBoundaryProjection.Ports(reopenedTarget)
            .Where(port => port.IsInput && port.InterfaceKind == GraphInterfaceKind.Flow).ToArray();
        Assert.HasCount(1, reopenedInputs);
        Assert.AreEqual(unsavedInputs[0].Id, reopenedInputs[0].Id);
        Assert.AreEqual("改名入口", reopenedInputs[0].DisplayName);
        Assert.AreEqual(persistedEdge.ToPortId, store.StoryLogicGraph.Load().Connections.Single().TargetPortId);

        var reopenedProject = new ProjectGraphViewModel([], projectDirectory: directory.Root);
        Assert.AreEqual(persistedEdge, reopenedProject.CanonicalHost!.Graph.Connections.Single());
        Assert.AreEqual(sourceBefore, File.ReadAllText(sourcePath));
    }

    [TestMethod]
    public void ExportingRawLegacyStoryUpgradesOnlyPackagePayloadAndKeepsSourceBytes()
    {
        using var directory = new ProjectGraph0331Directory();
        var store = new CanonicalProjectGraphStore(directory.Root);
        var sourcePath = WriteRawStory(directory.Root, "source", RawSourceStory);
        store.Memberships.Create(new CanonicalStoryMembershipManifest("source"));
        var before = File.ReadAllText(sourcePath);
        var packagePath = Path.Combine(directory.Root, "build", "source.dgrs");

        new DgrsStoryPackageExporter(directory.Root).Build("source", packagePath, "0.3.3.1");

        Assert.AreEqual(before, File.ReadAllText(sourcePath));
        using var archive = ZipFile.OpenRead(packagePath);
        var entry = archive.GetEntry("resources/canonical/stories/source.json");
        Assert.IsNotNull(entry);
        using var reader = new StreamReader(entry!.Open());
        using var json = JsonDocument.Parse(reader.ReadToEnd());
        var exits = json.RootElement.GetProperty("graph").GetProperty("nodes").EnumerateArray()
            .Where(node => node.GetProperty("type").GetString() == "terminate").ToArray();
        Assert.HasCount(2, exits);
        Assert.IsTrue(exits.All(node => node.GetProperty("properties").TryGetProperty("port_id", out var id)
            && id.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(id.GetString())));
        Assert.AreEqual("custom_exit", exits.Single(node => node.GetProperty("id").GetString() == "exit_explicit")
            .GetProperty("properties").GetProperty("port_id").GetString());
        Assert.AreEqual("终止", exits.Single(node => node.GetProperty("id").GetString() == "exit_missing")
            .GetProperty("properties").GetProperty("display_name").GetString());
    }

    private static string WriteRawStory(string root, string id, string json)
    {
        var path = Path.Combine(root, "resources", "canonical", "stories", id + ".json");
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, json);
        return path;
    }

    private static string StableLegacyExitId(string nodeId)
        => "story_exit_" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(nodeId))).ToLowerInvariant();

    private const string RawSourceStory = """
        {
          "schema_version": 1,
          "resource_kind": "story",
          "id": "source",
          "display_name": "旧源故事",
          "graph": {
            "nodes": [
              {"id":"start","type":"start","display_name":"开始","ports":[{"port_id":"source_entry","display_name":"入口","kind":"flow","direction":"output","order":0}],"properties":{"repeat_policy":"once","triggers":[{"port_id":"source_entry","display_name":"入口","trigger_type":"flow_driven","trigger_properties":{},"order":0}]}},
              {"id":"exit_missing","type":"terminate","display_name":"旧出口","ports":[{"port_id":"flow_in","display_name":"Flow In","kind":"flow","direction":"input","order":0}],"properties":{}},
              {"id":"exit_explicit","type":"terminate","display_name":"旧出口二","ports":[{"port_id":"flow_in","display_name":"Flow In","kind":"flow","direction":"input","order":0}],"properties":{"port_id":"custom_exit","display_name":"自定义出口"}}
            ],
            "connections": []
          }
        }
        """;

    private const string RawTargetStory = """
        {
          "schema_version": 1,
          "resource_kind": "story",
          "id": "target",
          "display_name": "旧目标故事",
          "graph": {
            "nodes": [
              {"id":"start","type":"start","display_name":"开始","ports":[],"properties":{"repeat_policy":"once","triggers":[{"port_id":"legacy_actor","display_name":"角色进入","trigger_type":"interact_actor","trigger_properties":{"actor_id":"hero"},"order":0}]}}
            ],
            "connections": []
          }
        }
        """;
}
