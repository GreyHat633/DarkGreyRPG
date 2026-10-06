using System.Text.Json;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Editing;

namespace DarkGreyRPG.Studio.Tests;

[TestClass]
public sealed class Construction0335Tests
{
    [TestMethod]
    public void AnimationTransmissionBudgetRejectsExcessInsteadOfDroppingSteps()
    {
        var node = GraphNodeFactory.Create(GraphScope.Session, "screen", "screen");
        var steps = Enumerable.Range(0, 400).Select(_ => new { kind = "enter", type = "fade", direction = "left", duration = .5, delay = 0 }).ToArray();
        node.Properties["layers"] = JsonSerializer.SerializeToElement(new[] { new { media_ref = "media/" + new string('a', 64) + ".png", x = 0, y = 0, width = 1, height = 1, anchor_x = 0, anchor_y = 0, z = 0, animations = steps, morph_duration = 0 } });
        Assert.IsTrue(CanonicalSessionPresentationSchema.Validate(node).Any(i => i.Code == "graph.session.presentation.budget"));
        Assert.AreEqual(400, node.Properties["layers"][0].GetProperty("animations").GetArrayLength());
    }

    [TestMethod]
    public void ConditionSwitchDisconnectsAndRestoresWireAcrossUndoAndMigration()
    {
        var node = GraphNodeFactory.Create(GraphScope.Session, "choice", "choice");
        SessionChoiceSchema.InitializeDefault(node, "option", "flow", "condition");
        var input = GraphNodeFactory.Create(GraphScope.Session, "logic_input", "gate");
        input.Properties["port_id"] = JsonSerializer.SerializeToElement("gate");
        input.Properties["display_name"] = JsonSerializer.SerializeToElement("Gate");
        var graph = new GraphDocument([node, input], [new("gate", "logic_out", "choice", "condition", GraphInterfaceKind.Logic)]);
        var edit = new GraphEditSession(graph, GraphScope.Session);
        JsonElement Option() => graph.Nodes.Single(n => n.Id == "choice").Properties["options"][0];
        Assert.IsFalse(SessionChoiceSchema.ConditionEnabled(Option(), graph, "choice"));
        Assert.IsTrue(edit.SetSessionChoiceConditionEnabled("choice", "option", true));
        Assert.IsTrue(edit.SetSessionChoiceConditionEnabled("choice", "option", false));
        Assert.HasCount(0, graph.Connections);
        Assert.AreEqual("condition", Option().GetProperty("condition_port_id").GetString());
        Assert.IsTrue(edit.Undo()); Assert.IsTrue(Option().GetProperty("condition_enabled").GetBoolean()); Assert.HasCount(1, graph.Connections);
        Assert.IsTrue(edit.Redo()); Assert.IsFalse(Option().GetProperty("condition_enabled").GetBoolean());
        graph.Connections.Add(new("gate", "logic_out", "choice", "condition", GraphInterfaceKind.Logic));
        var legacy = System.Text.Json.Nodes.JsonNode.Parse(Option().GetRawText())!.AsObject(); legacy.Remove("condition_enabled");
        graph.Nodes.Single(n => n.Id == "choice").Properties["options"] = JsonSerializer.SerializeToElement(new[] { legacy });
        ScreenAnimationSequence.NormalizeGraph(graph);
        Assert.IsTrue(Option().GetProperty("condition_enabled").GetBoolean());
        graph.Connections.Clear();
        Assert.IsTrue(GraphNodeShapeValidator.Validate(graph, GraphScope.Session).Any(i => i.Code == "graph.session.choice.condition.unconnected"));
        Assert.IsTrue(edit.SetSessionChoiceConditionEnabled("choice", "option", false));
        Assert.IsFalse(GraphNodeShapeValidator.Validate(graph, GraphScope.Session).Any(i => i.Code == "graph.session.choice.condition.unconnected"));
        graph.Connections.Add(new("gate", "logic_out", "choice", "condition", GraphInterfaceKind.Logic));
        ScreenAnimationSequence.NormalizeGraph(graph);
        Assert.HasCount(0, graph.Connections, "Explicitly disabled legacy conditions must not leave hidden wires after loading.");
        Assert.IsFalse(Option().GetProperty("condition_enabled").GetBoolean());
    }

    [TestMethod]
    public void ChoiceCopyRekeysOptionAndConditionAndUndoRestoresConnectedCondition()
    {
        var node = new GraphNodeAuthoringService().Create(new GraphDocument(), GraphScope.Session, "choice", "choice").Candidate!;
        var option = node.Properties["options"][0]; string id = option.GetProperty("option_id").GetString()!, condition = option.GetProperty("condition_port_id").GetString()!;
        var input = GraphNodeFactory.Create(GraphScope.Session, "logic_input", "gate");
        input.Properties["port_id"] = JsonSerializer.SerializeToElement("gate"); input.Properties["display_name"] = JsonSerializer.SerializeToElement("Gate");
        var graph = new GraphDocument([node, input], [new("gate", "logic_out", "choice", condition, GraphInterfaceKind.Logic)]);
        var copy = new GraphClipboardSnapshot(GraphScope.Session, graph, ["choice", "gate"]).CloneForPaste(GraphScope.Session, out _);
        var pasted = copy.Nodes.Single(n => n.Type == "choice");
        Assert.AreNotEqual(id, pasted.Properties["options"][0].GetProperty("option_id").GetString());
        Assert.AreNotEqual(condition, pasted.Properties["options"][0].GetProperty("condition_port_id").GetString());
        Assert.AreEqual(pasted.Properties["options"][0].GetProperty("condition_port_id").GetString(), copy.Connections.Single().ToPortId);
        var edit = new GraphEditSession(graph, GraphScope.Session);
        Assert.IsTrue(edit.AddSessionChoiceOption("choice", "second"));
        Assert.IsFalse(edit.RemoveSessionChoiceOption("choice", id));
        Assert.IsTrue(edit.RemoveSessionChoiceOption("choice", id, true)); Assert.IsEmpty(graph.Connections);
        Assert.IsTrue(edit.Undo()); Assert.AreEqual(condition, graph.Connections.Single().ToPortId);
        Assert.IsEmpty(SessionChoiceSchema.Validate(graph.Nodes.Single(n => n.Id == "choice")));
    }

    [TestMethod]
    public void DynamicBudgetUsesTypedCurrentNamesAndNeverChangesAuthorText()
    {
        string text = DynamicContentText.Encode([new(Type: "actor_name", ActorId: "ST-2345-6789-ABCD-EFGH~actor~a"), new(Type: "item_name", ItemId: "ST-2345-6789-ABCD-EFGH~item~i"), new(Type: "item_count", ItemId: "ST-2345-6789-ABCD-EFGH~item~i"), new(Type: "player_name")]);
        var result = DynamicTextBudget.Measure(text, _ => new string('界', 650), _ => "同名物品");
        Assert.AreEqual(2029, result.Bytes); Assert.IsTrue(result.Caption.Contains("可能超过"));
        Assert.AreEqual(4, DynamicContentText.Parse(text).Count);
        Assert.IsTrue(DynamicTextBudget.Measure(text, _ => null, _ => "物品").Unknown);
    }

    [TestMethod]
    public void MorphInterpolatesAnchoredRectAndReplacesTextureWithoutMatchingByMedia()
    {
        var a = new ScreenTransitionPreview.Sprite("old", "same", -.2, .1, .2, .4, 1, 0);
        var b = new ScreenTransitionPreview.Sprite("new", "same", .8, .5, .6, .8, 1, 2);
        var sprites = ScreenTransitionPreview.Sample([a], [b], "morph", "left", .5);
        Assert.HasCount(2, sprites); Assert.AreEqual(.3, sprites[0].X, 1e-9); Assert.AreEqual(.4, sprites[0].Width, 1e-9);
        Assert.AreEqual(.5, sprites[0].Alpha); Assert.AreEqual(.5, sprites[1].Alpha);
        var distinct = ScreenTransitionPreview.Sample([a], [b with { Media = "old", Key = "different" }], "morph", "left", .5);
        Assert.AreEqual(.8, distinct.Single(s => s.Key == "different").X); Assert.AreEqual(-.2, distinct.Single(s => s.Key == "same").X);
        Assert.AreEqual(b, ScreenTransitionPreview.Sample([a], [b], "morph", "left", 1).Single());
    }

    [TestMethod]
    public void LegacyScreenKeysUpgradeOnlyDetachedSnapshotAndSurviveCopy()
    {
        var node = GraphNodeFactory.Create(GraphScope.Session, "screen", "screen");
        node.Properties["layers"] = JsonSerializer.SerializeToElement(new[] { new { media_ref = "media/" + new string('a', 64) + ".png", x = 0, y = 0, width = 1, height = 1, anchor_x = 0, anchor_y = 0, z = 0 } });
        string before = new GraphDocument([node]).ToJson();
        var copy = new GraphClipboardSnapshot(GraphScope.Session, new GraphDocument([node]), [node.Id]).CloneForPaste(GraphScope.Session, out _);
        Assert.AreEqual(before, new GraphDocument([node]).ToJson());
        Assert.AreEqual(ScreenMorphKeys.Upgrade(node.Properties["layers"], node.Id)[0].GetProperty("morph_key").GetString(), copy.Nodes.Single().Properties["layers"][0].GetProperty("morph_key").GetString());
        Assert.IsEmpty(CanonicalSessionPresentationSchema.Validate(copy.Nodes.Single()));
    }
}
