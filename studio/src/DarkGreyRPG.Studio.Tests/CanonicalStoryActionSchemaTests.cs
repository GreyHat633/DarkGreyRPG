using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Editing;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class CanonicalStoryActionSchemaTests
{
    [TestMethod]
    public void AllSevenTypesAndAtomicBuffModeHaveStrictRoundtrips()
    {
        Assert.AreEqual(7, CanonicalStoryActionSchema.ActionTypes.Count);
        foreach (var type in CanonicalStoryActionSchema.ActionTypes)
        {
            var node = GraphNodeFactory.Create(GraphScope.StoryFlow, "action", "action");
            var graph = new GraphDocument([node]); var session = new GraphEditSession(graph, GraphScope.StoryFlow);
            Assert.IsTrue(session.ChangeStoryActionType("action", type));
            Assert.IsEmpty(CanonicalStoryActionSchema.Validate(node), type);
            if (type is "give_item" or "give_xp" or "give_health")
            {
                Assert.IsTrue(session.SetNodeProperty("action", "amount", -5));
                Assert.IsTrue(session.SetNodeProperty("action", "amount", 0));
            }
            if (type == "give_buff")
            {
                var before = session.UndoCount;
                Assert.IsTrue(session.ChangeStoryBuffMode("action", true));
                Assert.AreEqual(before + 1, session.UndoCount);
                Assert.IsFalse(node.Properties.ContainsKey("buff"));
                Assert.IsTrue(node.Properties.ContainsKey("mod_id"));
                Assert.IsTrue(session.Undo());
                Assert.IsFalse(graph.Nodes.Single().Properties["mod_extension"].GetBoolean());
            }
            if (type == "execute_command")
            {
                Assert.IsFalse(session.SetNodeProperty("action", "command", "say one\nsay two"));
                Assert.IsTrue(session.SetNodeProperty("action", "command", "say one"));
            }
            var roundtrip = GraphSerializer.Deserialize(GraphSerializer.Serialize(graph));
            Assert.IsEmpty(CanonicalStoryActionSchema.Validate(roundtrip.Nodes.Single()));
        }
    }

    [TestMethod]
    public void FactoryCreatesStrictMessageDefault()
    {
        var action = GraphNodeFactory.Create(GraphScope.StoryFlow, CanonicalStoryActionSchema.NodeType, "action");

        CollectionAssert.AreEquivalent(
            new[] { CanonicalStoryActionSchema.TypeProperty, CanonicalStoryActionSchema.MessageProperty },
            action.Properties.Keys.ToArray());
        Assert.AreEqual(CanonicalStoryActionSchema.SendMessage,
            action.Properties[CanonicalStoryActionSchema.TypeProperty].GetString());
        Assert.AreEqual("任务完成", action.Properties[CanonicalStoryActionSchema.MessageProperty].GetString());
        Assert.IsEmpty(CanonicalStoryActionSchema.Validate(action));
    }

    [TestMethod]
    public void TypeChangeReplacesPayloadAsOneUndoUnit()
    {
        var action = GraphNodeFactory.Create(GraphScope.StoryFlow, CanonicalStoryActionSchema.NodeType, "action");
        var graph = new GraphDocument([action]);
        var session = new GraphEditSession(graph, GraphScope.StoryFlow);

        Assert.IsTrue(session.ChangeStoryActionType("action", CanonicalStoryActionSchema.GiveItem));
        CollectionAssert.AreEquivalent(new[]
        {
            CanonicalStoryActionSchema.TypeProperty,
            CanonicalStoryActionSchema.ItemIdProperty,
            CanonicalStoryActionSchema.AmountProperty,
        }, action.Properties.Keys.ToArray());
        Assert.AreEqual("starter_reward", action.Properties[CanonicalStoryActionSchema.ItemIdProperty].GetString());
        Assert.AreEqual(10, action.Properties[CanonicalStoryActionSchema.AmountProperty].GetInt32());
        Assert.AreEqual(1, session.UndoCount);

        Assert.IsTrue(session.Undo());
        Assert.AreEqual(CanonicalStoryActionSchema.SendMessage,
            graph.Nodes.Single().Properties[CanonicalStoryActionSchema.TypeProperty].GetString());
        Assert.IsTrue(session.Redo());
        Assert.AreEqual(CanonicalStoryActionSchema.GiveItem,
            graph.Nodes.Single().Properties[CanonicalStoryActionSchema.TypeProperty].GetString());
    }

    [TestMethod]
    public void DirectTypeMutationAndInvalidPayloadFailWithoutMutation()
    {
        var action = GraphNodeFactory.Create(GraphScope.StoryFlow, CanonicalStoryActionSchema.NodeType, "action");
        var graph = new GraphDocument([action]);
        var session = new GraphEditSession(graph, GraphScope.StoryFlow);
        var before = graph.ToJson();

        Assert.IsFalse(session.SetNodeProperty("action", CanonicalStoryActionSchema.TypeProperty,
            JsonSerializer.SerializeToElement(CanonicalStoryActionSchema.GiveXp)));
        CollectionAssert.Contains(session.LastValidationIssues.Select(issue => issue.Code).ToArray(),
            "graph.story.action.type.atomic_required");
        Assert.AreEqual(before, graph.ToJson());

        Assert.IsFalse(session.SetNodeProperty("action", CanonicalStoryActionSchema.MessageProperty,
            JsonSerializer.SerializeToElement("   ")));
        CollectionAssert.Contains(session.LastValidationIssues.Select(issue => issue.Code).ToArray(),
            "graph.story.action.string.invalid");
        Assert.AreEqual(before, graph.ToJson());
        Assert.IsFalse(session.CanUndo);
    }

    [TestMethod]
    public void UnknownMissingAndExtraPropertiesAreRejected()
    {
        var action = GraphNodeFactory.Create(GraphScope.StoryFlow, CanonicalStoryActionSchema.NodeType, "action");
        action.Properties[CanonicalStoryActionSchema.TypeProperty] = JsonSerializer.SerializeToElement("teleport");
        CollectionAssert.Contains(CanonicalStoryActionSchema.Validate(action).Select(issue => issue.Code).ToArray(),
            "graph.story.action.type.invalid");

        action.Properties.Clear();
        action.Properties[CanonicalStoryActionSchema.TypeProperty] = JsonSerializer.SerializeToElement(CanonicalStoryActionSchema.GiveXp);
        action.Properties["unknown"] = JsonSerializer.SerializeToElement(true);
        var codes = CanonicalStoryActionSchema.Validate(action).Select(issue => issue.Code).ToArray();
        CollectionAssert.Contains(codes, "graph.story.action.property.missing");
        CollectionAssert.Contains(codes, "graph.story.action.property.unsupported");
        CollectionAssert.Contains(codes, "graph.story.action.integer.invalid");
    }
}
