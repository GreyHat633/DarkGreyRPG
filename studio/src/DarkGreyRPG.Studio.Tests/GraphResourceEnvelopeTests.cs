using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class GraphResourceEnvelopeTests
{
    [TestMethod]
    public void AllKindsRoundTripCanonicalGraphAndIdentity()
    {
        foreach (var (kind, scope) in new[]
        {
            (GraphResourceKind.Story, GraphScope.StoryFlow),
            (GraphResourceKind.Session, GraphScope.Session),
            (GraphResourceKind.Task, GraphScope.Task),
        })
        {
            var graph = SampleGraph();
            var envelope = new GraphResourceEnvelope(kind, $"{kind.ToString().ToLowerInvariant()}-id", "显示名", graph);
            var json = GraphResourceEnvelopeSerializer.Serialize(envelope);
            var restored = GraphResourceEnvelopeSerializer.Deserialize(json);
            var opened = GraphResourceScopeAdapter.Open(restored, scope);

            Assert.AreEqual(kind, restored.ResourceKind);
            Assert.AreEqual(envelope.Id, restored.Id);
            Assert.AreEqual(envelope.DisplayName, restored.DisplayName);
            Assert.AreEqual(graph.Nodes[0].Id, opened.Nodes[0].Id);
            Assert.AreEqual(graph.Nodes[0].Ports[0].Id, opened.Nodes[0].Ports[0].Id);
            Assert.AreEqual(graph.Nodes[0].Ports[0].InterfaceKind, opened.Nodes[0].Ports[0].InterfaceKind);
            Assert.AreEqual(graph.Connections[0], opened.Connections[0]);
            Assert.IsTrue(JsonElement.DeepEquals(graph.Nodes[0].Properties["payload"], opened.Nodes[0].Properties["payload"]));
        }
    }

    [TestMethod]
    public void SerializationHasOnlyFrozenRootMembersInOrderAndOneLf()
    {
        var json = new GraphResourceEnvelope(GraphResourceKind.Story, "story-id", "Story", new GraphDocument()).ToJson();
        StringAssert.StartsWith(json, "{\n  \"schema_version\": 1,\n  \"resource_kind\": \"story\",\n  \"id\": \"story-id\",\n  \"display_name\": \"Story\",\n  \"graph\":");
        Assert.IsTrue(json.EndsWith("}\n", StringComparison.Ordinal));
        Assert.AreNotEqual('\n', json[^2]);
        using var root = JsonDocument.Parse(json);
        CollectionAssert.AreEqual(new[] { "schema_version", "resource_kind", "id", "display_name", "graph" }, root.RootElement.EnumerateObject().Select(x => x.Name).ToArray());
    }

    [TestMethod]
    public void StrictDeserializerRejectsMalformedAndLegacyRoots()
    {
        var valid = "{\"schema_version\":1,\"resource_kind\":\"story\",\"id\":\"id\",\"display_name\":\"Name\",\"graph\":{\"nodes\":[],\"connections\":[]}}";
        foreach (var (json, code) in new[]
        {
            (valid.Replace("\"graph\":{", "\"extra\":true,\"graph\":{", StringComparison.Ordinal), "graph.resource.root.member.unsupported"),
            (valid.Replace("\"schema_version\":1", "\"schema_version\":2", StringComparison.Ordinal), "graph.resource.schema_version.unsupported"),
            (valid.Replace(",\"graph\":{\"nodes\":[],\"connections\":[]}", string.Empty, StringComparison.Ordinal), "graph.resource.root.member.required"),
            (valid.Replace("\"graph\":{\"nodes\":[],\"connections\":[]}", "\"graph\":null", StringComparison.Ordinal), "graph.resource.graph.required"),
            (valid.Replace("\"resource_kind\":\"story\"", "\"resource_kind\":\"dialogue\"", StringComparison.Ordinal), "graph.resource.kind.unsupported"),
            (valid.Replace("\"id\":\"id\"", "\"id\":\" \"", StringComparison.Ordinal), "graph.resource.id.required"),
            (valid.Replace("\"display_name\":\"Name\"", "\"display_name\":null", StringComparison.Ordinal), "graph.resource.root.member.null"),
            ("{\"schema_version\":2,\"id\":\"legacy\",\"title\":\"Old\",\"nodes\":[]}", "graph.resource.root.member.unsupported"),
        })
            Assert.AreEqual(code,
                Assert.ThrowsExactly<GraphResourceEnvelopeException>(
                    () => GraphResourceEnvelopeSerializer.Deserialize(json)).Code);

        Assert.AreEqual("graph.resource.json.required",
            Assert.ThrowsExactly<GraphResourceEnvelopeException>(
                () => GraphResourceEnvelopeSerializer.Deserialize(" ")).Code);
    }

    [TestMethod]
    public void ScopeAdapterMapsKindsAndFailsClosedOnMismatch()
    {
        Assert.AreEqual(GraphScope.StoryFlow, GraphResourceScopeAdapter.GetScope(GraphResourceKind.Story));
        Assert.AreEqual(GraphScope.Session, GraphResourceScopeAdapter.GetScope("session"));
        Assert.AreEqual(GraphScope.Task, GraphResourceScopeAdapter.GetScope(GraphResourceKind.Task));
        var envelope = new GraphResourceEnvelope(GraphResourceKind.Task, "task", "Task", new GraphDocument());
        var exception = Assert.ThrowsExactly<GraphResourceEnvelopeException>(() => GraphResourceScopeAdapter.Open(envelope, GraphScope.Session));
        Assert.AreEqual("graph.resource.scope.mismatch", exception.Code);
    }

    [TestMethod]
    public void OpenedSnapshotsDoNotAliasCallerGraphOrProperties()
    {
        var source = SampleGraph();
        var envelope = new GraphResourceEnvelope(GraphResourceKind.Story, "id", "Name", source);
        source.Nodes.Clear();
        source.Nodes.Add(new GraphNode("caller-only", "action", "Caller"));
        var opened = GraphResourceScopeAdapter.Open(envelope, GraphScope.StoryFlow);
        opened.Nodes[0].DisplayName = "changed";
        opened.Nodes[0].Properties["payload"] = JsonSerializer.SerializeToElement("changed");
        var openedAgain = GraphResourceScopeAdapter.Open(envelope, GraphScope.StoryFlow);
        Assert.AreEqual("Node", openedAgain.Nodes[0].DisplayName);
        Assert.IsTrue(openedAgain.Nodes[0].Properties["payload"].GetProperty("original").GetBoolean());
    }

    [TestMethod]
    public void OpenDocumentOwnsEditableGraphAndProducesDetachedPersistenceSnapshot()
    {
        var source = SampleGraph();
        var envelope = new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session", source);
        var document = GraphResourceScopeAdapter.OpenDocument(envelope, GraphScope.Session);

        source.Nodes.Clear();
        document.Graph.Nodes[0].DisplayName = "Edited";
        var snapshot = document.ToEnvelope();
        document.Graph.Nodes[0].DisplayName = "Edited Again";

        Assert.AreEqual("Edited", snapshot.Graph!.Nodes[0].DisplayName);
        Assert.AreEqual("Edited Again", document.Graph.Nodes[0].DisplayName);
        Assert.AreEqual("Node", envelope.Graph!.Nodes[0].DisplayName);
    }

    private static GraphDocument SampleGraph()
    {
        var node = new GraphNode("node-a", "start", "Node", [new GraphPort("port-a", "Port", false, GraphInterfaceKind.Flow, 2)], new Dictionary<string, JsonElement>
        {
            ["payload"] = JsonSerializer.SerializeToElement(new { original = true }),
        });
        return new GraphDocument([node, new GraphNode("node-b", "action", "Target")], [new("node-a", "port-a", "node-b", "missing", GraphInterfaceKind.Flow)]);
    }
}
