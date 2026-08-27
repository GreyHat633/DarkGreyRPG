using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.Core.Stories.Definitions;
using DarkGreyRPG.Studio.Core.Validation;
using DarkGreyRPG.Studio.ViewModels;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
public sealed class M6FlowEditorViewModelTests
{
    [TestMethod]
    public void BuildsSavesAndReloadsCompleteMysteryBranchFlow()
    {
        using var directory = new M6ProjectDirectory();
        var service = new ProjectService();
        var session = service.CreateProject(directory.Root, "m6_flow", "M6 Flow");
        session.Stories.CreateStory("mystery", "王城迷案");
        session.Stories.CreateStory("kingdom", "王国线");
        session.Stories.CreateStory("empire", "帝国线");
        service.CreateActorInStory("mystery", "detective", "侦探");
        service.CreateDialogueInStory("mystery", "final_confrontation", "最终质询");
        service.CreateQuestInStory("mystery", "evidence", "搜集证物");

        var editor = new StoryFlowEditorViewModel(
            session.Stories.LoadStoryDocument("mystery"),
            ["detective"], ["final_confrontation"], ["evidence"], ["mystery", "kingdom", "empire"]);

        editor.SelectOnly(editor.Nodes.Single());
        editor.DeleteSelectionCommand.Execute(null);
        editor.AddStoryStartCommand.Execute(null);
        editor.AddStartQuestCommand.Execute(null);
        editor.AddWaitQuestCompleteCommand.Execute(null);
        editor.AddActorInteractCommand.Execute(null);
        editor.AddPlayDialogueCommand.Execute(null);
        editor.AddDialogueExitBranchCommand.Execute(null);
        editor.AddEnterStoryCommand.Execute(null);
        editor.AddEnterStoryCommand.Execute(null);

        var start = editor.Nodes.Single(node => node.CanonicalType == "StoryStart");
        var startQuest = editor.Nodes.Single(node => node.CanonicalType == "StartQuest");
        var waitQuest = editor.Nodes.Single(node => node.CanonicalType == "WaitQuestComplete");
        var interact = editor.Nodes.Single(node => node.CanonicalType == "ActorInteract");
        var dialogue = editor.Nodes.Single(node => node.CanonicalType == "PlayDialogue");
        var branch = editor.Nodes.Single(node => node.CanonicalType == "DialogueExitBranch");
        var targets = editor.Nodes.Where(node => node.CanonicalType == "EnterStory").ToArray();
        targets[0].ResourceValue = "kingdom";
        targets[1].ResourceValue = "empire";

        Assert.IsTrue(editor.Connect(start.Id, "next", startQuest.Id));
        Assert.IsTrue(editor.Connect(startQuest.Id, "next", waitQuest.Id));
        Assert.IsTrue(editor.Connect(waitQuest.Id, "next", interact.Id));
        Assert.IsTrue(editor.Connect(interact.Id, "next", dialogue.Id));
        Assert.IsTrue(editor.Connect(dialogue.Id, "next", branch.Id));
        Assert.IsTrue(editor.Connect(branch.Id, "hand_over", targets[0].Id));
        Assert.IsTrue(editor.Connect(branch.Id, "conceal", targets[1].Id));
        editor.SelectOnly(branch);
        editor.MoveSelection(new Dictionary<string, (double X, double Y)> { [branch.Id] = (900, 300) });

        Assert.IsTrue(editor.IsDirty);
        Assert.IsEmpty(editor.ValidationErrors);
        session.Stories.SaveStory(editor.Document);
        Assert.IsFalse(editor.IsDirty);

        var restored = session.Stories.LoadStory("mystery");
        Assert.HasCount(8, restored.Nodes);
        Assert.HasCount(7, restored.Connections);
        Assert.AreEqual("start", restored.Entry);
        Assert.AreEqual(900, restored.Nodes.Single(node => node.Id == branch.Id).Position.X);
        Assert.IsTrue(restored.Nodes.Any(node => node.Type == "enter_story" && node.Properties["target_story_id"].GetString() == "kingdom"));
        Assert.IsTrue(restored.Nodes.Any(node => node.Type == "enter_story" && node.Properties["target_story_id"].GetString() == "empire"));
    }

    [TestMethod]
    public void SupportsCopyPasteDeleteAndUndoRedo()
    {
        using var directory = new M6ProjectDirectory();
        var service = new ProjectService();
        var session = service.CreateProject(directory.Root, "m6_history", "M6 History");
        session.Stories.CreateStory("flow", "Flow");
        var editor = new StoryFlowEditorViewModel(session.Stories.LoadStoryDocument("flow"), [], [], [], ["flow"]);
        editor.AddEndCommand.Execute(null);
        var added = editor.SelectedNode!;
        editor.CopyCommand.Execute(null);
        editor.PasteCommand.Execute(null);
        Assert.HasCount(3, editor.Nodes);
        editor.UndoCommand.Execute(null);
        Assert.HasCount(2, editor.Nodes);
        editor.RedoCommand.Execute(null);
        Assert.HasCount(3, editor.Nodes);
        editor.DeleteSelectionCommand.Execute(null);
        Assert.HasCount(2, editor.Nodes);
    }

    [TestMethod]
    public void PreservesLegacyRuntimeNodeTypesWithoutRewritingThemAsEndNodes()
    {
        var resource = new StoryResource
        {
            Id = "legacy", Entry = "condition",
            Nodes =
            [
                new StoryNodeResource { Id = "condition", Type = "quest_state", Position = new() { X = 10, Y = 20 }, Properties = StringProperties(("quest_id", "quest"), ("state", "COMPLETED")) },
                new StoryNodeResource { Id = "branch", Type = "branch", Position = new() { X = 220, Y = 20 }, Properties = StringProperties(("variable", "reputation"), ("operator", ">="), ("value", "10")) },
                new StoryNodeResource { Id = "message", Type = "send_message", Position = new() { X = 440, Y = 20 }, Properties = StringProperties(("message", "完成")) },
            ],
            Connections =
            [
                new StoryConnectionResource { From = "condition", Output = "true", To = "branch" },
                new StoryConnectionResource { From = "branch", Output = "true", To = "message" },
            ],
        };
        var editor = new StoryFlowEditorViewModel(StoryDocument.FromResource(resource), [], [], ["quest"], ["legacy"]);

        editor.SelectOnly(editor.Nodes.Single(node => node.Id == "message"));
        editor.MoveSelection(new Dictionary<string, (double X, double Y)> { ["message"] = (480, 60) });

        var saved = editor.Document.ToResource();
        CollectionAssert.AreEquivalent(
            new[] { "quest_state", "branch", "send_message" },
            saved.Nodes.Select(node => node.Type).ToArray());
        Assert.AreEqual("Branch", editor.Nodes.Single(node => node.Id == "branch").CanonicalType);
        Assert.IsEmpty(editor.ValidationErrors);
    }

    [TestMethod]
    public void RenamingNodeUpdatesConnectionsAndConnectionDeleteCanBeUndone()
    {
        var editor = new StoryFlowEditorViewModel(StoryDocument.CreateNew("rename"), [], [], [], ["rename"]);
        var start = editor.Nodes.Single(node => node.CanonicalType == "StoryStart");
        start.Id = "begin";

        Assert.AreEqual("begin", editor.Document.Entry);
        Assert.AreEqual("begin", editor.Connections.Single().From);
        Assert.IsEmpty(editor.ValidationErrors);

        editor.RemoveConnection(editor.Connections.Single());
        Assert.IsEmpty(editor.Connections);
        editor.UndoCommand.Execute(null);
        Assert.HasCount(1, editor.Connections);
        Assert.AreEqual("begin", editor.Connections.Single().From);
    }

    [TestMethod]
    public void AddNodeAtUsesGraphCoordinatesAndConnectReplacesExistingOutput()
    {
        var editor = new StoryFlowEditorViewModel(StoryDocument.CreateNew("graph_actions"), [], [], [], ["graph_actions"]);
        var source = editor.AddNodeAt("StoryStart", 321.5, 98.25);
        var first = editor.AddNodeAt("End", 600, 100);
        var second = editor.AddNodeAt("End", 600, 300);

        Assert.AreEqual(321.5, source.X);
        Assert.AreEqual(98.25, source.Y);
        Assert.IsTrue(editor.Connect(source.Id, "next", first.Id));
        Assert.IsTrue(editor.Connect(source.Id, "next", second.Id));
        var sourceConnections = editor.Connections.Where(connection => connection.From == source.Id && connection.Output == "next").ToArray();
        Assert.HasCount(1, sourceConnections);
        Assert.AreEqual(second.Id, sourceConnections[0].To);
    }

    [TestMethod]
    public void ConnectionQueriesReturnAllIncomingConnectionsAndConcreteOutputConnection()
    {
        var editor = new StoryFlowEditorViewModel(StoryDocument.CreateNew("connection_queries"), [], [], [], ["connection_queries"]);
        var first = editor.AddNodeAt("Branch", 100, 100);
        var second = editor.AddNodeAt("Branch", 100, 250);
        var target = editor.AddNodeAt("End", 500, 100);

        Assert.IsTrue(editor.Connect(second.Id, "true", target.Id));
        Assert.IsTrue(editor.Connect(first.Id, "false", target.Id));

        var incoming = editor.GetIncomingConnections(target.Id);
        Assert.HasCount(2, incoming);
        CollectionAssert.AreEqual(
            incoming.OrderBy(connection => connection.From, StringComparer.Ordinal).Select(connection => connection.From).ToArray(),
            incoming.Select(connection => connection.From).ToArray());
        Assert.AreSame(editor.GetConnection(first.Id, "false"), incoming.Single(connection => connection.From == first.Id));
        Assert.IsNull(editor.GetConnection(first.Id, "true"));
    }

    [TestMethod]
    public void ConnectionValidationRejectsInvalidEndpointsWithoutMutatingDocument()
    {
        var editor = new StoryFlowEditorViewModel(StoryDocument.CreateNew("connection_validation"), [], [], [], ["connection_validation"]);
        var source = editor.AddNodeAt("StoryStart", 100, 100);
        var terminal = editor.AddNodeAt("End", 300, 100);
        var before = StorySerializer.Serialize(editor.Document.ToResource());

        var invalid = new[]
        {
            (From: source.Id, Output: "next", To: source.Id),
            (From: "missing", Output: "next", To: terminal.Id),
            (From: terminal.Id, Output: "next", To: source.Id),
            (From: source.Id, Output: "unknown", To: terminal.Id),
            (From: source.Id, Output: "next", To: "missing"),
            (From: source.Id, Output: "next", To: "start"),
        };
        foreach (var request in invalid)
        {
            Assert.IsFalse(editor.ValidateConnection(request.From, request.Output, request.To));
            Assert.IsFalse(editor.Connect(request.From, request.Output, request.To));
        }

        Assert.AreEqual(before, StorySerializer.Serialize(editor.Document.ToResource()));
    }

    [TestMethod]
    public void ReconnectTargetIsOneUndoRedoTransactionAndInvalidReconnectPreservesOriginal()
    {
        var editor = new StoryFlowEditorViewModel(StoryDocument.CreateNew("reconnect_target"), [], [], [], ["reconnect_target"]);
        var source = editor.AddNodeAt("StoryStart", 100, 100);
        var originalTarget = editor.AddNodeAt("End", 400, 100);
        var replacementTarget = editor.AddNodeAt("End", 400, 250);
        Assert.IsTrue(editor.Connect(source.Id, "next", originalTarget.Id));
        var original = editor.GetConnection(source.Id, "next")!;

        Assert.IsTrue(editor.ReconnectConnection(original, source.Id, "next", replacementTarget.Id));
        Assert.AreEqual(replacementTarget.Id, editor.GetConnection(source.Id, "next")!.To);
        editor.UndoCommand.Execute(null);
        Assert.AreEqual(originalTarget.Id, editor.GetConnection(source.Id, "next")!.To);
        editor.RedoCommand.Execute(null);
        Assert.AreEqual(replacementTarget.Id, editor.GetConnection(source.Id, "next")!.To);

        var beforeInvalid = StorySerializer.Serialize(editor.Document.ToResource());
        Assert.IsFalse(editor.ReconnectConnection(editor.GetConnection(source.Id, "next"), "missing", "next", replacementTarget.Id));
        Assert.AreEqual(beforeInvalid, StorySerializer.Serialize(editor.Document.ToResource()));
    }

    [TestMethod]
    public void ReconnectSourceReplacesOccupiedOutputDeterministicallyAndUndoRestoresBoth()
    {
        var editor = new StoryFlowEditorViewModel(StoryDocument.CreateNew("reconnect_source"), [], [], [], ["reconnect_source"]);
        var originalSource = editor.AddNodeAt("StoryStart", 100, 100);
        var occupiedSource = editor.AddNodeAt("StoryStart", 100, 250);
        var target = editor.AddNodeAt("End", 500, 100);
        Assert.IsTrue(editor.Connect(originalSource.Id, "next", target.Id));
        Assert.IsTrue(editor.Connect(occupiedSource.Id, "next", target.Id));
        var original = editor.GetConnection(originalSource.Id, "next")!;

        Assert.IsTrue(editor.ReconnectConnection(original, occupiedSource.Id, "next", target.Id));
        var afterReconnect = editor.Connections.Where(connection => connection.From == originalSource.Id || connection.From == occupiedSource.Id).ToArray();
        Assert.HasCount(1, afterReconnect);
        Assert.AreEqual(occupiedSource.Id, afterReconnect.Single().From);
        editor.UndoCommand.Execute(null);
        var afterUndo = editor.Connections.Where(connection => connection.From == originalSource.Id || connection.From == occupiedSource.Id).ToArray();
        Assert.HasCount(2, afterUndo);
        Assert.IsNotNull(editor.GetConnection(originalSource.Id, "next"));
        Assert.IsNotNull(editor.GetConnection(occupiedSource.Id, "next"));
        editor.RedoCommand.Execute(null);
        var afterRedo = editor.Connections.Where(connection => connection.From == originalSource.Id || connection.From == occupiedSource.Id).ToArray();
        Assert.HasCount(1, afterRedo);
        Assert.AreEqual(occupiedSource.Id, afterRedo.Single().From);
    }

    [TestMethod]
    public void RegistryDrivesNodeLabelsPersistedTypesAndPortContracts()
    {
        var editor = new StoryFlowEditorViewModel(StoryDocument.CreateNew("registry_nodes"), ["actor"], ["dialogue"], ["quest"], ["registry_nodes", "target"]);

        foreach (var definition in StoryNodeDefinitionRegistry.Definitions)
        {
            var node = editor.AddNodeAt(definition.CanonicalType, 100, 100);
            Assert.AreEqual(definition.DisplayName, node.TypeLabel);
            Assert.AreEqual(definition.AllowsInput, node.AllowsInput);
            Assert.AreEqual(definition.PersistedType, node.ToResource().Type);
        }

        CollectionAssert.AreEqual(new[] { "true", "false" }, editor.Nodes.Last(node => node.CanonicalType == "QuestState").Outputs.ToArray());
        Assert.IsEmpty(editor.Nodes.Last(node => node.CanonicalType == "End").Outputs);
        Assert.IsFalse(editor.Nodes.Last(node => node.CanonicalType == "StoryStart").AllowsInput);
    }

    [TestMethod]
    public void LoadedCompatibilityOutputsStayVisibleAndInvalidEndpointConnectionsAreRejected()
    {
        var resource = new StoryResource
        {
            Id = "compatibility", Entry = "dialogue",
            Nodes =
            [
                new StoryNodeResource { Id = "dialogue", Type = "play_dialogue", Properties = new() { ["dialogue_id"] = System.Text.Json.JsonSerializer.SerializeToElement("offer") } },
                new StoryNodeResource { Id = "wait", Type = "quest_completed", Properties = new() { ["quest_id"] = System.Text.Json.JsonSerializer.SerializeToElement("quest") } },
                new StoryNodeResource { Id = "branch", Type = "branch" },
                new StoryNodeResource { Id = "start", Type = "story_start" },
                new StoryNodeResource { Id = "end", Type = "end" },
            ],
            Connections =
            [
                new StoryConnectionResource { From = "dialogue", Output = "accept", To = "wait" },
                new StoryConnectionResource { From = "wait", Output = "complete", To = "branch" },
                new StoryConnectionResource { From = "branch", Output = "true", To = "end" },
            ],
        };
        var editor = new StoryFlowEditorViewModel(StoryDocument.FromResource(resource), [], ["offer"], ["quest"], ["compatibility"]);

        CollectionAssert.Contains(editor.Nodes.Single(node => node.Id == "dialogue").Outputs.ToArray(), "accept");
        CollectionAssert.Contains(editor.Nodes.Single(node => node.Id == "wait").Outputs.ToArray(), "complete");
        Assert.IsFalse(editor.Connect("end", "next", "branch"));
        Assert.IsFalse(editor.Connect("branch", "next", "end"));
        Assert.IsFalse(editor.Connect("branch", "false", "start"));
        Assert.IsTrue(editor.Connect("branch", "false", "end"));
    }

    private static Dictionary<string, System.Text.Json.JsonElement> StringProperties(params (string Key, string Value)[] values) =>
        values.ToDictionary(pair => pair.Key, pair => System.Text.Json.JsonSerializer.SerializeToElement(pair.Value), StringComparer.Ordinal);

    [TestMethod]
    public void DisconnectAndDuplicateSelectionRemainUndoableDocumentEdits()
    {
        var editor = new StoryFlowEditorViewModel(StoryDocument.CreateNew("node_actions"), [], [], [], ["node_actions"]);
        var source = editor.AddNodeAt("StoryStart", 20, 30);
        var target = editor.AddNodeAt("End", 300, 30);
        editor.Connect(source.Id, "next", target.Id);
        editor.SelectOnly(source);

        var beforeDuplicate = editor.Nodes.Count;
        editor.DuplicateSelection();
        Assert.HasCount(beforeDuplicate + 1, editor.Nodes);
        editor.DisconnectNode(source.Id);
        Assert.IsFalse(editor.Connections.Any(connection => connection.From == source.Id || connection.To == source.Id));
        editor.UndoCommand.Execute(null);
        Assert.IsTrue(editor.Connections.Any(connection => connection.From == source.Id && connection.To == target.Id));
    }

    [TestMethod]
    public void ProjectResourceOutsideStoryMembershipIsWarningWhileMissingResourceIsError()
    {
        var document = StoryDocument.CreateNew("legacy_membership");
        var editor = new StoryFlowEditorViewModel(
            document,
            actorIds: [],
            dialogueIds: ["project_dialogue"],
            questIds: [],
            storyIds: ["legacy_membership"],
            storyDialogueIds: []);
        var node = editor.AddNodeAt("PlayDialogue", 100, 100);

        Assert.AreEqual("project_dialogue", node.ResourceValue);
        Assert.IsTrue(editor.ValidationIssues.Any(issue => issue.Code == "story.flow.dialogue.membership" && issue.Severity == ValidationSeverity.Warning));
        Assert.IsFalse(editor.ValidationErrors.Any(issue => issue.Code == "story.flow.dialogue.missing"));
        Assert.IsEmpty(document.ToResource().ReferencedResources.Dialogues);

        Assert.IsTrue(editor.AddResourceAsReference(node));
        CollectionAssert.Contains(document.ToResource().ReferencedResources.Dialogues, "project_dialogue");
        Assert.IsFalse(editor.ValidationIssues.Any(issue => issue.Code == "story.flow.dialogue.membership"));

        node.ResourceValue = "does_not_exist";

        Assert.IsTrue(editor.ValidationErrors.Any(issue => issue.Code == "story.flow.dialogue.missing"));
    }

    [TestMethod]
    public void ParameterHeaderExposesCountSummaryAndCoreDiscoverability()
    {
        var editor = new StoryFlowEditorViewModel(
            StoryDocument.CreateNew("parameter_summary"),
            actorIds: [], dialogueIds: [], questIds: ["quest_alpha", "quest_beta"], storyIds: ["parameter_summary"]);

        var questState = editor.AddNodeAt("QuestState", 100, 100);
        Assert.IsTrue(questState.HasParameters);
        Assert.IsTrue(questState.HasCoreParameters);
        Assert.AreEqual(2, questState.ParameterCount);
        StringAssert.Contains(questState.ParameterHeader, "参数（2）");
        StringAssert.Contains(questState.ParameterHeader, "quest_alpha");
        StringAssert.Contains(questState.ParameterHeader, "NOT_STARTED");

        questState.ResourceValue = "quest_beta";
        StringAssert.Contains(questState.ParameterHeader, "quest_beta");
        StringAssert.Contains(questState.ParameterAutomationName, $"{questState.Id} 参数：参数（2）");

        var terminal = editor.AddNodeAt("End", 400, 100);
        Assert.IsFalse(terminal.HasParameters);
        Assert.IsFalse(terminal.HasCoreParameters);
        Assert.AreEqual("参数（0）", terminal.ParameterHeader);
    }

    [TestMethod]
    public void DialogueExitRenamePlanMigratesConnectionAndUndoRedoAsOneEdit()
    {
        var editor = new StoryFlowEditorViewModel(StoryDocument.CreateNew("dynamic_dialogue"), [], [], [], ["dynamic_dialogue"]);
        var branch = editor.AddNodeAt("DialogueExitBranch", 100, 100);
        var target = editor.AddNodeAt("End", 400, 100);
        Assert.IsTrue(editor.Connect(branch.Id, "hand_over", target.Id));

        var plan = editor.AnalyzeDialogueExitNames(branch, ["accept", "conceal"]);
        Assert.IsTrue(plan.IsValid);
        Assert.AreEqual("hand_over", plan.RenameCandidate!.OldOutput);
        Assert.AreEqual("accept", plan.RenameCandidate.NewOutput);
        Assert.HasCount(1, plan.RemovedConnectedOutputs);
        Assert.IsTrue(editor.ApplyDialogueExitNameChange(plan, DialogueExitOutputDisposition.Migrate));
        Assert.AreEqual("accept", editor.GetConnection(branch.Id, "accept")!.Output);
        Assert.AreEqual("accept, conceal", branch.ExitNames);

        editor.UndoCommand.Execute(null);
        Assert.AreEqual("hand_over", editor.GetConnection(branch.Id, "hand_over")!.Output);
        editor.RedoCommand.Execute(null);
        Assert.AreEqual("accept", editor.GetConnection(branch.Id, "accept")!.Output);
    }

    [TestMethod]
    public void InvalidOrCancelledDialogueExitChangeDoesNotMutate()
    {
        var editor = new StoryFlowEditorViewModel(StoryDocument.CreateNew("dynamic_validation"), [], [], [], ["dynamic_validation"]);
        var branch = editor.AddNodeAt("DialogueExitBranch", 100, 100);
        var target = editor.AddNodeAt("End", 400, 100);
        Assert.IsTrue(editor.Connect(branch.Id, "hand_over", target.Id));
        var before = StorySerializer.Serialize(editor.Document.ToResource());

        Assert.IsFalse(editor.ApplyDialogueExitNameChange(editor.AnalyzeDialogueExitNames(branch, ["Accept", "conceal"])));
        Assert.IsFalse(editor.ApplyDialogueExitNameChange(editor.AnalyzeDialogueExitNames(branch, ["accept", "accept"])));
        var removal = editor.AnalyzeDialogueExitNames(branch, ["conceal"]);
        Assert.IsFalse(editor.ApplyDialogueExitNameChange(removal, DialogueExitOutputDisposition.Cancel));
        Assert.AreEqual(before, StorySerializer.Serialize(editor.Document.ToResource()));
        Assert.AreEqual("hand_over", editor.GetConnection(branch.Id, "hand_over")!.Output);
    }

    [TestMethod]
    public void DialogueExitDeletionRemovesConnectedOutputAtomicallyAndUndoRedoRestoresIt()
    {
        var editor = new StoryFlowEditorViewModel(StoryDocument.CreateNew("dynamic_delete"), [], [], [], ["dynamic_delete"]);
        var branch = editor.AddNodeAt("DialogueExitBranch", 100, 100);
        var target = editor.AddNodeAt("End", 400, 100);
        Assert.IsTrue(editor.Connect(branch.Id, "hand_over", target.Id));
        var plan = editor.AnalyzeDialogueExitNames(branch, ["conceal"]);

        Assert.IsTrue(editor.ApplyDialogueExitNameChange(plan, DialogueExitOutputDisposition.Delete));
        Assert.IsNull(editor.GetConnection(branch.Id, "hand_over"));
        Assert.IsFalse(branch.Outputs.Contains("hand_over", StringComparer.Ordinal));
        editor.UndoCommand.Execute(null);
        Assert.AreEqual("hand_over", editor.GetConnection(branch.Id, "hand_over")!.Output);
        editor.RedoCommand.Execute(null);
        Assert.IsNull(editor.GetConnection(branch.Id, "hand_over"));
    }

    [TestMethod]
    public void SequenceStepsSortLegacyOutputsAndProtectConnectedLastStep()
    {
        var resource = new StoryResource
        {
            Id = "sequence_dynamic", Entry = "sequence",
            Nodes =
            [
                new StoryNodeResource { Id = "sequence", Type = "sequence", Properties = StringProperties(("step_count", "3")) },
                new StoryNodeResource { Id = "end1", Type = "end" },
                new StoryNodeResource { Id = "end2", Type = "end" },
            ],
            Connections =
            [
                new StoryConnectionResource { From = "sequence", Output = "10", To = "end1" },
                new StoryConnectionResource { From = "sequence", Output = "2", To = "end2" },
            ],
        };
        var editor = new StoryFlowEditorViewModel(StoryDocument.FromResource(resource), [], [], [], ["sequence_dynamic"]);
        var sequence = editor.Nodes.Single(node => node.Id == "sequence");
        CollectionAssert.AreEqual(new[] { "1", "2", "3", "10" }, sequence.Outputs.ToArray());
        Assert.AreEqual("11", editor.GetNextSequenceOutput(sequence));
        Assert.IsFalse(editor.RemoveSequenceStep(sequence));
        Assert.IsTrue(editor.RemoveSequenceStep(sequence, allowConnectedRemoval: true));
        Assert.IsFalse(editor.Connections.Any(connection => connection.Output == "10"));
        editor.UndoCommand.Execute(null);
        Assert.IsTrue(editor.Connections.Any(connection => connection.Output == "10"));
        editor.RedoCommand.Execute(null);
        Assert.IsFalse(editor.Connections.Any(connection => connection.Output == "10"));
    }

    [TestMethod]
    public void SequenceAddExposesAndPersistsNextNumericOutput()
    {
        var editor = new StoryFlowEditorViewModel(StoryDocument.CreateNew("sequence_add"), [], [], [], ["sequence_add"]);
        var sequence = editor.AddNodeAt("Sequence", 100, 100);
        Assert.AreEqual("2", editor.GetNextSequenceOutput(sequence));
        var plan = editor.AnalyzeSequenceStepAddition(sequence);
        Assert.IsTrue(editor.AddSequenceStep(plan));
        CollectionAssert.AreEqual(new[] { "1", "2" }, sequence.Outputs.ToArray());
        editor.UndoCommand.Execute(null);
        CollectionAssert.AreEqual(new[] { "1" }, editor.Nodes.Single(node => node.Id == sequence.Id).Outputs.ToArray());
        editor.RedoCommand.Execute(null);
        CollectionAssert.AreEqual(new[] { "1", "2" }, editor.Nodes.Single(node => node.Id == sequence.Id).Outputs.ToArray());
    }

    [TestMethod]
    public void SequenceUnconnectedSecondStepCanBeRemovedAndUndone()
    {
        var editor = new StoryFlowEditorViewModel(StoryDocument.CreateNew("sequence_remove_second"), [], [], [], ["sequence_remove_second"]);
        var sequence = editor.AddNodeAt("Sequence", 100, 100);
        Assert.IsTrue(editor.AddSequenceStep(sequence));

        var removal = editor.AnalyzeSequenceStepRemoval(sequence);
        Assert.IsTrue(removal.IsValid);
        Assert.AreEqual("2", removal.Output);
        Assert.IsTrue(editor.RemoveSequenceStep(removal));
        CollectionAssert.AreEqual(new[] { "1" }, sequence.Outputs.ToArray());

        editor.UndoCommand.Execute(null);
        CollectionAssert.AreEqual(new[] { "1", "2" }, editor.Nodes.Single(node => node.Id == sequence.Id).Outputs.ToArray());
    }

    [TestMethod]
    public void PropertyEditorsClassifyKnownAndUnknownPropertiesAndExcludeSpecializedRows()
    {
        var resource = new StoryResource
        {
            Id = "property_editors", Entry = "item",
            Nodes =
            [
                new StoryNodeResource { Id = "item", Type = "has_item", Properties = StringProperties(
                    ("amount", "3"), ("unknown_z", "z"), ("item", "diamond"), ("metadata", "7"), ("unknown_a", "a")) },
                new StoryNodeResource { Id = "branch", Type = "dialogue_exit_branch", Properties = StringProperties(("exit_names", "accept, conceal"), ("compat", "yes")) },
                new StoryNodeResource { Id = "sequence", Type = "sequence", Properties = StringProperties(("stepCount", "2"), ("count", "2"), ("step_count", "2"), ("legacy", "kept")) },
            ],
        };
        var editor = new StoryFlowEditorViewModel(StoryDocument.FromResource(resource), [], [], [], ["property_editors"]);

        var item = editor.Nodes.Single(node => node.Id == "item");
        CollectionAssert.AreEqual(new[] { "item", "amount" }, item.CoreProperties.Select(property => property.Key).ToArray());
        CollectionAssert.AreEqual(new[] { "metadata", "unknown_a", "unknown_z" }, item.AdvancedProperties.Select(property => property.Key).ToArray());
        Assert.AreEqual("物品", item.CoreProperties[0].Label);
        Assert.IsTrue(item.AdvancedProperties[0].IsKnown);
        Assert.IsFalse(item.AdvancedProperties[1].IsKnown);
        Assert.AreEqual("7", item.AdvancedProperties[0].Value);
        Assert.AreEqual("a", item.AdvancedProperties[1].Value);

        var branch = editor.Nodes.Single(node => node.Id == "branch");
        Assert.IsFalse(branch.PropertyEditors.Any(property => property.Key == "exit_names"));
        Assert.AreEqual("yes", branch.AdvancedProperties.Single(property => property.Key == "compat").Value);
        var sequence = editor.Nodes.Single(node => node.Id == "sequence");
        Assert.IsFalse(sequence.PropertyEditors.Any(property => property.Key is "step_count" or "stepCount" or "count"));
        Assert.AreEqual("kept", sequence.AdvancedProperties.Single(property => property.Key == "legacy").Value);
    }

    [TestMethod]
    public void PropertyEditorValueUsesOneUndoRedoEditAndPersistsAfterRebuild()
    {
        var document = StoryDocument.FromResource(new StoryResource
        {
            Id = "property_edit", Entry = "item",
            Nodes = [new StoryNodeResource { Id = "item", Type = "has_item", Properties = StringProperties(("item", "iron"), ("amount", "1")) }],
        });
        var editor = new StoryFlowEditorViewModel(document, [], [], [], ["property_edit"]);
        var node = editor.Nodes.Single();
        var amount = node.CoreProperties.Single(property => property.Key == "amount");

        amount.Value = "4";
        Assert.AreEqual("4", node.GetProperty("amount"));
        Assert.AreEqual("4", document.ToResource().Nodes.Single().Properties["amount"].GetString());
        editor.UndoCommand.Execute(null);
        Assert.AreEqual("1", editor.Nodes.Single().GetProperty("amount"));
        editor.RedoCommand.Execute(null);
        Assert.AreEqual("4", editor.Nodes.Single().GetProperty("amount"));
        Assert.AreSame(node, editor.Nodes.Single());
        Assert.AreEqual("4", editor.Nodes.Single().CoreProperties.Single(property => property.Key == "amount").Value);
    }

    [TestMethod]
    public void UndoRedoPreservesExistingNodeEditorIdentity()
    {
        var editor = new StoryFlowEditorViewModel(StoryDocument.CreateNew("identity"), [], [], [], ["identity"]);
        var start = editor.Nodes.Single(node => node.CanonicalType == "StoryStart");
        var unaffected = editor.AddNodeAt("End", 500, 100);
        editor.SelectOnly(start);

        editor.MoveSelection(new Dictionary<string, (double X, double Y)> { [start.Id] = (240, 180) });
        editor.UndoCommand.Execute(null);
        Assert.AreSame(start, editor.Nodes.Single(node => node.Id == start.Id));
        Assert.AreSame(unaffected, editor.Nodes.Single(node => node.Id == unaffected.Id));

        editor.RedoCommand.Execute(null);
        Assert.AreSame(start, editor.Nodes.Single(node => node.Id == start.Id));
        Assert.AreSame(unaffected, editor.Nodes.Single(node => node.Id == unaffected.Id));
    }

    [TestMethod]
    public void ReferenceProblemsExposeNodeCoordinatesAndRequestFocusSelectsNode()
    {
        var resource = new StoryResource
        {
            Id = "problem_focus", Entry = "dialogue",
            Nodes = [new StoryNodeResource { Id = "dialogue", Type = "play_dialogue", Properties = StringProperties(("dialogue_id", "missing")) }],
        };
        var editor = new StoryFlowEditorViewModel(StoryDocument.FromResource(resource), [], [], [], ["problem_focus"]);

        var issue = editor.ValidationIssues.Single(candidate => candidate.Code == "story.flow.dialogue.missing");
        Assert.AreEqual("dialogue", issue.NodeId);
        Assert.AreEqual("dialogue_id", issue.Field);
        Assert.IsTrue(editor.RequestProblemFocus(issue.NodeId!, issue.Field));
        Assert.AreEqual("dialogue", editor.SelectedNode?.Id);
        Assert.AreEqual("dialogue_id", editor.ProblemFocusRequest?.Field);
        Assert.IsFalse(editor.RequestProblemFocus("unknown", "dialogue_id"));
    }

    private sealed class M6ProjectDirectory : IDisposable
    {
        public M6ProjectDirectory()
        {
            Root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".m6-test-data", Guid.NewGuid().ToString("N")));
            Directory.CreateDirectory(Root);
        }
        public string Root { get; }
        public void Dispose()
        {
            var root = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, ".m6-test-data"));
            if (!Root.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Unsafe M6 cleanup path.");
            if (Directory.Exists(Root)) Directory.Delete(Root, true);
        }
    }
}
