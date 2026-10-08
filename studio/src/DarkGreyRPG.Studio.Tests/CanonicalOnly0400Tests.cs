using System.Text.Json;
using System.Text.Json.Nodes;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Editing;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Identity;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CanonicalOnly0400Tests
{
    private const string Owner = "ST-2345-6789-ABCD-EFGH";
    private const string Other = "ST-JKLM-NPQR-STUV-WXYZ";

    [TestMethod]
    [DataRow("kill_entity", "entity", "actor")]
    [DataRow("collect_item", "item", "item")]
    [DataRow("collect_item", "item", "item_group")]
    [DataRow("submit_item", "item", "item_group")]
    [DataRow("interact_actor", "actor_id", "actor")]
    public void TypedTargetsRoundtripWithoutChangingText(string type, string field, string kind)
    {
        var node = Objective(type);
        var target = $"{Owner}~{kind}~target";
        node.Properties[field] = JsonSerializer.SerializeToElement(target);
        node.Properties["description"] = JsonSerializer.SerializeToElement("minecraft:zombie / minecraft:apple");
        var envelope = Envelope(node);
        var json = envelope.ToJson();
        var stored = JsonNode.Parse(json)!;
        Assert.AreEqual(3, stored["schema_version"]!.GetValue<int>());
        Assert.AreEqual(kind, stored["graph"]!["nodes"]![0]!["properties"]![field]!["kind"]!.GetValue<string>());
        var restored = GraphResourceEnvelopeSerializer.Deserialize(json).Graph!.Nodes.Single();
        Assert.AreEqual(target, restored.Properties[field].GetString());
        Assert.AreEqual("minecraft:zombie / minecraft:apple", restored.Properties["description"].GetString());
        node.Properties[field] = JsonSerializer.SerializeToElement($"{Other}~{kind}~target");
        Assert.AreNotEqual(target, GraphResourceEnvelopeSerializer.Deserialize(Envelope(node).ToJson()).Graph!.Nodes.Single().Properties[field].GetString());
    }

    [TestMethod]
    [DataRow("kill_entity", "entity")]
    [DataRow("collect_item", "item")]
    [DataRow("submit_item", "item")]
    [DataRow("interact_actor", "actor_id")]
    public void RawTargetsAndWrongKindsFailWithoutBecomingDrafts(string type, string field)
    {
        foreach (var target in new[] { "minecraft:zombie", "mod:custom_item", "broken~actor~id", $"{Owner}~session~wrong", " " })
        {
            var node = Objective(type);
            node.Properties[field] = JsonSerializer.SerializeToElement(target);
            var before = GraphSerializer.Serialize(new GraphDocument([node]));
            var issues = CanonicalTaskObjectiveSchema.Validate(node);
            Assert.IsNotEmpty(CanonicalTaskObjectiveSchema.AllowDraftIssues(node, issues));
            Reject(() => Envelope(node).ToJson());
            Assert.AreEqual(before, GraphSerializer.Serialize(new GraphDocument([node])));
        }
        var draft = Objective(type);
        draft.Properties[field] = JsonSerializer.SerializeToElement("");
        Assert.IsEmpty(CanonicalTaskObjectiveSchema.AllowDraftIssues(draft, CanonicalTaskObjectiveSchema.Validate(draft)));
        Assert.AreEqual("", GraphResourceEnvelopeSerializer.Deserialize(Envelope(draft).ToJson()).Graph!.Nodes.Single().Properties[field].GetString());
    }

    [TestMethod]
    [DataRow("kill_entity", "entity")]
    [DataRow("collect_item", "item")]
    [DataRow("submit_item", "item")]
    public void ReaderRejectsRegistryObjectsRawStringsAndOldGraphSchema(string type, string field)
    {
        var json = Envelope(Objective(type)).ToJson();
        foreach (var target in new JsonNode[] { JsonValue.Create("minecraft:zombie"), JsonNode.Parse("{\"registry_name\":\"minecraft:zombie\"}")! })
        {
            var root = JsonNode.Parse(json)!;
            root["graph"]!["nodes"]![0]!["properties"]![field] = target;
            Reject(() => GraphResourceEnvelopeSerializer.Deserialize(root.ToJsonString()));
        }
        var old = JsonNode.Parse(json)!;
        old["schema_version"] = 2;
        Assert.AreEqual("graph.resource.schema_version.unsupported", Assert.ThrowsExactly<GraphResourceEnvelopeException>(() => GraphResourceEnvelopeSerializer.Deserialize(old.ToJsonString())).Code);
    }

    [TestMethod]
    public void EditorRejectsNativeMutationAndRetainsUndoHistory()
    {
        var node = Objective("kill_entity");
        var graph = new GraphDocument([node]);
        var editor = new GraphEditSession(graph, GraphScope.Task);
        Assert.IsTrue(editor.SetNodeProperty(node.Id, "entity", $"{Owner}~actor~bandits"));
        var before = graph.ToJson();
        var history = editor.UndoCount;
        Assert.IsFalse(editor.SetNodeProperty(node.Id, "entity", "minecraft:zombie"));
        Assert.AreEqual(before, graph.ToJson());
        Assert.AreEqual(history, editor.UndoCount);
        Assert.IsTrue(editor.Undo());
        Assert.AreEqual("", graph.Nodes.Single().Properties["entity"].GetString());
        Assert.IsTrue(editor.Redo());
        Assert.AreEqual($"{Owner}~actor~bandits", graph.Nodes.Single().Properties["entity"].GetString());
    }

    [TestMethod]
    public void ProjectRequiresDeclaredTargetAndPreservesExplicitForeignReference()
    {
        var target = $"{Other}~actor~target";
        var node = Objective("kill_entity");
        node.Properties["entity"] = JsonSerializer.SerializeToElement(target);
        var task = Envelope(node);
        var story = new GraphResourceEnvelope(GraphResourceKind.Story, Owner, "Story",
            new GraphDocument([GraphNodeFactory.CreateStoryStart("start", triggerPortId: "entry")]));
        var membership = new CanonicalStoryMembershipManifest(Owner,
            new CanonicalStoryMembershipSet { Tasks = [task.Id] });
        var project = new CurrentProjectSnapshot([story, task], [membership], [], [], CanonicalStoryLogicGraph.Empty);
        var before = task.ToJson();
        StringAssert.Contains(Assert.ThrowsExactly<InvalidDataException>(() => CurrentProjectValidator.Validate(project)).Message,
            "undeclared Actor");
        Assert.AreEqual(before, task.ToJson());
        membership.ReferencedResources = new CanonicalStoryMembershipSet { Actors = [target] };
        CurrentProjectValidator.Validate(project);
        Assert.AreEqual(target, task.Graph!.Nodes.Single().Properties["entity"].GetString());
    }

    [TestMethod]
    public void EmptyItemDoesNotHideInvalidSubmitActor()
    {
        var node = Objective("submit_item");
        node.Properties["item"] = JsonSerializer.SerializeToElement("");
        node.Properties["actor_id"] = JsonSerializer.SerializeToElement("minecraft:villager");
        var issues = CanonicalTaskObjectiveSchema.AllowDraftIssues(node, CanonicalTaskObjectiveSchema.Validate(node));
        Assert.HasCount(1, issues);
        Assert.AreEqual("properties.actor_id", issues.Single().Field);
        Reject(() => Envelope(node).ToJson());
    }

    [TestMethod]
    public void GiveItemUsesOnlyIndividualItemAndPreservesSignedAmounts()
    {
        var node = GraphNodeFactory.Create(GraphScope.StoryFlow, "action", "give");
        node.Properties["item_id"] = JsonSerializer.SerializeToElement($"{Owner}~item~coin");
        foreach (var amount in new[] { -5, 0, 5 })
        {
            node.Properties["amount"] = JsonSerializer.SerializeToElement(amount);
            Assert.IsEmpty(CanonicalStoryActionSchema.Validate(node));
            var json = new GraphResourceEnvelope(GraphResourceKind.Story, Owner, "Story", new GraphDocument([node])).ToJson();
            Assert.AreEqual(amount, GraphResourceEnvelopeSerializer.Deserialize(json).Graph!.Nodes.Single().Properties["amount"].GetInt32());
        }
        node.Properties["item_id"] = JsonSerializer.SerializeToElement($"{Owner}~item_group~coins");
        Assert.IsNotEmpty(CanonicalStoryActionSchema.Validate(node));
        node.Properties.Remove("item_id");
        node.Properties["item"] = JsonSerializer.SerializeToElement("minecraft:apple");
        node.Properties["metadata"] = JsonSerializer.SerializeToElement(0);
        Assert.IsNotEmpty(CanonicalStoryActionSchema.Validate(node));
        Reject(() => new GraphResourceEnvelope(GraphResourceKind.Story, Owner, "Story", new GraphDocument([node])).ToJson());
    }

    private static GraphNode Objective(string type)
    {
        var node = GraphNodeFactory.Create(GraphScope.Task, "objective", "target");
        Assert.IsTrue(CanonicalTaskObjectiveSchema.TryInitializeDraftType(node, type, $"{Owner}~actor~receiver", out _));
        return node;
    }

    private static GraphResourceEnvelope Envelope(GraphNode node) => new(GraphResourceKind.Task, $"{Owner}~task~task", "Task", new GraphDocument([node]));

    private static void Reject(Action action)
    {
        try { action(); }
        catch (Exception error) when (error is ArgumentException or JsonException or GraphResourceEnvelopeException) { return; }
        Assert.Fail("Retired or malformed input was accepted.");
    }
}
