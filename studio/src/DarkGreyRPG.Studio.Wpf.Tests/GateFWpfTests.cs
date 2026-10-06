using DarkGreyRPG.Studio.ViewModels;
using DarkGreyRPG.Studio.Core.Projects;
using DarkGreyRPG.Studio.Core.Quests;
using DarkGreyRPG.Studio.Core.Stories;
using DarkGreyRPG.Studio.Services;

namespace DarkGreyRPG.Studio.Wpf.Tests;

[TestClass]
// Standalone legacy-model editor tests remain; current shell gates are parameterized in GateEWpfTests.
public sealed class GateFWpfTests
{
    [TestMethod]
    public void QuestEditorStartsEmptyAndFirstObjectiveCreatesMainGroup()
    {
        var document = QuestDocument.CreateNew("quest");
        var editor = new QuestEditorViewModel(document, ["hero"]);

        Assert.IsTrue(editor.IsEmptyState);
        Assert.IsEmpty(editor.Objectives);
        Assert.IsEmpty(document.ObjectiveGroups);

        editor.AddFirstCollectCommand.Execute(null);
        Assert.HasCount(1, editor.Objectives);
        Assert.AreEqual("main", document.ObjectiveGroups.Single().Id);
        Assert.AreEqual("collect", document.ObjectiveGroups.Single().Objectives.Single());
        Assert.AreEqual("ALL", editor.CompletionMode);

        editor.CompletionMode = "SEQUENCE";
        Assert.AreEqual("SEQUENCE", document.ObjectiveGroups.Single().Mode);
        editor.DeleteObjectiveCommand.Execute(null);
        Assert.IsTrue(editor.IsEmptyState);
        Assert.IsEmpty(document.ObjectiveGroups);
    }

    [TestMethod]
    public void InteractWithoutActorsRemainsBlankAndInvalid()
    {
        var document = QuestDocument.CreateNew("quest");
        var editor = new QuestEditorViewModel(document);

        editor.AddFirstInteractCommand.Execute(null);

        Assert.AreEqual(string.Empty, editor.SelectedObjective!.ActorId);
        Assert.IsTrue(document.ValidationErrors.Any(issue => issue.Code == "quest.objective.actor.required"));
        Assert.DoesNotContain("actor", document.ToResource().Objectives.Select(item => item.ActorId));
    }

    [TestMethod]
    public void FirstObjectiveDefaultsAreValidAndNeverUseFakeActor()
    {
        foreach (var item in new[] { "kill", "collect", "interact" })
        {
            var document = QuestDocument.CreateNew($"quest_{item}");
            var editor = new QuestEditorViewModel(document, item == "interact" ? ["hero"] : []);
            if (item == "kill") editor.AddFirstKillCommand.Execute(null);
            if (item == "collect") editor.AddFirstCollectCommand.Execute(null);
            if (item == "interact") editor.AddFirstInteractCommand.Execute(null);

            var objective = editor.SelectedObjective!;
            Assert.AreEqual(item switch { "kill" => "kill_entity", "collect" => "collect_item", _ => "interact_actor" }, objective.Type);
            Assert.AreEqual("main", document.ObjectiveGroups.Single().Id);
            Assert.AreEqual(objective.Id, document.ObjectiveGroups.Single().Objectives.Single());
            Assert.IsGreaterThan(0, objective.Required);
            Assert.AreNotEqual("actor", objective.ActorId);
        }

        var noActor = QuestDocument.CreateNew("quest_no_actor");
        var noActorEditor = new QuestEditorViewModel(noActor);
        noActorEditor.AddFirstInteractCommand.Execute(null);
        Assert.AreEqual(string.Empty, noActorEditor.SelectedObjective!.ActorId);
        Assert.IsTrue(noActor.ValidationErrors.Any(issue => issue.Code == "quest.objective.actor.required"));
    }

    [TestMethod]
    public void CompletionModeEmptyStateAndDeleteUndoRedoPreserveState()
    {
        var document = QuestDocument.CreateNew("quest_modes");
        var editor = new QuestEditorViewModel(document);
        editor.CompletionMode = "SEQUENCE";
        Assert.IsEmpty(document.ObjectiveGroups);
        Assert.AreEqual("SEQUENCE", editor.CompletionMode);
        editor.AddFirstKillCommand.Execute(null);
        foreach (var mode in new[] { "ALL", "ANY", "SEQUENCE" })
        {
            editor.CompletionMode = mode;
            Assert.AreEqual(mode, document.ObjectiveGroups.Single().Mode);
        }
        editor.AddFirstCollectCommand.Execute(null);
        _ = editor.SelectedObjective!.Id;
        editor.DeleteObjectiveCommand.Execute(null);
        editor.DeleteObjectiveCommand.Execute(null);
        Assert.IsEmpty(editor.Objectives);
        Assert.IsEmpty(document.ObjectiveGroups);
        editor.UndoCommand.Execute(null);
        Assert.AreEqual("kill", editor.SelectedObjective!.Id);
        Assert.AreEqual("SEQUENCE", document.ObjectiveGroups.Single().Mode);
        editor.RedoCommand.Execute(null);
        Assert.IsEmpty(editor.Objectives);
        Assert.IsEmpty(document.ObjectiveGroups);
    }

}
