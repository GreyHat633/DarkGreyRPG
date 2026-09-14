using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class StoryAction0330InspectorTests
{
    [TestMethod]
    public void AdvancedCommandsAndBuffModesUseTypedAtomicEditing()
    {
        var action = GraphNodeFactory.Create(GraphScope.StoryFlow, "action", "action");
        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(GraphResourceKind.Story, "story", "Story", new GraphDocument([action])));
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());
        Assert.IsFalse(inspector.AdvancedActions);
        Assert.IsFalse(inspector.StoryActionTypeOptions.Any(type => type.Value == "execute_command"));
        inspector.SelectedStoryActionType = inspector.StoryActionTypeOptions.Single(type => type.Value == "give_health");
        inspector.HealthDelta = "-2.5";
        Assert.AreEqual(-2.5, editor.Host.Graph.Nodes.Single().Properties["amount"].GetDouble());
        inspector.SelectedStoryActionType = inspector.StoryActionTypeOptions.Single(type => type.Value == "teleport_player");
        inspector.TeleportDimension = "-7"; inspector.TeleportX = "1.25"; inspector.TeleportY = "64"; inspector.TeleportZ = "-2.5";
        Assert.AreEqual(-7, editor.Host.Graph.Nodes.Single().Properties["dimension_id"].GetInt32());
        inspector.TeleportDimension = "1.5";
        Assert.IsTrue(editor.Host.LastValidationIssues.Any(issue => issue.Code == "graph.story.action.authoring"));
        inspector.TeleportDimension = "0";
        inspector.SelectedStoryActionType = inspector.StoryActionTypeOptions.Single(type => type.Value == "give_buff");
        Assert.IsTrue(inspector.VanillaBuff);
        Assert.AreEqual(23, inspector.VanillaBuffOptions.Count);
        var count = editor.Host.Session.UndoCount;
        inspector.ModBuff = true;
        Assert.AreEqual(count + 1, editor.Host.Session.UndoCount);
        inspector.BuffModId = "testmod"; inspector.BuffInternalName = "potion.test";
        inspector.BuffDurationDelta = "-5"; inspector.BuffLevelDelta = "-1";
        Assert.IsFalse(editor.Host.Graph.Nodes.Single().Properties.ContainsKey("buff"));
        inspector.AdvancedActions = true;
        inspector.SelectedStoryActionType = inspector.StoryActionTypeOptions.Single(type => type.Value == "execute_command");
        inspector.AdvancedCommand = "say one\nsay two";
        Assert.IsTrue(editor.Host.LastValidationIssues.Any(issue => issue.Code == "graph.story.action.authoring"));
        inspector.AdvancedCommand = "say one";
        inspector.AdvancedActions = false;
        Assert.IsTrue(inspector.IsSendMessageAction);
        Assert.IsFalse(inspector.StoryActionTypeOptions.Any(type => type.Value == "execute_command"));
        Assert.IsTrue(editor.Host.Undo());
        Assert.IsTrue(inspector.IsCommandAction && inspector.AdvancedActions);
        Assert.AreEqual("say one", inspector.AdvancedCommand);
    }
}
