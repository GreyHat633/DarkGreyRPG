using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Packaging;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class PackageBoundary0336Tests
{
    [TestMethod]
    [DataRow(GraphResourceKind.Story)]
    [DataRow(GraphResourceKind.Session)]
    [DataRow(GraphResourceKind.Task)]
    public void BlankPublicBoundaryRejectsExportAndPreservesPriorArtifact(GraphResourceKind kind)
    {
        using var project = new TestProjectDirectory();
        const string uid = "ST-2345-6789-ABCD-EFGH";
        var store = new CanonicalProjectGraphStore(project.Root);
        store.Stories.Create(new(GraphResourceKind.Story, uid, "Story", new GraphDocument([GraphNodeFactory.CreateStoryStart("start")])));
        store.Memberships.Create(new(uid));
        var id = kind == GraphResourceKind.Story ? uid : uid + (kind == GraphResourceKind.Session ? "~session~session" : "~task~task");
        var scope = kind == GraphResourceKind.Story ? GraphScope.StoryFlow : kind == GraphResourceKind.Session ? GraphScope.Session : GraphScope.Task;
        if (kind != GraphResourceKind.Story) new CanonicalStoryResourceLifecycleService(store).CreateOwned(uid, kind, id, "Resource");
        var repository = kind == GraphResourceKind.Story ? store.Stories : kind == GraphResourceKind.Session ? store.Sessions : store.Tasks;
        var resource = repository.Load(id);
        var graph = resource.Graph!;
        var boundary = GraphNodeFactory.Create(scope, "logic_output", "output");
        boundary.Properties["port_id"] = JsonSerializer.SerializeToElement("output_port");
        boundary.Properties["display_name"] = JsonSerializer.SerializeToElement("Output");
        graph.Nodes.Add(boundary); resource.Graph = graph; repository.Replace(resource);
        var resourcePath = repository.GetPath(id);
        var validBytes = File.ReadAllBytes(resourcePath);
        boundary.Properties["display_name"] = JsonSerializer.SerializeToElement("   ");
        resource.Graph = graph;
        Assert.Throws<GraphResourceRepositoryException>(() => repository.Replace(resource));
        CollectionAssert.AreEqual(validBytes, File.ReadAllBytes(resourcePath));
        // Simulate an externally corrupted document without bypassing the save contract.
        var raw = System.Text.Json.Nodes.JsonNode.Parse(validBytes)!;
        var rawBoundary = raw["graph"]!["nodes"]!.AsArray().Single(node => node!["id"]!.GetValue<string>() == "output")!;
        rawBoundary["properties"]!["display_name"] = "   ";
        File.WriteAllText(resourcePath, raw.ToJsonString());
        var output = Path.Combine(project.Root, "previous.dgrs");
        File.WriteAllText(output, "previous package bytes");
        var error = Assert.Throws<GraphResourceRepositoryException>(() => new DgrsStoryPackageExporter(project.Root).Build(uid, output));
        StringAssert.Contains(error.ToString(), "输出端口名称");
        Assert.AreEqual("previous package bytes", File.ReadAllText(output));
    }
}
