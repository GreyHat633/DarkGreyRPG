using System.Text.Json;
using DarkGreyRPG.Studio.Core.Actors;
using DarkGreyRPG.Studio.Core.Graphs;
using DarkGreyRPG.Studio.Core.Graphs.Definitions;
using DarkGreyRPG.Studio.Core.Graphs.Resources;
using DarkGreyRPG.Studio.Core.Items;
using DarkGreyRPG.Studio.ViewModels.Graph;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class CanonicalNodeInspectorViewModelTests
{
    [TestMethod]
    public void LargeGraphRefreshesOnlyInspectorForChangedNode()
    {
        var nodes = new List<GraphNode>();
        for (var index = 0; index < 30; index++)
        {
            var objective = GraphNodeFactory.Create(GraphScope.Task,
                CanonicalTaskObjectiveSchema.NodeType, $"objective_{index}");
            objective.Properties[CanonicalTaskObjectiveSchema.EntityProperty] =
                JsonSerializer.SerializeToElement($"entity_{index}");
            nodes.Add(objective);
        }
        for (var index = 0; index < 70; index++)
            nodes.Add(new GraphNode($"logic_{index}", "and", $"Logic {index}"));
        var host = new GraphEditorHostViewModel(new GraphDocument(nodes), GraphScope.Task);
        var inspectors = host.Nodes.Where(node => node.Type == CanonicalTaskObjectiveSchema.NodeType)
            .Take(20).Select(node => new CanonicalNodeInspectorViewModel(host, node)).ToArray();
        try
        {
            var before = inspectors.Select(inspector => inspector.RefreshCount).ToArray();
            inspectors[0].ObjectiveDescription = "仅刷新当前目标";

            Assert.AreEqual(before[0] + 1, inspectors[0].RefreshCount);
            for (var index = 1; index < inspectors.Length; index++)
                Assert.AreEqual(before[index], inspectors[index].RefreshCount,
                    $"Unrelated inspector {index} refreshed.");
        }
        finally
        {
            foreach (var inspector in inspectors) inspector.Dispose();
        }
    }

    [TestMethod]
    public void WorkspaceGraphSelectionUsesOneInspectorPathAndClearRestoresResource()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line-1");
        using var workspace = new CanonicalStoryWorkspaceViewModel(
            new DarkGreyRPG.Studio.Core.Graphs.Resources.GraphResourceEnvelope(
                DarkGreyRPG.Studio.Core.Graphs.Resources.GraphResourceKind.Story,
                "story", "Story", new GraphDocument()),
            sessions: [new DarkGreyRPG.Studio.Core.Graphs.Resources.GraphResourceEnvelope(
                DarkGreyRPG.Studio.Core.Graphs.Resources.GraphResourceKind.Session,
                "session", "Session", new GraphDocument([line]))]);
        var session = workspace.SessionItems.Single();
        Assert.IsTrue(workspace.OpenGraphResource(session));
        Assert.IsTrue(workspace.SelectGraphNode(workspace.ActiveGraphHost.Nodes.Single()));
        Assert.AreSame(workspace.NodeInspector, workspace.InspectorSelection);
        Assert.AreEqual("line-1", workspace.InspectorId);
        workspace.ClearGraphSelection();
        Assert.IsNull(workspace.NodeInspector);
        Assert.AreSame(session.Editor, workspace.InspectorSelection);
    }

    [TestMethod]
    public void SessionLineFieldUsesHostTransactionAndUpdatesRevision()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line-1");
        using var host = new CanonicalGraphResourceEditorViewModel(
            new DarkGreyRPG.Studio.Core.Graphs.Resources.GraphResourceEnvelope(
                DarkGreyRPG.Studio.Core.Graphs.Resources.GraphResourceKind.Session,
                "session", "Session", new GraphDocument([line])));
        var node = host.Host.Nodes.Single();
        var inspector = new CanonicalNodeInspectorViewModel(host.Host, node);
        using (inspector)
        {
            inspector.LineText = "Hello";
            Assert.AreEqual("Hello", host.Host.Graph.Nodes.Single().Properties["pages"][0].GetProperty("text").GetString());
            Assert.IsTrue(host.IsDirty);
            Assert.AreEqual("Hello", inspector.LineText);
        }
    }

    [TestMethod]
    public void StoryActionInspectorAtomicallySwitchesPayloadAndStagesInvalidFields()
    {
        var action = GraphNodeFactory.Create(GraphScope.StoryFlow, CanonicalStoryActionSchema.NodeType, "action-1");
        Assert.IsTrue(CanonicalStoryActionSchema.TryInitializeType(action, CanonicalStoryActionSchema.SendMessage, out _));
        using var editor = new CanonicalGraphResourceEditorViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Story, "story", "Story", new GraphDocument([action])));
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());

        Assert.IsTrue(inspector.IsStoryAction);
        Assert.IsTrue(inspector.IsSendMessageAction);
        // Free text fields start as empty authoring drafts; example copy must
        // never be materialized into the graph payload.
        Assert.AreEqual(string.Empty, inspector.StoryActionMessage);
        CollectionAssert.AreEqual(
            new[] { CanonicalStoryActionSchema.GiveItem, CanonicalStoryActionSchema.GiveXp, CanonicalStoryActionSchema.GiveBuff, CanonicalStoryActionSchema.GiveHealth, CanonicalStoryActionSchema.Teleport, CanonicalStoryActionSchema.SendMessage },
            inspector.StoryActionTypeOptions.Select(option => option.Value).ToArray());
        CollectionAssert.AreEqual(
            new[] { "物品给予", "经验给予", "BUFF给予", "生命给予", "玩家传送", "消息发送" },
            inspector.StoryActionTypeOptions.Select(option => option.DisplayName).ToArray());
        Assert.AreEqual("执行「消息发送」", editor.Host.Nodes.Single().DisplayName);
        var beforeTypeRevision = editor.GraphRevision;
        var beforeTypeUndo = editor.Host.Session.UndoCount;

        inspector.SelectedStoryActionType = inspector.StoryActionTypeOptions
            .Single(option => option.Value == CanonicalStoryActionSchema.GiveItem);

        Assert.AreEqual(beforeTypeRevision + 1, editor.GraphRevision);
        Assert.AreEqual(beforeTypeUndo + 1, editor.Host.Session.UndoCount);
        Assert.IsTrue(inspector.IsGiveItemAction);
        Assert.AreEqual("执行「物品给予」", editor.Host.Nodes.Single().DisplayName);
        Assert.AreEqual(string.Empty, inspector.StoryActionItem);
        Assert.AreEqual("10", inspector.StoryActionAmountText);
        CollectionAssert.AreEquivalent(new[]
        {
            CanonicalStoryActionSchema.TypeProperty,
            CanonicalStoryActionSchema.ItemProperty,
            CanonicalStoryActionSchema.AmountProperty,
        }, editor.Host.Graph.Nodes.Single().Properties.Keys.ToArray());

        inspector.StoryActionAmountText = "25";
        Assert.AreEqual(25, editor.Host.Graph.Nodes.Single().Properties[CanonicalStoryActionSchema.AmountProperty].GetInt32());
        inspector.StoryActionAmountText = "invalid";
        Assert.AreEqual("invalid", inspector.StoryActionAmountText);
        StringAssert.Contains(inspector.StoryActionAmountError, "整数");
        Assert.IsTrue(editor.Host.LastValidationIssues.Any(issue =>
            issue.Code == "graph.story.action.amount.authoring_invalid"));
        Assert.AreEqual(25, editor.Host.Graph.Nodes.Single().Properties[CanonicalStoryActionSchema.AmountProperty].GetInt32());
        inspector.StoryActionAmountText = "30";
        Assert.AreEqual(string.Empty, inspector.StoryActionAmountError);
        Assert.IsFalse(editor.Host.LastValidationIssues.Any(issue =>
            issue.Code == "graph.story.action.amount.authoring_invalid"));
        Assert.AreEqual(30, editor.Host.Graph.Nodes.Single().Properties[CanonicalStoryActionSchema.AmountProperty].GetInt32());
        Assert.IsTrue(editor.IsDirty);
    }

    [TestMethod]
    public void SessionLineSpeakerUsesSortedActorOptionsAndPersistsStableId()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line-1");
        using var editor = new CanonicalGraphResourceEditorViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session", new GraphDocument([line])));
        var actors = new[]
        {
            new CanonicalStoryActorItem(new ActorResourceInfo("z-id", "同名", "z.json", []),
                CanonicalStoryWorkspaceMembershipKind.Referenced),
            new CanonicalStoryActorItem(new ActorResourceInfo("a-id", "同名", "a.json", [])),
            new CanonicalStoryActorItem(new ActorResourceInfo("hero", "英雄", "hero.json", [])),
        };
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host,
            editor.Host.Nodes.Single(), actors);

        CollectionAssert.AreEqual(new[] { "a-id", "z-id", "hero" },
            inspector.SpeakerOptions.Select(option => option.Id).ToArray());
        Assert.IsTrue(inspector.SpeakerOptions.Single(option => option.Id == "z-id").IsReferenced);
        Assert.IsTrue(inspector.SpeakerOptions.Single(option => option.Id == "a-id").IsOwned);
        Assert.IsNull(inspector.SelectedSpeaker);
        Assert.IsFalse(inspector.IsSpeakerResolved);
        Assert.AreEqual(string.Empty, inspector.SpeakerStatusText);
        Assert.IsFalse(inspector.HasSpeakerStatus);
        var stableOptions = inspector.SpeakerOptions;

        inspector.SelectedSpeaker = inspector.SpeakerOptions.Single(option => option.Id == "z-id");

        Assert.AreSame(stableOptions, inspector.SpeakerOptions);
        Assert.AreEqual("z-id", inspector.SpeakerActorId);
        Assert.AreEqual("z-id", editor.Host.Graph.Nodes.Single().Properties["speaker_actor_id"].GetString());
        Assert.IsTrue(editor.IsDirty);
        Assert.IsTrue(inspector.IsSpeakerResolved);
    }

    [TestMethod]
    public void ExistingUnknownSpeakerRemainsVisibleAsUnresolvedOption()
    {
        var line = GraphNodeFactory.Create(GraphScope.Session, "line", "line-1");
        line.Properties["speaker_actor_id"] = JsonSerializer.SerializeToElement("deleted-actor");
        using var editor = new CanonicalGraphResourceEditorViewModel(
            new GraphResourceEnvelope(GraphResourceKind.Session, "session", "Session", new GraphDocument([line])));
        var actor = new CanonicalStoryActorItem(new ActorResourceInfo("known", "已知角色", "known.json", []));
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host,
            editor.Host.Nodes.Single(), [actor]);

        var unresolved = inspector.SpeakerOptions.Single(option => option.Id == "deleted-actor");
        Assert.IsTrue(unresolved.IsUnresolved);
        Assert.IsTrue(unresolved.DisplayName.Contains("deleted-actor", StringComparison.Ordinal));
        Assert.AreSame(unresolved, inspector.SelectedSpeaker);
        Assert.IsTrue(inspector.IsSpeakerUnresolved);
        Assert.AreEqual("deleted-actor",
            editor.Host.Graph.Nodes.Single().Properties["speaker_actor_id"].GetString());
    }

    [TestMethod]
    public void ChoiceOptionsUseFlowOnlySemanticCommandsAndHideStableIdsFromDisplay()
    {
        var choice = GraphNodeFactory.Create(GraphScope.Session, "choice", "choice");
        SessionChoiceSchema.InitializeDefault(choice, "option_1", "flow_1");
        using var editor = new CanonicalGraphResourceEditorViewModel(
            new DarkGreyRPG.Studio.Core.Graphs.Resources.GraphResourceEnvelope(
                DarkGreyRPG.Studio.Core.Graphs.Resources.GraphResourceKind.Session,
                "session", "Session", new GraphDocument([choice])));
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());

        CollectionAssert.AreEqual(
            new[] { "flow_in", "flow_1" },
            choice.Ports.Select(port => port.Id).ToArray());
        Assert.IsTrue(choice.Ports.All(port => port.Kind == GraphInterfaceKind.Flow));

        Assert.HasCount(1, inspector.ChoiceOptions);
        Assert.AreEqual("选项 1", inspector.ChoiceOptions[0].DisplayText);
        Assert.IsFalse(inspector.ChoiceOptions[0].MoveUpCommand.CanExecute(null));
        Assert.IsFalse(inspector.ChoiceOptions[0].MoveDownCommand.CanExecute(null));
        var changedNodes = new List<string>();
        editor.Host.PortsChanged += (_, args) => changedNodes.AddRange(args.NodeIds);
        Assert.IsTrue(inspector.AddChoiceOption("Second"));
        Assert.AreEqual("choice", changedNodes.Single());
        var first = inspector.ChoiceOptions[0];
        var second = inspector.ChoiceOptions[1];
        Assert.IsFalse(first.MoveUpCommand.CanExecute(null));
        Assert.IsTrue(first.MoveDownCommand.CanExecute(null));
        Assert.IsTrue(second.MoveUpCommand.CanExecute(null));
        Assert.IsFalse(second.MoveDownCommand.CanExecute(null));
        first.MoveDownCommand.Execute(null);
        Assert.IsTrue(inspector.ChoiceOptions[1].MoveUpCommand.CanExecute(null));
        inspector.ChoiceOptions[1].MoveUpCommand.Execute(null);
        Assert.IsTrue(inspector.RenameChoiceOption(second.OptionId, "Renamed"));
        Assert.IsTrue(inspector.ReorderChoiceOption(second.OptionId, 0));
        Assert.AreSame(second, inspector.ChoiceOptions[0], "Editing and dragging must retain the row instance and focus.");

        var options = editor.Host.Graph.Nodes.Single().Properties["options"].EnumerateArray().ToArray();
        Assert.AreEqual("Renamed", options[0].GetProperty("display_text").GetString());
        Assert.AreEqual(inspector.ChoiceOptions[0].OptionId, options[0].GetProperty("option_id").GetString());
        StringAssert.StartsWith(options[0].GetProperty("flow_port_id").GetString()!, "dynamic_port_");
    }

    [TestMethod]
    public void ReferencedChoiceRemoveFailsClosedAndRetainsOption()
    {
        var choice = GraphNodeFactory.Create(GraphScope.Session, "choice", "choice");
        SessionChoiceSchema.InitializeDefault(choice, "option_1", "flow_1");
        choice.Properties["options"] = JsonSerializer.SerializeToElement(new[]
        {
            new { option_id = "option_1", display_text = "One", flow_port_id = "flow_1" },
            new { option_id = "option_2", display_text = "Two", flow_port_id = "flow_2" },
        });
        choice.Ports.Single(port => port.Id == "flow_1").DisplayName = "One";
        choice.Ports.Add(new GraphPort("option_1", "已选择：One", false, GraphInterfaceKind.Logic, 0));
        choice.Ports.Add(new GraphPort("flow_2", "Two", false, GraphInterfaceKind.Flow, 1));
        choice.Ports.Add(new GraphPort("option_2", "已选择：Two", false, GraphInterfaceKind.Logic, 1));
        var target = GraphNodeFactory.Create(GraphScope.Session, "logic_output", "logic");
        target.Properties["port_id"] = JsonSerializer.SerializeToElement("known");
        target.Properties["display_name"] = JsonSerializer.SerializeToElement("Known");
        var graph = new GraphDocument([choice, target], [
            new GraphConnection("choice", "option_2", "logic", "logic_in", GraphInterfaceKind.Logic)]);
        using var editor = new CanonicalGraphResourceEditorViewModel(
            new DarkGreyRPG.Studio.Core.Graphs.Resources.GraphResourceEnvelope(
                DarkGreyRPG.Studio.Core.Graphs.Resources.GraphResourceKind.Session,
                "session", "Session", graph));
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host,
            editor.Host.Nodes.Single(node => node.NodeId == "choice"));

        Assert.IsFalse(inspector.RemoveChoiceOption("option_2"));
        Assert.HasCount(2, inspector.ChoiceOptions);
        CollectionAssert.Contains(editor.Host.LastValidationIssues.Select(issue => issue.Code).ToArray(),
            "graph.session.choice.references.confirmation_required");
    }

    [TestMethod]
    public void UnreferencedChoiceRemoveDoesNotRequestConfirmation()
    {
        var choice = ReferencedChoice("choice", includeReference: false);
        using var editor = ChoiceEditor(choice);
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host,
            editor.Host.Nodes.Single(node => node.NodeId == "choice"));
        var confirmationCount = 0;
        inspector.ChoiceOptionRemovalConfirmationRequested = _ =>
        {
            confirmationCount++;
            return true;
        };

        Assert.IsTrue(inspector.RemoveChoiceOption("option_2"));
        Assert.AreEqual(0, confirmationCount);
        Assert.HasCount(1, inspector.ChoiceOptions);
        Assert.IsFalse(editor.Host.Graph.Nodes.Single(node => node.Id == "choice").Ports.Any(port => port.Id == "option_2"));
    }

    [TestMethod]
    public void ReferencedChoiceRemoveCancellationRetainsPortsAndConnections()
    {
        var choice = ReferencedChoice("choice", includeReference: true);
        using var editor = ChoiceEditor(choice, includeReference: true);
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host,
            editor.Host.Nodes.Single(node => node.NodeId == "choice"));
        var confirmationCount = 0;
        inspector.ChoiceOptionRemovalConfirmationRequested = confirmation =>
        {
            confirmationCount++;
            Assert.AreEqual("Two", confirmation.DisplayText);
            return false;
        };
        var beforeConnections = editor.Host.Graph.Connections.ToArray();
        var beforePorts = editor.Host.Graph.Nodes.Single(node => node.Id == "choice").Ports.Select(port => port.Id).ToArray();

        Assert.IsFalse(inspector.RemoveChoiceOption("option_2"));
        Assert.AreEqual(1, confirmationCount);
        CollectionAssert.AreEqual(beforeConnections, editor.Host.Graph.Connections);
        CollectionAssert.AreEqual(beforePorts, editor.Host.Graph.Nodes.Single(node => node.Id == "choice").Ports.Select(port => port.Id).ToArray());
        Assert.HasCount(2, inspector.ChoiceOptions);
    }

    [TestMethod]
    public void ReferencedChoiceRemoveConfirmationCleansBothOutputsAndConnectionsAsOneUndoUnit()
    {
        var choice = ReferencedChoice("choice", includeReference: true);
        using var editor = ChoiceEditor(choice, includeReference: true);
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host,
            editor.Host.Nodes.Single(node => node.NodeId == "choice"));
        inspector.ChoiceOptionRemovalConfirmationRequested = confirmation => confirmation.DisplayText == "Two";
        var undoCount = editor.Host.Session.UndoCount;

        Assert.IsTrue(inspector.RemoveChoiceOption("option_2"));
        Assert.AreEqual(undoCount + 1, editor.Host.Session.UndoCount);
        Assert.HasCount(1, inspector.ChoiceOptions);
        var remainingChoice = editor.Host.Graph.Nodes.Single(node => node.Id == "choice");
        CollectionAssert.DoesNotContain(remainingChoice.Ports.Select(port => port.Id).ToArray(), "flow_2");
        CollectionAssert.DoesNotContain(remainingChoice.Ports.Select(port => port.Id).ToArray(), "option_2");
        Assert.IsEmpty(editor.Host.Graph.Connections);
    }

    [TestMethod]
    public void MinimumChoiceOptionValidationDoesNotRequestConfirmation()
    {
        var choice = GraphNodeFactory.Create(GraphScope.Session, "choice", "choice");
        SessionChoiceSchema.InitializeDefault(choice, "option_1", "flow_1");
        using var editor = ChoiceEditor(choice);
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host,
            editor.Host.Nodes.Single(node => node.NodeId == "choice"));
        var confirmationCount = 0;
        inspector.ChoiceOptionRemovalConfirmationRequested = _ =>
        {
            confirmationCount++;
            return true;
        };

        Assert.IsFalse(inspector.RemoveChoiceOption("option_1"));
        Assert.AreEqual(0, confirmationCount);
        CollectionAssert.Contains(editor.Host.LastValidationIssues.Select(issue => issue.Code).ToArray(),
            "graph.session.choice.options.minimum");
    }

    [TestMethod]
    public void EndAndLogicOutputDisplayNamesUseHostTransactions()
    {
        var end = GraphNodeFactory.Create(GraphScope.Session, "end", "end");
        end.Properties["display_name"] = JsonSerializer.SerializeToElement("Done");
        var logic = GraphNodeFactory.Create(GraphScope.Session, "logic_output", "logic");
        logic.Properties["display_name"] = JsonSerializer.SerializeToElement("Known");
        using var editor = new CanonicalGraphResourceEditorViewModel(
            new DarkGreyRPG.Studio.Core.Graphs.Resources.GraphResourceEnvelope(
                DarkGreyRPG.Studio.Core.Graphs.Resources.GraphResourceKind.Session,
                "session", "Session", new GraphDocument([end, logic])));
        var endInspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single(node => node.NodeId == "end"));
        using (endInspector)
        {
            endInspector.EndDisplayName = string.Empty;
            Assert.AreEqual(string.Empty, endInspector.EndDisplayName);
            Assert.IsFalse(string.IsNullOrEmpty(endInspector.EndDisplayNameError));
            Assert.AreEqual("Done", editor.Host.Graph.Nodes.Single(node => node.Id == "end").Properties["display_name"].GetString());
            Assert.AreEqual(0L, editor.GraphRevision);

            endInspector.EndDisplayName = "Finished";
            Assert.AreEqual(string.Empty, endInspector.EndDisplayNameError);
            Assert.AreEqual("Finished", editor.Host.Graph.Nodes.Single(node => node.Id == "end").Properties["display_name"].GetString());
            Assert.AreEqual("Finished", editor.Host.Graph.Nodes.Single(node => node.Id == "end")
                .Ports.Single(port => port.Id == "flow_in").DisplayName);
            Assert.AreEqual(1L, editor.GraphRevision);
        }
        using var logicInspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single(node => node.NodeId == "logic"));
        logicInspector.LogicOutputDisplayName = "Known now";
        Assert.AreEqual("Known now", editor.Host.Graph.Nodes.Single(node => node.Id == "logic").Properties["display_name"].GetString());
    }

    [TestMethod]
    public void TaskSettleInspectorEditsVisiblePriorityWithoutChangingStableIds()
    {
        var settle = GraphNodeFactory.Create(GraphScope.Task, "settle", "settle");
        settle.Ports.Add(new GraphPort("result_a", "A", true, GraphInterfaceKind.Logic, 0));
        settle.Ports.Add(new GraphPort("result_b", "B", true, GraphInterfaceKind.Logic, 1));
        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(
            GraphResourceKind.Task, "task", "Task", new GraphDocument([
                GraphNodeFactory.Create(GraphScope.Task, "objective", "objective"), settle])));
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host,
            editor.Host.Nodes.Single(node => node.NodeId == "settle"));

        Assert.HasCount(2, inspector.TaskResultSlots);
        var second = inspector.TaskResultSlots[1];
        var changedNodes = new List<string>();
        editor.Host.PortsChanged += (_, args) => changedNodes.AddRange(args.NodeIds);
        Assert.IsTrue(inspector.AddTaskResultSlot("C"));
        Assert.AreEqual("settle", changedNodes.Single());
        Assert.IsTrue(inspector.RenameTaskResultSlot(second.PortId, "Renamed"));
        Assert.IsTrue(inspector.ReorderTaskResultSlot(second.PortId, 0));
        Assert.AreSame(second, inspector.TaskResultSlots[0], "Settlement rows must retain focus and identity through edits and reorder.");

        var slots = editor.Host.Graph.Nodes.Single(node => node.Id == "settle").Ports
            .OrderBy(port => port.Order).ToArray();
        CollectionAssert.AreEqual(new[] { "result_b", "result_a", slots[2].Id }, slots.Select(port => port.Id).ToArray());
        Assert.AreEqual("Renamed", slots[0].DisplayName);
        Assert.AreEqual(1, editor.Host.Session.UndoCount - 2); // add and rename plus one move
        Assert.IsTrue(inspector.RemoveTaskResultSlot(slots[2].Id));
    }

    [TestMethod]
    public void TaskSettleDefaultResultNameFillsFirstStandardGap()
    {
        var settle = GraphNodeFactory.Create(GraphScope.Task, "settle", "settle");
        settle.Ports.Add(new GraphPort("one", "结果 1", true, GraphInterfaceKind.Logic, 0));
        settle.Ports.Add(new GraphPort("custom", "完美完成", true, GraphInterfaceKind.Logic, 1));
        settle.Ports.Add(new GraphPort("three", "结果 3", true, GraphInterfaceKind.Logic, 2));
        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(
            GraphResourceKind.Task, "task", "Task", new GraphDocument([
                GraphNodeFactory.Create(GraphScope.Task, "objective", "objective"), settle])));
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host,
            editor.Host.Nodes.Single(node => node.NodeId == "settle"));

        Assert.IsTrue(inspector.AddTaskResultSlot());

        Assert.IsTrue(editor.Host.Graph.Nodes.Single(node => node.Id == "settle").Ports
            .Any(port => port.DisplayName == "结果 2"));
    }

    [TestMethod]
    public void TaskSettleReferencedRemoveConfirmsAndCleansEdgeAsOneUndoUnit()
    {
        var objective = GraphNodeFactory.Create(GraphScope.Task, "objective", "objective");
        var settle = GraphNodeFactory.Create(GraphScope.Task, "settle", "settle");
        settle.Ports.Add(new GraphPort("result", "Result", true, GraphInterfaceKind.Logic, 0));
        settle.Ports.Add(new GraphPort("other", "Other", true, GraphInterfaceKind.Logic, 1));
        settle.Ports.Add(new GraphPort("third", "Third", true, GraphInterfaceKind.Logic, 2));
        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(
            GraphResourceKind.Task, "task", "Task", new GraphDocument([objective, settle], [
                new GraphConnection("objective", "logic_status", "settle", "result", GraphInterfaceKind.Logic)])));
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host,
            editor.Host.Nodes.Single(node => node.NodeId == "settle"));
        var confirmationCount = 0;
        inspector.TaskResultSlotRemovalConfirmationRequested = confirmation =>
        {
            confirmationCount++;
            return confirmationCount > 1 && confirmation.DisplayName == "Result";
        };

        Assert.IsFalse(inspector.RemoveTaskResultSlot("result"));
        Assert.AreEqual(1, confirmationCount);
        Assert.IsTrue(inspector.RemoveTaskResultSlot("result"));
        Assert.IsEmpty(editor.Host.Graph.Connections);
        Assert.HasCount(2, editor.Host.Graph.Nodes.Single(node => node.Id == "settle").Ports);
        CollectionAssert.AreEqual(new[] { 0, 1 }, editor.Host.Graph.Nodes.Single(node => node.Id == "settle").Ports
            .OrderBy(port => port.Order).Select(port => port.Order).ToArray());
        Assert.IsTrue(inspector.ReorderTaskResultSlot("third", 0));
        Assert.IsTrue(editor.Host.Undo());
        Assert.IsTrue(editor.Host.Undo());
        Assert.HasCount(1, editor.Host.Graph.Connections);
    }

    [TestMethod]
    public void TaskLogicOutputDisplayNameUsesHostTransaction()
    {
        var output = new GraphNodeAuthoringService(() => "public_task").Create(
            new GraphDocument(), GraphScope.Task, "logic_output", "logic").Candidate!;
        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(
            GraphResourceKind.Task, "task", "Task", new GraphDocument([
                GraphNodeFactory.Create(GraphScope.Task, "objective", "objective"),
                GraphNodeFactory.Create(GraphScope.Task, "settle", "settle"), output])));
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host,
            editor.Host.Nodes.Single(node => node.NodeId == "logic"));

        Assert.IsTrue(inspector.IsLogicOutput);
        inspector.LogicOutputDisplayName = "Ready";
        Assert.AreEqual("Ready", editor.Host.Graph.Nodes.Single(node => node.Id == "logic").Properties["display_name"].GetString());
        Assert.AreEqual("public_task", editor.Host.Graph.Nodes.Single(node => node.Id == "logic").Properties["port_id"].GetString());
    }

    [TestMethod]
    public void StoryStartInspectorEditsTypedPayloadsWithoutChangingOpaquePort()
    {
        var start = GraphNodeFactory.CreateStoryStart("start", triggerPortId: "opaque-start");
        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(
            GraphResourceKind.Story, "story", "Story", new GraphDocument([start])));
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single(),
            [new CanonicalStoryActorItem(new ActorResourceInfo("actor", "Actor", "actor.json", []))]);

        var initialTrigger = inspector.StoryStartTriggers.Single();
        var changedNodes = new List<string>();
        editor.Host.PortsChanged += (_, args) => changedNodes.AddRange(args.NodeIds);
        var removeStateChanges = 0;
        initialTrigger.RemoveCommand.CanExecuteChanged += (_, _) => removeStateChanges++;
        Assert.IsFalse(initialTrigger.RemoveCommand.CanExecute(null));
        Assert.IsFalse(initialTrigger.TriggerTypeOptions.Any(option => option.Value == StoryStartSchema.EnterStory));

        // Story Start authoring accepts only supported trigger types;
        // EnterStory remains a compatibility-only persisted trigger.
        Assert.IsTrue(inspector.AddStoryStartTrigger(
            displayName: "角色触发", triggerType: StoryStartSchema.ActorInteraction,
            triggerProperties: StoryStartSchema.DefaultTriggerProperties(
                StoryStartSchema.ActorInteraction, "actor")));
        Assert.AreEqual("start", changedNodes.Single());
        var trigger = inspector.StoryStartTriggers.Single(item => item.StablePortId != "opaque-start");
        Assert.IsTrue(initialTrigger.RemoveCommand.CanExecute(null));
        Assert.IsGreaterThan(0, removeStateChanges);
        Assert.IsTrue(trigger.RemoveCommand.CanExecute(null));
        Assert.AreEqual(StoryStartSchema.ActorInteraction, trigger.TriggerType);
        Assert.IsFalse(string.IsNullOrWhiteSpace(trigger.StablePortId));
        changedNodes.Clear();
        Assert.IsTrue(inspector.SetStoryStartTriggerType(trigger.StablePortId, StoryStartSchema.Logic));
        Assert.AreEqual("start", changedNodes.Single());
        Assert.IsTrue(inspector.StoryStartTriggers.Single(item => item.StablePortId == trigger.StablePortId).IsLogic);
        changedNodes.Clear();
        Assert.IsTrue(inspector.SetStoryStartTriggerType(trigger.StablePortId, StoryStartSchema.RegionEntry));
        trigger = inspector.StoryStartTriggers.Single(item => item.StablePortId == trigger.StablePortId);
        var persistedRadius = editor.Host.Graph.Nodes.Single().Properties[StoryStartSchema.TriggersProperty]
            .GetArrayLength();
        trigger.RadiusText = "bad";
        Assert.AreEqual("bad", trigger.RadiusText);
        StringAssert.Contains(trigger.RadiusError, "大于 0");
        Assert.IsTrue(editor.Host.LastValidationIssues.Any(issue =>
            issue.Code == "graph.story.start.trigger.authoring_invalid"));
        Assert.AreEqual(persistedRadius, editor.Host.Graph.Nodes.Single().Properties[StoryStartSchema.TriggersProperty]
            .GetArrayLength());
        trigger.RadiusText = "8";
        Assert.AreEqual("8", trigger.RadiusText);
        Assert.AreEqual(string.Empty, trigger.RadiusError);
        Assert.AreEqual(trigger.StablePortId, editor.Host.Graph.Nodes.Single().Ports.OrderBy(port => port.Order).Last().Id);
        Assert.IsTrue(StoryStartSchema.IsValid(editor.Host.Graph.Nodes.Single()));
        Assert.IsTrue(inspector.RemoveStoryStartTrigger("opaque-start"));
        var remaining = inspector.StoryStartTriggers.Single();
        Assert.AreEqual(trigger.StablePortId, remaining.StablePortId);
        Assert.IsFalse(remaining.RemoveCommand.CanExecute(null));
    }

    [TestMethod]
    public void StoryStartRepeatableProjectionMapsStringAndUsesHostUndoRedo()
    {
        var start = GraphNodeFactory.CreateStoryStart("start", triggerPortId: "opaque-start");
        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(
            GraphResourceKind.Story, "story", "Story", new GraphDocument([start])));
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());

        Assert.IsFalse(inspector.IsRepeatable);
        Assert.AreEqual(StoryStartSchema.Once, inspector.RepeatPolicy);

        inspector.IsRepeatable = true;
        Assert.IsTrue(inspector.IsRepeatable);
        Assert.AreEqual(StoryStartSchema.Repeatable, inspector.RepeatPolicy);
        Assert.AreEqual(StoryStartSchema.Repeatable,
            editor.Host.Graph.Nodes.Single().Properties[StoryStartSchema.RepeatPolicyProperty].GetString());

        Assert.IsTrue(editor.Host.Undo());
        Assert.IsFalse(inspector.IsRepeatable);
        Assert.AreEqual(StoryStartSchema.Once, inspector.RepeatPolicy);
        Assert.IsTrue(editor.Host.Redo());
        Assert.IsTrue(inspector.IsRepeatable);

        var snapshot = editor.CreatePersistenceSnapshot();
        using var reopened = new CanonicalGraphResourceEditorViewModel(snapshot);
        using var reopenedInspector = new CanonicalNodeInspectorViewModel(
            reopened.Host, reopened.Host.Nodes.Single());
        Assert.IsTrue(reopenedInspector.IsRepeatable);
        Assert.AreEqual(StoryStartSchema.Repeatable, reopenedInspector.RepeatPolicy);
    }

    [TestMethod]
    public void LegacyEnterStoryTriggerIsVisibleButNotOfferedForNewTriggers()
    {
        var start = GraphNodeFactory.CreateStoryStart("start", triggerPortId: "legacy-port");
        start.Properties[StoryStartSchema.TriggersProperty] = JsonSerializer.SerializeToElement(new[]
        {
            new
            {
                port_id = "legacy-port",
                display_name = "进入故事",
                trigger_type = StoryStartSchema.EnterStory,
                trigger_properties = new Dictionary<string, object>(),
                order = 0,
            },
        });
        using var editor = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(
            GraphResourceKind.Story, "story", "Story", new GraphDocument([start])));
        using var inspector = new CanonicalNodeInspectorViewModel(editor.Host, editor.Host.Nodes.Single());

        var trigger = inspector.StoryStartTriggers.Single();
        Assert.AreEqual(StoryStartSchema.EnterStory, trigger.TriggerType);
        Assert.AreEqual("进入故事（旧版兼容）",
            trigger.TriggerTypeOptions.Single(option => option.Value == StoryStartSchema.EnterStory).DisplayName);
        Assert.IsFalse(inspector.AddStoryStartTrigger(
            "旧触发", StoryStartSchema.EnterStory, StoryStartSchema.DefaultTriggerProperties(StoryStartSchema.EnterStory)));
    }

    [TestMethod]
    public void ObjectiveAndGiveItemUseStoryResourceSelectors()
    {
        var objective = GraphNodeFactory.Create(GraphScope.Task, "objective", "objective");
        using var task = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(
            GraphResourceKind.Task, "task", "Task", new GraphDocument([
                objective, GraphNodeFactory.Create(GraphScope.Task, "settle", "settle")])));
        var actor = new CanonicalStoryActorItem(new ActorResourceInfo(
            "slimes", "史莱姆", "slimes.json", [], CollectiveActorResource.ResourceType));
        var individual = new CanonicalStoryItemItem(new IndividualItemResource
            { ItemId = "coin", DisplayName = "铜币" });
        var collective = new CanonicalStoryItemItem(new CollectiveItemResource
            { GroupId = "ore", DisplayName = "矿石" });
        using var objectiveInspector = new CanonicalNodeInspectorViewModel(
            task.Host, task.Host.Nodes.Single(node => node.NodeId == "objective"), [actor], [individual, collective]);

        objectiveInspector.SelectedObjectiveActor = objectiveInspector.ObjectiveActorOptions.Single(option => option.Id == "slimes");
        Assert.AreEqual("slimes", task.Host.Graph.Nodes.Single(node => node.Id == "objective")
            .Properties[CanonicalTaskObjectiveSchema.EntityProperty].GetString());
        objectiveInspector.SelectedObjectiveType = objectiveInspector.ObjectiveTypeOptions.Single(option => option.Value == CanonicalTaskObjectiveSchema.CollectItem);
        objectiveInspector.SelectedObjectiveItem = objectiveInspector.ObjectiveItemOptions.Single(option => option.Id == "ore");
        Assert.AreEqual("ore", task.Host.Graph.Nodes.Single(node => node.Id == "objective")
            .Properties[CanonicalTaskObjectiveSchema.ItemProperty].GetString());

        var action = GraphNodeFactory.Create(GraphScope.StoryFlow, "action", "action");
        using var story = new CanonicalGraphResourceEditorViewModel(new GraphResourceEnvelope(
            GraphResourceKind.Story, "story", "Story", new GraphDocument([action])));
        using var actionInspector = new CanonicalNodeInspectorViewModel(
            story.Host, story.Host.Nodes.Single(), [actor], [individual, collective]);
        actionInspector.SelectedStoryActionType = actionInspector.StoryActionTypeOptions.Single(option => option.Value == CanonicalStoryActionSchema.GiveItem);

        CollectionAssert.AreEqual(new[] { "coin" }, actionInspector.StoryActionItemOptions.Select(option => option.Id).ToArray());
        actionInspector.SelectedStoryActionItem = actionInspector.StoryActionItemOptions.Single(option => option.Id == "coin");
        Assert.AreEqual("coin", story.Host.Graph.Nodes.Single().Properties[CanonicalStoryActionSchema.ItemProperty].GetString());
    }

    private static CanonicalGraphResourceEditorViewModel ChoiceEditor(GraphNode choice, bool includeReference = false)
    {
        var nodes = new List<GraphNode> { choice };
        var connections = new List<GraphConnection>();
        if (includeReference)
        {
            var target = GraphNodeFactory.Create(GraphScope.Session, "logic_output", "logic");
            target.Properties["port_id"] = JsonSerializer.SerializeToElement("known");
            target.Properties["display_name"] = JsonSerializer.SerializeToElement("Known");
            nodes.Add(target);
            connections.Add(new GraphConnection("choice", "option_2", "logic", "logic_in", GraphInterfaceKind.Logic));
        }

        return new(new DarkGreyRPG.Studio.Core.Graphs.Resources.GraphResourceEnvelope(
            DarkGreyRPG.Studio.Core.Graphs.Resources.GraphResourceKind.Session,
            "session", "Session", new GraphDocument(nodes, connections)));
    }

    private static GraphNode ReferencedChoice(string nodeId, bool includeReference)
    {
        var choice = GraphNodeFactory.Create(GraphScope.Session, "choice", nodeId);
        SessionChoiceSchema.InitializeDefault(choice, "option_1", "flow_1");
        choice.Properties["options"] = JsonSerializer.SerializeToElement(new[]
        {
            new { option_id = "option_1", display_text = "One", flow_port_id = "flow_1" },
            new { option_id = "option_2", display_text = "Two", flow_port_id = "flow_2" },
        });
        choice.Ports.Single(port => port.Id == "flow_1").DisplayName = "One";
        choice.Ports.Add(new GraphPort("option_1", "已选择：One", false, GraphInterfaceKind.Logic, 0));
        choice.Ports.Add(new GraphPort("flow_2", "Two", false, GraphInterfaceKind.Flow, 1));
        choice.Ports.Add(new GraphPort("option_2", "已选择：Two", false, GraphInterfaceKind.Logic, 1));
        if (!includeReference) return choice;

        return choice;
    }
}
